using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// The notification routes over HTTP: page parameters, the JSON contract (old fields kept, new fields added),
/// the keyset cursor, mark-read / mark-all counts, ownership (404) and antiforgery (400).
/// Learner 101 owns 28 unread notifications; learner 103 owns 3.
/// </summary>
public sealed class NotificationPageIntegrationTests
{
    private const int Owner = TestDataIds.UserId;       // 101
    private const int Other = TestDataIds.OutsiderUserId; // 103
    private const int LearningRows = 25;

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var now = DateTime.UtcNow;

        for (var i = 1; i <= LearningRows; i++)
        {
            db.Notifications.Add(new Notification
            {
                UserId = Owner,
                Type = NotificationType.GoalProgress,
                Title = $"Learn {i:000}",
                Message = $"Learn message {i:000}",
                IconClass = "fas fa-chart-line",
                RelatedUrl = "/Goals",
                CreatedAt = now.AddMinutes(-60 - i)
            });
        }

        db.Notifications.AddRange(
            new Notification
            {
                UserId = Owner, Type = NotificationType.BadgeEarned, Title = "BADGE-ONE", Message = "Badge message",
                RelatedUrl = "/Goals#badges", CreatedAt = now.AddMinutes(-1)
            },
            new Notification
            {
                UserId = Owner, Type = NotificationType.PremiumActivated, Title = "PAY-ONE", Message = "Pay message",
                RelatedUrl = "/Billing/Order/TCT-1", CreatedAt = now.AddMinutes(-2)
            },
            new Notification
            {
                UserId = Owner, Type = NotificationType.AdminAnnouncement, Title = "SYS-ONE", Message = "System message",
                RelatedUrl = null, CreatedAt = now.AddMinutes(-3)
            });

        for (var i = 1; i <= 3; i++)
        {
            db.Notifications.Add(new Notification
            {
                UserId = Other, Type = NotificationType.BadgeEarned, Title = $"OTHER-{i}", Message = "Other",
                CreatedAt = now.AddMinutes(-i)
            });
        }

