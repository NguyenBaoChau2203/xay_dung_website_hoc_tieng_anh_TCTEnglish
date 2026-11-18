using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Admin;

/// <summary>
/// Admin rules for listening lessons. System lessons (no owner) are edited here; a Premium learner's own
/// lesson can only be viewed, unpublished and deleted. All validation is server-side and never throws on
/// empty input; saves of lines, quiz and vocabulary are all-or-nothing.
/// </summary>
public sealed partial class AdminListeningService : IAdminListeningService
{
    public const int MaxTitleLength = 255;
    public const int MaxTopicLength = 100;
    public const int MaxUrlLength = 500;
    public const int MaxSpeakerNameLength = 150;
    public const int MaxCountryLength = 100;
    public const int MaxLineSpeakerLength = 100;
    public const int MaxLineTextLength = 1000;
    public const int MaxMeaningLength = 1000;
    public const int MaxLines = 2000;
    public const int MaxQuestionTextLength = 500;
    public const int MaxOptionLength = 500;
    public const int MaxExplanationLength = 1000;
    public const int MaxQuestions = 100;
    public const int MaxVocabWordLength = 200;
    public const int MaxVocabDefinitionLength = 1000;
    public const int MaxVocabExampleLength = 1000;
    public const int MaxVocabItems = 200;

    /// <summary>A question a learner cannot answer: option A or B empty, answer not A-D, or the right answer points at an empty option.</summary>
    private static readonly Expression<Func<ListeningQuizQuestion, bool>> InvalidQuestion = q =>
        q.OptionA == null || q.OptionA.Trim() == "" || q.OptionB == null || q.OptionB.Trim() == ""
        || (q.CorrectAnswer != "A" && q.CorrectAnswer != "B" && q.CorrectAnswer != "C" && q.CorrectAnswer != "D")
        || (q.CorrectAnswer == "C" && (q.OptionC == null || q.OptionC.Trim() == ""))
        || (q.CorrectAnswer == "D" && (q.OptionD == null || q.OptionD.Trim() == ""));

    // One import per lesson at a time, across requests.
    private static readonly ConcurrentDictionary<int, byte> ImportsInProgress = new();

    [GeneratedRegex(@"^\s*\[?([A-Za-z][A-Za-z\s]{0,30}?)\]?\s*:\s*", RegexOptions.CultureInvariant)]
    private static partial Regex SpeakerPattern();

    private readonly DbflashcardContext _db;
    private readonly IYoutubeTranscriptService _transcripts;
    private readonly ILogger<AdminListeningService> _logger;

    public AdminListeningService(DbflashcardContext db, IYoutubeTranscriptService transcripts, ILogger<AdminListeningService> logger)
    {
        _db = db;
        _transcripts = transcripts;
        _logger = logger;
    }

    // =====================================================================================
    // List, figures, editor
    // =====================================================================================

    public async Task<AdminLessonListResult> ListLessonsAsync(AdminLessonQuery query, CancellationToken ct = default)
    {
        var source = query.Source is "learner" or "all" ? query.Source : "system";
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var badQuizLessonIds = _db.ListeningQuizQuestions.Where(InvalidQuestion).Select(q => q.LessonId);

        var lessons = _db.ListeningLessons.AsNoTracking().AsQueryable();
        lessons = source switch
        {
            "learner" => lessons.Where(l => l.OwnerUserId != null),
            "all" => lessons,
            _ => lessons.Where(l => l.OwnerUserId == null),
        };

        if (AdminInputRules.IsCefrLevel(query.Level))
        {
            var level = query.Level!;
            lessons = lessons.Where(l => l.Level == level);
        }

        lessons = query.Status switch
        {
            "published" => lessons.Where(l => l.IsPublished),
            "draft" => lessons.Where(l => !l.IsPublished),
            "incomplete" => lessons.Where(l => l.TranscriptLines.Count == 0 || badQuizLessonIds.Contains(l.Id)),
            _ => lessons,
        };

        var text = AdminInputRules.NormalizeText(query.Q, 200).Value;
        if (text.Length > 0)
        {
            lessons = lessons.Where(l => l.Title.Contains(text) || l.Topic.Contains(text) || (l.YoutubeId != null && l.YoutubeId.Contains(text)));
        }

        var total = await lessons.CountAsync(ct);
        var totalPages = AdminInputRules.TotalPages(total, pageSize);
        var page = AdminInputRules.ClampPage(query.Page, totalPages);

        var rows = await lessons
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AdminLessonRow
            {
                Id = l.Id,
                Title = l.Title,
                Topic = l.Topic,
                Level = l.Level,
                Duration = l.Duration,
                YoutubeId = l.YoutubeId,
                ThumbnailUrl = l.ThumbnailUrl,
                IsPublished = l.IsPublished,
                IsLearnerOwned = l.OwnerUserId != null,
                OwnerName = l.OwnerUser != null ? (l.OwnerUser.FullName ?? l.OwnerUser.Email) : null,
                TranscriptCount = l.TranscriptLines.Count,
                TimedLineCount = l.TranscriptLines.Count(t => t.StartTime != null && t.EndTime != null),
                QuizCount = l.QuizQuestions.Count,
                VocabCount = l.VocabItems.Count,
                HasInvalidQuiz = badQuizLessonIds.Contains(l.Id),
                CreatedAt = l.CreatedAt,
            })
            .ToListAsync(ct);

