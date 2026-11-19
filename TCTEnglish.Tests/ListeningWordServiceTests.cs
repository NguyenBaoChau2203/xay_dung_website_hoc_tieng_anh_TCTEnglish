using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services;
using TCTEnglish.Services.Listening;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Listening redesign, server side (plan 2026-11-18-listening-redesign, sections 4.1-4.4): the C2 level fix, the new
/// view-model fields, the tap-a-word lookup and the save-to-set rules.
/// </summary>
public sealed class ListeningWordServiceTests
{
    private const int StandardId = 9302;
    private const int OtherPremiumId = 9303;
    private const int AdminId = 9304;
    private const int FastPremiumId = 9305;   // exhausts the process-wide rate limit, so no other test may use it

    private const int PublicLessonId = 93001;   // A1, topic "Daily", vocab: order, to go, latte
    private const int PrivateLessonId = 93002;  // private, owned by OtherPremiumId
    private const int UnpublishedLessonId = 93003;

    private const string SetName = "Từ vựng Luyện nghe";

    // ------------------------------------------------------------------ fakes and setup

    private sealed class FakeSuggestService : IVocabSuggestService
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public bool Fail { get; set; }

        public Task<VocabSuggestionResult> SuggestAsync(string term, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            if (Fail)
            {
                return Task.FromResult(new VocabSuggestionResult { Success = false, ErrorMessage = "provider down" });
            }

            return Task.FromResult(new VocabSuggestionResult
            {
                Success = true,
                Phonetic = "/fake/",
                Example = $"An example with {term}.",
                ExampleTranslation = "Ví dụ.",
                Definitions = new List<DefinitionSuggestion>
                {
                    new() { PartOfSpeech = "noun", Definition = $"nghĩa của {term}" },
                    new() { PartOfSpeech = "verb", Definition = "nghĩa thứ hai" },
                },
            });
        }

