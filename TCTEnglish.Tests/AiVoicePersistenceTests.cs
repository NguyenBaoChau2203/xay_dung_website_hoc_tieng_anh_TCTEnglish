using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.AI.Voice;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiVoicePersistenceTests
{
    [Fact]
    public async Task StopBeforeReserveSurvivesANewScope()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var id = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            await Quota(db).CancelAsync(TestDataIds.UserId, id, "stopped", default);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var error = await Assert.ThrowsAsync<VoiceException>(() => Quota(db).ReserveAsync(TestDataIds.UserId, id, Topic, false, default));
            Assert.Equal(409, error.Status);
            Assert.Empty(await db.AiVoiceUserLeases.ToListAsync());
        }
    }

    [Fact]
    public async Task UnknownOutcomeRetainsLeaseAndReservationAcrossScopes()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var id = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var quota = Quota(db);
        var reserved = await quota.ReserveAsync(TestDataIds.UserId, id, Topic, false, default);
        await quota.MutateAsync(id, s => { s.ProviderDispatched = true; s.SetupFinished = true; }, default);
        await quota.CancelAsync(TestDataIds.UserId, id, "network", default);
        await quota.SettleClosedAsync(id, false, default);
        Assert.NotEmpty(await db.AiVoiceUserLeases.AsNoTracking().ToListAsync());
        await Assert.ThrowsAsync<VoiceException>(() => quota.ReserveAsync(TestDataIds.UserId, Guid.NewGuid(), Topic, false, default));
        await quota.SettleClosedAsync(id, true, default);
        await quota.SettleClosedAsync(id, true, default);
        // A late setup-error continuation must not settle an already-accounted call twice.
        await quota.MutateAsync(id, s => s.State = "closing", default);
        await quota.SettleClosedAsync(id, true, default);
        Assert.Empty(await db.AiVoiceUserLeases.AsNoTracking().ToListAsync());
        var day = await db.AiVoiceUsageDays.AsNoTracking().SingleAsync();
        Assert.Equal(0, day.ReservedSeconds);
        Assert.Equal(reserved.ReservedSeconds, day.ConsumedSeconds);
    }

    internal static readonly VoiceTopicSnapshot Topic = new("catalog", 1, "Introduction", "Meet a friend", "Friend", "Visitor", "A2");

    [Fact]
    public async Task MaximumVietnameseScenarioFitsSnapshotWithoutTruncation()
    {
        await using var factory = new TestWebApplicationFactory(); await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var topic = Topic with { Scenario = new string('ệ', 2000) };
        var row = await Quota(db).ReserveAsync(TestDataIds.UserId, Guid.NewGuid(), topic, false, default);
        Assert.True(row.TopicSnapshot.Length <= 6000);
        Assert.Equal(topic, System.Text.Json.JsonSerializer.Deserialize<VoiceTopicSnapshot>(row.TopicSnapshot));
    }
    internal static AiVoiceQuotaService Quota(DbflashcardContext db) => new(db, Options.Create(new AiVoiceOptions()), TimeProvider.System);
}
