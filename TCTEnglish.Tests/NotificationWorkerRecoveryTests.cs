using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using TCTVocabulary.Workers;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class NotificationWorkerRecoveryTests
{
    [Fact]
    public async Task Worker_CatchUpAfterEvening_CreatesCheckpointOnce()
    {
        // 21:00 VN on 2026-10-30 = 14:00 UTC
        var utc = new DateTime(2026, 10, 30, 14, 0, 0, DateTimeKind.Utc);

        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(utc));
        });
        await factory.InitializeAsync();

        var worker = new NotificationWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationWorker>.Instance);

        for (var poll = 0; poll < 8; poll++)
        {
            await worker.TryRunDueJobsAsync(CancellationToken.None);
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var clock = scope.ServiceProvider.GetRequiredService<LearningBusinessClock>();

        var runs = await context.NotificationJobRuns
            .Where(r => r.JobName == NotificationWorker.DailyJobName && r.BusinessDate == clock.BusinessDate)
            .ToListAsync();

        Assert.Single(runs);
        Assert.Equal("succeeded", runs[0].Status);
    }

    [Fact]
    public async Task Worker_BeforeEvening_DoesNotRun()
    {
        // 10:00 VN on 2026-10-30 = 03:00 UTC
        var utc = new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(utc));
        });
        await factory.InitializeAsync();

        var worker = new NotificationWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationWorker>.Instance);

        await worker.TryRunDueJobsAsync(CancellationToken.None);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Empty(await context.NotificationJobRuns.ToListAsync());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
