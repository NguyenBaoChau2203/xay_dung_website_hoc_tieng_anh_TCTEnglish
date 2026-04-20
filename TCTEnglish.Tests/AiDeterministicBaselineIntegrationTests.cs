using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiDeterministicBaselineIntegrationTests
{
    [Fact]
    public async Task Send_WithDefaultInternalProvider_ReturnsGroundedVocabularyAnswer()
    {
        await using var factory = new TestWebApplicationFactory();
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
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateSendRequest("cach tao lop hoc", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = payload.RootElement.GetProperty("text").GetString();

        Assert.NotNull(text);
        Assert.Contains("/Class/Create", text);
    }

    [Fact]
    public async Task Send_WithDefaultInternalProvider_ReturnsGroundedClassAnswer()
    {
        await using var factory = new TestWebApplicationFactory();
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
        await using var factory = new TestWebApplicationFactory();
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
