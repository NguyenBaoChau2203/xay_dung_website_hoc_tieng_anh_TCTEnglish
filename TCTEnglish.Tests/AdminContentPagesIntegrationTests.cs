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

    // ==================================================================================
    // Listening lessons
    // ==================================================================================

    private const int FullLessonId = 9101, DraftLessonId = 9102, LearnerLessonId = 9103;

    private static async Task SeedListeningAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var full = new ListeningLesson { Id = FullLessonId, Title = "Bài đầy đủ", Level = "B1", Topic = "Du lịch", YoutubeId = "dQw4w9WgXcQ", IsPublished = true, CreatedAt = DateTime.UtcNow };
        full.TranscriptLines.Add(new ListeningTranscriptLine { OrderIndex = 1, Speaker = "Anna", Text = "Hello there", StartTime = 0, EndTime = 2 });
        full.TranscriptLines.Add(new ListeningTranscriptLine { OrderIndex = 2, Speaker = "Ben", Text = "Hi Anna", StartTime = 2, EndTime = 4 });
        full.QuizQuestions.Add(new ListeningQuizQuestion { OrderIndex = 1, QuestionText = "Ai nói trước?", OptionA = "Anna", OptionB = "Ben", CorrectAnswer = "A" });
        full.VocabItems.Add(new ListeningVocabItem { OrderIndex = 1, Word = "luggage", Definition = "hành lý" });

        var draft = new ListeningLesson { Id = DraftLessonId, Title = "Bài nháp", Level = "A2", Topic = "Công việc", IsPublished = false, CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
        var learner = new ListeningLesson { Id = LearnerLessonId, Title = "Bài riêng", Level = "B2", Topic = "Giáo dục", YoutubeId = "aaaaaaaaaaa", IsPublished = true, OwnerUserId = TestDataIds.UserId, CreatedAt = DateTime.UtcNow.AddMinutes(-10) };
        learner.TranscriptLines.Add(new ListeningTranscriptLine { OrderIndex = 1, Speaker = "A", Text = "Private line" });
        learner.QuizQuestions.Add(new ListeningQuizQuestion { OrderIndex = 1, QuestionText = "Private?", OptionA = "yes", OptionB = "no", CorrectAnswer = "A" });

        db.ListeningLessons.AddRange(full, draft, learner);
        await db.SaveChangesAsync();
    }

    private static readonly string[] ListeningPages =
    {
        "/Admin/ListeningManagement",
        "/Admin/ListeningManagement?source=all&status=incomplete&level=B1",
        "/Admin/ListeningManagement?source=learner",
        "/Admin/ListeningManagement/Create",
        "/Admin/ListeningManagement/Editor/9101",
        "/Admin/ListeningManagement/Editor/9101?tab=transcript",
        "/Admin/ListeningManagement/Editor/9101?tab=quiz",
        "/Admin/ListeningManagement/Editor/9101?tab=vocab",
        "/Admin/ListeningManagement/Editor/9102?tab=transcript",
        "/Admin/ListeningManagement/Editor/9103",
        "/Admin/ListeningManagement/Editor/9103?tab=quiz",
    };

    [Fact]
    public async Task ListeningPages_RenderForAnAdmin_AndAreClosedToOthers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var standard = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var anonymous = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        foreach (var route in ListeningPages)
        {
            var response = await admin.GetAsync(route);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route} answered {(int)response.StatusCode}");
            var html = await response.Content.ReadAsStringAsync();
            var body = PageBody(html);

            Assert.DoesNotContain("bi bi-", body);
            Assert.DoesNotContain("style=\"", body);
            Assert.DoesNotContain("onsubmit=", body);
            Assert.Contains("href=\"/css/admin-content.css", html);
            Assert.Equal(HttpStatusCode.Forbidden, (await standard.GetAsync(route)).StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, (await anonymous.GetAsync(route)).StatusCode);
        }

        Assert.Contains("/js/admin-listening-editor.js", await admin.GetStringAsync("/Admin/ListeningManagement/Editor/9101"));
    }

    [Fact]
    public async Task ListeningList_ShowsSystemLessonsByDefault_WithVietnameseTitlesFiguresAndCounts()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var body = PageBody(Decoded(await admin.GetStringAsync("/Admin/ListeningManagement")));

        Assert.Contains("Quản lý bài nghe", body);
        Assert.Contains("Thêm bài nghe", body);
        Assert.Contains("Bài hệ thống", body);
        Assert.Contains("Bản nháp / thiếu nội dung", body);
        Assert.Contains("Bài riêng của học viên", body);
        Assert.Contains("Danh sách bài nghe", body);
        Assert.Contains("Bài đầy đủ", body);
        Assert.Contains("Bài nháp", body);
        Assert.DoesNotContain("Bài riêng</", body);      // the learner lesson is not in the default list
        Assert.Contains("2 dòng", body);
        Assert.Contains("Có mốc thời gian", body);
        Assert.Contains("1 câu", body);
        Assert.Contains("1 từ", body);
        Assert.Contains("data-ac-confirm-name=\"Bài đầy đủ\"", body);
        Assert.Matches(new Regex("Hiển thị 1–2 trong 2"), body);

        var learner = PageBody(Decoded(await admin.GetStringAsync("/Admin/ListeningManagement?source=learner")));
        Assert.Contains("Bài riêng", learner);
        Assert.DoesNotContain("Bài đầy đủ", learner);
        Assert.Contains("Bài riêng của người dùng", learner);

        var incomplete = PageBody(Decoded(await admin.GetStringAsync("/Admin/ListeningManagement?status=incomplete")));
        Assert.Contains("Bài nháp", incomplete);
        Assert.DoesNotContain("Bài đầy đủ", incomplete);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("999")]
    public async Task ListeningList_ClampsThePageInsteadOfFailing(string page)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var response = await admin.GetAsync($"/Admin/ListeningManagement?page={page}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hiển thị 1–", Decoded(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task TheOldListeningRoutes_RedirectToTheEditorTabs()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        foreach (var (route, tab) in new[]
        {
            ("/Admin/ListeningManagement/Edit/9101", "info"),
            ("/Admin/ListeningManagement/ManageTranscript/9101", "transcript"),
            ("/Admin/ListeningManagement/ManageQuiz/9101", "quiz"),
            ("/Admin/ListeningManagement/ManageVocab/9101", "vocab"),
        })
        {
            var response = await admin.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location!.OriginalString;
            Assert.Contains("/Admin/ListeningManagement/Editor/9101", location);
            Assert.Contains($"tab={tab}", location);
        }

        foreach (var route in new[] { "/Admin/ListeningManagement/Editor/9999", "/Admin/ListeningManagement/Editor/9999?tab=quiz" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(route)).StatusCode);
        }
    }

    [Fact]
    public async Task ListeningPosts_NeedAnAntiforgeryToken()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        foreach (var url in new[]
        {
            "/Admin/ListeningManagement/CreateEdit", "/Admin/ListeningManagement/ManageTranscript/9101", "/Admin/ListeningManagement/ManageQuiz/9101",
            "/Admin/ListeningManagement/ManageVocab/9101", "/Admin/ListeningManagement/ImportTranscript/9101", "/Admin/ListeningManagement/Publish/9102",
            "/Admin/ListeningManagement/Unpublish/9101", "/Admin/ListeningManagement/Delete/9101",
        })
        {
            var response = await admin.PostAsync(url, new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("confirmName", "Bài đầy đủ") }));
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{url} answered {(int)response.StatusCode}");
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.True(await db.ListeningLessons.AnyAsync(l => l.Id == FullLessonId && l.IsPublished));
        Assert.False(await db.ListeningLessons.AnyAsync(l => l.Id == DraftLessonId && l.IsPublished));
    }

    [Fact]
    public async Task CreatingALesson_MakesADraft_AndShowsMessagesInVietnameseOnAnError()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var bad = await PostFormAsync(admin, "/Admin/ListeningManagement/Create", "/Admin/ListeningManagement/CreateEdit",
            ("id", "0"), ("Title", "Bài lỗi"), ("Level", "D1"), ("Topic", ""), ("YoutubeId", "không phải video"), ("Duration", "4 phút"));
        var badHtml = Decoded(await bad.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains(AdminListeningMessages.YoutubeInvalid, badHtml);
        Assert.Contains(AdminListeningMessages.DurationInvalid, badHtml);
        Assert.Contains("Trình độ phải là A1, A2, B1, B2, C1 hoặc C2.", badHtml);
        Assert.Contains("value=\"Bài lỗi\"", badHtml);
        Assert.Contains("value=\"không phải video\"", badHtml);

        var publishNow = await PostFormAsync(admin, "/Admin/ListeningManagement/Create", "/Admin/ListeningManagement/CreateEdit",
            ("id", "0"), ("Title", "Bài mới"), ("Level", "B1"), ("Topic", "Du lịch"), ("PublishNow", "true"));
        Assert.Contains(AdminListeningMessages.NoTranscriptToPublish, Decoded(await publishNow.Content.ReadAsStringAsync()));

        var good = await PostFormAsync(admin, "/Admin/ListeningManagement/Create", "/Admin/ListeningManagement/CreateEdit",
            ("id", "0"), ("Title", "[Thử nghiệm] Bài nghe"), ("Level", "B1"), ("Topic", "Du lịch"), ("YoutubeId", "https://youtu.be/dQw4w9WgXcQ"), ("Duration", "4:12"));
        Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);
        Assert.Contains("tab=transcript", good.Headers.Location!.OriginalString);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var lesson = await db.ListeningLessons.AsNoTracking().SingleAsync(l => l.Title == "[Thử nghiệm] Bài nghe");
        Assert.False(lesson.IsPublished);
        Assert.Null(lesson.OwnerUserId);
        Assert.Equal("dQw4w9WgXcQ", lesson.YoutubeId);
        Assert.Equal("04:12", lesson.Duration);
        Assert.False(await db.ListeningLessons.AnyAsync(l => l.Title == "Bài lỗi" || l.Title == "Bài mới"));
    }

    [Fact]
    public async Task TheTranscriptTab_ParsesClockTimes_AndKeepsTypedRowsWithPerRowMessages()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        const string tab = "/Admin/ListeningManagement/Editor/9102?tab=transcript";

        var bad = await PostFormAsync(admin, tab, "/Admin/ListeningManagement/ManageTranscript/9102",
            ("lines[0].Speaker", "Anna"), ("lines[0].Text", "Hello"), ("lines[0].Start", "0:00.0"), ("lines[0].End", "0:02.0"),
            ("lines[1].Text", "Dòng hai gõ dở"), ("lines[1].Start", "0:05.0"), ("lines[1].End", "0:04.0"),
            ("lines[2].Text", "Dòng ba"), ("lines[2].Start", "1:75"), ("lines[2].End", "2:00"));
        var badHtml = Decoded(await bad.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("Dòng 2: thời điểm kết thúc phải sau thời điểm bắt đầu.", badHtml);
        Assert.Contains("Dòng 3: thời điểm bắt đầu không hợp lệ.", badHtml);
        Assert.Contains("Dòng hai gõ dở", badHtml);
        Assert.Contains("value=\"1:75\"", badHtml);

        var good = await PostFormAsync(admin, tab, "/Admin/ListeningManagement/ManageTranscript/9102",
            ("lines[0].Speaker", "Anna"), ("lines[0].Text", "Hello"), ("lines[0].Start", "0:00.0"), ("lines[0].End", "0:02.0"),
            ("lines[1].Text", "How are you?"), ("lines[1].Start", "1:05.5"), ("lines[1].End", "1:08"),
            ("lines[2].Text", "No timing"));
        Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);
        Assert.Contains("tab=transcript", good.Headers.Location!.OriginalString);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var lines = await db.ListeningTranscriptLines.AsNoTracking().Where(l => l.LessonId == DraftLessonId).OrderBy(l => l.OrderIndex).ToListAsync();
        Assert.Equal(3, lines.Count);
        Assert.Equal(65.5, lines[1].StartTime);
        Assert.Equal(68.0, lines[1].EndTime);
        Assert.Null(lines[2].StartTime);
        Assert.Equal("Speaker 1", lines[1].Speaker);

        // The saved times come back in the clock form.
        var again = Decoded(await admin.GetStringAsync(tab));
        Assert.Contains("value=\"1:05.5\"", again);
        Assert.Contains("value=\"1:08.0\"", again);
    }

    [Fact]
    public async Task TheQuizTab_ReportsAnEmptyCorrectOption_WithoutAnError500_AndKeepsTheTypedValues()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        const string tab = "/Admin/ListeningManagement/Editor/9101?tab=quiz";

        var bad = await PostFormAsync(admin, tab, "/Admin/ListeningManagement/ManageQuiz/9101",
            ("questions[0].QuestionText", "Ai nói trước?"), ("questions[0].OptionA", "Anna"), ("questions[0].OptionB", "Ben"), ("questions[0].CorrectAnswer", "A"),
            ("questions[1].QuestionText", "Nơi nào?"), ("questions[1].OptionA", "Quán cà phê"), ("questions[1].OptionB", "Ngân hàng"), ("questions[1].OptionC", ""), ("questions[1].CorrectAnswer", "C"),
            ("questions[2].QuestionText", "Thiếu phương án"), ("questions[2].OptionA", ""), ("questions[2].OptionB", "b"), ("questions[2].CorrectAnswer", "B"));
        var badHtml = Decoded(await bad.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("Câu 2: đáp án đúng C đang để trống.", badHtml);
        Assert.Contains("Câu 3: phương án A không được để trống.", badHtml);
        Assert.Contains("Quán cà phê", badHtml);
        Assert.Contains("checked", Regex.Match(badHtml, "<input[^>]*name=\"questions\\[1\\]\\.CorrectAnswer\"[^>]*value=\"C\"[^>]*>").Value);

        var good = await PostFormAsync(admin, tab, "/Admin/ListeningManagement/ManageQuiz/9101",
            ("questions[0].QuestionText", "Ai nói trước?"), ("questions[0].OptionA", "Anna"), ("questions[0].OptionB", "Ben"), ("questions[0].CorrectAnswer", "B"),
            ("questions[1].QuestionText", "Nơi nào?"), ("questions[1].OptionA", "Quán cà phê"), ("questions[1].OptionB", "Ngân hàng"), ("questions[1].OptionC", "Khách sạn"), ("questions[1].CorrectAnswer", "C"));
        Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var questions = await db.ListeningQuizQuestions.AsNoTracking().Where(q => q.LessonId == FullLessonId).OrderBy(q => q.OrderIndex).ToListAsync();
        Assert.Equal(new[] { "B", "C" }, questions.Select(q => q.CorrectAnswer));
    }

    [Fact]
    public async Task TheVocabularyTab_ReportsAnEmptyDefinition_WithoutAnError500()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        const string tab = "/Admin/ListeningManagement/Editor/9101?tab=vocab";

        var bad = await PostFormAsync(admin, tab, "/Admin/ListeningManagement/ManageVocab/9101",
            ("items[0].Word", "luggage"), ("items[0].Definition", "hành lý"),
            ("items[1].Word", "ticket"), ("items[1].Definition", ""), ("items[1].ImageUrl", "http://evil.example/a.png"));
        var badHtml = Decoded(await bad.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("Từ 2: hãy nhập nghĩa.", badHtml);
        Assert.Contains("Từ 2: ảnh phải là liên kết https", badHtml);
        Assert.Contains("value=\"ticket\"", badHtml);

        var good = await PostFormAsync(admin, tab, "/Admin/ListeningManagement/ManageVocab/9101",
            ("items[0].Word", "luggage"), ("items[0].Definition", "hành lý"), ("items[1].Word", "ticket"), ("items[1].Definition", "vé"));
        Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(2, await db.ListeningVocabItems.CountAsync(v => v.LessonId == FullLessonId));
    }

    [Fact]
    public async Task Publishing_IsBlockedWithoutATranscript_AndWorksWithOne()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var blocked = await PostFormAsync(admin, "/Admin/ListeningManagement/Editor/9102", "/Admin/ListeningManagement/Publish/9102");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        var page = Decoded(await admin.GetStringAsync("/Admin/ListeningManagement/Editor/9102"));
        Assert.Contains(AdminListeningMessages.NoTranscriptToPublish, page);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.False((await db.ListeningLessons.AsNoTracking().SingleAsync(l => l.Id == DraftLessonId)).IsPublished);
        }

        await PostFormAsync(admin, "/Admin/ListeningManagement/Editor/9102?tab=transcript", "/Admin/ListeningManagement/ManageTranscript/9102", ("lines[0].Text", "Hello"));
        var published = await PostFormAsync(admin, "/Admin/ListeningManagement/Editor/9102", "/Admin/ListeningManagement/Publish/9102");
        Assert.Equal(HttpStatusCode.Redirect, published.StatusCode);
        Assert.Contains(AdminListeningMessages.NoQuizWarning, Decoded(await admin.GetStringAsync("/Admin/ListeningManagement/Editor/9102")));

        var unpublished = await PostFormAsync(admin, "/Admin/ListeningManagement/Editor/9102", "/Admin/ListeningManagement/Unpublish/9102");
        Assert.Equal(HttpStatusCode.Redirect, unpublished.StatusCode);

        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False((await verifyDb.ListeningLessons.AsNoTracking().SingleAsync(l => l.Id == DraftLessonId)).IsPublished);
    }

    [Fact]
    public async Task ALearnersLesson_IsReadOnlyInTheEditor_ButCanBeDeletedWithItsTitle()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var page = Decoded(await admin.GetStringAsync("/Admin/ListeningManagement/Editor/9103"));
        var body = PageBody(page);
        Assert.Contains("Bài nghe riêng của", body);
        Assert.Contains("Admin chỉ xem và có thể xóa.", body);
        Assert.Contains("<fieldset class=\"ac-fieldset\" disabled", body);
        Assert.DoesNotContain("Lưu thông tin", body);
        Assert.DoesNotContain("Xuất bản<", body);

        var quizTab = PageBody(Decoded(await admin.GetStringAsync("/Admin/ListeningManagement/Editor/9103?tab=quiz")));
        Assert.DoesNotContain("Lưu quiz", quizTab);
        Assert.DoesNotContain("Thêm câu hỏi", quizTab);

        var edit = await PostFormAsync(admin, "/Admin/ListeningManagement/Editor/9103", "/Admin/ListeningManagement/ManageQuiz/9103",
            ("questions[0].QuestionText", "Bị sửa?"), ("questions[0].OptionA", "a"), ("questions[0].OptionB", "b"), ("questions[0].CorrectAnswer", "A"));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        var publish = await PostFormAsync(admin, "/Admin/ListeningManagement/Editor/9103", "/Admin/ListeningManagement/Publish/9103");
        Assert.Equal(HttpStatusCode.Redirect, publish.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.Equal("Private?", (await db.ListeningQuizQuestions.AsNoTracking().SingleAsync(q => q.LessonId == LearnerLessonId)).QuestionText);
        }

        var wrong = await PostFormAsync(admin, "/Admin/ListeningManagement", "/Admin/ListeningManagement/Delete/9103", ("confirmName", "sai tên"));
        Assert.Equal(HttpStatusCode.Redirect, wrong.StatusCode);
        var right = await PostFormAsync(admin, "/Admin/ListeningManagement", "/Admin/ListeningManagement/Delete/9103", ("confirmName", "Bài riêng"));
        Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);

        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False(await verifyDb.ListeningLessons.AnyAsync(l => l.Id == LearnerLessonId));
        Assert.True(await verifyDb.ListeningLessons.AnyAsync(l => l.Id == FullLessonId));
    }

    [Fact]
    public async Task TheImport_NeedsAConfirmedReplacement_AndAYoutubeId_WithoutCallingYoutube()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var unconfirmed = await PostFormAsync(admin, "/Admin/ListeningManagement/Editor/9101?tab=transcript", "/Admin/ListeningManagement/ImportTranscript/9101", ("replaceExisting", "false"));
        Assert.Equal(HttpStatusCode.Redirect, unconfirmed.StatusCode);
        var withLines = Decoded(await admin.GetStringAsync("/Admin/ListeningManagement/Editor/9101?tab=transcript"));
        Assert.Contains(AdminListeningMessages.ImportReplaceNeeded(2), withLines);
        Assert.Contains("data-ac-import-replace=\"2\"", withLines);

        var noVideo = await PostFormAsync(admin, "/Admin/ListeningManagement/Editor/9102?tab=transcript", "/Admin/ListeningManagement/ImportTranscript/9102", ("replaceExisting", "true"));
        Assert.Equal(HttpStatusCode.Redirect, noVideo.StatusCode);
        Assert.Contains(AdminListeningMessages.ImportNeedsYoutube, Decoded(await admin.GetStringAsync("/Admin/ListeningManagement/Editor/9102?tab=transcript")));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(2, await db.ListeningTranscriptLines.CountAsync(l => l.LessonId == FullLessonId));
    }

    [Fact]
    public async Task DeletingALesson_NeedsTheExactTitle_AndRemovesItsProgress()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.UserListeningProgresses.Add(new UserListeningProgress { UserId = TestDataIds.UserId, LessonId = FullLessonId, QuizScore = 90 });
            await db.SaveChangesAsync();
        }

        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        var dialog = Decoded(await admin.GetStringAsync("/Admin/ListeningManagement/Editor/9101"));
        Assert.Contains("1 học viên đã học bài này", dialog);

        var wrong = await PostFormAsync(admin, "/Admin/ListeningManagement", "/Admin/ListeningManagement/Delete/9101", ("confirmName", "bài đầy đủ"));
        Assert.Equal(HttpStatusCode.Redirect, wrong.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.True(await db.ListeningLessons.AnyAsync(l => l.Id == FullLessonId));
        }

        var right = await PostFormAsync(admin, "/Admin/ListeningManagement", "/Admin/ListeningManagement/Delete/9101", ("confirmName", "Bài đầy đủ"));
        Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);

        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False(await verifyDb.ListeningLessons.AnyAsync(l => l.Id == FullLessonId));
        Assert.False(await verifyDb.UserListeningProgresses.AnyAsync(p => p.LessonId == FullLessonId));
        Assert.False(await verifyDb.ListeningQuizQuestions.AnyAsync(q => q.LessonId == FullLessonId));
    }

    // ==================================================================================
    // Payments (presentation only: figures, range filter, copy, money format)
    // ==================================================================================

    private const long RecentPaidId = 9201, OldPaidId = 9202, PendingFreshId = 9203, PendingOverdueId = 9204, ManualReviewId = 9205, FailedId = 9206;

    private static async Task SeedBillingAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var now = DateTime.UtcNow;

        var plan = new TCTEnglish.Models.PremiumPlan { Code = "admin-ui-plan", Name = "Gói thử nghiệm", Description = "d", PriceVnd = 399000, DurationDays = 30, IsActive = true, CreatedAtUtc = now };
        db.PremiumPlans.Add(plan);
        await db.SaveChangesAsync();

        TCTEnglish.Models.PaymentOrder Order(long id, string code, string status, decimal amount, DateTime created, DateTime expires, DateTime? paid = null, string provider = "vnpay") => new()
        {
            Id = id, OrderCode = code, UserId = TestDataIds.UserId, PlanId = plan.Id, Provider = provider, AmountVnd = amount, Status = status,
            CreatedAtUtc = created, UpdatedAtUtc = created, ExpiresAtUtc = expires, PaidAtUtc = paid,
        };

        db.PaymentOrders.AddRange(
            Order(RecentPaidId, "ORD-RECENT", "paid", 399000, now.AddDays(-2), now.AddDays(-2).AddHours(1), now.AddDays(-2)),
            Order(OldPaidId, "ORD-OLD", "paid", 1000000, now.AddDays(-40), now.AddDays(-40).AddHours(1), now.AddDays(-40), "momo"),
            Order(PendingFreshId, "ORD-FRESH", "pending", 399000, now.AddMinutes(-5), now.AddMinutes(25)),
            Order(PendingOverdueId, "ORD-OVERDUE", "pending", 399000, now.AddDays(-3), now.AddDays(-2)),
            Order(ManualReviewId, "ORD-REVIEW", "manual_review", 399000, now.AddDays(-20), now.AddDays(-20).AddHours(1)),
            Order(FailedId, "ORD-FAILED", "failed", 399000, now.AddDays(-60), now.AddDays(-60).AddHours(1)));

        db.UserSubscriptions.Add(new TCTEnglish.Models.UserSubscription
        {
            UserId = TestDataIds.UserId, PlanId = plan.Id, Status = "active", StartsAtUtc = now.AddDays(-2), EndsAtUtc = now.AddDays(28), CreatedAtUtc = now.AddDays(-2),
            ActivatedByPaymentOrderId = RecentPaidId,
        });
        await db.SaveChangesAsync();
    }

    private static readonly string[] BillingPages =
    {
        "/Admin/BillingManagement",
        "/Admin/BillingManagement?range=7",
        "/Admin/BillingManagement?status=manual_review",
        "/Admin/BillingManagement/Details/9201",
        "/Admin/BillingManagement/Details/9205",
        "/Admin/BillingManagement/Subscriptions",
        "/Admin/BillingManagement/PremiumUsers",
        "/Admin/BillingManagement/PremiumUserDetails?userId=101",
        "/Admin/BillingManagement/Health",
    };

    [Fact]
    public async Task BillingPages_UseTheKit_AreVietnamese_AndFormatMoneyTheSameWay()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedBillingAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var standard = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        foreach (var route in BillingPages)
        {
            var response = await admin.GetAsync(route);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route} answered {(int)response.StatusCode}");
            var html = await response.Content.ReadAsStringAsync();
            var body = PageBody(Decoded(html));

            Assert.DoesNotContain("bi bi-", body);
            Assert.DoesNotContain("style=\"", body);
            Assert.DoesNotContain("onsubmit=", body);
            Assert.Contains("href=\"/css/admin-content.css", html);
            foreach (var english in new[] { "Provider Health", "Payment Provider", "Don pending", "payment order", "Payment orders", "Quản lý subscription", "Danh sách subscriptions" })
            {
                Assert.DoesNotContain(english, body, StringComparison.OrdinalIgnoreCase);
            }

            Assert.DoesNotMatch(new Regex(@"\d[.,]\d{3} VND"), body);
            Assert.Equal(HttpStatusCode.Forbidden, (await standard.GetAsync(route)).StatusCode);
        }

        var titles = new Dictionary<string, string>
        {
            ["/Admin/BillingManagement"] = "Quản lý thanh toán",
            ["/Admin/BillingManagement/Health"] = "Tình trạng cổng thanh toán",
            ["/Admin/BillingManagement/Subscriptions"] = "Gói đăng ký",
            ["/Admin/BillingManagement/PremiumUsers"] = "Người dùng Premium",
            ["/Admin/BillingManagement/Details/9201"] = "Chi tiết đơn thanh toán",
        };
        foreach (var (route, title) in titles)
        {
            Assert.Contains($"<title>{title}", Decoded(await admin.GetStringAsync(route)));
        }
    }

    [Fact]
    public async Task BillingList_ShowsTheFigures_ThatMatchTheDatabase_AndTheManualReviewAlert()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedBillingAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        decimal revenue;
        int active, pending, overdue, review;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var now = DateTime.UtcNow;
            revenue = await db.PaymentOrders.Where(o => o.Status == "paid" && o.PaidAtUtc >= now.AddDays(-30)).SumAsync(o => o.AmountVnd);
            active = await db.UserSubscriptions.Where(s => s.Status == "active" && s.EndsAtUtc > now).Select(s => s.UserId).Distinct().CountAsync();
            pending = await db.PaymentOrders.CountAsync(o => o.Status == "pending" && o.ExpiresAtUtc > now);
            overdue = await db.PaymentOrders.CountAsync(o => o.Status == "pending" && o.ExpiresAtUtc <= now);
            review = await db.PaymentOrders.CountAsync(o => o.Status == "manual_review");
        }

        Assert.Equal(399000m, revenue);
        Assert.Equal((1, 1, 1, 1), (active, pending, overdue, review));

        var body = PageBody(Decoded(await admin.GetStringAsync("/Admin/BillingManagement")));
        Assert.Contains("Doanh thu 30 ngày", body);
        Assert.Contains("399.000 ₫", body);
        Assert.DoesNotContain("1.399.000", body);                  // the old paid order is outside the 30-day window
        Assert.Contains("Premium đang hoạt động", body);
        Assert.Contains("Đơn đang chờ", body);
        Assert.Contains("1 đơn đã quá hạn", body);
        Assert.Contains("Đơn cần kiểm tra", body);
        Assert.Contains("<strong>1</strong> đơn cần kiểm tra thủ công", body);
        Assert.Contains("status=manual_review", body);
        Assert.Contains("Gói đăng ký", body);
        Assert.Contains("Người dùng Premium", body);
        Assert.Contains("Tình trạng cổng", body);
        Assert.Contains("Đóng đơn chờ quá hạn", body);
        Assert.Contains("data-ac-confirm-message=\"Đóng tất cả đơn đang chờ đã quá hạn và chuyển sang hết hạn?\"", body);
        Assert.Contains("Kiểm tra thủ công", body);               // status tag of the manual-review order
        Assert.Contains("VNPay", body);
        Assert.Contains("MoMo", body);

        // The figures ignore the list filters.
        var filtered = PageBody(Decoded(await admin.GetStringAsync("/Admin/BillingManagement?status=failed")));
        Assert.Contains("399.000 ₫", filtered);
        Assert.Contains("<strong>1</strong> đơn cần kiểm tra thủ công", filtered);
    }

    [Theory]
    [InlineData("", 6)]
    [InlineData("7", 3)]
    [InlineData("30", 4)]
    [InlineData("90", 6)]
    [InlineData("abc", 6)]
    public async Task BillingList_FiltersByTheTimeRange(string range, int expected)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedBillingAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var body = PageBody(Decoded(await admin.GetStringAsync($"/Admin/BillingManagement?range={range}")));

        Assert.Matches(new Regex($"ac-count\"[^>]*>{expected}<"), body);
        Assert.Equal(expected, Regex.Matches(body, "<td>\\s*<div class=\"ac-cell-main\">ORD-").Count);
    }

    [Fact]
    public async Task BillingList_CombinesStatusSearchAndRange_AndKeepsTheRangeInPagerLinks()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedBillingAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var review = PageBody(Decoded(await admin.GetStringAsync("/Admin/BillingManagement?status=manual_review")));
        Assert.Contains("ORD-REVIEW", review);
        Assert.DoesNotContain("ORD-RECENT", review);

        var searched = PageBody(Decoded(await admin.GetStringAsync("/Admin/BillingManagement?q=ORD-OLD")));
        Assert.Contains("ORD-OLD", searched);
        Assert.DoesNotContain("ORD-FRESH", searched);

        var tooNarrow = PageBody(Decoded(await admin.GetStringAsync("/Admin/BillingManagement?q=ORD-OLD&range=30")));
        Assert.Contains("Không tìm thấy đơn thanh toán nào.", tooNarrow);

        foreach (var page in new[] { "0", "-4", "99" })
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/Admin/BillingManagement?page={page}")).StatusCode);
        }
    }

    [Fact]
    public async Task EveryExistingBillingAction_IsStillOnItsPage()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedBillingAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var index = await admin.GetStringAsync("/Admin/BillingManagement");
        Assert.Contains("/Admin/BillingManagement/CleanupExpiredPending", index);
        Assert.Contains("/Admin/BillingManagement/Health", index);
        Assert.Contains("/Admin/BillingManagement/Subscriptions", index);
        Assert.Contains("/Admin/BillingManagement/PremiumUsers", index);
        Assert.Contains("/Admin/BillingManagement/Details/9205", index);

        var review = await admin.GetStringAsync("/Admin/BillingManagement/Details/9205");
        Assert.Contains("/Admin/BillingManagement/ResolveManualReviewConfirmPaid", review);
        Assert.Contains("/Admin/BillingManagement/ResolveManualReviewReject", review);

        var paid = await admin.GetStringAsync("/Admin/BillingManagement/Details/9201");
        Assert.Contains("/Admin/BillingManagement/MarkManualReview", await admin.GetStringAsync("/Admin/BillingManagement/Details/9204"));
        Assert.Contains("Hoàn tiền tự động chưa được hỗ trợ", Decoded(paid));

        foreach (var page in new[] { "/Admin/BillingManagement/Subscriptions", "/Admin/BillingManagement/PremiumUsers" })
        {
            var html = await admin.GetStringAsync(page);
            Assert.Contains("/Admin/BillingManagement/Grant", html);
            Assert.Contains("/Admin/BillingManagement/Revoke", html);
            Assert.Contains("grantPremiumModal", html);
        }

        Assert.Contains("Quay lại", Decoded(await admin.GetStringAsync("/Admin/BillingManagement/PremiumUserDetails?userId=101")));
    }

    [Fact]
    public async Task ClosingOverdueOrders_NeedsAToken_AndOnlyTouchesOverdueOnes()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedBillingAsync(factory);
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var noToken = await admin.PostAsync("/Admin/BillingManagement/CleanupExpiredPending", new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        var done = await PostFormAsync(admin, "/Admin/BillingManagement", "/Admin/BillingManagement/CleanupExpiredPending");
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal("expired", (await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == PendingOverdueId)).Status);
        Assert.Equal("pending", (await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == PendingFreshId)).Status);
        Assert.Equal("manual_review", (await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == ManualReviewId)).Status);
        Assert.Equal("paid", (await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == RecentPaidId)).Status);
    }

    // ==================================================================================
    // Notifications (recipient count, preview, confirmation; sending logic untouched)
    // ==================================================================================

    private static readonly string[] NotificationPages =
    {
        "/Admin/NotificationManagement/Create",
        "/Admin/NotificationManagement/History",
    };

    [Fact]
    public async Task NotificationPages_UseTheKit_AndKeepTheirExistingContract()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var standard = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        foreach (var route in NotificationPages)
        {
            var response = await admin.GetAsync(route);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route} answered {(int)response.StatusCode}");
            var html = await response.Content.ReadAsStringAsync();
            var body = PageBody(html);

            Assert.DoesNotContain("bi bi-", body);
            Assert.DoesNotContain("style=\"", body);
            Assert.DoesNotContain("<script", body);
            Assert.Contains("href=\"/css/admin-content.css", html);
            Assert.Equal(HttpStatusCode.Forbidden, (await standard.GetAsync(route)).StatusCode);
        }

        var create = Decoded(await admin.GetStringAsync("/Admin/NotificationManagement/Create"));
        Assert.Contains("for=\"titleInput\"", create);
        Assert.Contains("for=\"messageInput\"", create);
        Assert.Contains("data-ac-max=\"200\"", create);
        Assert.Contains("data-ac-max=\"1000\"", create);
        Assert.Contains("name=\"Url\"", create);
        Assert.Contains("Đường dẫn trong TCT English, ví dụ /Premium hoặc /Vocabulary", create);
        Assert.Contains("Thông báo sẽ hiện như thế này trong chuông thông báo của người dùng:", create);
        Assert.Contains("id=\"modalPreviewUrl\"", create);
        Assert.Contains("data-ac-submit-form=\"announcementForm\"", create);
        Assert.Contains("Vừa xong", create);
    }

    [Fact]
    public async Task TheAnnouncementForm_ShowsTheRecipientCount_ThatMatchesTheSendFilter()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var blocked = await db.Users.SingleAsync(u => u.UserId == TestDataIds.OutsiderUserId);
            blocked.Status = UserStatus.Blocked;
            var gone = await db.Users.SingleAsync(u => u.UserId == TestDataIds.MemberUserId);
            gone.AnonymizedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        int expected;
        int allUsers;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            expected = await db.Users.CountAsync(u => u.Status != UserStatus.Blocked && u.AnonymizedAt == null);
            allUsers = await db.Users.CountAsync();
        }

        Assert.True(expected < allUsers, "the fixture must contain accounts that do not receive the announcement");

        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        var html = Decoded(await admin.GetStringAsync("/Admin/NotificationManagement/Create"));

        Assert.Contains($"Sẽ gửi tới <strong>{expected}</strong> tài khoản đang hoạt động", html);
        Assert.Contains($"Gửi thông báo tới {expected} tài khoản?", html);
        Assert.Contains("Không thể thu hồi sau khi gửi.", html);

        // A refused post (empty title) shows the same count again and sends nothing.
        var refused = await PostFormAsync(admin, "/Admin/NotificationManagement/Create", "/Admin/NotificationManagement/Create",
            ("BatchId", Guid.NewGuid().ToString()), ("Title", ""), ("Message", "Nội dung vẫn còn"));
        var refusedHtml = Decoded(await refused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("Tiêu đề không được để trống", refusedHtml);
        Assert.Contains("Nội dung vẫn còn", refusedHtml);
        Assert.Contains($"Sẽ gửi tới <strong>{expected}</strong> tài khoản đang hoạt động", refusedHtml);

        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False(await verifyDb.Notifications.AnyAsync(n => n.Title == ""));
    }

    [Fact]
    public async Task TheAnnouncementHistory_KeepsItsColumnsAndShowsTheEmptyStateInVietnamese()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var empty = Decoded(await admin.GetStringAsync("/Admin/NotificationManagement/History"));
        Assert.Contains("Chưa có thông báo nào", empty);
        Assert.Contains("Gửi thông báo mới", empty);

        var body = PageBody(empty);
        Assert.Contains("Lịch sử thông báo", body);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Admin/NotificationManagement/History?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Admin/NotificationManagement/History?page=999")).StatusCode);
    }
}
