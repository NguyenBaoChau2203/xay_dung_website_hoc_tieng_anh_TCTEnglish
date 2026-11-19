using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Services.Onboarding;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>S3: state, prompts, preview, creating the decks, idempotency, rollback and refill.</summary>
public sealed class OnboardingServiceTests
{
    private static readonly DateTime Start = new(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);

    private const int NewUser = 901;
    private const int OldUser = 902;
    private const int UserWithSet = 903;
    private const int UserWithProgress = 904;
    private const int OtherUser = 905;

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(Start);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan span) => _now += span;
    }

    /// <summary>Fails the <c>failOn</c>-th SaveChanges after it is armed (to prove the transaction rolls back).</summary>
    private sealed class FailNthSave : SaveChangesInterceptor
    {
        private int _seen;
        public bool Armed { get; set; }
        public int FailOn { get; set; } = 2;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && ++_seen == FailOn)
            {
                throw new InvalidOperationException("simulated failure");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed record Env(TestWebApplicationFactory Factory, Clock Clock) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Factory.DisposeAsync();

        /// <summary>A fresh scope and service, like a new request.</summary>
        public (IServiceScope Scope, OnboardingService Service, DbflashcardContext Db) Request()
        {
            var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            return (scope, new OnboardingService(db, Clock), db);
        }
    }

    private static async Task<Env> CreateAsync(FailNthSave? fail = null, CountingDbCommandInterceptor? counter = null)
    {
        var factory = new TestWebApplicationFactory();
        if (fail != null) { factory.SaveInterceptors.Add(fail); }
        if (counter != null) { factory.SaveInterceptors.Add(counter); }
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var systemId = await db.Users.Where(u => u.Email == "system@tct.local").Select(u => u.UserId).SingleAsync();

        foreach (var (id, created) in new[]
        {
            (NewUser, Start.AddHours(-1)), (OldUser, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            (UserWithSet, Start.AddHours(-1)), (UserWithProgress, Start.AddHours(-1)), (OtherUser, Start.AddHours(-1))
        })
        {
            db.Users.Add(new User
            {
                UserId = id, Email = $"onb{id}@test.local", PasswordHash = "hash", FullName = "Onboarding " + id,
                Role = Roles.Standard, Status = UserStatus.Online, CreatedAt = created
            });
        }

        await db.SaveChangesAsync();

        // System content: four topics under their exact catalogue names, every card fully populated.
        SystemSet(db, systemId, "Phương tiện giao thông", "veh", 30, "A1", "A2");
        SystemSet(db, systemId, "Địa điểm trong thành phố", "place", 30, "A1", "A2");
        SystemSet(db, systemId, "Du lịch & sân bay", "air", 30, "B1", "B2");
        SystemSet(db, systemId, "Hỏi và chỉ đường", "phrase", 20, "A1", "A2");
        await db.SaveChangesAsync();

        db.Sets.Add(new Set { OwnerId = UserWithSet, SetName = "Already mine", Visibility = "Private", CreatedAt = Start, UpdatedAt = Start });
        var firstSystemCard = await db.Cards.OrderBy(c => c.CardId).Select(c => c.CardId).FirstAsync();
        db.LearningProgresses.Add(new LearningProgress { UserId = UserWithProgress, CardId = firstSystemCard, Status = "Learning", RepetitionCount = 1 });
        await db.SaveChangesAsync();

        return new Env(factory, new Clock());
    }

    private static void SystemSet(DbflashcardContext db, int systemId, string name, string prefix, int count, string cefrA, string cefrB)
    {
        var set = new Set { OwnerId = systemId, SetName = name, Visibility = "Public", CreatedAt = Start, UpdatedAt = Start, Description = "#test" };
        for (var i = 1; i <= count; i++)
        {
            set.Cards.Add(new Card
            {
                Term = $"{prefix}{i:D2}",
                Definition = $"nghĩa {prefix}{i:D2}",
                ImageUrl = $"/uploads/shared/{prefix}{i:D2}.webp",
                Phonetic = $"/{prefix}{i}/",
                PhoneticUk = $"/{prefix}{i}uk/",
                Example = $"Example of {prefix}{i:D2}.",
                ExampleTranslation = $"Ví dụ {prefix}{i:D2}.",
                Topic = name,
                PartsOfSpeech = "noun",
                MeaningEn = $"meaning {prefix}{i:D2}",
                Cefr = i % 2 == 0 ? cefrB : cefrA,
                Emoji = "🚌",
                DetailsJson = "{\"synonyms\":[\"x\"]}",
                ImageSourceJson = "{\"provider\":\"commons\"}",
                AiGenerated = i == 3,
                PersonalNote = "system note " + i,
                CreatedAt = Start,
                UpdatedAt = Start
            });
        }

        db.Sets.Add(set);
    }

    private static OnboardingChoices Choices(string[]? goals = null, string[]? topics = null, string level = "A2", int pace = 10) =>
        new(goals ?? new[] { "travel", "communication" }, topics ?? new[] { "A16", "A15", "C04" }, level, pace);

    // ── state and prompts ────────────────────────────────────────────────

    [Fact]
    public async Task NewAccount_IsPending_AndPromptedOnce()
    {
        await using var env = await CreateAsync();
        var (scope, service, _) = env.Request();
        using var _ = scope;

        var state = await service.GetStateAsync(NewUser);
        Assert.True(state.ShouldPrompt);
        Assert.Equal(OnboardingStatus.Pending, state.Status);
        Assert.False(state.HasDecks);
    }

    [Theory]
    [InlineData(OldUser)]          // created before the feature date
    [InlineData(UserWithSet)]      // already has a set
    [InlineData(UserWithProgress)] // already has learning progress
    public async Task ExistingAccounts_AreNeverPromptedAutomatically(int userId)
    {
        await using var env = await CreateAsync();
        var (scope, service, db) = env.Request();
        using var _ = scope;

        var state = await service.GetStateAsync(userId);
        Assert.False(state.ShouldPrompt);
        Assert.Equal(OnboardingStatus.NotEligible, state.Status);
        Assert.Equal(OnboardingStatus.NotEligible, (await db.UserOnboardings.SingleAsync(x => x.UserId == userId)).Status);

        await service.MarkPromptedAsync(userId);
        Assert.False((await service.GetStateAsync(userId)).ShouldPrompt);
    }

    [Fact]
    public async Task UnknownUser_GetsNoRowAndNoPrompt()
    {
        await using var env = await CreateAsync();
        var (scope, service, db) = env.Request();
        using var _ = scope;

        Assert.False((await service.GetStateAsync(424242)).ShouldPrompt);
        Assert.Equal(0, await db.UserOnboardings.CountAsync(x => x.UserId == 424242));
    }

    [Fact]
    public async Task Prompts_AreSpacedByTwentyHours_AndStopAfterTwo()
    {
        await using var env = await CreateAsync();

        async Task<(bool Should, int Count)> Check(Func<OnboardingService, Task>? act = null)
        {
            var (scope, service, db) = env.Request();
            using var _ = scope;
            if (act != null) { await act(service); }
            var state = await service.GetStateAsync(NewUser);
            return (state.ShouldPrompt, (await db.UserOnboardings.AsNoTracking().SingleAsync(x => x.UserId == NewUser)).PromptCount);
        }

        Assert.Equal((true, 0), await Check());
        Assert.Equal((false, 1), await Check(s => s.MarkPromptedAsync(NewUser)));
        Assert.Equal((false, 1), await Check(s => s.MarkPromptedAsync(NewUser))); // a second call in the window counts nothing

        env.Clock.Advance(TimeSpan.FromHours(19));
        Assert.False((await Check()).Should);
        env.Clock.Advance(TimeSpan.FromHours(1.5));
        Assert.True((await Check()).Should);

        Assert.Equal((false, 2), await Check(s => s.MarkPromptedAsync(NewUser)));
        env.Clock.Advance(TimeSpan.FromDays(30));
        Assert.False((await Check()).Should); // two prompts is the limit
    }

    [Fact]
    public async Task SecondSkip_EndsThePrompts_FirstOneDoesNot()
    {
        await using var env = await CreateAsync();

        async Task<string> Skip(bool prompted)
        {
            var (scope, service, _) = env.Request();
            using var _ = scope;
            if (prompted) { await service.MarkPromptedAsync(NewUser); }
            return await service.SkipAsync(NewUser);
        }

        Assert.Equal(OnboardingStatus.Pending, await Skip(prompted: true));
        env.Clock.Advance(TimeSpan.FromHours(21));
        Assert.Equal(OnboardingStatus.Skipped, await Skip(prompted: true));

        var (scope2, service2, _) = env.Request();
        using var _2 = scope2;
        env.Clock.Advance(TimeSpan.FromDays(10));
        Assert.False((await service2.GetStateAsync(NewUser)).ShouldPrompt);
        Assert.Equal(OnboardingStatus.Skipped, await service2.SkipAsync(NewUser));
    }

    [Fact]
    public async Task ASkipWithoutARecordedPrompt_StillCountsAsOne()
    {
        await using var env = await CreateAsync();
        var (scope, service, db) = env.Request();
        using var _ = scope;

        Assert.Equal(OnboardingStatus.Pending, await service.SkipAsync(NewUser));
        Assert.Equal(1, (await db.UserOnboardings.SingleAsync(x => x.UserId == NewUser)).PromptCount);
        Assert.False((await service.GetStateAsync(NewUser)).ShouldPrompt);
    }

    // ── preview ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_BuildsDecks_Deterministically_WithTheDaysEstimate()
    {
        await using var env = await CreateAsync();
        var (scope, service, _) = env.Request();
        using var _ = scope;

        var first = await service.BuildPreviewAsync(NewUser, Choices());
        var second = await service.BuildPreviewAsync(NewUser, Choices());

        Assert.True(first.IsSuccess);
        Assert.Equal(
            first.Value!.Decks.SelectMany(d => d.Cards.Select(c => c.CardId)),
            second.Value!.Decks.SelectMany(d => d.Cards.Select(c => c.CardId)));
        Assert.Equal(new[] { "Du lịch – bộ khởi đầu", "Câu giao tiếp khi đi du lịch" }, first.Value.Decks.Select(d => d.Name));
        Assert.Equal(first.Value.Decks.Sum(d => d.Cards.Count), first.Value.Total);
        Assert.Equal((first.Value.Total + 9) / 10, first.Value.Days);
        Assert.All(first.Value.Decks.SelectMany(d => d.Cards), c => Assert.StartsWith("/uploads/shared/", c.ImageUrl));
    }

    [Theory]
    [InlineData(new string[0], new[] { "A16", "A15", "C04" }, "A2", 10)]
    [InlineData(new[] { "travel", "work", "study", "hobbies" }, new[] { "A16", "A15", "C04" }, "A2", 10)]
    [InlineData(new[] { "travel", "travel" }, new[] { "A16", "A15", "C04" }, "A2", 10)]
    [InlineData(new[] { "nonsense" }, new[] { "A16", "A15", "C04" }, "A2", 10)]
    [InlineData(new[] { "travel" }, new[] { "A16", "A15" }, "A2", 10)]
    [InlineData(new[] { "travel" }, new[] { "A01", "A02", "A03", "A04", "A05", "A06", "A07", "A08", "A09" }, "A2", 10)]
    [InlineData(new[] { "travel" }, new[] { "A16", "A15", "ZZ9" }, "A2", 10)]
    [InlineData(new[] { "travel" }, new[] { "A16", "A15", "C04" }, "C2", 10)]
    [InlineData(new[] { "travel" }, new[] { "A16", "A15", "C04" }, "A2", 7)]
    public async Task Preview_RejectsInvalidChoices(string[] goals, string[] topics, string level, int pace)
    {
        await using var env = await CreateAsync();
        var (scope, service, _) = env.Request();
        using var _ = scope;

        var result = await service.BuildPreviewAsync(NewUser, new OnboardingChoices(goals, topics, level, pace));
        Assert.Equal(OnboardingOutcome.Invalid, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public async Task Preview_ExcludesTermsTheUserAlreadyHas()
    {
        await using var env = await CreateAsync();
        var (scope, service, db) = env.Request();
        using var _ = scope;
        var mine = new Set { OwnerId = NewUser, SetName = "Mine", Visibility = "Private", CreatedAt = Start, UpdatedAt = Start };
        mine.Cards.Add(new Card { Term = "VEH01", Definition = "x" }); // different case on purpose
        db.Sets.Add(mine);
        await db.SaveChangesAsync();

        var preview = await service.BuildPreviewAsync(NewUser, Choices());
        Assert.DoesNotContain(preview.Value!.Decks.SelectMany(d => d.Cards), c => c.Term == "veh01");
    }

    [Fact]
    public async Task Preview_QueryCountDoesNotGrowWithTheTopics()
    {
        var counter = new CountingDbCommandInterceptor();
        await using var env = await CreateAsync(counter: counter);

        async Task<int> Selects(string[] topics)
        {
            var (scope, service, _) = env.Request();
            using var _ = scope;
            await service.GetStateAsync(NewUser); // the row exists, so only the preview queries are counted
            counter.Reset();
            Assert.True((await service.BuildPreviewAsync(NewUser, Choices(new[] { "travel" }, topics))).IsSuccess);
            return counter.Counts.GetValueOrDefault("SELECT");
        }

        var few = await Selects(new[] { "A16", "A15", "B04" });
        var many = await Selects(new[] { "A16", "A15", "B04", "C04", "A01", "A02", "A03", "A04" }); // topics without sets are simply empty
        Assert.Equal(few, many);
        Assert.True(few <= 8, $"The preview used {few} SELECTs.");
    }

    // ── complete ─────────────────────────────────────────────────────────

    private static async Task<(OnboardingPreview Preview, CompleteRequest Request)> PrepareAsync(
        Env env, Guid? requestId = null, string[]? decksOff = null, int knownCount = 0)
    {
        var (scope, service, _) = env.Request();
        using var _ = scope;
        var preview = (await service.BuildPreviewAsync(NewUser, Choices())).Value!;
        var known = preview.Decks.SelectMany(d => d.Cards).Take(knownCount).Select(c => c.CardId).ToList();
        return (preview, new CompleteRequest(requestId ?? Guid.NewGuid(), Choices(), decksOff ?? Array.Empty<string>(), known));
    }

    [Fact]
    public async Task Complete_CreatesOrdinaryPrivateSets_InOneFolder_WithCompleteCardCopies_AndLeavesTheSystemUntouched()
    {
        await using var env = await CreateAsync();
        var (preview, request) = await PrepareAsync(env);

        string systemBefore;
        using (var scope = env.Factory.Services.CreateScope())
        {
            systemBefore = await SystemFingerprintAsync(scope.ServiceProvider.GetRequiredService<DbflashcardContext>());
        }

        var (scope2, service, db) = env.Request();
        using var _ = scope2;
        var result = await service.CompleteAsync(NewUser, request);
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(preview.Decks.Count, result.Value!.Decks.Count);

        var folder = await db.Folders.AsNoTracking().SingleAsync(f => f.UserId == NewUser);
        Assert.Equal("Lộ trình của tôi", folder.FolderName);
        Assert.Equal(folder.FolderId, result.Value.FolderId);

        var sets = await db.Sets.AsNoTracking().Include(s => s.Cards).Where(s => s.OwnerId == NewUser).ToListAsync();
        Assert.Equal(preview.Decks.Count, sets.Count);
        Assert.All(sets, s =>
        {
            Assert.Equal("Private", s.Visibility);
            Assert.Equal(folder.FolderId, s.FolderId);
            Assert.Null(s.ShareSlug);
            Assert.Null(s.ClonedFromSetId);
            Assert.StartsWith("Tạo từ sở thích · Nguồn: ", s.Description);
        });

        // every copied card equals its source field by field (the personal note is never copied)
        var sourceByTerm = await db.Cards.AsNoTracking().Where(c => c.Set.Owner.Email == "system@tct.local").ToDictionaryAsync(c => c.Term);
        foreach (var copy in sets.SelectMany(s => s.Cards))
        {
            var source = sourceByTerm[copy.Term];
            Assert.Equal(source.Definition, copy.Definition);
            Assert.Equal(source.ImageUrl, copy.ImageUrl);
            Assert.Equal(source.Phonetic, copy.Phonetic);
            Assert.Equal(source.PhoneticUk, copy.PhoneticUk);
            Assert.Equal(source.Example, copy.Example);
            Assert.Equal(source.ExampleTranslation, copy.ExampleTranslation);
            Assert.Equal(source.Topic, copy.Topic);
            Assert.Equal(source.PartsOfSpeech, copy.PartsOfSpeech);
            Assert.Equal(source.MeaningEn, copy.MeaningEn);
            Assert.Equal(source.Cefr, copy.Cefr);
            Assert.Equal(source.Emoji, copy.Emoji);
            Assert.Equal(source.DetailsJson, copy.DetailsJson);
            Assert.Equal(source.ImageSourceJson, copy.ImageSourceJson);
            Assert.Equal(source.AiGenerated, copy.AiGenerated);
            Assert.Null(copy.PersonalNote);
            Assert.NotEqual(source.CardId, copy.CardId);
            Assert.NotEqual(source.SetId, copy.SetId);
        }

        var row = await db.UserOnboardings.AsNoTracking().SingleAsync(x => x.UserId == NewUser);
        Assert.Equal(OnboardingStatus.Completed, row.Status);
        Assert.Equal("A2", row.Level);
        Assert.Equal(10, row.DailyNewWords);
        Assert.Equal(folder.FolderId, row.FolderId);
        Assert.Equal(sets.Count, JsonDocument.Parse(row.DecksJson!).RootElement.GetArrayLength());

        var state = await service.GetStateAsync(NewUser);
        Assert.Equal(OnboardingStatus.Completed, state.Status);
        Assert.False(state.ShouldPrompt);
        Assert.True(state.HasDecks);
        Assert.True(state.BannerVisible);

        using var scope3 = env.Factory.Services.CreateScope();
        Assert.Equal(systemBefore, await SystemFingerprintAsync(scope3.ServiceProvider.GetRequiredService<DbflashcardContext>()));
    }

    private static async Task<string> SystemFingerprintAsync(DbflashcardContext db)
    {
        var rows = await db.Cards.AsNoTracking().Where(c => c.Set.Owner.Email == "system@tct.local")
            .OrderBy(c => c.CardId)
            .Select(c => new { c.CardId, c.SetId, c.Term, c.Definition, c.ImageUrl, c.Phonetic, c.Example, c.Cefr, c.DetailsJson, c.PersonalNote })
            .ToListAsync();
        var sets = await db.Sets.AsNoTracking().Where(s => s.Owner.Email == "system@tct.local").OrderBy(s => s.SetId)
            .Select(s => new { s.SetId, s.SetName, s.Visibility, s.FolderId }).ToListAsync();
        return JsonSerializer.Serialize(new { rows, sets });
    }

    [Fact]
    public async Task Complete_DropsSwitchedOffDecks_AndKnownWords_AndRemembersThem()
    {
        await using var env = await CreateAsync();
        var (preview, request) = await PrepareAsync(env, knownCount: 3);
        var off = preview.Decks[1].Key;
        request = request with { DecksOff = new[] { off }, KnownCardIds = preview.Decks[0].Cards.Take(3).Select(c => c.CardId).ToList() };

        var (scope, service, db) = env.Request();
        using var _ = scope;
        var result = await service.CompleteAsync(NewUser, request);
        Assert.True(result.IsSuccess, result.Error);

        var deck = Assert.Single(result.Value!.Decks);
        Assert.Equal(preview.Decks[0].Cards.Count - 3, deck.Count);
        var terms = await db.Cards.AsNoTracking().Where(c => c.Set.OwnerId == NewUser).Select(c => c.Term).ToListAsync();
        foreach (var known in preview.Decks[0].Cards.Take(3))
        {
            Assert.DoesNotContain(known.Term, terms);
        }

        var row = await db.UserOnboardings.AsNoTracking().SingleAsync(x => x.UserId == NewUser);
        var stored = JsonSerializer.Deserialize<List<string>>(row.KnownTermsJson!)!;
        Assert.Equal(preview.Decks[0].Cards.Take(3).Select(c => c.Term.ToLowerInvariant()).OrderBy(x => x), stored.OrderBy(x => x));
        // "Đã biết" only excludes: nothing is marked as learned
        Assert.Equal(0, await db.LearningProgresses.CountAsync(p => p.UserId == NewUser));
    }

    [Fact]
    public async Task Complete_SameRequestId_IsIdempotent_AndADifferentPayloadIsAConflict()
    {
        await using var env = await CreateAsync();
        var (_, request) = await PrepareAsync(env);

        CompleteResult first;
        {
            var (scope, service, _) = env.Request();
            using var _ = scope;
            first = (await service.CompleteAsync(NewUser, request)).Value!;
        }

        int sets, cards;
        {
            var (scope, service, db) = env.Request();
            using var _ = scope;
            var again = await service.CompleteAsync(NewUser, request); // a retry after a lost response
            Assert.True(again.IsSuccess);
            Assert.Equal(first.Decks.Select(d => d.SetId), again.Value!.Decks.Select(d => d.SetId));
            sets = await db.Sets.CountAsync(s => s.OwnerId == NewUser);
            cards = await db.Cards.CountAsync(c => c.Set.OwnerId == NewUser);
            Assert.Equal(first.Decks.Count, sets);
            Assert.Equal(1, await db.Folders.CountAsync(f => f.UserId == NewUser));
        }

        {
            var (scope, service, db) = env.Request();
            using var _ = scope;
            var different = await service.CompleteAsync(NewUser, request with { Choices = Choices(level: "B1") });
            Assert.Equal(OnboardingOutcome.Conflict, different.Outcome);
            Assert.Equal(sets, await db.Sets.CountAsync(s => s.OwnerId == NewUser));
            Assert.Equal(cards, await db.Cards.CountAsync(c => c.Set.OwnerId == NewUser));
        }
    }

    [Fact]
    public async Task Complete_FailureHalfWay_RollsEverythingBack_AndARetryWorks()
    {
        var fail = new FailNthSave { FailOn = 2 };
        await using var env = await CreateAsync(fail);
        var (_, request) = await PrepareAsync(env); // creates the row too
        fail.Armed = true;

        {
            var (scope, service, _) = env.Request();
            using var _ = scope;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(NewUser, request));
        }

        {
            var (scope, service, db) = env.Request();
            using var _ = scope;
            Assert.Equal(0, await db.Sets.CountAsync(s => s.OwnerId == NewUser));
            Assert.Equal(0, await db.Folders.CountAsync(f => f.UserId == NewUser)); // the folder was created in the same transaction
            Assert.Equal(0, await db.Cards.CountAsync(c => c.Set.OwnerId == NewUser));
            var row = await db.UserOnboardings.AsNoTracking().SingleAsync(x => x.UserId == NewUser);
            Assert.Equal(OnboardingStatus.Pending, row.Status);
            Assert.Null(row.LastRequestId); // the claim was rolled back too, so the same request can be retried

            fail.Armed = false;
            Assert.True((await service.CompleteAsync(NewUser, request)).IsSuccess);
            Assert.True(await db.Sets.CountAsync(s => s.OwnerId == NewUser) > 0);
        }
    }

    [Fact]
    public async Task Complete_ValidatesAgainstThePreview()
    {
        await using var env = await CreateAsync();
        var (preview, request) = await PrepareAsync(env);
        var (scope, service, db) = env.Request();
        using var _ = scope;

        var strangerCard = await db.Cards.AsNoTracking().Where(c => c.Set.OwnerId != NewUser).OrderByDescending(c => c.CardId).Select(c => c.CardId).FirstAsync();
        Assert.Equal(OnboardingOutcome.Invalid,
            (await service.CompleteAsync(NewUser, request with { RequestId = Guid.NewGuid(), KnownCardIds = new[] { strangerCard + 1000 } })).Outcome);
        Assert.Equal(OnboardingOutcome.Invalid,
            (await service.CompleteAsync(NewUser, request with { RequestId = Guid.NewGuid(), DecksOff = new[] { "no-such-deck" } })).Outcome);
        Assert.Equal(OnboardingOutcome.Invalid,
            (await service.CompleteAsync(NewUser, request with { RequestId = Guid.NewGuid(), DecksOff = preview.Decks.Select(d => d.Key).ToList() })).Outcome);
        Assert.Equal(OnboardingOutcome.Invalid, (await service.CompleteAsync(NewUser, request with { RequestId = Guid.Empty })).Outcome);
        Assert.Equal(0, await db.Sets.CountAsync(s => s.OwnerId == NewUser));
    }

    [Fact]
    public async Task Complete_StopsAtTheOwnedSetLimit()
    {
        await using var env = await CreateAsync();
        using (var scope = env.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            for (var i = 0; i < 199; i++)
            {
                db.Sets.Add(new Set { OwnerId = OtherUser, SetName = "Filler " + i, Visibility = "Private", CreatedAt = Start, UpdatedAt = Start });
            }

            await db.SaveChangesAsync();
        }

        var (scope2, service, db2) = env.Request();
        using var _ = scope2;
        var preview = (await service.BuildPreviewAsync(OtherUser, Choices())).Value!;
        Assert.True(preview.Decks.Count >= 2);

        var result = await service.CompleteAsync(OtherUser, new CompleteRequest(Guid.NewGuid(), Choices(), Array.Empty<string>(), Array.Empty<int>()));
        Assert.Equal(OnboardingOutcome.Invalid, result.Outcome);
        Assert.Contains("200", result.Error);
        Assert.Equal(199, await db2.Sets.CountAsync(s => s.OwnerId == OtherUser));
    }

    [Fact]
    public async Task AgainMode_ReusesTheFolder_AndNumbersTheNames_AndNeverRepeatsWords()
    {
        await using var env = await CreateAsync();
        var (_, first) = await PrepareAsync(env);

        {
            var (scope, service, _) = env.Request();
            using var _ = scope;
            Assert.True((await service.CompleteAsync(NewUser, first)).IsSuccess);
        }

        {
            var (scope, service, db) = env.Request();
            using var _ = scope;
            var second = await service.CompleteAsync(NewUser,
                new CompleteRequest(Guid.NewGuid(), Choices(topics: new[] { "A16", "A15", "C04", "B04" }), Array.Empty<string>(), Array.Empty<int>()));
            Assert.True(second.IsSuccess, second.Error);

            Assert.Equal(1, await db.Folders.CountAsync(f => f.UserId == NewUser && f.FolderName == "Lộ trình của tôi"));
            var names = await db.Sets.Where(s => s.OwnerId == NewUser).Select(s => s.SetName).ToListAsync();
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.Contains(names, n => n.EndsWith("(2)", StringComparison.Ordinal));

            var terms = await db.Cards.Where(c => c.Set.OwnerId == NewUser).Select(c => c.Term).ToListAsync();
            Assert.Equal(terms.Count, terms.Select(t => t.ToLowerInvariant()).Distinct().Count());

            var row = await db.UserOnboardings.AsNoTracking().SingleAsync(x => x.UserId == NewUser);
            Assert.Equal(names.Count, JsonDocument.Parse(row.DecksJson!).RootElement.GetArrayLength());
        }
    }

    [Fact]
    public async Task AnExistingFolderWithTheSameName_IsReusedAndNotDuplicated()
    {
        await using var env = await CreateAsync();
        using (var scope = env.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.Folders.Add(new Folder { FolderName = "  LỘ TRÌNH CỦA TÔI ", UserId = NewUser });
            await db.SaveChangesAsync();
        }

        var (_, request) = await PrepareAsync(env);
        var (scope2, service, db2) = env.Request();
        using var _ = scope2;
        var result = await service.CompleteAsync(NewUser, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await db2.Folders.CountAsync(f => f.UserId == NewUser));
        Assert.Equal((await db2.Folders.SingleAsync(f => f.UserId == NewUser)).FolderId, result.Value!.FolderId);
    }

    // ── refill and banner ────────────────────────────────────────────────

    private static async Task<(int SetId, int Count)> CompleteForRefillAsync(Env env)
    {
        var (_, request) = await PrepareAsync(env);
        var (scope, service, _) = env.Request();
        using var _ = scope;
        var created = (await service.CompleteAsync(NewUser, request)).Value!;
        var deck = created.Decks[0];
        return (deck.SetId, deck.Count);
    }

    [Fact]
    public async Task Refill_AddsTheNextWords_WithoutDuplicates_AndIsIdempotent()
    {
        await using var env = await CreateAsync();
        var (setId, before) = await CompleteForRefillAsync(env);
        var requestId = Guid.NewGuid();

        RefillResult first;
        {
            var (scope, service, db) = env.Request();
            using var _ = scope;
            var result = await service.RefillAsync(NewUser, setId, 5, requestId);
            Assert.True(result.IsSuccess, result.Error);
            first = result.Value!;
            Assert.Equal(5, first.Added);
            Assert.False(first.Exhausted);

            var terms = await db.Cards.Where(c => c.Set.OwnerId == NewUser).Select(c => c.Term).ToListAsync();
            Assert.Equal(terms.Count, terms.Select(t => t.ToLowerInvariant()).Distinct().Count());
            Assert.Equal(before + 5, await db.Cards.CountAsync(c => c.SetId == setId));
        }

        {
            var (scope, service, db) = env.Request();
            using var _ = scope;
            var replay = await service.RefillAsync(NewUser, setId, 5, requestId); // same request again
            Assert.Equal(first, replay.Value);
            Assert.Equal(before + 5, await db.Cards.CountAsync(c => c.SetId == setId));
            Assert.Equal(OnboardingOutcome.Conflict, (await service.RefillAsync(NewUser, setId, 7, requestId)).Outcome);
        }
    }

    [Fact]
    public async Task Refill_NeverBringsBackKnownWords_AndReportsWhenTheTopicsRunOut()
    {
        await using var env = await CreateAsync();
        var (preview, request) = await PrepareAsync(env);
        var known = preview.Decks.SelectMany(d => d.Cards).Take(4).ToList();
        request = request with { KnownCardIds = known.Select(c => c.CardId).ToList() };

        int setId;
        {
            var (scope, service, _) = env.Request();
            using var _ = scope;
            setId = (await service.CompleteAsync(NewUser, request)).Value!.Decks[0].SetId;
        }

        var (scope2, service2, db) = env.Request();
        using var _2 = scope2;
        var added = 0;
        var exhausted = false;
        for (var i = 0; i < 8 && !exhausted; i++)
        {
            var result = (await service2.RefillAsync(NewUser, setId, 20, Guid.NewGuid())).Value!;
            added += result.Added;
            exhausted = result.Exhausted;
        }

        Assert.True(exhausted);
        var terms = await db.Cards.Where(c => c.Set.OwnerId == NewUser).Select(c => c.Term.ToLower()).ToListAsync();
        foreach (var card in known)
        {
            Assert.DoesNotContain(card.Term.ToLowerInvariant(), terms);
        }

        Assert.Equal(terms.Count, terms.Distinct().Count());
        Assert.True(added > 0);
    }

    [Fact]
    public async Task Refill_IsLimitedToTheUsersOwnOnboardingDecks()
    {
        await using var env = await CreateAsync();
        var (setId, _) = await CompleteForRefillAsync(env);

        var (scope, service, db) = env.Request();
        using var _ = scope;
        // another user cannot refill it, a plain personal set is not an onboarding deck, bad counts are rejected
        Assert.Equal(OnboardingOutcome.NotFound, (await service.RefillAsync(OtherUser, setId, 5, Guid.NewGuid())).Outcome);
        var ownSet = await db.Sets.Where(s => s.OwnerId == UserWithSet).Select(s => s.SetId).FirstAsync();
        Assert.Equal(OnboardingOutcome.NotFound, (await service.RefillAsync(UserWithSet, ownSet, 5, Guid.NewGuid())).Outcome);
        Assert.Equal(OnboardingOutcome.NotFound, (await service.RefillAsync(NewUser, ownSet, 5, Guid.NewGuid())).Outcome);
        Assert.Equal(OnboardingOutcome.NotFound, (await service.RefillAsync(NewUser, 999999, 5, Guid.NewGuid())).Outcome);
        Assert.Equal(OnboardingOutcome.Invalid, (await service.RefillAsync(NewUser, setId, 0, Guid.NewGuid())).Outcome);
        Assert.Equal(OnboardingOutcome.Invalid, (await service.RefillAsync(NewUser, setId, 21, Guid.NewGuid())).Outcome);
        Assert.Equal(OnboardingOutcome.Invalid, (await service.RefillAsync(NewUser, setId, 5, Guid.Empty)).Outcome);
    }

    [Fact]
    public async Task Banner_ShowsAfterCompleting_UntilDismissed()
    {
        await using var env = await CreateAsync();
        await CompleteForRefillAsync(env);

        var (scope, service, _) = env.Request();
        using var _ = scope;
        Assert.True((await service.GetStateAsync(NewUser)).BannerVisible);
        await service.DismissBannerAsync(NewUser);
        Assert.False((await service.GetStateAsync(NewUser)).BannerVisible);
        await service.DismissBannerAsync(NewUser); // harmless twice
        Assert.False((await service.GetStateAsync(NewUser)).BannerVisible);
    }
}

public sealed class OnboardingRequestLimiterTests
{
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan span) => _now += span;
    }

    [Fact]
    public void TenCallsPerTenMinutes_PerUser()
    {
        var clock = new Clock();
        var limiter = new OnboardingRequestLimiter(clock);

        for (var i = 0; i < OnboardingRequestLimiter.Permits; i++)
        {
            Assert.True(limiter.TryAcquire(1));
        }

        Assert.False(limiter.TryAcquire(1));
        Assert.True(limiter.TryAcquire(2)); // another user is not affected

        clock.Advance(TimeSpan.FromMinutes(9));
        Assert.False(limiter.TryAcquire(1));
        clock.Advance(TimeSpan.FromMinutes(1.1));
        Assert.True(limiter.TryAcquire(1));
    }
}