        public Task<VocabEnrichResponse> EnrichAsync(string term, string? meaningHint = null, string? context = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VocabEnrichBatchItem>> EnrichBatchAsync(IReadOnlyList<VocabEnrichBatchRequest> items, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ImageSearchHit>> SearchImagesAsync(string query, int max, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync(FakeSuggestService fake)
    {
        var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IVocabSuggestService>();
            services.AddSingleton<IVocabSuggestService>(fake);
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.Users.AddRange(
            User(StandardId, "word-standard@test.local", Roles.Standard),
            User(OtherPremiumId, "word-other@test.local", Roles.Premium),
            User(AdminId, "word-admin@test.local", Roles.Admin),
            User(FastPremiumId, "word-fast-service@test.local", Roles.Premium));
        db.ListeningLessons.AddRange(
            Lesson(PublicLessonId, "Ordering coffee", "A1", "Daily", published: true, owner: null),
            Lesson(PrivateLessonId, "Private lesson", "B1", "Mine", published: true, owner: OtherPremiumId),
            Lesson(UnpublishedLessonId, "Hidden lesson", "A1", "Daily", published: false, owner: null));
        await db.SaveChangesAsync();
        return factory;
    }

    private static User User(int id, string email, string role) => new()
    {
        UserId = id, Email = email, PasswordHash = "hash", FullName = email, Role = role, Status = UserStatus.Online, CreatedAt = DateTime.UtcNow,
    };

    private static ListeningLesson Lesson(int id, string title, string level, string topic, bool published, int? owner)
    {
        var lesson = new ListeningLesson
        {
            Id = id, Title = title, Level = level, Topic = topic, YoutubeId = "dQw4w9WgXcQ",
            IsPublished = published, OwnerUserId = owner, CreatedAt = DateTime.UtcNow,
        };
        lesson.TranscriptLines.Add(new ListeningTranscriptLine { OrderIndex = 1, Speaker = "A", Text = "Can I have a latte to go?", VietnameseMeaning = "Cho tôi một ly latte mang đi?", StartTime = 0, EndTime = 4 });
        lesson.QuizQuestions.Add(new ListeningQuizQuestion { OrderIndex = 1, QuestionText = "Q", OptionA = "a", OptionB = "b", OptionC = "c", OptionD = "d", CorrectAnswer = "A" });
        lesson.VocabItems.Add(new ListeningVocabItem { OrderIndex = 1, Word = "order", Definition = "đặt hàng", ExampleSentence = "I would like to order a coffee." });
        lesson.VocabItems.Add(new ListeningVocabItem { OrderIndex = 2, Word = "to go", Definition = "mang đi", ExampleSentence = "A coffee to go, please." });
        lesson.VocabItems.Add(new ListeningVocabItem { OrderIndex = 3, Word = "latte", Definition = "cà phê sữa", ExampleSentence = null });
        return lesson;
    }

    private static IListeningWordService Words(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IListeningWordService>();

    private static string Letters(int number)
    {
        var sb = new StringBuilder();
        do { sb.Insert(0, (char)('a' + number % 26)); number /= 26; } while (number > 0);
        return sb.ToString();
    }

    /// <summary>A letters-only word that no other test uses (the lookup cache and the rate limiter are process-wide).</summary>
    private static string UniqueWord(string prefix)
        => prefix + new string(Guid.NewGuid().ToString("N").Take(10).Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))).ToArray());

    private static ListeningWordSaveRequest Save(string term, string definition = "nghĩa", string? example = null)
        => new() { LessonId = PublicLessonId, Term = term, Definition = definition, Example = example };

    // ------------------------------------------------------------------ 4.1 the C2 fix

    [Fact]
    public async Task CatalogDeepLink_LevelC2_PreselectsC2()
    {
        await using var factory = await CreateFactoryAsync(new FakeSuggestService());
        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var upper = await guest.GetStringAsync("/Home/Listening?level=C2");
        Assert.Contains("\"selectedLevel\":\"C2\"", upper, StringComparison.Ordinal);

        var lower = await guest.GetStringAsync("/Home/Listening?level=c2");
        Assert.Contains("\"selectedLevel\":\"C2\"", lower, StringComparison.Ordinal);

        var invalid = await guest.GetStringAsync("/Home/Listening?level=C3");
        Assert.Contains("\"selectedLevel\":\"all\"", invalid, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the shared word-form rule

    [Fact]
    public void BaseForms_AgreesWithTheSharedFixtureThatTheJavaScriptCoreAlsoChecks()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "listening-base-forms.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(20, cases.Count);

        foreach (var item in cases)
        {
            var input = item.GetProperty("input").GetString();
            var expected = item.GetProperty("forms").EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Equal(expected, ListeningWordForms.BaseForms(input));
        }

        Assert.Empty(ListeningWordForms.BaseForms(null));
        Assert.Empty(ListeningWordForms.BaseForms("  "));
    }

    // ------------------------------------------------------------------ 4.4 lookup

    [Fact]
    public async Task Lookup_LessonVocabExactMatch_IsAnsweredFromTheLessonForEveryRole_WithoutTheAi()
    {
        var fake = new FakeSuggestService();
        await using var factory = await CreateFactoryAsync(fake);
        using var scope = factory.Services.CreateScope();
        var words = Words(scope);

        foreach (int? user in new int?[] { null, StandardId, OtherPremiumId, AdminId })
        {
            var result = await words.LookupAsync(PublicLessonId, "Order", user);
            Assert.Equal(ListeningWordLookupStatus.Found, result.Status);
            Assert.Equal("lesson", result.Source);
            Assert.Equal("order", result.Word);
            Assert.Equal("đặt hàng", result.Definition);
            Assert.Equal("I would like to order a coffee.", result.Example);
        }

        Assert.Equal(0, fake.Calls);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("ordered")]
    [InlineData("ordering")]
    [InlineData("Order's")]
    public async Task Lookup_InflectionOfALessonVocabWord_MatchesTheBaseWord(string term)
    {
        var fake = new FakeSuggestService();
        await using var factory = await CreateFactoryAsync(fake);
        using var scope = factory.Services.CreateScope();

        var result = await Words(scope).LookupAsync(PublicLessonId, term, StandardId);
        Assert.Equal(ListeningWordLookupStatus.Found, result.Status);
        Assert.Equal("lesson", result.Source);
        Assert.Equal("order", result.Word);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Lookup_StandardAndGuest_NonVocabWord_IsLockedAndTheAiIsNeverCalled()
    {
        var fake = new FakeSuggestService();
        await using var factory = await CreateFactoryAsync(fake);
        using var scope = factory.Services.CreateScope();
        var words = Words(scope);
        var term = UniqueWord("locked");

        foreach (int? user in new int?[] { null, StandardId })
        {
            var result = await words.LookupAsync(PublicLessonId, term, user);
            Assert.Equal(ListeningWordLookupStatus.Found, result.Status);
            Assert.Equal("locked", result.Source);
            Assert.True(result.LineTranslationOnly);
            Assert.Null(result.Definition);
        }

        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Lookup_PremiumAndAdmin_NonVocabWord_UsesTheAiOnceAndThenTheCache()
    {
        var fake = new FakeSuggestService();
        await using var factory = await CreateFactoryAsync(fake);
        using var scope = factory.Services.CreateScope();
        var words = Words(scope);
        var term = UniqueWord("cache");

        var first = await words.LookupAsync(PublicLessonId, term, OtherPremiumId);
        Assert.Equal(ListeningWordLookupStatus.Found, first.Status);
        Assert.Equal("dictionary", first.Source);
        Assert.Equal(term, first.Word);
        Assert.Equal("/fake/", first.Phonetic);
        Assert.Equal($"nghĩa của {term}", first.Definition); // the first definition
        Assert.Equal($"An example with {term}.", first.Example);
        Assert.Equal(1, fake.Calls);

        var second = await words.LookupAsync(PublicLessonId, term.ToUpperInvariant(), AdminId);
        Assert.Equal("dictionary", second.Source);
        Assert.Equal(first.Definition, second.Definition);
        Assert.Equal(1, fake.Calls); // served from the 24 h cache, whatever the case or the caller
    }

    [Fact]
    public async Task Lookup_Premium_IsRateLimitedAfter30AiLookups_ButCachedAndLessonWordsStillAnswer()
    {
        var fake = new FakeSuggestService();
        await using var factory = await CreateFactoryAsync(fake);
        using var scope = factory.Services.CreateScope();
        var words = Words(scope);
        var prefix = UniqueWord("rate");

        string Term(int i) => prefix + Letters(i);
        for (var i = 0; i < 30; i++)
        {
            var ok = await words.LookupAsync(PublicLessonId, Term(i), FastPremiumId);
            Assert.Equal(ListeningWordLookupStatus.Found, ok.Status);
        }

        Assert.Equal(30, fake.Calls);

        var limited = await words.LookupAsync(PublicLessonId, Term(30), FastPremiumId);
        Assert.Equal(ListeningWordLookupStatus.RateLimited, limited.Status);
        Assert.True(limited.RetryAfterSeconds > 0);
        Assert.Equal(30, fake.Calls); // a refused lookup never reaches the provider

        var cached = await words.LookupAsync(PublicLessonId, Term(3), FastPremiumId);
        Assert.Equal(ListeningWordLookupStatus.Found, cached.Status);
        var lesson = await words.LookupAsync(PublicLessonId, "latte", FastPremiumId);
        Assert.Equal("lesson", lesson.Source);

        // The limit is per user: an admin is not affected.
        var other = await words.LookupAsync(PublicLessonId, Term(31), AdminId);
        Assert.Equal(ListeningWordLookupStatus.Found, other.Status);
    }

    [Fact]
    public async Task Lookup_AiFailure_IsReportedAndNeverCached()
    {
        var fake = new FakeSuggestService { Fail = true };
        await using var factory = await CreateFactoryAsync(fake);
        using var scope = factory.Services.CreateScope();
        var words = Words(scope);
        var term = UniqueWord("fail");

        var failed = await words.LookupAsync(PublicLessonId, term, OtherPremiumId);
        Assert.Equal(ListeningWordLookupStatus.ProviderFailed, failed.Status);
        Assert.Null(failed.Definition);

        fake.Fail = false;
        var retried = await words.LookupAsync(PublicLessonId, term, OtherPremiumId);
        Assert.Equal(ListeningWordLookupStatus.Found, retried.Status);
        Assert.Equal(2, fake.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>")]
    [InlineData("123")]
    [InlineData("a b")]
    [InlineData("-dash")]
    [InlineData("'quote")]
    [InlineData("café<b>")]
    public async Task Lookup_InvalidTerm_IsRejectedBeforeAnythingElse(string? term)
    {
        var fake = new FakeSuggestService();
        await using var factory = await CreateFactoryAsync(fake);
        using var scope = factory.Services.CreateScope();

        var result = await Words(scope).LookupAsync(PublicLessonId, term, OtherPremiumId);
        Assert.Equal(ListeningWordLookupStatus.InvalidTerm, result.Status);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Lookup_TermLength_IsLimitedTo40Characters_AndShapesWithApostropheAndHyphenAreAccepted()
    {
        var fake = new FakeSuggestService();
        await using var factory = await CreateFactoryAsync(fake);
        using var scope = factory.Services.CreateScope();
        var words = Words(scope);

        Assert.Equal(ListeningWordLookupStatus.InvalidTerm, (await words.LookupAsync(PublicLessonId, new string('a', 41), OtherPremiumId)).Status);
        Assert.Equal(ListeningWordLookupStatus.Found, (await words.LookupAsync(PublicLessonId, new string('b', 40), OtherPremiumId)).Status);
        Assert.Equal(ListeningWordLookupStatus.Found, (await words.LookupAsync(PublicLessonId, "well-known", OtherPremiumId)).Status);
        Assert.Equal(ListeningWordLookupStatus.Found, (await words.LookupAsync(PublicLessonId, "don’t", OtherPremiumId)).Status);
    }

    [Fact]
    public async Task Lookup_And_Save_LessonsTheCallerMayNotUse_AreNotFound()
    {
        var fake = new FakeSuggestService();
        await using var factory = await CreateFactoryAsync(fake);
        using var scope = factory.Services.CreateScope();
        var words = Words(scope);

        // another user's private lesson, an unpublished lesson, a missing lesson, and a guest asking for a private lesson
        foreach (var (lessonId, user) in new (int, int?)[] { (PrivateLessonId, StandardId), (PrivateLessonId, AdminId), (PrivateLessonId, null), (UnpublishedLessonId, AdminId), (999999, StandardId) })
        {
            var lookup = await words.LookupAsync(lessonId, "order", user);
            Assert.Equal(ListeningWordLookupStatus.NotFound, lookup.Status);
            Assert.Null(lookup.Definition);

            if (user is int uid)
            {
                var save = await words.SaveAsync(lessonId, new ListeningWordSaveRequest { LessonId = lessonId, Term = "order", Definition = "đặt" }, uid);
                Assert.Equal(ListeningWordSaveStatus.NotFound, save.Status);
            }
        }

        // the owner can use it
        var own = await words.LookupAsync(PrivateLessonId, UniqueWord("mine"), OtherPremiumId);
        Assert.Equal(ListeningWordLookupStatus.Found, own.Status);

        using var db = factory.Services.CreateScope();
        var context = db.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Empty(await context.Sets.Where(s => s.SetName == SetName && s.OwnerId != OtherPremiumId).ToListAsync());
    }

    // ------------------------------------------------------------------ 4.4 save

    [Fact]
    public async Task Save_CreatesThePrivateSetOnce_AppendsLaterWords_AndFlagsDuplicates()
    {
        await using var factory = await CreateFactoryAsync(new FakeSuggestService());
        using var scope = factory.Services.CreateScope();
        var words = Words(scope);

        var first = await words.SaveAsync(PublicLessonId, Save("order", "đặt hàng", "I would like to order a coffee."), StandardId);
        Assert.Equal(ListeningWordSaveStatus.Saved, first.Status);
        Assert.NotNull(first.SetId);
        Assert.Equal($"/Sets/{first.SetId}", first.SetUrl);

        var second = await words.SaveAsync(PublicLessonId, Save("to go", "mang đi"), StandardId);
        Assert.Equal(ListeningWordSaveStatus.Saved, second.Status);
        Assert.Equal(first.SetId, second.SetId);

        var duplicate = await words.SaveAsync(PublicLessonId, Save("ORDER", "đặt hàng"), StandardId);
        Assert.Equal(ListeningWordSaveStatus.Duplicate, duplicate.Status);
        Assert.Equal(first.SetId, duplicate.SetId);
        Assert.Equal($"/Sets/{first.SetId}", duplicate.SetUrl);

        using var checkScope = factory.Services.CreateScope();
        var db = checkScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var sets = await db.Sets.AsNoTracking().Where(s => s.OwnerId == StandardId && s.SetName == SetName).ToListAsync();
        var set = Assert.Single(sets);
        Assert.Equal("Private", set.Visibility);
        Assert.Null(set.FolderId);
        var cards = await db.Cards.AsNoTracking().Where(c => c.SetId == set.SetId).OrderBy(c => c.CardId).ToListAsync();
        Assert.Equal(new[] { "order", "to go" }, cards.Select(c => c.Term).ToArray());
        Assert.Equal("đặt hàng", cards[0].Definition);
        Assert.Equal("I would like to order a coffee.", cards[0].Example);
        Assert.Equal("Daily", cards[0].Topic);
        Assert.Equal("A1", cards[0].Cefr);
        Assert.Null(cards[1].Example);
    }

    [Fact]
    public async Task Save_NeverUsesASetOfAnotherUser_EvenWithTheSameName()
    {
        await using var factory = await CreateFactoryAsync(new FakeSuggestService());
        int otherSetId;
        using (var seedScope = factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var set = new Set { SetName = SetName, OwnerId = OtherPremiumId, Visibility = "Private", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Sets.Add(set);
            await db.SaveChangesAsync();
            otherSetId = set.SetId;
        }

        using var scope = factory.Services.CreateScope();
        var result = await Words(scope).SaveAsync(PublicLessonId, Save("latte", "cà phê sữa"), StandardId);
        Assert.Equal(ListeningWordSaveStatus.Saved, result.Status);
        Assert.NotEqual(otherSetId, result.SetId);

        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Empty(await check.Cards.Where(c => c.SetId == otherSetId).ToListAsync());
        Assert.Single(await check.Cards.Where(c => c.SetId == result.SetId).ToListAsync());
        Assert.Equal(StandardId, (await check.Sets.SingleAsync(s => s.SetId == result.SetId)).OwnerId);

        // and when the other user saves, their own set is used
        var theirs = await Words(scope).SaveAsync(PublicLessonId, Save("order", "đặt hàng"), OtherPremiumId);
        Assert.Equal(otherSetId, theirs.SetId);
    }

    [Theory]
    [InlineData("", "nghĩa")]
    [InlineData("<b>x</b>", "nghĩa")]
    [InlineData("order", "")]
    [InlineData("order", "   ")]
    public async Task Save_InvalidTermOrDefinition_IsRejectedAndWritesNothing(string term, string definition)
    {
        await using var factory = await CreateFactoryAsync(new FakeSuggestService());
        using var scope = factory.Services.CreateScope();

        var result = await Words(scope).SaveAsync(PublicLessonId, Save(term, definition), StandardId);
        Assert.Equal(ListeningWordSaveStatus.Invalid, result.Status);

        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Empty(await db.Sets.Where(s => s.OwnerId == StandardId).ToListAsync());
    }

    [Fact]
    public async Task Save_LimitsDefinitionAndExampleLength_AndAcceptsAPhraseTerm()
    {
        await using var factory = await CreateFactoryAsync(new FakeSuggestService());
        using var scope = factory.Services.CreateScope();
        var words = Words(scope);

        Assert.Equal(ListeningWordSaveStatus.Invalid, (await words.SaveAsync(PublicLessonId, Save("order", new string('d', 301)), StandardId)).Status);
        Assert.Equal(ListeningWordSaveStatus.Invalid, (await words.SaveAsync(PublicLessonId, Save("order", "ok", new string('e', 301)), StandardId)).Status);
        Assert.Equal(ListeningWordSaveStatus.Saved, (await words.SaveAsync(PublicLessonId, Save("break the ice", new string('d', 300), new string('e', 300)), StandardId)).Status);
        Assert.Equal(ListeningWordSaveStatus.Invalid, (await words.SaveAsync(PublicLessonId, Save("one two three four five six", "ok"), StandardId)).Status);
    }

    // ------------------------------------------------------------------ 4.2 view-model additions

    [Fact]
    public async Task PracticeViewModel_ExposesTheLookupFlagAndTheWordSetUrl()
    {
        await using var factory = await CreateFactoryAsync(new FakeSuggestService());
        using var scope = factory.Services.CreateScope();
        var listening = scope.ServiceProvider.GetRequiredService<IListeningService>();

        var guest = await listening.GetPracticeViewModelAsync(PublicLessonId, null);
        var standard = await listening.GetPracticeViewModelAsync(PublicLessonId, StandardId);
        var premium = await listening.GetPracticeViewModelAsync(PublicLessonId, OtherPremiumId);
        var admin = await listening.GetPracticeViewModelAsync(PublicLessonId, AdminId);
        Assert.False(guest!.CanLookupAnyWord);
        Assert.False(standard!.CanLookupAnyWord);
        Assert.True(premium!.CanLookupAnyWord);
        Assert.True(admin!.CanLookupAnyWord);
        Assert.Null(guest.WordSetUrl);
        Assert.Null(standard.WordSetUrl); // read-only: opening the page never creates the set

        var saved = await Words(scope).SaveAsync(PublicLessonId, Save("latte", "cà phê sữa"), StandardId);
        var after = await listening.GetPracticeViewModelAsync(PublicLessonId, StandardId);
        Assert.Equal($"/Sets/{saved.SetId}", after!.WordSetUrl);
        Assert.Null((await listening.GetPracticeViewModelAsync(PublicLessonId, OtherPremiumId))!.WordSetUrl);
    }

    [Fact]
    public async Task IndexViewModel_ReportsCompletedCountPerLevel_AndTheAverageQuizScore()
    {
        await using var factory = await CreateFactoryAsync(new FakeSuggestService());
        using (var seedScope = factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.ListeningLessons.Add(Lesson(93010, "A1 second", "A1", "Daily", published: true, owner: null));
            db.ListeningLessons.Add(Lesson(93011, "B2 first", "B2", "Work", published: true, owner: null));
            await db.SaveChangesAsync();
            db.UserListeningProgresses.AddRange(
                new UserListeningProgress { UserId = StandardId, LessonId = PublicLessonId, TranscriptCompleted = true, QuizCompleted = true, QuizScore = 80, VocabReviewed = true, CompletedAt = DateTime.UtcNow, LastAccessedAt = DateTime.UtcNow },
                new UserListeningProgress { UserId = StandardId, LessonId = 93010, TranscriptCompleted = true, QuizCompleted = true, QuizScore = 60, VocabReviewed = true, CompletedAt = DateTime.UtcNow, LastAccessedAt = DateTime.UtcNow },
                new UserListeningProgress { UserId = StandardId, LessonId = 93011, TranscriptCompleted = true, LastAccessedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var listening = scope.ServiceProvider.GetRequiredService<IListeningService>();

        var user = await listening.GetIndexViewModelAsync(null, null, StandardId);
        Assert.Equal(2, user.CompletedLessons);
        Assert.Equal(70, user.AverageQuizScore); // (80 + 60) / 2, finished public lessons only
        Assert.Equal(2, user.Levels.Single(l => l.Key == "A1").CompletedCount);
        Assert.Equal(2, user.Levels.Single(l => l.Key == "A1").LessonCount);
        Assert.Equal(0, user.Levels.Single(l => l.Key == "B2").CompletedCount); // started, not finished
        Assert.Equal(1, user.Levels.Single(l => l.Key == "B2").LessonCount);

        var guest = await listening.GetIndexViewModelAsync(null, null, null);
        Assert.Null(guest.AverageQuizScore);
        Assert.All(guest.Levels, l => Assert.Equal(0, l.CompletedCount));

        var nobody = await listening.GetIndexViewModelAsync(null, null, OtherPremiumId);
        Assert.Null(nobody.AverageQuizScore);
    }

    // ------------------------------------------------------------------ HTTP contract (4.3)

    private static HttpClient Client(TestWebApplicationFactory factory, int userId, string role)
        => IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, role);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<HttpResponseMessage> PostSaveAsync(HttpClient client, object body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/listening/word/save")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        if (token != null) request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Http_Lookup_MapsEveryOutcomeToItsStatusCode()
    {
        var fake = new FakeSuggestService();
        await using var factory = await CreateFactoryAsync(fake);
        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var premium = Client(factory, OtherPremiumId, Roles.Premium);

        using var lesson = await guest.GetAsync($"/api/listening/word?lessonId={PublicLessonId}&term=order");
        Assert.Equal(HttpStatusCode.OK, lesson.StatusCode);
        var body = await JsonAsync(lesson);
        Assert.Equal("lesson", body.GetProperty("source").GetString());
        Assert.Equal("đặt hàng", body.GetProperty("definition").GetString());

        using var locked = await guest.GetAsync($"/api/listening/word?lessonId={PublicLessonId}&term={UniqueWord("httplock")}");
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        var lockedBody = await JsonAsync(locked);
        Assert.Equal("locked", lockedBody.GetProperty("source").GetString());
        Assert.True(lockedBody.GetProperty("lineTranslationOnly").GetBoolean());

        using var invalid = await guest.GetAsync($"/api/listening/word?lessonId={PublicLessonId}&term=%3Cscript%3E");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("Từ không hợp lệ.", (await JsonAsync(invalid)).GetProperty("error").GetString());

        using var missing = await guest.GetAsync($"/api/listening/word?lessonId={PrivateLessonId}&term=order");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var dictionary = await premium.GetAsync($"/api/listening/word?lessonId={PublicLessonId}&term={UniqueWord("httpdict")}");
        Assert.Equal(HttpStatusCode.OK, dictionary.StatusCode);
        Assert.Equal("dictionary", (await JsonAsync(dictionary)).GetProperty("source").GetString());

        fake.Fail = true;
        using var failed = await premium.GetAsync($"/api/listening/word?lessonId={PublicLessonId}&term={UniqueWord("httpfail")}");
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal("Chưa tra được từ này. Thử lại sau.", (await JsonAsync(failed)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Http_Lookup_RateLimitAnswers429WithTheVietnameseMessageAndRetryAfter()
    {
        var fake = new FakeSuggestService();
        await using var factory = await CreateFactoryAsync(fake);
        using var premium = Client(factory, FastPremiumId + 100, Roles.Premium);
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.Users.Add(User(FastPremiumId + 100, "word-fast@test.local", Roles.Premium));
            await db.SaveChangesAsync();
        }

        var prefix = UniqueWord("httprate");
        HttpResponseMessage? last = null;
        for (var i = 0; i < 31; i++)
        {
            last?.Dispose();
            last = await premium.GetAsync($"/api/listening/word?lessonId={PublicLessonId}&term={prefix}{Letters(i)}");
            if (i < 30) Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        }

        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        Assert.Equal("Bạn tra từ hơi nhanh, thử lại sau ít phút.", (await JsonAsync(last)).GetProperty("error").GetString());
        Assert.True(last.Headers.Contains("Retry-After"));
        last.Dispose();
    }

    [Fact]
    public async Task Http_Save_GuestIsUnauthorized_MissingTokenIsBadRequest_AndASignedInSaveWorks()
    {
        await using var factory = await CreateFactoryAsync(new FakeSuggestService());
        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var student = Client(factory, StandardId, Roles.Standard);
        var body = new { lessonId = PublicLessonId, term = "latte", definition = "cà phê sữa", example = (string?)null };

        using var guestResponse = await PostSaveAsync(guest, body, null);
        Assert.Equal(HttpStatusCode.Unauthorized, guestResponse.StatusCode);

        using var noToken = await PostSaveAsync(student, body, null);
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(student, $"/Home/Listening/Practice/{PublicLessonId}");
        using var saved = await PostSaveAsync(student, body, token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedBody = await JsonAsync(saved);
        Assert.True(savedBody.GetProperty("saved").GetBoolean());
        Assert.StartsWith("/Sets/", savedBody.GetProperty("setUrl").GetString());

        using var duplicate = await PostSaveAsync(student, body, token);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var duplicateBody = await JsonAsync(duplicate);
        Assert.False(duplicateBody.GetProperty("saved").GetBoolean());
        Assert.True(duplicateBody.GetProperty("duplicate").GetBoolean());

        using var invalid = await PostSaveAsync(student, new { lessonId = PublicLessonId, term = "<b>", definition = "x" }, token);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var hidden = await PostSaveAsync(student, new { lessonId = PrivateLessonId, term = "latte", definition = "x" }, token);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        using var emptyBody = new HttpRequestMessage(HttpMethod.Post, "/api/listening/word/save") { Content = new StringContent("null", Encoding.UTF8, "application/json") };
        emptyBody.Headers.Add("RequestVerificationToken", token);
        using var nullResponse = await student.SendAsync(emptyBody);
        Assert.Equal(HttpStatusCode.BadRequest, nullResponse.StatusCode);
    }
}
