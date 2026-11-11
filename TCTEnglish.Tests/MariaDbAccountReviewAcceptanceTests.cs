using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using TCTEnglish.Models;
using TCTEnglish.Services.Accounts;
using TCTEnglish.Services.Learning;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
[Trait("Category", "MariaDbAcceptance")]
public sealed class MariaDbAccountReviewAcceptanceTests
{
    private const string Password = "NativeReview1!";
    private static readonly string Hash = BCrypt.Net.BCrypt.HashPassword(Password);

    [MariaDbAcceptanceFact]
    public async Task ParallelTabs_CannotExceedElapsedWallTime()
    {
        var clock = new ReviewClock();
        await using var factory = Factory(clock);
        await factory.InitializeAsync();
        var id = await SeedAsync(factory);
        (Guid SessionId, string LeaseToken)? first, second;
        using (var scope = factory.Services.CreateScope())
        {
            var tracker = scope.ServiceProvider.GetRequiredService<ILearningActivityTracker>();
            first = await tracker.OpenSessionAsync(id, "Flashcard", null);
            second = await tracker.OpenSessionAsync(id, "Speaking", null);
        }
        clock.Advance(TimeSpan.FromSeconds(30));
        using var left = factory.Services.CreateScope();
        using var right = factory.Services.CreateScope();
        ActivityCheckpointRequest Request((Guid SessionId, string LeaseToken)? session) => new()
        {
            SessionId = session!.Value.SessionId, LeaseToken = session.Value.LeaseToken,
            Sequence = 1, CumulativeActiveSeconds = 30, Visible = true
        };
        var outcomes = await Task.WhenAll(
            left.ServiceProvider.GetRequiredService<ILearningActivityTracker>().AcceptCheckpointAsync(id, Request(first)),
            right.ServiceProvider.GetRequiredService<ILearningActivityTracker>().AcceptCheckpointAsync(id, Request(second)));
        Assert.All(outcomes, outcome => Assert.True(outcome.Accepted, outcome.Error));
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(30, await db.LearningActivitySegments.Where(row => row.UserId == id).SumAsync(row => row.AcceptedSeconds));
    }

