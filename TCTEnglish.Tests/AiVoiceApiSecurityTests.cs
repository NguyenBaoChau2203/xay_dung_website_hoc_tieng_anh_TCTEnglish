using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels.AI.Voice;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiVoiceApiSecurityTests
{
    [Fact]
    public async Task MutationsRequireAntiforgeryAndSameOrigin()
    {
        await using var f = AiVoiceSessionLifecycleTests.VoiceFactory(new()); await f.InitializeAsync();
        using var client = Client(f, TestDataIds.UserId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ai/voice/topics", Topic())).StatusCode);
        await Token(client);
        client.DefaultRequestHeaders.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ai/voice/topics", Topic())).StatusCode);
    }

    [Fact]
    public async Task PremiumCookieCannotGrantStandardAccountCustomTopics()
    {
        var fake = new FakeVoiceProvider(); await using var f = AiVoiceSessionLifecycleTests.VoiceFactory(fake); await f.InitializeAsync();
        using var client = Client(f, TestDataIds.UserId); client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, Roles.Premium);
        await Token(client);
        var response = await client.PostAsJsonAsync("/api/ai/voice/topics", Topic());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Contains("premium_required", await response.Content.ReadAsStringAsync()); Assert.Equal(0, fake.Creates);
    }

    [Fact]
    public async Task OtherOwnersTopicsHistoryEventsAndStopAre404()
    {
        await using var f = AiVoiceSessionLifecycleTests.VoiceFactory(new()); await f.InitializeAsync();
        using var owner = Client(f, TestDataIds.AdminUserId); await Token(owner);
        var created = await owner.PostAsJsonAsync("/api/ai/voice/topics", Topic()); created.EnsureSuccessStatusCode();
        var topic = await created.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        using var outsider = Client(f, TestDataIds.UserId); await Token(outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PutAsJsonAsync("/api/ai/voice/topics/" + topic!["id"], Topic())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.DeleteAsync("/api/ai/voice/topics/" + topic["id"])).StatusCode);
        var catalog = await AiVoiceSessionLifecycleTests.SeedTopic(f); var id = Guid.NewGuid();
        (await owner.PostAsJsonAsync("/api/ai/voice/sessions", AiVoiceSessionLifecycleTests.Input(id, catalog))).EnsureSuccessStatusCode();
        foreach (var path in new[] { $"history/{id}", $"sessions/{id}/events" }) Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync("/api/ai/voice/" + path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsync($"/api/ai/voice/sessions/{id}/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync("/api/ai/voice/sessions", AiVoiceSessionLifecycleTests.Input(id, catalog))).StatusCode);
        (await owner.PostAsync($"/api/ai/voice/sessions/{id}/close", null)).EnsureSuccessStatusCode();
    }

    internal static HttpClient Client(TestWebApplicationFactory f, int id)
    { var c = f.CreateClient(); c.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, id.ToString()); return c; }
    internal static async Task Token(HttpClient c)
    {
        var html = await c.GetStringAsync("/AI/Voice");
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success); c.DefaultRequestHeaders.Remove("RequestVerificationToken"); c.DefaultRequestHeaders.Add("RequestVerificationToken", WebUtility.HtmlDecode(match.Groups[1].Value));
    }
    private static VoiceTopicInput Topic() => new() { Title = "My role-play", Scenario = "Ask for directions", AiRole = "Local resident", LearnerRole = "Tourist", Level = "A2" };

    [Fact]
    public async Task StopReportsAcceptedWhileCleanupClaimIsHeldThenConfirmsIdempotentClosure()
    {
        var fake = new FakeVoiceProvider(); await using var f = AiVoiceSessionLifecycleTests.VoiceFactory(fake); await f.InitializeAsync();
        using var client = Client(f, TestDataIds.UserId); await Token(client);
        var topic = await AiVoiceSessionLifecycleTests.SeedTopic(f); var id = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/ai/voice/sessions", AiVoiceSessionLifecycleTests.Input(id, topic))).EnsureSuccessStatusCode();
        using var scope = f.Services.CreateScope(); var quota = scope.ServiceProvider.GetRequiredService<TCTEnglish.Services.AI.Voice.AiVoiceQuotaService>();
        await quota.MutateAsync(id, s => { s.CleanupClaim = "another_cleanup"; s.CleanupClaimUntilUtc = DateTime.UtcNow.AddMinutes(1); }, default);
        var pending = await client.PostAsync($"/api/ai/voice/sessions/{id}/close", null);
        Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
        var body = await pending.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        Assert.False(body!["closed"]); Assert.True(body["cleanupPending"]); Assert.Equal(0, fake.Hangups);
        await quota.MutateAsync(id, s => { s.CleanupClaim = null; s.CleanupClaimUntilUtc = null; }, default);
        for (var i = 0; i < 2; i++)
        {
            var closed = await client.PostAsync($"/api/ai/voice/sessions/{id}/close", null);
            Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
            body = await closed.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
            Assert.True(body!["closed"]); Assert.False(body["cleanupPending"]);
        }
        Assert.Equal(1, fake.Hangups); Assert.Equal(1, fake.Creates);
    }

    [Fact]
    public async Task DowngradeKeepsPrivateReadsAndDeleteButBlocksPremiumMutationsAndStart()
    {
        await using var f = AiVoiceSessionLifecycleTests.VoiceFactory(new()); await f.InitializeAsync();
        using var client = Client(f, TestDataIds.AdminUserId); await Token(client);
        var created = await client.PostAsJsonAsync("/api/ai/voice/topics", Topic()); created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<Dictionary<string, int>>())!["id"];
        using (var scope = f.Services.CreateScope())
        { var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>(); (await db.Users.FindAsync(TestDataIds.AdminUserId))!.Role = Roles.Standard; await db.SaveChangesAsync(); }
        var topics = await client.GetFromJsonAsync<VoiceTopicsVm>("/api/ai/voice/topics"); Assert.False(topics!.CanCreateCustom); Assert.Single(topics.Custom);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/ai/voice/topics/" + id, Topic())).StatusCode);
        var input = AiVoiceSessionLifecycleTests.Input(Guid.NewGuid(), id); input.TopicKind = "custom";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/ai/voice/sessions", input)).StatusCode);
        (await client.DeleteAsync("/api/ai/voice/topics/" + id)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DeletingPrivateTopicRequestsCleanupOfItsActiveSession()
    {
        await using var f = AiVoiceSessionLifecycleTests.VoiceFactory(new()); await f.InitializeAsync();
        using var client = Client(f, TestDataIds.AdminUserId); await Token(client);
        var created = await client.PostAsJsonAsync("/api/ai/voice/topics", Topic()); created.EnsureSuccessStatusCode();
        var topic = (await created.Content.ReadFromJsonAsync<Dictionary<string, int>>())!["id"];
        var id = Guid.NewGuid(); var input = AiVoiceSessionLifecycleTests.Input(id, topic); input.TopicKind = "custom";
        (await client.PostAsJsonAsync("/api/ai/voice/sessions", input)).EnsureSuccessStatusCode();
        (await client.DeleteAsync("/api/ai/voice/topics/" + topic)).EnsureSuccessStatusCode();
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var row = await db.AiVoiceSessions.AsNoTracking().SingleAsync(x => x.Id == id); Assert.True(row.CancelRequested);
        Assert.Equal("topic_deleted", row.CloseReason);
        (await client.PostAsync($"/api/ai/voice/sessions/{id}/close", null)).EnsureSuccessStatusCode();
    }
}
