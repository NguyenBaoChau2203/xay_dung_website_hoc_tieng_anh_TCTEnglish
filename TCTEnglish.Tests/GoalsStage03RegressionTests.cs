using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.Accounts;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels;
using TCTVocabulary.Controllers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using DashboardViewModel = TCTVocabulary.ViewModels.DashboardViewModel;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsStage03RegressionTests
{
    internal sealed class TestClock(DateTime utc) : TimeProvider
    {
        public DateTime Now { get; set; } = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    internal static void Configure(IServiceCollection services, TestClock clock)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(clock);
    }

    [Fact]
    public async Task PartialAccepted_MidnightReplayAndMissedDay_KeepLifetimeRewardUnique()
    {
        var clock = new TestClock(new DateTime(2026, 11, 13, 16, 59, 59));
        await using var factory = new TestWebApplicationFactory(s => Configure(s, clock));
        await factory.InitializeAsync();
        async Task Submit()
        {
            using var scope = factory.Services.CreateScope();
            Assert.True((await scope.ServiceProvider.GetRequiredService<IWritingService>()
                .EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId))!.Passed);
        }
        await Submit();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var user = await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
            Assert.Equal(1, user.Streak);
            Assert.Equal(new DateTime(2026, 11, 13), user.LastStudyDate);
            var day = await db.UserDailyActivities.SingleAsync();
            Assert.Equal(10, day.XpEarned);
            Assert.Equal(0, day.WritingCompletedCount);
            Assert.Empty(await db.LearningCompletionCredits.ToListAsync());
        }
        clock.Now = clock.Now.AddSeconds(1);
        await Submit();
        for (var i = 0; i < 100; i++) await Submit();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.Equal(2, (await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId)).Streak);
            Assert.Equal(20, await db.UserDailyActivities.SumAsync(a => a.XpEarned));
            Assert.Equal(102, await db.UserWritingAttempts.CountAsync());
            Assert.All(await db.UserWritingAttempts.ToListAsync(), a => Assert.InRange(a.CreatedAtUtc,
                new DateTime(2026, 11, 13, 16, 59, 59), new DateTime(2026, 11, 13, 17, 0, 0)));
        }
        clock.Now = clock.Now.AddDays(2);
        await Submit();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var user = await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
            Assert.Equal(1, user.Streak);
            Assert.Equal(2, user.LongestStreak);
            Assert.Equal(30, await db.UserDailyActivities.SumAsync(a => a.XpEarned));
        }
    }

    [Theory]
    [InlineData(-2, 0)]
    [InlineData(-1, 5)]
    [InlineData(0, 5)]
    [InlineData(1, 0)]
    public async Task GoalsProfileAndDashboard_ReadSameStreak_WithoutChangingHistory(int offset, int expected)
    {
        var clock = new TestClock(new DateTime(2026, 11, 14, 17, 0, 0));
        await using var factory = new TestWebApplicationFactory(s => Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
        user.Streak = 5; user.LongestStreak = 9; user.LastStudyDate = new DateTime(2026, 11, 15).AddDays(offset);
        await db.SaveChangesAsync();
        var goals = await scope.ServiceProvider.GetRequiredService<IGoalsService>().GetGoalsAsync(TestDataIds.UserId);
        var profile = await scope.ServiceProvider.GetRequiredService<IAccountProfileService>()
            .BuildAsync(TestDataIds.UserId, false, null, null);
        Assert.Equal(expected, goals!.CurrentStreak);
        Assert.Equal(expected, profile!.StreakDays);
        Assert.Equal(expected, (await DashboardAsync(scope.ServiceProvider)).Streak);
        db.ChangeTracker.Clear();
        Assert.Equal(5, (await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId)).Streak);
        Assert.Empty(await db.UserDailyActivities.ToListAsync());
        Assert.Empty(await db.UserBadges.ToListAsync());
    }

    [Fact]
    public async Task WrongOrReviewOnlyAndPageReads_DoNotEarnLearningDay()
    {
        var clock = new TestClock(new DateTime(2026, 11, 14, 0, 0, 0));
        await using var factory = new TestWebApplicationFactory(s => Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var writing = scope.ServiceProvider.GetRequiredService<IWritingService>();
        await writing.GetWritingPracticeDataAsync(1, TestDataIds.UserId);
        Assert.False((await writing.EvaluateWritingSentenceAsync(1, 1, "Good morning friend.", TestDataIds.UserId))!.Passed);
        await scope.ServiceProvider.GetRequiredService<IGoalsService>().GetGoalsAsync(TestDataIds.UserId);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Empty(await db.UserDailyActivities.ToListAsync());
        Assert.Empty(await db.LearningCompletionCredits.ToListAsync());
        Assert.Equal(0, (await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId)).Streak);
    }

    [Fact]
    public async Task CompletedExercise_ReplayedNextDay_HasStudyPresenceWithoutAnotherGoalOrCompletionXp()
    {
        var clock = new TestClock(new DateTime(2026, 11, 14, 0, 0, 0));
        await using var factory = new TestWebApplicationFactory(s => Configure(s, clock));
        await factory.InitializeAsync();
        await WritingCompletionTransactionTests.SeedOneSentenceAsync(factory);
        async Task Submit()
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IWritingService>()
                .EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId);
        }
        await Submit();
        clock.Now = clock.Now.AddDays(1);
        await Submit();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var days = await db.UserDailyActivities.OrderBy(a => a.ActivityDate).ToListAsync();
        Assert.Equal(new[] { 30, 10 }, days.Select(a => a.XpEarned));
        Assert.Equal(new[] { 1, 0 }, days.Select(a => a.WritingCompletedCount));
        Assert.Single(await db.LearningCompletionCredits.ToListAsync());
        var goals = (await scope.ServiceProvider.GetRequiredService<IGoalsService>().GetGoalsAsync(TestDataIds.UserId))!;
        Assert.Equal(2, goals.CurrentStreak);
        Assert.True(goals.StudyDays[^1].HasConfirmedStudy);
        Assert.True(goals.StudyDays[^2].HasConfirmedStudy);
        Assert.False(goals.StudyDays[^1].AllGoalsMet);
        Assert.All(goals.WeeklyActivity, a => Assert.Equal(0, a.ActivityCount));
        Assert.Equal(0, goals.ProgressPercent);
        var profile = (await scope.ServiceProvider.GetRequiredService<IAccountProfileService>()
            .BuildAsync(TestDataIds.UserId, false, null, null))!;
        Assert.Equal(goals.ProgressPercent, profile.TodayGoalPercent);
        Assert.Contains("2/2", profile.Habits.PracticeDaysText);
        var dashboard = await DashboardAsync(scope.ServiceProvider);
        Assert.Equal(40, dashboard.TotalXp);
        Assert.Equal(10, dashboard.TodayXp);
        Assert.Equal(goals.ProgressPercent, dashboard.DailyGoalPercent);
        Assert.Equal(goals.StudyDays.Select(d => d.HasConfirmedStudy), dashboard.StudyDays.Select(d => d.HasConfirmedStudy));
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        foreach (var route in new[] { "/Goals", "/Account/Profile", "/Home/Index" })
        {
            var html = await client.GetStringAsync(route);
            if (route == "/Home/Index") Assert.Contains("Hôm nay: 10 XP", System.Net.WebUtility.HtmlDecode(html));
            if (Environment.GetEnvironmentVariable("TCT_STAGE03_UI_CAPTURE") == "1")
            {
                var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.appdata/local/test-results/goals-stage03"));
                Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder, route == "/Goals" ? "goals.html" : route == "/Home/Index" ? "dashboard.html" : "profile.html"), html);
            }
        }
    }

    [Fact]
    public async Task TargetEdits_PauseAndMixedUnits_DoNotRewriteYesterdayOrMintRewards()
    {
        var clock = new TestClock(new DateTime(2026, 11, 10, 0, 0, 0));
        await using var factory = new TestWebApplicationFactory(s => Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        Assert.Equal(OperationStatus.Success, (await goals.UpdateGoalAsync(TestDataIds.UserId, GoalArea.Writing, 1)).Status);
        clock.Now = clock.Now.AddDays(1);
        await goals.RecordLearningActivityAsync(TestDataIds.UserId, new GoalsActivityUpdate { WritingCompletedCount = 1, XpEarned = 20 });
        clock.Now = clock.Now.AddDays(1);
        Assert.Equal(OperationStatus.Success, (await goals.UpdateGoalAsync(TestDataIds.UserId, GoalArea.Writing, 3)).Status);
        var page = (await goals.GetGoalsAsync(TestDataIds.UserId))!;
        Assert.True(page.StudyDays[^2].AllGoalsMet);
        Assert.Null(page.StudyDays[^3].AllGoalsMet); // Initial baseline is incomplete history.
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var xp = await db.UserDailyActivities.SumAsync(a => a.XpEarned);
        foreach (var area in Enum.GetValues<GoalArea>())
        {
            Assert.Equal(OperationStatus.Success, (await goals.UpdateGoalAsync(TestDataIds.UserId, area, 0)).Status);
            Assert.Equal(OperationStatus.Invalid, (await goals.UpdateGoalAsync(TestDataIds.UserId, area, -1)).Status);
            Assert.Equal(OperationStatus.Invalid, (await goals.UpdateGoalAsync(TestDataIds.UserId, area, 501)).Status);
            Assert.Equal(OperationStatus.Success, (await goals.UpdateGoalAsync(TestDataIds.UserId, area, 500)).Status);
            await goals.UpdateGoalAsync(TestDataIds.UserId, area, 0);
        }
        Assert.Equal(OperationStatus.Invalid, (await goals.UpdateGoalAsync(TestDataIds.UserId, (GoalArea)999, 1)).Status);
        Assert.Equal(0, (await goals.GetGoalsAsync(TestDataIds.UserId))!.ProgressPercent);
        await goals.UpdateGoalAsync(TestDataIds.UserId, GoalArea.Writing, 2);
        await goals.UpdateGoalAsync(TestDataIds.UserId, GoalArea.Vocabulary, 10);
        await goals.RecordActivityAsync(TestDataIds.UserId, new GoalsActivityUpdate { WritingCompletedCount = 1, CardsReviewed = 10 });
        page = (await goals.GetGoalsAsync(TestDataIds.UserId))!;
        Assert.Equal(75, page.ProgressPercent); // 50% Writing + 100% Vocabulary / 2
        Assert.True(page.StudyDays[^2].AllGoalsMet);
        Assert.False(page.StudyDays[^1].AllGoalsMet);
        Assert.Equal(xp, await db.UserDailyActivities.SumAsync(a => a.XpEarned));
        var profile = (await scope.ServiceProvider.GetRequiredService<IAccountProfileService>()
            .BuildAsync(TestDataIds.UserId, false, null, null))!;
        Assert.Equal(page.ProgressPercent, profile.TodayGoalPercent);
    }

    [Fact]
    public async Task BadgeRecency_UsesTodayUtcPlusSeven_AndPreservesEarnedAwards()
    {
        var clock = new TestClock(new DateTime(2026, 11, 14, 0, 0, 0));
        await using var factory = new TestWebApplicationFactory(s => Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.UserBadges.AddRange(new UserBadge { UserId = TestDataIds.UserId, BadgeId = 1, AwardedAt = new DateTime(2026, 11, 13, 17, 0, 0) },
            new UserBadge { UserId = TestDataIds.UserId, BadgeId = 2, AwardedAt = new DateTime(2026, 11, 13, 16, 59, 59) });
        await db.SaveChangesAsync();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var profile = await goals.GetEarnedBadgesForProfileAsync(TestDataIds.UserId);
        var page = (await goals.GetGoalsAsync(TestDataIds.UserId))!;
        Assert.True(profile.Single(b => b.Code == "first-session").IsRecentlyUnlocked);
        Assert.False(profile.Single(b => b.Code == "three-day-streak").IsRecentlyUnlocked);
        Assert.All(profile, badge => Assert.Equal(badge.IsRecentlyUnlocked,
            page.Badges.Single(b => b.Code == badge.Code).IsRecentlyUnlocked));
        Assert.Equal(2, await db.UserBadges.CountAsync());
        Assert.Empty(await goals.GetEarnedBadgesForProfileAsync(TestDataIds.OutsiderUserId));
    }

    [Fact]
    public async Task LegacyEvidenceAndEmptyOrXpOnlyRows_DoNotMintHistoryOnRead_AndAreUserScoped()
    {
        var clock = new TestClock(new DateTime(2026, 11, 14, 0, 0, 0));
        await using var factory = new TestWebApplicationFactory(s => Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.UserWritingAttempts.Add(new UserWritingAttempt { UserId = TestDataIds.UserId, WritingExerciseId = 1,
            WritingExerciseSentenceId = 1, Passed = true, SubmittedAnswer = "Hello!", EvaluationSource = "local-exact", CreatedAtUtc = clock.Now.AddDays(-1) });
        db.UserDailyActivities.AddRange(new UserDailyActivity { UserId = TestDataIds.UserId, ActivityDate = new DateTime(2026, 11, 14), XpEarned = 7 },
            new UserDailyActivity { UserId = TestDataIds.UserId, ActivityDate = new DateTime(2026, 11, 12) },
            new UserDailyActivity { UserId = TestDataIds.OutsiderUserId, ActivityDate = new DateTime(2026, 11, 14), CardsReviewed = 10 });
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var goals = (await service.GetGoalsAsync(TestDataIds.UserId))!;
        Assert.True(goals.StudyDays[^2].HasConfirmedStudy);
        Assert.False(goals.StudyDays[^1].HasConfirmedStudy);
        Assert.False(goals.StudyDays[^3].HasConfirmedStudy);
        Assert.Equal(0, goals.CurrentStreak);
        Assert.All(goals.StudyDays.Take(6), d => Assert.Null(d.AllGoalsMet));
        Assert.True((await service.GetGoalsAsync(TestDataIds.OutsiderUserId))!.StudyDays[^1].HasConfirmedStudy);
        Assert.Empty(await db.UserBadges.ToListAsync());
        Assert.Empty(await db.LearningCompletionCredits.ToListAsync());
        Assert.Equal(7, await db.UserDailyActivities.Where(a => a.UserId == TestDataIds.UserId).SumAsync(a => a.XpEarned));
    }

    [Fact]
    public async Task UnitThatCrossesMidnight_UsesAttemptInstantForAllRewardsAndOutbox()
    {
        var clock = new TestClock(new DateTime(2026, 11, 14, 16, 59, 59));
        await using var factory = new TestWebApplicationFactory(s => Configure(s, clock));
        factory.SaveInterceptors.Add(new MidnightAdvance(clock));
        await factory.InitializeAsync();
        await WritingCompletionTransactionTests.SeedOneSentenceAsync(factory);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IWritingService>().EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(new DateTime(2026, 11, 15), scope.ServiceProvider.GetRequiredService<LearningBusinessClock>().BusinessDate);
        Assert.Equal(new DateTime(2026, 11, 14), (await db.UserDailyActivities.SingleAsync()).ActivityDate);
        Assert.Equal(new DateTime(2026, 11, 14), (await db.LearningCompletionCredits.SingleAsync()).BusinessDate);
        Assert.All(await db.LearningNotificationOutboxEvents.ToListAsync(), o => Assert.Equal(new DateTime(2026, 11, 14), o.BusinessDate));
        Assert.Equal(30, (await db.UserDailyActivities.AsNoTracking().SingleAsync()).XpEarned);
    }

    private sealed class MidnightAdvance(TestClock clock) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<UserWritingAttempt>().Any())
                clock.Now = new DateTime(2026, 11, 14, 17, 0, 0, DateTimeKind.Utc);
            return ValueTask.FromResult(result);
        }
    }

    private static async Task<DashboardViewModel> DashboardAsync(IServiceProvider services)
    {
        var controller = ActivatorUtilities.CreateInstance<HomeController>(services);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, TestDataIds.UserId.ToString())], "test"))
            }
        };
        return Assert.IsType<DashboardViewModel>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await controller.Index()).Model);
    }

    [Fact]
    public async Task TwoUsersAndAnonymizedOrMissingUser_DoNotShareOrAccumulateProgress()
    {
        var clock = new TestClock(new DateTime(2026, 11, 14, 0, 0, 0));
        await using var factory = new TestWebApplicationFactory(s => Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        await scope.ServiceProvider.GetRequiredService<IWritingService>()
            .EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId);
        Assert.False((await goals.GetGoalsAsync(TestDataIds.OutsiderUserId))!.StudyDays[^1].HasConfirmedStudy);
        Assert.Equal(0, (await goals.GetGoalsAsync(TestDataIds.OutsiderUserId))!.CurrentStreak);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
        user.AnonymizedAt = clock.Now;
        await db.SaveChangesAsync();
        Assert.Null(await goals.GetGoalsAsync(TestDataIds.UserId));
        Assert.Equal(OperationStatus.NotFound, (await goals.RecordLearningActivityAsync(TestDataIds.UserId,
            new GoalsActivityUpdate { CardsReviewed = 1, XpEarned = 5 })).Status);
        Assert.Equal(OperationStatus.NotFound, (await goals.UpdateGoalAsync(TestDataIds.UserId, GoalArea.Writing, 10)).Status);
        Assert.Equal(OperationStatus.NotFound, (await goals.RecordLearningActivityAsync(int.MaxValue,
            new GoalsActivityUpdate { CardsReviewed = 1 })).Status);
        Assert.Equal(10, (await db.UserDailyActivities.AsNoTracking().SingleAsync()).XpEarned);
        Assert.Equal(1, await db.UserWritingAttempts.CountAsync());
    }

    [Fact]
    public async Task OldAttemptReplayAndEmptySignal_CannotMintAnotherDay()
    {
        var clock = new TestClock(new DateTime(2026, 11, 14, 0, 0, 0));
        await using var factory = new TestWebApplicationFactory(s => Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        await goals.RecordLearningActivityAsync(TestDataIds.UserId, new GoalsActivityUpdate());
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Empty(await db.UserDailyActivities.ToListAsync());
        await scope.ServiceProvider.GetRequiredService<IWritingService>()
            .EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId);
        var id = (await db.UserWritingAttempts.SingleAsync()).Id;
        clock.Now = clock.Now.AddDays(1);
        await using var unit = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider
            .GetRequiredService<TCTEnglish.Services.Learning.ICompletionCreditService>()
            .RecordWritingPracticeAsync(TestDataIds.UserId, id, CancellationToken.None));
        await unit.RollbackAsync();
        Assert.Equal(10, (await db.UserDailyActivities.AsNoTracking().SingleAsync()).XpEarned);
    }
}
