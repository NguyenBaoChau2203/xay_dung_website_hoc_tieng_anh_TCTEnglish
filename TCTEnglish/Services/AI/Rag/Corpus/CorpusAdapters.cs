using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Rag.Corpus;

public sealed class GuideCorpusAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Reviewed against docs/feature-audits/hybrid-rag/phase-0/coverage-matrix.md.
    /// study-read claims a Read mode that is not in the study switcher.
    /// ai-chatbox-usage claims the assistant already answers from live learner data;
    /// that claim was an explicit discovery gap and is not rewritten here.
    /// </summary>
    private static readonly HashSet<string> ExcludedIds = new(StringComparer.Ordinal)
    {
        "study-read",
        "ai-chatbox-usage"
    };

    private static readonly HashSet<string> ReviewedIds = new(StringComparer.Ordinal)
    {
        "create-set",
        "add-card",
        "edit-delete-set",
        "create-folder",
        "study-flashcard",
        "study-quiz",
        "study-write",
        "study-matching",
        "create-class",
        "join-class",
        "share-folder-to-class",
        "leave-class",
        "speaking-playlist",
        "speaking-practice",
        "set-daily-goal",
        "view-streak",
        "account-profile",
        "change-password",
        "login-google-facebook",
        "search-set",
        "reading-quick-action",
        "writing-quick-action",
        "listening-quick-action",
        "reading-practice",
        "writing-practice",
        "listening-practice",
        "notifications",
        "site-overview-navigation",
        "goals-overview",
        "daily-challenge",
        "class-chat",
        "organize-sets-in-folders",
        "premium-pricing",
        "payment-history",
        "premium-feature-access",
        "grammar-page",
        "privacy-terms",
        "admin-management-overview",
        "contact-support"
    };

    private static readonly HashSet<string> PublicIds = new(StringComparer.Ordinal)
    {
        "login-google-facebook",
        "grammar-page",
        "privacy-terms",
        "contact-support",
        "writing-quick-action",
        "writing-practice",
        "listening-quick-action",
        "listening-practice"
    };

    private readonly string _guideFilePath;

    public GuideCorpusAdapter(string guideFilePath)
    {
        _guideFilePath = guideFilePath;
    }

    public async Task<CorpusCollection> CollectAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var collection = new CorpusCollection();
        if (!File.Exists(_guideFilePath))
        {
            collection.Exclusions.Add(new CorpusExclusion("guide:*", "guide-file-missing"));
            return collection;
        }

        await using var stream = File.OpenRead(_guideFilePath);
        var document = await JsonSerializer.DeserializeAsync<GuideFile>(stream, JsonOptions, ct);
        var version = string.IsNullOrWhiteSpace(document?.Version) ? "unversioned" : document.Version;
        foreach (var guide in document?.Guides ?? [])
        {
            ct.ThrowIfCancellationRequested();
            var id = guide.Id?.Trim() ?? string.Empty;
            var key = string.IsNullOrEmpty(id) ? "guide:" : "guide:" + id;
            if (id.Length == 0 || ExcludedIds.Contains(id) || !ReviewedIds.Contains(id))
            {
                collection.Exclusions.Add(new CorpusExclusion(key, id.Length == 0 ? "missing-id" : "unreviewed-claim"));
                continue;
            }

            var route = guide.Route?.Trim() ?? string.Empty;
            if (route.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
            {
                collection.Exclusions.Add(new CorpusExclusion(key, "admin-route"));
                continue;
            }

            var built = Build(guide, version, route);
            if (built is null)
            {
                collection.Exclusions.Add(new CorpusExclusion(key, "empty-guide"));
                continue;
            }

            collection.Documents.Add(built);
        }

        return collection;
    }

    public async Task<CorpusDocument?> ReadAsync(string sourceKey, CancellationToken ct)
    {
        var collection = await CollectAsync(ct);
        return collection.Documents.FirstOrDefault(document => document.SourceKey == sourceKey);
    }

    private static CorpusDocument? Build(GuideEntry guide, string fileVersion, string route)
    {
        var id = guide.Id!.Trim();
        var title = guide.Title ?? string.Empty;
        var body = guide.Body ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var keywords = guide.Keywords ?? [];
        var visibility = PublicIds.Contains(id) ? "public" : "authenticated";
        var sourceKey = "guide:" + id;
        var hash = CorpusHash.Sha256(CorpusHash.Canonical(sourceKey, visibility, route, title, keywords, body));
        return new CorpusDocument(
            "guide",
            sourceKey,
            id,
            title,
            keywords,
            body,
            route,
            visibility,
            hash,
            fileVersion + ":" + id + ":" + hash);
    }

    private sealed class GuideFile
    {
        [System.Text.Json.Serialization.JsonPropertyName("_version")]
        public string? Version { get; set; }

        public List<GuideEntry> Guides { get; set; } = [];
    }

    private sealed class GuideEntry
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public List<string>? Keywords { get; set; }
        public string? Body { get; set; }
        public string? Route { get; set; }
        public string? Topic { get; set; }
    }
}

