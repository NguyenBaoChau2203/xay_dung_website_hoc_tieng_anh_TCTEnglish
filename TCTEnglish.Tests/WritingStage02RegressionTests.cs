using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.Writing;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingStage02RegressionTests
{
    [Fact]
    public async Task AdminForm_WithLegacyUnicodeHash_CanSaveWithoutFalseStaleConflict()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.WritingAcceptedAnswers.Add(new WritingAcceptedAnswer
            {
                WritingExerciseSentenceId = 1, AnswerText = "I like cafe\u0301.",
                // Before NFC normalization the combining accent was discarded.
                NormalizedHash = WritingAssessmentPolicy.ComputeNormalizedHash("I like cafe."),
                ApprovedByUserId = TestDataIds.AdminUserId, ApprovedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        var html = await client.GetStringAsync("/Admin/WritingExerciseManagement/Edit/1");
        var match = System.Text.RegularExpressions.Regex.Match(html, "name=\"ExpectedStateToken\" value=\"([a-f0-9]+)\"");
        Assert.True(match.Success);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Admin/WritingExerciseManagement/Edit/1");
        using var response = await client.PostAsync("/Admin/WritingExerciseManagement/SaveAcceptedAnswers",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["ExerciseId"] = "1", ["SentenceId"] = "1",
                ["ExpectedStateToken"] = match.Groups[1].Value, ["Answers[0]"] = "I like cafe\u0301."
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        var answer = await verify.ServiceProvider.GetRequiredService<DbflashcardContext>().WritingAcceptedAnswers.SingleAsync();
        Assert.Equal(WritingAssessmentPolicy.ComputeNormalizedHash(answer.AnswerText), answer.NormalizedHash);
    }

    [Theory]
    [InlineData("rate_limited", false)]
    [InlineData("provider_unavailable", false)]
    [InlineData("invalid_json", false)]
    [InlineData("rate_limited", true)]
    [InlineData("provider_unavailable", true)]
    [InlineData("invalid_json", true)]
    public async Task OptionalProviderFailure_KeepsLocalStatusThroughReload_WithoutLeakingDetails(string failure, bool accepted)
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.AddSingleton<IOptions<WritingEvaluationOptions>>(Options.Create(new WritingEvaluationOptions
                { EvaluationMode = "Local", CloudEnhancementEnabled = true }));
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _, _) =>
                failure == "invalid_json" ? Task.FromResult(new AiProviderReply("{}", 0, 0, 0, "fake", "fake"))
                    : throw new AiProviderException("secret-provider-details", failure, true)));
        });
        await factory.InitializeAsync();
        await WritingCompletionTransactionTests.SeedOneSentenceAsync(factory);
        using var scope = factory.Services.CreateScope();
        var writing = scope.ServiceProvider.GetRequiredService<IWritingService>();
        var result = await writing.EvaluateWritingSentenceAsync(1, 1, accepted ? "Hello!" : "Hi, my friend!", TestDataIds.UserId);
        Assert.Equal(accepted, result!.Passed);
        Assert.Equal(accepted ? "Accepted" : "NeedsReview", result.AssessmentStatus);
        Assert.DoesNotContain("secret-provider-details", System.Text.Json.JsonSerializer.Serialize(result), StringComparison.Ordinal);
        using var reloadScope = factory.Services.CreateScope();
        var reloaded = await reloadScope.ServiceProvider.GetRequiredService<IWritingService>()
            .GetWritingPracticeDataAsync(1, TestDataIds.UserId);
        Assert.Equal(result.AssessmentStatus, reloaded!.Sentences.Single().LastEvaluation!.AssessmentStatus);
        Assert.Equal(accepted ? 1 : 0, await reloadScope.ServiceProvider.GetRequiredService<DbflashcardContext>().LearningCompletionCredits.CountAsync());
    }

    [Fact]
    public async Task PracticeBootstrap_HidesUnsubmittedReferenceAnswers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var html = await client.GetStringAsync("/Home/Writing/Practice?exerciseId=1");
        Assert.DoesNotContain("I hope you are well.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("englishMeaning", html, StringComparison.OrdinalIgnoreCase);
        // Optional, synthetic Razor capture for local browser acceptance; no real user data.
        var capture = Environment.GetEnvironmentVariable("TCT_STAGE02_HTML");
        if (!string.IsNullOrWhiteSpace(capture)) await File.WriteAllTextAsync(capture, html);
    }

    [Theory]
    [InlineData("{\"exerciseId\":1,\"sentenceId\":1,\"userAnswer\":42}")]
    [InlineData("{\"exerciseId\":\"oops\",\"sentenceId\":1,\"userAnswer\":\"Hello!\"}")]
    [InlineData("{\"exerciseId\":1,\"sentenceId\":1,\"userAnswer\":null}")]
    [InlineData("{\"exerciseId\":1,\"sentenceId\":1,\"userAnswer\":\"   \"}")]
    [InlineData("{")]
    public async Task MalformedOrEmptyAnswer_Is400AndDoesNotPersist(string json)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Writing/Exercises");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DbflashcardContext>().UserWritingAttempts.ToListAsync());
    }

    [Fact]
    public async Task MissingAntiforgery_AndMismatchedSentence_DoNotWrite()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var noToken = await client.PostAsJsonAsync("/Home/Writing/Practice/Evaluate",
            new { exerciseId = 1, sentenceId = 1, userAnswer = "Hello!" });
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using var scope = factory.Services.CreateScope();
        var writing = scope.ServiceProvider.GetRequiredService<IWritingService>();
        Assert.Null(await writing.EvaluateWritingSentenceAsync(1, int.MaxValue, "Hello!", TestDataIds.UserId));
        Assert.Null(await writing.GetWritingSentenceHintAsync(1, int.MaxValue, TestDataIds.UserId));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DbflashcardContext>().UserWritingAttempts.ToListAsync());
    }

    [Theory]
    [InlineData(Roles.Standard, false, true, true)]
    [InlineData(Roles.Premium, false, true, true)]
    [InlineData(Roles.Standard, false, false, false)]
    [InlineData(Roles.Premium, false, false, false)]
    [InlineData(Roles.Premium, true, false, true)]
    [InlineData(Roles.Standard, true, false, false)]
    [InlineData(Roles.Admin, false, false, true)]
    public async Task AccessMatrix_AllPracticeSurfacesAgree(string role, bool privateOwner, bool published, bool allowed)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            (await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId)).Role = role;
            var exercise = await db.WritingExercises.SingleAsync(e => e.Id == 1);
            exercise.UserId = privateOwner ? TestDataIds.UserId : null;
            exercise.IsPublished = published;
            await db.SaveChangesAsync();
        }
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, role);
        var expected = allowed ? HttpStatusCode.OK : HttpStatusCode.NotFound;
        foreach (var path in new[] { "/Home/Writing/Practice?exerciseId=1", "/Home/Writing/Practice/Data?exerciseId=1",
            "/Home/Writing/Practice/Hint?exerciseId=1&sentenceId=1" })
        {
            using var response = await client.GetAsync(path);
            Assert.True(response.StatusCode == expected, $"{role} {path}: expected {expected}, actual {response.StatusCode}");
        }
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Writing/Exercises");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = JsonContent.Create(new { exerciseId = 1, sentenceId = 1, userAnswer = "Hello!" })
        };
        request.Headers.Add("RequestVerificationToken", token);
        using var evaluation = await client.SendAsync(request);
        Assert.Equal(expected, evaluation.StatusCode);
    }

    [Fact]
    public async Task OversizedNormalizedMatch_IsRejectedWithoutWritingAttempt()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client,
            "/Home/Writing/Practice?exerciseId=1");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = JsonContent.Create(new { exerciseId = 1, sentenceId = 1, userAnswer = "Hello" + new string('!', 1000) })
        };
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Empty(await db.UserWritingAttempts.ToListAsync());
        Assert.Empty(await db.UserWritingSentenceProgresses.ToListAsync());
    }

    [Theory]
    [InlineData("I\u00a0hope\u2003you\u202fare well.")]
    [InlineData("I hope you are well.\u00a0")]
    public void UnicodeWhitespace_NormalizesLikeBrowser(string answer)
    {
        Assert.Equal("I hope you are well.", WritingAssessmentPolicy.CollapseWhitespace(answer));
        Assert.True(WritingAssessmentPolicy.Assess("I hope you are well.", answer).Passed);
    }

    [Theory]
    [InlineData("I visit the café.", "I visit the cafe\u0301.")]
    public void CanonicallyEquivalentUnicode_IsAccepted(string reference, string answer)
    {
        Assert.True(WritingAssessmentPolicy.Assess(reference, answer).Passed);
        Assert.Equal(WritingAssessmentPolicy.ComputeNormalizedHash(reference), WritingAssessmentPolicy.ComputeNormalizedHash(answer));
    }

    [Fact]
    public void CombiningAccent_IsNotSilentlyDiscardedToAcceptDifferentWord()
    {
        Assert.False(WritingAssessmentPolicy.Assess("I visit the cafe.", "I visit the cafe\u0301.").Passed);
    }

    [Fact]
    public async Task FailedRetry_PreservesAcceptedStateAndResumeAfterReload()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var writing = scope.ServiceProvider.GetRequiredService<IWritingService>();
            Assert.True((await writing.EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId))!.Passed);
            Assert.False((await writing.EvaluateWritingSentenceAsync(1, 1, "A different greeting.", TestDataIds.UserId))!.Passed);
        }
        using var reload = factory.Services.CreateScope();
        var data = await reload.ServiceProvider.GetRequiredService<IWritingService>().GetWritingPracticeDataAsync(1, TestDataIds.UserId);
        Assert.NotNull(data);
        Assert.Equal(1, data.CompletedSentenceCount);
        Assert.Equal(2, data.ResumeSentenceId);
        var sentence = data.Sentences.Single(s => s.Id == 1);
        Assert.True(sentence.HasAccepted);
        Assert.Equal("Hello!", sentence.AcceptedAnswer);
        Assert.Equal("NeedsReview", sentence.LastEvaluation!.AssessmentStatus);
        var db = reload.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.True((await db.UserWritingSentenceProgresses.SingleAsync()).IsPassed);
        Assert.Empty(await db.LearningCompletionCredits.ToListAsync());
    }
}
