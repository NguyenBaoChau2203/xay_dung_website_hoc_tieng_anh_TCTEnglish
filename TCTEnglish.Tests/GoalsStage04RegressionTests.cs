using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTEnglish.Services.Learning;
using TCTEnglish.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using TCTVocabulary.Workers;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsStage04RegressionTests
{
    internal static GoalsStage03RegressionTests.TestClock Clock() => new(new DateTime(2026, 11, 14, 12, 0, 0));

    public static IEnumerable<object[]> Boundaries()
        => BadgeSeedData.CreateBadges().Where(b => b.Id >= 13)
            .SelectMany(b => new[] { -1, 0, 1 }.Select(delta => new object[] { b.Id, delta }));

    // Synthetic durable history, never developer data. Aggregate counters alone cannot prove distinct exercises.
    internal static async Task SeedEvidenceAsync(DbflashcardContext db, int userId, Badge badge, int value, DateTime now)
    {
        if (badge.MetricType == BadgeMetricType.DistinctWritingExercisesCompleted)
            for (var i = 1; i <= value; i++)
                db.LearningCompletionCredits.Add(new LearningCompletionCredit
                {
                    UserId = userId, Feature = "Writing", ContentKey = $"exercise:{10000 + i}",
                    CreditKey = $"{userId}:Writing:exercise:{10000 + i}", BusinessDate = now.Date,
                    CreatedAtUtc = now, XpAwarded = 20
                });
        else if (badge.MetricType == BadgeMetricType.TotalXp)
            db.UserDailyActivities.Add(new UserDailyActivity { UserId = userId, ActivityDate = now.Date, XpEarned = value });
        else
            for (var i = 0; i < value; i++)
                db.UserDailyActivities.Add(new UserDailyActivity { UserId = userId, ActivityDate = now.Date.AddDays(-i), CardsReviewed = 1 });
        await db.SaveChangesAsync();
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task EveryNewAward_BoundaryDryRunReconcileReplayAndIsolation(int badgeId, int delta)
    {
        var clock = Clock();
        await using var factory = new TestWebApplicationFactory(s => GoalsStage03RegressionTests.Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var service = (GoalsService)scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var badge = await db.Badges.SingleAsync(b => b.Id == badgeId);
        await SeedEvidenceAsync(db, TestDataIds.UserId, badge, badge.ThresholdValue + delta, clock.Now);
        var dailyBefore = await db.UserDailyActivities.AsNoTracking().Select(a => new { a.Id, a.ActivityDate, a.XpEarned, a.CardsReviewed }).ToListAsync();
        var credits = await db.LearningCompletionCredits.CountAsync();
        var dry = await service.ReconcileNewBadgesAsync(new[] { TestDataIds.UserId, TestDataIds.OutsiderUserId });
        Assert.Equal(delta >= 0, dry[0].BadgeIds.Contains(badgeId));
        Assert.Empty(dry[1].BadgeIds);
        Assert.Empty(await db.UserBadges.ToListAsync());
        var applied = await service.ReconcileNewBadgesAsync(new[] { TestDataIds.UserId }, dryRun: false);
        Assert.Equal(dry[0].BadgeIds, applied[0].BadgeIds);
        Assert.Empty((await service.ReconcileNewBadgesAsync(new[] { TestDataIds.UserId }, dryRun: false))[0].BadgeIds);
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.Empty(await db.LearningNotificationOutboxEvents.ToListAsync());
        Assert.Equal(credits, await db.LearningCompletionCredits.CountAsync());
        Assert.Equal(dailyBefore, await db.UserDailyActivities.AsNoTracking().Select(a => new { a.Id, a.ActivityDate, a.XpEarned, a.CardsReviewed }).ToListAsync());
        using var reload = factory.Services.CreateScope();
        var page = (await reload.ServiceProvider.GetRequiredService<IGoalsService>().GetGoalsAsync(TestDataIds.UserId))!;
        if (delta >= 0)
        {
            var projected = Assert.Single(page.Badges.Where(b => b.Code == badge.Code));
            Assert.True(projected.IsUnlocked);
            Assert.True(projected.IsRetroactive);
            Assert.False(projected.IsRedacted);
            Assert.NotNull(projected.AwardedAt);
            Assert.Contains(await reload.ServiceProvider.GetRequiredService<IGoalsService>().GetEarnedBadgesForProfileAsync(TestDataIds.UserId), b => b.Code == badge.Code);
        }
    }

    [Fact]
    public async Task AllNewAwards_AreReachableThroughRealWritingActions_AndReplayDoesNotNotifyAgain()
    {
        var clock = Clock();
        await using var factory = new TestWebApplicationFactory(s => GoalsStage03RegressionTests.Configure(s, clock));
        await factory.InitializeAsync();
        await RealWritingJourneyAsync(factory, clock);
    }

    internal static async Task RealWritingJourneyAsync(TestWebApplicationFactory factory, GoalsStage03RegressionTests.TestClock clock)
    {
        var lessons = new List<(int Exercise, int Sentence)>();
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            for (var i = 0; i < 50; i++)
            {
                var exercise = new WritingExercise { Title = $"Badge fixture {i}", Level = "beginner", ContentType = "emails", Topic = "Fixture", PreviewText = "Fixture", IsPublished = true, CreatedAt = clock.Now };
                exercise.WritingExerciseSentences.Add(new WritingExerciseSentence { SortOrder = 1, VietnameseText = "Xin chào!", EnglishMeaning = "Hello!" });
                db.WritingExercises.Add(exercise);
                await db.SaveChangesAsync();
                lessons.Add((exercise.Id, exercise.WritingExerciseSentences.Single().Id));
            }
        }
        async Task Submit((int Exercise, int Sentence) lesson)
        {
            using var scope = factory.Services.CreateScope();
            Assert.True((await scope.ServiceProvider.GetRequiredService<IWritingService>()
                .EvaluateWritingSentenceAsync(lesson.Exercise, lesson.Sentence, "Hello!", TestDataIds.UserId))!.Passed);
        }
        foreach (var lesson in lessons) await Submit(lesson);
        for (var day = 1; day < 30; day++) { clock.Now = clock.Now.AddDays(1); await Submit(lessons[0]); }
        using (var verify = factory.Services.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.Equal(10, await db.UserBadges.CountAsync(b => b.UserId == TestDataIds.UserId && b.BadgeId >= 13));
            Assert.Equal(50, await db.LearningCompletionCredits.CountAsync(c => c.Feature == "Writing"));
            Assert.Equal(50, await db.UserDailyActivities.SumAsync(a => a.WritingCompletedCount));
            Assert.Equal(1300, await db.UserDailyActivities.SumAsync(a => a.XpEarned));
            var badgeKeys = await db.LearningNotificationOutboxEvents.Where(o => o.EventType == "BadgeEarned").Select(o => o.NotificationEventKey).ToListAsync();
            Assert.Equal(10, badgeKeys.Count(key => int.TryParse(key!.Split(':')[^1], out var id) && id >= 13));
            Assert.Equal(badgeKeys.Count, badgeKeys.Distinct().Count());
        }
        clock.Now = clock.Now.AddDays(3); // current streak breaks; earned history remains.
        await Submit(lessons[0]);
        await Submit(lessons[0]);
        using var reload = factory.Services.CreateScope();
        var context = reload.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(10, await context.UserBadges.CountAsync(b => b.BadgeId >= 13));
        Assert.Equal(1, (await reload.ServiceProvider.GetRequiredService<IGoalsService>().GetGoalsAsync(TestDataIds.UserId))!.CurrentStreak);
        Assert.Equal(50, await context.LearningCompletionCredits.CountAsync());
        Assert.False(await context.UserBadges.AnyAsync(b => b.UserId == TestDataIds.OutsiderUserId));
    }

    [Fact]
    public void Projection_RedactsEveryLockedField_UnknownTierFallsBack_AndEarnedSecretsReveal()
    {
        foreach (var badge in BadgeSeedData.CreateBadges().Where(b => b.IsSecret))
        {
            var hidden = BadgeProjection.Build(badge, 12, null, false, false);
            Assert.Equal(string.Empty, hidden.Code);
            Assert.Null(hidden.DifficultyKey);
            Assert.Null(hidden.ProgressValue);
            Assert.Null(hidden.TargetValue);
            Assert.Null(hidden.ProgressPercent);
            Assert.Empty(hidden.MetricLabel);
            Assert.False(hidden.CanRecommend);
            Assert.True(hidden.IsRedacted);
            var payload = JsonSerializer.Serialize(hidden);
            Assert.DoesNotContain(badge.Code, payload);
            Assert.DoesNotContain(badge.Name, payload);
            Assert.DoesNotContain(badge.Description, payload);
            var earned = BadgeProjection.Build(badge, 0, Clock().Now, true, false);
            Assert.Equal(badge.Code, earned.Code);
            Assert.Equal(100, earned.ProgressPercent);
            Assert.True(earned.IsRecentlyUnlocked);
            Assert.Equal("bi-an", earned.DisplayGroupKey);
        }
        var unknown = new Badge { Code = "future", Name = "Future", DifficultyKey = "unknown", MetricType = (BadgeMetricType)999, ThresholdValue = 1 };
        var fallback = BadgeProjection.Build(unknown, 0, null, false, false);
        Assert.Equal("other", fallback.DisplayGroupKey);
        Assert.False(fallback.HasKnownProgress);
        Assert.False(fallback.CanRecommend);
    }

    [Fact]
    public async Task NearBadgeWorkerAndDirectDelivery_NeverRevealUnownedSecrets()
    {
        var clock = Clock(); clock.Now = clock.Now.AddHours(2);
        await using var factory = new TestWebApplicationFactory(s => GoalsStage03RegressionTests.Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        foreach (var badge in await db.Badges.Where(b => !b.IsSecret).ToListAsync())
            db.UserBadges.Add(new UserBadge { UserId = TestDataIds.UserId, BadgeId = badge.Id, AwardedAt = clock.Now });
        await SeedEvidenceAsync(db, TestDataIds.UserId, new Badge { MetricType = BadgeMetricType.TotalDaysActive }, 20, clock.Now);
        var worker = new NotificationWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<NotificationWorker>.Instance);
        for (var i = 0; i < 6; i++) await worker.TryRunDueJobsAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<INotificationService>().GenerateBadgeNotificationAsync(TestDataIds.UserId, 22);
        Assert.False(await db.Notifications.AnyAsync(n => n.UserId == TestDataIds.UserId
            && (n.Type == TCTEnglish.Models.NotificationType.BadgeNear || n.Type == TCTEnglish.Models.NotificationType.BadgeEarned)));
    }

    [Fact]
    public async Task UnknownAggregateOrFutureHistory_IsNotEvidence_DuplicateEvidenceCountsOnce_AndOldAwardsRemain()
    {
        var clock = Clock();
        await using var factory = new TestWebApplicationFactory(s => GoalsStage03RegressionTests.Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var goals = (GoalsService)scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var user = await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
        user.Streak = 0; user.LongestStreak = 30; user.LastStudyDate = clock.Now.AddDays(-40);
        var oldDate = clock.Now.AddYears(-1);
        db.UserBadges.Add(new UserBadge { UserId = user.UserId, BadgeId = 3, AwardedAt = oldDate });
        db.UserDailyActivities.Add(new UserDailyActivity { UserId = user.UserId, ActivityDate = clock.Now.Date, WritingCompletedCount = 50 });
        db.UserDailyActivities.Add(new UserDailyActivity { UserId = user.UserId, ActivityDate = clock.Now.Date.AddDays(-1) });
        db.UserDailyActivities.Add(new UserDailyActivity { UserId = user.UserId, ActivityDate = clock.Now.Date.AddDays(-2), XpEarned = 7 });
        await SeedEvidenceAsync(db, user.UserId, new Badge { MetricType = BadgeMetricType.DistinctWritingExercisesCompleted }, 2, clock.Now);
        db.LearningCompletionCredits.Add(new LearningCompletionCredit { UserId = user.UserId, Feature = "Writing", ContentKey = "writing-exercise:10001", CreditKey = "legacy-duplicate", BusinessDate = clock.Now.Date, CreatedAtUtc = clock.Now });
        db.LearningCompletionCredits.Add(new LearningCompletionCredit { UserId = user.UserId, Feature = "Writing", ContentKey = "exercise:10003", CreditKey = "future", BusinessDate = clock.Now.Date.AddDays(1), CreatedAtUtc = clock.Now.AddDays(1) });
        await db.SaveChangesAsync();
        Assert.Empty((await goals.ReconcileNewBadgesAsync(new[] { user.UserId }, dryRun: false))[0].BadgeIds);
        Assert.Equal(oldDate, (await db.UserBadges.SingleAsync()).AwardedAt);
        Assert.Equal(30, user.LongestStreak);
        Assert.Equal(7, await db.UserDailyActivities.SumAsync(a => a.XpEarned));
        await Assert.ThrowsAsync<ArgumentException>(() => goals.ReconcileNewBadgesAsync(Array.Empty<int>()));
        await Assert.ThrowsAsync<ArgumentException>(() => goals.ReconcileNewBadgesAsync(Enumerable.Range(1, 101).ToArray()));
        var htmlClient = factory.CreateClient();
        htmlClient.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, user.UserId.ToString());
        var html = await htmlClient.GetStringAsync("/Goals");
        foreach (var secret in BadgeSeedData.CreateBadges().Where(b => b.IsSecret))
        {
            Assert.DoesNotContain(secret.Code, html);
            Assert.DoesNotContain(secret.Name, System.Net.WebUtility.HtmlDecode(html));
            Assert.DoesNotContain(secret.Description, System.Net.WebUtility.HtmlDecode(html));
        }
        if (Environment.GetEnvironmentVariable("TCT_STAGE04_CAPTURE") == "1")
            await File.WriteAllTextAsync(Path.GetFullPath("../../../../.appdata/local/test-results/goals-stage04/goals-locked.html", AppContext.BaseDirectory), html);
    }

    [Fact]
    public async Task CatalogAndLockedSecrets_AreServerRedacted_AndGetDoesNotGrant()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        Assert.Equal(22, await db.Badges.CountAsync());
        var page = (await goals.GetGoalsAsync(TestDataIds.UserId))!;
        var payload = JsonSerializer.Serialize(page.Badges);
        foreach (var code in new[] { "secret-plot-twist", "secret-quiet-effort" })
            Assert.DoesNotContain(code, payload);
        Assert.Empty(await db.UserBadges.ToListAsync());
        Assert.Empty(await goals.GetEarnedBadgesForProfileAsync(TestDataIds.UserId));
    }

    [Fact]
    public async Task UnsupportedOrInvalidRules_NeverGrant()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.Badges.AddRange(
            new Badge { Id = 900, Code = "unknown", Name = "Unknown", Description = "Invalid", MetricType = (BadgeMetricType)999, ThresholdValue = 0 },
            new Badge { Id = 901, Code = "invalid", Name = "Invalid", Description = "Invalid", MetricType = BadgeMetricType.TotalDaysActive, ThresholdValue = -1 });
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IGoalsService>().RecordLearningActivityAsync(TestDataIds.UserId,
            new GoalsActivityUpdate { CardsReviewed = 1, XpEarned = 5 });
        Assert.False(await db.UserBadges.AnyAsync(b => b.BadgeId >= 900));
    }
}
