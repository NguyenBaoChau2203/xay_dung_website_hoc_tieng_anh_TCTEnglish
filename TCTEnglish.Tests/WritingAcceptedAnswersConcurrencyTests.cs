using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.Writing;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingAcceptedAnswersStaleContentTests
{
    [Fact]
    public async Task SaveAcceptedAnswers_AfterBulkEditChangesCanonical_RejectsStaleHelloToken()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory,
            TestDataIds.AdminUserId,
            Roles.Admin);

        int exerciseId;
        int sentenceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercise = new WritingExercise
            {
                Title = "Stale Content Fixture",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Stale",
                PreviewText = "Stale token fixture",
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
            context.WritingExercises.Add(exercise);
            await context.SaveChangesAsync();
            exerciseId = exercise.Id;
            sentenceId = exercise.WritingExerciseSentences.Single().Id;
        }

        var helloToken = WritingAssessmentPolicy.BuildAcceptedAnswersStateToken(
            exerciseId,
            sentenceId,
            "Xin chao.",
            "Hello.",
            Array.Empty<(int Id, string NormalizedHash, long ApprovedAtUtcTicks)>());

        var editToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            adminClient,
            $"/Admin/WritingExerciseManagement/Edit/{exerciseId}");

        using (var editResponse = await adminClient.PostAsync(
                   $"/Admin/WritingExerciseManagement/Edit/{exerciseId}",
                   new FormUrlEncodedContent(
                   [
                       new KeyValuePair<string, string>("__RequestVerificationToken", editToken),
                       new KeyValuePair<string, string>("Id", exerciseId.ToString()),
                       new KeyValuePair<string, string>("Title", "Stale Content Fixture"),
                       new KeyValuePair<string, string>("Level", "beginner"),
                       new KeyValuePair<string, string>("ContentType", "emails"),
                       new KeyValuePair<string, string>("Topic", "Stale"),
                       new KeyValuePair<string, string>("PreviewText", "Stale token fixture"),
                       new KeyValuePair<string, string>("FullVietnameseText", "Tam biet."),
                       new KeyValuePair<string, string>("FullEnglishText", "Goodbye."),
                       new KeyValuePair<string, string>("IsPublished", "true")
                   ])))
        {
            Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var sentence = await context.WritingExerciseSentences.AsNoTracking()
                .SingleAsync(item => item.Id == sentenceId);
            Assert.Equal("Goodbye.", sentence.EnglishMeaning);
        }

        var saveToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            adminClient,
            $"/Admin/WritingExerciseManagement/Edit/{exerciseId}");

        using (var saveResponse = await adminClient.PostAsync(
                   "/Admin/WritingExerciseManagement/SaveAcceptedAnswers",
                   new FormUrlEncodedContent(
                   [
                       new KeyValuePair<string, string>("__RequestVerificationToken", saveToken),
                       new KeyValuePair<string, string>("ExerciseId", exerciseId.ToString()),
                       new KeyValuePair<string, string>("SentenceId", sentenceId.ToString()),
                       new KeyValuePair<string, string>("ExpectedStateToken", helloToken),
                       new KeyValuePair<string, string>("Answers[0]", "Hi there."),
                       new KeyValuePair<string, string>("Answers[1]", ""),
                       new KeyValuePair<string, string>("Answers[2]", ""),
                       new KeyValuePair<string, string>("Answers[3]", ""),
                       new KeyValuePair<string, string>("Answers[4]", "")
                   ])))
        {
            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
            var body = await saveResponse.Content.ReadAsStringAsync();
            Assert.Contains("Hi there.", body, StringComparison.Ordinal);
            Assert.Contains("alert-danger", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Goodbye.", body, StringComparison.Ordinal);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var sentence = await context.WritingExerciseSentences.AsNoTracking()
                .SingleAsync(item => item.Id == sentenceId);
            Assert.Equal("Tam biet.", sentence.VietnameseText);
            Assert.Equal("Goodbye.", sentence.EnglishMeaning);
            Assert.Equal(0, await context.WritingAcceptedAnswers.CountAsync(a => a.WritingExerciseSentenceId == sentenceId));
        }
    }
}