        await db.SaveChangesAsync();
        return factory;
    }

    private static HttpClient Client(TestWebApplicationFactory factory, int userId = Owner)
        => IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, Roles.Standard);

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{url} -> {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (token != null)
        {
            request.Headers.Add("RequestVerificationToken", token);
        }

        return await client.SendAsync(request);
    }

    private static async Task<int> UnreadAsync(TestWebApplicationFactory factory, int userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        return await db.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    private static string Decoded(string html) => WebUtility.HtmlDecode(html);

    // ── Page routes and parameters ─────────────────────────────────────

    [Theory]
    [InlineData("/Notification")]
    [InlineData("/Notification/Index")]
    public async Task Page_OpensAtBothPaths(string path)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("BADGE-ONE", Decoded(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Page_RequiresSignIn()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync("/Notification");

        // The test host answers an anonymous request with 401; the real cookie scheme redirects to the login page.
        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized,
            $"Unexpected status {(int)response.StatusCode}");
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("Login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Page_UnknownFilterAndCategory_FallBackToEverything()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        using var response = await client.GetAsync("/Notification?filter=%3Cscript%3E&category=zzz");
        var html = Decoded(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("BADGE-ONE", html);
        Assert.Contains("SYS-ONE", html);
        // The page never builds a link from the raw query values, and nothing is echoed unencoded.
        // (The shared create-folder dialog keeps the current URL in an encoded hidden "returnUrl" input; that
        // is layout code and is not part of this page.)
        Assert.DoesNotMatch("href=\"[^\"]*(category=zzz|filter=%3Cscript)", html);
        Assert.DoesNotContain("filter=<script>", html);
    }

    [Theory]
    [InlineData("badge", "BADGE-ONE")]
    [InlineData("BADGE", "BADGE-ONE")]
    [InlineData("payment", "PAY-ONE")]
    [InlineData("system", "SYS-ONE")]
    [InlineData("learning", "Learn 001")]
    public async Task Page_Category_ShowsOnlyThatCategory(string category, string expectedTitle)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        using var response = await client.GetAsync($"/Notification?category={category}");
        var html = Decoded(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedTitle, html);
        foreach (var other in new[] { "BADGE-ONE", "PAY-ONE", "SYS-ONE", "Learn 001" }.Where(t => t != expectedTitle))
        {
            Assert.DoesNotContain(other, html);
        }

        Assert.DoesNotContain("OTHER-1", html); // another user's rows never show
    }

    [Fact]
    public async Task Page_BeyondTheLastPage_RedirectsToTheLastPageWithTheSameFilters()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        // 28 unread notifications at 20 per page = 2 pages.
        using var response = await client.GetAsync("/Notification?page=9&filter=unread");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("page=2", location);
        Assert.Contains("filter=unread", location);

        using var landed = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, landed.StatusCode);
    }

    [Fact]
    public async Task Page_BeyondTheLastPage_KeepsTheCategory()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        using var response = await client.GetAsync("/Notification?page=5&category=badge");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("page=1", location);
        Assert.Contains("category=badge", location);
    }

    [Theory]
    [InlineData("/Notification?page=2")]
    [InlineData("/Notification?page=1")]
    [InlineData("/Notification?page=0")]
    [InlineData("/Notification?page=-4")]
    public async Task Page_InRangeOrNonPositivePage_DoesNotRedirect(string url)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── JSON list ──────────────────────────────────────────────────────

    [Fact]
    public async Task Api_List_KeepsTheOldFields_AndAddsTheNewOnes()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        var json = await GetJsonAsync(client, "/api/notifications?page=1&pageSize=5");

        Assert.True(json.GetProperty("success").GetBoolean());
        var data = json.GetProperty("data");
        foreach (var field in new[] { "items", "unreadCount", "totalCount", "currentPage", "hasMore", "nextCursor" })
        {
            Assert.True(data.TryGetProperty(field, out _), $"data.{field} is missing");
        }

        Assert.Equal(28, data.GetProperty("unreadCount").GetInt32());
        Assert.Equal(28, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, data.GetProperty("currentPage").GetInt32());
        Assert.True(data.GetProperty("hasMore").GetBoolean());

        var items = data.GetProperty("items");
        Assert.Equal(5, items.GetArrayLength());
        var first = items[0];
        foreach (var field in new[]
                 {
                     // original contract
                     "id", "type", "title", "message", "isRead", "relatedUrl", "iconClass", "createdAt", "timeAgo",
                     // added
                     "category", "tone", "url", "linkLabel", "timeText", "dayGroup", "createdAtUtc"
                 })
        {
            Assert.True(first.TryGetProperty(field, out _), $"item.{field} is missing");
        }

        Assert.Equal("BADGE-ONE", first.GetProperty("title").GetString());
        Assert.Equal("BadgeEarned", first.GetProperty("type").GetString());
        Assert.Equal("badge", first.GetProperty("category").GetString());
        Assert.Equal("warning", first.GetProperty("tone").GetString());
        Assert.Equal("/Goals#badges", first.GetProperty("url").GetString());
        Assert.Equal("/Goals#badges", first.GetProperty("relatedUrl").GetString());
        Assert.Equal("Xem huy hiệu", first.GetProperty("linkLabel").GetString());
        Assert.Equal(first.GetProperty("timeText").GetString(), first.GetProperty("timeAgo").GetString());
        Assert.False(first.GetProperty("isRead").GetBoolean());
        Assert.EndsWith("Z", first.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", first.GetProperty("createdAtUtc").GetString());

        // An item without a link carries nulls, not an empty string.
        var system = items.EnumerateArray().First(i => i.GetProperty("title").GetString() == "SYS-ONE");
        Assert.Equal(JsonValueKind.Null, system.GetProperty("url").ValueKind);
        Assert.Equal(JsonValueKind.Null, system.GetProperty("relatedUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, system.GetProperty("linkLabel").ValueKind);
    }

    [Fact]
    public async Task Api_List_WithoutACursor_StillPagesByNumber()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        var page2 = (await GetJsonAsync(client, "/api/notifications?page=2&pageSize=10")).GetProperty("data");

        Assert.Equal(2, page2.GetProperty("currentPage").GetInt32());
        Assert.Equal(10, page2.GetProperty("items").GetArrayLength());
        Assert.True(page2.GetProperty("hasMore").GetBoolean());
    }

    [Fact]
    public async Task Api_List_Category_And_UnreadOnly_Filter()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        var badges = (await GetJsonAsync(client, "/api/notifications?category=badge")).GetProperty("data");
        Assert.Equal(1, badges.GetProperty("totalCount").GetInt32());
        Assert.Equal("BADGE-ONE", badges.GetProperty("items")[0].GetProperty("title").GetString());

        var unreadOnly = (await GetJsonAsync(client, "/api/notifications?unreadOnly=true&pageSize=50")).GetProperty("data");
        Assert.Equal(28, unreadOnly.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Api_List_Cursor_WalksEveryNotificationOnce()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        var seen = new List<int>();
        var url = "/api/notifications?pageSize=10";
        for (var guard = 0; guard < 10; guard++)
        {
            var data = (await GetJsonAsync(client, url)).GetProperty("data");
            seen.AddRange(data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));
            if (!data.GetProperty("hasMore").GetBoolean())
            {
                Assert.Equal(JsonValueKind.Null, data.GetProperty("nextCursor").ValueKind);
                break;
            }

            var cursor = data.GetProperty("nextCursor");
            url = $"/api/notifications?pageSize=10&before={Uri.EscapeDataString(cursor.GetProperty("before").GetString()!)}"
                  + $"&beforeId={cursor.GetProperty("beforeId").GetInt32()}";
        }

        Assert.Equal(28, seen.Count);
        Assert.Equal(28, seen.Distinct().Count());
    }

    [Theory]
    [InlineData("/api/notifications?before=not-a-date")]
    [InlineData("/api/notifications?before=2026-11-17T09:00:00Z&beforeId=abc")]
    [InlineData("/api/notifications?category=bogus")]
    public async Task Api_List_BadCursorOrCategory_Is400WithAVietnameseError(string url)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("error").GetString();
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task Api_UnreadCount_KeepsItsShape()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        var json = await GetJsonAsync(client, "/api/notifications/unread-count");

        Assert.Equal(28, json.GetProperty("count").GetInt32());
    }

    // ── Mark read / mark all ───────────────────────────────────────────

    [Fact]
    public async Task MarkRead_ReturnsTheNewUnreadCount()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Notification");
        var first = (await GetJsonAsync(client, "/api/notifications?pageSize=1")).GetProperty("data")
            .GetProperty("items")[0].GetProperty("id").GetInt32();

        using var response = await PostAsync(client, $"/api/notifications/{first}/mark-read", token);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(27, json.GetProperty("unreadCount").GetInt32());
        Assert.Equal(27, await UnreadAsync(factory, Owner));
    }

    [Fact]
    public async Task MarkRead_AnotherUsersNotification_Is404AndChangesNothing()
    {
        await using var factory = await CreateFactoryAsync();
        using var owner = Client(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(owner, "/Notification");
        int otherId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            otherId = await db.Notifications.Where(n => n.UserId == Other).Select(n => n.Id).FirstAsync();
        }

        using var response = await PostAsync(owner, $"/api/notifications/{otherId}/mark-read", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(3, await UnreadAsync(factory, Other));
    }

    [Fact]
    public async Task MarkRead_WithoutTheAntiforgeryToken_Is400()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);
        var id = (await GetJsonAsync(client, "/api/notifications?pageSize=1")).GetProperty("data")
            .GetProperty("items")[0].GetProperty("id").GetInt32();

        using var response = await PostAsync(client, $"/api/notifications/{id}/mark-read", token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(28, await UnreadAsync(factory, Owner));
    }

    [Fact]
    public async Task MarkAll_ReturnsTheUpdatedCount_ForTheOwnerOnly()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Notification");

        using var response = await PostAsync(client, "/api/notifications/mark-all-read", token);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(28, json.GetProperty("updated").GetInt32());
        Assert.Equal(0, json.GetProperty("unreadCount").GetInt32());
        Assert.Equal(0, await UnreadAsync(factory, Owner));
        Assert.Equal(3, await UnreadAsync(factory, Other));

        using var again = await PostAsync(client, "/api/notifications/mark-all-read", token);
        var second = JsonDocument.Parse(await again.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(0, second.GetProperty("updated").GetInt32());
    }

    [Fact]
    public async Task MarkAll_WithACategory_OnlyMarksThatCategory()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Notification");

        using var response = await PostAsync(client, "/api/notifications/mark-all-read?category=badge", token);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.GetProperty("updated").GetInt32());
        Assert.Equal(27, json.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task MarkAll_WithAnUnknownCategory_Is400AndChangesNothing()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Notification");

        using var response = await PostAsync(client, "/api/notifications/mark-all-read?category=bogus", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(28, await UnreadAsync(factory, Owner));
    }

    [Fact]
    public async Task MarkAll_WithoutTheAntiforgeryToken_Is400()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory);

        using var response = await PostAsync(client, "/api/notifications/mark-all-read", token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(28, await UnreadAsync(factory, Owner));
    }
}
