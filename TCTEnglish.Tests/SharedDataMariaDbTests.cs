using Microsoft.EntityFrameworkCore;
using TCTEnglish.Infrastructure.SharedData;
using TCTVocabulary.Models;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

public class SharedDataMariaDbTests
{
    public sealed class DisposableMariaDbFactAttribute : FactAttribute
    {
        public DisposableMariaDbFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("TCT_SHARED_MARIADB_TESTS") != "1")
                Skip = "Requires the CI-owned disposable MariaDB after verify-mariadb.ps1; never runs against local/hosting.";
        }
    }

    [DisposableMariaDbFact]
    public async Task UpgradeRerunUnicodeIdsConflictAndLearnerHistoryOnHostingProvider()
    {
        // No configurable connection: this exact disposable CI endpoint is mandatory.
        var options = new DbContextOptionsBuilder<DbflashcardContext>().UseMySql(
            "Server=127.0.0.1;Port=33307;Database=tct_ci;User=tct_ci;Password=CiDisposableOnly;SslMode=None;",
            new MariaDbServerVersion(new Version(10,5,23))).Options;
        await using var db = new DbflashcardContext(options);
        await db.Database.OpenConnectionAsync();
        await using (var identity = db.Database.GetDbConnection().CreateCommand())
        {
            identity.CommandText = "SELECT CONCAT(DATABASE(),':',@@lower_case_table_names,':',SUBSTRING_INDEX(VERSION(),'-',1))";
            Assert.Equal("tct_ci:1:10.5.23", await identity.ExecuteScalarAsync());
        }
        var sentinel = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == 900001);
        Assert.Equal("ci-user@example.test", sentinel.Email);
        Assert.Equal("not-a-login-password", sentinel.PasswordHash);
        var users = await db.Users.CountAsync();
        var migrationCount = (await db.Database.GetAppliedMigrationsAsync()).Count();
        await db.Database.MigrateAsync();
        Assert.Equal(migrationCount, (await db.Database.GetAppliedMigrationsAsync()).Count());
        using var folder = new TempFolder();
        var one = await WriteAsync(folder.Path, Package(1, Reading(title:"Bài đọc tiếng Việt — dùng chung")));
        var engine = new SharedDataEngine(db,folder.Path,new());
        await engine.ApplyAsync([one]);
        var binding = await db.SharedDataBindings.SingleAsync(b => b.Key == "team.reading.one");
        db.UserReadingHistories.Add(new() {UserId=sentinel.UserId,ReadingPassageId=binding.EntityId,Score=98,IsCompleted=true,ViewedAt=DateTime.UtcNow});
        await db.SaveChangesAsync();
        await engine.ApplyAsync([one]);
        var two = await WriteAsync(folder.Path, Package(2,Reading(title:"Nội dung phiên bản hai")));
        await engine.ApplyAsync([one,two]);
        var row = await db.ReadingPassages.SingleAsync(r=>r.Id==binding.EntityId);
        Assert.Equal("Nội dung phiên bản hai",row.Title);
        row.Title="Bản chỉnh riêng chưa xuất"; await db.SaveChangesAsync();
        var three = await WriteAsync(folder.Path,Package(3,Reading(title:"Incoming conflict"),Reading("team.reading.must.rollback")));
        await Assert.ThrowsAsync<SharedDataException>(()=>engine.ApplyAsync([one,two,three]));
        db.ChangeTracker.Clear();
        Assert.Equal("Bản chỉnh riêng chưa xuất",(await db.ReadingPassages.SingleAsync(r=>r.Id==binding.EntityId)).Title);
        Assert.False(await db.SharedDataBindings.AnyAsync(b=>b.Key=="team.reading.must.rollback"));
        Assert.Equal(98,(await db.UserReadingHistories.SingleAsync(h=>h.UserId==sentinel.UserId && h.ReadingPassageId==binding.EntityId)).Score);
        Assert.Equal(2,await db.SharedDataApplications.CountAsync(a=>a.PackageId=="team.demo"));
        Assert.Equal(users,await db.Users.CountAsync());
        Assert.Equal(sentinel.PasswordHash,(await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==sentinel.UserId)).PasswordHash);
    }
}
