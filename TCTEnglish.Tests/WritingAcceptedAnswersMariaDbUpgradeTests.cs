using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TCTEnglish.Services.Writing;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Fresh-only WritingAcceptedAnswers upgrade path. Requires an empty disposable
/// acceptance DB (sentinel only). Set TCT_WRITING_ANSWERS_UPGRADE=1 plus
/// TCT_ACCEPTANCE_MARIADB / TCT_ACCEPTANCE_RUN_ID. Never points at a concurrency
/// fixture that is already at latest.
/// </summary>
[Collection("MariaDbAcceptance")]
public sealed class WritingAcceptedAnswersMariaDbUpgradeTests
{
    public const string BaselineMigrationId = "20261103141227_RemoveRedundantAiQuotaUniqueIndex";
    public const string AcceptedAnswersMigrationId = "20261104110702_AddWritingAcceptedAnswers";

    [WritingAnswersUpgradeFact]
    public async Task UpgradeToAcceptedAnswers_ThenRepeat_PreservesSentinelRowsAndEnforcesUniqueHash()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")
            ?? throw new InvalidOperationException("TCT_ACCEPTANCE_MARIADB missing.");
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId(runId);

        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);
        var dbConnection = db.Database.GetDbConnection();
        await MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(
            dbConnection,
            target.Database,
            run);

        await db.Database.MigrateAsync(BaselineMigrationId);

        var user = new User
        {
            Email = "writing-answers-upgrade@example.invalid",
            PasswordHash = "synthetic-only",
            FullName = "Writing answers upgrade",
            Role = Roles.Admin,
            Status = UserStatus.Offline,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var exercise = new WritingExercise
        {
            Title = "Sentinel Writing Exercise",
            Level = "beginner",
            ContentType = "emails",
            Topic = "Sentinel",
            PreviewText = "Preserve me",
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        };
        exercise.WritingExerciseSentences.Add(new WritingExerciseSentence
        {
            SortOrder = 1,
            VietnameseText = "Xin chao.",
            EnglishMeaning = "Hello.",
            BreakAfter = false
        });
        db.WritingExercises.Add(exercise);
        await db.SaveChangesAsync();

        var userId = user.UserId;
        var exerciseId = exercise.Id;
        var sentenceId = exercise.WritingExerciseSentences.Single().Id;

        var attempt = new UserWritingAttempt
        {
            UserId = userId,
            WritingExerciseId = exerciseId,
            WritingExerciseSentenceId = sentenceId,
            SubmittedAnswer = "Hello.",
            Passed = true,
            UsedAi = false,
            EvaluationSource = "local-exact",
            SummaryTitle = "Câu đạt yêu cầu",
            SummaryText = "Sentinel attempt",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.UserWritingAttempts.Add(attempt);
        db.UserWritingSentenceProgresses.Add(new UserWritingSentenceProgress
        {
            UserId = userId,
            WritingExerciseId = exerciseId,
            SentenceId = sentenceId,
            AttemptCount = 1,
            IsPassed = true,
            AcceptedAnswer = "Hello.",
            LastAttemptAt = DateTime.UtcNow,
            PassedAt = DateTime.UtcNow
        });
        db.UserWritingExerciseProgresses.Add(new UserWritingExerciseProgress
        {
            UserId = userId,
            WritingExerciseId = exerciseId,
            TotalSentenceCount = 1,
            PassedSentenceCount = 1,
            AttemptCount = 1,
            IsCompleted = true,
            LastAttemptAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
        db.LearningCompletionCredits.Add(new LearningCompletionCredit
        {
            UserId = userId,
            Feature = "Writing",
            ContentKey = $"writing-exercise:{exerciseId}",
            CreditKey = $"writing-upgrade-sentinel:{exerciseId}",
            XpAwarded = 10,
            BusinessDate = DateTime.UtcNow.Date,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var attemptId = attempt.Id;

        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();

        db.ChangeTracker.Clear();
        Assert.True(await db.Users.AnyAsync(item => item.UserId == userId));
        Assert.True(await db.WritingExercises.AnyAsync(item => item.Id == exerciseId));
        Assert.True(await db.WritingExerciseSentences.AnyAsync(item => item.Id == sentenceId));
        Assert.Contains(
            AcceptedAnswersMigrationId,
            await db.Database.GetAppliedMigrationsAsync());

        var preservedAttempt = await db.UserWritingAttempts.AsNoTracking()
            .SingleAsync(item => item.Id == attemptId);
        Assert.Equal("Hello.", preservedAttempt.SubmittedAnswer);
        Assert.True(preservedAttempt.Passed);
        Assert.Equal("local-exact", preservedAttempt.EvaluationSource);

        var preservedSentenceProgress = await db.UserWritingSentenceProgresses.AsNoTracking()
            .SingleAsync(item => item.UserId == userId && item.SentenceId == sentenceId);
        Assert.True(preservedSentenceProgress.IsPassed);
        Assert.Equal("Hello.", preservedSentenceProgress.AcceptedAnswer);

        var preservedExerciseProgress = await db.UserWritingExerciseProgresses.AsNoTracking()
            .SingleAsync(item => item.UserId == userId && item.WritingExerciseId == exerciseId);
        Assert.True(preservedExerciseProgress.IsCompleted);
        Assert.Equal(1, preservedExerciseProgress.PassedSentenceCount);

        var preservedCredit = await db.LearningCompletionCredits.AsNoTracking()
            .SingleAsync(item => item.UserId == userId && item.CreditKey == $"writing-upgrade-sentinel:{exerciseId}");
        Assert.Equal(10, preservedCredit.XpAwarded);

        db.WritingAcceptedAnswers.Add(new WritingAcceptedAnswer
        {
            WritingExerciseSentenceId = sentenceId,
            AnswerText = "Hi there.",
            NormalizedHash = WritingAssessmentPolicy.ComputeNormalizedHash("Hi there."),
            ApprovedByUserId = userId,
            ApprovedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.WritingAcceptedAnswers.CountAsync(item => item.WritingExerciseSentenceId == sentenceId));
        Assert.Equal("Hi there.", (await db.WritingAcceptedAnswers.AsNoTracking()
            .SingleAsync(item => item.WritingExerciseSentenceId == sentenceId)).AnswerText);
        Assert.Equal(attemptId, (await db.UserWritingAttempts.AsNoTracking()
            .SingleAsync(item => item.Id == attemptId)).Id);
        Assert.Equal(10, (await db.LearningCompletionCredits.AsNoTracking()
            .SingleAsync(item => item.UserId == userId && item.CreditKey == $"writing-upgrade-sentinel:{exerciseId}"))
            .XpAwarded);

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            db.WritingAcceptedAnswers.Add(new WritingAcceptedAnswer
            {
                WritingExerciseSentenceId = sentenceId,
                AnswerText = "hi there!",
                NormalizedHash = WritingAssessmentPolicy.ComputeNormalizedHash("hi there!"),
                ApprovedByUserId = userId,
                ApprovedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        await db.Database.OpenConnectionAsync();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT COUNT(DISTINCT INDEX_NAME) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='WritingAcceptedAnswers' AND INDEX_NAME='UX_WritingAcceptedAnswers_Sentence_Hash'";
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
        command.CommandText =
            "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='WritingAcceptedAnswers' AND INDEX_NAME='UX_WritingAcceptedAnswers_Sentence_Hash'";
        Assert.Equal(2, Convert.ToInt32(await command.ExecuteScalarAsync()));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}

/// <summary>
/// Proves an already-upgraded acceptance DB is rejected before any baseline DDL/DML.
/// Uses the shared concurrency/fixture connection (latest schema) — never downgrades it.
/// </summary>
[Collection("MariaDbAcceptance")]
public sealed class WritingAcceptedAnswersMariaDbUpgradeGuardTests
{
    [MariaDbAcceptanceFact]
    public async Task AlreadyUpgradedTarget_IsRejectedBeforeBaselineMigrate_AndKeepsRows()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")
            ?? throw new InvalidOperationException("TCT_ACCEPTANCE_MARIADB missing.");
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId(runId);

        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);
        await MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(
            db.Database.GetDbConnection(),
            target.Database,
            run);

        await db.Database.MigrateAsync();

        var user = new User
        {
            Email = $"upgrade-guard-{Guid.NewGuid():N}@example.invalid",
            PasswordHash = "synthetic-only",
            FullName = "Upgrade guard admin",
            Role = Roles.Admin,
            Status = UserStatus.Offline,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var userId = user.UserId;

        var exercise = new WritingExercise
        {
            Title = "Already Upgraded Guard Fixture",
            Level = "beginner",
            ContentType = "emails",
            Topic = "Guard",
            PreviewText = "Must survive refused baseline",
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        };
        exercise.WritingExerciseSentences.Add(new WritingExerciseSentence
        {
            SortOrder = 1,
            VietnameseText = "Xin chao.",
            EnglishMeaning = "Hello.",
            BreakAfter = false
        });
        db.WritingExercises.Add(exercise);
        await db.SaveChangesAsync();
        var sentenceId = exercise.WritingExerciseSentences.Single().Id;
        var exerciseId = exercise.Id;

        db.UserWritingAttempts.Add(new UserWritingAttempt
        {
            UserId = userId,
            WritingExerciseId = exerciseId,
            WritingExerciseSentenceId = sentenceId,
            SubmittedAnswer = "Hello.",
            Passed = true,
            UsedAi = false,
            EvaluationSource = "local-exact",
            SummaryTitle = "ok",
            SummaryText = "guard",
            CreatedAtUtc = DateTime.UtcNow
        });
        db.WritingAcceptedAnswers.Add(new WritingAcceptedAnswer
        {
            WritingExerciseSentenceId = sentenceId,
            AnswerText = "Hi there.",
            NormalizedHash = WritingAssessmentPolicy.ComputeNormalizedHash("Hi there."),
            ApprovedByUserId = userId,
            ApprovedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var beforeVariants = await db.WritingAcceptedAnswers.AsNoTracking()
            .CountAsync(item => item.WritingExerciseSentenceId == sentenceId);
        var beforeAttempts = await db.UserWritingAttempts.AsNoTracking()
            .CountAsync(item => item.WritingExerciseSentenceId == sentenceId);
        Assert.Equal(1, beforeVariants);
        Assert.Equal(1, beforeAttempts);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(
                db.Database.GetDbConnection(),
                target.Database,
                run));
        Assert.Contains("refused", refused.Message, StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();
        Assert.Equal(
            1,
            await db.WritingAcceptedAnswers.AsNoTracking()
                .CountAsync(item => item.WritingExerciseSentenceId == sentenceId));
        Assert.Equal(
            "Hi there.",
            (await db.WritingAcceptedAnswers.AsNoTracking()
                .SingleAsync(item => item.WritingExerciseSentenceId == sentenceId)).AnswerText);
        Assert.Equal(
            1,
            await db.UserWritingAttempts.AsNoTracking()
                .CountAsync(item => item.WritingExerciseSentenceId == sentenceId));
        Assert.Contains(
            WritingAcceptedAnswersMariaDbUpgradeTests.AcceptedAnswersMigrationId,
            await db.Database.GetAppliedMigrationsAsync());
    }
}

public sealed class WritingAnswersUpgradeFactAttribute : FactAttribute
{
    public WritingAnswersUpgradeFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("TCT_WRITING_ANSWERS_UPGRADE"),
                "1",
                StringComparison.Ordinal))
        {
            Skip =
                "Requires a virgin disposable acceptance DB. Set TCT_WRITING_ANSWERS_UPGRADE=1 " +
                "and point TCT_ACCEPTANCE_MARIADB at a sentinel-only database (never an upgraded concurrency fixture).";
            return;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")))
        {
            Skip = "Not run: set TCT_ACCEPTANCE_MARIADB to a fresh isolated MariaDB acceptance connection string.";
        }
    }
}

/// <summary>
/// C1.1: history-empty leftover app tables (Sets/Cards/SpeakingVideos) must be
/// refused before baseline Migrate. Uses a virgin acceptance DB that the test
/// intentionally dirties with allowlist-violating tables — never an upgraded fixture.
/// </summary>
[Collection("MariaDbAcceptance")]
public sealed class WritingAcceptedAnswersMariaDbSchemaLeftoverGuardTests
{
    [WritingAnswersUpgradeFact]
    public async Task LeftoverSetsCardsSpeakingVideos_WithEmptyHistory_AreRejected_AndRowsKept()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")
            ?? throw new InvalidOperationException("TCT_ACCEPTANCE_MARIADB missing.");
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId(runId);

        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);
        var dbConnection = db.Database.GetDbConnection();
        await MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(
            dbConnection,
            target.Database,
            run);

        await db.Database.OpenConnectionAsync();
        foreach (var sql in new[]
                 {
                     """
                     CREATE TABLE `Sets` (
                       `Id` INT NOT NULL PRIMARY KEY,
                       `Marker` VARCHAR(64) NOT NULL
                     );
                     """,
                     """
                     CREATE TABLE `Cards` (
                       `Id` INT NOT NULL PRIMARY KEY,
                       `Marker` VARCHAR(64) NOT NULL
                     );
                     """,
                     """
                     CREATE TABLE `SpeakingVideos` (
                       `Id` INT NOT NULL PRIMARY KEY,
                       `Marker` VARCHAR(64) NOT NULL
                     );
                     """,
                     "INSERT INTO `Sets` (`Id`, `Marker`) VALUES (1, 'sets-leftover');",
                     "INSERT INTO `Cards` (`Id`, `Marker`) VALUES (1, 'cards-leftover');",
                     "INSERT INTO `SpeakingVideos` (`Id`, `Marker`) VALUES (1, 'speaking-leftover');"
                 })
        {
            await using var create = dbConnection.CreateCommand();
            create.CommandText = sql;
            await create.ExecuteNonQueryAsync();
        }

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(
                dbConnection,
                target.Database,
                run));
        Assert.Contains("Sets", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cards", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SpeakingVideos", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowlist", refused.Message, StringComparison.OrdinalIgnoreCase);

        await using (var verify = dbConnection.CreateCommand())
        {
            verify.CommandText = "SELECT COUNT(*) FROM `Sets` WHERE `Marker`='sets-leftover';";
            Assert.Equal(1, Convert.ToInt32(await verify.ExecuteScalarAsync()));
            verify.CommandText = "SELECT COUNT(*) FROM `Cards` WHERE `Marker`='cards-leftover';";
            Assert.Equal(1, Convert.ToInt32(await verify.ExecuteScalarAsync()));
            verify.CommandText = "SELECT COUNT(*) FROM `SpeakingVideos` WHERE `Marker`='speaking-leftover';";
            Assert.Equal(1, Convert.ToInt32(await verify.ExecuteScalarAsync()));
            verify.CommandText =
                """
                SELECT COUNT(*)
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = '__EFMigrationsHistory';
                """;
            Assert.Equal(0, Convert.ToInt32(await verify.ExecuteScalarAsync()));
        }
    }
}
