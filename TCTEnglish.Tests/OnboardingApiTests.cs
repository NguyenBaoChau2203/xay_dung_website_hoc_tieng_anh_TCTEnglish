using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>S4: the JSON API: authentication, antiforgery, validation, idempotency, ownership and the rate limit.</summary>
public sealed class OnboardingApiTests
{
    private const int NewUser = 950;
    private const int OtherNewUser = 951;

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var systemId = await db.Users.Where(u => u.Email == "system@tct.local").Select(u => u.UserId).SingleAsync();
        foreach (var id in new[] { NewUser, OtherNewUser })
        {
            db.Users.Add(new User
            {
                UserId = id, Email = $"api{id}@test.local", PasswordHash = "hash", FullName = "Api " + id,
                Role = Roles.Standard, Status = UserStatus.Online, CreatedAt = DateTime.UtcNow
            });
        }

        Seed(db, systemId, "Phương tiện giao thông", "veh", 30);
        Seed(db, systemId, "Địa điểm trong thành phố", "place", 30);
        Seed(db, systemId, "Hỏi và chỉ đường", "phrase", 20);
        await db.SaveChangesAsync();
        return factory;
    }

    private static void Seed(DbflashcardContext db, int systemId, string name, string prefix, int count)
    {
        var set = new Set { OwnerId = systemId, SetName = name, Visibility = "Public", CreatedAt = DateTime.UtcNow };
        for (var i = 1; i <= count; i++)
        {
            set.Cards.Add(new Card
            {
                Term = $"{prefix}{i:D2}", Definition = $"nghĩa {prefix}{i:D2}", Cefr = i % 2 == 0 ? "A2" : "A1",
                ImageUrl = $"/uploads/shared/{prefix}{i:D2}.webp", Example = "Example.", Topic = name
            });
        }

        db.Sets.Add(set);
    }

    private static async Task<(HttpClient Client, string Token)> SignInAsync(TestWebApplicationFactory factory, int userId)
    {
        var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, Roles.Standard);
        return (client, await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Sets"));
    }

    private static HttpRequestMessage Post(string url, string? token, object? body = null, string? rawJson = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(rawJson ?? JsonSerializer.Serialize(body ?? new { }), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Add("RequestVerificationToken", token);
        }

        return request;
    }

    private static object Choices(string level = "A2", string[]? topics = null) =>
        new { goals = new[] { "travel", "communication" }, topics = topics ?? new[] { "A16", "A15", "C04" }, level, pace = 10 };

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static object CompleteBody(string requestId, string level = "A2") => new
    {
        requestId, goals = new[] { "travel", "communication" }, topics = new[] { "A16", "A15", "C04" }, level, pace = 10,
        decksOff = Array.Empty<string>(), knownCardIds = Array.Empty<int>()
    };

    // ── authentication and antiforgery ───────────────────────────────────

    [Theory]
    [InlineData("GET", "/api/onboarding/state")]
    [InlineData("POST", "/api/onboarding/prompted")]
    [InlineData("POST", "/api/onboarding/skip")]
    [InlineData("POST", "/api/onboarding/preview")]
    [InlineData("POST", "/api/onboarding/complete")]
    [InlineData("POST", "/api/onboarding/refill/1")]
    [InlineData("POST", "/api/onboarding/banner/dismiss")]
    public async Task Guests_AreNotServed(string method, string url)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST") { request.Content = new StringContent("{}", Encoding.UTF8, "application/json"); }

        using var response = await client.SendAsync(request);
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.BadRequest,
            $"A guest got {(int)response.StatusCode} from {method} {url}.");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/onboarding/prompted")]
    [InlineData("/api/onboarding/skip")]
    [InlineData("/api/onboarding/preview")]
    [InlineData("/api/onboarding/complete")]
    [InlineData("/api/onboarding/refill/1")]
    [InlineData("/api/onboarding/banner/dismiss")]
    public async Task EveryPost_RequiresTheAntiforgeryToken(string url)
    {
        await using var factory = await CreateFactoryAsync();
        var (client, _) = await SignInAsync(factory, NewUser);
        using var _ = client;

        using var response = await client.SendAsync(Post(url, token: null, body: Choices()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── state, preview, validation ───────────────────────────────────────

    [Fact]
    public async Task State_ForANewAccount_SaysToPrompt_AndNeverLeaksOtherData()
    {
        await using var factory = await CreateFactoryAsync();
        var (client, _) = await SignInAsync(factory, NewUser);
        using var _ = client;

        using var response = await client.GetAsync("/api/onboarding/state");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(text).RootElement;

        Assert.True(json.GetProperty("shouldPrompt").GetBoolean());
        Assert.Equal("Pending", json.GetProperty("status").GetString());
        Assert.Equal("first", json.GetProperty("mode").GetString());
        Assert.False(json.GetProperty("hasDecks").GetBoolean());
        Assert.DoesNotContain("@", text);
    }

    [Fact]
    public async Task State_ForAnExistingAccount_DoesNotPrompt()
    {
        await using var factory = await CreateFactoryAsync();
        var (client, _) = await SignInAsync(factory, TestDataIds.UserId); // owns a set already
        using var _ = client;

        var json = await JsonOf(await client.GetAsync("/api/onboarding/state"));
        Assert.False(json.GetProperty("shouldPrompt").GetBoolean());
        Assert.Equal("NotEligible", json.GetProperty("status").GetString());
        Assert.Equal("again", json.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Preview_ReturnsDecksWithCards_AndRejectsBadChoices()
    {
        await using var factory = await CreateFactoryAsync();
        var (client, token) = await SignInAsync(factory, NewUser);
        using var _ = client;

        using var ok = await client.SendAsync(Post("/api/onboarding/preview", token, Choices()));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var json = await JsonOf(ok);
        var decks = json.GetProperty("decks").EnumerateArray().ToList();
        Assert.Equal(2, decks.Count);
        Assert.Equal("Du lịch – bộ khởi đầu", decks[0].GetProperty("name").GetString());
        Assert.True(decks[0].GetProperty("cards").GetArrayLength() > 0);
        Assert.Equal("Phương tiện giao thông", decks[0].GetProperty("topics")[0].GetProperty("name").GetString());
        Assert.Equal(json.GetProperty("total").GetInt32(), decks.Sum(d => d.GetProperty("cards").GetArrayLength()));
        Assert.True(json.GetProperty("days").GetInt32() > 0);

        foreach (var bad in new object[]
        {
            new { goals = Array.Empty<string>(), topics = new[] { "A16", "A15", "C04" }, level = "A2", pace = 10 },
            new { goals = new[] { "travel" }, topics = new[] { "A16" }, level = "A2", pace = 10 },
            new { goals = new[] { "travel" }, topics = new[] { "A16", "A15", "C04" }, level = "Z9", pace = 10 },
            new { goals = new[] { "travel" }, topics = new[] { "A16", "A15", "C04" }, level = "A2", pace = 3 },
        })
        {
            using var response = await client.SendAsync(Post("/api/onboarding/preview", token, bad));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(string.IsNullOrWhiteSpace((await JsonOf(response)).GetProperty("error").GetString()));
        }

        using var broken = await client.SendAsync(Post("/api/onboarding/preview", token, rawJson: "{ not json"));
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
    }

    // ── complete, idempotency, refill, ownership ─────────────────────────

    [Fact]
    public async Task Complete_CreatesTheDecks_ReturnsLinks_AndARetryReturnsTheSameThing()
    {
        await using var factory = await CreateFactoryAsync();
        var (client, token) = await SignInAsync(factory, NewUser);
        using var _ = client;
        var requestId = Guid.NewGuid().ToString();

        using var first = await client.SendAsync(Post("/api/onboarding/complete", token, CompleteBody(requestId)));
        var text = await first.Content.ReadAsStringAsync();
        Assert.True(first.StatusCode == HttpStatusCode.OK, text);
        var json = JsonDocument.Parse(text).RootElement;
        var folderId = json.GetProperty("folderId").GetInt32();
        Assert.Equal($"/Sets/Folders/{folderId}", json.GetProperty("folderUrl").GetString());
        var decks = json.GetProperty("decks").EnumerateArray().ToList();
        Assert.Equal(2, decks.Count);
        var firstSetId = decks[0].GetProperty("setId").GetInt32();
        Assert.Equal($"/Sets/{firstSetId}", decks[0].GetProperty("url").GetString());
        Assert.Equal($"/Practice/learn?set={firstSetId}&n=10&from=set", json.GetProperty("firstLearnUrl").GetString());
        Assert.DoesNotContain("@", text);

        using var retry = await client.SendAsync(Post("/api/onboarding/complete", token, CompleteBody(requestId)));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(firstSetId, (await JsonOf(retry)).GetProperty("decks")[0].GetProperty("setId").GetInt32());

        using var conflict = await client.SendAsync(Post("/api/onboarding/complete", token, CompleteBody(requestId, level: "B1")));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(2, await db.Sets.CountAsync(s => s.OwnerId == NewUser));
        Assert.Equal(1, await db.Folders.CountAsync(f => f.UserId == NewUser));

        // the decks are ordinary personal sets: the normal page opens them
        using var page = await client.GetAsync($"/Sets/{firstSetId}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task Complete_RejectsABadRequestId_AndBadBodies()
    {
        await using var factory = await CreateFactoryAsync();
        var (client, token) = await SignInAsync(factory, NewUser);
        using var _ = client;

        foreach (var id in new[] { "", "not-a-guid", Guid.Empty.ToString() })
        {
            using var response = await client.SendAsync(Post("/api/onboarding/complete", token, CompleteBody(id)));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var missingPreview = await client.SendAsync(Post("/api/onboarding/complete", token, new
        {
            requestId = Guid.NewGuid().ToString(), goals = new[] { "travel" }, topics = new[] { "A16", "A15", "C04" }, level = "A2", pace = 10,
            decksOff = new[] { "nope" }, knownCardIds = Array.Empty<int>()
        }));
        Assert.Equal(HttpStatusCode.BadRequest, missingPreview.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(0, await db.Sets.CountAsync(s => s.OwnerId == NewUser));
    }

    [Fact]
    public async Task Refill_AddsWords_ToTheUsersOwnDeck_AndOtherUsersGetA404()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, ownerToken) = await SignInAsync(factory, NewUser);
        var (other, otherToken) = await SignInAsync(factory, OtherNewUser);
        using var _ = owner;
        using var __ = other;

        var created = await JsonOf(await owner.SendAsync(Post("/api/onboarding/complete", ownerToken, CompleteBody(Guid.NewGuid().ToString()))));
        var setId = created.GetProperty("decks")[0].GetProperty("setId").GetInt32();

        using var refill = await owner.SendAsync(Post($"/api/onboarding/refill/{setId}", ownerToken, new { requestId = Guid.NewGuid().ToString(), count = 5 }));
        Assert.Equal(HttpStatusCode.OK, refill.StatusCode);
        var json = await JsonOf(refill);
        Assert.Equal(5, json.GetProperty("added").GetInt32());
        Assert.Equal($"/Sets/{setId}", json.GetProperty("setUrl").GetString());

        // not theirs: 404 (never 403), and nothing is added
        using var stranger = await other.SendAsync(Post($"/api/onboarding/refill/{setId}", otherToken, new { requestId = Guid.NewGuid().ToString(), count = 5 }));
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        using var missing = await owner.SendAsync(Post("/api/onboarding/refill/999999", ownerToken, new { requestId = Guid.NewGuid().ToString(), count = 5 }));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var badCount = await owner.SendAsync(Post($"/api/onboarding/refill/{setId}", ownerToken, new { requestId = Guid.NewGuid().ToString(), count = 50 }));
        Assert.Equal(HttpStatusCode.BadRequest, badCount.StatusCode);
        using var badId = await owner.SendAsync(Post($"/api/onboarding/refill/{setId}", ownerToken, new { requestId = "x", count = 5 }));
        Assert.Equal(HttpStatusCode.BadRequest, badId.StatusCode);
    }

    [Fact]
    public async Task SkipPromptedAndBanner_WorkForTheSignedInUser()
    {
        await using var factory = await CreateFactoryAsync();
        var (client, token) = await SignInAsync(factory, NewUser);
        using var _ = client;

        using var prompted = await client.SendAsync(Post("/api/onboarding/prompted", token));
        Assert.Equal(HttpStatusCode.NoContent, prompted.StatusCode);
        using var skip = await client.SendAsync(Post("/api/onboarding/skip", token));
        Assert.Equal("Pending", (await JsonOf(skip)).GetProperty("status").GetString());
        using var banner = await client.SendAsync(Post("/api/onboarding/banner/dismiss", token));
        Assert.Equal(HttpStatusCode.NoContent, banner.StatusCode);

        var state = await JsonOf(await client.GetAsync("/api/onboarding/state"));
        Assert.False(state.GetProperty("shouldPrompt").GetBoolean()); // prompted just now
    }

    [Fact]
    public async Task WritingCalls_AreRateLimited_PerUser()
    {
        await using var factory = await CreateFactoryAsync();
        var (client, token) = await SignInAsync(factory, NewUser);
        var (other, otherToken) = await SignInAsync(factory, OtherNewUser);
        using var _ = client;
        using var __ = other;

        for (var i = 0; i < 10; i++)
        {
            using var response = await client.SendAsync(Post("/api/onboarding/complete", token, CompleteBody("bad-" + i)));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // a bad id is rejected before it counts
        }

        for (var i = 0; i < 10; i++)
        {
            using var response = await client.SendAsync(Post($"/api/onboarding/refill/999999", token, new { requestId = Guid.NewGuid().ToString(), count = 5 }));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var limited = await client.SendAsync(Post("/api/onboarding/refill/999999", token, new { requestId = Guid.NewGuid().ToString(), count = 5 }));
        Assert.Equal((HttpStatusCode)429, limited.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await JsonOf(limited)).GetProperty("error").GetString()));

        // another user has their own allowance
        using var fine = await other.SendAsync(Post("/api/onboarding/refill/999999", otherToken, new { requestId = Guid.NewGuid().ToString(), count = 5 }));
        Assert.Equal(HttpStatusCode.NotFound, fine.StatusCode);
    }
}
