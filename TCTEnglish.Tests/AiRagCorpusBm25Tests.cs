using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Internal.Retrievers;
using TCTEnglish.Services.AI.Rag;
using TCTEnglish.Services.AI.Rag.Corpus;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;
using Xunit.Abstractions;

namespace TCTEnglish.Tests;

public sealed class AiRagCorpusBm25Tests
{
    private readonly ITestOutputHelper _output;

    public AiRagCorpusBm25Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Bm25_RanksBodyMatchRepeatedTermsLengthAndStableTies()
    {
        var bodyOnly = Doc("reading:1", "c1", title: ["daily"], keywords: ["a1"], body: ["tidal", "harbor", "marker"]);
        var titleOnly = Doc("reading:2", "c2", title: ["unrelated"], keywords: ["notes"], body: ["other", "words"]);
        var ranked = Bm25Ranker.Rank([bodyOnly, titleOnly], ["tidal", "harbor", "marker"]);
        Assert.Equal("reading:1", ranked[0].SourceKey);

        var once = Doc("reading:once", "c1", body: ["harbor", "x", "y", "z"]);
        var repeated = Doc("reading:repeated", "c2", body: ["harbor", "harbor", "harbor", "harbor"]);
        var repetition = Bm25Ranker.Rank([once, repeated], ["harbor"]);
        Assert.Equal("reading:repeated", repetition[0].SourceKey);
        Assert.True(repetition[0].Score > repetition[1].Score);

        var filler = Enumerable.Repeat("pad", 80).ToList();
        var shortDoc = Doc("reading:short", "c1", body: ["harbor"]);
        var longDoc = Doc("reading:long", "c2", body: filler.Prepend("harbor").ToList());
        var length = Bm25Ranker.Rank([longDoc, shortDoc], ["harbor"]);
        Assert.Equal("reading:short", length[0].SourceKey);
        Assert.True(length[0].Score > length[1].Score);

        var left = Doc("reading:b", "c2", body: ["harbor", "marker"]);
        var right = Doc("reading:a", "c1", body: ["harbor", "marker"]);
        var tied = Bm25Ranker.Rank([left, right], ["harbor"]);
        Assert.Equal("reading:a", tied[0].SourceKey);
        Assert.Equal("reading:b", tied[1].SourceKey);
        Assert.True(Bm25Ranker.ScoresTie(tied[0].Score, tied[1].Score));

        Assert.Empty(Bm25Ranker.Rank([], ["harbor"]));
        var empty = Doc("reading:empty", "c0");
        var withEmpty = Bm25Ranker.Rank([empty, bodyOnly], ["marker"]);
        Assert.Equal("reading:1", Assert.Single(withEmpty).SourceKey);
    }

    [Fact]
    public void Normalizer_FoldsVietnameseWithoutChangingDisplaySentences()
    {
        Assert.Equal("cach tao bo tu vung", LexicalNormalizer.Normalize("Cách tạo bộ từ vựng?"));
        Assert.Equal("phrase:tao_bo_tu", LexicalNormalizer.PhraseToken("tạo bộ từ"));
        var document = new CorpusDocument("guide", "guide:sample", "sample", "Tạo bộ từ vựng", ["tạo bộ từ"], "Đăng nhập và tạo bộ.\n\n# Mục\nGiữ nguyên dấu.", "/Home/CreateSet", "authenticated", "hash", "v");
        var prepared = CorpusChunker.Prepare(document with { ContentHash = CorpusHash.Sha256(CorpusHash.Canonical(document.SourceKey, document.Visibility, document.Route, document.Title, document.Keywords, document.Body)) });
        Assert.Contains(prepared.Sentences, sentence => sentence.Text.Contains('ộ') || sentence.Text.Contains('ă'));
        Assert.Equal(prepared.Sentences.Select(sentence => sentence.SentenceId), CorpusChunker.Prepare(prepared.Document).Sentences.Select(sentence => sentence.SentenceId));
        Assert.Equal(ApproximateTokenCounter.Count("abcd"), new SimpleAiTokenCounter().CountTokens("abcd"));
    }

