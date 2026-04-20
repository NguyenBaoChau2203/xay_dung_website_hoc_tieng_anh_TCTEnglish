using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiDeterministicBaselineIntegrationTests
{
    [Fact]
    public async Task Send_WithDefaultInternalProvider_ReturnsGroundedVocabularyAnswer()
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateSendRequest("toi co nhung bo tu vung nao", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = payload.RootElement.GetProperty("text").GetString();

        Assert.NotNull(text);
        Assert.Contains("Sprint One User Set", text);
    }

    [Fact]
    public async Task Send_WithDefaultInternalProvider_ReturnsGroundedWebsiteGuideAnswer()
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateSendRequest("cach tao lop hoc", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = payload.RootElement.GetProperty("text").GetString();

        Assert.NotNull(text);
        Assert.Contains("/Home/CreateClass", text);
    }

    [Fact]
    public async Task Send_WithDefaultInternalProvider_ReturnsGroundedClassAnswer()
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateSendRequest("lop hoc cua toi", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = payload.RootElement.GetProperty("text").GetString();

        Assert.NotNull(text);
        Assert.Contains("Sprint One Class", text);
    }

    [Fact]
    public async Task Send_WithDefaultInternalProvider_ReturnsGroundedCardLookupAnswer()
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateSendRequest("forecast nghia la gi", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = payload.RootElement.GetProperty("text").GetString();

        Assert.NotNull(text);
        Assert.Contains("forecast", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("predict", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_StudyRecommendation_WithOwnedSetAndNoProgress_ReturnsConcreteRecommendation()
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        var text = await SendAndReadTextAsync(factory, TestDataIds.UserId, "toi nen hoc gi tiep theo");

        Assert.Contains("Sprint One User Set", text);
        Assert.Contains("Flashcard", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quiz", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chưa có đủ dữ liệu học", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_StudyRecommendation_WithLearningProgress_ReturnsProgressBasedRecommendation()
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        await SeedStudyRecommendationProgressAsync(factory);

        var text = await SendAndReadTextAsync(factory, TestDataIds.UserId, "toi nen hoc gi tiep");

        Assert.Contains("Sprint One User Set", text);
        Assert.Contains("còn 2 thẻ", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chưa có đủ dữ liệu học", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_StudyRecommendation_WhenNoOwnedSet_ReturnsEmptyState()
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        var text = await SendAndReadTextAsync(factory, TestDataIds.OutsiderUserId, "toi nen hoc gi tiep");

        Assert.Contains("chưa có đủ dữ liệu học", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_StudyRecommendation_WithMasteredStatusVariants_UsesRealCompositionAndCountsRemainingCorrectly()
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        await SeedStudyRecommendationMasteredStatusVariantsAsync(factory);

        var text = await SendAndReadTextAsync(factory, TestDataIds.UserId, "toi nen hoc gi tiep");

        Assert.Contains("Sprint One User Set", text);
        Assert.Contains("còn 1 thẻ", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sprint One System Set", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_StudyRecommendation_DoesNotLeakAnotherUsersSetsOrCards()
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        await SeedStudyRecommendationOutsiderDataAsync(factory);

        var text = await SendAndReadTextAsync(factory, TestDataIds.UserId, "toi nen hoc gi tiep");

        Assert.Contains("Sprint One User Set", text);
        Assert.DoesNotContain("Outsider Study Set", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outsider-term", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Làm bài Reading như thế nào?", "/Home/Reading")]
    [InlineData("Tính năng Writing hoạt động ra sao?", "/Home/Writing")]
    [InlineData("Tính năng Listening gồm những phần nào?", "/Home/Listening")]
    [InlineData("Cách liên hệ hỗ trợ?", "/Home/Contact")]
    [InlineData("Tôi có thể xem thông báo ở đâu?", "/Notification/Index")]
    [InlineData("Cách đặt mục tiêu học hằng ngày?", "/Goals")]
    [InlineData("Daily challenge ở đâu?", "/Home/Index")]
    [InlineData("Cách gửi ảnh trong chat lớp học?", "/Home/Class")]
    [InlineData("Làm sao đưa set vào folder?", "/Home/Folder")]
    public async Task Send_QuickActionGuidePrompts_ReturnExpectedCurrentRoutes(string message, string expectedRoute)
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        var text = await SendAndReadTextAsync(factory, TestDataIds.UserId, message);

        Assert.Contains(expectedRoute, text);
    }

    [Theory]
    [InlineData("Giải thích passive voice")]
    [InlineData("Làm bài Reading này giúp tôi")]
    [InlineData("Please do my English homework")]
    [InlineData("Tell me a random fact about Mars")]
    public async Task Send_OutOfScopePrompts_AreRefused(string message)
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        var text = await SendAndReadTextAsync(factory, TestDataIds.UserId, message);

        Assert.Contains("TCT English", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chỉ hỗ trợ", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Home/Reading")]
    [InlineData("/Home/Writing")]
    [InlineData("/Home/Listening")]
    [InlineData("/Home/Contact")]
    [InlineData("/Notification/Index")]
    public async Task QuickActionGuideRoutes_ResolveToCurrentAppEndpoints(string route)
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WebsiteGuideRoutes_ResolveToCurrentAppEndpoints()
    {
        await using var factory = CreateDeterministicFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        foreach (var route in GetWebsiteGuideRoutes())
        {
            using var response = await client.GetAsync(route);

            Assert.True(IsAcceptableRouteStatus(response.StatusCode), $"Route {route} returned {response.StatusCode}.");
        }
    }

    private static async Task<string> SendAndReadTextAsync(TestWebApplicationFactory factory, int userId, string message)
    {
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateSendRequest(message, antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = payload.RootElement.GetProperty("text").GetString();

        Assert.False(string.IsNullOrWhiteSpace(text));
        return text!;
    }

    private static async Task SeedStudyRecommendationMasteredStatusVariantsAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        context.Cards.AddRange(
            new Card
            {
                CardId = 410,
                SetId = TestDataIds.UserSetId,
                Term = "term-mastered-1",
                Definition = "definition-mastered-1"
            },
            new Card
            {
                CardId = 411,
                SetId = TestDataIds.UserSetId,
                Term = "term-mastered-2",
                Definition = "definition-mastered-2"
            },
            new Card
            {
                CardId = 412,
                SetId = TestDataIds.UserSetId,
                Term = "term-mastered-3",
                Definition = "definition-mastered-3"
            });

        context.LearningProgresses.AddRange(
            new LearningProgress
            {
                ProgressId = 510,
                UserId = TestDataIds.UserId,
                CardId = 401,
                Status = "Mastered"
            },
            new LearningProgress
            {
                ProgressId = 511,
                UserId = TestDataIds.UserId,
                CardId = 410,
                Status = "mastered"
            },
            new LearningProgress
            {
                ProgressId = 512,
                UserId = TestDataIds.UserId,
                CardId = 411,
                Status = "Learned"
            },
            new LearningProgress
            {
                ProgressId = 513,
                UserId = TestDataIds.UserId,
                CardId = 402,
                Status = "Mastered"
            });

        await context.SaveChangesAsync();
    }

    private static IReadOnlyList<string> GetWebsiteGuideRoutes()
    {
        var guideFilePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "TCTEnglish",
            "wwwroot",
            "data",
            "ai",
            "website-guides.json"));

        using var stream = File.OpenRead(guideFilePath);
        using var document = JsonDocument.Parse(stream);

        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var guide in document.RootElement.GetProperty("guides").EnumerateArray())
        {
            if (!guide.TryGetProperty("route", out var routeElement))
            {
                continue;
            }

            var route = routeElement.GetString();
            if (string.IsNullOrWhiteSpace(route))
            {
                continue;
            }

            routes.Add(route);
        }

        return routes.ToList();
    }

    private static bool IsAcceptableRouteStatus(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.OK
            || statusCode == HttpStatusCode.Redirect
            || statusCode == HttpStatusCode.RedirectMethod
            || statusCode == HttpStatusCode.RedirectKeepVerb
            || statusCode == HttpStatusCode.SeeOther
            || statusCode == HttpStatusCode.PermanentRedirect;
    }

    private static async Task SeedStudyRecommendationProgressAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        context.Cards.AddRange(
            new Card
            {
                CardId = 420,
                SetId = TestDataIds.UserSetId,
                Term = "term-learning",
                Definition = "definition-learning"
            },
            new Card
            {
                CardId = 421,
                SetId = TestDataIds.UserSetId,
                Term = "term-mastered",
                Definition = "definition-mastered"
            });

        context.LearningProgresses.AddRange(
            new LearningProgress
            {
                ProgressId = 520,
                UserId = TestDataIds.UserId,
                CardId = 420,
                Status = "Learning",
                LastReviewedDate = DateTime.UtcNow
            },
            new LearningProgress
            {
                ProgressId = 521,
                UserId = TestDataIds.UserId,
                CardId = 421,
                Status = "Mastered",
                LastReviewedDate = DateTime.UtcNow.AddMinutes(-5)
            });

        await context.SaveChangesAsync();
    }

    private static async Task SeedStudyRecommendationOutsiderDataAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        context.Sets.Add(new Set
        {
            SetId = 990,
            OwnerId = TestDataIds.OutsiderUserId,
            SetName = "Outsider Study Set",
            CreatedAt = DateTime.UtcNow
        });

        context.Cards.Add(new Card
        {
            CardId = 991,
            SetId = 990,
            Term = "outsider-term",
            Definition = "outsider definition"
        });

        context.LearningProgresses.AddRange(
            new LearningProgress
            {
                ProgressId = 992,
                UserId = TestDataIds.OutsiderUserId,
                CardId = 991,
                Status = "Learning",
                LastReviewedDate = DateTime.UtcNow
            },
            new LearningProgress
            {
                ProgressId = 993,
                UserId = TestDataIds.UserId,
                CardId = 991,
                Status = "Learning",
                LastReviewedDate = DateTime.UtcNow
            });

        await context.SaveChangesAsync();
    }

    private static TestWebApplicationFactory CreateDeterministicFactory()
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiQueryClassifier>();
            services.AddSingleton<IAiQueryClassifier, DeterministicIntentClassifier>();
        });
    }

    private static HttpRequestMessage CreateSendRequest(string message, string antiForgeryToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Send")
        {
            Content = JsonContent.Create(new
            {
                conversationId = (Guid?)null,
                message
            })
        };

        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return request;
    }
}
