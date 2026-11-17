using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingBriefGenerationTests
{
    internal const string CreateRoute = "/Home/Writing/Exercises/CreateFromAi";
    internal static object Brief(string key = "brief-1", string brief = "Write an email to request leave to visit my family.") =>
        new { mode = "from_brief", brief, level = "beginner", contentType = "emails", length = "short", tone = "formal", idempotencyKey = key };

    internal static TestWebApplicationFactory Factory(Func<IReadOnlyList<AiContextMessage>, CancellationToken, Task<AiProviderReply>> handler) =>
        new(s => { s.RemoveAll<IAiProviderClient>(); s.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, m, ct) => handler(m, ct))); });

    internal static async Task Premium(TestWebApplicationFactory factory)
    {
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
        user.Role = Roles.Premium; await db.SaveChangesAsync();
    }

    internal static async Task<HttpResponseMessage> Post(HttpClient client, string route, object body)
    {
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client,
            "/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1");
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task BriefOutageOffersExplicitConfirmationInsteadOfTranslatingDescription()
    {
        IReadOnlyList<AiContextMessage>? messages = null;
        await using var f = Factory((m, _) => { messages = m; throw new AiProviderException("synthetic", "circuit_open", false); });
        await Premium(f);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(f, TestDataIds.UserId, Roles.Premium);
        using var response = await Post(client, CreateRoute, Brief());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("awaiting_template", json.GetProperty("data").GetProperty("status").GetString());
        Assert.Contains(messages!, m => m.Role == "system" && m.Content.Contains("Compose original", StringComparison.Ordinal));
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False(await db.WritingExercises.AnyAsync(e => e.UserId == TestDataIds.UserId));
        Assert.False(await db.WritingGenerationLogs.AnyAsync(l => l.IsSuccess));
    }

    [Fact]
    public async Task SameUserAndKeyDifferentPayloadConflictsDurably()
    {
        var calls = 0;
        await using var f = Factory((_, _) => { calls++; return Task.FromResult(new AiProviderReply("{}", 0, 0, 0, "fake", null)); });
        await Premium(f);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(f, TestDataIds.UserId, Roles.Premium);
        using var first = await Post(client, CreateRoute, new { sourceText = "This source contains enough text for conversion.", idempotencyKey = "same-key" });
        using var second = await Post(client, CreateRoute, new { sourceText = "Another source contains enough text for conversion.", idempotencyKey = "same-key" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, calls);
    }
}
