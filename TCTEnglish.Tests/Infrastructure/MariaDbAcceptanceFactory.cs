using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTVocabulary.Models;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Isolated MariaDB-backed factory for G03 acceptance. Requires a loopback
/// acceptance database with sentinel identity; never targets dev/hosting DBs.
/// </summary>
public sealed class MariaDbAcceptanceFactory : TestWebApplicationFactory
{
    private readonly string _connectionString;
    private readonly MariaDbAcceptanceConnectionGuard.ParsedTarget _target;

    public MariaDbAcceptanceFactory(string connectionString)
        : base(null)
    {
        _target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connectionString);
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DbflashcardContext>>();
            services.RemoveAll<DbflashcardContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<DbflashcardContext>>();

            var serverVersion = ServerVersion.Parse("10.5.23-mariadb");
            services.AddDbContext<DbflashcardContext>(options =>
            {
                options.UseMySql(_connectionString, serverVersion);
            });
        });
    }

    public override async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await MariaDbAcceptanceConnectionGuard.EnsureSentinelAsync(connection);
        await MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(connection, _target.Database);

        var foreignKeysDisabled = false;
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=0;");
            foreignKeysDisabled = true;

            // Truncate only known acceptance tables; any failure fails the run.
            foreach (var table in AcceptanceTables)
            {
                await context.Database.ExecuteSqlRawAsync($"DELETE FROM `{table}`;");
            }
        }
        finally
        {
            if (foreignKeysDisabled)
            {
                await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=1;");
            }
        }

        await MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(connection, _target.Database);
        await SeedAsync(context);
    }

    private static readonly string[] AcceptanceTables =
    [
        "LearningCompletionCredits",
        "LearningNotificationOutbox",
        "UserSpeakingProgresses",
        "UserWritingAttempts",
        "UserWritingExerciseProgresses",
        "UserWritingSentenceProgresses",
        "UserSpeakingVideoCompletions",
        "SpeakingSentences",
        "SpeakingVideos",
        "SpeakingPlaylists",
        "WritingExerciseSentences",
        "WritingExercises",
        "ClassMessages",
        "ClassFolders",
        "ClassMembers",
        "Classes",
        "Cards",
        "Sets",
        "Folders",
        "Users"
    ];
}
