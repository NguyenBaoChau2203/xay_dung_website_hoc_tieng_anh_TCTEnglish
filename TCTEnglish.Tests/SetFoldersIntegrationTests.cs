using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>Folder endpoints of the personal-set workspace: create, page, rename, delete, move, unsave and the legacy create.</summary>
public sealed class SetFoldersIntegrationTests
{
    private static HttpClient ClientFor(TestWebApplicationFactory factory, int userId, string role = Roles.Standard)
        => IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, role);

    // A page that always renders an antiforgery token for a signed-in user (the helper follows one redirect).
    private static Task<string> TokenAsync(HttpClient client) => IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Folder");

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string route, string form, string? token, bool json = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        if (token != null)
        {
            request.Headers.Add("RequestVerificationToken", token);
        }

        if (json)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        return await client.SendAsync(request);
    }

    private static async Task<T> DbAsync<T>(TestWebApplicationFactory factory, Func<DbflashcardContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<DbflashcardContext>());
    }

    private static string Location(HttpResponseMessage response) => response.Headers.Location?.OriginalString ?? string.Empty;

    private static async Task<TestWebApplicationFactory> NewFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        return factory;
    }

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task Create_StoresTheFolderForTheCallerAndRedirectsToItsPage()
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);

        using var response = await PostAsync(client, "/Sets/Folders/Create", "name=" + Uri.EscapeDataString("  Công việc  "), await TokenAsync(client));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var match = Regex.Match(Location(response), "^/Sets/Folders/(\\d+)$");
        Assert.True(match.Success, "unexpected redirect: " + Location(response));
        var folder = await DbAsync(factory, db => db.Folders.AsNoTracking().SingleAsync(f => f.FolderId == int.Parse(match.Groups[1].Value)));
        Assert.Equal("Công việc", folder.FolderName);
        Assert.Equal(TestDataIds.UserId, folder.UserId);
    }

    [Theory]
    [InlineData("", null, "/Sets")]
    [InlineData("   ", "/Sets/Folders/201", "/Sets/Folders/201")]
    [InlineData("", "https://evil.example/steal", "/Sets")]
    [InlineData("", "//evil.example/steal", "/Sets")]
    public async Task Create_WithAnInvalidNameGoesBackToALocalReturnUrlOnlyAndCreatesNothing(string name, string? returnUrl, string expected)
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);
        var before = await DbAsync(factory, db => db.Folders.CountAsync());
        var form = "name=" + Uri.EscapeDataString(name) + (returnUrl == null ? string.Empty : "&returnUrl=" + Uri.EscapeDataString(returnUrl));

        using var response = await PostAsync(client, "/Sets/Folders/Create", form, await TokenAsync(client));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, Location(response));
        Assert.Equal(before, await DbAsync(factory, db => db.Folders.CountAsync()));
    }

    [Fact]
    public async Task Create_JsonVariantAnswersWithTheNewUrlOrTheError()
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);
        var token = await TokenAsync(client);

        using var created = await PostAsync(client, "/Sets/Folders/Create", "name=Du+l%E1%BB%8Bch", token, json: true);
        using var duplicate = await PostAsync(client, "/Sets/Folders/Create", "name=" + Uri.EscapeDataString("DU LỊCH"), token, json: true);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var ok = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.True(ok.RootElement.GetProperty("ok").GetBoolean());
        var id = ok.RootElement.GetProperty("id").GetInt32();
        Assert.Equal($"/Sets/Folders/{id}", ok.RootElement.GetProperty("url").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using var bad = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.False(bad.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("Bạn đã có thư mục tên này.", bad.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task EveryFolderPostRejectsAMissingAntiforgeryToken()
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);
        var before = await DbAsync(factory, db => db.Folders.CountAsync());

        foreach (var (route, form) in new[]
                 {
                     ("/Sets/Folders/Create", "name=X"),
                     ($"/Sets/Folders/{TestDataIds.UserFolderId}/Rename", "name=X"),
                     ($"/Sets/Folders/{TestDataIds.DeletableUserFolderId}/Delete", string.Empty),
                     ($"/Sets/{TestDataIds.UserSetId}/MoveToFolder", "folderId="),
                     ($"/Sets/Folders/{TestDataIds.UserFolderId}/Unsave", string.Empty),
                     ("/Home/CreateFolder", "folderName=X"),
                 })
        {
            using var response = await PostAsync(client, route, form, token: null);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{route} answered {(int)response.StatusCode} without a token");
        }

        Assert.Equal(before, await DbAsync(factory, db => db.Folders.CountAsync()));
    }

    // ------------------------------------------------------------------ page

    [Fact]
    public async Task Page_OpensForTheOwnerAndAClassMemberButNotForAStranger()
    {
        await using var factory = await NewFactoryAsync();
        using var owner = ClientFor(factory, TestDataIds.UserId);
        using var member = ClientFor(factory, TestDataIds.MemberUserId);
        using var stranger = ClientFor(factory, TestDataIds.OutsiderUserId);
        var route = $"/Sets/Folders/{TestDataIds.UserFolderId}";

        using var ownerResponse = await owner.GetAsync(route);
        using var memberResponse = await member.GetAsync(route);
        using var strangerResponse = await stranger.GetAsync(route);
        using var missing = await owner.GetAsync("/Sets/Folders/987654");

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Contains("Sprint One Folder", System.Net.WebUtility.HtmlDecode(await ownerResponse.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, memberResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, strangerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Page_OpensForASavedFollower()
    {
        await using var factory = await NewFactoryAsync();
        await DbAsync(factory, async db =>
        {
            db.SavedFolders.Add(new SavedFolder { UserId = TestDataIds.OutsiderUserId, FolderId = TestDataIds.UserFolderId });
            await db.SaveChangesAsync();
            return 0;
        });
        using var follower = ClientFor(factory, TestDataIds.OutsiderUserId);

        using var response = await follower.GetAsync($"/Sets/Folders/{TestDataIds.UserFolderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------ rename

    [Fact]
    public async Task Rename_ChangesTheNameForTheOwnerAndIsNotFoundForAStranger()
    {
        await using var factory = await NewFactoryAsync();
        using var owner = ClientFor(factory, TestDataIds.UserId);
        using var stranger = ClientFor(factory, TestDataIds.OutsiderUserId);
        var route = $"/Sets/Folders/{TestDataIds.UserFolderId}/Rename";

        using var denied = await PostAsync(stranger, route, "name=Hacked", await TokenAsync(stranger));
        using var done = await PostAsync(owner, route, "name=" + Uri.EscapeDataString("Tên mới"), await TokenAsync(owner));

        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.Equal($"/Sets/Folders/{TestDataIds.UserFolderId}", Location(done));
        Assert.Equal("Tên mới", (await DbAsync(factory, db => db.Folders.AsNoTracking().SingleAsync(f => f.FolderId == TestDataIds.UserFolderId))).FolderName);
    }

    [Fact]
    public async Task Rename_ToADuplicateKeepsTheNameAndExplainsInHtmlOrJson()
    {
        await using var factory = await NewFactoryAsync();
        using var owner = ClientFor(factory, TestDataIds.UserId);
        var token = await TokenAsync(owner);
        var route = $"/Sets/Folders/{TestDataIds.UserFolderId}/Rename";
        var duplicate = "name=" + Uri.EscapeDataString("  sprint one delete folder ");

        using var html = await PostAsync(owner, route, duplicate, token);
        using var json = await PostAsync(owner, route, duplicate, token, json: true);

        Assert.Equal(HttpStatusCode.Redirect, html.StatusCode);
        Assert.Equal($"/Sets/Folders/{TestDataIds.UserFolderId}", Location(html));
        Assert.Equal(HttpStatusCode.BadRequest, json.StatusCode);
        Assert.Equal("Sprint One Folder", (await DbAsync(factory, db => db.Folders.AsNoTracking().SingleAsync(f => f.FolderId == TestDataIds.UserFolderId))).FolderName);
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Delete_RemovesTheFolderButKeepsItsSetsForTheOwnerAndIsNotFoundForAStranger()
    {
        await using var factory = await NewFactoryAsync();
        using var owner = ClientFor(factory, TestDataIds.UserId);
        using var stranger = ClientFor(factory, TestDataIds.OutsiderUserId);
        var route = $"/Sets/Folders/{TestDataIds.UserFolderId}/Delete";

        using var denied = await PostAsync(stranger, route, string.Empty, await TokenAsync(stranger));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.True(await DbAsync(factory, db => db.Folders.AnyAsync(f => f.FolderId == TestDataIds.UserFolderId)));

        using var done = await PostAsync(owner, route, string.Empty, await TokenAsync(owner));

        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.Equal("/Sets", Location(done));
        Assert.False(await DbAsync(factory, db => db.Folders.AnyAsync(f => f.FolderId == TestDataIds.UserFolderId)));
        var set = await DbAsync(factory, db => db.Sets.AsNoTracking().SingleAsync(s => s.SetId == TestDataIds.UserSetId));
        Assert.Null(set.FolderId);
        Assert.False(await DbAsync(factory, db => db.ClassFolders.AnyAsync(l => l.FolderId == TestDataIds.UserFolderId)));
    }

    [Fact]
    public async Task Delete_OfAFolderWithChildFoldersAnswers409ForJsonAndKeepsEverything()
    {
        await using var factory = await NewFactoryAsync();
        await DbAsync(factory, async db =>
        {
            db.Folders.Add(new Folder { FolderName = "Con", UserId = TestDataIds.UserId, ParentFolderId = TestDataIds.UserFolderId });
            await db.SaveChangesAsync();
            return 0;
        });
        using var owner = ClientFor(factory, TestDataIds.UserId);
        var token = await TokenAsync(owner);
        var route = $"/Sets/Folders/{TestDataIds.UserFolderId}/Delete";

        using var json = await PostAsync(owner, route, string.Empty, token, json: true);
        using var html = await PostAsync(owner, route, string.Empty, token);

        Assert.Equal(HttpStatusCode.Conflict, json.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, html.StatusCode);
        Assert.Equal($"/Sets/Folders/{TestDataIds.UserFolderId}", Location(html));
        Assert.True(await DbAsync(factory, db => db.Folders.AnyAsync(f => f.FolderId == TestDataIds.UserFolderId)));
    }

    // ------------------------------------------------------------------ move

    [Fact]
    public async Task Move_PutsTheSetIntoAnotherFolderAndTakesItOut()
    {
        await using var factory = await NewFactoryAsync();
        using var owner = ClientFor(factory, TestDataIds.UserId);
        var token = await TokenAsync(owner);
        var route = $"/Sets/{TestDataIds.UserSetId}/MoveToFolder";

        using var into = await PostAsync(owner, route, $"folderId={TestDataIds.DeletableUserFolderId}", token);
        Assert.Equal(HttpStatusCode.Redirect, into.StatusCode);
        Assert.Equal("/Sets", Location(into));
        Assert.Equal(TestDataIds.DeletableUserFolderId, (await DbAsync(factory, db => db.Sets.AsNoTracking().SingleAsync(s => s.SetId == TestDataIds.UserSetId))).FolderId);

        using var outOf = await PostAsync(owner, route, "folderId=&returnUrl=" + Uri.EscapeDataString("/Sets/Folders/" + TestDataIds.DeletableUserFolderId), token);
        Assert.Equal(HttpStatusCode.Redirect, outOf.StatusCode);
        Assert.Equal($"/Sets/Folders/{TestDataIds.DeletableUserFolderId}", Location(outOf));
        Assert.Null((await DbAsync(factory, db => db.Sets.AsNoTracking().SingleAsync(s => s.SetId == TestDataIds.UserSetId))).FolderId);
    }

    [Fact]
    public async Task Move_IgnoresANonLocalReturnUrlAndRefusesForeignSetsAndFolders()
    {
        await using var factory = await NewFactoryAsync();
        using var owner = ClientFor(factory, TestDataIds.UserId);
        using var stranger = ClientFor(factory, TestDataIds.OutsiderUserId);

        using var evil = await PostAsync(owner, $"/Sets/{TestDataIds.UserSetId}/MoveToFolder", $"folderId={TestDataIds.DeletableUserFolderId}&returnUrl=" + Uri.EscapeDataString("https://evil.example"), await TokenAsync(owner));
        Assert.Equal("/Sets", Location(evil));

        using var foreignSet = await PostAsync(stranger, $"/Sets/{TestDataIds.UserSetId}/MoveToFolder", "folderId=", await TokenAsync(stranger));
        using var foreignFolder = await PostAsync(owner, $"/Sets/{TestDataIds.UserSetId}/MoveToFolder", $"folderId={TestDataIds.SystemFolderId}", await TokenAsync(owner));

        Assert.Equal(HttpStatusCode.NotFound, foreignSet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignFolder.StatusCode);
    }

    // ------------------------------------------------------------------ unsave

    [Fact]
    public async Task Unsave_RemovesTheCallersSaveOnce()
    {
        await using var factory = await NewFactoryAsync();
        await DbAsync(factory, async db =>
        {
            db.SavedFolders.Add(new SavedFolder { UserId = TestDataIds.OutsiderUserId, FolderId = TestDataIds.UserFolderId });
            await db.SaveChangesAsync();
            return 0;
        });
        using var follower = ClientFor(factory, TestDataIds.OutsiderUserId);
        var token = await TokenAsync(follower);
        var route = $"/Sets/Folders/{TestDataIds.UserFolderId}/Unsave";

        using var first = await PostAsync(follower, route, string.Empty, token);
        using var second = await PostAsync(follower, route, string.Empty, token);

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal("/Sets", Location(first));
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.False(await DbAsync(factory, db => db.SavedFolders.AnyAsync(s => s.FolderId == TestDataIds.UserFolderId)));
    }

    // ------------------------------------------------------------------ sidebar and dialog (shared layout)

    private static async Task<string> PageAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sidebar_KeepsOnlyCreateFolderAndMySetsUnderFolders()
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);

        var html = await PageAsync(client, "/Sets");
        var menu = Regex.Match(html, "<div class=\"nav-dropdown-menu\" id=\"folderDropdownMenu\">.*?</div>", RegexOptions.Singleline).Value;

        Assert.NotEmpty(menu);
        Assert.Contains("Tạo thư mục mới", menu, StringComparison.Ordinal);
        Assert.Contains("Bộ thẻ của tôi", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("Thư mục của tôi", html, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(menu, "class=\"nav-sub-item").Count);
        Assert.Contains("id=\"newFolderBtn\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/Home/Folder\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Sets", true)]
    [InlineData("/Sets/Folders/201", true)]
    [InlineData("/Home/Index", false)]
    public async Task Sidebar_FolderGroupIsActiveOnlyOnTheSetAndFolderPages(string route, bool active)
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);

        var html = await PageAsync(client, route);

        Assert.Equal(active, Regex.IsMatch(html, "class=\"nav-dropdown-item active\"[^>]*id=\"folderDropdownBtn\""));
    }

    [Fact]
    public async Task Dialog_IsVietnameseAndPostsToTheServiceWithATokenAndTheCurrentPath()
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);

        var html = await PageAsync(client, "/Sets?filter=own");
        var dialog = Regex.Match(html, "<dialog id=\"folderModal\".*?</dialog>", RegexOptions.Singleline).Value;

        Assert.NotEmpty(dialog);
        foreach (var text in new[] { "Tạo thư mục mới", "Dùng thư mục để gom các bộ thẻ cùng chủ đề. Thư mục hiện ngay trong Bộ thẻ của tôi.", "Tên thư mục", "Hủy", "Tạo thư mục", "0/100" })
        {
            Assert.Contains(text, dialog, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Create new folder", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Decline", html, StringComparison.Ordinal);
        var formTag = Regex.Match(dialog, "<form[^>]*>").Value;
        Assert.Contains("action=\"/Sets/Folders/Create\"", formTag, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", formTag, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", dialog, StringComparison.Ordinal);
        Assert.Matches("name=\"name\"[^>]*maxlength=\"100\"|maxlength=\"100\"[^>]*name=\"name\"", dialog);
        Assert.Contains("name=\"returnUrl\" value=\"/Sets?filter=own\"", dialog, StringComparison.Ordinal);
        Assert.Contains("id=\"cancelFolderBtn\"", dialog, StringComparison.Ordinal);
        Assert.Contains("aria-labelledby=\"folderDialogTitle\"", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("data-open-on-load", dialog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dialog_ReopensOnceWithTheMessageAndTheTypedNameAfterAFailedCreate()
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);
        var token = await TokenAsync(client);

        using var empty = await PostAsync(client, "/Sets/Folders/Create", "name=+&returnUrl=%2FSets", token);
        var afterEmpty = await PageAsync(client, "/Sets");
        Assert.Equal(HttpStatusCode.Redirect, empty.StatusCode);
        Assert.Contains("data-open-on-load", afterEmpty, StringComparison.Ordinal);
        Assert.Contains("Hãy nhập tên thư mục.", afterEmpty, StringComparison.Ordinal);

        using var duplicate = await PostAsync(client, "/Sets/Folders/Create", "name=sprint+one+folder&returnUrl=%2FSets", token);
        var afterDuplicate = await PageAsync(client, "/Sets");
        Assert.Contains("data-open-on-load", afterDuplicate, StringComparison.Ordinal);
        Assert.Contains("Bạn đã có thư mục tên này.", afterDuplicate, StringComparison.Ordinal);
        Assert.Contains("value=\"sprint one folder\"", afterDuplicate, StringComparison.Ordinal);
        Assert.Contains("17/100", afterDuplicate, StringComparison.Ordinal);

        var later = await PageAsync(client, "/Sets");
        Assert.False(later.Contains("data-open-on-load", StringComparison.Ordinal), Regex.Match(later, ".{0,120}data-open-on-load.{0,120}", RegexOptions.Singleline).Value);
        Assert.DoesNotContain("Bạn đã có thư mục tên này.", later, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pages_AlwaysCarryAnAntiforgeryTokenSoTheLegacyFolderTestsKeepWorking()
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);

        foreach (var route in new[] { "/Sets", "/Home/Index", $"/Sets/Folders/{TestDataIds.UserFolderId}" })
        {
            var html = await PageAsync(client, route);
            Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task LegacyFolderList_RedirectsToSetsWithARelativeLocation()
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);

        using var response = await client.GetAsync("/Home/Folder");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(response.Headers.Location!.IsAbsoluteUri);
        Assert.Equal("/Sets", Location(response));
    }

    // ------------------------------------------------------------------ legacy create

    [Fact]
    public async Task LegacyCreateFolder_UsesTheServiceAndLandsOnTheNewFolderPage()
    {
        await using var factory = await NewFactoryAsync();
        using var client = ClientFor(factory, TestDataIds.UserId);
        var token = await TokenAsync(client);

        using var created = await PostAsync(client, "/Home/CreateFolder", "folderName=" + Uri.EscapeDataString("  Từ hộp thoại cũ "), token);
        using var empty = await PostAsync(client, "/Home/CreateFolder", "folderName=+", token);
        using var duplicate = await PostAsync(client, "/Home/CreateFolder", "folderName=" + Uri.EscapeDataString("TỪ HỘP THOẠI CŨ"), token);

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var match = Regex.Match(Location(created), "^/Sets/Folders/(\\d+)$");
        Assert.True(match.Success, "unexpected redirect: " + Location(created));
        Assert.Equal("Từ hộp thoại cũ", (await DbAsync(factory, db => db.Folders.AsNoTracking().SingleAsync(f => f.FolderId == int.Parse(match.Groups[1].Value)))).FolderName);
        Assert.Equal("/Sets", Location(empty));
        Assert.Equal("/Sets", Location(duplicate));
        Assert.Equal(1, await DbAsync(factory, db => db.Folders.CountAsync(f => f.UserId == TestDataIds.UserId && f.FolderName == "Từ hộp thoại cũ")));
    }
}