public sealed class LessonCorpusAdapter
{
    private readonly DbflashcardContext _context;

    public LessonCorpusAdapter(DbflashcardContext context)
    {
        _context = context;
    }

    public async Task<CorpusCollection> CollectAsync(CancellationToken ct)
    {
        var collection = new CorpusCollection();
        await AddReadingAsync(collection, ct);
        await AddWritingAsync(collection, ct);
        await AddListeningAsync(collection, ct);
        await AddSpeakingAsync(collection, ct);
        return collection;
    }

    public Task<CorpusDocument?> ReadAsync(string sourceKey, CancellationToken ct)
    {
        if (sourceKey.StartsWith("reading:", StringComparison.Ordinal) && int.TryParse(sourceKey["reading:".Length..], out var readingId))
        {
            return ReadReadingAsync(readingId, ct);
        }

        if (sourceKey.StartsWith("writing:", StringComparison.Ordinal) && int.TryParse(sourceKey["writing:".Length..], out var writingId))
        {
            return ReadWritingAsync(writingId, ct);
        }

        if (sourceKey.StartsWith("listening:", StringComparison.Ordinal) && int.TryParse(sourceKey["listening:".Length..], out var listeningId))
        {
            return ReadListeningAsync(listeningId, ct);
        }

        if (sourceKey.StartsWith("speaking-sentence:", StringComparison.Ordinal) && int.TryParse(sourceKey["speaking-sentence:".Length..], out var sentenceId))
        {
            return ReadSpeakingAsync(sentenceId, ct);
        }

        return Task.FromResult<CorpusDocument?>(null);
    }

    private async Task AddReadingAsync(CorpusCollection collection, CancellationToken ct)
    {
        var unpublished = await _context.ReadingPassages.AsNoTracking()
            .Where(passage => !passage.IsPublished)
            .OrderBy(passage => passage.Id)
            .Take(CorpusVersions.MaxSourceRows)
            .Select(passage => passage.Id)
            .ToListAsync(ct);
        collection.Exclusions.AddRange(unpublished.Select(id => new CorpusExclusion($"reading:{id}", "unpublished")));
        var rows = await _context.ReadingPassages.AsNoTracking()
            .Where(passage => passage.IsPublished)
            .OrderBy(passage => passage.Id)
            .Take(CorpusVersions.MaxSourceRows + 1)
            .Select(passage => new { passage.Id, passage.Title, passage.Content, passage.Level, passage.Topic })
            .ToListAsync(ct);
        if (rows.Count > CorpusVersions.MaxSourceRows)
        {
            collection.Truncated = true;
            rows = rows.Take(CorpusVersions.MaxSourceRows).ToList();
        }

        foreach (var row in rows)
        {
            var document = CreateDocument("reading", $"reading:{row.Id}", row.Id.ToString(), row.Title, Keywords(row.Level, row.Topic), row.Content, $"/Reading/Study/{row.Id}", "authenticated");
            AddOrExclude(collection, document, $"reading:{row.Id}");
        }
    }

