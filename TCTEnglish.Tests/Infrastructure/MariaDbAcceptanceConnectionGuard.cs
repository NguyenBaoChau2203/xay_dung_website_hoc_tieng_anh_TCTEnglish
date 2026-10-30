using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Offline + online identity checks for MariaDB acceptance fixtures.
/// Bootstrap is separate from fixture verify/reset and never overwrites an
/// existing sentinel purpose/run.
/// </summary>
public static class MariaDbAcceptanceConnectionGuard
{
    public const string SentinelTable = "__tct_acceptance_sentinel";
    public const string ExpectedPurpose = "final-acceptance-g03";
    public const string DatabaseNamePattern = @"^tct_acceptance_[a-z0-9_]{4,64}$";
    public const string RunIdEnvironmentVariable = "TCT_ACCEPTANCE_RUN_ID";

    private static readonly HashSet<string> ForbiddenDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "tctenglish_dev",
        "nhtctsnn_tctenglish",
        "mysql",
        "information_schema",
        "performance_schema",
        "sys"
    };

    public sealed record ParsedTarget(
        string Host,
        int Port,
        string Database,
        string User);

    public static ParsedTarget ParseAndValidateOffline(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Acceptance connection string is required.");
        }

        if (connectionString.Contains(';') && connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Count(part => part.StartsWith("Database=", StringComparison.OrdinalIgnoreCase)
                    || part.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase)) > 1)
        {
            throw new InvalidOperationException("Multiple database names are not allowed.");
        }

        MySqlConnectionStringBuilder builder;
        try
        {
            builder = new MySqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Acceptance connection string could not be parsed.", ex);
        }

        var host = (builder.Server ?? string.Empty).Trim();
        var database = (builder.Database ?? string.Empty).Trim();
        var user = (builder.UserID ?? string.Empty).Trim();
        var port = builder.Port == 0 ? 3306 : (int)builder.Port;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(user))
        {
            throw new InvalidOperationException("Acceptance connection requires host, database and user.");
        }

        if (!IsLoopbackHost(host))
        {
            throw new InvalidOperationException("Acceptance MariaDB host must be loopback.");
        }

        if (ForbiddenDatabases.Contains(database)
            || (database.Contains("tctenglish", StringComparison.OrdinalIgnoreCase)
                && !Regex.IsMatch(database, DatabaseNamePattern)))
        {
            throw new InvalidOperationException("Database is not an acceptance target.");
        }

        if (!Regex.IsMatch(database, DatabaseNamePattern))
        {
            throw new InvalidOperationException("Database name must match tct_acceptance_<runid>.");
        }

        if (port is < 3308 or > 3399)
        {
            throw new InvalidOperationException("Acceptance MariaDB port must be in 3308-3399.");
        }

        return new ParsedTarget(host, port, database, user);
    }

    public static string RequireRunId(string? runId = null)
    {
        var value = runId
            ?? Environment.GetEnvironmentVariable(RunIdEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, @"^[a-z0-9_]{4,64}$"))
        {
            throw new InvalidOperationException(
                $"Acceptance run id is required via {RunIdEnvironmentVariable} (a-z0-9_, 4-64).");
        }

        return value;
    }

    /// <summary>
    /// Read-only identity check. Never creates or updates the sentinel.
    /// </summary>
    public static async Task VerifyOnlineIdentityAsync(
        DbConnection connection,
        string expectedDatabase,
        string expectedRunId,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var dbCommand = connection.CreateCommand())
        {
            dbCommand.CommandText = "SELECT DATABASE();";
            var current = Convert.ToString(await dbCommand.ExecuteScalarAsync(cancellationToken));
            if (!string.Equals(current, expectedDatabase, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Connected database does not match acceptance target.");
            }
        }

        string? purpose;
        string? runId;
        try
        {
            await using var purposeCommand = connection.CreateCommand();
            purposeCommand.CommandText =
                $"SELECT purpose FROM `{SentinelTable}` WHERE id = 1 LIMIT 1;";
            purpose = Convert.ToString(await purposeCommand.ExecuteScalarAsync(cancellationToken));

            await using var runCommand = connection.CreateCommand();
            runCommand.CommandText =
                $"SELECT run_id FROM `{SentinelTable}` WHERE id = 1 LIMIT 1;";
            runId = Convert.ToString(await runCommand.ExecuteScalarAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Acceptance sentinel is missing; bootstrap the ephemeral DB before DML.",
                ex);
        }

        if (!string.Equals(purpose, ExpectedPurpose, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Acceptance sentinel purpose mismatch.");
        }

        if (!string.Equals(runId, expectedRunId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Acceptance sentinel run id mismatch.");
        }
    }

    /// <summary>
    /// Owned bootstrap for a brand-new acceptance DB. Never overwrites an existing
    /// purpose/run. Call only after offline/container ownership checks.
    /// </summary>
    public static async Task BootstrapSentinelAsync(
        DbConnection connection,
        string expectedDatabase,
        string runId,
        CancellationToken cancellationToken = default)
    {
        runId = RequireRunId(runId);
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var dbCommand = connection.CreateCommand())
        {
            dbCommand.CommandText = "SELECT DATABASE();";
            var current = Convert.ToString(await dbCommand.ExecuteScalarAsync(cancellationToken));
            if (!string.Equals(current, expectedDatabase, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Bootstrap refused: connected database mismatch.");
            }
        }

        await using (var create = connection.CreateCommand())
        {
            create.CommandText =
                $"""
                 CREATE TABLE IF NOT EXISTS `{SentinelTable}` (
                   id INT NOT NULL PRIMARY KEY,
                   purpose VARCHAR(64) NOT NULL,
                   run_id VARCHAR(64) NOT NULL,
                   created_at_utc DATETIME(6) NOT NULL
                 );
                 """;
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        object? existingPurpose = null;
        object? existingRunId = null;
        try
        {
            await using var purposeCommand = connection.CreateCommand();
            purposeCommand.CommandText =
                $"SELECT purpose FROM `{SentinelTable}` WHERE id = 1 LIMIT 1;";
            existingPurpose = await purposeCommand.ExecuteScalarAsync(cancellationToken);

            await using var runCommand = connection.CreateCommand();
            runCommand.CommandText =
                $"SELECT run_id FROM `{SentinelTable}` WHERE id = 1 LIMIT 1;";
            existingRunId = await runCommand.ExecuteScalarAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Table empty / no row yet.
        }

        if (existingPurpose is not null || existingRunId is not null)
        {
            if (!string.Equals(Convert.ToString(existingPurpose), ExpectedPurpose, StringComparison.Ordinal)
                || !string.Equals(Convert.ToString(existingRunId), runId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Acceptance sentinel already exists with a different identity; refusing to overwrite.");
            }

            return;
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                $"""
                 INSERT INTO `{SentinelTable}` (id, purpose, run_id, created_at_utc)
                 VALUES (1, @purpose, @runId, UTC_TIMESTAMP(6));
                 """;
            var purposeParameter = insert.CreateParameter();
            purposeParameter.ParameterName = "@purpose";
            purposeParameter.Value = ExpectedPurpose;
            insert.Parameters.Add(purposeParameter);
            var runParameter = insert.CreateParameter();
            runParameter.ParameterName = "@runId";
            runParameter.Value = runId;
            insert.Parameters.Add(runParameter);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Fixture entry: verify identity first, then run owned reset/seed. No bootstrap.
    /// </summary>
    public static async Task PrepareFixtureAsync(
        DbConnection connection,
        string expectedDatabase,
        string expectedRunId,
        Func<CancellationToken, Task> resetAndSeedAsync,
        CancellationToken cancellationToken = default)
    {
        await VerifyOnlineIdentityAsync(connection, expectedDatabase, expectedRunId, cancellationToken);
        await resetAndSeedAsync(cancellationToken);
        await VerifyOnlineIdentityAsync(connection, expectedDatabase, expectedRunId, cancellationToken);
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(host, out var address))
        {
            return IPAddress.IsLoopback(address);
        }

        try
        {
            var addresses = Dns.GetHostAddresses(host);
            return addresses.Length > 0 && addresses.All(IPAddress.IsLoopback);
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