    [MariaDbAcceptanceFact]
    public async Task ParallelProofConsumption_OnlyOneRequestSucceeds()
    {
        await using var factory = Factory();
        await factory.InitializeAsync();
        var id = await SeedAsync(factory);
        const string token = "native-proof";
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AccountSecurityChallenges.Add(new AccountSecurityChallenge
            {
                UserId = id, Purpose = AccountProofPurpose.Deletion,
                TokenDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant(),
                SecurityVersion = 0, CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });
            await db.SaveChangesAsync();
        }
        using var left = factory.Services.CreateScope();
        using var right = factory.Services.CreateScope();
        var lUser = await left.ServiceProvider.GetRequiredService<DbflashcardContext>().Users.SingleAsync(row => row.UserId == id);
        var rUser = await right.ServiceProvider.GetRequiredService<DbflashcardContext>().Users.SingleAsync(row => row.UserId == id);
        var outcomes = await Task.WhenAll(
            left.ServiceProvider.GetRequiredService<IAccountSecurityService>().HasFreshProofAsync(lUser, null, token, AccountProofPurpose.Deletion),
            right.ServiceProvider.GetRequiredService<IAccountSecurityService>().HasFreshProofAsync(rUser, null, token, AccountProofPurpose.Deletion));
        Assert.Single(outcomes.Where(value => value));
    }

    [MariaDbAcceptanceFact]
    public async Task ParallelGoalChanges_KeepDistinctCommittedHistoryRevisions()
    {
        await using var factory = Factory();
        await factory.InitializeAsync();
        var id = await SeedAsync(factory);
        using var left = factory.Services.CreateScope();
        using var right = factory.Services.CreateScope();
        var outcomes = await Task.WhenAll(
            left.ServiceProvider.GetRequiredService<IGoalsService>().UpdateGoalAsync(id, GoalArea.Writing, 3),
            right.ServiceProvider.GetRequiredService<IGoalsService>().UpdateGoalAsync(id, GoalArea.Writing, 5));
        Assert.All(outcomes, outcome => Assert.Equal(OperationStatus.Success, outcome.Status));
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var revisions = await db.UserGoalTargetRevisions.Where(row => row.UserId == id).OrderBy(row => row.RevisionNumber).ToListAsync();
        Assert.Equal(new[] { 1, 2 }, revisions.Select(row => row.RevisionNumber));
        var goal = await db.UserGoals.SingleAsync(row => row.UserId == id && row.GoalArea == GoalArea.Writing);
        Assert.Equal(revisions[^1].TargetValue, goal.TargetValue);
    }

    [MariaDbAcceptanceFact]
    public async Task AccountDeletion_WorksWithRetryingMySqlExecutionStrategy()
    {
        await using var factory = Factory();
        await factory.InitializeAsync();
        var id = await SeedAsync(factory);
        using var scope = factory.Services.CreateScope();
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB"), ServerVersion.Parse("10.5.23-mariadb"), mysql => mysql.EnableRetryOnFailure()).Options;
        await using var db = new DbflashcardContext(options);
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var security = new AccountSecurityService(db, scope.ServiceProvider.GetRequiredService<IAppEmailSender>(), scope.ServiceProvider.GetRequiredService<IConfiguration>(), time);
        var deletion = new AccountDeletionService(db, security, scope.ServiceProvider.GetRequiredService<IAvatarUploadService>(), time, NullLogger<AccountDeletionService>.Instance);
        var result = await deletion.DeleteAsync(id, "CONFIRM", Password, null);
        Assert.True(result.Succeeded, result.Error);
        Assert.False(await db.Users.AsNoTracking().AnyAsync(row => row.UserId == id));
    }

    [MariaDbAcceptanceFact]
    public async Task ConcurrentDeletion_CannotRemoveTheLastActiveAdministrator()
    {
        await using var factory = Factory();
        await factory.InitializeAsync();
        using (var setup = factory.Services.CreateScope())
        {
            await setup.ServiceProvider.GetRequiredService<DbflashcardContext>().Users
                .Where(row => row.Role == Roles.Admin).ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.Status, UserStatus.Blocked).SetProperty(row => row.LockExpiry, (DateTime?)null));
        }
        var first = await SeedAsync(factory, Roles.Admin);
        var second = await SeedAsync(factory, Roles.Admin);
        using var left = factory.Services.CreateScope();
        using var right = factory.Services.CreateScope();
        var results = await Task.WhenAll(
            left.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(first, "CONFIRM", Password, null),
            right.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(second, "CONFIRM", Password, null));
        Assert.Single(results.Where(result => result.Succeeded));
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(1, await db.Users.CountAsync(row => row.Role == Roles.Admin && row.Status != UserStatus.Blocked && row.AnonymizedAt == null));
    }

    [MariaDbAcceptanceFact]
    public async Task Deletion_PreservesPaymentReferenceButRemovesPersonalAndLearningIdentity()
    {
        await using var factory = Factory();
        await factory.InitializeAsync();
        var id = await SeedAsync(factory);
        long orderId;
        using (var setup = factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var user = await db.Users.SingleAsync(row => row.UserId == id);
            user.Streak = 12; user.LongestStreak = 20; user.LastStudyDate = DateTime.UtcNow.Date; user.Goal = 17;
            var plan = new PremiumPlan { Code = "rv" + Guid.NewGuid().ToString("N")[..12], Name = "Synthetic review plan", Description = "Isolated test", PriceVnd = 1000, DurationDays = 30, IsActive = false, CreatedAtUtc = DateTime.UtcNow };
            var order = new PaymentOrder
            {
                OrderCode = "rv" + Guid.NewGuid().ToString("N")[..20], UserId = id, Plan = plan, Provider = "vnpay",
                AmountVnd = 1000, Currency = "VND", Status = "paid", CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };
            db.PaymentOrders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(id, "CONFIRM", Password, null);
        Assert.True(result.Succeeded, result.Error);
        Assert.False(result.Removed);
        var verify = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var stored = await verify.Users.AsNoTracking().SingleAsync(row => row.UserId == id);
        Assert.NotNull(stored.AnonymizedAt);
        Assert.Null(stored.FullName); Assert.Null(stored.Streak); Assert.Null(stored.LongestStreak);
        Assert.Null(stored.LastStudyDate); Assert.Null(stored.Goal); Assert.Null(stored.CreatedAt);
        Assert.False(BCrypt.Net.BCrypt.Verify(Password, stored.PasswordHash));
        Assert.EndsWith("@users.invalid", stored.Email);
        Assert.Equal(id, (await verify.PaymentOrders.AsNoTracking().SingleAsync(row => row.Id == orderId)).UserId);
    }

    private static MariaDbAcceptanceFactory Factory(ReviewClock? clock = null)
        => new(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!, Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID"), services =>
        {
            if (clock != null) { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }
        });

    private static async Task<int> SeedAsync(MariaDbAcceptanceFactory factory, string role = Roles.Standard)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = new User { Email = $"native-{Guid.NewGuid():N}@example.test", FullName = "Native review", PasswordHash = Hash, Role = role, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.UserId;
    }

    private sealed class ReviewClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 11, 13, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
