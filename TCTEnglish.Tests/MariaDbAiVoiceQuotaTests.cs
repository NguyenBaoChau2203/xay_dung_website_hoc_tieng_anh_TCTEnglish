using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;
using TCTEnglish.Models;
using TCTEnglish.Services.AI.Voice;
using TCTVocabulary.Models;
using Xunit;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TCTEnglish.Infrastructure.SharedData;

namespace TCTEnglish.Tests;

public sealed class VoiceMariaDbFactAttribute : FactAttribute
{
    public VoiceMariaDbFactAttribute()
    { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TCT_VOICE_DISPOSABLE_MARIADB"))) Skip = "Requires the isolated voice migration-check database; never dev/hosting."; }
}

public sealed class MariaDbAiVoiceQuotaTests
{
    [VoiceMariaDbFact]
    public async Task AdditiveUpgradePreservesExistingUserChatAndSubscriptionAndRepeatsSafely()
    {
        await using var guarded = Open();
        var builder = new MySqlConnectionStringBuilder(guarded.Database.GetConnectionString()!);
        var database = "tct_voice_upgrade_" + Guid.NewGuid().ToString("N");
        await using (var connection = new MySqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new MySqlCommand("CREATE DATABASE `" + database + "`", connection); await command.ExecuteNonQueryAsync();
        }
        builder.Database = database;
        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>().UseMySql(builder.ConnectionString, new MariaDbServerVersion(new Version(10, 5, 23))).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261116102446_AddUserDailyActivityDailyChallengeCompleted");
        var user = new User { Email = "preserved@voice.test.invalid", PasswordHash = "fixture-not-login", Role = Roles.Premium, FullName = "Preserved fixture" };
        db.Users.Add(user); await db.SaveChangesAsync();
        var plan = new PremiumPlan { Code = "voice-upgrade", Name = "Fixture plan", Description = "Upgrade proof", DurationDays = 30, PriceVnd = 1 };
        db.PremiumPlans.Add(plan); await db.SaveChangesAsync();
        var sub = new UserSubscription { UserId = user.UserId, PlanId = plan.Id, Status = "Active", StartsAtUtc = DateTime.UtcNow.Date, EndsAtUtc = DateTime.UtcNow.Date.AddDays(30), CreatedAtUtc = DateTime.UtcNow.Date };
        var conversation = new AiConversation { Id = Guid.NewGuid(), UserId = user.UserId, Title = "Existing text chat", CreatedAtUtc = DateTime.UtcNow.Date, UpdatedAtUtc = DateTime.UtcNow.Date };
        db.UserSubscriptions.Add(sub); db.AiConversations.Add(conversation); await db.SaveChangesAsync();
        // DATETIME preserves wall-clock ticks, not DateTime.Kind; do not mistake CLR metadata for data loss.
        var before = System.Text.Json.JsonSerializer.Serialize(new { user.Email, user.PasswordHash, user.Role, user.FullName, sub.Status, EndsAtUtcTicks = sub.EndsAtUtc.Ticks, conversation.Title });
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        user = await db.Users.SingleAsync(); sub = await db.UserSubscriptions.SingleAsync(); conversation = await db.AiConversations.SingleAsync();
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(new { user.Email, user.PasswordHash, user.Role, user.FullName, sub.Status, EndsAtUtcTicks = sub.EndsAtUtc.Ticks, conversation.Title }));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.Empty(await db.AiVoiceSessions.ToListAsync());
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "shared"));
        var packages = (await SharedDataFormat.LoadAsync(root)).Where(p => p.Package.Records.All(r => r.Kind == "AiVoiceCatalogTopic")).ToList();
        Assert.Equal(6, packages.Count);
        var webRoot = Path.Combine(Path.GetTempPath(), "tct-voice-no-assets");
        await new SharedDataEngine(db, webRoot, []).ApplyAsync(packages);
        var topicIds = await db.AiVoiceCatalogTopics.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        await new SharedDataEngine(db, webRoot, []).ApplyAsync(packages);
        Assert.Equal(topicIds, await db.AiVoiceCatalogTopics.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToListAsync());
        Assert.Equal(6, topicIds.Count);
        var unicode = AiVoicePersistenceTests.Topic with { Scenario = new string('ệ', 2000) };
        var session = await AiVoicePersistenceTests.Quota(db).ReserveAsync(user.UserId, Guid.NewGuid(), unicode, false, default);
        Assert.Equal(unicode.Scenario, System.Text.Json.JsonSerializer.Deserialize<VoiceTopicSnapshot>(session.TopicSnapshot)!.Scenario);
        // The database remains inside this task's disposable container; no destructive SQL cleanup.
    }

    [VoiceMariaDbFact]
    public async Task TwoIndependentConnectionsCanReserveOnlyOneLease()
    {
        await using var seed = Open();
        var user = new User { Email = Guid.NewGuid().ToString("N") + "@voice.test.invalid", PasswordHash = "not-a-login", Role = Roles.Standard };
        seed.Users.Add(user); await seed.SaveChangesAsync();
        await using var a = Open(); await using var b = Open();
        var qa = Quota(a); var qb = Quota(b);
        async Task<bool> Try(AiVoiceQuotaService q)
        { try { await q.ReserveAsync(user.UserId, Guid.NewGuid(), AiVoicePersistenceTests.Topic, false, default); return true; } catch (VoiceException ex) when (ex.Status == 409) { return false; } }
        var results = await Task.WhenAll(Try(qa), Try(qb));
        Assert.Single(results.Where(x => x));
        Assert.Equal(1, await seed.AiVoiceUserLeases.CountAsync(x => x.UserId == user.UserId));
        Assert.Equal(180, (await seed.AiVoiceUsageDays.SingleAsync(x => x.UserId == user.UserId)).ReservedSeconds);
        var sessionId = (await seed.AiVoiceUserLeases.SingleAsync(x => x.UserId == user.UserId)).SessionId;
        await qa.MutateAsync(sessionId, s => s.SetupFinished = true, default);
        await qa.SettleClosedAsync(sessionId, true, default);
    }

    [VoiceMariaDbFact]
    public async Task UtcMidnightDoesNotMoveOldReservationAndConfirmedSettleIsIdempotent()
    {
        await using var db = Open();
        var user = new User { Email = Guid.NewGuid().ToString("N") + "@voice.test.invalid", PasswordHash = "not-a-login", Role = Roles.Standard };
        db.Users.Add(user); await db.SaveChangesAsync();
        var clock = new VoiceClock { Now = new DateTimeOffset(2026, 11, 18, 23, 59, 30, TimeSpan.Zero) };
        var q = Quota(db, clock);
        var id = Guid.NewGuid(); var row = await q.ReserveAsync(user.UserId, id, AiVoicePersistenceTests.Topic, false, default);
        Assert.Equal(30, row.ReservedSeconds); Assert.Equal(clock.Now.Date.AddDays(1), row.ExpiresAtUtc);
        clock.Now = clock.Now.AddMinutes(1);
        await q.MutateAsync(id, s => { s.SetupFinished = true; s.ProviderDispatched = true; }, default);
        await q.SettleClosedAsync(id, false, default);
        await Assert.ThrowsAsync<VoiceException>(() => q.ReserveAsync(user.UserId, Guid.NewGuid(), AiVoicePersistenceTests.Topic, false, default));
        await q.SettleClosedAsync(id, true, default); await q.SettleClosedAsync(id, true, default);
        var oldDay = await db.AiVoiceUsageDays.AsNoTracking().SingleAsync(x => x.UserId == user.UserId);
        Assert.Equal(30, oldDay.ConsumedSeconds); Assert.Equal(0, oldDay.ReservedSeconds);
        var next = await q.ReserveAsync(user.UserId, Guid.NewGuid(), AiVoicePersistenceTests.Topic, false, default);
        Assert.Equal(180, next.ReservedSeconds); Assert.NotEqual(oldDay.UtcDate, next.QuotaDayUtc);
        await q.MutateAsync(next.Id, s => s.SetupFinished = true, default); await q.SettleClosedAsync(next.Id, true, default);
    }

    [VoiceMariaDbFact]
    public async Task AccountScrubPreservesOnlyDurableCleanupWithoutForeignKeyBlocker()
    {
        await using var db = Open();
        var user = new User { Email = Guid.NewGuid().ToString("N") + "@voice.test.invalid", PasswordHash = "not-a-login", Role = Roles.Standard };
        db.Users.Add(user); await db.SaveChangesAsync();
        var q = Quota(db); var id = Guid.NewGuid();
        await q.ReserveAsync(user.UserId, id, AiVoicePersistenceTests.Topic, false, default);
        await q.MutateAsync(id, s => { s.ProviderDispatched = true; s.ProviderCallId = "rtc_cleanup"; s.SetupFinished = true; }, default);
        db.AiVoiceTurns.Add(new() { SessionId = id, ItemId = "item", Speaker = "user", Text = "private" }); await db.SaveChangesAsync();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await AiVoiceHistoryService.ScrubAccountAsync(db, user.UserId, default);
            await db.Users.Where(x => x.UserId == user.UserId).ExecuteDeleteAsync(); await tx.CommitAsync();
        }
        var retained = await db.AiVoiceSessions.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Null(retained.UserId); Assert.Equal("rtc_cleanup", retained.ProviderCallId);
        Assert.Empty(retained.TopicSnapshot); Assert.Empty(retained.TopicTitle); Assert.True(retained.HistoryDeleted);
        Assert.False(await db.AiVoiceTurns.AnyAsync(x => x.SessionId == id));
        await q.SettleClosedAsync(id, true, default); // Synthetic call ID; no provider traffic occurred.
    }

    private static DbflashcardContext Open()
    {
        var cs = Environment.GetEnvironmentVariable("TCT_VOICE_DISPOSABLE_MARIADB") ?? throw new InvalidOperationException("Missing disposable connection.");
        var b = new MySqlConnectionStringBuilder(cs);
        if (b.Server != "127.0.0.1" || b.Port != 33317 || b.Database != "tct_migration_check") throw new InvalidOperationException("Refusing non-disposable database.");
        return new(new DbContextOptionsBuilder<DbflashcardContext>().UseMySql(cs, new MariaDbServerVersion(new Version(10, 5, 23))).Options);
    }
    private sealed class VoiceClock : TimeProvider { public DateTimeOffset Now; public override DateTimeOffset GetUtcNow() => Now; }
    // Preserve earlier failed-run fixtures; these tests exercise per-user invariants, not global capacity.
    private static AiVoiceQuotaService Quota(DbflashcardContext db, TimeProvider? clock = null) =>
        new(db, Options.Create(new AiVoiceOptions { MaxConcurrentSessions = 100 }), clock ?? TimeProvider.System);
}