    [Fact]
    public async Task Adapters_ExcludeUnreviewedGuidesAndPrivateLessons()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.ReadingPassages.AddRange(
            new ReadingPassage { Title = "Public passage", Content = "The tidal harbor marker is visible at dawn.", Level = "A1", Topic = "Sea", IsPublished = true, CreatedAt = DateTime.UtcNow },
            new ReadingPassage { Title = "Draft passage", Content = "PRIVATE_READING_DRAFT", Level = "A1", IsPublished = false, CreatedAt = DateTime.UtcNow });
        context.WritingExercises.AddRange(
            new WritingExercise { Title = "Public writing", Level = "A1", ContentType = "sentence", Topic = "Work", SourceType = "admin", PreviewText = "Public preview", IsPublished = true, CreatedAt = DateTime.UtcNow, UserId = null, WritingExerciseSentences = [new WritingExerciseSentence { SortOrder = 1, VietnameseText = "Xin chào", EnglishMeaning = "A public writing sentence." }] },
            new WritingExercise { Title = "Private writing", Level = "A1", ContentType = "sentence", Topic = "Work", SourceType = "user", PreviewText = "PRIVATE_WRITING", IsPublished = true, CreatedAt = DateTime.UtcNow, UserId = 1, WritingExerciseSentences = [new WritingExerciseSentence { SortOrder = 1, VietnameseText = "bí mật", EnglishMeaning = "PRIVATE_WRITING_SENTENCE" }] });
        context.ListeningLessons.AddRange(
            new ListeningLesson { Title = "Public listening", Level = "A1", Topic = "Daily", IsPublished = true, OwnerUserId = null, CreatedAt = DateTime.UtcNow, TranscriptLines = [new ListeningTranscriptLine { OrderIndex = 1, Speaker = "A", Text = "A public transcript line." }] },
            new ListeningLesson { Title = "Private listening", Level = "A1", Topic = "Daily", IsPublished = true, OwnerUserId = 1, CreatedAt = DateTime.UtcNow, TranscriptLines = [new ListeningTranscriptLine { OrderIndex = 1, Speaker = "A", Text = "PRIVATE_LISTENING" }] });
        context.SpeakingVideos.AddRange(
            new SpeakingVideo { Title = "Public speaking", YoutubeId = "public", OwnerUserId = null, ImportStatus = "ready", Level = "A1", Topic = "Travel", CreatedAt = DateTime.UtcNow, SpeakingSentences = [new SpeakingSentence { StartTime = 0, EndTime = 1, Text = "A public speaking sentence.", VietnameseMeaning = "câu công khai" }] },
            new SpeakingVideo { Title = "Draft speaking", YoutubeId = "draft", OwnerUserId = null, ImportStatus = "draft", Level = "A1", Topic = "Travel", CreatedAt = DateTime.UtcNow, SpeakingSentences = [new SpeakingSentence { StartTime = 0, EndTime = 1, Text = "PRIVATE_DRAFT_SENTENCE", VietnameseMeaning = "nháp" }] },
            new SpeakingVideo { Title = "Owned speaking", YoutubeId = "owned", OwnerUserId = 1, ImportStatus = "ready", Level = "A1", Topic = "Travel", CreatedAt = DateTime.UtcNow, SpeakingSentences = [new SpeakingSentence { StartTime = 0, EndTime = 1, Text = "PRIVATE_OWNED_SENTENCE", VietnameseMeaning = "riêng" }] });
        await context.SaveChangesAsync();

        var lessons = await new LessonCorpusAdapter(context).CollectAsync(CancellationToken.None);
        var snapshotText = JsonSerializer.Serialize(lessons.Documents);
        Assert.Contains(lessons.Documents, document => document.SourceKey.StartsWith("reading:", StringComparison.Ordinal) && document.Body.Contains("tidal harbor marker", StringComparison.Ordinal));
        Assert.DoesNotContain("PRIVATE_", snapshotText);
        Assert.Contains(lessons.Exclusions, exclusion => exclusion.Reason is "unpublished" or "private-owner" or "draft");
        Assert.DoesNotContain(lessons.Documents, document => document.SourceKey.StartsWith("card:", StringComparison.Ordinal));

