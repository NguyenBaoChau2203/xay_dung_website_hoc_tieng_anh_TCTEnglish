using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Internal.Retrievers;
using TCTEnglish.Services.AI.Rag;
using TCTEnglish.Services.AI.Rag.Corpus;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiRagPhase2FixRound1Tests
{
    [Fact]
    public void AdminForm_KeepsSelectedOperationWhenSubmitButtonsAreDisabled()
    {
        var view = File.ReadAllText(Path.Combine(RepoRoot(), "TCTEnglish", "Areas", "Admin", "Views", "AiManagement", "Index.cshtml"));
        Assert.DoesNotContain("name=\"dryRun\"", view, StringComparison.Ordinal);
        var scriptAt = view.IndexOf("retrievalIndexForm", StringComparison.Ordinal);
        var assignAt = view.IndexOf("field.value = operation", scriptAt, StringComparison.Ordinal);
        var disableAt = view.IndexOf("button.disabled = true", scriptAt, StringComparison.Ordinal);
        Assert.True(assignAt > scriptAt);
        Assert.True(disableAt > assignAt);
    }

    [Fact]
    public async Task AdminRebuild_DryRunDoesNotPromoteAndMissingOperationDoesNotMutate()
    {
        var root = Path.Combine(RepoRoot(), ".appdata", "local", "corpus-phase2-tests", "admin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = new TestWebApplicationFactory(services =>
            {
                services.RemoveAll<ICorpusSnapshotStore>();
                services.AddSingleton<ICorpusSnapshotStore>(_ => new CorpusSnapshotStore(root));
                services.RemoveAll<MlNetAiQueryClassifier>();
                services.AddSingleton<MlNetAiQueryClassifier>();
            });
            await factory.InitializeAsync();
            var manifest = Path.Combine(root, ".appdata", "local", "ai-rag", "manifest.json");

            using var anonymous = IntegrationTestClientHelper.CreateAnonymousClient(factory);
            using var anonymousBuild = new FormUrlEncodedContent(new Dictionary<string, string> { ["operation"] = "build" });
            var anonymousResponse = await anonymous.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", anonymousBuild);
            Assert.NotEqual(HttpStatusCode.OK, anonymousResponse.StatusCode);
            Assert.False(File.Exists(manifest));

            using var standard = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
            using var standardBuild = Form(string.Empty, "build");
            var standardResponse = await standard.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", standardBuild);
            Assert.True(standardResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest);
            Assert.False(File.Exists(manifest));

            using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
            using var missingToken = Form(string.Empty, "build");
            var missingTokenResponse = await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", missingToken);
            Assert.Equal(HttpStatusCode.BadRequest, missingTokenResponse.StatusCode);
            Assert.False(File.Exists(manifest));

            var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, "/Admin/AiManagement");
            Assert.Contains("name=\"operation\"", await admin.GetStringAsync("/Admin/AiManagement"), StringComparison.Ordinal);
            using var dryRun = Form(token, "dry-run");
            var dryRunResponse = await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", dryRun);
            Assert.Equal(HttpStatusCode.Redirect, dryRunResponse.StatusCode);
            Assert.False(File.Exists(manifest));

            token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, "/Admin/AiManagement");
            using var missingOperation = Form(token, null);
            var missingOperationResponse = await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", missingOperation);
            Assert.Equal(HttpStatusCode.Redirect, missingOperationResponse.StatusCode);
            Assert.False(File.Exists(manifest));

            token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, "/Admin/AiManagement");
            using var invalid = Form(token, "mutate");
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", invalid)).StatusCode);
            Assert.False(File.Exists(manifest));

            token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, "/Admin/AiManagement");
            using var build = Form(token, "build");
            var buildResponse = await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", build);
            Assert.Equal(HttpStatusCode.Redirect, buildResponse.StatusCode);
            var afterBuild = await admin.GetStringAsync("/Admin/AiManagement");
            var status = Regex.Match(afterBuild, "role=\"status\">(?<message>.*?)</div>", RegexOptions.Singleline).Groups["message"].Value;
            var snapshot = Regex.Match(afterBuild, "Snapshot</dt>\\s*<dd class=\"col-sm-8\">(?<id>.*?)</dd>", RegexOptions.Singleline).Groups["id"].Value;
            Assert.True(File.Exists(manifest), "status=" + status + "; snapshot=" + snapshot);
            var promoted = await File.ReadAllBytesAsync(manifest);
            var snapshotId = JsonDocument.Parse(promoted).RootElement.GetProperty("snapshotId").GetString();

            token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, "/Admin/AiManagement");
            using var secondDryRun = Form(token, "dry-run");
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", secondDryRun)).StatusCode);
            Assert.Equal(promoted, await File.ReadAllBytesAsync(manifest));
            Assert.Equal(snapshotId, JsonDocument.Parse(await File.ReadAllBytesAsync(manifest)).RootElement.GetProperty("snapshotId").GetString());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ChunkLimit_SplitsOrRejectsOversizedSentenceBeforeEvidence()
    {
        var huge = string.Join(' ', Enumerable.Repeat("harborword", 2700)) + ".";
        Assert.Equal(29_700, huge.Length);
        Assert.Equal(7_425, ApproximateTokenCounter.Count(huge));
        var afterNormal = "Welcome to the lesson. " + huge;

        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.ReadingPassages.Add(new ReadingPassage
        {
            Title = "Long harbor note",
            Content = afterNormal,
            Level = "A1",
            Topic = "Sea",
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var service = new RetrievalCorpusService(GuidePath(), context, new CorpusSnapshotStore(root));
            var built = await service.BuildAsync(null, CancellationToken.None);
            Assert.Equal("promoted", built.Status);
            var loaded = service.ReadStatus();
            var store = new CorpusSnapshotStore(root);
            var snapshotJson = store.LoadCurrent().Json;
            Assert.False(string.IsNullOrWhiteSpace(snapshotJson));
            var snapshot = JsonSerializer.Deserialize<CorpusSnapshotFile>(snapshotJson!, SnapshotJson.Options);
            var passage = snapshot!.Documents.Single(document => document.SourceKey.StartsWith("reading:", StringComparison.Ordinal));
            Assert.Equal(CorpusVersions.ChunkerVersion, snapshot.ChunkerVersion);
            Assert.NotEqual("paragraph-sentence-v1", snapshot.ChunkerVersion);
            Assert.All(passage.Sentences, sentence =>
            {
                Assert.True(ApproximateTokenCounter.Count(sentence.Text) <= CorpusVersions.MaxChunkTokens);
                Assert.Equal(CorpusHash.Sha256(sentence.Text), sentence.ContentHash);
                Assert.NotEqual(huge, sentence.Text);
            });
            Assert.Equal(huge, string.Concat(passage.Sentences.Skip(1).Select(sentence => sentence.Text)));
            Assert.StartsWith("reading:", passage.Sentences[1].SentenceId, StringComparison.Ordinal);
            Assert.Contains("p1", passage.Sentences[1].SentenceId, StringComparison.Ordinal);
            Assert.All(passage.Chunks, chunk => Assert.True(ChunkBudget(passage, chunk) <= CorpusVersions.MaxChunkTokens));

            var stamped = snapshotJson!.Replace(CorpusVersions.ChunkerVersion, "paragraph-sentence-v1", StringComparison.Ordinal);
            Assert.False(SnapshotJson.TryValidate(stamped, out var detail));
            Assert.Equal("mixed-version", detail);

            var found = await service.RetrieveAsync(1, "zzzz harborword qqqq marker", CancellationToken.None);
            var bounded = found.Hits.Where(item => item.SourceKey == passage.SourceKey).ToList();
            Assert.NotEmpty(bounded);
            Assert.All(bounded, hit =>
            {
                Assert.True(ApproximateTokenCounter.Count(hit.Text) <= CorpusVersions.MaxChunkTokens);
                Assert.Contains("harborword", hit.Text, StringComparison.Ordinal);
            });
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task MatchedSentence_ComesFromTheSpanThatContainsTheQuery()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
            var welcome = Passage("Welcome to the lesson. The tidal harbor marker is visible at dawn.");
            context.ReadingPassages.Add(welcome);
        context.ReadingPassages.Add(Passage("The tidal harbor marker is visible at dawn. Ordinary closing note."));
        context.ReadingPassages.Add(Passage("Opening note. The tidal harbor marker is visible at dawn. Closing note."));
        context.ReadingPassages.Add(Passage("Opening note. Middle note. The tidal harbor marker is visible at dawn."));
        context.ReadingPassages.Add(Passage("# Harbor notes\n\nWelcome ashore. The tidal harbor marker is visible at dawn."));
        var filler = string.Join(' ', Enumerable.Repeat("Ordinary classroom note.", 120));
        context.ReadingPassages.Add(Passage(filler + " The flint tidal harbor marker is visible at dawn."));
        context.ReadingPassages.Add(Passage("The tidal harbor marker is visible at dawn. The tidal harbor marker is visible at dawn."));
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var service = new RetrievalCorpusService(GuidePath(), context, new CorpusSnapshotStore(root));
            Assert.Equal("promoted", (await service.BuildAsync(null, CancellationToken.None)).Status);
            var result = await service.RetrieveAsync(1, "zzzz tidal harbor marker qqqq", CancellationToken.None);
            var lessonHits = result.Hits.Where(hit => hit.SourceType == "reading-passage").ToList();
            Assert.NotEmpty(lessonHits);
            Assert.All(lessonHits, hit =>
            {
                Assert.Contains("tidal harbor marker is visible at dawn.", hit.Text, StringComparison.Ordinal);
                Assert.Equal(CorpusHash.Sha256(hit.Text), hit.ContentHash);
                Assert.DoesNotContain("Welcome", hit.Text, StringComparison.Ordinal);
                Assert.DoesNotContain("# Harbor", hit.Text, StringComparison.Ordinal);
            });

            var later = await service.RetrieveAsync(1, "flint", CancellationToken.None);
            var laterHit = Assert.Single(later.Hits, hit => hit.Text.Contains("flint", StringComparison.Ordinal));
            Assert.Equal("The flint tidal harbor marker is visible at dawn.", laterHit.Text);
            var snapshot = JsonSerializer.Deserialize<CorpusSnapshotFile>(new CorpusSnapshotStore(root).LoadCurrent().Json!, SnapshotJson.Options);
            var laterDocument = snapshot!.Documents.Single(document => document.Body.Contains("flint", StringComparison.Ordinal));
            var laterSentence = laterDocument.Sentences.Single(sentence => sentence.Text.Contains("flint", StringComparison.Ordinal));
            Assert.DoesNotContain(laterSentence.Number, laterDocument.Chunks.OrderBy(chunk => chunk.Number).First().SentenceNumbers);

            welcome.IsPublished = false;
            await context.SaveChangesAsync();
            var revoked = await service.RetrieveAsync(1, "zzzz tidal harbor marker qqqq", CancellationToken.None);
            Assert.DoesNotContain(revoked.Hits, hit => hit.Text.StartsWith("Welcome", StringComparison.Ordinal));
            Assert.All(revoked.Hits.Where(hit => hit.SourceType == "reading-passage"), hit =>
                Assert.Contains("tidal harbor marker is visible at dawn.", hit.Text, StringComparison.Ordinal));
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task UnsupportedLexicalQuestion_DoesNotBecomeFullAnswer()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.Sets.Add(new Set { SetId = 1, OwnerId = 1, SetName = "Metals", CreatedAt = DateTime.UtcNow });
        context.Cards.Add(new Card { CardId = 1, SetId = 1, Term = "vang", Definition = "SCOPED_CARD_DEFINITION" });
        context.ReadingPassages.Add(Passage("The tidal harbor marker is visible at dawn."));
        context.ReadingPassages.Add(Passage("The red lantern stands beside the gate."));
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var service = new RetrievalCorpusService(GuidePath(), context, new CorpusSnapshotStore(root));
            var answers = CreateProductionAnswers(context, service);
            Assert.Equal("promoted", (await service.BuildAsync(null, CancellationToken.None)).Status);

            foreach (var question in new[]
            {
                "Why does the tidal harbor marker glow at midnight?",
                "How many people live near the tidal harbor marker?",
                "Vi sao tidal harbor marker phat sang luc nua dem?",
                "Co bao nhieu nguoi song gan tidal harbor marker?",
                "Why does the red lantern burn at noon?"
            })
            {
                var answer = await answers.AnswerAsync(1, question, [], CancellationToken.None);
                Assert.NotEqual(HybridRagOutcome.Answered, answer.Outcome);
                Assert.NotEqual("full", answer.AnswerScope);
                Assert.Empty(answer.Evidence);
                Assert.DoesNotContain("visible at dawn", answer.Text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("stands beside the gate", answer.Text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Flashcard", answer.Text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Add card", answer.Text, StringComparison.OrdinalIgnoreCase);
            }

            var located = await answers.AnswerAsync(1, "Where is the tidal harbor marker visible?", [], CancellationToken.None);
            Assert.NotEqual(HybridRagOutcome.Answered, located.Outcome);
            Assert.NotEqual("full", located.AnswerScope);
            Assert.Empty(located.Evidence);
            Assert.DoesNotContain("visible at dawn", located.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stands beside the gate", located.Text, StringComparison.OrdinalIgnoreCase);

            var reference = await answers.AnswerAsync(1, "zzzz tidal harbor marker qqqq", [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.Answered, reference.Outcome);
            Assert.Contains("tidal harbor marker", reference.Text, StringComparison.Ordinal);

            var password = await answers.AnswerAsync(1, "How do I change my password?", [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.Answered, password.Outcome);
            Assert.Contains("/Account/Settings", password.Text, StringComparison.Ordinal);

            var price = await answers.AnswerAsync(1, "Giá vàng hôm nay là bao nhiêu?", [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.OutOfScope, price.Outcome);

            var card = await answers.AnswerAsync(1, "vang nghia la gi", [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.Answered, card.Outcome);
            Assert.Contains("SCOPED_CARD_DEFINITION", card.Text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    private static int ChunkBudget(CorpusSnapshotEntry passage, CorpusSnapshotChunk chunk)
    {
        var sentences = passage.Sentences.ToDictionary(sentence => sentence.Number);
        var total = 0;
        var seen = 0;
        foreach (var number in chunk.SentenceNumbers)
        {
            total += (seen == 0 ? 0 : 1) + ApproximateTokenCounter.Count(sentences[number].Text);
            seen++;
        }

        return total;
    }

    private static FormUrlEncodedContent Form(string token, string? operation)
    {
        var fields = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(token))
        {
            fields["__RequestVerificationToken"] = token;
        }

        if (operation is not null)
        {
            fields["operation"] = operation;
        }

        return new FormUrlEncodedContent(fields);
    }

    private static AiChatAnswerService CreateProductionAnswers(DbflashcardContext context, ILexicalRetrievalService lexical)
    {
        return new AiChatAnswerService(
            new DeterministicIntentClassifier(),
            [
                new WebsiteGuideRetriever(GuidePath()),
                new UserVocabularyRetriever(context),
                new LearningProgressRetriever(context),
                new StudyRecommendationRetriever(context),
                new CardLookupRetriever(context),
                new SpeakingRetriever(context),
                new ClassRetriever(context),
                new LearningAreaProgressRetriever(context),
                new GoalsRetriever(context),
                new NotificationRetriever(context),
                new BillingRetriever(context),
                new SecurityPolicyRetriever()
            ],
            new TemplateAnswerComposer(),
            new SimpleAiTokenCounter(),
            lexical);
    }

    private static ReadingPassage Passage(string content) => new()
    {
        Title = "Harbor observations " + Guid.NewGuid().ToString("N")[..8],
        Content = content,
        Level = "A1",
        Topic = "Sea",
        IsPublished = true,
        CreatedAt = DateTime.UtcNow
    };

    private static string RepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static string GuidePath()
    {
        return Path.Combine(RepoRoot(), "TCTEnglish", "wwwroot", "data", "ai", "website-guides.json");
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

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.Data.Sqlite.SqliteConnection> SqliteKeepers = new();

    private static DbflashcardContext CreateSqlite()
    {
        var connection = SqliteKeepers.GetOrAdd(Guid.NewGuid().ToString("N"), static name =>
        {
            var keeper = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source=file:{name}?mode=memory&cache=shared");
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
}
