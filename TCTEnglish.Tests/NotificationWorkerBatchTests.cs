using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Workers;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class NotificationWorkerBatchTests
{
    private static readonly DateTime EveningUtc = new(2026, 10, 30, 14, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(201)]
    [InlineData(450)]
    public async Task BadgeBatch_WithOneExistingKey_EventuallyNotifiesEveryEligibleUserOnce(int eligibleCount)
    {
        await using var factory = CreateFactory();
        await factory.InitializeAsync();
        var ids = await SeedEligibleUsersAsync(factory, eligibleCount, firstUserId: 5000 + eligibleCount);
        var blockedId = ids[^1] + 1;
        await AddUserAsync(factory, blockedId, UserStatus.Blocked);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            context.Notifications.Add(BadgeNear(ids[0], EveningUtc));
            await context.SaveChangesAsync();
        }

        var worker = CreateWorker(factory);
        await RunUntilSucceededAsync(worker, factory);

        using var verify = factory.Services.CreateScope();
        var verifyContext = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var rows = await verifyContext.Notifications.AsNoTracking()
            .Where(n => n.Type == NotificationType.BadgeNear && ids.Contains(n.UserId))
            .Select(n => n.EventKey)
            .ToListAsync();
        Assert.Equal(eligibleCount, rows.Count);
        Assert.Equal(rows.Count, rows.Distinct().Count());
        Assert.Equal(0, await verifyContext.Notifications.CountAsync(n => n.UserId == blockedId));
        var run = await verifyContext.NotificationJobRuns.SingleAsync();
        Assert.Equal("succeeded", run.Status);
        Assert.Equal(NotificationWorker.PhaseComplete, run.Phase);
    }

    [Fact]
    public async Task NonUniqueSaveFailure_DoesNotAdvanceCursor_AndRetryCompletes()
    {
        await using var factory = CreateFactory();
        await factory.InitializeAsync();
        await SeedEligibleUsersAsync(factory, 3, firstUserId: 8100);
        var worker = CreateWorker(factory);
        worker.BeforeBatchCommitAsync = _ => throw new DbUpdateException("forced write failure", new InvalidOperationException("disk"));

        await worker.TryRunDueJobsAsync(CancellationToken.None);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var run = await context.NotificationJobRuns.SingleAsync();
            Assert.Equal("failed", run.Status);
            Assert.False(string.IsNullOrWhiteSpace(run.LastError));
            Assert.Null(run.LastUserId);
            Assert.Equal(0, await context.Notifications.CountAsync(n => n.Type == NotificationType.BadgeNear && n.UserId >= 8100));
        }

        worker.BeforeBatchCommitAsync = null;
        await RunUntilSucceededAsync(worker, factory);
        using var verify = factory.Services.CreateScope();
        var verifyContext = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(3, await verifyContext.Notifications.CountAsync(n => n.Type == NotificationType.BadgeNear && n.UserId >= 8100));
    }

    [Fact]
    public async Task ExpiredLease_LetsTheNewWorkerCommit_AndTheOldTokenDoesNotDuplicate()
    {
        await using var factory = CreateFactory();
        await factory.InitializeAsync();
        await SeedEligibleUsersAsync(factory, 5, firstUserId: 8200);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            context.NotificationJobRuns.Add(new NotificationJobRun
            {
                JobName = NotificationWorker.DailyJobName,
                BusinessDate = new DateTime(2026, 10, 30),
                Status = "queued",
                Phase = NotificationWorker.PhaseBadge,
                AttemptCount = 0
            });
            await context.SaveChangesAsync();
        }

        var workerA = CreateWorker(factory);
        var workerB = CreateWorker(factory);
        workerA.BeforeBatchCommitAsync = async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            await context.NotificationJobRuns.ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.LeaseUntilUtc, EveningUtc.AddMinutes(-1)));
            await workerB.TryRunDueJobsAsync(CancellationToken.None);
        };

        await workerA.TryRunDueJobsAsync(CancellationToken.None);

        using var verify = factory.Services.CreateScope();
        var verifyContext = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var keys = await verifyContext.Notifications.AsNoTracking()
            .Where(n => n.UserId >= 8200 && n.UserId < 8205)
            .Select(n => n.EventKey)
            .ToListAsync();
        Assert.Equal(5, keys.Count);
        Assert.Equal(5, keys.Distinct().Count());
    }

    [Fact]
    public async Task Heartbeat_ExtendsTheLeaseAfterOneMinuteOfWork()
    {
        var clock = new MutableTimeProvider(EveningUtc);
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        });
        await factory.InitializeAsync();
        var worker = CreateWorker(factory);
        worker.BeforeBatchCommitAsync = _ =>
        {
            clock.Utc = new DateTimeOffset(EveningUtc.AddMinutes(2));
            return Task.CompletedTask;
        };

        await worker.TryRunDueJobsAsync(CancellationToken.None);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var run = await context.NotificationJobRuns.SingleAsync();
        Assert.Equal("running", run.Status);
        Assert.NotNull(run.LeaseUntilUtc);
        Assert.Equal(EveningUtc.AddMinutes(7), DateTime.SpecifyKind(run.LeaseUntilUtc.Value, DateTimeKind.Utc));
    }

    private static TestWebApplicationFactory CreateFactory()
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(EveningUtc));
        });
    }

    private static NotificationWorker CreateWorker(TestWebApplicationFactory factory)
    {
        return new NotificationWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationWorker>.Instance);
    }

    private static async Task RunUntilSucceededAsync(NotificationWorker worker, TestWebApplicationFactory factory)
    {
        for (var poll = 0; poll < 12; poll++)
        {
            await worker.TryRunDueJobsAsync(CancellationToken.None);
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var status = await context.NotificationJobRuns.Select(r => r.Status).SingleOrDefaultAsync();
            if (status == "succeeded")
            {
                return;
            }
        }

        throw new InvalidOperationException("Notification job did not succeed.");
    }

    private static async Task<List<int>> SeedEligibleUsersAsync(TestWebApplicationFactory factory, int count, int firstUserId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await context.Users.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.Status, UserStatus.Blocked));
        var ids = Enumerable.Range(firstUserId, count).ToList();
        context.Users.AddRange(ids.Select(id => NewUser(id, UserStatus.Online)));
        await context.SaveChangesAsync();
        return ids;
    }

    private static async Task AddUserAsync(TestWebApplicationFactory factory, int userId, UserStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        context.Users.Add(NewUser(userId, status));
        await context.SaveChangesAsync();
    }

    private static User NewUser(int userId, UserStatus status) => new()
    {
        UserId = userId,
        Email = $"batch{userId}@tct.test",
        PasswordHash = "hash",
        FullName = "Batch " + userId,
        Role = Roles.Standard,
        Status = status,
        CreatedAt = EveningUtc
    };

    private static Notification BadgeNear(int userId, DateTime createdAt) => new()
    {
        UserId = userId,
        Type = NotificationType.BadgeNear,
        Title = "existing",
        Message = "existing",
        IconClass = "fas fa-award",
        RelatedUrl = "/Goals",
        EventKey = $"badge-near:{userId}:1:2026-10-30",
        CreatedAt = createdAt
    };

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Utc { get; set; }
        public MutableTimeProvider(DateTime utcNow) => Utc = new DateTimeOffset(utcNow);
        public override DateTimeOffset GetUtcNow() => Utc;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