    private async Task AddWritingAsync(CorpusCollection collection, CancellationToken ct)
    {
        var rejected = await _context.WritingExercises.AsNoTracking()
            .Where(exercise => exercise.UserId != null || !exercise.IsPublished)
            .OrderBy(exercise => exercise.Id)
            .Take(CorpusVersions.MaxSourceRows)
            .Select(exercise => new { exercise.Id, exercise.UserId, exercise.IsPublished })
            .ToListAsync(ct);
        foreach (var row in rejected)
        {
            var reason = row.UserId != null ? "private-owner" : "unpublished";
            collection.Exclusions.Add(new CorpusExclusion($"writing:{row.Id}", reason));
        }

        var rows = await _context.WritingExercises.AsNoTracking()
            .Where(exercise => exercise.UserId == null && exercise.IsPublished)
            .OrderBy(exercise => exercise.Id)
            .Take(CorpusVersions.MaxSourceRows + 1)
            .Select(exercise => new
            {
                exercise.Id,
                exercise.Title,
                exercise.Level,
                exercise.Topic,
                exercise.ContentType,
                exercise.PreviewText,
                Sentences = exercise.WritingExerciseSentences
                    .OrderBy(sentence => sentence.SortOrder)
                    .Select(sentence => new { sentence.VietnameseText, sentence.EnglishMeaning })
                    .ToList()
            })
            .ToListAsync(ct);
        if (rows.Count > CorpusVersions.MaxSourceRows)
        {
            collection.Truncated = true;
            rows = rows.Take(CorpusVersions.MaxSourceRows).ToList();
        }

        foreach (var row in rows)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(row.PreviewText))
            {
                parts.Add(row.PreviewText);
            }

            foreach (var sentence in row.Sentences)
            {
                parts.Add(sentence.VietnameseText + "\n" + sentence.EnglishMeaning);
            }