[Collection("MariaDbAcceptance")]
public sealed class WritingAcceptedAnswersMariaDbConcurrencyTests
{
    [MariaDbAcceptanceFact]
    public async Task ConcurrentEmptyStateSaves_WithLockBarrier_DoNotExceedFiveVariants()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        var barrier = new ForUpdateContentionBarrier();
        await using var factory = new MariaDbAcceptanceFactory(connection, runId);
        factory.SaveInterceptors.Add(barrier);
        await EnsureMigratedAsync(connection, runId);
        await factory.InitializeAsync();

        int exerciseId;
        int sentenceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercise = new WritingExercise
            {
                Title = "Concurrent Empty Barrier",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Concurrency",
                PreviewText = "empty",
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
            context.WritingExercises.Add(exercise);
            await context.SaveChangesAsync();
            exerciseId = exercise.Id;
            sentenceId = exercise.WritingExerciseSentences.Single().Id;
        }

        await AssertExclusiveWinnerAsync(
            factory,
            barrier,
            exerciseId,
            sentenceId,
            "Xin chao.",
            "Hello.",
            existingAnswers: Array.Empty<string>(),
            answersA: Enumerable.Range(1, 5).Select(i => $"Alpha variant {i}.").ToArray(),
            answersB: Enumerable.Range(1, 5).Select(i => $"Beta variant {i}.").ToArray());
    }

    [MariaDbAcceptanceFact]
    public async Task ConcurrentExistingListSaves_WithLockBarrier_ReplaceEntireWinnerList()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        var barrier = new ForUpdateContentionBarrier();
        await using var factory = new MariaDbAcceptanceFactory(connection, runId);
        factory.SaveInterceptors.Add(barrier);
        await EnsureMigratedAsync(connection, runId);
        await factory.InitializeAsync();

        int exerciseId;
        int sentenceId;
        string[] seedAnswers = ["Seed one.", "Seed two.", "Seed three."];
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercise = new WritingExercise
            {
                Title = "Concurrent Existing Barrier",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Concurrency",
                PreviewText = "existing",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            };
            var sentence = new WritingExerciseSentence
            {
                SortOrder = 1,
                VietnameseText = "Xin chao.",
                EnglishMeaning = "Hello.",
                BreakAfter = false
            };
            foreach (var answer in seedAnswers)
            {
                sentence.WritingAcceptedAnswers.Add(new WritingAcceptedAnswer
                {
                    AnswerText = answer,
                    NormalizedHash = WritingAssessmentPolicy.ComputeNormalizedHash(answer),
                    ApprovedByUserId = TestDataIds.AdminUserId,
                    ApprovedAtUtc = DateTime.UtcNow
                });
            }

            exercise.WritingExerciseSentences.Add(sentence);
            context.WritingExercises.Add(exercise);
            await context.SaveChangesAsync();
            exerciseId = exercise.Id;
            sentenceId = sentence.Id;
        }

        await AssertExclusiveWinnerAsync(
            factory,
            barrier,
            exerciseId,
            sentenceId,
            "Xin chao.",
            "Hello.",
            existingAnswers: seedAnswers,
            answersA: Enumerable.Range(1, 5).Select(i => $"Alpha keep {i}.").ToArray(),
            answersB: Enumerable.Range(1, 5).Select(i => $"Beta keep {i}.").ToArray());
    }

    [MariaDbAcceptanceFact]
    public async Task SaveVariantsVersusBulkEdit_StayConsistent()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        await using var factory = new MariaDbAcceptanceFactory(connection, runId);
        await EnsureMigratedAsync(connection, runId);
        await factory.InitializeAsync();

        int exerciseId;
        int sentenceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercise = new WritingExercise
            {
                Title = "Save vs Edit Fixture",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Concurrency",
                PreviewText = "save-vs-edit",
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
            context.WritingExercises.Add(exercise);
            await context.SaveChangesAsync();
            exerciseId = exercise.Id;
            sentenceId = exercise.WritingExerciseSentences.Single().Id;
        }

        var emptyToken = WritingAssessmentPolicy.BuildAcceptedAnswersStateToken(
            exerciseId,
            sentenceId,
            "Xin chao.",
            "Hello.",
            Array.Empty<(int Id, string NormalizedHash, long ApprovedAtUtcTicks)>());

        using var saveClient = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory, TestDataIds.AdminUserId, Roles.Admin);
        using var editClient = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory, TestDataIds.AdminUserId, Roles.Admin);

        var saveToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            saveClient, $"/Admin/WritingExerciseManagement/Edit/{exerciseId}");
        var editToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            editClient, $"/Admin/WritingExerciseManagement/Edit/{exerciseId}");

        var answers = Enumerable.Range(1, 5).Select(i => $"Keep variant {i}.").ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var saveTask = PostVariantsAsync(
            saveClient, saveToken, exerciseId, sentenceId, emptyToken, answers, timeout.Token);
        var editTask = editClient.PostAsync(
            $"/Admin/WritingExerciseManagement/Edit/{exerciseId}",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", editToken),
                new KeyValuePair<string, string>("Id", exerciseId.ToString()),
                new KeyValuePair<string, string>("Title", "Save vs Edit Fixture"),
                new KeyValuePair<string, string>("Level", "beginner"),
                new KeyValuePair<string, string>("ContentType", "emails"),
                new KeyValuePair<string, string>("Topic", "Concurrency"),
                new KeyValuePair<string, string>("PreviewText", "save-vs-edit"),
                new KeyValuePair<string, string>("FullVietnameseText", "Tam biet."),
                new KeyValuePair<string, string>("FullEnglishText", "Goodbye."),
                new KeyValuePair<string, string>("IsPublished", "true")
            ]),
            timeout.Token);

        var saveStatus = await saveTask;
        using var editResponse = await editTask;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var sentence = await context.WritingExerciseSentences.AsNoTracking()
                .SingleAsync(item => item.Id == sentenceId);
            var variants = await context.WritingAcceptedAnswers.AsNoTracking()
                .Where(answer => answer.WritingExerciseSentenceId == sentenceId)
                .Select(answer => answer.AnswerText)
                .OrderBy(text => text)
                .ToListAsync();

            // Either save won first (Hello + 5 variants, edit blocked by guard)
            // or edit won first (Goodbye + 0 variants, save rejected as stale).
            if (string.Equals(sentence.EnglishMeaning, "Hello.", StringComparison.Ordinal))
            {
                Assert.Equal(HttpStatusCode.Redirect, saveStatus);
                Assert.Equal(5, variants.Count);
                Assert.All(variants, text => Assert.StartsWith("Keep variant ", text, StringComparison.Ordinal));
                Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
            }
            else
            {
                Assert.Equal("Goodbye.", sentence.EnglishMeaning);
                Assert.Equal("Tam biet.", sentence.VietnameseText);
                Assert.Empty(variants);
                Assert.Equal(HttpStatusCode.OK, saveStatus);
            }

            Assert.Equal(0, await context.UserWritingAttempts.CountAsync());
            Assert.Equal(0, await context.LearningCompletionCredits.CountAsync());
        }
    }

    private static async Task AssertExclusiveWinnerAsync(
        MariaDbAcceptanceFactory factory,
        ForUpdateContentionBarrier barrier,
        int exerciseId,
        int sentenceId,
        string vietnamese,
        string english,
        IReadOnlyList<string> existingAnswers,
        IReadOnlyList<string> answersA,
        IReadOnlyList<string> answersB)
    {
        IEnumerable<(int Id, string NormalizedHash, long ApprovedAtUtcTicks)> existingRows =
            Array.Empty<(int, string, long)>();
        if (existingAnswers.Count > 0)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            existingRows = await context.WritingAcceptedAnswers.AsNoTracking()
                .Where(answer => answer.WritingExerciseSentenceId == sentenceId)
                .OrderBy(answer => answer.Id)
                .Select(answer => new ValueTuple<int, string, long>(
                    answer.Id,
                    answer.NormalizedHash,
                    answer.ApprovedAtUtc.Ticks))
                .ToListAsync();
        }

        var token = WritingAssessmentPolicy.BuildAcceptedAnswersStateToken(
            exerciseId,
            sentenceId,
            vietnamese,
            english,
            existingRows);

        using var adminA = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory, TestDataIds.AdminUserId, Roles.Admin);
        using var adminB = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory, TestDataIds.AdminUserId, Roles.Admin);

        var tokenA = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            adminA, $"/Admin/WritingExerciseManagement/Edit/{exerciseId}");
        var tokenB = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            adminB, $"/Admin/WritingExerciseManagement/Edit/{exerciseId}");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var taskA = PostVariantsAsync(adminA, tokenA, exerciseId, sentenceId, token, answersA, timeout.Token);
        // Give A time to reach FOR UPDATE and wait for B-in-flight signal.
        await Task.Delay(80);
        var taskB = PostVariantsAsync(adminB, tokenB, exerciseId, sentenceId, token, answersB, timeout.Token);
        barrier.SignalSecondRequestStarted();

        var results = await Task.WhenAll(taskA, taskB);
        Assert.True(barrier.ForUpdateEntries >= 1);
        Assert.Contains(results, status => status == HttpStatusCode.Redirect);
        Assert.Contains(results, status => status == HttpStatusCode.OK);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var variants = await context.WritingAcceptedAnswers.AsNoTracking()
                .Where(answer => answer.WritingExerciseSentenceId == sentenceId)
                .Select(answer => answer.AnswerText)
                .ToListAsync();

            Assert.Equal(5, variants.Count);
            var allA = variants.All(text => answersA.Contains(text, StringComparer.Ordinal));
            var allB = variants.All(text => answersB.Contains(text, StringComparer.Ordinal));
            Assert.True(allA || allB, "Winner must fully replace the list, not merge A+B.");
            Assert.False(allA && allB);
        }
    }

    private static async Task EnsureMigratedAsync(string connection, string? runId)
    {
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId(runId);
        await using var migrateDb = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);
        await MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(
            migrateDb.Database.GetDbConnection(),
            target.Database,
            run);
        await migrateDb.Database.MigrateAsync();
    }

    private static async Task<HttpStatusCode> PostVariantsAsync(
        HttpClient client,
        string antiForgeryToken,
        int exerciseId,
        int sentenceId,
        string expectedStateToken,
        IReadOnlyList<string> answers,
        CancellationToken cancellationToken)
    {
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", antiForgeryToken),
            new("ExerciseId", exerciseId.ToString()),
            new("SentenceId", sentenceId.ToString()),
            new("ExpectedStateToken", expectedStateToken)
        };

        for (var index = 0; index < 5; index++)
        {
            pairs.Add(new KeyValuePair<string, string>(
                $"Answers[{index}]",
                index < answers.Count ? answers[index] : string.Empty));
        }

        using var response = await client.PostAsync(
            "/Admin/WritingExerciseManagement/SaveAcceptedAnswers",
            new FormUrlEncodedContent(pairs),
            cancellationToken);
        return response.StatusCode;
    }

    /// <summary>
    /// Holds the first FOR UPDATE until the test signals the second HTTP request
    /// is in flight, proving both contended at the lock point without sleep-only races.
    /// </summary>
    private sealed class ForUpdateContentionBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _secondStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _forUpdateEntries;

        public int ForUpdateEntries => _forUpdateEntries;

        public void SignalSecondRequestStarted() => _secondStarted.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                var entry = Interlocked.Increment(ref _forUpdateEntries);
                if (entry == 1)
                {
                    await _secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                    await Task.Delay(30, cancellationToken);
                }
            }

            return result;
        }
    }
}