        return new AdminLessonListResult
        {
            Rows = rows,
            TotalItems = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Source = source,
        };
    }

    public async Task<AdminLessonFigures> GetFiguresAsync(CancellationToken ct = default)
    {
        var badQuizLessonIds = _db.ListeningQuizQuestions.Where(InvalidQuestion).Select(q => q.LessonId);
        var system = _db.ListeningLessons.AsNoTracking().Where(l => l.OwnerUserId == null);

        var systemCount = await system.CountAsync(ct);
        var published = await system.CountAsync(l => l.IsPublished, ct);
        var unfinished = await system.CountAsync(l => !l.IsPublished || l.TranscriptLines.Count == 0 || badQuizLessonIds.Contains(l.Id), ct);
        var learner = await _db.ListeningLessons.AsNoTracking().CountAsync(l => l.OwnerUserId != null, ct);

        return new AdminLessonFigures(systemCount, published, unfinished, learner);
    }

    public async Task<AdminLessonEditor?> GetEditorAsync(int id, CancellationToken ct = default)
    {
        var lesson = await _db.ListeningLessons.AsNoTracking()
            .Include(l => l.OwnerUser)
            .Include(l => l.TranscriptLines)
            .Include(l => l.QuizQuestions)
            .Include(l => l.VocabItems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lesson == null)
        {
            return null;
        }

        var progress = await _db.UserListeningProgresses.AsNoTracking().CountAsync(p => p.LessonId == id, ct);

        return new AdminLessonEditor
        {
            Id = lesson.Id,
            IsPublished = lesson.IsPublished,
            IsLearnerOwned = lesson.OwnerUserId != null,
            OwnerName = lesson.OwnerUser == null ? null : (string.IsNullOrWhiteSpace(lesson.OwnerUser.FullName) ? lesson.OwnerUser.Email : lesson.OwnerUser.FullName),
            CreatedAt = lesson.CreatedAt,
            LearnerProgressCount = progress,
            Info = new AdminLessonInfoInput
            {
                Title = lesson.Title,
                Level = lesson.Level,
                Topic = lesson.Topic,
                YoutubeId = lesson.YoutubeId,
                AudioUrl = lesson.AudioUrl,
                ThumbnailUrl = lesson.ThumbnailUrl,
                Duration = lesson.Duration,
                Speaker1Name = lesson.Speaker1Name,
                Speaker1Country = lesson.Speaker1Country,
                Speaker2Name = lesson.Speaker2Name,
                Speaker2Country = lesson.Speaker2Country,
            },
            Lines = lesson.TranscriptLines.OrderBy(l => l.OrderIndex).ThenBy(l => l.Id)
                .Select(l => new AdminTranscriptLineInput { Speaker = l.Speaker, Text = l.Text, VietnameseMeaning = l.VietnameseMeaning, StartTime = l.StartTime, EndTime = l.EndTime })
                .ToList(),
            Questions = lesson.QuizQuestions.OrderBy(q => q.OrderIndex).ThenBy(q => q.Id)
                .Select(q => new AdminQuizInput { QuestionText = q.QuestionText, OptionA = q.OptionA, OptionB = q.OptionB, OptionC = q.OptionC, OptionD = q.OptionD, CorrectAnswer = q.CorrectAnswer, Explanation = q.Explanation })
                .ToList(),
            Items = lesson.VocabItems.OrderBy(v => v.OrderIndex).ThenBy(v => v.Id)
                .Select(v => new AdminVocabInput { Word = v.Word, Definition = v.Definition, ExampleSentence = v.ExampleSentence, ImageUrl = v.ImageUrl })
                .ToList(),
        };
    }

    // =====================================================================================
    // Info
    // =====================================================================================

    public async Task<AdminResult<int>> SaveInfoAsync(int? id, AdminLessonInfoInput input, CancellationToken ct = default)
    {
        ListeningLesson? lesson = null;
        if (id.HasValue)
        {
            lesson = await _db.ListeningLessons.FirstOrDefaultAsync(l => l.Id == id.Value, ct);
            if (lesson == null)
            {
                return AdminResult<int>.NotFound();
            }

            if (lesson.OwnerUserId != null)
            {
                return AdminResult<int>.Forbidden(AdminListeningMessages.LearnerLessonReadOnly);
            }
        }

        var errors = new Dictionary<string, string>();
        var clean = CheckInfo(input, errors);
        if (!id.HasValue && input.PublishNow)
        {
            // A brand-new lesson has no transcript yet, so the publish rule can never pass here.
            errors["PublishNow"] = AdminListeningMessages.NoTranscriptToPublish;
        }

        if (errors.Count > 0)
        {
            return AdminResult<int>.Invalid("Hãy kiểm tra lại các trường được đánh dấu.", errors);
        }

        var warnings = new List<string>();
        if (clean.YoutubeId != null)
        {
            var youtube = clean.YoutubeId;
            var currentId = id ?? 0;
            var duplicate = await _db.ListeningLessons.AsNoTracking()
                .AnyAsync(l => l.OwnerUserId == null && l.YoutubeId == youtube && l.Id != currentId, ct);
            if (duplicate)
            {
                warnings.Add(AdminListeningMessages.DuplicateYoutubeWarning);
            }
        }

        if (lesson == null)
        {
            lesson = new ListeningLesson { CreatedAt = DateTime.UtcNow, IsPublished = false, OwnerUserId = null };
            Apply(lesson, clean);
            _db.ListeningLessons.Add(lesson);
        }
        else
        {
            Apply(lesson, clean);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Admin saved the info of listening lesson {LessonId}", lesson.Id);
        return AdminResult<int>.Success(lesson.Id, warnings);
    }

    private sealed record CleanInfo(
        string Title, string Level, string Topic, string? YoutubeId, string? AudioUrl, string? ThumbnailUrl, string? Duration,
        string? Speaker1Name, string? Speaker1Country, string? Speaker2Name, string? Speaker2Country);

    private static CleanInfo CheckInfo(AdminLessonInfoInput input, Dictionary<string, string> errors)
    {
        string Required(string? raw, string field, int max, string label)
        {
            var check = AdminInputRules.NormalizeText(raw, max);
            if (check.IsEmpty)
            {
                errors[field] = $"Hãy nhập {label}.";
            }
            else if (check.TooLong)
            {
                errors[field] = $"{Capitalize(label)} tối đa {max} ký tự.";
            }

            return check.Value;
        }

        string? Optional(string? raw, string field, int max, string label)
        {
            var check = AdminInputRules.NormalizeText(raw, max);
            if (check.TooLong)
            {
                errors[field] = $"{Capitalize(label)} tối đa {max} ký tự.";
            }

            return check.IsEmpty ? null : check.Value;
        }

        string? Url(string? raw, string field, string label)
        {
            var value = raw?.Trim() ?? "";
            if (value.Length == 0)
            {
                return null;
            }

            if (value.Length > MaxUrlLength)
            {
                errors[field] = $"{Capitalize(label)} tối đa {MaxUrlLength} ký tự.";
                return null;
            }

            if (!AdminInputRules.IsSafeMediaUrl(value))
            {
                errors[field] = $"{Capitalize(label)} phải là liên kết https://… hoặc đường dẫn nội bộ bắt đầu bằng /.";
                return null;
            }

            return value;
        }

        var title = Required(input.Title, "Title", MaxTitleLength, "tiêu đề bài nghe");
        var topic = Required(input.Topic, "Topic", MaxTopicLength, "chủ đề");

        var level = (input.Level ?? "").Trim();
        if (!AdminInputRules.IsCefrLevel(level))
        {
            errors["Level"] = "Trình độ phải là A1, A2, B1, B2, C1 hoặc C2.";
        }

        string? youtube = null;
        if (!string.IsNullOrWhiteSpace(input.YoutubeId))
        {
            if (AdminInputRules.TryParseYoutubeId(input.YoutubeId, out var parsed))
            {
                youtube = parsed;
            }
            else
            {
                errors["YoutubeId"] = AdminListeningMessages.YoutubeInvalid;
            }
        }

        var audio = Url(input.AudioUrl, "AudioUrl", "liên kết âm thanh");
        var thumbnail = Url(input.ThumbnailUrl, "ThumbnailUrl", "liên kết ảnh bìa");

        string? duration = null;
        if (!AdminInputRules.TryNormalizeDuration(input.Duration, out duration))
        {
            errors["Duration"] = AdminListeningMessages.DurationInvalid;
        }

        var speaker1 = Optional(input.Speaker1Name, "Speaker1Name", MaxSpeakerNameLength, "tên người nói 1");
        var country1 = Optional(input.Speaker1Country, "Speaker1Country", MaxCountryLength, "quốc gia người nói 1");
        var speaker2 = Optional(input.Speaker2Name, "Speaker2Name", MaxSpeakerNameLength, "tên người nói 2");
        var country2 = Optional(input.Speaker2Country, "Speaker2Country", MaxCountryLength, "quốc gia người nói 2");

        return new CleanInfo(title, level, topic, youtube, audio, thumbnail ?? AdminInputRules.YoutubeThumbnailUrl(youtube), duration, speaker1, country1, speaker2, country2);
    }

    private static void Apply(ListeningLesson lesson, CleanInfo clean)
    {
        lesson.Title = clean.Title;
        lesson.Level = clean.Level;
        lesson.Topic = clean.Topic;
        lesson.YoutubeId = clean.YoutubeId;
        lesson.AudioUrl = clean.AudioUrl;
        lesson.ThumbnailUrl = clean.ThumbnailUrl;
        lesson.Duration = clean.Duration;
        lesson.Speaker1Name = clean.Speaker1Name;
        lesson.Speaker1Country = clean.Speaker1Country;
        lesson.Speaker2Name = clean.Speaker2Name;
        lesson.Speaker2Country = clean.Speaker2Country;
    }

    // =====================================================================================
    // Transcript
    // =====================================================================================

    public async Task<AdminResult> SaveTranscriptAsync(int id, IReadOnlyList<AdminTranscriptLineInput> lines, CancellationToken ct = default)
    {
        var lesson = await _db.ListeningLessons.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new { l.Id, l.IsPublished, l.OwnerUserId })
            .FirstOrDefaultAsync(ct);
        if (lesson == null)
        {
            return AdminResult.NotFound();
        }

        if (lesson.OwnerUserId != null)
        {
            return AdminResult.Forbidden(AdminListeningMessages.LearnerLessonReadOnly);
        }

        var errors = new Dictionary<string, string>();
        var kept = new List<ListeningTranscriptLine>();
        double? lastStart = null;
        var posted = lines ?? Array.Empty<AdminTranscriptLineInput>();

        if (posted.Count > MaxLines)
        {
            errors["Lines"] = $"Lời thoại tối đa {MaxLines} dòng.";
        }

        for (var i = 0; i < posted.Count && posted.Count <= MaxLines; i++)
        {
            var row = posted[i];
            var number = i + 1;
            var speaker = AdminInputRules.NormalizeText(row.Speaker, MaxLineSpeakerLength);
            var text = AdminInputRules.NormalizeText(row.Text, MaxLineTextLength);
            var meaning = AdminInputRules.NormalizeText(row.VietnameseMeaning, MaxMeaningLength);

            if (speaker.IsEmpty && text.IsEmpty && meaning.IsEmpty && row.StartTime == null && row.EndTime == null)
            {
                continue;
            }

            void Add(string field, string message) => errors.TryAdd($"Lines[{i}].{field}", $"Dòng {number}: {message}");

            if (text.IsEmpty)
            {
                Add("Text", "hãy nhập lời thoại.");
            }
            else if (text.TooLong)
            {
                Add("Text", $"lời thoại tối đa {MaxLineTextLength} ký tự.");
            }

            if (speaker.TooLong)
            {
                Add("Speaker", $"tên người nói tối đa {MaxLineSpeakerLength} ký tự.");
            }

            if (meaning.TooLong)
            {
                Add("VietnameseMeaning", $"nghĩa tiếng Việt tối đa {MaxMeaningLength} ký tự.");
            }

            var start = row.StartTime;
            var end = row.EndTime;
            if ((start.HasValue && !double.IsFinite(start.Value)) || (end.HasValue && !double.IsFinite(end.Value)))
            {
                Add("StartTime", "thời điểm không hợp lệ.");
            }
            else if (start.HasValue && !end.HasValue)
            {
                Add("EndTime", "hãy nhập cả thời điểm kết thúc.");
            }
            else if (!start.HasValue && end.HasValue)
            {
                Add("StartTime", "hãy nhập cả thời điểm bắt đầu.");
            }
            else if (start.HasValue && end.HasValue)
            {
                if (start.Value < 0)
                {
                    Add("StartTime", "thời điểm bắt đầu không được âm.");
                }
                else if (end.Value <= start.Value)
                {
                    Add("EndTime", "thời điểm kết thúc phải sau thời điểm bắt đầu.");
                }
            }

            if (start.HasValue && double.IsFinite(start.Value))
            {
                if (lastStart.HasValue && start.Value < lastStart.Value)
                {
                    Add("StartTime", "thời điểm bắt đầu không được sớm hơn dòng trước.");
                }

                lastStart = start.Value;
            }

            kept.Add(new ListeningTranscriptLine
            {
                LessonId = id,
                OrderIndex = kept.Count + 1,
                Speaker = speaker.IsEmpty ? "Speaker 1" : speaker.Value,
                Text = text.Value,
                VietnameseMeaning = meaning.IsEmpty ? null : meaning.Value,
                StartTime = start,
                EndTime = end,
            });
        }

        if (errors.Count > 0)
        {
            return AdminResult.Invalid("Hãy kiểm tra lại các dòng được đánh dấu. Chưa lưu gì cả.", errors);
        }

        if (kept.Count == 0 && lesson.IsPublished)
        {
            return AdminResult.Invalid(AdminListeningMessages.PublishedNeedsTranscript);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.ListeningTranscriptLines.Where(l => l.LessonId == id).ExecuteDeleteAsync(ct);
        _db.ListeningTranscriptLines.AddRange(kept);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        _db.ChangeTracker.Clear();

        _logger.LogInformation("Admin saved {Count} transcript lines of lesson {LessonId}", kept.Count, id);
        return AdminResult.Success();
    }

    public async Task<AdminResult<int>> ImportTranscriptAsync(int id, bool replaceExisting, CancellationToken ct = default)
    {
        var lesson = await _db.ListeningLessons.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lesson == null)
        {
            return AdminResult<int>.NotFound();
        }

        if (lesson.OwnerUserId != null)
        {
            return AdminResult<int>.Forbidden(AdminListeningMessages.LearnerLessonReadOnly);
        }

        if (string.IsNullOrWhiteSpace(lesson.YoutubeId))
        {
            return AdminResult<int>.Invalid(AdminListeningMessages.ImportNeedsYoutube);
        }

        var existing = await _db.ListeningTranscriptLines.CountAsync(l => l.LessonId == id, ct);
        if (existing > 0 && !replaceExisting)
        {
            return AdminResult<int>.Invalid(AdminListeningMessages.ImportReplaceNeeded(existing));
        }

        if (!ImportsInProgress.TryAdd(id, 0))
        {
            return AdminResult<int>.Invalid(AdminListeningMessages.ImportBusy);
        }

        try
        {
            List<SpeakingSentence> sentences;
            string? providerDuration = null;
            try
            {
                sentences = await _transcripts.GetTranscriptAsync(lesson.YoutubeId);
                if (sentences.Count > 0 && string.IsNullOrWhiteSpace(lesson.Duration))
                {
                    providerDuration = await _transcripts.GetVideoDurationAsync(lesson.YoutubeId);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Transcript import failed for listening lesson {LessonId}", id);
                return AdminResult<int>.Invalid(AdminListeningMessages.ImportFailed);
            }

            if (sentences.Count == 0)
            {
                return AdminResult<int>.Invalid(AdminListeningMessages.ImportEmpty);
            }

            var speaker1 = string.IsNullOrWhiteSpace(lesson.Speaker1Name) ? "Speaker 1" : lesson.Speaker1Name.Trim();
            var speaker2 = string.IsNullOrWhiteSpace(lesson.Speaker2Name) ? "Speaker 2" : lesson.Speaker2Name.Trim();
            var imported = new List<ListeningTranscriptLine>();
            foreach (var sentence in sentences)
            {
                var (speaker, text) = DetectSpeaker(sentence.Text, speaker1, speaker2);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                imported.Add(new ListeningTranscriptLine
                {
                    LessonId = id,
                    OrderIndex = imported.Count + 1,
                    Speaker = Truncate(speaker, MaxLineSpeakerLength),
                    Text = text.Trim(),
                    StartTime = sentence.StartTime,
                    EndTime = sentence.EndTime,
                });
            }

            if (imported.Count == 0)
            {
                return AdminResult<int>.Invalid(AdminListeningMessages.ImportEmpty);
            }

            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await _db.ListeningTranscriptLines.Where(l => l.LessonId == id).ExecuteDeleteAsync(ct);
            _db.ListeningTranscriptLines.AddRange(imported);
            if (AdminInputRules.TryNormalizeDuration(providerDuration, out var duration) && duration != null)
            {
                lesson.Duration = duration;
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _db.ChangeTracker.Clear();

            _logger.LogInformation("Admin imported {Count} transcript lines into lesson {LessonId} (replaced {Existing})", imported.Count, id, existing);
            return AdminResult<int>.Success(imported.Count);
        }
        finally
        {
            ImportsInProgress.TryRemove(id, out _);
        }
    }

    /// <summary>"Name: text", "[Name]: text" or "Name: text" at the start of a caption sets the speaker.</summary>
    private static (string Speaker, string Text) DetectSpeaker(string? text, string defaultSpeaker1, string defaultSpeaker2)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (defaultSpeaker1, string.Empty);
        }

        var match = SpeakerPattern().Match(text);
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            var rest = text[match.Length..].Trim();
            if (rest.Length > 0 && name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4)
            {
                return (name, rest);
            }
        }

        _ = defaultSpeaker2; // kept for the day the importer can tell two voices apart
        return (defaultSpeaker1, text);
    }

    // =====================================================================================
    // Quiz and vocabulary
    // =====================================================================================

    public async Task<AdminResult> SaveQuizAsync(int id, IReadOnlyList<AdminQuizInput> questions, CancellationToken ct = default)
    {
        var gate = await CheckEditableAsync(id, ct);
        if (gate != null)
        {
            return gate;
        }

        var errors = new Dictionary<string, string>();
        var kept = new List<ListeningQuizQuestion>();
        var posted = questions ?? Array.Empty<AdminQuizInput>();

        if (posted.Count > MaxQuestions)
        {
            errors["Questions"] = $"Quiz tối đa {MaxQuestions} câu.";
        }

        for (var i = 0; i < posted.Count && posted.Count <= MaxQuestions; i++)
        {
            var row = posted[i];
            var number = i + 1;
            var question = AdminInputRules.NormalizeText(row.QuestionText, MaxQuestionTextLength);
            var a = AdminInputRules.NormalizeText(row.OptionA, MaxOptionLength);
            var b = AdminInputRules.NormalizeText(row.OptionB, MaxOptionLength);
            var c = AdminInputRules.NormalizeText(row.OptionC, MaxOptionLength);
            var d = AdminInputRules.NormalizeText(row.OptionD, MaxOptionLength);
            var explanation = AdminInputRules.NormalizeText(row.Explanation, MaxExplanationLength, singleLine: false);
            var answer = (row.CorrectAnswer ?? "").Trim();

            if (question.IsEmpty && a.IsEmpty && b.IsEmpty && c.IsEmpty && d.IsEmpty && explanation.IsEmpty)
            {
                continue;
            }

            void Add(string field, string message) => errors.TryAdd($"Questions[{i}].{field}", $"Câu {number}: {message}");

            if (question.IsEmpty)
            {
                Add("QuestionText", "hãy nhập nội dung câu hỏi.");
            }
            else if (question.TooLong)
            {
                Add("QuestionText", $"câu hỏi tối đa {MaxQuestionTextLength} ký tự.");
            }

            foreach (var (field, option, label, required) in new (string, TextCheck, string, bool)[]
            {
                ("OptionA", a, "A", true), ("OptionB", b, "B", true), ("OptionC", c, "C", false), ("OptionD", d, "D", false),
            })
            {
                if (required && option.IsEmpty)
                {
                    Add(field, $"phương án {label} không được để trống.");
                }
                else if (option.TooLong)
                {
                    Add(field, $"phương án {label} tối đa {MaxOptionLength} ký tự.");
                }
            }

            if (!d.IsEmpty && c.IsEmpty)
            {
                Add("OptionD", "chỉ nhập phương án D khi đã có phương án C.");
            }

            if (explanation.TooLong)
            {
                Add("Explanation", $"giải thích tối đa {MaxExplanationLength} ký tự.");
            }

            if (answer is not ("A" or "B" or "C" or "D"))
            {
                Add("CorrectAnswer", "hãy chọn đáp án đúng (A, B, C hoặc D).");
            }
            else
            {
                var chosen = answer switch { "A" => a, "B" => b, "C" => c, _ => d };
                if (chosen.IsEmpty)
                {
                    Add("CorrectAnswer", $"đáp án đúng {answer} đang để trống.");
                }
            }

            kept.Add(new ListeningQuizQuestion
            {
                LessonId = id,
                OrderIndex = kept.Count + 1,
                QuestionText = question.Value,
                OptionA = a.Value,
                OptionB = b.Value,
                OptionC = c.IsEmpty ? null : c.Value,
                OptionD = d.IsEmpty ? null : d.Value,
                CorrectAnswer = answer,
                Explanation = explanation.IsEmpty ? null : explanation.Value,
            });
        }

        if (errors.Count > 0)
        {
            return AdminResult.Invalid("Hãy kiểm tra lại các câu hỏi được đánh dấu. Chưa lưu gì cả.", errors);
        }

        // UserListeningProgress only stores flags and a score, so replacing the questions cannot orphan it.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.ListeningQuizQuestions.Where(q => q.LessonId == id).ExecuteDeleteAsync(ct);
        _db.ListeningQuizQuestions.AddRange(kept);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        _db.ChangeTracker.Clear();

        _logger.LogInformation("Admin saved {Count} quiz questions of lesson {LessonId}", kept.Count, id);
        return AdminResult.Success();
    }

    public async Task<AdminResult> SaveVocabAsync(int id, IReadOnlyList<AdminVocabInput> items, CancellationToken ct = default)
    {
        var gate = await CheckEditableAsync(id, ct);
        if (gate != null)
        {
            return gate;
        }

        var errors = new Dictionary<string, string>();
        var kept = new List<ListeningVocabItem>();
        var posted = items ?? Array.Empty<AdminVocabInput>();

        if (posted.Count > MaxVocabItems)
        {
            errors["Items"] = $"Từ vựng tối đa {MaxVocabItems} mục.";
        }

        for (var i = 0; i < posted.Count && posted.Count <= MaxVocabItems; i++)
        {
            var row = posted[i];
            var number = i + 1;
            var word = AdminInputRules.NormalizeText(row.Word, MaxVocabWordLength);
            var definition = AdminInputRules.NormalizeText(row.Definition, MaxVocabDefinitionLength);
            var example = AdminInputRules.NormalizeText(row.ExampleSentence, MaxVocabExampleLength);
            var image = row.ImageUrl?.Trim() ?? "";

            if (word.IsEmpty && definition.IsEmpty && example.IsEmpty && image.Length == 0)
            {
                continue;
            }

            void Add(string field, string message) => errors.TryAdd($"Items[{i}].{field}", $"Từ {number}: {message}");

            if (word.IsEmpty)
            {
                Add("Word", "hãy nhập từ.");
            }
            else if (word.TooLong)
            {
                Add("Word", $"từ tối đa {MaxVocabWordLength} ký tự.");
            }

            if (definition.IsEmpty)
            {
                Add("Definition", "hãy nhập nghĩa.");
            }
            else if (definition.TooLong)
            {
                Add("Definition", $"nghĩa tối đa {MaxVocabDefinitionLength} ký tự.");
            }

            if (example.TooLong)
            {
                Add("ExampleSentence", $"ví dụ tối đa {MaxVocabExampleLength} ký tự.");
            }

            if (image.Length > MaxUrlLength)
            {
                Add("ImageUrl", $"liên kết ảnh tối đa {MaxUrlLength} ký tự.");
            }
            else if (!AdminInputRules.IsSafeImageUrl(image))
            {
                Add("ImageUrl", "ảnh phải là liên kết https://… hoặc đường dẫn nội bộ bắt đầu bằng /.");
            }

            kept.Add(new ListeningVocabItem
            {
                LessonId = id,
                OrderIndex = kept.Count + 1,
                Word = word.Value,
                Definition = definition.Value,
                ExampleSentence = example.IsEmpty ? null : example.Value,
                ImageUrl = image.Length == 0 ? null : image,
            });
        }

        if (errors.Count > 0)
        {
            return AdminResult.Invalid("Hãy kiểm tra lại các từ được đánh dấu. Chưa lưu gì cả.", errors);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.ListeningVocabItems.Where(v => v.LessonId == id).ExecuteDeleteAsync(ct);
        _db.ListeningVocabItems.AddRange(kept);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        _db.ChangeTracker.Clear();

        _logger.LogInformation("Admin saved {Count} vocabulary items of lesson {LessonId}", kept.Count, id);
        return AdminResult.Success();
    }

    // =====================================================================================
    // Publish and delete
    // =====================================================================================

    public async Task<AdminResult> SetPublishedAsync(int id, bool published, CancellationToken ct = default)
    {
        var lesson = await _db.ListeningLessons.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lesson == null)
        {
            return AdminResult.NotFound();
        }

        if (!published)
        {
            if (lesson.IsPublished)
            {
                lesson.IsPublished = false;
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Admin unpublished lesson {LessonId}", id);
            }

            return AdminResult.Success();
        }

        if (lesson.OwnerUserId != null)
        {
            return AdminResult.Forbidden(AdminListeningMessages.LearnerCannotBePublished);
        }

        var lineCount = await _db.ListeningTranscriptLines.CountAsync(l => l.LessonId == id, ct);
        if (lineCount == 0)
        {
            return AdminResult.Invalid(AdminListeningMessages.NoTranscriptToPublish);
        }

        var warnings = new List<string>();
        if (!await _db.ListeningQuizQuestions.AnyAsync(q => q.LessonId == id, ct))
        {
            warnings.Add(AdminListeningMessages.NoQuizWarning);
        }

        if (!await _db.ListeningVocabItems.AnyAsync(v => v.LessonId == id, ct))
        {
            warnings.Add(AdminListeningMessages.NoVocabWarning);
        }

        if (!string.IsNullOrWhiteSpace(lesson.YoutubeId)
            && !await _db.ListeningTranscriptLines.AnyAsync(l => l.LessonId == id && l.StartTime != null && l.EndTime != null, ct))
        {
            warnings.Add(AdminListeningMessages.NoTimedLinesWarning);
        }

        lesson.IsPublished = true;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Admin published lesson {LessonId} with {Warnings} warnings", id, warnings.Count);
        return AdminResult.Success(warnings);
    }

    public async Task<AdminResult> DeleteAsync(int id, string? confirmTitle, int adminId, CancellationToken ct = default)
    {
        var lesson = await _db.ListeningLessons.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new { l.Id, l.Title, l.OwnerUserId })
            .FirstOrDefaultAsync(ct);
        if (lesson == null)
        {
            return AdminResult.NotFound();
        }

        if (confirmTitle == null || !string.Equals(confirmTitle.Trim(), lesson.Title.Trim(), StringComparison.Ordinal))
        {
            return AdminResult.Invalid(AdminListeningMessages.ConfirmTitleMismatch);
        }

        var progress = await _db.UserListeningProgresses.AsNoTracking().CountAsync(p => p.LessonId == id, ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.UserListeningProgresses.Where(p => p.LessonId == id).ExecuteDeleteAsync(ct);
        await _db.ListeningQuizQuestions.Where(q => q.LessonId == id).ExecuteDeleteAsync(ct);
        await _db.ListeningVocabItems.Where(v => v.LessonId == id).ExecuteDeleteAsync(ct);
        await _db.ListeningTranscriptLines.Where(l => l.LessonId == id).ExecuteDeleteAsync(ct);
        await _db.ListeningLessons.Where(l => l.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        _db.ChangeTracker.Clear();

        _logger.LogInformation("Admin {AdminId} deleted listening lesson {LessonId} (owner {OwnerId}, {Progress} learner progress rows)", adminId, id, lesson.OwnerUserId, progress);
        return AdminResult.Success();
    }

    // =====================================================================================
    // Helpers
    // =====================================================================================

    /// <summary>NotFound for a missing lesson and Forbidden for a learner's own lesson; null when the admin may edit it.</summary>
    private async Task<AdminResult?> CheckEditableAsync(int id, CancellationToken ct)
    {
        var owner = await _db.ListeningLessons.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new { l.OwnerUserId })
            .FirstOrDefaultAsync(ct);
        if (owner == null)
        {
            return AdminResult.NotFound();
        }

        return owner.OwnerUserId != null ? AdminResult.Forbidden(AdminListeningMessages.LearnerLessonReadOnly) : null;
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
