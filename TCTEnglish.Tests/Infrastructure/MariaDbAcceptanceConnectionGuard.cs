using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using MySqlConnector;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Offline + online identity checks for MariaDB acceptance fixtures.
/// Rejects before Open/DML when the target is not an ephemeral acceptance DB.
/// </summary>
public static class MariaDbAcceptanceConnectionGuard
{
    public const string SentinelTable = "__tct_acceptance_sentinel";
    public const string ExpectedPurpose = "final-acceptance-g03";
    public const string DatabaseNamePattern = @"^tct_acceptance_[a-z0-9_]{4,64}$";

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
            || database.Contains("tctenglish", StringComparison.OrdinalIgnoreCase)
                && !System.Text.RegularExpressions.Regex.IsMatch(database, DatabaseNamePattern))
        {
            throw new InvalidOperationException("Database is not an acceptance target.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(database, DatabaseNamePattern))
        {
            throw new InvalidOperationException("Database name must match tct_acceptance_<runid>.");
        }

        // Dev compose uses 3307; acceptance harness uses a dedicated port contract (3308+).
        if (port is < 3308 or > 3399)
        {
            throw new InvalidOperationException("Acceptance MariaDB port must be in 3308-3399.");
        }

        return new ParsedTarget(host, port, database, user);
    }

    public static async Task VerifyOnlineIdentityAsync(
        DbConnection connection,
        string expectedDatabase,
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

        await using (var sentinel = connection.CreateCommand())
        {
            sentinel.CommandText =
                $"SELECT purpose FROM `{SentinelTable}` WHERE id = 1 LIMIT 1;";
            object? purpose;
            try
            {
                purpose = await sentinel.ExecuteScalarAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Acceptance sentinel is missing; bootstrap the ephemeral DB before DML.",
                    ex);
            }

            if (!string.Equals(Convert.ToString(purpose), ExpectedPurpose, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Acceptance sentinel purpose mismatch.");
            }
        }
    }

    public static async Task EnsureSentinelAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var create = connection.CreateCommand())
        {
            create.CommandText =
                $"""
                 CREATE TABLE IF NOT EXISTS `{SentinelTable}` (
                   id INT NOT NULL PRIMARY KEY,
                   purpose VARCHAR(64) NOT NULL,
                   created_at_utc DATETIME(6) NOT NULL
                 );
                 """;
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var upsert = connection.CreateCommand())
        {
            upsert.CommandText =
                $"""
                 INSERT INTO `{SentinelTable}` (id, purpose, created_at_utc)
                 VALUES (1, @purpose, UTC_TIMESTAMP(6))
                 ON DUPLICATE KEY UPDATE purpose = VALUES(purpose);
                 """;
            var parameter = upsert.CreateParameter();
            parameter.ParameterName = "@purpose";
            parameter.Value = ExpectedPurpose;
            upsert.Parameters.Add(parameter);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }
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
