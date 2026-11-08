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

namespace TCTEnglish.Tests;

public sealed class AiRagPhase2FixRound2Tests
{
    private const string MarkerSentence = "The tidal harbor marker is visible beside the gate.";
    private const string IntroSentence = "Here you can find examples in this lesson.";

    [Fact]
    public async Task FramingQueries_RetrieveTheSubjectSentence()
    {
        var body = IntroSentence + " " + MarkerSentence;
        await using var session = await CorpusSession.Open(body);
        var passage = session.Passages[0];
        foreach (var question in new[]
        {
            "Where can I find the tidal harbor marker in this lesson?",
            "Can I find the tidal harbor marker in this lesson?"
        })
        {
            var result = await session.Retrieval.RetrieveAsync(1, question, CancellationToken.None);
            var hit = Assert.Single(result.Hits, item => item.SourceKey == "reading:" + passage.Id);
            Assert.Equal("Daily notes", hit.Title);
            Assert.Equal(MarkerSentence, hit.Text);
            Assert.Equal(CorpusHash.Sha256(MarkerSentence), hit.ContentHash);
            Assert.Equal($"reading:{passage.Id}#s2", hit.SentenceId);
            Assert.DoesNotContain(IntroSentence, hit.Text, StringComparison.Ordinal);

            var answer = await session.Answers.AnswerAsync(1, question, [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.Answered, answer.Outcome);
            Assert.Equal("full", answer.AnswerScope);
            Assert.Contains(MarkerSentence, answer.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(IntroSentence, answer.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task FramingVariants_KeepTheSubjectSpan()
    {
        await using var competing = await CorpusSession.Open(
            IntroSentence + " Ordinary closing note.",
            IntroSentence + " The copper bell hangs above the pier.");
        var copper = competing.Passages[1];
        var copperResult = await competing.Retrieval.RetrieveAsync(1, "Where can I find the copper bell in this lesson?", CancellationToken.None);
        var copperHit = Assert.Single(copperResult.Hits, hit => hit.SourceKey == "reading:" + copper.Id);
        Assert.Equal("The copper bell hangs above the pier.", copperHit.Text);
        Assert.Equal(CorpusHash.Sha256(copperHit.Text), copperHit.ContentHash);
        Assert.Equal($"reading:{copper.Id}#s2", copperHit.SentenceId);

        await using var reordered = await CorpusSession.Open(
            "Examples you can find in this lesson are here. The copper bell hangs above the pier.");
        var reorderedPassage = reordered.Passages[0];
        var reorderedResult = await reordered.Retrieval.RetrieveAsync(1, "In this lesson can I find the copper bell?", CancellationToken.None);
        var reorderedHit = Assert.Single(reorderedResult.Hits, hit => hit.SourceKey == "reading:" + reorderedPassage.Id);
        Assert.Equal("The copper bell hangs above the pier.", reorderedHit.Text);
        Assert.Equal($"reading:{reorderedPassage.Id}#s2", reorderedHit.SentenceId);

        await using var relevantFirst = await CorpusSession.Open(
            "The amber quay light is visible beside the gate. " + IntroSentence);
        var first = relevantFirst.Passages[0];
        var firstResult = await relevantFirst.Retrieval.RetrieveAsync(
            1,
            "Where can I find the amber quay light in this lesson?",
            CancellationToken.None);
        var firstHit = Assert.Single(firstResult.Hits, hit => hit.SourceKey == "reading:" + first.Id);
        Assert.Equal("The amber quay light is visible beside the gate.", firstHit.Text);
        Assert.Equal(CorpusHash.Sha256(firstHit.Text), firstHit.ContentHash);
        Assert.Equal($"reading:{first.Id}#s1", firstHit.SentenceId);
    }

    [Fact]
    public async Task UnsupportedFacts_DoNotBecomeFullAnswers()
    {
        await using var year = await CorpusSession.Open("The tidal harbor marker was installed in 2008.");
        await AssertUnsupported(year, "How many people live near the tidal harbor marker?", "2008", "installed in 2008");

        await using var repaint = await CorpusSession.Open("The tidal harbor marker was repainted because the old paint faded.");
        await AssertUnsupported(repaint, "Why does the tidal harbor marker glow at midnight?", "repainted", "old paint faded");

        await using var dawn = await CorpusSession.Open("The tidal harbor marker is visible at dawn.");
        await AssertUnsupported(dawn, "Where is the tidal harbor marker visible?", "visible at dawn");
        await AssertUnsupported(dawn, "When was the tidal harbor marker constructed?", "visible at dawn");
        await AssertUnsupported(dawn, "Who built the tidal harbor marker?", "visible at dawn");
        await AssertUnsupported(dawn, "What color does the tidal harbor marker have?", "visible at dawn");
        await AssertUnsupported(dawn, "Which color does the tidal harbor marker have?", "visible at dawn");
        await AssertUnsupported(
            dawn,
            "When is the tidal harbor marker visible and who built the tidal harbor marker?",
            "visible at dawn");
    }

    [Fact]
    public async Task SupportedFacts_StayFullWhenTheSourceStatesThem()
    {
        await using var visible = await CorpusSession.Open("The tidal harbor marker is visible at dawn.");
        var when = await visible.Answers.AnswerAsync(1, "When is the tidal harbor marker visible?", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, when.Outcome);
        Assert.Equal("full", when.AnswerScope);
        Assert.Contains("The tidal harbor marker is visible at dawn.", when.Text, StringComparison.Ordinal);

        await using var located = await CorpusSession.Open("The tidal harbor marker stands beside the eastern gate.");
        var where = await located.Answers.AnswerAsync(1, "Where does the tidal harbor marker stand?", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, where.Outcome);
        Assert.Equal("full", where.AnswerScope);
        Assert.Contains("beside the eastern gate.", where.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("at dawn", where.Text, StringComparison.OrdinalIgnoreCase);

        await using var passage = await CorpusSession.Open("The tidal harbor marker is visible at dawn.");
        var reference = await passage.Answers.AnswerAsync(1, "zzzz stored passage tidal harbor marker qqqq", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, reference.Outcome);
        Assert.Contains("tidal harbor marker", reference.Text, StringComparison.Ordinal);
        Assert.NotEmpty(reference.Evidence);

        await using var people = await CorpusSession.Open("12 people live near the tidal harbor marker.");
        var count = await people.Answers.AnswerAsync(1, "How many people live near the tidal harbor marker?", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, count.Outcome);
        Assert.Contains("12 people", count.Text, StringComparison.Ordinal);

        await using var cause = await CorpusSession.Open("The tidal harbor marker glows because the lamp is lit.");
        var why = await cause.Answers.AnswerAsync(1, "Why does the tidal harbor marker glow?", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, why.Outcome);
        Assert.Contains("glows because the lamp is lit.", why.Text, StringComparison.Ordinal);

        await using var builder = await CorpusSession.Open("The tidal harbor marker was built by Mira.");
        var who = await builder.Answers.AnswerAsync(1, "Who built the tidal harbor marker?", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, who.Outcome);
        Assert.Contains("built by Mira.", who.Text, StringComparison.Ordinal);

        await using var color = await CorpusSession.Open("The red lantern stands beside the eastern gate.");
        var which = await color.Answers.AnswerAsync(1, "What color is the red lantern?", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, which.Outcome);
        Assert.Contains("red lantern", which.Text, StringComparison.Ordinal);

        await using var both = await CorpusSession.Open("The tidal harbor marker is visible at dawn beside the eastern gate.");
        var mixed = await both.Answers.AnswerAsync(
            1,
            "When is the tidal harbor marker visible and where is the tidal harbor marker?",
            [],
            CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, mixed.Outcome);
        Assert.Contains("visible at dawn", mixed.Text, StringComparison.Ordinal);
        Assert.Contains("beside the eastern gate.", mixed.Text, StringComparison.Ordinal);

        await using var account = await CorpusSession.Open(
            ["The tidal harbor marker is visible at dawn."],
            context =>
            {
                context.Sets.Add(new Set { SetId = 1, OwnerId = 1, SetName = "Metals", CreatedAt = DateTime.UtcNow });
                context.Cards.Add(new Card { CardId = 1, SetId = 1, Term = "vang", Definition = "SCOPED_CARD_DEFINITION" });
                context.UserGoals.Add(new UserGoal
                {
                    UserId = 1,
                    GoalArea = GoalArea.Vocabulary,
                    TargetValue = 12,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            });

        var password = await account.Answers.AnswerAsync(1, "How do I change my password?", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, password.Outcome);
        Assert.Contains("/Account/Settings", password.Text, StringComparison.Ordinal);

        var card = await account.Answers.AnswerAsync(1, "vang nghia la gi", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, card.Outcome);
        Assert.Contains("SCOPED_CARD_DEFINITION", card.Text, StringComparison.Ordinal);

        var goal = await account.Answers.AnswerAsync(1, "xem goal cua toi", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, goal.Outcome);
        Assert.Contains("12", goal.Text, StringComparison.Ordinal);
        Assert.Contains("/Goals", goal.Text, StringComparison.Ordinal);

        var price = await account.Answers.AnswerAsync(1, "Giá vàng hôm nay là bao nhiêu?", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.OutOfScope, price.Outcome);
    }

    [Theory]
    [InlineData("What color is the tidal harbor marker?", "The tidal harbor marker is visible beside a red lantern.")]
    [InlineData("How many people live near the tidal harbor marker?", "The tidal harbor marker is visible while 12 people live near the copper bell.")]
    [InlineData("Why does the tidal harbor marker glow?", "The tidal harbor marker glows while the copper bell rings because the wind is strong.")]
    [InlineData("When was the tidal harbor marker constructed?", "The tidal harbor marker was constructed beside a lantern installed in 2008.")]
    [InlineData("Who built the tidal harbor marker?", "The tidal harbor marker was built beside a mural painted by Mira.")]
    [InlineData("Where is the tidal harbor marker visible?", "The tidal harbor marker is visible at dawn while the copper bell hangs beside the gate.")]
    [InlineData("How does the tidal harbor marker glow?", "The tidal harbor marker glows while the copper bell is rung by Mira.")]
    [InlineData("Please tell me the height of the tidal harbor marker.", "The tidal harbor marker is visible at dawn.")]
    [InlineData("Please tell me about the height of the tidal harbor marker.", "The tidal harbor marker is visible at dawn.")]
    [InlineData("Ai đã xây tidal harbor marker?", "The tidal harbor marker is visible at dawn.")]
    [InlineData("When is the copper harbor marker visible?", "The tidal harbor marker is visible at dawn.")]
    [InlineData("Why does the tidal harbor marker glow at midnight?", "The tidal harbor marker glows because the lamp is lit.")]
    [InlineData("What does the passage say about the tidal harbor marker and who built it?", "The tidal harbor marker is visible at dawn.")]
    [InlineData("When is the tidal harbor marker visible and what is its height?", "The tidal harbor marker is visible at dawn.")]
    public async Task FullAnswer_RequiresTheSameSubjectRelationAndQualifiers(string question, string body)
    {
        await using var session = await CorpusSession.Open(body);
        await AssertUnsupported(session, question);
    }

    [Theory]
    [InlineData("When was the tidal harbor marker constructed?", "The tidal harbor marker was constructed in 2008.")]
    [InlineData("Why does the copper bell ring at midnight?", "The copper bell rings at midnight because the keeper pulls the rope.")]
    [InlineData("What color is the tidal harbor marker?", "The tidal harbor marker is blue.")]
    [InlineData("Where does the copper bell hang?", "The copper bell hangs above the pier.")]
    [InlineData("What does the passage say about the tidal harbor marker?", "The tidal harbor marker is visible at dawn.")]
    [InlineData("What does the passage say about passive voice?", "Passive voice is used in this stored example.")]
    public async Task ExplicitlySupportedRequests_RetainExactEvidence(string question, string body)
    {
        await using var session = await CorpusSession.Open(body);
        var answer = await session.Answers.AnswerAsync(1, question, [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, answer.Outcome);
        Assert.Contains(body, answer.Text, StringComparison.Ordinal);
        var evidence = Assert.Single(answer.Evidence);
        Assert.Equal(body, evidence.Text);
        Assert.Equal(CorpusHash.Sha256(body), evidence.ContentHash);
    }

    [Fact]
    public async Task KeywordSearch_ReturnsAnExcerptWithoutClaimingAFullAnswer()
    {
        await using var session = await CorpusSession.Open("The tidal harbor marker is visible at dawn.");
        var answer = await session.Answers.AnswerAsync(1, "zzzz tidal harbor marker qqqq", [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, answer.Outcome);
        Assert.Equal("excerpt", answer.AnswerScope);
        Assert.StartsWith("Đoạn học liệu khớp từ khóa", answer.Text, StringComparison.Ordinal);
        Assert.Single(answer.Evidence);
    }

    private static async Task AssertUnsupported(CorpusSession session, string question, params string[] forbidden)
    {
        var answer = await session.Answers.AnswerAsync(1, question, [], CancellationToken.None);
        Assert.Equal(HybridRagOutcome.InsufficientEvidence, answer.Outcome);
        Assert.NotEqual("full", answer.AnswerScope);
        Assert.Empty(answer.Evidence);
        Assert.Equal(LexicalBoundaryMessages.Unsupported, answer.Text);
        foreach (var phrase in forbidden)
        {
            Assert.DoesNotContain(phrase, answer.Text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class CorpusSession : IAsyncDisposable
    {
        private readonly string _root;
        private readonly DbflashcardContext _context;

        private CorpusSession(string root, DbflashcardContext context, RetrievalCorpusService retrieval, IReadOnlyList<ReadingPassage> passages)
        {
            _root = root;
            _context = context;
            Retrieval = retrieval;
            Passages = passages;
            Answers = new AiChatAnswerService(
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
                retrieval);
        }

        public RetrievalCorpusService Retrieval { get; }

        public AiChatAnswerService Answers { get; }

        public IReadOnlyList<ReadingPassage> Passages { get; }

        public static async Task<CorpusSession> Open(params string[] bodies)
        {
            return await Open(bodies, null);
        }

        public static async Task<CorpusSession> Open(IEnumerable<string> bodies, Action<DbflashcardContext>? seed)
        {
            var context = CreateSqlite();
            context.Users.Add(User());
            var passages = bodies.Select(body => new ReadingPassage
            {
                Title = "Daily notes",
                Content = body,
                Level = "A1",
                Topic = "Sea",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            }).ToList();
            context.ReadingPassages.AddRange(passages);
            seed?.Invoke(context);
            await context.SaveChangesAsync();
            var root = CreateContentRoot();
            var retrieval = new RetrievalCorpusService(GuidePath(), context, new CorpusSnapshotStore(root));
            Assert.Equal("promoted", (await retrieval.BuildAsync(null, CancellationToken.None)).Status);
            return new CorpusSession(root, context, retrieval, passages);
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            var parent = Path.Combine(RepoRoot(), ".appdata", "local", "corpus-phase2-tests");
            if (CorpusPathGuard.IsContained(parent, _root) && Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

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

    private static User User() => new()
    {
        UserId = 1,
        Email = "user1@test.local",
        PasswordHash = "hash",
        FullName = "User 1",
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
