using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class PremiumWritingLearnerFlowIntegrationTests
{
    [Fact]
    public async Task WritingExercises_PremiumOwner_ShowsPrivateExerciseInMySectionWithoutLeakingIntoPublicCatalog()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);
        var seededExercise = await SeedPrivateExerciseAsync(factory, TestDataIds.UserId);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);

        var body = await client.GetStringAsync("/Home/Writing/Exercises?level=beginner&contentType=emails");
        var jsonBody = await client.GetStringAsync("/Home/Writing/Exercises/Data?level=beginner&contentType=emails");
        using var json = System.Text.Json.JsonDocument.Parse(jsonBody);

        Assert.Contains("Bài viết của tôi", body, StringComparison.Ordinal);
        Assert.Contains(seededExercise.Title, body, StringComparison.Ordinal);

        var publicIds = json.RootElement.GetProperty("exercises")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32())
            .ToList();

        var myExercises = json.RootElement.GetProperty("myExercises").EnumerateArray().ToList();

        Assert.True(json.RootElement.GetProperty("canCreateFromAi").GetBoolean());
        Assert.False(json.RootElement.GetProperty("isMyExercisesLocked").GetBoolean());
        Assert.DoesNotContain(seededExercise.ExerciseId, publicIds);
        Assert.Contains(myExercises, item => item.GetProperty("id").GetInt32() == seededExercise.ExerciseId);

        using var practiceResponse = await client.GetAsync($"/Home/Writing/Practice/Data?exerciseId={seededExercise.ExerciseId}");
        var practiceBody = await practiceResponse.Content.ReadAsStringAsync();
        using var practiceJson = System.Text.Json.JsonDocument.Parse(practiceBody);

        Assert.Equal(HttpStatusCode.OK, practiceResponse.StatusCode);
        Assert.Equal(seededExercise.ExerciseId, practiceJson.RootElement.GetProperty("exerciseId").GetInt32());
        Assert.Equal(seededExercise.Title, practiceJson.RootElement.GetProperty("exerciseTitle").GetString());
    }

    [Fact]
    public async Task WritingExercises_DowngradedOwner_ShowsLockedShellAndBlocksPrivatePracticeAndDelete()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var seededExercise = await SeedPrivateExerciseAsync(factory, TestDataIds.UserId);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var listResponse = await client.GetAsync("/Home/Writing/Exercises?level=beginner&contentType=emails");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        var jsonBody = await client.GetStringAsync("/Home/Writing/Exercises/Data?level=beginner&contentType=emails");
        using var json = System.Text.Json.JsonDocument.Parse(jsonBody);
        var myExercises = json.RootElement.GetProperty("myExercises").EnumerateArray().ToList();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains("Bài viết của tôi", listBody, StringComparison.Ordinal);
        Assert.Contains(seededExercise.Title, listBody, StringComparison.Ordinal);
        Assert.Contains("Đã khóa do gói hiện tại", listBody, StringComparison.Ordinal);
        Assert.True(json.RootElement.GetProperty("isMyExercisesLocked").GetBoolean());
        Assert.False(json.RootElement.GetProperty("canCreateFromAi").GetBoolean());
        Assert.Contains(
            myExercises,
            item => item.GetProperty("id").GetInt32() == seededExercise.ExerciseId
                && item.GetProperty("isLocked").GetBoolean());

        using var practiceResponse = await client.GetAsync($"/Home/Writing/Practice/Data?exerciseId={seededExercise.ExerciseId}");
        Assert.Equal(HttpStatusCode.NotFound, practiceResponse.StatusCode);

        using var hintResponse = await client.GetAsync($"/Home/Writing/Practice/Hint?exerciseId={seededExercise.ExerciseId}&sentenceId={seededExercise.FirstSentenceId}");
        Assert.Equal(HttpStatusCode.NotFound, hintResponse.StatusCode);

        using var evaluateResponse = await PostEvaluateAsync(client, seededExercise.ExerciseId, seededExercise.FirstSentenceId);
        Assert.Equal(HttpStatusCode.NotFound, evaluateResponse.StatusCode);

        using var deleteResponse = await PostDeleteAsync(client, seededExercise.ExerciseId);
        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.True(await context.WritingExercises.AsNoTracking().AnyAsync(item => item.Id == seededExercise.ExerciseId));
    }

    [Fact]
    public async Task WritingExercises_Outsider_CannotSeeOrOpenAnotherUsersPrivateExercise()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);
        var seededExercise = await SeedPrivateExerciseAsync(factory, TestDataIds.UserId);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var jsonBody = await client.GetStringAsync("/Home/Writing/Exercises/Data?level=beginner&contentType=emails");
        using var json = System.Text.Json.JsonDocument.Parse(jsonBody);
        var myExercises = json.RootElement.GetProperty("myExercises").EnumerateArray().ToList();
        var publicExercises = json.RootElement.GetProperty("exercises").EnumerateArray().ToList();

        Assert.Empty(myExercises);
        Assert.DoesNotContain(
            publicExercises,
            item => item.GetProperty("id").GetInt32() == seededExercise.ExerciseId);

        using var practiceResponse = await client.GetAsync($"/Home/Writing/Practice/Data?exerciseId={seededExercise.ExerciseId}");
        Assert.Equal(HttpStatusCode.NotFound, practiceResponse.StatusCode);

        using var hintResponse = await client.GetAsync($"/Home/Writing/Practice/Hint?exerciseId={seededExercise.ExerciseId}&sentenceId={seededExercise.FirstSentenceId}");
        Assert.Equal(HttpStatusCode.NotFound, hintResponse.StatusCode);

        using var evaluateResponse = await PostEvaluateAsync(client, seededExercise.ExerciseId, seededExercise.FirstSentenceId);
        Assert.Equal(HttpStatusCode.NotFound, evaluateResponse.StatusCode);

        using var deleteResponse = await PostDeleteAsync(client, seededExercise.ExerciseId);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteOwnedWritingExercise_PremiumOwner_RemovesPrivateExerciseWithoutAttemptHistory()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);
        var seededExercise = await SeedPrivateExerciseAsync(factory, TestDataIds.UserId);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);
        using var deleteResponse = await PostDeleteAsync(client, seededExercise.ExerciseId);

        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False(await context.WritingExercises.AsNoTracking().AnyAsync(item => item.Id == seededExercise.ExerciseId));
    }

    [Fact]
    public async Task DeleteOwnedWritingExercise_WhenAttemptHistoryExists_KeepsExerciseAndShowsError()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);
        var seededExercise = await SeedPrivateExerciseAsync(factory, TestDataIds.UserId, addAttemptHistory: true);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);
        using var deleteResponse = await PostDeleteAsync(client, seededExercise.ExerciseId);

        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
        Assert.NotNull(deleteResponse.Headers.Location);

        var followupBody = await client.GetStringAsync(deleteResponse.Headers.Location);

        Assert.Contains("lịch sử luyện", followupBody, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.True(await context.WritingExercises.AsNoTracking().AnyAsync(item => item.Id == seededExercise.ExerciseId));
    }

    [Fact]
    public async Task DeleteAccount_WithPrivateWritingData_HardDeletesWritingRecordsBeforeUserRemoval()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);
        var seededExercise = await SeedPrivateExerciseAsync(factory, TestDataIds.UserId);

        await SeedWritingLifecycleDependenciesAsync(factory, seededExercise.ExerciseId, seededExercise.FirstSentenceId, TestDataIds.UserId, TestDataIds.OutsiderUserId);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);
        using var deleteAccountResponse = await PostDeleteAccountAsync(client);

        Assert.Equal(HttpStatusCode.Redirect, deleteAccountResponse.StatusCode);
        Assert.NotNull(deleteAccountResponse.Headers.Location);
        Assert.Equal("/Account/Login", deleteAccountResponse.Headers.Location!.OriginalString);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        Assert.False(await context.Users.AsNoTracking().AnyAsync(user => user.UserId == TestDataIds.UserId));
        Assert.False(await context.WritingExercises.AsNoTracking().AnyAsync(exercise => exercise.Id == seededExercise.ExerciseId));
        Assert.False(await context.WritingExerciseSentences.AsNoTracking().AnyAsync(sentence => sentence.WritingExerciseId == seededExercise.ExerciseId));
        Assert.False(await context.UserWritingAttempts.AsNoTracking().AnyAsync(attempt => attempt.WritingExerciseId == seededExercise.ExerciseId));
        Assert.False(await context.WritingGenerationLogs.AsNoTracking().AnyAsync(log => log.UserId == TestDataIds.UserId));
    }

    private static async Task UpdateUserRoleAsync(TestWebApplicationFactory factory, int userId, string role)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(item => item.UserId == userId);
        user.Role = role;
        await context.SaveChangesAsync();
    }

    private static async Task<SeededPrivateExercise> SeedPrivateExerciseAsync(
        TestWebApplicationFactory factory,
        int ownerUserId,
        bool addAttemptHistory = false)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var nextExerciseId = (await context.WritingExercises.MaxAsync(item => (int?)item.Id) ?? 0) + 1;
        var nextSentenceId = (await context.WritingExerciseSentences.MaxAsync(item => (int?)item.Id) ?? 0) + 1;
        var createdAtUtc = DateTime.UtcNow.AddMinutes(-5);
        const string title = "Private AI Writing Drill";

        context.WritingExercises.Add(new WritingExercise
        {
            Id = nextExerciseId,
            UserId = ownerUserId,
            Title = title,
            Level = "beginner",
            ContentType = "emails",
            Topic = "Personal Writing",
            SourceType = "premium-user-ai",
            PreviewText = "Practice a short private writing exercise created from AI.",
            IsPublished = false,
            CreatedAt = createdAtUtc
        });

        context.WritingExerciseSentences.AddRange(
            new WritingExerciseSentence
            {
                Id = nextSentenceId,
                WritingExerciseId = nextExerciseId,
                SortOrder = 1,
                VietnameseText = "Xin chào, tôi muốn cập nhật tiến độ hôm nay.",
                EnglishMeaning = "Hello, I want to share today's progress update.",
                BreakAfter = false
            },
            new WritingExerciseSentence
            {
                Id = nextSentenceId + 1,
                WritingExerciseId = nextExerciseId,
                SortOrder = 2,
                VietnameseText = "Chúng ta có thể chốt lịch vào chiều mai.",
                EnglishMeaning = "We can finalize the schedule tomorrow afternoon.",
                BreakAfter = true
            });

        if (addAttemptHistory)
        {
            context.UserWritingAttempts.Add(new UserWritingAttempt
            {
                UserId = ownerUserId,
                WritingExerciseId = nextExerciseId,
                WritingExerciseSentenceId = nextSentenceId,
                SubmittedAnswer = "Hello, I want to share today's progress update.",
                Passed = true,
                UsedAi = false,
                EvaluationSource = "rule-based",
                SummaryTitle = "Accepted",
                SummaryText = "Kept for delete-policy coverage.",
                ReviewText = "Existing history should block learner delete.",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
            });
        }

        await context.SaveChangesAsync();

        return new SeededPrivateExercise(nextExerciseId, nextSentenceId, title);
    }

    private static async Task<HttpResponseMessage> PostDeleteAsync(HttpClient client, int exerciseId)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Home/Writing/Exercises?level=beginner&contentType=emails");

        return await client.PostAsync(
            "/Home/Writing/Exercises/Delete",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                new KeyValuePair<string, string>("exerciseId", exerciseId.ToString()),
                new KeyValuePair<string, string>("level", "beginner"),
                new KeyValuePair<string, string>("contentType", "emails"),
                new KeyValuePair<string, string>("topic", "all"),
                new KeyValuePair<string, string>("status", "all"),
                new KeyValuePair<string, string>("page", "1")
            ]));
    }

    private static async Task<HttpResponseMessage> PostEvaluateAsync(HttpClient client, int exerciseId, int sentenceId)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Home/Writing/Practice?level=beginner&contentType=emails");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = JsonContent.Create(new
            {
                exerciseId,
                sentenceId,
                userAnswer = "This is a protected sentence attempt."
            })
        };

        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostDeleteAccountAsync(HttpClient client)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Account/Settings");

        return await client.PostAsync(
            "/Account/DeleteAccount",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            ]));
    }

    private static async Task SeedWritingLifecycleDependenciesAsync(
        TestWebApplicationFactory factory,
        int exerciseId,
        int sentenceId,
        int ownerUserId,
        int outsiderUserId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        context.UserWritingAttempts.Add(new UserWritingAttempt
        {
            UserId = outsiderUserId,
            WritingExerciseId = exerciseId,
            WritingExerciseSentenceId = sentenceId,
            SubmittedAnswer = "Outsider attempt bound to owner private exercise.",
            Passed = false,
            UsedAi = false,
            EvaluationSource = "rule-based",
            SummaryTitle = "Outsider attempt",
            SummaryText = "Used for account deletion lifecycle hardening test.",
            ReviewText = "Should be hard-deleted before owner account removal.",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });

        context.WritingGenerationLogs.Add(new WritingGenerationLog
        {
            Id = Guid.NewGuid(),
            UserId = ownerUserId,
            RequestType = "create-from-ai",
            IsSuccess = true,
            RequestedAtUtc = DateTime.UtcNow.AddMinutes(-3)
        });

        await context.SaveChangesAsync();
    }

    private sealed record SeededPrivateExercise(int ExerciseId, int FirstSentenceId, string Title);
}
