using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTVocabulary.Models;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Isolated MariaDB-backed factory for G03 acceptance. Requires a loopback
/// acceptance database whose sentinel was bootstrapped out-of-band for this run.
/// </summary>
public sealed class MariaDbAcceptanceFactory : TestWebApplicationFactory
{
    private readonly string _connectionString;
    private readonly MariaDbAcceptanceConnectionGuard.ParsedTarget _target;
    private readonly string _runId;

    public MariaDbAcceptanceFactory(
        string connectionString,
        string? runId = null,
        Action<IServiceCollection>? configureTestServices = null)
        : base(configureTestServices)
    {
        _target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connectionString);
        _runId = MariaDbAcceptanceConnectionGuard.RequireRunId(runId);
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

        await MariaDbAcceptanceConnectionGuard.PrepareFixtureAsync(
            connection,
            _target.Database,
            _runId,
            async cancellationToken =>
            {
                var foreignKeysDisabled = false;
                try
                {
                    await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=0;", cancellationToken);
                    foreignKeysDisabled = true;
                    foreach (var table in AcceptanceTables)
                    {
                        await context.Database.ExecuteSqlRawAsync($"DELETE FROM `{table}`;", cancellationToken);
                    }
                }
                finally
                {
                    if (foreignKeysDisabled)
                    {
                        await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=1;", cancellationToken);
                    }
                }

                await SeedAsync(context);
                await SeedAcceptanceGoalsAsync(context);
            });
    }

    /// <summary>
    /// MariaDB acceptance only: active speaking/writing goals so completion credits
    /// produce observable outbox intents without changing SQLite suites that assert
    /// create-mode Goals UI when no goals exist.
    /// </summary>
    private static async Task SeedAcceptanceGoalsAsync(DbflashcardContext context)
    {
        var now = DateTime.UtcNow;
        context.UserGoals.AddRange(
            new UserGoal
            {
                UserId = TestDataIds.UserId,
                GoalArea = GoalArea.Speaking,
                TargetValue = 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new UserGoal
            {
                UserId = TestDataIds.UserId,
                GoalArea = GoalArea.Writing,
                TargetValue = 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        await context.SaveChangesAsync();
    }

    private static readonly string[] AcceptanceTables =
    [
        "LearningCompletionCredits",
        "LearningNotificationOutbox",
        "Notifications",
        "NotificationJobRuns",
        "UserSpeakingProgresses",
        "UserWritingAttempts",
        "UserWritingExerciseProgresses",
        "UserWritingSentenceProgresses",
        "UserSpeakingVideoCompletions",
        "UserDailyActivities",
        "UserGoals",
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
