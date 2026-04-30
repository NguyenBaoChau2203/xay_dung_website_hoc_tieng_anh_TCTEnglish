using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiProductionClassifierIntegrationTests
{
    [Fact]
    public async Task Send_WithProductionClassifier_UsesWebsiteGuideRoutesForQuickActions()
    {
        await using var factory = CreateProductionClassifierFactory();
        await factory.InitializeAsync();

        EnsureMlNetClassifierRegistered(factory);

        var cases = new (string Message, string Route)[]
        {
            ("Làm bài Reading như thế nào?", "/Home/Reading"),
            ("Tính năng Writing hoạt động ra sao?", "/Home/Writing"),
            ("Tính năng Listening gồm những phần nào?", "/Home/Listening"),
            ("Cách liên hệ hỗ trợ?", "/Home/Contact"),
            ("Tôi có thể xem thông báo ở đâu?", "/Notification/Index"),
            ("Cách đặt mục tiêu học hằng ngày?", "/Goals"),
            ("Daily challenge ở đâu?", "/Home/Index"),
            ("Cách gửi ảnh trong chat lớp học?", "/Home/Class"),
            ("Làm sao đưa set vào folder?", "/Home/Folder")
        };

        foreach (var (message, route) in cases)
        {
            var text = await SendAndReadTextAsync(factory, TestDataIds.UserId, message);
            Assert.Contains(route, text);
        }
    }

    [Fact]
    public async Task Send_WithProductionClassifier_UsesStudyRecommendationForQuickAction()
    {
        await using var factory = CreateProductionClassifierFactory();
        await factory.InitializeAsync();

        EnsureMlNetClassifierRegistered(factory);

        var text = await SendAndReadTextAsync(factory, TestDataIds.UserId, "toi nen hoc gi tiep theo");

        Assert.Contains("Sprint One User Set", text);
        Assert.Contains("Flashcard", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Giải thích passive voice")]
    [InlineData("Please do my English homework")]
    [InlineData("Tell me a random fact about Mars")]
    public async Task Send_WithProductionClassifier_RefusesOutOfScopePrompts(string message)
    {
        await using var factory = CreateProductionClassifierFactory();
        await factory.InitializeAsync();

        EnsureMlNetClassifierRegistered(factory);

        var text = await SendAndReadTextAsync(factory, TestDataIds.UserId, message);

        Assert.Contains("TCT English", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chỉ hỗ trợ", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MlNetClassifierAssets_ArePresentAndStable()
    {
        var contentRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "TCTEnglish"));

        var modelPath = Path.Combine(contentRoot, "Services", "AI", "Internal", "Data", "intent-classifier-model.zip");
        var seedPath = Path.Combine(contentRoot, "Services", "AI", "Internal", "Data", "intent-samples.seed.csv");

        Assert.True(File.Exists(modelPath), $"Missing model artifact at {modelPath}.");
        Assert.True(File.Exists(seedPath), $"Missing seed dataset at {seedPath}.");

        Assert.Equal("1E642396EBD6331A3BB3D046A9C79935A91DB3D745A8C89C37985F564EA74ECC", ComputeSha256(modelPath));
        Assert.Equal("C24D0196DFB51F2CE6E781329B01DF713A6DF9774EFE5C0B5C3B92AB65A2A371", ComputeSha256(seedPath));
    }

    private static TestWebApplicationFactory CreateProductionClassifierFactory()
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiQueryClassifier>();
            services.AddSingleton<IAiQueryClassifier, MlNetAiQueryClassifier>();
        });
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static void EnsureMlNetClassifierRegistered(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var classifier = scope.ServiceProvider.GetRequiredService<IAiQueryClassifier>();
        Assert.IsType<MlNetAiQueryClassifier>(classifier);
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
