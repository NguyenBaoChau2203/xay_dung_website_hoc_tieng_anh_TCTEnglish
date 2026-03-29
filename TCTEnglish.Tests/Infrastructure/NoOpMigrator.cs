using Microsoft.EntityFrameworkCore.Migrations;

namespace TCTEnglish.Tests.Infrastructure;

public sealed class NoOpMigrator : IMigrator
{
    public void Migrate(string? targetMigration = null)
    {
    }

    public Task MigrateAsync(string? targetMigration = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public string GenerateScript(
        string? fromMigration = null,
        string? toMigration = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        return string.Empty;
    }

    public bool HasPendingModelChanges()
    {
        return false;
    }

    public Task<bool> HasPendingModelChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
