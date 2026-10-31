using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Workers;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// G03 MariaDB provider slice: notification batch dedup/lease on disposable acceptance DB.
/// Requires TCT_ACCEPTANCE_MARIADB + TCT_ACCEPTANCE_RUN_ID.
/// </summary>
[Collection("MariaDbAcceptance")]
[Trait("Category", "MariaDbAcceptance")]
public sealed class MariaDbNotificationWorkerAcceptanceTests
{
    private static readonly DateTime EveningUtc = new(2026, 10, 30, 14, 0, 0, DateTimeKind.Utc);

    [MariaDbAcceptanceTheory]
    [InlineData(201)]
    [InlineData(450)]
    public async Task BadgeBatch_Recipients_DedupsExistingKey_OnMariaDb(int eligibleCount)
    {
        var connectionString = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");

        await using var factory = new MariaDbAcceptanceFactory(
            connectionString,
            runId,
            services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(EveningUtc));
            });
        await factory.InitializeAsync();

        var ids = await SeedEligibleUsersAsync(factory, eligibleCount, firstUserId: 15000 + eligibleCount);
        var blockedId = ids[^1] + 1;
        await AddUserAsync(factory, blockedId, UserStatus.Blocked);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            context.Notifications.Add(new Notification
            {
                UserId = ids[0],
                Type = NotificationType.BadgeNear,
                Title = "existing",
                Message = "existing",
                IconClass = "fas fa-award",
                RelatedUrl = "/Goals",
                EventKey = $"badge-near:{ids[0]}:1:2026-10-30",
                CreatedAt = EveningUtc
            });
            await context.SaveChangesAsync();
        }

        var worker = CreateWorker(factory);
        await RunUntilSucceededAsync(worker, factory);

        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var rows = await db.Notifications.AsNoTracking()
            .Where(n => n.Type == NotificationType.BadgeNear && ids.Contains(n.UserId))
            .Select(n => n.EventKey)
            .ToListAsync();
        Assert.Equal(eligibleCount, rows.Count);
        Assert.Equal(rows.Count, rows.Distinct().Count());
        Assert.Equal(0, await db.Notifications.CountAsync(n => n.UserId == blockedId));
        var run = await db.NotificationJobRuns.SingleAsync();
        Assert.Equal("succeeded", run.Status);
        Assert.Equal(NotificationWorker.PhaseComplete, run.Phase);
    }

    [MariaDbAcceptanceFact]
    public async Task ExpiredLease_LetsNewWorkerCommit_WithoutDuplicate_OnMariaDb()
    {
        var connectionString = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");

        await using var factory = new MariaDbAcceptanceFactory(
            connectionString,
            runId,
            services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(EveningUtc));
            });
        await factory.InitializeAsync();
        await SeedEligibleUsersAsync(factory, 5, firstUserId: 18200);

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
            // Finish all owned phases before the stale worker resumes, so the
            // assertion proves its token cannot revert a completed checkpoint.
            await RunUntilSucceededAsync(workerB, factory);
        };

        await workerA.TryRunDueJobsAsync(CancellationToken.None);

        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var keys = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId >= 18200 && n.UserId < 18205)
            .Select(n => n.EventKey)
            .ToListAsync();
        Assert.Equal(5, keys.Count);
        Assert.Equal(5, keys.Distinct().Count());
        var completed = await db.NotificationJobRuns.AsNoTracking().SingleAsync();
        Assert.Equal("succeeded", completed.Status);
        Assert.Equal(NotificationWorker.PhaseComplete, completed.Phase);
    }

    private static NotificationWorker CreateWorker(MariaDbAcceptanceFactory factory)
    {
        return new NotificationWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationWorker>.Instance);
    }

    private static async Task RunUntilSucceededAsync(
        NotificationWorker worker,
        MariaDbAcceptanceFactory factory)
    {
        for (var poll = 0; poll < 30; poll++)
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

        throw new InvalidOperationException("Notification job did not succeed on MariaDB.");
    }

    private static async Task<List<int>> SeedEligibleUsersAsync(
        MariaDbAcceptanceFactory factory,
        int count,
        int firstUserId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await context.Users.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.Status, UserStatus.Blocked));
        var ids = Enumerable.Range(firstUserId, count).ToList();
        context.Users.AddRange(ids.Select(id => new User
        {
            UserId = id,
            Email = $"mdb{id}@tct.test",
            PasswordHash = "hash",
            FullName = "MDB " + id,
            Role = Roles.Standard,
            Status = UserStatus.Online,
            CreatedAt = EveningUtc
        }));
        await context.SaveChangesAsync();
        return ids;
    }

    private static async Task AddUserAsync(
        MariaDbAcceptanceFactory factory,
        int userId,
        UserStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        context.Users.Add(new User
        {
            UserId = userId,
            Email = $"mdb{userId}@tct.test",
            PasswordHash = "hash",
            FullName = "MDB " + userId,
            Role = Roles.Standard,
            Status = status,
            CreatedAt = EveningUtc
        });
        await context.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
