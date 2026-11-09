using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public class PersonalSetsIntegrationTests
{
    private const string PrivateName = "Private personal deck";

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        return factory;
    }

    private static async Task<(int SetId, int CardId)> AddPrivateSetAsync(TestWebApplicationFactory factory, int ownerId = TestDataIds.UserId, string name = PrivateName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var set = new Set { SetName = name, OwnerId = ownerId, Visibility = "Private", CreatedAt = DateTime.UtcNow };
        set.Cards.Add(new Card { Term = "secret-term", Definition = "bí mật" });
        db.Sets.Add(set);
        await db.SaveChangesAsync();
        return (set.SetId, set.Cards.Single().CardId);
    }

    private static HttpClient Client(TestWebApplicationFactory factory, int userId, string role = Roles.Standard)
        => IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, role);

    private static HttpRequestMessage Post(string url, string token, string? json = null, string? form = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = json != null
                ? new StringContent(json, Encoding.UTF8, "application/json")
                : new StringContent(form ?? string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        if (token.Length > 0)
        {
            request.Headers.Add("RequestVerificationToken", token);
        }

        return request;
    }

    // ------------------------------------------------------------------ read pages

    [Fact]
    public async Task Owner_CanOpenEveryPersonalSetPage_AndExportCsv()
    {
        await using var factory = await CreateFactoryAsync();
        var (setId, cardId) = await AddPrivateSetAsync(factory);
        using var client = Client(factory, TestDataIds.UserId);

        foreach (var url in new[]
                 {
                     "/Sets", $"/Sets/{setId}", $"/Sets/{setId}/Settings", $"/Sets/{setId}/Add", $"/Sets/{setId}/Import",
                     $"/Sets/Cards/{cardId}", $"/Sets/Cards/{cardId}/Edit", $"/Home/EditSet/{setId}", "/Home/CreateSet", $"/api/sets/{setId}/cards",
                 })
        {
            using var response = await client.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} -> {(int)response.StatusCode}\n{body[..Math.Min(body.Length, 600)]}");
        }

        using var list = await client.GetAsync("/Sets");
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.Contains(PrivateName, listBody);
        Assert.Contains("Bộ thẻ của tôi", listBody);
        Assert.Contains("class=\"tct-theme tct-page ts-page ts-page--list\"", listBody);

        using var export = await client.GetAsync($"/Sets/{setId}/Export");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.StartsWith("text/csv", export.Content.Headers.ContentType?.ToString());
        var bytes = await export.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.Contains("secret-term", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Outsider_GetsNotFoundForEveryRouteOfAPrivateSet()
    {
        await using var factory = await CreateFactoryAsync();
        var (setId, cardId) = await AddPrivateSetAsync(factory);
        using var client = Client(factory, TestDataIds.OutsiderUserId);

        foreach (var url in new[]
                 {
                     $"/Sets/{setId}", $"/Sets/{setId}/Settings", $"/Sets/{setId}/Add", $"/Sets/{setId}/Import", $"/Sets/{setId}/Export",
                     $"/Sets/Cards/{cardId}", $"/Sets/Cards/{cardId}/Edit", $"/api/sets/{setId}/cards", $"/Home/EditSet/{setId}",
                     $"/Home/Study/{setId}", $"/Home/QuizMode/{setId}", $"/Home/WriteMode/{setId}", $"/Home/MatchingMode/{setId}",
                 })
        {
            using var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{url} -> {(int)response.StatusCode}");
        }

        using var list = await client.GetAsync("/Sets");
        Assert.DoesNotContain(PrivateName, await list.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Viewer_SeesAPublicSetWithoutEditOrSettingsControls_AndOwnerSeesThem()
    {
        await using var factory = await CreateFactoryAsync();
        using var viewer = Client(factory, TestDataIds.OutsiderUserId);
        using var owner = Client(factory, TestDataIds.UserId);

        using var response = await viewer.GetAsync($"/Sets/{TestDataIds.UserSetId}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sprint One User Set", body);
        Assert.DoesNotContain($"/Sets/{TestDataIds.UserSetId}/Settings", body);
        Assert.DoesNotContain($"/Sets/{TestDataIds.UserSetId}/Add", body);
        Assert.DoesNotContain("tsShareModal", body);
        Assert.Contains("Theo học", body);
        Assert.Contains("Sao chép về bộ của tôi", body);

        foreach (var url in new[] { $"/Sets/{TestDataIds.UserSetId}/Settings", $"/Sets/{TestDataIds.UserSetId}/Add", $"/Sets/{TestDataIds.UserSetId}/Import" })
        {
            using var denied = await viewer.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }

        using var ownerResponse = await owner.GetAsync($"/Sets/{TestDataIds.UserSetId}");
        var ownerBody = await ownerResponse.Content.ReadAsStringAsync();
        Assert.Contains($"/Sets/{TestDataIds.UserSetId}/Settings", ownerBody);
        Assert.Contains("tsShareModal", ownerBody);
        Assert.DoesNotContain("Theo học", ownerBody);
    }

    [Fact]
    public async Task ClassMemberAndSubscriber_CanOpenAPrivateSet_ThatTheChatRuleAlsoCovers()
    {
        await using var factory = await CreateFactoryAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            (await db.Sets.SingleAsync(s => s.SetId == TestDataIds.UserSetId)).Visibility = "Private";
            await db.SaveChangesAsync();
        }

        using var member = Client(factory, TestDataIds.MemberUserId);
        foreach (var url in new[] { $"/Sets/{TestDataIds.UserSetId}", $"/Home/Study/{TestDataIds.UserSetId}", $"/Sets/Cards/{TestDataIds.UserCardId}" })
        {
            using var response = await member.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} -> {(int)response.StatusCode}");
        }

        using var outsider = Client(factory, TestDataIds.OutsiderUserId);
        using var denied = await outsider.GetAsync($"/Sets/{TestDataIds.UserSetId}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.SetSubscriptions.Add(new SetSubscription { SetId = TestDataIds.UserSetId, UserId = TestDataIds.OutsiderUserId, SubscribedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var allowed = await outsider.GetAsync($"/Sets/{TestDataIds.UserSetId}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    // ------------------------------------------------------------------ shared page

    [Fact]
    public async Task SharedPage_WorksAnonymously_HidesPostFormsFromThem_AndRespectsTheSlugLifecycle()
    {
        await using var factory = await CreateFactoryAsync();
        var (setId, _) = await AddPrivateSetAsync(factory);
        using var owner = Client(factory, TestDataIds.UserId);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(owner, $"/Sets/{setId}");

        using var enable = Post($"/api/sets/{setId}/share-link", token, "{\"enabled\":true}");
        using var enabled = await owner.SendAsync(enable);
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        var json = JsonDocument.Parse(await enabled.Content.ReadAsStringAsync()).RootElement;
        var slug = json.GetProperty("slug").GetString()!;
        Assert.EndsWith("/s/" + slug, json.GetProperty("url").GetString());

        using var anonymous = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var page = await anonymous.GetAsync("/s/" + slug);
        var body = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains(PrivateName, body);
        Assert.Contains("Đăng nhập để học bộ này", body);
        Assert.DoesNotContain("<form", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lưu vào bộ thẻ của tôi", body);

        using var otherUser = Client(factory, TestDataIds.OutsiderUserId);
        using var asUser = await otherUser.GetAsync("/s/" + slug);
        var userBody = await asUser.Content.ReadAsStringAsync();
        Assert.Contains("Lưu vào bộ thẻ của tôi", userBody);
        Assert.Contains("Sao chép về bộ của tôi", userBody);

        using var ownerView = await owner.GetAsync("/s/" + slug);
        Assert.Equal(HttpStatusCode.Redirect, ownerView.StatusCode);
        Assert.Equal($"/Sets/{setId}", ownerView.Headers.Location?.OriginalString);

        using var bad = await anonymous.GetAsync("/s/NOT-A-SLUG");
        Assert.Equal(HttpStatusCode.NotFound, bad.StatusCode);

        using var regenerate = Post($"/api/sets/{setId}/share-link", token, "{\"enabled\":true,\"regenerate\":true}");
        using var regenerated = await owner.SendAsync(regenerate);
        Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);
        using var oldSlug = await anonymous.GetAsync("/s/" + slug);
        Assert.Equal(HttpStatusCode.NotFound, oldSlug.StatusCode);

        using var outsiderManage = Post($"/api/sets/{setId}/share-link", await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(otherUser, "/Sets"), "{\"enabled\":true}");
        using var outsiderResponse = await otherUser.SendAsync(outsiderManage);
        Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
    }

    [Fact]
    public async Task SubscribeAndClone_ThroughTheSharedPage_Work_AndSubscribeByDisabledSlugIsNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        var (setId, _) = await AddPrivateSetAsync(factory);
        using var owner = Client(factory, TestDataIds.UserId);
        var ownerToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(owner, $"/Sets/{setId}");
        using var enable = Post($"/api/sets/{setId}/share-link", ownerToken, "{\"enabled\":true}");
        var slug = JsonDocument.Parse(await (await owner.SendAsync(enable)).Content.ReadAsStringAsync()).RootElement.GetProperty("slug").GetString()!;

        using var user = Client(factory, TestDataIds.OutsiderUserId);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(user, "/s/" + slug);

        using var follow = Post("/Sets/Subscribe", token, form: $"slug={slug}");
        using var followed = await user.SendAsync(follow);
        Assert.Equal(HttpStatusCode.Redirect, followed.StatusCode);
        Assert.Equal($"/Sets/{setId}", followed.Headers.Location?.OriginalString);

        using var again = Post("/Sets/Subscribe", token, form: $"slug={slug}");
        using var againResponse = await user.SendAsync(again);
        Assert.Equal(HttpStatusCode.Redirect, againResponse.StatusCode);

        using var clone = Post("/Sets/Clone", token, form: $"slug={slug}");
        using var cloned = await user.SendAsync(clone);
        Assert.Equal(HttpStatusCode.Redirect, cloned.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.Equal(1, await db.SetSubscriptions.CountAsync(x => x.UserId == TestDataIds.OutsiderUserId));
            var copy = await db.Sets.AsNoTracking().SingleAsync(s => s.OwnerId == TestDataIds.OutsiderUserId);
            Assert.Equal("Private", copy.Visibility);
            Assert.Equal(setId, copy.ClonedFromSetId);
        }

        using var disable = Post($"/api/sets/{setId}/share-link", ownerToken, "{\"enabled\":false}");
        await owner.SendAsync(disable);
        using var third = Client(factory, TestDataIds.MemberUserId);
        var thirdToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(third, "/Sets");
        using var dead = Post("/Sets/Subscribe", thirdToken, form: $"slug={slug}");
        using var deadResponse = await third.SendAsync(dead);
        Assert.Equal(HttpStatusCode.NotFound, deadResponse.StatusCode);
    }

    // ------------------------------------------------------------------ writes

    [Fact]
    public async Task CreateDeck_CreatesAPrivateSetAndRedirectsToAddWords_AndRejectsForeignFolders()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, TestDataIds.OutsiderUserId);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/CreateSet");

        using var create = Post("/Sets/Create", token, form: "SetName=New+personal+deck&Description=hello&Visibility=Private");
        using var created = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Matches(@"^/Sets/\d+/Add$", created.Headers.Location!.OriginalString);

        using var foreign = Post("/Sets/Create", token, form: $"SetName=Nope&FolderId={TestDataIds.UserFolderId}");
        using var foreignResponse = await client.SendAsync(foreign);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);

        using var blank = Post("/Sets/Create", token, form: "SetName=+++");
        using var blankResponse = await client.SendAsync(blank);
        var blankBody = await blankResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, blankResponse.StatusCode);
        Assert.Contains("Nhập tên bộ thẻ", blankBody);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var set = await db.Sets.AsNoTracking().SingleAsync(s => s.OwnerId == TestDataIds.OutsiderUserId);
        Assert.Equal("Private", set.Visibility);
        Assert.Equal("New personal deck", set.SetName);
    }

    [Fact]
    public async Task LegacyCreateSet_IsPrivate_AndLegacyEditKeepsProgressAndPhonetic()
    {
        await using var factory = await CreateFactoryAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var card = await db.Cards.SingleAsync(c => c.CardId == TestDataIds.UserCardId);
            card.Phonetic = "/ˈfɔːkɑːst/";
            card.Example = "A weather forecast.";
            card.Topic = "Thời tiết";
            db.LearningProgresses.Add(new LearningProgress { UserId = TestDataIds.UserId, CardId = card.CardId, Status = "Mastered", WrongCount = 1, RepetitionCount = 3 });
            await db.SaveChangesAsync();
        }

        using var client = Client(factory, TestDataIds.UserId);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/CreateSet");

        using var create = Post("/Home/CreateSet", token, form: "SetName=Legacy+made&Terms=alpha&Definitions=a");
        using var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        using var edit = Post("/Home/EditSet", token, form: $"SetId={TestDataIds.UserSetId}&SetName=Edited&Terms=forecast&Terms=extra&Definitions=predict+updated&Definitions=more&ExistingImageUrls=&ExistingImageUrls=");
        using var editResponse = await client.SendAsync(edit);
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        using var scope2 = factory.Services.CreateScope();
        var context = scope2.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal("Private", (await context.Sets.AsNoTracking().SingleAsync(s => s.SetName == "Legacy made")).Visibility);
        var forecast = await context.Cards.AsNoTracking().SingleAsync(c => c.CardId == TestDataIds.UserCardId);
        Assert.Equal("predict updated", forecast.Definition);
        Assert.Equal("/ˈfɔːkɑːst/", forecast.Phonetic);
        Assert.Equal("A weather forecast.", forecast.Example);
        Assert.Equal("Thời tiết", forecast.Topic);
        Assert.Equal("Mastered", (await context.LearningProgresses.AsNoTracking().SingleAsync(p => p.CardId == TestDataIds.UserCardId)).Status);
        Assert.Equal(2, await context.Cards.CountAsync(c => c.SetId == TestDataIds.UserSetId));
    }

    [Fact]
    public async Task LegacyDeleteSet_WithLearningProgress_Succeeds()
    {
        await using var factory = await CreateFactoryAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.LearningProgresses.Add(new LearningProgress { UserId = TestDataIds.OutsiderUserId, CardId = TestDataIds.UserCardId, Status = "Learning", WrongCount = 0, RepetitionCount = 0 });
            await db.SaveChangesAsync();
        }

        using var client = Client(factory, TestDataIds.UserId);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/CreateSet");
        using var delete = Post("/Home/DeleteSet", token, form: $"setId={TestDataIds.UserSetId}&folderId={TestDataIds.UserFolderId}");
        using var response = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope2 = factory.Services.CreateScope();
        var context = scope2.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False(await context.Sets.AnyAsync(s => s.SetId == TestDataIds.UserSetId));
        Assert.Empty(await context.LearningProgresses.ToListAsync());
    }

    [Fact]
    public async Task DeleteDeck_RequiresTheExactName_AndRemovesTheSet()
    {
        await using var factory = await CreateFactoryAsync();
        var (setId, _) = await AddPrivateSetAsync(factory);
        using var client = Client(factory, TestDataIds.UserId);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Sets/{setId}/Settings");

        using var wrong = Post($"/Sets/{setId}/Delete", token, form: "confirmName=wrong+name");
        using var wrongResponse = await client.SendAsync(wrong);
        Assert.Equal(HttpStatusCode.Redirect, wrongResponse.StatusCode);
        Assert.Equal($"/Sets/{setId}/Settings", wrongResponse.Headers.Location?.OriginalString);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.True(await db.Sets.AnyAsync(s => s.SetId == setId));

        using var right = Post($"/Sets/{setId}/Delete", token, form: "confirmName=" + Uri.EscapeDataString(PrivateName));
        using var rightResponse = await client.SendAsync(right);
        Assert.Equal("/Sets", rightResponse.Headers.Location?.OriginalString);
        Assert.False(await db.Sets.AsNoTracking().AnyAsync(s => s.SetId == setId));
    }

    [Fact]
    public async Task AddSamples_ThroughTheEmptyState_ClonesSystemSets_AndIsIdempotent()
    {
        await using var factory = await CreateFactoryAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var systemSet = await db.Sets.SingleAsync(s => s.SetId == TestDataIds.SystemSetId);
            for (var i = 0; i < 5; i++)
            {
                db.Cards.Add(new Card { SetId = systemSet.SetId, Term = $"sys{i}", Definition = "d" });
            }

            await db.SaveChangesAsync();
        }

        using var client = Client(factory, TestDataIds.MemberUserId);
        using var empty = await client.GetAsync("/Sets");
        Assert.Contains("Thêm bộ thẻ mẫu", await empty.Content.ReadAsStringAsync());

        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Sets");
        for (var i = 0; i < 2; i++)
        {
            using var post = Post("/Sets/AddSamples", token);
            using var response = await client.SendAsync(post);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        using var scope2 = factory.Services.CreateScope();
        var context = scope2.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var owned = await context.Sets.AsNoTracking().Where(s => s.OwnerId == TestDataIds.MemberUserId).ToListAsync();
        Assert.Single(owned);
        Assert.Equal(6, await context.Cards.CountAsync(c => c.SetId == owned[0].SetId));
    }

    // ------------------------------------------------------------------ JSON API

    [Fact]
    public async Task Api_BatchBulkSuspendDuplicatesAndSearch_RoundTripThroughHttp()
    {
        await using var factory = await CreateFactoryAsync();
        var (setId, cardId) = await AddPrivateSetAsync(factory);
        using var client = Client(factory, TestDataIds.UserId);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Sets/{setId}/Add");

        using var batch = Post($"/api/sets/{setId}/cards/batch", token,
            "{\"cards\":[{\"term\":\"alpha\",\"definition\":\"a\",\"partsOfSpeech\":[\"noun\"],\"examples\":[{\"text\":\"An alpha.\",\"vi\":\"Một alpha.\"}]},{\"term\":\"secret-term\",\"definition\":\"dup\"},{\"term\":\"\",\"definition\":\"x\"}]}");
        using var batchResponse = await client.SendAsync(batch);
        var batchJson = JsonDocument.Parse(await batchResponse.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode);
        Assert.Equal("alpha", batchJson.GetProperty("inserted")[0].GetProperty("term").GetString());
        Assert.Equal("secret-term", batchJson.GetProperty("skipped")[0].GetString());
        Assert.Equal(1, batchJson.GetProperty("invalid").GetArrayLength());
        var alphaId = batchJson.GetProperty("inserted")[0].GetProperty("id").GetInt32();

        using var search = await client.GetAsync($"/api/sets/{setId}/cards?q=alp&pos=noun&sort=az");
        var searchJson = JsonDocument.Parse(await search.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1, searchJson.GetProperty("total").GetInt32());
        Assert.Equal("alpha", searchJson.GetProperty("rows")[0].GetProperty("term").GetString());
        Assert.Equal("new", searchJson.GetProperty("rows")[0].GetProperty("status").GetProperty("key").GetString());

        using var suspend = Post($"/api/sets/cards/{alphaId}/suspend", token, "{\"suspended\":true}");
        using var suspendResponse = await client.SendAsync(suspend);
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);
        using var suspended = await client.GetAsync($"/api/sets/{setId}/cards?status=suspended");
        Assert.Equal(1, JsonDocument.Parse(await suspended.Content.ReadAsStringAsync()).RootElement.GetProperty("total").GetInt32());

        using var dupes = await client.GetAsync("/api/sets/duplicates?term=ALPHA");
        Assert.Equal(1, JsonDocument.Parse(await dupes.Content.ReadAsStringAsync()).RootElement.GetArrayLength());

        using var bulk = Post($"/api/sets/{setId}/cards/bulk", token, $"{{\"action\":\"topic\",\"cardIds\":[{alphaId},{cardId}],\"topic\":\"Khác\"}}");
        using var bulkResponse = await client.SendAsync(bulk);
        Assert.Equal(2, JsonDocument.Parse(await bulkResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("count").GetInt32());

        using var none = Post($"/api/sets/{setId}/cards/bulk", token, "{\"action\":\"delete\",\"cardIds\":[999999]}");
        using var noneResponse = await client.SendAsync(none);
        Assert.Equal(HttpStatusCode.NotFound, noneResponse.StatusCode);
        Assert.Contains("Không có từ nào được cập nhật.", await noneResponse.Content.ReadAsStringAsync());

        using var outsider = Client(factory, TestDataIds.OutsiderUserId);
        var outsiderToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(outsider, "/Sets");
        foreach (var (url, json) in new[]
                 {
                     ($"/api/sets/{setId}/cards/batch", "{\"cards\":[{\"term\":\"x\",\"definition\":\"y\"}]}"),
                     ($"/api/sets/{setId}/cards/bulk", "{\"action\":\"delete\",\"cardIds\":[1]}"),
                     ($"/api/sets/cards/{cardId}/suspend", "{\"suspended\":true}"),
                 })
        {
            using var request = Post(url, outsiderToken, json);
            using var response = await outsider.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{url} -> {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task EveryNewPostWithoutAnAntiforgeryToken_Returns400_AndLeavesDataUnchanged()
    {
        await using var factory = await CreateFactoryAsync();
        var (setId, cardId) = await AddPrivateSetAsync(factory);
        using var client = Client(factory, TestDataIds.UserId);

        var posts = new (string Url, string? Json, string? Form)[]
        {
            ("/Sets/Create", null, "SetName=blocked"),
            ($"/Sets/{setId}/Settings", null, "SetName=blocked"),
            ($"/Sets/{setId}/Delete", null, "confirmName=" + PrivateName),
            ($"/Sets/Cards/{cardId}/Edit", null, "Term=blocked&Definition=x"),
            ($"/Sets/Cards/{cardId}/Delete", null, ""),
            ("/Sets/Subscribe", null, $"setId={TestDataIds.SystemSetId}"),
            ($"/Sets/{TestDataIds.SystemSetId}/Unsubscribe", null, ""),
            ("/Sets/Clone", null, $"setId={TestDataIds.SystemSetId}"),
            ("/Sets/AddSamples", null, ""),
            ($"/api/sets/{setId}/cards/batch", "{\"cards\":[{\"term\":\"blocked\",\"definition\":\"x\"}]}", null),
            ($"/api/sets/{setId}/cards/bulk", "{\"action\":\"delete\",\"cardIds\":[" + cardId + "]}", null),
            ($"/api/sets/cards/{cardId}/suspend", "{\"suspended\":true}", null),
            ($"/api/sets/{setId}/share-link", "{\"enabled\":true}", null),
            ("/api/vocab/enrich", "{\"term\":\"blocked\"}", null),
        };

        foreach (var (url, json, form) in posts)
        {
            using var request = Post(url, string.Empty, json, form);
            using var response = await client.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{url} -> {(int)response.StatusCode}");
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var set = await db.Sets.AsNoTracking().SingleAsync(s => s.SetId == setId);
        Assert.Equal(PrivateName, set.SetName);
        Assert.Null(set.ShareSlug);
        Assert.Equal(1, await db.Cards.CountAsync(c => c.SetId == setId));
        Assert.False(await db.Cards.AnyAsync(c => c.Term == "blocked"));
        Assert.Empty(await db.SetSubscriptions.ToListAsync());
        Assert.False(await db.LearningProgresses.AnyAsync());
    }

    [Fact]
    public async Task CardImageUpload_RejectsNonImages()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, TestDataIds.UserId);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Sets");

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("<html>not an image</html>")), "file", "evil.png");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sets/card-image") { Content = content };
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("error", await response.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------------------ study touch points

    [Fact]
    public async Task UpdateCardProgress_RejectsMissingAndUnviewableCards_AndKeepsTheContract()
    {
        await using var factory = await CreateFactoryAsync();
        var (_, privateCardId) = await AddPrivateSetAsync(factory);
        using var client = Client(factory, TestDataIds.OutsiderUserId);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/Study/{TestDataIds.UserSetId}");

        foreach (var cardId in new[] { privateCardId, 987654 })
        {
            using var request = Post("/Home/UpdateCardProgress", token, $"{{\"cardId\":{cardId},\"isKnown\":true}}");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var ok = Post("/Home/UpdateCardProgress", token, $"{{\"cardId\":{TestDataIds.UserCardId},\"isKnown\":true}}");
        using var okResponse = await client.SendAsync(ok);
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
        Assert.Contains("\"success\":true", await okResponse.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(new[] { TestDataIds.UserCardId }, await db.LearningProgresses.Select(p => p.CardId).ToArrayAsync());
    }

    [Fact]
    public async Task StudyDueMode_ShowsDueAndNewCards_AndRedirectsWithAMessageWhenNothingIsDue()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, TestDataIds.UserId);

        using var due = await client.GetAsync($"/Home/Study/{TestDataIds.UserSetId}?mode=due");
        var dueBody = await due.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, due.StatusCode);
        Assert.Contains("Ôn đến hạn", dueBody);
        Assert.Contains("forecast", dueBody);
        Assert.Contains($"/Sets/{TestDataIds.UserSetId}", dueBody);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.LearningProgresses.Add(new LearningProgress
            {
                UserId = TestDataIds.UserId, CardId = TestDataIds.UserCardId, Status = "Mastered", WrongCount = 0, RepetitionCount = 1,
                NextReviewDate = DateTime.UtcNow.AddDays(5),
            });
            await db.SaveChangesAsync();
        }

        using var none = await client.GetAsync($"/Home/Study/{TestDataIds.UserSetId}?mode=due");
        Assert.Equal(HttpStatusCode.Redirect, none.StatusCode);
        Assert.Equal($"/Sets/{TestDataIds.UserSetId}", none.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task StudyOfAnEmptySetWithoutAFolder_RedirectsToTheSetInsteadOfFolderDetailZero()
    {
        await using var factory = await CreateFactoryAsync();
        int setId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var set = new Set { SetName = "Empty", OwnerId = TestDataIds.UserId, Visibility = "Private", CreatedAt = DateTime.UtcNow };
            db.Sets.Add(set);
            await db.SaveChangesAsync();
            setId = set.SetId;
        }

        using var client = Client(factory, TestDataIds.UserId);
        using var response = await client.GetAsync($"/Home/Study/{setId}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Sets/{setId}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task FolderDetail_HidesPrivateSetsFromViewers_ButNotFromOwnerOrSavedFolderFollowers()
    {
        await using var factory = await CreateFactoryAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.Sets.Add(new Set { SetName = "Hidden private deck", OwnerId = TestDataIds.UserId, FolderId = TestDataIds.UserFolderId, Visibility = "Private", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var owner = Client(factory, TestDataIds.UserId);
        using var ownerPage = await owner.GetAsync($"/Home/FolderDetail/{TestDataIds.UserFolderId}");
        Assert.Contains("Hidden private deck", await ownerPage.Content.ReadAsStringAsync());

        using var stranger = Client(factory, TestDataIds.OutsiderUserId);
        using var strangerPage = await stranger.GetAsync($"/Home/FolderDetail/{TestDataIds.UserFolderId}");
        var strangerBody = await strangerPage.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Hidden private deck", strangerBody);
        Assert.Contains("Sprint One User Set", strangerBody);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.SavedFolders.Add(new SavedFolder { UserId = TestDataIds.OutsiderUserId, FolderId = TestDataIds.UserFolderId });
            await db.SaveChangesAsync();
        }

        using var follower = await stranger.GetAsync($"/Home/FolderDetail/{TestDataIds.UserFolderId}");
        Assert.Contains("Hidden private deck", await follower.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------------------ account deletion and seeded content

    [Fact]
    public async Task AccountDeletion_WithSubscriptionsOnBothSides_Succeeds()
    {
        await using var factory = await CreateFactoryAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            // Someone follows the owner's set, and the owner follows someone else's set.
            db.SetSubscriptions.Add(new SetSubscription { SetId = TestDataIds.UserSetId, UserId = TestDataIds.OutsiderUserId, SubscribedAt = DateTime.UtcNow });
            var theirs = new Set { SetName = "Theirs", OwnerId = TestDataIds.OutsiderUserId, Visibility = "Public", CreatedAt = DateTime.UtcNow };
            db.Sets.Add(theirs);
            await db.SaveChangesAsync();
            db.SetSubscriptions.Add(new SetSubscription { SetId = theirs.SetId, UserId = TestDataIds.UserId, SubscribedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var client = Client(factory, TestDataIds.UserId);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Account/Settings");
        using var response = await client.PostAsync("/Account/DeleteAccount", new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("__RequestVerificationToken", token)]));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope2 = factory.Services.CreateScope();
        var context = scope2.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False(await context.Users.AnyAsync(u => u.UserId == TestDataIds.UserId));
        Assert.Empty(await context.SetSubscriptions.ToListAsync());
        Assert.True(await context.Sets.AnyAsync(s => s.SetName == "Theirs"));
    }

    [Fact]
    public async Task SeededAndSystemSets_StayPublicAndStudyable()
    {
        await using var factory = await CreateFactoryAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.All(await db.Sets.AsNoTracking().ToListAsync(), s => Assert.Equal("Public", s.Visibility));

        using var stranger = Client(factory, TestDataIds.OutsiderUserId);
        using var response = await stranger.GetAsync($"/Home/Study/{TestDataIds.SystemSetId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
