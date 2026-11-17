using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class WritingBriefMariaDbTests
{
    [WritingBriefUpgradeFact]
    public async Task UpgradeRepeatPreservesOldRowsAndRealProviderQuotaReplayCleanup()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId();
        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);
        await MariaDbAcceptanceConnectionGuard.BootstrapSentinelAsync(db.Database.GetDbConnection(), target.Database, run);
        await MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(db.Database.GetDbConnection(), target.Database, run);
        await db.Database.MigrateAsync("20261114191845_AddUserWritingExerciseVisits");
        db.Users.Add(new User { UserId = 92001, Email = "preserve-brief@example.invalid", PasswordHash = "synthetic", Role = Roles.Premium, Streak = 9 });
        await db.SaveChangesAsync();
        var before = await WritingRecentExercisesTests.RewardSnapshot(db);
        var lessons = await db.WritingExercises.OrderBy(e => e.Id).Select(e => new { e.Id, e.Title, e.CreatedAt }).ToListAsync();
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        Assert.Equal(before, await WritingRecentExercisesTests.RewardSnapshot(db));
        Assert.Equal(lessons, await db.WritingExercises.OrderBy(e => e.Id).Select(e => new { e.Id, e.Title, e.CreatedAt }).ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(await db.WritingExercises.AnyAsync(e => e.GenerationMethod != null));
        var indexes = await db.Database.SqlQueryRaw<string>("SELECT DISTINCT INDEX_NAME AS Value FROM information_schema.statistics WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='WritingGenerationOperations'").ToListAsync();
        Assert.Contains("IX_WritingGenerationOperations_UserId_IdempotencyKey", indexes);
        Assert.Contains("IX_WritingGenerationOperations_UserId_Reserved_LeaseExpiresAtUtc", indexes);
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='WritingGenerationOperations'").SingleAsync());

        var calls = 0; var mode = "template";
        await using var f = new MariaDbAcceptanceFactory(connection, run, s =>
        {
            s.RemoveAll<IAiProviderClient>(); s.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient(async (_, _, _) =>
            {
                var n = Interlocked.Increment(ref calls);
                if (mode == "template") throw new AiProviderException("synthetic", "network", true).WithRecordedUsage(5, 5, 10, 1000);
                if (mode == "mixed" && n > 2) throw new AiProviderException("synthetic", "circuit_open", false);
                await Task.Delay(50); return WritingBriefLifecycleTests.Reply();
            }));
        });
        await WritingBriefGenerationTests.Premium(f);
        await WritingBriefLifecycleTests.AssertTemplateLifecycle(f, () => calls);
        await AssertExerciseDeletion(f);
        await f.InitializeAsync(); await SetPremium(f); calls = 0; mode = "ai";
        await WritingBriefDurabilityTests.AssertQuotaRace(f); Assert.Equal(5, calls);
        await f.InitializeAsync(); await SetPremium(f); calls = 0; mode = "mixed";
        await WritingBriefDurabilityTests.AssertSharedQuota(f); Assert.Equal(5, calls);
        mode = "ai";
        await WritingBriefDurabilityTests.AssertCleanup(f, false);
        await WritingBriefDurabilityTests.AssertCleanup(f, true);
        foreach (var afterCommit in new[] { false, true })
        {
            var clock = new GoalsStage03RegressionTests.TestClock(DateTime.UtcNow);
            await using var interrupted = new MariaDbAcceptanceFactory(connection, run, s =>
            {
                GoalsStage03RegressionTests.Configure(s, clock); s.RemoveAll<IAiProviderClient>();
                s.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _, _) => Task.FromResult(WritingBriefLifecycleTests.Reply())));
            });
            var pre = new WritingBriefCommitTests.BeforePublication(); var post = new WritingBriefCommitTests.AfterPublication();
            interrupted.SaveInterceptors.Add(afterCommit ? post : pre);
            await WritingBriefCommitTests.AssertRecovery(interrupted, clock, afterCommit, pre, post);
        }
        await AssertCancellationAndCleanupRace(connection, run);
        // Separate OS processes, then a third process after both have exited. No AI process is used.
        await f.InitializeAsync(); await SetPremium(f);
        await AssertProcessReplay(connection, run, f);
    }

    private static async Task AssertCancellationAndCleanupRace(string connection, string run)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var f = new MariaDbAcceptanceFactory(connection, run, s =>
        {
            s.RemoveAll<IAiProviderClient>(); s.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient(async (_, _, _) =>
            { entered.TrySetResult(); await release.Task; return WritingBriefLifecycleTests.Reply(); }));
        });
        await WritingBriefGenerationTests.Premium(f);
        var running = WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var op = await db.WritingGenerationOperations.AsNoTracking().SingleAsync();
        var cancelled = await scope.ServiceProvider.GetRequiredService<IWritingService>().CancelWritingGenerationAsync(TestDataIds.UserId, op.Id);
        Assert.Equal("cancelled", cancelled.Status); release.SetResult();
        Assert.Equal("cancelled", (await running).Status);
        Assert.False(await db.WritingExercises.AnyAsync(e => e.UserId == TestDataIds.UserId));
        Assert.False(await db.WritingGenerationLogs.AnyAsync(l => l.IsSuccess));
        // Delete the account while another scope waits for its already dispatched provider.
        entered = new(TaskCreationOptions.RunContinuationsAsynchronously); release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var user = new User { Email = "delete-race@example.invalid", Role = Roles.Premium,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("BriefRace1!") };
        db.Users.Add(user); await db.SaveChangesAsync();
        async Task<WritingCreateFromAiResultViewModel> CreateForDeletedUser()
        {
            using var request = f.Services.CreateScope();
            return await request.ServiceProvider.GetRequiredService<IWritingService>()
                .CreateFromAiAsync(WritingBriefLifecycleTests.Request("delete-race"), user.UserId);
        }
        var second = CreateForDeletedUser();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var deleted = await scope.ServiceProvider.GetRequiredService<TCTEnglish.Services.Accounts.IAccountDeletionService>()
            .DeleteAsync(user.UserId, "CONFIRM", "BriefRace1!", null);
        Assert.True(deleted.Succeeded, deleted.Error); release.SetResult();
        Assert.Equal(WritingCreateFromAiOutcome.NotFound, (await second).Outcome);
        Assert.False(await db.WritingGenerationOperations.AnyAsync(o => o.UserId == user.UserId));
        Assert.False(await db.WritingExercises.AnyAsync(e => e.UserId == user.UserId));
    }

    internal static async Task SetPremium(TestWebApplicationFactory f)
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        (await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId)).Role = Roles.Premium; await db.SaveChangesAsync();
    }

    private static async Task AssertExerciseDeletion(TestWebApplicationFactory f)
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        var offered = await service.CreateFromAiAsync(WritingBriefLifecycleTests.Request("delete-unused-result"), TestDataIds.UserId);
        var created = await service.ConfirmWritingTemplateAsync(TestDataIds.UserId, offered.OperationId!.Value, WritingBriefLifecycleTests.Choice());
        Assert.Equal(WritingCreateFromAiOutcome.Success, created.Outcome);
        var op = await db.WritingGenerationOperations.SingleAsync(o => o.Id == offered.OperationId); var id = op.Id;
        db.WritingExercises.Remove(await db.WritingExercises.SingleAsync(e => e.Id == op.ExerciseId)); await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); Assert.Null((await db.WritingGenerationOperations.SingleAsync(o => o.Id == id)).ExerciseId);
        Assert.Equal("result_deleted", (await scope.ServiceProvider.GetRequiredService<IWritingService>().GetWritingGenerationAsync(TestDataIds.UserId, id))!.Status);
    }

    private static async Task AssertProcessReplay(string connection, string run, TestWebApplicationFactory f)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".tmp", "writing-brief", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        async Task Run(string name)
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("vstest"); start.ArgumentList.Add(typeof(WritingBriefMariaDbTests).Assembly.Location);
            start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName~WritingBriefProcessWorker.RunOwnedOperation");
            start.Environment["TCT_ACCEPTANCE_MARIADB"] = connection; start.Environment["TCT_ACCEPTANCE_RUN_ID"] = run;
            start.Environment["TCT_WRITING_BRIEF_WORKER"] = "1"; start.Environment["TCT_WRITING_BRIEF_WORKER_ROOT"] = root;
            start.Environment["TCT_WRITING_BRIEF_WORKER_NAME"] = name;
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
            File.WriteAllText(Path.Combine(root, name + ".log"), await stdout + await stderr);
            Assert.Equal(0, process.ExitCode);
        }
        await Task.WhenAll(Run("first"), Run("second"));
        await Run("restart");
        Assert.Single(File.ReadAllLines(Path.Combine(root, "provider-calls.txt")));
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Single(await db.WritingGenerationOperations.ToListAsync());
        Assert.Equal(1, await db.WritingGenerationLogs.CountAsync(l => l.IsSuccess));
        Assert.Equal(1, await db.WritingExercises.CountAsync(e => e.UserId == TestDataIds.UserId));
    }
}

