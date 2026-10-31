using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class MariaDbAcceptanceOrchestrationTests
{
    private const string Database = "tct_acceptance_s01";
    private const string RunId = "s01_run";

    [Fact]
    public async Task Verify_MissingSentinel_RejectsWithoutWrites()
    {
        await using var connection = new RecordingDbConnection(Database);
        await connection.OpenAsync();
        connection.SentinelTableExists = false;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(connection, Database, RunId));

        Assert.Contains("missing", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connection.WriteCommandCount);
    }

    [Fact]
    public async Task Verify_WrongPurpose_RejectsWithoutWrites()
    {
        await using var connection = new RecordingDbConnection(Database);
        await connection.OpenAsync();
        connection.SentinelTableExists = true;
        connection.SentinelPurpose = "unrelated-suite-must-not-reset";
        connection.SentinelRunId = RunId;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(connection, Database, RunId));

        Assert.Contains("purpose", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connection.WriteCommandCount);
        Assert.Equal("unrelated-suite-must-not-reset", connection.SentinelPurpose);
    }

    [Fact]
    public async Task Verify_WrongRunId_RejectsWithoutWrites()
    {
        await using var connection = new RecordingDbConnection(Database);
        await connection.OpenAsync();
        connection.SentinelTableExists = true;
        connection.SentinelPurpose = MariaDbAcceptanceConnectionGuard.ExpectedPurpose;
        connection.SentinelRunId = "other_run";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(connection, Database, RunId));

        Assert.Contains("run id", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connection.WriteCommandCount);
    }

    [Fact]
    public async Task PrepareFixture_MissingSentinel_DoesNotResetOrSeed()
    {
        await using var connection = new RecordingDbConnection(Database);
        await connection.OpenAsync();
        connection.SentinelTableExists = false;
        var resetCalled = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.PrepareFixtureAsync(
                connection,
                Database,
                RunId,
                _ =>
                {
                    resetCalled = true;
                    return Task.CompletedTask;
                }));

        Assert.False(resetCalled);
        Assert.Equal(0, connection.WriteCommandCount);
    }

    [Fact]
    public async Task Bootstrap_DoesNotOverwriteExistingForeignPurpose()
    {
        await using var connection = new RecordingDbConnection(Database);
        await connection.OpenAsync();
        connection.SentinelTableExists = true;
        connection.SentinelPurpose = "unrelated-suite-must-not-reset";
        connection.SentinelRunId = "foreign";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.BootstrapSentinelAsync(connection, Database, RunId));

        Assert.Equal("unrelated-suite-must-not-reset", connection.SentinelPurpose);
        Assert.Equal("foreign", connection.SentinelRunId);
        Assert.DoesNotContain(
            connection.Commands,
            sql => sql.Contains("ON DUPLICATE KEY UPDATE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Bootstrap_ThenPrepareFixture_AllowsOwnedReset()
    {
        await using var connection = new RecordingDbConnection(Database);
        await connection.OpenAsync();
        connection.SentinelTableExists = false;

        await MariaDbAcceptanceConnectionGuard.BootstrapSentinelAsync(connection, Database, RunId);
        Assert.Equal(MariaDbAcceptanceConnectionGuard.ExpectedPurpose, connection.SentinelPurpose);
        Assert.Equal(RunId, connection.SentinelRunId);

        var resetCalled = false;
        await MariaDbAcceptanceConnectionGuard.PrepareFixtureAsync(
            connection,
            Database,
            RunId,
            _ =>
            {
                resetCalled = true;
                return Task.CompletedTask;
            });

        Assert.True(resetCalled);
    }

    [Fact]
    public async Task LegacyEnsureThenVerify_MustNotBeUsedByFixture()
    {
        // Documents the S01 defect shape: stamping before verify must not exist on the fixture path.
        await using var connection = new RecordingDbConnection(Database);
        await connection.OpenAsync();
        connection.SentinelTableExists = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(connection, Database, RunId));

        // Fixture path uses PrepareFixture, which never bootstraps.
        var resetCalled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.PrepareFixtureAsync(
                connection,
                Database,
                RunId,
                _ =>
                {
                    resetCalled = true;
                    return Task.CompletedTask;
                }));
        Assert.False(resetCalled);
        Assert.False(connection.SentinelTableExists);
    }
}