            var document = CreateDocument("writing", $"writing:{row.Id}", row.Id.ToString(), row.Title, Keywords(row.Level, row.Topic, row.ContentType), string.Join("\n\n", parts), $"/Home/Writing/Practice?exerciseId={row.Id}", "authenticated");
            AddOrExclude(collection, document, $"writing:{row.Id}");
        }
    }

    private async Task AddListeningAsync(CorpusCollection collection, CancellationToken ct)
    {
        var rejected = await _context.ListeningLessons.AsNoTracking()
            .Where(lesson => lesson.OwnerUserId != null || !lesson.IsPublished)
            .OrderBy(lesson => lesson.Id)
            .Take(CorpusVersions.MaxSourceRows)
            .Select(lesson => new { lesson.Id, lesson.OwnerUserId, lesson.IsPublished })
            .ToListAsync(ct);
        foreach (var row in rejected)
        {
            collection.Exclusions.Add(new CorpusExclusion($"listening:{row.Id}", row.OwnerUserId != null ? "private-owner" : "unpublished"));
        }

        var rows = await _context.ListeningLessons.AsNoTracking()
            .Where(lesson => lesson.OwnerUserId == null && lesson.IsPublished)
            .OrderBy(lesson => lesson.Id)
            .Take(CorpusVersions.MaxSourceRows + 1)
            .Select(lesson => new
            {
                lesson.Id,
                lesson.Title,
                lesson.Level,
                lesson.Topic,
                Lines = lesson.TranscriptLines
                    .OrderBy(line => line.OrderIndex)
                    .Select(line => new { line.Text, line.VietnameseMeaning })
                    .ToList()
            })
            .ToListAsync(ct);
        if (rows.Count > CorpusVersions.MaxSourceRows)
        {
            collection.Truncated = true;
            rows = rows.Take(CorpusVersions.MaxSourceRows).ToList();
        }

        foreach (var row in rows)
        {
            var parts = row.Lines.Select(line => string.IsNullOrWhiteSpace(line.VietnameseMeaning) ? line.Text : line.Text + "\n" + line.VietnameseMeaning);
            var document = CreateDocument("listening", $"listening:{row.Id}", row.Id.ToString(), row.Title, Keywords(row.Level, row.Topic), string.Join("\n\n", parts), $"/Home/Listening/Practice/{row.Id}", "public");
            AddOrExclude(collection, document, $"listening:{row.Id}");
        }
    }

    private async Task AddSpeakingAsync(CorpusCollection collection, CancellationToken ct)
    {
        var rejected = await _context.SpeakingVideos.AsNoTracking()
            .Where(video => video.OwnerUserId != null || video.ImportStatus.ToLower() != "ready")
            .OrderBy(video => video.Id)
            .Take(CorpusVersions.MaxSourceRows)
            .Select(video => new { video.Id, video.OwnerUserId, video.ImportStatus })
            .ToListAsync(ct);
        foreach (var row in rejected)
        {
            collection.Exclusions.Add(new CorpusExclusion($"speaking:{row.Id}", row.OwnerUserId != null ? "private-owner" : "draft"));
        }

        var rows = await _context.SpeakingVideos.AsNoTracking()
            .Where(video => video.OwnerUserId == null && video.ImportStatus.ToLower() == "ready")
            .OrderBy(video => video.Id)
            .Take(CorpusVersions.MaxSourceRows + 1)
            .Select(video => new
            {
                video.Id,
                video.Title,
                video.Level,
                video.Topic,
                Sentences = video.SpeakingSentences
                    .OrderBy(sentence => sentence.StartTime)
                    .ThenBy(sentence => sentence.Id)
                    .Select(sentence => new { sentence.Id, sentence.Text, sentence.VietnameseMeaning })
                    .ToList()
            })
            .ToListAsync(ct);
        if (rows.Count > CorpusVersions.MaxSourceRows)
        {
            collection.Truncated = true;
            rows = rows.Take(CorpusVersions.MaxSourceRows).ToList();
        }

        foreach (var row in rows)
        {
            if (row.Sentences.Count == 0)
            {
                collection.Exclusions.Add(new CorpusExclusion($"speaking:{row.Id}", "empty-text"));
                continue;
            }

            foreach (var sentence in row.Sentences)
            {
                var body = string.IsNullOrWhiteSpace(sentence.VietnameseMeaning) ? sentence.Text : sentence.Text + "\n" + sentence.VietnameseMeaning;
                var key = $"speaking-sentence:{sentence.Id}";
                var document = CreateDocument("speaking", key, sentence.Id.ToString(), row.Title, Keywords(row.Level, row.Topic), body, $"/Speaking/Practice/{row.Id}", "authenticated");
                AddOrExclude(collection, document, key);
            }
        }
    }

    private async Task<CorpusDocument?> ReadReadingAsync(int id, CancellationToken ct)
    {
        var row = await _context.ReadingPassages.AsNoTracking()
            .Where(passage => passage.Id == id && passage.IsPublished)
            .Select(passage => new { passage.Id, passage.Title, passage.Content, passage.Level, passage.Topic })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : CreateDocument("reading", $"reading:{row.Id}", row.Id.ToString(), row.Title, Keywords(row.Level, row.Topic), row.Content, $"/Reading/Study/{row.Id}", "authenticated");
    }

    private async Task<CorpusDocument?> ReadWritingAsync(int id, CancellationToken ct)
    {
        var row = await _context.WritingExercises.AsNoTracking()
            .Where(exercise => exercise.Id == id && exercise.UserId == null && exercise.IsPublished)
            .Select(exercise => new
            {
                exercise.Id,
                exercise.Title,
                exercise.Level,
                exercise.Topic,
                exercise.ContentType,
                exercise.PreviewText,
                Sentences = exercise.WritingExerciseSentences
                    .OrderBy(sentence => sentence.SortOrder)
                    .Select(sentence => new { sentence.VietnameseText, sentence.EnglishMeaning })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(row.PreviewText))
        {
            parts.Add(row.PreviewText);
        }

        foreach (var sentence in row.Sentences)
        {
            parts.Add(sentence.VietnameseText + "\n" + sentence.EnglishMeaning);
        }

        return CreateDocument("writing", $"writing:{row.Id}", row.Id.ToString(), row.Title, Keywords(row.Level, row.Topic, row.ContentType), string.Join("\n\n", parts), $"/Home/Writing/Practice?exerciseId={row.Id}", "authenticated");
    }

    private async Task<CorpusDocument?> ReadListeningAsync(int id, CancellationToken ct)
    {
        var row = await _context.ListeningLessons.AsNoTracking()
            .Where(lesson => lesson.Id == id && lesson.OwnerUserId == null && lesson.IsPublished)
            .Select(lesson => new
            {
                lesson.Id,
                lesson.Title,
                lesson.Level,
                lesson.Topic,
                Lines = lesson.TranscriptLines
                    .OrderBy(line => line.OrderIndex)
                    .Select(line => new { line.Text, line.VietnameseMeaning })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var parts = row.Lines.Select(line => string.IsNullOrWhiteSpace(line.VietnameseMeaning) ? line.Text : line.Text + "\n" + line.VietnameseMeaning);
        return CreateDocument("listening", $"listening:{row.Id}", row.Id.ToString(), row.Title, Keywords(row.Level, row.Topic), string.Join("\n\n", parts), $"/Home/Listening/Practice/{row.Id}", "public");
    }

    private async Task<CorpusDocument?> ReadSpeakingAsync(int sentenceId, CancellationToken ct)
    {
        var row = await _context.SpeakingSentences.AsNoTracking()
            .Where(sentence => sentence.Id == sentenceId && sentence.SpeakingVideo.OwnerUserId == null && sentence.SpeakingVideo.ImportStatus.ToLower() == "ready")
            .Select(sentence => new
            {
                sentence.Id,
                sentence.Text,
                sentence.VietnameseMeaning,
                sentence.VideoId,
                sentence.SpeakingVideo.Title,
                sentence.SpeakingVideo.Level,
                sentence.SpeakingVideo.Topic
            })
            .FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var body = string.IsNullOrWhiteSpace(row.VietnameseMeaning) ? row.Text : row.Text + "\n" + row.VietnameseMeaning;
        return CreateDocument("speaking", $"speaking-sentence:{row.Id}", row.Id.ToString(), row.Title, Keywords(row.Level, row.Topic), body, $"/Speaking/Practice/{row.VideoId}", "authenticated");
    }

    private static void AddOrExclude(CorpusCollection collection, CorpusDocument? document, string key)
    {
        if (document is null)
        {
            collection.Exclusions.Add(new CorpusExclusion(key, "empty-text"));
            return;
        }

        if (document.Body.Length > CorpusVersions.MaxBodyCharacters)
        {
            collection.Exclusions.Add(new CorpusExclusion(key, "over-length"));
            return;
        }

        collection.Documents.Add(document);
    }

    private static CorpusDocument? CreateDocument(
        string kind,
        string sourceKey,
        string entityKey,
        string? title,
        IReadOnlyList<string> keywords,
        string? body,
        string route,
        string visibility)
    {
        var safeTitle = title ?? string.Empty;
        var safeBody = body ?? string.Empty;
        if (string.IsNullOrWhiteSpace(safeTitle) && string.IsNullOrWhiteSpace(safeBody))
        {
            return null;
        }

        if (route.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hash = CorpusHash.Sha256(CorpusHash.Canonical(sourceKey, visibility, route, safeTitle, keywords, safeBody));
        return new CorpusDocument(kind, sourceKey, entityKey, safeTitle, keywords, safeBody, route, visibility, hash, kind + ":" + entityKey + ":" + hash);
    }

    private static IReadOnlyList<string> Keywords(params string?[] values)
    {
        return values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
