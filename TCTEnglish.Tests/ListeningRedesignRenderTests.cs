using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Listening redesign (direction F): what the two pages render for guests, Standard and Premium users
/// (plan 2026-11-18-listening-redesign, sections 5.1-5.7). Behaviour of the scripts is covered by the node tests
/// and the browser runs; these tests pin the server-rendered contract the scripts and the CSS depend on.
/// </summary>
public sealed class ListeningRedesignRenderTests
{
    private const int StudentId = 9401;
    private const int PremiumId = 9402;
    private const int OtherPremiumId = 9403;

    private const int A1FullId = 94001;      // A1 Daily: transcript + quiz + vocab, the first lesson of the lowest level
    private const int A1PlainId = 94002;     // A1 Travel: transcript only
    private const int A2Id = 94003;          // A2 Daily: transcript + quiz
    private const int B1Id = 94004;          // B1 Work: transcript + vocab
    private const int C2Id = 94005;          // C2 Daily: transcript + quiz + vocab
    private const int PrivateId = 94006;     // private, owned by PremiumId

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.Users.AddRange(
            User(StudentId, "render-student@test.local", Roles.Standard),
            User(PremiumId, "render-premium@test.local", Roles.Premium),
            User(OtherPremiumId, "render-other@test.local", Roles.Premium));
        db.ListeningLessons.AddRange(
            Lesson(A1FullId, "Ordering coffee", "A1", "Daily", null, lines: 3, questions: 2, vocab: 2),
            Lesson(A1PlainId, "At the airport", "A1", "Travel", null, lines: 2, questions: 0, vocab: 0),
            Lesson(A2Id, "Doctor visit", "A2", "Daily", null, lines: 2, questions: 1, vocab: 0),
            Lesson(B1Id, "Job talk", "B1", "Work", null, lines: 2, questions: 0, vocab: 2),
            Lesson(C2Id, "Idioms", "C2", "Daily", null, lines: 2, questions: 1, vocab: 1),
            Lesson(PrivateId, "My own video", "B2", "Mine", PremiumId, lines: 2, questions: 1, vocab: 0));
        await db.SaveChangesAsync();
        return factory;
    }

    private static User User(int id, string email, string role) => new()
    {
        UserId = id, Email = email, PasswordHash = "hash", FullName = email, Role = role, Status = UserStatus.Online, CreatedAt = DateTime.UtcNow,
    };

    private static ListeningLesson Lesson(int id, string title, string level, string topic, int? owner, int lines, int questions, int vocab)
    {
        var lesson = new ListeningLesson
        {
            Id = id, Title = title, Level = level, Topic = topic, YoutubeId = "dQw4w9WgXcQ", Duration = "01:20",
            IsPublished = true, OwnerUserId = owner, CreatedAt = DateTime.UtcNow.AddMinutes(id % 1000),
        };
        for (var i = 0; i < lines; i++)
            lesson.TranscriptLines.Add(new ListeningTranscriptLine { OrderIndex = i + 1, Speaker = "A", Text = $"Line {i + 1}", StartTime = i * 4, EndTime = i * 4 + 3 });
        for (var i = 0; i < questions; i++)
            lesson.QuizQuestions.Add(new ListeningQuizQuestion { OrderIndex = i + 1, QuestionText = $"Q{i + 1}", OptionA = "a", OptionB = "b", OptionC = "c", OptionD = "d", CorrectAnswer = "A" });
        for (var i = 0; i < vocab; i++)
            lesson.VocabItems.Add(new ListeningVocabItem { OrderIndex = i + 1, Word = $"word{i + 1}", Definition = "meaning" });
        return lesson;
    }

    private static async Task SeedAsync(TestWebApplicationFactory factory, Action<DbflashcardContext> seed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private static HttpClient Client(TestWebApplicationFactory factory, int userId, string role)
        => IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, role);

    private static async Task<string> HtmlAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// The page's own markup, HTML-decoded: from its wrapper to the config script (the shared layout is not under test).
    /// Razor writes Vietnamese letters as numeric entities, so every text assertion runs on the decoded markup.
    /// </summary>
    private static string PageBody(string html)
    {
        var start = html.IndexOf("class=\"tct-theme tct-page tl-page", StringComparison.Ordinal);
        var end = html.IndexOf("id=\"tlConfig\"", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "page wrapper and config script are present");
        return WebUtility.HtmlDecode(html[start..end]);
    }

    private static string Decode(string html) => WebUtility.HtmlDecode(html);

    // ------------------------------------------------------------------ hero states

    [Fact]
    public async Task Catalog_Hero_Guest_InvitesToSignIn_AndOffersTheFirstLessonOfTheLowestLevel()
    {
        await using var factory = await CreateFactoryAsync();
        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var html = await HtmlAsync(guest, "/Home/Listening");
        var body = PageBody(html);

        Assert.Contains("data-hero=\"guest\"", body, StringComparison.Ordinal);
        Assert.Contains("Đăng nhập để lưu tiến độ và tiếp tục bài đang nghe.", Decode(body), StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/Login?returnUrl=%2FHome%2FListening\"", body, StringComparison.Ordinal);
        Assert.Contains("Nghe thử một bài", Decode(body), StringComparison.Ordinal);
        Assert.Matches($"class=\"btn btn-outline-primary\" href=\"/Home/Listening/Practice/{A1FullId}\"", body);
        Assert.DoesNotContain("Học tiếp", Decode(body), StringComparison.Ordinal);
        Assert.DoesNotContain("tlStatuses", body, StringComparison.Ordinal); // status chips are for signed-in users
        Assert.DoesNotContain("Bài nghe của tôi", Decode(body), StringComparison.Ordinal);
        Assert.DoesNotContain("tlImport", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_Hero_SignedInWithoutProgress_StartsTheFirstUnfinishedLesson()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, "/Home/Listening"));
        var text = Decode(body);

        Assert.Contains("data-hero=\"start\"", body, StringComparison.Ordinal);
        Assert.Contains("Bắt đầu", text, StringComparison.Ordinal);
        Assert.Contains("Bắt đầu nghe", text, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Home/Listening/Practice/{A1FullId}\"", body, StringComparison.Ordinal);
        Assert.Contains("Ordering coffee", text, StringComparison.Ordinal);
        Assert.Contains("A1 · Daily", text, StringComparison.Ordinal);
        Assert.Contains("0</strong> bài xong", text, StringComparison.Ordinal);
        Assert.DoesNotContain("quiz TB", text, StringComparison.Ordinal); // no finished lesson, no average
    }

    [Fact]
    public async Task Catalog_Hero_Continue_ShowsTheStepChipsTheNextTabAndTheStats()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedAsync(factory, db =>
        {
            db.UserListeningProgresses.AddRange(
                // a finished lesson with a stored quiz score feeds "bài xong" and "quiz TB"
                new UserListeningProgress { UserId = StudentId, LessonId = A2Id, TranscriptCompleted = true, QuizCompleted = true, QuizScore = 90, CompletedAt = DateTime.UtcNow.AddDays(-1), LastAccessedAt = DateTime.UtcNow.AddDays(-1) },
                // the lesson being listened to: transcript done, quiz and vocabulary still open
                new UserListeningProgress { UserId = StudentId, LessonId = A1FullId, TranscriptCompleted = true, LastAccessedAt = DateTime.UtcNow });
        });
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, "/Home/Listening"));
        var text = Decode(body);

        Assert.Contains("data-hero=\"continue\"", body, StringComparison.Ordinal);
        Assert.Contains("Nghe tiếp", text, StringComparison.Ordinal);
        Assert.Contains("Học tiếp", text, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Home/Listening/Practice/{A1FullId}#quiz\"", body, StringComparison.Ordinal); // first unfinished step
        Assert.Matches("tct-badge tct-badge--success\">\\s*<i class=\"fa-solid fa-check\"[^>]*></i>\\s*Nghe lời thoại", body);
        Assert.Contains("data-tl-hero-dict", body, StringComparison.Ordinal);   // filled from this browser's storage
        Assert.Matches("data-total=\"3\"", body);
        Assert.Contains("Từ vựng", text, StringComparison.Ordinal);
        Assert.Contains("1</strong> bài xong", text, StringComparison.Ordinal);
        Assert.Contains("90%</strong> quiz TB", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_Hero_EveryPublicLessonFinished_OffersToReviewThem()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedAsync(factory, db =>
        {
            foreach (var id in new[] { A1FullId, A1PlainId, A2Id, B1Id, C2Id })
                db.UserListeningProgresses.Add(new UserListeningProgress { UserId = StudentId, LessonId = id, TranscriptCompleted = true, QuizCompleted = true, QuizScore = 80, VocabReviewed = true, CompletedAt = DateTime.UtcNow, LastAccessedAt = DateTime.UtcNow });
        });
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, "/Home/Listening"));
        var text = Decode(body);

        Assert.Contains("data-hero=\"alldone\"", body, StringComparison.Ordinal);
        Assert.Contains("Bạn đã nghe hết các bài hiện có", text, StringComparison.Ordinal);
        Assert.Contains("Ôn lại bài đã xong", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/Home/Listening?status=done\"", body, StringComparison.Ordinal);
        Assert.Contains("5</strong> bài xong", text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ level row

    [Fact]
    public async Task Catalog_LevelRow_HasSevenCardsIncludingC2_WithCountsBarsAndDisabledEmptyLevels()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedAsync(factory, db =>
        {
            db.UserListeningProgresses.Add(new UserListeningProgress { UserId = StudentId, LessonId = A2Id, TranscriptCompleted = true, QuizCompleted = true, QuizScore = 70, CompletedAt = DateTime.UtcNow, LastAccessedAt = DateTime.UtcNow });
        });
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, "/Home/Listening"));

        var groupStart = body.IndexOf("id=\"tlLevels\"", StringComparison.Ordinal);
        var groupEnd = body.IndexOf("class=\"tl-filters\"", groupStart, StringComparison.Ordinal);
        Assert.True(groupStart >= 0 && groupEnd > groupStart, "the level row precedes the filters");
        var group = body[groupStart..groupEnd];
        var cards = Regex.Matches(group, "role=\"radio\"");
        Assert.Equal(7, cards.Count);
        foreach (var key in new[] { "all", "A1", "A2", "B1", "B2", "C1", "C2" })
            Assert.Contains($"data-level=\"{key}\"", group, StringComparison.Ordinal);

        // counts: signed in "{done}/{total}", "0 bài" for an empty level
        Assert.Matches("data-level=\"A2\"[\\s\\S]*?tl-level-card__count\">1/1<", group);
        Assert.Matches("data-level=\"A1\"[\\s\\S]*?tl-level-card__count\">0/2<", group);
        Assert.Matches("data-level=\"C2\"[\\s\\S]*?tl-level-card__count\">0/1<", group);
        Assert.Matches("data-level=\"all\"[\\s\\S]*?tl-level-card__count\">1/5<", group);

        // a finished level gets the success bar; an empty level is disabled and says "0 bài"
        Assert.Matches("data-level=\"A2\"[\\s\\S]*?tl-bar tl-bar--done\" max=\"100\" value=\"100\"", group);
        Assert.Matches("data-level=\"C1\"[^>]*disabled", group);
        Assert.Matches("data-level=\"B2\"[^>]*disabled", group); // the only B2 lesson is private
        Assert.Matches("data-level=\"C1\"[\\s\\S]*?tl-level-card__count\">0 bài<", group);

        // exactly one card is checked and tabbable
        Assert.Single(Regex.Matches(group, "aria-checked=\"true\""));
        Assert.Matches("data-level=\"all\"[^>]*aria-checked=\"true\"[^>]*tabindex=\"0\"", group);
        // the shared layout dialog (_CreateFolderDialog) ships its own <style>; the catalog itself must not
        var ownMarkup = Regex.Replace(body, @"<dialog[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
        Assert.DoesNotContain("<style", ownMarkup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Catalog_LevelRow_Guest_ShowsLessonCountsOnly_AndDeepLinkChecksTheLevel()
    {
        await using var factory = await CreateFactoryAsync();
        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var body = PageBody(await HtmlAsync(guest, "/Home/Listening?level=C2"));

        Assert.Matches("data-level=\"A1\"[\\s\\S]*?tl-level-card__count\">2 bài<", body);
        Assert.Matches("data-level=\"C2\"[^>]*aria-checked=\"true\"[^>]*tabindex=\"0\"", body);
        Assert.Matches("data-level=\"all\"[^>]*aria-checked=\"false\"[^>]*tabindex=\"-1\"", body);
        Assert.Matches("value=\"0\"", body); // guests have no progress
    }

    // ------------------------------------------------------------------ filters, shelves, cards

    [Fact]
    public async Task Catalog_Filters_TopicSelectListsTopicsAndStatusChipsAreForSignedInUsersOnly()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, "/Home/Listening?topic=Travel"));
        var text = Decode(body);

        Assert.Contains("<label for=\"tlTopic\" class=\"tl-topic__label\">Chủ đề:</label>", text, StringComparison.Ordinal);
        Assert.Contains("<option value=\"all\">Tất cả</option>", text, StringComparison.Ordinal);
        foreach (var topic in new[] { "Daily", "Travel", "Work" })
            Assert.Contains($"<option value=\"{topic}\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"Mine\"", text, StringComparison.Ordinal); // private topics never appear
        Assert.Matches("<option value=\"Travel\" selected=\"selected\">", body);

        foreach (var label in new[] { "Mọi trạng thái", "Chưa học", "Đang học", "Đã xong" })
            Assert.Contains(label, text, StringComparison.Ordinal);
        Assert.Matches("data-status=\"all\" aria-pressed=\"true\"", body);
        Assert.DoesNotContain("tlTopicsToggle", body, StringComparison.Ordinal); // the chip cloud is gone (D5)
        Assert.Contains("id=\"tlEmpty\"", body, StringComparison.Ordinal);
        Assert.Contains("Không có bài nào khớp bộ lọc.", text, StringComparison.Ordinal);
        Assert.Contains("id=\"tlClearFilters\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_Shelves_RenderEveryLessonWithItsHooks_AndCardsUseNativeProgressBarsOnly()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedAsync(factory, db =>
        {
            db.UserListeningProgresses.Add(new UserListeningProgress { UserId = StudentId, LessonId = A1FullId, TranscriptCompleted = true, LastAccessedAt = DateTime.UtcNow });
        });
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, "/Home/Listening"));
        var text = Decode(body);

        foreach (var key in new[] { "A1", "A2", "B1", "C2" })
            Assert.Contains($"data-level-section=\"{key}\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("data-level-section=\"C1\"", body, StringComparison.Ordinal);
        Assert.Contains("A1 · Sơ cấp", text, StringComparison.Ordinal);
        Assert.Contains("C2 · Thành thạo", text, StringComparison.Ordinal);
        Assert.Matches("data-level-section=\"A1\"[\\s\\S]*?data-level-count data-done=\"0\">0/2<", body);
        Assert.Contains("data-level-all=\"A1\"", body, StringComparison.Ordinal);
        Assert.Contains("Xem thêm bài", text, StringComparison.Ordinal);

        // card hooks used by the script and by the older tests
        Assert.Matches($"data-id=\"{A1FullId}\"[^>]*data-status=\"progress\"", body);
        Assert.Matches($"data-id=\"{A2Id}\"[^>]*data-status=\"new\"", body);
        Assert.Contains($"href=\"/Home/Listening/Practice/{A1FullId}\"", body, StringComparison.Ordinal);
        Assert.Contains("Đang học", text, StringComparison.Ordinal);
        Assert.Matches("<progress class=\"tl-bar tl-card__bar\" max=\"3\" value=\"1\"", body);

        // no inline style anywhere in the page, no inline handlers, one h1
        Assert.DoesNotContain(" style=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--p:", body, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(body, "<h1[ >]"));
    }

    // ------------------------------------------------------------------ "Bài nghe của tôi"

    [Fact]
    public async Task Catalog_MyLessons_PremiumSeesTheTileTheHiddenFormAndTheirCards()
    {
        await using var factory = await CreateFactoryAsync();
        using var premium = Client(factory, PremiumId, Roles.Premium);
        var body = PageBody(await HtmlAsync(premium, "/Home/Listening"));
        var text = Decode(body);

        Assert.Contains("Bài nghe của tôi", text, StringComparison.Ordinal);
        Assert.Contains("Thêm từ YouTube", text, StringComparison.Ordinal);
        Assert.Matches("id=\"tlImportOpen\"[^>]*aria-expanded=\"false\"[^>]*aria-controls=\"tlImportPanel\"", body);
        Assert.Matches("id=\"tlImportPanel\" hidden", body);
        Assert.Contains("id=\"tlImport\"", body, StringComparison.Ordinal);
        Assert.Matches("id=\"tlImportTile\"[^>]*aria-controls=\"tlImportPanel\"", body);
        Assert.Contains("Dán link YouTube", text, StringComparison.Ordinal);
        Assert.Contains($"data-mine-id=\"{PrivateId}\"", body, StringComparison.Ordinal);
        Assert.Contains($"data-tl-delete=\"{PrivateId}\"", body, StringComparison.Ordinal);
        Assert.Contains("Riêng tư", text, StringComparison.Ordinal);
        Assert.Contains("id=\"tlDeleteLesson\"", body, StringComparison.Ordinal); // the delete confirmation modal is still rendered

        using var other = Client(factory, OtherPremiumId, Roles.Premium);
        var otherBody = PageBody(await HtmlAsync(other, "/Home/Listening"));
        Assert.DoesNotContain($"data-mine-id=\"{PrivateId}\"", otherBody, StringComparison.Ordinal); // another user's lesson
    }

    [Fact]
    public async Task Catalog_MyLessons_StandardSeesTheLockedNotice_GuestSeesNothing()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var studentBody = PageBody(await HtmlAsync(student, "/Home/Listening"));
        Assert.Contains("Nhập video YouTube để luyện nghe là tính năng Premium.", Decode(studentBody), StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/Premium\"", studentBody, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"tlImport", studentBody, StringComparison.Ordinal);
        Assert.DoesNotContain("tlImportTile", studentBody, StringComparison.Ordinal);

        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var guestHtml = await HtmlAsync(guest, "/Home/Listening");
        Assert.DoesNotContain("Bài nghe của tôi", Decode(PageBody(guestHtml)), StringComparison.Ordinal);
        Assert.DoesNotContain("tlImport", PageBody(guestHtml), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_Config_CarriesTheShelfAndGridSizesAndOnlyTheSignedInUsersOwnKey()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var html = await HtmlAsync(student, "/Home/Listening");
        var config = Regex.Match(html, "<script type=\"application/json\" id=\"tlConfig\">([\\s\\S]*?)</script>").Groups[1].Value;
        using var json = System.Text.Json.JsonDocument.Parse(config);

        Assert.Equal(4, json.RootElement.GetProperty("shelfSize").GetInt32());
        Assert.Equal(8, json.RootElement.GetProperty("gridSize").GetInt32());
        Assert.Equal(StudentId.ToString(), json.RootElement.GetProperty("userKey").GetString());
        Assert.True(json.RootElement.GetProperty("isAuthenticated").GetBoolean());

        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var guestHtml = await HtmlAsync(guest, "/Home/Listening");
        var guestConfig = Regex.Match(guestHtml, "<script type=\"application/json\" id=\"tlConfig\">([\\s\\S]*?)</script>").Groups[1].Value;
        using var guestJson = System.Text.Json.JsonDocument.Parse(guestConfig);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, guestJson.RootElement.GetProperty("userKey").ValueKind);
    }

    // ------------------------------------------------------------------ practice layout (L4, plan 5.2)

    [Fact]
    public async Task Practice_Standard_ShowsBreadcrumbWithLevel_LineButtons_LockedSpeed_AndFourStepRowsInOrder()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var html = await HtmlAsync(student, $"/Home/Listening/Practice/{A1FullId}");
        var body = PageBody(html);

        // breadcrumb: Luyện nghe / A1 / title (the level links back to the filtered catalog)
        Assert.Contains("href=\"/Home/Listening?level=A1\"", body, StringComparison.Ordinal);
        Assert.Contains("Ordering coffee", body, StringComparison.Ordinal);
        Assert.Contains("Daily · 01:20", body, StringComparison.Ordinal);

        // player ids survive, the two line buttons are new
        foreach (var id in new[] { "tlYoutube", "tlPrevLine", "tlBack", "tlPlay", "tlForward", "tlNextLine", "tlLoop" })
            Assert.Contains($"id=\"{id}\"", body, StringComparison.Ordinal);

        // Standard: fixed 1x with the Premium tooltip, no speed radios
        Assert.DoesNotContain("name=\"tlSpeed\"", body, StringComparison.Ordinal);
        Assert.Contains("title=\"Đổi tốc độ là tính năng Premium\"", body, StringComparison.Ordinal);

        // step card: title, four rows in plan order, the dictation row is optional and filled client-side
        Assert.Contains("Tiến độ bài này", body, StringComparison.Ordinal);
        Assert.Matches("data-step=\"transcript\"(?s:.)*Nghe hết lời thoại(?s:.)*data-step=\"dictation\"(?s:.)*Chép chính tả(?s:.)*Tùy chọn(?s:.)*data-step=\"quiz\"(?s:.)*Làm quiz(?s:.)*0/2(?s:.)*data-step=\"vocab\"(?s:.)*Ôn từ vựng trong bài(?s:.)*2 từ", body);
        Assert.Contains("data-tl-goto=\"dictation\"", body, StringComparison.Ordinal);
        Assert.Contains("id=\"tlDone\"", body, StringComparison.Ordinal);
        Assert.Contains("Space", body, StringComparison.Ordinal);
        Assert.Contains("đổi câu", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Practice_StepRowsFollowTheLessonContent_AndPremiumSeesTheSpeedRadios()
    {
        await using var factory = await CreateFactoryAsync();
        using var premium = Client(factory, PremiumId, Roles.Premium);

        // transcript only: no quiz or vocab row (same rule as requiredSteps), dictation stays
        var plain = PageBody(await HtmlAsync(premium, $"/Home/Listening/Practice/{A1PlainId}"));
        Assert.Contains("data-step=\"transcript\"", plain, StringComparison.Ordinal);
        Assert.Contains("data-step=\"dictation\"", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("data-step=\"quiz\"", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("data-step=\"vocab\"", plain, StringComparison.Ordinal);

        // Premium: five speed radios, no lock tooltip
        Assert.Equal(5, Regex.Matches(plain, "name=\"tlSpeed\"").Count);
        Assert.DoesNotContain("Đổi tốc độ là tính năng Premium", plain, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Practice_CompletedSteps_ShowTheirValues_AndTheConfigCarriesOnlyTheSignedInUsersKey()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedAsync(factory, db => db.UserListeningProgresses.Add(new UserListeningProgress
        {
            UserId = StudentId, LessonId = A1FullId, TranscriptCompleted = true, QuizCompleted = true, QuizScore = 86, VocabReviewed = true, CompletedAt = DateTime.UtcNow, LastAccessedAt = DateTime.UtcNow,
        }));
        using var student = Client(factory, StudentId, Roles.Standard);
        var html = await HtmlAsync(student, $"/Home/Listening/Practice/{A1FullId}");
        var body = PageBody(html);

        Assert.Matches("data-step=\"transcript\" data-done=\"true\"(?s:.)*Xong", body);
        Assert.Matches("data-step=\"quiz\" data-done=\"true\"(?s:.)*86%", body);
        Assert.Matches("data-step=\"vocab\" data-done=\"true\"(?s:.)*Xong", body);

        var config = Regex.Match(html, "<script type=\"application/json\" id=\"tlConfig\">((?s:.)*?)</script>").Groups[1].Value;
        using var json = System.Text.Json.JsonDocument.Parse(config);
        Assert.Equal(StudentId.ToString(), json.RootElement.GetProperty("userKey").GetString());

        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var guestHtml = await HtmlAsync(guest, $"/Home/Listening/Practice/{A1FullId}");
        var guestConfig = Regex.Match(guestHtml, "<script type=\"application/json\" id=\"tlConfig\">((?s:.)*?)</script>").Groups[1].Value;
        using var guestJson = System.Text.Json.JsonDocument.Parse(guestConfig);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, guestJson.RootElement.GetProperty("userKey").ValueKind);
        Assert.Contains("href=\"/Account/Login?returnUrl=%2FHome%2FListening%2FPractice%2F" + A1FullId + "\"", PageBody(guestHtml), StringComparison.Ordinal);
    }
}
