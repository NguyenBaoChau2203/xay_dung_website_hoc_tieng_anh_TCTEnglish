using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TCTEnglish.Models;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class QuotaMigrationUpgradeTests
{
    [MigrationUpgradeFact]
    public async Task UpgradeOctoberSchema_ThenRepeat_PreservesExistingUserAndHistory()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId();
        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);
        await MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(db.Database.GetDbConnection(), target.Database, run);
        // Establish the real baseline on this explicitly guarded fresh DB. Do not
        // depend on a design-time CLI connection that can fall back to dev secrets.
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        await db.GetService<IMigrator>().MigrateAsync("20261030113000_LearningImportSourceHash");
        var history = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        Assert.Equal(new[] { "20261102104017_AddAiUserDailyQuota", "20261102111404_AddAiQuotaReservationLedger",
            "20261103141227_RemoveRedundantAiQuotaUniqueIndex" }, pending);
        var user = new User { Email = "upgrade-fixture@example.invalid", PasswordHash = "synthetic-only", FullName = "Upgrade preservation", Role = Roles.Standard, Status = UserStatus.Offline, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync();
        var id = user.UserId;
        await db.GetService<IMigrator>().MigrateAsync("20261102111404_AddAiQuotaReservationLedger");
        db.AiUserDailyQuotas.Add(new AiUserDailyQuota
        {
            UserId = id, DayUtc = DateTime.UtcNow.Date, PendingQuestions = 2, PendingTokens = 200, UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var preserved = await db.Users.SingleAsync(u => u.UserId == id);
        Assert.Equal("upgrade-fixture@example.invalid", preserved.Email);
        Assert.Equal("synthetic-only", preserved.PasswordHash);
        Assert.Equal(2, (await db.AiUserDailyQuotas.AsNoTracking().SingleAsync(q => q.UserId == id)).PendingQuestions);
        await db.Database.OpenConnectionAsync();
        using var indexCheck = db.Database.GetDbConnection().CreateCommand();
        indexCheck.CommandText = "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='AiUserDailyQuotas' AND INDEX_NAME='UX_AiUserDailyQuotas_UserId_DayUtc'";
        Assert.Equal(0, Convert.ToInt32(await indexCheck.ExecuteScalarAsync()));
        indexCheck.CommandText = "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='AiUserDailyQuotas' AND INDEX_NAME='PRIMARY'";
        Assert.Equal(2, Convert.ToInt32(await indexCheck.ExecuteScalarAsync()));
        Assert.Equal(history.Concat(pending), await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}

public sealed class MigrationUpgradeFactAttribute : FactAttribute
{
    public MigrationUpgradeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TCT_MIGRATION_UPGRADE") != "1")
            Skip = "Requires fresh guarded October-schema disposable DB; never reset an existing database.";
    }
}