        var root = CreateContentRoot();
        try
        {
            var guidePath = Path.Combine(root, "guides.json");
            await File.WriteAllTextAsync(guidePath, """
            {"_version":"1.0.0","guides":[
              {"id":"create-set","title":"Tạo bộ","keywords":["tạo bộ từ"],"body":"Tạo bộ từ.","route":"/Admin/Secret","topic":"vocabulary"},
              {"id":"study-read","title":"Read","keywords":["read"],"body":"Chế độ Read.","route":"/Home/Folder","topic":"study"},
              {"id":"not-reviewed","title":"Upload","keywords":["upload"],"body":"PRIVATE_UPLOAD","route":"/Home/Folder","topic":"other"}
            ]}
            """);
            var guides = await new GuideCorpusAdapter(guidePath).CollectAsync(CancellationToken.None);
            Assert.DoesNotContain(guides.Documents, document => document.SourceKey == "guide:study-read" || document.Route.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(guides.Exclusions, exclusion => exclusion.SourceKey == "guide:create-set" && exclusion.Reason == "admin-route");
            Assert.DoesNotContain("PRIVATE_UPLOAD", JsonSerializer.Serialize(guides.Documents));

            var realGuides = await new GuideCorpusAdapter(GuidePath()).CollectAsync(CancellationToken.None);
            Assert.DoesNotContain(realGuides.Documents, document => document.SourceKey is "guide:study-read" or "guide:ai-chatbox-usage");
            Assert.Equal(39, realGuides.Documents.Count);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task Snapshot_PreservesLastValidAcrossFailureCancellationAndCorruption()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.ReadingPassages.Add(new ReadingPassage { Title = "Daily notes", Content = "The tidal harbor marker is visible at dawn.", Level = "A1", Topic = "Sea", IsPublished = true, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var store = new CorpusSnapshotStore(root);
            var service = new RetrievalCorpusService(GuidePath(), context, store);
            var dry = await service.DryRunAsync(CancellationToken.None);
            Assert.Equal("dry-run", dry.Status);
            Assert.False(Directory.Exists(store.ApprovedRoot));

            var refused = await service.BuildAsync(@"D:\foreign-cache\ai-rag", CancellationToken.None);
            Assert.Equal("refused", refused.Status);
            Assert.False(Directory.Exists(store.ApprovedRoot));
            Assert.Null(new CorpusSnapshotStore(Path.Combine(RepoRoot(), "TCTEnglish", "wwwroot")).ApprovedRoot);

            var built = await service.BuildAsync(null, CancellationToken.None);
            Assert.Equal("promoted", built.Status);
            var firstId = built.SnapshotId;
            var firstLoad = store.LoadCurrent();
            Assert.Equal(LexicalAvailability.Ready, firstLoad.Availability);
            Assert.Contains("tidal harbor marker", firstLoad.Json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"embeddingModel\":\"", firstLoad.Json, StringComparison.Ordinal);

            var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var busyStore = new CorpusSnapshotStore(root, new GateProbe(started, blocked), TimeSpan.FromMilliseconds(100));
            var busyService = new RetrievalCorpusService(GuidePath(), context, busyStore);
            var firstBuild = Task.Run(() => busyService.BuildAsync(null, CancellationToken.None));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var busy = await busyService.BuildAsync(null, CancellationToken.None);
            Assert.Equal("busy", busy.Status);
            blocked.TrySetResult();
            var completed = await firstBuild.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(completed.Status == "promoted", completed.Status + ":" + completed.Message);

            var throwing = new CorpusSnapshotStore(root, new ThrowProbe());
            var failed = await new RetrievalCorpusService(GuidePath(), context, throwing).BuildAsync(null, CancellationToken.None);
            Assert.Equal("failed", failed.Status);
            Assert.Equal(firstLoad.ChecksumSha256, store.LoadSnapshot(firstId!).ChecksumSha256);

            var cancelled = await service.BuildAsync(null, new CancellationToken(true));
            Assert.Equal("cancelled", cancelled.Status);
            Assert.Equal(LexicalAvailability.Ready, store.LoadSnapshot(firstId!).Availability);

            var snapshotPath = Path.Combine(store.ApprovedRoot!, "snapshots", store.LoadCurrent().SnapshotId!, "corpus.json");
            var bytes = await File.ReadAllBytesAsync(snapshotPath);
            bytes[0] ^= 0x1;
            await File.WriteAllBytesAsync(snapshotPath, bytes);
            var corrupt = store.LoadCurrent();
            Assert.Equal(LexicalAvailability.Corrupt, corrupt.Availability);
            var hidden = await service.RetrieveAsync(1, "tidal harbor marker", CancellationToken.None);
            Assert.Equal(LexicalAvailability.Corrupt, hidden.Availability);
            Assert.Empty(hidden.Hits);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task Retrieval_RechecksPublicationHashAndRoleBeforeEvidence()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        var passage = new ReadingPassage { Title = "Daily notes", Content = "The tidal harbor marker is visible at dawn.", Level = "A1", Topic = "Sea", IsPublished = true, CreatedAt = DateTime.UtcNow };
        context.ReadingPassages.Add(passage);
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var store = new CorpusSnapshotStore(root);
            var service = new RetrievalCorpusService(GuidePath(), context, store);
            Assert.Equal("promoted", (await service.BuildAsync(null, CancellationToken.None)).Status);
            var found = await service.RetrieveAsync(1, "tidal harbor marker", CancellationToken.None);
            var hit = Assert.Single(found.Hits);
            Assert.StartsWith("reading:", hit.SourceKey, StringComparison.Ordinal);
            Assert.Contains("tidal harbor marker", hit.Text, StringComparison.Ordinal);
            Assert.Null(hit.Route is null ? null : hit.Route.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) ? hit.Route : null);

            passage.Content = "The marker sentence has changed.";
            await context.SaveChangesAsync();
            var changed = await service.RetrieveAsync(1, "tidal harbor marker", CancellationToken.None);
            Assert.Empty(changed.Hits);
            Assert.True(changed.RecheckRejected);
            Assert.DoesNotContain("tidal harbor marker", changed.Hits.Select(item => item.Text));

            passage.Content = "The tidal harbor marker is visible at dawn.";
            passage.IsPublished = false;
            await context.SaveChangesAsync();
            var unpublished = await service.RetrieveAsync(1, "tidal harbor marker", CancellationToken.None);
            Assert.True(unpublished.RecheckRejected);
            Assert.Empty(unpublished.Hits);

            passage.IsPublished = true;
            await context.SaveChangesAsync();
            context.Users.RemoveRange(context.Users);
            await context.SaveChangesAsync();
            var revoked = await service.RetrieveAsync(1, "tidal harbor marker", CancellationToken.None);
            Assert.Empty(revoked.Hits);
            Assert.True(revoked.RecheckRejected);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task ChatBoundary_UsesLexicalEvidenceWithoutReplacingLiveFacts()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.Sets.Add(new Set { SetId = 1, OwnerId = 1, SetName = "Metals", CreatedAt = DateTime.UtcNow });
        context.Cards.Add(new Card { CardId = 1, SetId = 1, Term = "vang", Definition = "SCOPED_CARD_DEFINITION" });
        context.ReadingPassages.Add(new ReadingPassage { Title = "Daily notes", Content = "The tidal harbor marker is visible at dawn.", Level = "A1", Topic = "Sea", IsPublished = true, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var service = new RetrievalCorpusService(GuidePath(), context, new CorpusSnapshotStore(root));
            var answers = new AiChatAnswerService(new DeterministicIntentClassifier(), [new CardLookupRetriever(context)], new TemplateAnswerComposer(), new SimpleAiTokenCounter(), service);
            var missing = await answers.AnswerAsync(1, "Cach dung folder zephyr index probe?", [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.InsufficientEvidence, missing.Outcome);
            Assert.Equal(LexicalBoundaryMessages.IndexUnavailable, missing.Text);
            Assert.Empty(missing.Evidence);

            var price = await answers.AnswerAsync(1, "Giá vàng hôm nay là bao nhiêu?", [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.OutOfScope, price.Outcome);
            Assert.NotEqual(LexicalBoundaryMessages.IndexUnavailable, price.Text);

            Assert.Equal("promoted", (await service.BuildAsync(null, CancellationToken.None)).Status);
            const string lessonQuestion = "zzzz tidal harbor marker qqqq";
            var lesson = await answers.AnswerAsync(1, lessonQuestion, [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.Answered, lesson.Outcome);
            Assert.Contains("tidal harbor marker", lesson.Text, StringComparison.Ordinal);
            Assert.Contains(lesson.Evidence, evidence => evidence.SourceType == "reading-passage" && evidence.EmbeddingModel is null);

            var passage = context.ReadingPassages.Single();
            passage.IsPublished = false;
            await context.SaveChangesAsync();
            var revoked = await answers.AnswerAsync(1, lessonQuestion, [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.InsufficientEvidence, revoked.Outcome);
            Assert.Equal(LexicalBoundaryMessages.SourceRevoked, revoked.Text);
            Assert.DoesNotContain("tidal harbor marker", revoked.Text, StringComparison.Ordinal);
            Assert.Empty(revoked.Evidence);

            var card = await answers.AnswerAsync(1, "vang nghia la gi", [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.Answered, card.Outcome);
            Assert.Contains("SCOPED_CARD_DEFINITION", card.Text, StringComparison.Ordinal);

            var folded = await service.RetrieveAsync(1, "Cach tao bo tu vung", CancellationToken.None);
            Assert.Equal("guide:create-set", folded.Hits[0].SourceKey);
            var readMode = await service.RetrieveAsync(1, "che do read", CancellationToken.None);
            Assert.DoesNotContain(readMode.Hits, hit => hit.SourceKey == "guide:study-read");
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task DevelopmentGuideBaseline_DoesNotScoreHeldOutCases()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var service = new RetrievalCorpusService(GuidePath(), context, new CorpusSnapshotStore(root));
            var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
            var watch = Stopwatch.StartNew();
            var built = await service.BuildAsync(null, CancellationToken.None);
            watch.Stop();
            var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
            using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(FixturePath()));
            var cases = fixture.RootElement.GetProperty("cases").EnumerateArray()
                .Where(item => item.GetProperty("split").GetString() == "development")
                .Where(item =>
                {
                    var sources = item.GetProperty("requiredSourceIds").EnumerateArray().Select(source => source.GetString()).ToList();
                    return sources.Count > 0 && sources.All(source => source != null && source.StartsWith("src-guide-", StringComparison.Ordinal));
                })
                .ToList();
            var heldOut = fixture.RootElement.GetProperty("cases").EnumerateArray().Count(item => item.GetProperty("split").GetString() == "held_out");
            Assert.True(heldOut > 0);
            var heldOutScored = 0;
            var hits = 0;
            var misses = new List<string>();
            var queryWatch = Stopwatch.StartNew();
            foreach (var item in cases)
            {
                var expected = item.GetProperty("requiredSourceIds").EnumerateArray().Select(source => source.GetString()!).First();
                var id = item.GetProperty("id").GetString() ?? expected;
                if (!GuideSources.TryGetValue(expected, out var sourceKey))
                {
                    misses.Add(id + ":unmapped");
                    continue;
                }

                var result = await service.RetrieveAsync(1, item.GetProperty("question").GetString() ?? string.Empty, CancellationToken.None);
                if (result.Hits.Count > 0 && result.Hits[0].SourceKey == sourceKey)
                {
                    hits++;
                }
                else
                {
                    misses.Add(id + "->" + (result.Hits.Count == 0 ? "none" : result.Hits[0].SourceKey));
                }
            }

            queryWatch.Stop();
            var line = $"lexical-baseline developmentGuideCases={cases.Count} top1={hits} corpus={CorpusVersions.CorpusVersion} chunker={CorpusVersions.ChunkerVersion} normalizer={CorpusVersions.NormalizerVersion} heldOutScored={heldOutScored} documents={built.DocumentCount} bytes={built.SnapshotBytes} buildMs={watch.ElapsedMilliseconds} queryMs={queryWatch.ElapsedMilliseconds} ramEstimateBytes={Math.Max(0, memoryAfter - memoryBefore)} misses={string.Join(",", misses)}";
            _output.WriteLine(line);
            var evidenceDir = Path.Combine(RepoRoot(), ".tmp", "hybrid-rag-review", "phase-2");
            Directory.CreateDirectory(evidenceDir);
            await File.WriteAllTextAsync(Path.Combine(evidenceDir, "lexical-baseline.txt"), line);
            Assert.True(cases.Count > 0);
            Assert.Equal(0, heldOutScored);
            Assert.Equal("promoted", built.Status);
            Assert.True(built.SnapshotBytes > 0);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    private static Bm25FieldDocument Doc(string sourceKey, string chunkId, IReadOnlyList<string>? title = null, IReadOnlyList<string>? keywords = null, IReadOnlyList<string>? body = null)
    {
        return new Bm25FieldDocument(chunkId, sourceKey, title ?? [], keywords ?? [], body ?? []);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string GuidePath()
    {
        return Path.Combine(RepoRoot(), "TCTEnglish", "wwwroot", "data", "ai", "website-guides.json");
    }

    private static string FixturePath()
    {
        return Path.Combine(RepoRoot(), "TCTEnglish.Tests", "Fixtures", "AI", "hybrid-rag-evaluation.json");
    }

    private static string CreateContentRoot()
    {
        var root = Path.Combine(RepoRoot(), ".appdata", "local", "corpus-phase2-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteContentRoot(string root)
    {
        var parent = Path.Combine(RepoRoot(), ".appdata", "local", "corpus-phase2-tests");
        if (CorpusPathGuard.IsContained(parent, root) && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static User User(int id) => new()
    {
        UserId = id,
        Email = $"user{id}@test.local",
        PasswordHash = "hash",
        FullName = $"User {id}",
        Role = Roles.Standard
    };

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SqliteConnection> SqliteKeepers = new();

    private static DbflashcardContext CreateSqlite()
    {
        var connection = SqliteKeepers.GetOrAdd(Guid.NewGuid().ToString("N"), static name =>
        {
            var keeper = new SqliteConnection($"Data Source=file:{name}?mode=memory&cache=shared");
            keeper.Open();
            return keeper;
        });
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>()
            .Options;
        var context = new DbflashcardContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static readonly Dictionary<string, string> GuideSources = new()
    {
        ["src-guide-create-set"] = "guide:create-set",
        ["src-guide-password"] = "guide:change-password",
        ["src-guide-reading"] = "guide:reading-quick-action",
        ["src-guide-grammar"] = "guide:grammar-page",
        ["src-guide-billing"] = "guide:payment-history",
        ["src-guide-premium"] = "guide:premium-pricing",
        ["src-guide-class"] = "guide:join-class",
        ["src-guide-speaking"] = "guide:speaking-practice",
        ["src-guide-dashboard"] = "guide:view-streak",
        ["src-guide-folder"] = "guide:create-folder",
        ["src-guide-notification"] = "guide:notifications",
        ["src-guide-support"] = "guide:contact-support",
        ["src-guide-login"] = "guide:login-google-facebook",
        ["src-guide-privacy"] = "guide:privacy-terms",
        ["src-guide-writing"] = "guide:writing-quick-action",
        ["src-guide-listening"] = "guide:listening-quick-action",
        ["src-guide-goals"] = "guide:set-daily-goal",
        ["src-guide-chat"] = "guide:class-chat",
        ["src-guide-quiz"] = "guide:study-quiz",
        ["src-guide-matching"] = "guide:study-matching"
    };

    private sealed class ThrowProbe : ICorpusWriteProbe
    {
        public void BeforeWrite(string fullPath)
        {
            if (fullPath.EndsWith("corpus.json", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("simulated disk failure");
            }
        }
    }

    private sealed class GateProbe : ICorpusWriteProbe
    {
        private int _seen;
        private readonly TaskCompletionSource _started;
        private readonly TaskCompletionSource _release;

        public GateProbe(TaskCompletionSource started, TaskCompletionSource release)
        {
            _started = started;
            _release = release;
        }

        public void BeforeWrite(string fullPath)
        {
            if (!fullPath.EndsWith("corpus.json", StringComparison.OrdinalIgnoreCase) || Interlocked.Increment(ref _seen) != 1)
            {
                return;
            }

            _started.TrySetResult();
            _release.Task.GetAwaiter().GetResult();
        }
    }
}
