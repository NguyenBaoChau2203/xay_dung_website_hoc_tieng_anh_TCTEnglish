using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class PremiumWritingGenerationIntegrationTests
{
    [Fact]
    public async Task CreateFromAi_PremiumUser_CreatesPrivateExerciseAndSuccessLog()
    {
        var providerCallCount = 0;

        await using var factory = CreateFactory((_, _) =>
        {
            providerCallCount++;
            return Task.FromResult(new AiProviderReply(
                """
                {
                  "detectedSourceLanguage": "vi",
                  "suggestedTitle": "Client update follow-up",
                  "suggestedTopic": "Business",
                  "suggestedLevel": "advanced",
                  "suggestedContentType": "articles",
                  "previewText": "Xin chao, toi muon cap nhat du an.",
                  "sentences": [
                    {
                      "vietnameseText": "Xin chao, toi muon cap nhat du an.",
                      "englishMeaning": "Hello, I want to share a project update.",
                      "breakAfter": false
                    },
                    {
                      "vietnameseText": "Chung ta can chot lich trong tuan nay.",
                      "englishMeaning": "We need to finalize the schedule this week.",
                      "breakAfter": true
                    }
                  ]
                }
                """,
                120,
                180,
                300,
                "test-writing-model",
                "premium-writing-create"));
        });
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);

        using var response = await SendCreateFromAiRequestAsync(
            client,
            "premium-success-key",
            "Xin chao, toi muon cap nhat du an. Chung ta can chot lich trong tuan nay.");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, providerCallCount);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());

        var data = json.RootElement.GetProperty("data");
        var exerciseId = data.GetProperty("exerciseId").GetInt32();
        Assert.False(data.GetProperty("isReplay").GetBoolean());
        Assert.Equal(2, data.GetProperty("sentenceCount").GetInt32());
        Assert.Equal("advanced", data.GetProperty("level").GetString());
        Assert.Equal("articles", data.GetProperty("contentType").GetString());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var exercise = await context.WritingExercises
            .AsNoTracking()
            .Include(item => item.WritingExerciseSentences)
            .SingleAsync(item => item.Id == exerciseId);

        Assert.Equal(TestDataIds.UserId, exercise.UserId);
        Assert.Equal("premium-user-ai", exercise.SourceType);
        Assert.False(exercise.IsPublished);
        Assert.Equal("advanced", exercise.Level);
        Assert.Equal("articles", exercise.ContentType);
        Assert.Equal("Business", exercise.Topic);
        Assert.Equal(2, exercise.WritingExerciseSentences.Count);

        var successLog = await context.WritingGenerationLogs
            .AsNoTracking()
            .SingleAsync(log => log.UserId == TestDataIds.UserId);

        Assert.True(successLog.IsSuccess);
        Assert.Equal("create-from-ai", successLog.RequestType);
        Assert.Null(successLog.ErrorCode);
    }

    [Fact]
    public async Task CreateFromAi_PremiumUser_UsesDedicatedWritingGenerationRequestOptions()
    {
        AiProviderRequestOptions? capturedRequestOptions = null;

        await using var factory = CreateFactory(
            (_, requestOptions, _) =>
            {
                capturedRequestOptions = requestOptions;
                return Task.FromResult(new AiProviderReply(
                    """
                    {
                      "detectedSourceLanguage": "vi",
                      "suggestedTitle": "Dedicated options check",
                      "suggestedTopic": "Business",
                      "suggestedLevel": "intermediate",
                      "suggestedContentType": "articles",
                      "previewText": "Toi muon kiem tra cau hinh tao bai viet.",
                      "sentences": [
                        {
                          "vietnameseText": "Toi muon kiem tra cau hinh tao bai viet.",
                          "englishMeaning": "I want to verify writing generation request options.",
                          "breakAfter": true
                        }
                      ]
                    }
                    """,
                    80,
                    100,
                    180,
                    "test-writing-model",
                    "dedicated-options"));
            },
            services =>
            {
                services.PostConfigure<AiOptions>(options =>
                {
                    options.MaxOutputTokens = 9000;
                    options.RequestTimeoutSeconds = 300;
                });
            });
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);

        using var response = await SendCreateFromAiRequestAsync(
            client,
            "dedicated-options-key",
            "Toi muon kiem tra cau hinh tao bai viet AI nay trong he thong de tranh dung sai gioi han.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedRequestOptions);
        Assert.Equal(2400, capturedRequestOptions!.MaxOutputTokens);
        Assert.Equal(90, capturedRequestOptions.RequestTimeoutSeconds);
    }

    [Fact]
    public async Task CreateFromAi_StandardUser_IsForbiddenAndLogsFailureWithoutCallingProvider()
    {
        var providerCallCount = 0;

        await using var factory = CreateFactory((_, _) =>
        {
            providerCallCount++;
            return Task.FromResult(new AiProviderReply("{}", 1, 1, 2, "test-writing-model", "forbidden-call"));
        });
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await SendCreateFromAiRequestAsync(
            client,
            "standard-forbidden-key",
            "Xin chao, day la noi dung hop le de thu tao bai viet bang AI.");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, providerCallCount);
        Assert.Contains("premium_required", body, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        Assert.False(await context.WritingExercises
            .AsNoTracking()
            .AnyAsync(item => item.UserId == TestDataIds.UserId && item.SourceType == "premium-user-ai"));

        var failureLog = await context.WritingGenerationLogs
            .AsNoTracking()
            .SingleAsync(log => log.UserId == TestDataIds.UserId);

        Assert.False(failureLog.IsSuccess);
        Assert.Equal("premium_required", failureLog.ErrorCode);
    }

    [Fact]
    public async Task CreateFromAi_PremiumUser_ReplaysDuplicateSubmissionWithoutDuplicateExerciseOrQuotaBurn()
    {
        var providerCallCount = 0;

        await using var factory = CreateFactory((_, _) =>
        {
            providerCallCount++;
            return Task.FromResult(new AiProviderReply(
                """
                {
                  "detectedSourceLanguage": "vi",
                  "suggestedTitle": "Weekly follow-up",
                  "suggestedTopic": "Work",
                  "suggestedLevel": "intermediate",
                  "suggestedContentType": "emails",
                  "previewText": "Toi gui ban mot ban cap nhat.",
                  "sentences": [
                    {
                      "vietnameseText": "Toi gui ban mot ban cap nhat.",
                      "englishMeaning": "I am sending you an update.",
                      "breakAfter": false
                    },
                    {
                      "vietnameseText": "Hay cho toi biet khi nao ban ranh.",
                      "englishMeaning": "Please let me know when you are free.",
                      "breakAfter": true
                    }
                  ]
                }
                """,
                90,
                140,
                230,
                "test-writing-model",
                "duplicate-replay"));
        });
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);

        using var firstResponse = await SendCreateFromAiRequestAsync(
            client,
            "duplicate-replay-key",
            "Toi gui ban mot ban cap nhat. Hay cho toi biet khi nao ban ranh.");
        using var secondResponse = await SendCreateFromAiRequestAsync(
            client,
            "duplicate-replay-key",
            "Toi gui ban mot ban cap nhat. Hay cho toi biet khi nao ban ranh.");

        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(1, providerCallCount);

        using var firstJson = JsonDocument.Parse(firstBody);
        using var secondJson = JsonDocument.Parse(secondBody);

        var firstData = firstJson.RootElement.GetProperty("data");
        var secondData = secondJson.RootElement.GetProperty("data");

        Assert.Equal(firstData.GetProperty("exerciseId").GetInt32(), secondData.GetProperty("exerciseId").GetInt32());
        Assert.False(firstData.GetProperty("isReplay").GetBoolean());
        Assert.True(secondData.GetProperty("isReplay").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        Assert.Equal(1, await context.WritingExercises
            .AsNoTracking()
            .CountAsync(item => item.UserId == TestDataIds.UserId && item.SourceType == "premium-user-ai"));

        Assert.Equal(1, await context.WritingGenerationLogs
            .AsNoTracking()
            .CountAsync(log => log.UserId == TestDataIds.UserId && log.IsSuccess));
    }

    [Fact]
    public async Task CreateFromAi_PremiumUser_WhenDailyQuotaExceeded_ReturnsTooManyRequestsBeforeProviderCall()
    {
        var providerCallCount = 0;

        await using var factory = CreateFactory((_, _) =>
        {
            providerCallCount++;
            return Task.FromResult(new AiProviderReply("{}", 1, 1, 2, "test-writing-model", "quota-check"));
        });
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);
        await SeedSuccessfulGenerationLogsAsync(factory, TestDataIds.UserId, dailyLimit: 5);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);

        using var response = await SendCreateFromAiRequestAsync(
            client,
            "quota-exceeded-key",
            "Noi dung hop le de xac nhan gioi han tao bai viet trong ngay.");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
        Assert.Equal(0, providerCallCount);
        Assert.True(response.Headers.TryGetValues("Retry-After", out var retryAfterValues));
        Assert.NotEmpty(retryAfterValues);
        Assert.Contains("daily_quota_exceeded", body, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        Assert.Equal(5, await context.WritingGenerationLogs
            .AsNoTracking()
            .CountAsync(log => log.UserId == TestDataIds.UserId && log.IsSuccess));

        Assert.Equal(1, await context.WritingGenerationLogs
            .AsNoTracking()
            .CountAsync(log => log.UserId == TestDataIds.UserId && !log.IsSuccess && log.ErrorCode == "daily_quota_exceeded"));
    }

    [Fact]
    public async Task CreateFromAi_PremiumUser_WhenAiJsonIsMalformed_DoesNotPersistExerciseAndLogsFailure()
    {
        await using var factory = CreateFactory((_, _) =>
            Task.FromResult(new AiProviderReply(
                "not valid json",
                50,
                60,
                110,
                "test-writing-model",
                "malformed-json")));
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);

        using var response = await SendCreateFromAiRequestAsync(
            client,
            "malformed-json-key",
            "Day la mot doan van hop le de kiem tra truong hop AI tra ve JSON loi.");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("invalid_ai_json", body, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        Assert.False(await context.WritingExercises
            .AsNoTracking()
            .AnyAsync(item => item.UserId == TestDataIds.UserId && item.SourceType == "premium-user-ai"));

        var failureLog = await context.WritingGenerationLogs
            .AsNoTracking()
            .SingleAsync(log => log.UserId == TestDataIds.UserId);

        Assert.False(failureLog.IsSuccess);
        Assert.Equal("invalid_ai_json", failureLog.ErrorCode);
    }

    private static TestWebApplicationFactory CreateFactory(
        Func<IReadOnlyList<AiContextMessage>, CancellationToken, Task<AiProviderReply>> handler,
        Action<IServiceCollection>? additionalConfiguration = null)
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, msgs, ct) => handler(msgs, ct)));
            additionalConfiguration?.Invoke(services);
        });
    }

    private static TestWebApplicationFactory CreateFactory(
        Func<IReadOnlyList<AiContextMessage>, AiProviderRequestOptions?, CancellationToken, Task<AiProviderReply>> handler,
        Action<IServiceCollection>? additionalConfiguration = null)
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, msgs, opts, ct) => handler(msgs, opts, ct)));
            additionalConfiguration?.Invoke(services);
        });
    }

    private static async Task<HttpResponseMessage> SendCreateFromAiRequestAsync(
        HttpClient client,
        string idempotencyKey,
        string sourceText)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Exercises/CreateFromAi")
        {
            Content = JsonContent.Create(new
            {
                sourceText,
                idempotencyKey
            })
        };

        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return await client.SendAsync(request);
    }

    private static async Task UpdateUserRoleAsync(TestWebApplicationFactory factory, int userId, string role)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(item => item.UserId == userId);
        user.Role = role;
        await context.SaveChangesAsync();
    }

    private static async Task SeedSuccessfulGenerationLogsAsync(
        TestWebApplicationFactory factory,
        int userId,
        int dailyLimit)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        for (var index = 0; index < dailyLimit; index++)
        {
            context.WritingGenerationLogs.Add(new WritingGenerationLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RequestType = "create-from-ai",
                IsSuccess = true,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-index)
            });
        }

        await context.SaveChangesAsync();
    }
}
