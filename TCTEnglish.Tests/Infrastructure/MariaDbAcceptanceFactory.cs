using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTVocabulary.Models;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Isolated MariaDB-backed factory for G03 acceptance. Does not reuse the local
/// Docker volume or hosting database. Requires TCT_ACCEPTANCE_MARIADB.
/// </summary>
public sealed class MariaDbAcceptanceFactory : TestWebApplicationFactory
{
    private readonly string _connectionString;

    public MariaDbAcceptanceFactory(string connectionString)
        : base(null)
    {
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
        await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=0;");
        foreach (var table in new[]
                 {
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
                 })
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync($"DELETE FROM `{table}`;");
            }
            catch (Exception)
            {
                // Table may not exist on a fresh partial schema; migrations should have created it.
            }
        }

        await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=1;");
        await SeedAsync(context);
    }
}
