using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Native MariaDB check for the pinned MySqlConnector. Skips unless the
/// isolated acceptance fixture is selected by the existing guard.
/// </summary>
[Collection("MariaDbAcceptance")]
[Trait("Category", "MariaDbAcceptance")]
public sealed class MariaDbConnectorPinAcceptanceTests
{
    [MariaDbAcceptanceFact]
    public async Task UnicodeName_CommitsAndRolledBackUpdateDoesNotPersist()
    {
        Assert.Contains(
            "2.6.2",
            typeof(MySqlConnection).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        var connectionString = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        const string committed = "Nguyễn Văn Á — 日本語 — café";

        await using var factory = new MariaDbAcceptanceFactory(connectionString, runId);
        await factory.InitializeAsync();

        string serverVersion;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using (var versionCommand = connection.CreateCommand())
            {
                versionCommand.CommandText = "SELECT VERSION();";
                serverVersion = Convert.ToString(await versionCommand.ExecuteScalarAsync()) ?? string.Empty;
            }

            var user = await db.Users.SingleAsync(row => row.UserId == TestDataIds.UserId);
            user.FullName = committed;
            await db.SaveChangesAsync();

            await using var transaction = await db.Database.BeginTransactionAsync();
            user.FullName = "rolled-back";
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var readScope = factory.Services.CreateAsyncScope();
        var saved = await readScope.ServiceProvider.GetRequiredService<DbflashcardContext>()
            .Users.AsNoTracking()
            .SingleAsync(row => row.UserId == TestDataIds.UserId);

        Assert.Contains("10.5.23", serverVersion, StringComparison.Ordinal);
        Assert.Equal(committed, saved.FullName);
    }
}
