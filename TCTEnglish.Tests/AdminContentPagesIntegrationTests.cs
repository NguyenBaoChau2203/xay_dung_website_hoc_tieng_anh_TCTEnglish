using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.Admin;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>Host-level checks for the admin content modules (vocabulary sets, listening lessons, payments, notifications).</summary>
public sealed class AdminContentPagesIntegrationTests
{
    private static readonly string[] VocabularyPages =
    {
        "/Admin/SetManagement",
        "/Admin/SetManagement?ownerFilter=system",
        "/Admin/SetManagement?ownerFilter=user&visibility=Private&sort=cards",
        "/Admin/SetManagement/Create",
        "/Admin/SetManagement/Edit/302",
        "/Admin/SetManagement/Cards/302",
        "/Admin/SetManagement/Cards/301",
        "/Admin/SetManagement/AddCard?setId=302",
        "/Admin/SetManagement/EditCard/402",
    };

    private static string Decoded(string html) => WebUtility.HtmlDecode(html);

    /// <summary>The page body between the admin layout's main landmark and its end (skips the sidebar).</summary>
    private static string PageBody(string html)
    {
        var start = html.IndexOf("class=\"ac-page", StringComparison.Ordinal);
        Assert.True(start >= 0, "the page does not use the ac-page root");
        var end = html.IndexOf("</main>", start, StringComparison.Ordinal);
        return html[start..(end > 0 ? end : html.Length)];
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string tokenRoute, string url, params (string Key, string Value)[] fields)
    {
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, tokenRoute);
        var form = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        form.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));
        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task TheAdminServices_AreRegisteredInTheHost()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();

        Assert.IsType<AdminVocabularyService>(scope.ServiceProvider.GetRequiredService<IAdminVocabularyService>());
        Assert.IsType<AdminListeningService>(scope.ServiceProvider.GetRequiredService<IAdminListeningService>());
    }

    [Fact]
    public async Task VocabularyPages_RenderForAnAdmin_AndAreClosedToOthers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var standard = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var anonymous = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        foreach (var route in VocabularyPages)
        {
            var response = await admin.GetAsync(route);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route} answered {(int)response.StatusCode}");
            var html = await response.Content.ReadAsStringAsync();
            var body = PageBody(html);

            Assert.DoesNotContain("bi bi-", body);
            Assert.DoesNotContain("style=\"", body);
            Assert.Contains("href=\"/css/admin-content.css", html);
            Assert.Contains("/js/admin-content.js", html);

            Assert.Equal(HttpStatusCode.Forbidden, (await standard.GetAsync(route)).StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, (await anonymous.GetAsync(route)).StatusCode);
        }
    }

    [Fact]
    public async Task VocabularyList_ShowsVietnameseTitlesFiguresAndTheRightActionsPerSource()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var html = Decoded(await admin.GetStringAsync("/Admin/SetManagement"));
        var body = PageBody(html);

        Assert.Contains("Quản lý bộ từ vựng", body);
        Assert.Contains("Tạo bộ từ vựng hệ thống", body);
        Assert.Contains("Bộ từ vựng hệ thống", body);
        Assert.Contains("Thẻ thiếu ví dụ/IPA", body);
        Assert.Contains("Danh sách bộ từ vựng", body);
        Assert.Contains("TCT English (hệ thống)", body);
        // A system row can be edited; a learner row can be made private but not edited.
        Assert.Contains("/Admin/SetManagement/Edit/302", body);
        Assert.DoesNotContain("/Admin/SetManagement/Edit/301", body);
        Assert.Contains("Cards/301", body);
        // Every delete asks for the exact name.
        Assert.Contains("data-ac-confirm-name=\"", body);
        Assert.DoesNotContain("onsubmit=", body);
        Assert.Matches(new Regex("Hiển thị 1–\\d+ trong \\d+"), body);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("999")]
    public async Task VocabularyList_ClampsThePageInsteadOfFailing(string page)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var response = await admin.GetAsync($"/Admin/SetManagement?page={page}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hiển thị 1–", Decoded(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task VocabularyPosts_NeedAnAntiforgeryToken()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        foreach (var (url, fields) in new (string, KeyValuePair<string, string>[])[]
        {
            ("/Admin/SetManagement/Create", new[] { new KeyValuePair<string, string>("Name", "Bộ lậu") }),
            ("/Admin/SetManagement/Edit", new[] { new KeyValuePair<string, string>("SetId", "302"), new KeyValuePair<string, string>("Name", "x") }),
            ("/Admin/SetManagement/Delete", new[] { new KeyValuePair<string, string>("id", "302"), new KeyValuePair<string, string>("confirmName", "x") }),
            ("/Admin/SetManagement/SetVisibility", new[] { new KeyValuePair<string, string>("id", "301"), new KeyValuePair<string, string>("visibility", "Private") }),
            ("/Admin/SetManagement/DeleteCard", new[] { new KeyValuePair<string, string>("cardId", "402") }),
            ("/Admin/SetManagement/AddCard", new[] { new KeyValuePair<string, string>("setId", "302"), new KeyValuePair<string, string>("Term", "x"), new KeyValuePair<string, string>("Definition", "y") }),
            ("/Admin/SetManagement/EditCard", new[] { new KeyValuePair<string, string>("cardId", "402"), new KeyValuePair<string, string>("Term", "x"), new KeyValuePair<string, string>("Definition", "y") }),
        })
        {
            var response = await admin.PostAsync(url, new FormUrlEncodedContent(fields));
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{url} answered {(int)response.StatusCode}");
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.True(await db.Sets.AnyAsync(s => s.SetId == TestDataIds.SystemSetId));
        Assert.False(await db.Sets.AnyAsync(s => s.SetName == "Bộ lậu"));
    }

    [Fact]
    public async Task CreatingASystemSet_MakesItSystemOwnedAndPublic_AndAsksAgainOnAnError()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var created = await PostFormAsync(admin, "/Admin/SetManagement/Create", "/Admin/SetManagement/Create",
            ("Name", "[Thử nghiệm] Bộ mới"), ("Description", "mô tả"), ("FolderId", TestDataIds.SystemFolderId.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Contains("/Admin/SetManagement/Cards/", created.Headers.Location!.OriginalString);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var set = await db.Sets.Include(s => s.Owner).SingleAsync(s => s.SetName == "[Thử nghiệm] Bộ mới");
            Assert.Equal("system@tct.local", set.Owner.Email);
            Assert.Equal("Public", set.Visibility);
            Assert.Equal(TestDataIds.SystemFolderId, set.FolderId);
        }

        // Same name again, and a folder that belongs to a learner (S1): the form comes back with messages and the typed values.
        var duplicate = await PostFormAsync(admin, "/Admin/SetManagement/Create", "/Admin/SetManagement/Create", ("Name", "[Thử nghiệm] Bộ mới"));
        var duplicateHtml = Decoded(await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("Đã có bộ từ vựng hệ thống trùng tên này.", duplicateHtml);
        Assert.Contains("value=\"[Thử nghiệm] Bộ mới\"", duplicateHtml);

        var foreign = await PostFormAsync(admin, "/Admin/SetManagement/Create", "/Admin/SetManagement/Create",
            ("Name", "Bộ khác"), ("FolderId", TestDataIds.UserFolderId.ToString()));
        var foreignHtml = Decoded(await foreign.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, foreign.StatusCode);
        Assert.Contains(AdminVocabularyMessages.FolderNotOwned, foreignHtml);

        var blank = await PostFormAsync(admin, "/Admin/SetManagement/Create", "/Admin/SetManagement/Create", ("Name", "  "));
        Assert.Contains("Hãy nhập tên bộ từ vựng.", Decoded(await blank.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task EditingASystemSet_KeepsTheName_AndLearnerSetsCannotBeEdited()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var editPage = Decoded(await admin.GetStringAsync("/Admin/SetManagement/Edit/302"));
        Assert.Contains("readonly", PageBody(editPage));
        Assert.Contains("tệp dữ liệu đồng bộ theo tên", editPage);

        var rename = await PostFormAsync(admin, "/Admin/SetManagement/Edit/302", "/Admin/SetManagement/Edit",
            ("SetId", "302"), ("Name", "Tên khác"), ("Description", "mô tả"));
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Contains(AdminVocabularyMessages.SystemRenameBlocked, Decoded(await rename.Content.ReadAsStringAsync()));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var set = await db.Sets.AsNoTracking().SingleAsync(s => s.SetId == TestDataIds.SystemSetId);
            Assert.NotEqual("Tên khác", set.SetName);
        }

        var learnerEdit = await admin.GetAsync("/Admin/SetManagement/Edit/301");
        Assert.Equal(HttpStatusCode.Redirect, learnerEdit.StatusCode);
        Assert.Contains("/Admin/SetManagement/Cards/301", learnerEdit.Headers.Location!.OriginalString);

        var learnerPost = await PostFormAsync(admin, "/Admin/SetManagement/Create", "/Admin/SetManagement/Edit", ("SetId", "301"), ("Name", "Đổi tên"));
        Assert.Equal(HttpStatusCode.Redirect, learnerPost.StatusCode);
        Assert.Contains("/Cards/301", learnerPost.Headers.Location!.OriginalString);
        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.NotEqual("Đổi tên", (await verifyDb.Sets.AsNoTracking().SingleAsync(s => s.SetId == TestDataIds.UserSetId)).SetName);
    }

    [Fact]
    public async Task TheCardsPage_ExplainsTheSeederForSystemSets_AndIsReadOnlyForLearnerSets()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var system = PageBody(Decoded(await admin.GetStringAsync("/Admin/SetManagement/Cards/302")));
        Assert.Contains("Bộ hệ thống được đồng bộ từ tệp dữ liệu.", system);
        Assert.Contains("system-vocabulary.json", system);
        Assert.Contains("Tải JSON", system);
        Assert.Contains("Thêm thẻ", system);
        Assert.Contains("Xem như học viên", system);
        Assert.Contains("Tổng số thẻ", system);
        Assert.Contains("/Admin/SetManagement/EditCard/402", system);

        var learner = PageBody(Decoded(await admin.GetStringAsync("/Admin/SetManagement/Cards/301")));
        Assert.Contains("Admin chỉ xem", learner);
        Assert.DoesNotContain("Tải JSON", learner);
        Assert.DoesNotContain("/Admin/SetManagement/EditCard/", learner);
        Assert.DoesNotContain("AddCard", learner);

        var learnerAdd = await admin.GetAsync($"/Admin/SetManagement/AddCard?setId={TestDataIds.UserSetId}");
        Assert.Equal(HttpStatusCode.Redirect, learnerAdd.StatusCode);
        var learnerEditCard = await admin.GetAsync($"/Admin/SetManagement/EditCard/{TestDataIds.UserCardId}");
        Assert.Equal(HttpStatusCode.Redirect, learnerEditCard.StatusCode);
    }

    [Fact]
    public async Task CardForms_ValidateInVietnamese_AndRedirectToTheCardsRealSet()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var bad = await PostFormAsync(admin, "/Admin/SetManagement/AddCard?setId=302", "/Admin/SetManagement/AddCard",
            ("setId", "302"), ("Term", ""), ("Definition", "nghĩa"), ("ImageUrl", "http://evil.example/a.png"), ("Example", "Vẫn giữ ví dụ này"));
        var badHtml = Decoded(await bad.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("Hãy nhập từ.", badHtml);
        Assert.Contains("Ảnh phải là liên kết https", badHtml);
        Assert.Contains("Vẫn giữ ví dụ này", badHtml);

        var good = await PostFormAsync(admin, "/Admin/SetManagement/AddCard?setId=302", "/Admin/SetManagement/AddCard",
            ("setId", "302"), ("Term", "Zebra"), ("Definition", "Ngựa vằn"), ("Emoji", "🦓"), ("Cefr", "A2"), ("PartsOfSpeech", "noun"));
        Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);
        Assert.Contains("/Cards/302", good.Headers.Location!.OriginalString);

        // EditCard ignores any posted set id and goes back to the card's own set (S7).
        var edit = await PostFormAsync(admin, "/Admin/SetManagement/EditCard/402", "/Admin/SetManagement/EditCard",
            ("cardId", TestDataIds.SystemCardId.ToString()), ("setId", "301"), ("Term", "Edited"), ("Definition", "Đã sửa"), ("Emoji", "✏️"), ("PhoneticUk", "/ɛd/"));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.Contains("/Cards/302", edit.Headers.Location!.OriginalString);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var zebra = await db.Cards.AsNoTracking().SingleAsync(c => c.Term == "Zebra");
        Assert.Equal(TestDataIds.SystemSetId, zebra.SetId);
        Assert.Equal("🦓", zebra.Emoji);
        var edited = await db.Cards.AsNoTracking().SingleAsync(c => c.CardId == TestDataIds.SystemCardId);
        Assert.Equal("Edited", edited.Term);
        Assert.Equal("✏️", edited.Emoji);
        Assert.Equal("/ɛd/", edited.PhoneticUk);
    }

    [Fact]
    public async Task DeletingASet_NeedsTheExactName_AndLearnerSetsCanBeMadePrivate()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        string setName;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            setName = (await db.Sets.AsNoTracking().SingleAsync(s => s.SetId == TestDataIds.UserSetId)).SetName;
            var learner = await db.Sets.SingleAsync(s => s.SetId == TestDataIds.UserSetId);
            learner.Visibility = "Public";
            learner.ShareSlug = "abcdefghij";
            await db.SaveChangesAsync();
        }

        var priv = await PostFormAsync(admin, "/Admin/SetManagement", "/Admin/SetManagement/SetVisibility", ("id", "301"), ("visibility", "Private"));
        Assert.Equal(HttpStatusCode.Redirect, priv.StatusCode);

        var wrong = await PostFormAsync(admin, "/Admin/SetManagement", "/Admin/SetManagement/Delete", ("id", "301"), ("confirmName", "sai tên"));
        Assert.Equal(HttpStatusCode.Redirect, wrong.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var set = await db.Sets.AsNoTracking().SingleAsync(s => s.SetId == TestDataIds.UserSetId);
            Assert.Equal("Private", set.Visibility);
            Assert.Null(set.ShareSlug);
        }

        var right = await PostFormAsync(admin, "/Admin/SetManagement", "/Admin/SetManagement/Delete", ("id", "301"), ("confirmName", setName));
        Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.False(await db.Sets.AnyAsync(s => s.SetId == TestDataIds.UserSetId));
            Assert.False(await db.Cards.AnyAsync(c => c.SetId == TestDataIds.UserSetId));
        }
    }

    [Fact]
    public async Task ExportingASystemSet_DownloadsTheSeederShape_AndIsNotForLearnerSets()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var response = await admin.GetAsync("/Admin/SetManagement/Export/302");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition!.ToString());
        Assert.StartsWith("system-vocabulary-", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("Sets", doc.RootElement[0].EnumerateObject().Last().Name);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/Admin/SetManagement/Export/301")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/Admin/SetManagement/Export/9999")).StatusCode);
    }

    [Fact]
    public async Task MissingSetsAndCards_AnswerNotFound()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        foreach (var route in new[] { "/Admin/SetManagement/Edit/9999", "/Admin/SetManagement/Cards/9999", "/Admin/SetManagement/AddCard?setId=9999", "/Admin/SetManagement/EditCard/9999" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(route)).StatusCode);
        }
    }
}
