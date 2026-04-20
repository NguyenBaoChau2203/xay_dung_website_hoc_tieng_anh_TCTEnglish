using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsPhase2IntegrationTests
{
    [Fact]
    public async Task GoalsPage_ShowsCreateGoalCopy_WhenUserHasNoGoal()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedWithoutGoalAsync(factory);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Goals");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-testid=\"goal-header-cta\"", body, StringComparison.Ordinal);
        Assert.Contains("data-goal-mode=\"create\"", body, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"goal-empty-state-cta\"", body, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"goal-modal-title\"", body, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"goal-modal-submit\" data-goal-mode=\"create\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoalsPage_ShowsEditGoalCopy_WhenUserAlreadyHasGoal()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedGoalOnlyAsync(factory, goal: 9);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Goals");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-testid=\"goal-header-cta\"", body, StringComparison.Ordinal);
        Assert.Contains("data-goal-mode=\"edit\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("data-testid=\"goal-empty-state-cta\"", body, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"goal-modal-title\" data-goal-mode=\"edit\"", body, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"goal-modal-submit\" data-goal-mode=\"edit\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoalsPage_ShowsWeeklyEmptyState_WhenUserHasNoDailyActivity()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedGoalOnlyAsync(factory, goal: 9);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Goals");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("chart-empty-state", body, StringComparison.Ordinal);
        Assert.Contains("Biểu đồ sẽ sáng dần khi bạn bắt đầu ôn thẻ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"chart-bar", body, StringComparison.Ordinal);
    }

    private static async Task SeedWithoutGoalAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        user.Goal = null;
        user.Streak = 0;
        user.LongestStreak = 0;
        user.LastStudyDate = null;

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetGoalsAsync_FillsMissingWeeklyDays_FromUserDailyActivity()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var today = BusinessDateHelper.Today;
        await SeedDailyActivitiesAsync(
            factory,
            goal: 12,
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = today.AddDays(-4),
                CardsReviewed = 3,
                XpEarned = 15
            },
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = today,
                CardsReviewed = 5,
                XpEarned = 25
            });

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGoalsService>();

        var model = await service.GetGoalsAsync(TestDataIds.UserId);

        Assert.NotNull(model);
        Assert.Equal(7, model!.WeeklyActivity.Count);
        Assert.True(model.HasWeeklyActivity);
        Assert.Equal(5, model.TodayProgressValue);
        Assert.Equal(42, model.ProgressPercent);
        Assert.Equal(0, model.WeeklyActivity[0].ActivityCount);
        Assert.Equal(0, model.WeeklyActivity[1].ActivityCount);
        Assert.Equal(3, model.WeeklyActivity[2].ActivityCount);
        Assert.Equal(0, model.WeeklyActivity[3].ActivityCount);
        Assert.Equal(0, model.WeeklyActivity[4].ActivityCount);
        Assert.Equal(0, model.WeeklyActivity[5].ActivityCount);
        Assert.Equal(5, model.WeeklyActivity[6].ActivityCount);
        Assert.Equal(today.ToString("dd/MM"), model.WeeklyActivity[6].FullLabel);
    }

    [Fact]
    public async Task RecordActivityAsync_RejectsNegativeCounters_AndDoesNotPersist()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var result = await service.RecordActivityAsync(
            TestDataIds.UserId,
            new GoalsActivityUpdate
            {
                CardsReviewed = -1
            });

        Assert.Equal(OperationStatus.Invalid, result.Status);

        var rows = await context.UserDailyActivities
            .AsNoTracking()
            .Where(activity => activity.UserId == TestDataIds.UserId && activity.ActivityDate == BusinessDateHelper.Today)
            .CountAsync();

        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task RecordLearningActivityAsync_MasteredCompletionCountsOnceAcrossRetries()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IGoalsService>();

            var firstTransition = service.BuildVocabularyActivityUpdate(
                isNewProgress: false,
                previousStatus: "Reviewing",
                currentStatus: "Mastered");

            var retryTransition = service.BuildVocabularyActivityUpdate(
                isNewProgress: false,
                previousStatus: "Mastered",
                currentStatus: "Mastered");

            var firstResult = await service.RecordLearningActivityAsync(TestDataIds.UserId, firstTransition);
            var secondResult = await service.RecordLearningActivityAsync(TestDataIds.UserId, retryTransition);

            Assert.Equal(OperationStatus.Success, firstResult.Status);
            Assert.Equal(OperationStatus.Success, secondResult.Status);
        }

        using var verificationScope = factory.Services.CreateScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var today = BusinessDateHelper.Today;
        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(a => a.UserId == TestDataIds.UserId && a.ActivityDate == today);

        Assert.Equal(2, activity.CardsReviewed);
        Assert.Equal(1, activity.VocabularyCompletedCount);
        Assert.Equal(30, activity.XpEarned);
        Assert.Equal(10, activity.StreakXpAwarded);
    }

    [Fact]
    public async Task GoalsPage_Loads_WhenUserDailyActivitiesSchemaMissingPhase2AreaCounters()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedGoalOnlyAsync(factory, goal: 9);
        await DowngradeUserDailyActivitiesSchemaAsync(factory);
        await SeedLegacyDailyActivityAsync(
            factory,
            BusinessDateHelper.Today,
            xpEarned: 15,
            cardsReviewed: 5,
            speakingCompletedCount: 2);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Goals");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-testid=\"goal-header-cta\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordLearningActivityAsync_Works_WhenUserDailyActivitiesSchemaMissingPhase2AreaCounters()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await DowngradeUserDailyActivitiesSchemaAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IGoalsService>();

            var update = service.BuildVocabularyActivityUpdate(
                isNewProgress: false,
                previousStatus: "Reviewing",
                currentStatus: "Mastered");

            var result = await service.RecordLearningActivityAsync(TestDataIds.UserId, update);

            Assert.Equal(OperationStatus.Success, result.Status);
        }

        using var verificationScope = factory.Services.CreateScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == TestDataIds.UserId && a.ActivityDate == BusinessDateHelper.Today)
            .Select(a => new
            {
                a.CardsReviewed,
                a.XpEarned,
                a.StreakXpAwarded
            })
            .SingleAsync();

        Assert.Equal(1, activity.CardsReviewed);
        Assert.Equal(25, activity.XpEarned);
        Assert.Equal(10, activity.StreakXpAwarded);
    }

    [Fact]
    public async Task GetGoalsAsync_ReturnsAllGoalAreasIncludingReadingAndListening_WhenSignalsAreAvailable()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

            context.UserGoals.AddRange(
                new UserGoal { UserId = TestDataIds.UserId, GoalArea = GoalArea.Vocabulary, TargetValue = 10, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new UserGoal { UserId = TestDataIds.UserId, GoalArea = GoalArea.Speaking, TargetValue = 3, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new UserGoal { UserId = TestDataIds.UserId, GoalArea = GoalArea.Writing, TargetValue = 4, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new UserGoal { UserId = TestDataIds.UserId, GoalArea = GoalArea.Reading, TargetValue = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new UserGoal { UserId = TestDataIds.UserId, GoalArea = GoalArea.Listening, TargetValue = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

            context.UserDailyActivities.Add(new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = BusinessDateHelper.Today,
                CardsReviewed = 6,
                SpeakingCompletedCount = 2,
                WritingCompletedCount = 3,
                ReadingCompletedCount = 4,
                ListeningCompletedCount = 1,
                XpEarned = 30
            });

            await context.SaveChangesAsync();
        }

        using var verificationScope = factory.Services.CreateScope();
        var service = verificationScope.ServiceProvider.GetRequiredService<IGoalsService>();

        var model = await service.GetGoalsAsync(TestDataIds.UserId);
        var cards = model!.GoalCards.ToDictionary(card => card.GoalArea, card => card.TodayProgressValue);
        var goalAreaOptionValues = model.GoalAreaOptions.Select(option => option.Value).ToList();
        var deferredLabels = model.DeferredGoalAreaLabels;

        Assert.Equal(6, cards[GoalArea.Vocabulary]);
        Assert.Equal(2, cards[GoalArea.Speaking]);
        Assert.Equal(3, cards[GoalArea.Writing]);
        Assert.Equal(4, cards[GoalArea.Reading]);
        Assert.Equal(1, cards[GoalArea.Listening]);
        Assert.Contains(nameof(GoalArea.Reading), goalAreaOptionValues);
        Assert.Contains(nameof(GoalArea.Listening), goalAreaOptionValues);
        Assert.DoesNotContain("Reading", deferredLabels);
        Assert.DoesNotContain("Listening", deferredLabels);
    }

    private static async Task SeedGoalOnlyAsync(TestWebApplicationFactory factory, int goal)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        user.Goal = goal;
        user.Streak = 0;
        user.LongestStreak = 0;
        user.LastStudyDate = null;

        await context.SaveChangesAsync();
    }

    private static async Task SeedDailyActivitiesAsync(
        TestWebApplicationFactory factory,
        int goal,
        params UserDailyActivity[] activities)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        user.Goal = goal;
        user.Streak = 2;
        user.LongestStreak = 4;
        user.LastStudyDate = BusinessDateHelper.Today;

        context.UserDailyActivities.AddRange(activities);
        await context.SaveChangesAsync();
    }

    private static async Task DowngradeUserDailyActivitiesSchemaAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        await context.Database.ExecuteSqlRawAsync("ALTER TABLE UserDailyActivities DROP COLUMN VocabularyCompletedCount;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE UserDailyActivities DROP COLUMN WritingCompletedCount;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE UserDailyActivities DROP COLUMN ReadingCompletedCount;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE UserDailyActivities DROP COLUMN ListeningCompletedCount;");
    }

    private static async Task SeedLegacyDailyActivityAsync(
        TestWebApplicationFactory factory,
        DateTime activityDate,
        int xpEarned,
        int cardsReviewed,
        int speakingCompletedCount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        await context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO UserDailyActivities (
    UserID,
    ActivityDate,
    XpEarned,
    StreakXpAwarded,
    CardsReviewed,
    NewCardsLearned,
    QuizzesCompleted,
    SpeakingCompletedCount)
VALUES (
    {TestDataIds.UserId},
    {activityDate.Date},
    {xpEarned},
    {0},
    {cardsReviewed},
    {0},
    {0},
    {speakingCompletedCount})");
    }
}