public sealed class WritingBriefProcessWorker
{
    [WritingBriefWorkerFact]
    public async Task RunOwnedOperation()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId();
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var root = Environment.GetEnvironmentVariable("TCT_WRITING_BRIEF_WORKER_ROOT")!;
        await using var f = new MariaDbAcceptanceFactory(connection, run, s =>
        {
            s.RemoveAll<IAiProviderClient>(); s.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient(async (_, _, _) =>
            { await File.AppendAllTextAsync(Path.Combine(root, "provider-calls.txt"), "synthetic provider dispatched\n");
                await Task.Delay(500); return WritingBriefLifecycleTests.Reply(); }));
        });
        // No fixture initialization/reset in a worker; the parent owns that stage.
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(db.Database.GetDbConnection(), target.Database, run);
        var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        var result = await service.CreateFromAiAsync(WritingBriefLifecycleTests.Request("process-replay"), TestDataIds.UserId);
        Assert.Contains(result.Outcome, new[] { WritingCreateFromAiOutcome.Success, WritingCreateFromAiOutcome.Pending });
        await File.WriteAllTextAsync(Path.Combine(root, Environment.GetEnvironmentVariable("TCT_WRITING_BRIEF_WORKER_NAME") + ".json"),
            JsonSerializer.Serialize(new { result.OperationId, result.ExerciseId, result.Status, result.IsReplay }));
    }
}

public sealed class WritingBriefUpgradeFactAttribute : FactAttribute
{
    public WritingBriefUpgradeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TCT_WRITING_BRIEF_UPGRADE") != "1" || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")))
            Skip = "Requires opted-in fresh disposable MariaDB 10.5.23 database.";
    }
}
public sealed class WritingBriefWorkerFactAttribute : FactAttribute
{
    public WritingBriefWorkerFactAttribute()
    { if (Environment.GetEnvironmentVariable("TCT_WRITING_BRIEF_WORKER") != "1") Skip = "Owned subprocess worker only."; }
}
