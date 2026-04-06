using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class Sprint1SmokeTests
{
    [Fact]
    public async Task Landing_AllowsAnonymousUsers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var response = await client.GetAsync("/Home/Landing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_ReturnsOkForAuthenticatedUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var response = await client.GetAsync("/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task DailyChallengePartial_ReturnsOkForAuthenticatedUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var response = await client.GetAsync("/Home/GetDailyChallenge");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("answer-btn", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dashboard_RedirectsAnonymousUsersToLanding()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var response = await client.GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.DoesNotContain("/Home/Index", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Home/FolderDetail/201")]
    [InlineData("/Vocabulary/Detail?setId=302")]
    [InlineData("/Vocabulary/Study?setId=302")]
    [InlineData("/Home/ClassDetail/501")]
    public async Task CoreDetailRoutes_ReturnOkForAuthenticatedUser(string route)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Theory]
    [InlineData("/Home/Writing")]
    [InlineData("/Home/Writing/Exercises?level=beginner&contentType=emails")]
    [InlineData("/Home/Writing/Exercises/Data?level=beginner&contentType=emails")]
    public async Task WritingBrowseRoutes_ReturnOkForAnonymousUsers(string route)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task WritingExercises_ListPage_HidesFakeProgressMetadataAndReferenceAnswers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var response = await client.GetAsync("/Home/Writing/Exercises?level=beginner&contentType=emails");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("writing-attempt-badge", body, StringComparison.Ordinal);
        Assert.DoesNotContain("writing-status-badge", body, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"status\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("englishMeaning", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1")]
    [InlineData("/Home/Writing/Practice/Data?exerciseId=1")]
    [InlineData("/Home/Writing/Practice/Hint?exerciseId=1&sentenceId=1")]
    public async Task WritingPracticeRoutes_RejectAnonymousUsers(string route)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var response = await client.GetAsync(route);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Unauthorized });
    }

    [Fact]
    public async Task WritingPractice_RendersServerBackedLessonContent()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var response = await client.GetAsync("/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Just Checking In!", body, StringComparison.Ordinal);
        Assert.Contains("0/14 câu đã xong", body, StringComparison.Ordinal);
        Assert.Contains("data-sentence-item", body, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("credits", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("points", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tran trong", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Chuc may man", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Toi dang hoc lap trinh de xay dung mot trang web.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"englishMeaning\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WritingJsonEndpoints_ReturnServiceBackedPayloads()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var anonymousClient = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var authenticatedClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var exercisesBody = await anonymousClient.GetStringAsync("/Home/Writing/Exercises/Data?level=beginner&contentType=emails&topic=Personal%20Check-In");
        var practiceBody = await authenticatedClient.GetStringAsync("/Home/Writing/Practice/Data?exerciseId=1");
        var hintBody = await authenticatedClient.GetStringAsync("/Home/Writing/Practice/Hint?exerciseId=1&sentenceId=1");

        using var exercisesJson = JsonDocument.Parse(exercisesBody);
        using var practiceJson = JsonDocument.Parse(practiceBody);
        using var hintJson = JsonDocument.Parse(hintBody);
        var firstExercise = exercisesJson.RootElement.GetProperty("exercises")[0];

        Assert.Equal("emails", exercisesJson.RootElement.GetProperty("selectedContentTypeKey").GetString());
        Assert.Equal("Personal Check-In", exercisesJson.RootElement.GetProperty("selectedTopic").GetString());
        Assert.Equal("all", exercisesJson.RootElement.GetProperty("selectedStatus").GetString());
        Assert.False(exercisesJson.RootElement.GetProperty("showProgressMetadata").GetBoolean());
        Assert.True(exercisesJson.RootElement.GetProperty("exercises").GetArrayLength() > 0);
        Assert.True(firstExercise.TryGetProperty("sentenceCount", out var sentenceCount));
        Assert.True(sentenceCount.GetInt32() > 0);
        Assert.Equal(string.Empty, firstExercise.GetProperty("statusKey").GetString());
        Assert.Equal(string.Empty, firstExercise.GetProperty("statusLabel").GetString());
        Assert.Equal(0, firstExercise.GetProperty("attemptCount").GetInt32());
        Assert.Equal(0, firstExercise.GetProperty("completedSentenceCount").GetInt32());
        Assert.False(firstExercise.TryGetProperty("englishMeaning", out _));

        Assert.Equal(1, practiceJson.RootElement.GetProperty("exerciseId").GetInt32());
        Assert.Equal("Just Checking In!", practiceJson.RootElement.GetProperty("exerciseTitle").GetString());
        Assert.Equal(14, practiceJson.RootElement.GetProperty("totalSentenceCount").GetInt32());
        Assert.Equal(0, practiceJson.RootElement.GetProperty("completedSentenceCount").GetInt32());
        Assert.Equal(0, practiceJson.RootElement.GetProperty("attemptCount").GetInt32());
        Assert.Equal(1, practiceJson.RootElement.GetProperty("resumeSentenceId").GetInt32());
        Assert.Equal("not-started", practiceJson.RootElement.GetProperty("statusKey").GetString());
        Assert.True(practiceJson.RootElement.GetProperty("sentences").GetArrayLength() > 0);
        Assert.False(practiceJson.RootElement.GetProperty("sentences")[0].TryGetProperty("englishMeaning", out _));
        Assert.False(practiceJson.RootElement.GetProperty("sentences")[0].GetProperty("hasAccepted").GetBoolean());

        foreach (var sentence in practiceJson.RootElement.GetProperty("sentences").EnumerateArray())
        {
            var vietnameseText = sentence.GetProperty("vietnameseText").GetString() ?? string.Empty;
            Assert.DoesNotContain("Tran trong", vietnameseText, StringComparison.Ordinal);
            Assert.DoesNotContain("Chuc may man", vietnameseText, StringComparison.Ordinal);
        }

        Assert.Equal(1, hintJson.RootElement.GetProperty("sentenceId").GetInt32());
        Assert.Equal(1, hintJson.RootElement.GetProperty("sentenceNumber").GetInt32());
        Assert.True(hintJson.RootElement.TryGetProperty("hintText", out var hintText));
        Assert.False(string.IsNullOrWhiteSpace(hintText.GetString()));
        Assert.NotEqual("Hello!", hintText.GetString());

        int? closingSentenceId = null;
        foreach (var sentence in practiceJson.RootElement.GetProperty("sentences").EnumerateArray())
        {
            var sentenceId = sentence.GetProperty("id").GetInt32();
            var candidateHintBody = await authenticatedClient.GetStringAsync($"/Home/Writing/Practice/Hint?exerciseId=1&sentenceId={sentenceId}");
            using var candidateHintJson = JsonDocument.Parse(candidateHintBody);
            if (string.Equals(
                candidateHintJson.RootElement.GetProperty("hintTitle").GetString(),
                "Gợi ý lời kết",
                StringComparison.Ordinal))
            {
                closingSentenceId = sentenceId;
                break;
            }
        }

        Assert.True(closingSentenceId.HasValue, "Expected a seeded writing sentence with a closing phrase.");

        var closingHintBody = await authenticatedClient.GetStringAsync($"/Home/Writing/Practice/Hint?exerciseId=1&sentenceId={closingSentenceId.Value}");
        using var closingHintJson = JsonDocument.Parse(closingHintBody);
        Assert.Equal("Gợi ý lời kết", closingHintJson.RootElement.GetProperty("hintTitle").GetString());
    }

    [Fact]
    public async Task WritingEvaluate_RejectsAnonymousUsers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = new StringContent(
                """{"exerciseId":1,"sentenceId":1,"userAnswer":"Hello!"}""",
                Encoding.UTF8,
                "application/json")
        };

        var response = await client.SendAsync(request);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Unauthorized });
    }

    [Fact]
    public async Task WritingEvaluate_RejectsMissingAntiforgeryToken()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = new StringContent(
                """{"exerciseId":1,"sentenceId":1,"userAnswer":"Hello!"}""",
                Encoding.UTF8,
                "application/json")
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WritingEvaluate_ReturnsPassResultWithoutLeakingReferenceAnswer()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = new StringContent(
                """{"exerciseId":1,"sentenceId":1,"userAnswer":"Hello!"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());

        var data = json.RootElement.GetProperty("data");
        Assert.Equal(1, data.GetProperty("sentenceId").GetInt32());
        Assert.True(data.GetProperty("passed").GetBoolean());
        Assert.True(data.GetProperty("canAutoAdvance").GetBoolean());
        Assert.Equal("rule-based", data.GetProperty("evaluationSource").GetString());
        Assert.False(data.TryGetProperty("englishMeaning", out _));
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("summaryTitle").GetString()));
    }

    [Fact]
    public async Task WritingHint_ReturnsTooManyRequestsWhenRateLimited()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IWritingRequestRateLimiter>();
            services.AddSingleton<IWritingRequestRateLimiter>(new FixedWritingRequestRateLimiter(
                allowHint: false,
                allowEvaluation: true,
                retryAfterSeconds: 7));
        });
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Home/Writing/Practice/Hint?exerciseId=1&sentenceId=1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Retry-After", out var values));
        Assert.Contains("7", values);
        Assert.Contains("\"retryAfterSeconds\":7", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WritingEvaluate_ReturnsTooManyRequestsWhenRateLimited()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IWritingRequestRateLimiter>();
            services.AddSingleton<IWritingRequestRateLimiter>(new FixedWritingRequestRateLimiter(
                allowHint: true,
                allowEvaluation: false,
                retryAfterSeconds: 9));
        });
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = new StringContent(
                """{"exerciseId":1,"sentenceId":1,"userAnswer":"Hello!"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Retry-After", out var values));
        Assert.Contains("9", values);
        Assert.Contains("\"retryAfterSeconds\":9", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassDetail_RejectsAnonymousUsersSafely()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var response = await client.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized
            || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected an auth failure for anonymous class detail, got {(int)response.StatusCode} {response.StatusCode}.");
    }

    [Fact]
    public async Task LearningRecord_RejectsAnonymousRequests()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var response = await client.PostAsync(
            "/api/LearningApi/record",
            new StringContent("""{"cardId":401,"masteryLevel":"good","timestamp":"2026-03-19T00:00:00Z"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LearningRecord_AcceptsAuthenticatedRequestsWithAntiforgery()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Vocabulary/Study?setId=302");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/LearningApi/record")
        {
            Content = new StringContent(
                """{"cardId":401,"masteryLevel":"good","timestamp":"2026-03-19T00:00:00Z"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"success\":true", payload);
    }

    [Fact]
    public async Task LearningRecord_RejectsMissingAntiforgeryToken()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/LearningApi/record")
        {
            Content = new StringContent(
                """{"cardId":401,"masteryLevel":"good","timestamp":"2026-03-19T00:00:00Z"}""",
                Encoding.UTF8,
                "application/json")
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminUserList_ReturnsOkForAdminAndForbiddenForStandardUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var standardClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var adminResponse = await adminClient.GetAsync("/Admin/UserManagement");
        var standardResponse = await standardClient.GetAsync("/Admin/UserManagement");
        var adminBody = await adminResponse.Content.ReadAsStringAsync();

        Assert.True(adminResponse.StatusCode == HttpStatusCode.OK, adminBody);
        Assert.Equal(HttpStatusCode.Forbidden, standardResponse.StatusCode);
    }

    [Fact]
    public async Task JoinClass_RejectsWrongPassword()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Class");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/JoinClass")
        {
            Content = new StringContent(
                $"classId={TestDataIds.ClassId}&password=wrong-password",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task JoinClass_RejectsMissingAntiforgeryToken()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/JoinClass")
        {
            Content = new StringContent(
                $"classId={TestDataIds.ClassId}&password=correct-password",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ClassDetail_HidesPrivateDataFromOutsider()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var response = await client.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("tham gia", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE-CHAT-501", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Sprint One Admin", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Sprint One Folder", body, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"chatMessages\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tab-members", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tab-folders", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatUpload_ReturnsNotFoundForNonMembers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Class");
        using var formData = IntegrationTestClientHelper.CreatePngUploadForm(
            "image",
            "chat.png",
            "classId",
            TestDataIds.ClassId.ToString());

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Chat/UploadImage")
        {
            Content = formData
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class FixedWritingRequestRateLimiter : IWritingRequestRateLimiter
    {
        private readonly bool _allowHint;
        private readonly bool _allowEvaluation;
        private readonly int _retryAfterSeconds;

        public FixedWritingRequestRateLimiter(bool allowHint, bool allowEvaluation, int retryAfterSeconds)
        {
            _allowHint = allowHint;
            _allowEvaluation = allowEvaluation;
            _retryAfterSeconds = retryAfterSeconds;
        }

        public bool TryConsumeHint(int userId, string? ipAddress, out int retryAfterSeconds)
        {
            retryAfterSeconds = _allowHint ? 0 : _retryAfterSeconds;
            return _allowHint;
        }

        public bool TryConsumeEvaluation(int userId, string? ipAddress, out int retryAfterSeconds)
        {
            retryAfterSeconds = _allowEvaluation ? 0 : _retryAfterSeconds;
            return _allowEvaluation;
        }
    }
}
