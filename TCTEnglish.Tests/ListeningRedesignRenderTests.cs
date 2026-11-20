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

    // ------------------------------------------------------------------ hero states (catalogue direction B)

    [Fact]
    public async Task Catalog_Hero_Guest_OffersTheFirstLessonOfTheLowestLevel_AndALoginHint()
    {
        await using var factory = await CreateFactoryAsync();
        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var html = await HtmlAsync(guest, "/Home/Listening");
        var body = PageBody(html);
        var text = Decode(body);

        Assert.Contains("data-hero=\"guest\"", body, StringComparison.Ordinal);
        Assert.Contains("Nghe thử", text, StringComparison.Ordinal);
        Assert.Contains("Ordering coffee", text, StringComparison.Ordinal);
        Assert.Matches($"class=\"btn btn-primary\" href=\"/Home/Listening/Practice/{A1FullId}\"[^>]*>(?s:.)*?Nghe thử miễn phí", body);
        Assert.Contains("class=\"tl-hero__hint tl-hero__hint--link\" href=\"/Account/Login?returnUrl=%2FHome%2FListening\">Đăng nhập để lưu tiến độ</a>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Học tiếp", text, StringComparison.Ordinal);
        Assert.DoesNotContain("tlStatuses", body, StringComparison.Ordinal); // status chips are for signed-in users
        Assert.DoesNotContain("Bài nghe của tôi", text, StringComparison.Ordinal);
        Assert.DoesNotContain("tlImport", body, StringComparison.Ordinal);
        // the thumbnail is a real image behind the scrim, not a CSS background
        Assert.Matches("<div class=\"tl-hero__media\"[^>]*>\\s*<img class=\"tl-thumb\" src=\"[^\"]+\" alt=\"\"", body);
        Assert.Contains("tl-hero__scrim", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_Hero_SignedInWithoutProgress_StartsTheFirstUnfinishedLesson()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, "/Home/Listening"));
        var text = Decode(body);

        Assert.Contains("data-hero=\"start\"", body, StringComparison.Ordinal);
        Assert.Contains("tl-hero__chip\">Bắt đầu<", text, StringComparison.Ordinal);
        Assert.Contains("Bắt đầu nghe", text, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Home/Listening/Practice/{A1FullId}\"", body, StringComparison.Ordinal);
        Assert.Contains("Ordering coffee", text, StringComparison.Ordinal);
        Assert.Contains("A1 · Daily · 01:20", text, StringComparison.Ordinal);
        Assert.Contains("5 bài đang chờ bạn", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_Hero_Continue_ShowsTheMissingStepsAndOpensTheNextTab()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedAsync(factory, db =>
        {
            db.UserListeningProgresses.AddRange(
                new UserListeningProgress { UserId = StudentId, LessonId = A2Id, TranscriptCompleted = true, QuizCompleted = true, QuizScore = 90, CompletedAt = DateTime.UtcNow.AddDays(-1), LastAccessedAt = DateTime.UtcNow.AddDays(-1) },
                // the lesson being listened to: transcript done, quiz and vocabulary still open
                new UserListeningProgress { UserId = StudentId, LessonId = A1FullId, TranscriptCompleted = true, LastAccessedAt = DateTime.UtcNow });
        });
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, "/Home/Listening"));
        var text = Decode(body);

        Assert.Contains("data-hero=\"continue\"", body, StringComparison.Ordinal);
        Assert.Contains("tl-hero__chip\">Nghe tiếp<", text, StringComparison.Ordinal);
        Assert.Contains("Học tiếp", text, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Home/Listening/Practice/{A1FullId}#quiz\"", body, StringComparison.Ordinal); // first unfinished step
        Assert.Contains("<p class=\"tl-hero__hint\">Còn quiz và từ vựng</p>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("data-tl-hero-dict", body, StringComparison.Ordinal); // the dictation chip moved out of the hero
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

        Assert.Contains("data-hero=\"review\"", body, StringComparison.Ordinal);
        Assert.Contains("tl-hero__chip\">Ôn lại<", text, StringComparison.Ordinal);
        Assert.Contains("Bạn đã nghe hết các bài hiện có", text, StringComparison.Ordinal);
        Assert.Contains("Ôn lại bài đã xong", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/Home/Listening?status=done\"", body, StringComparison.Ordinal);
        Assert.Contains("data-tl-hero-done", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the YouTube card (top row, right)

    [Fact]
    public async Task Catalog_YoutubeCard_SitsNextToTheHero_AboveTheFilters_ForEveryRole()
    {
        await using var factory = await CreateFactoryAsync();

        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var guestBody = PageBody(await HtmlAsync(guest, "/Home/Listening"));
        Assert.Contains("Bài nghe từ YouTube", Decode(guestBody), StringComparison.Ordinal);
        Assert.Contains("Đăng nhập để tạo bài nghe từ video YouTube.", Decode(guestBody), StringComparison.Ordinal);
        Assert.Contains("data-yt=\"guest\"", guestBody, StringComparison.Ordinal);

        using var student = Client(factory, StudentId, Roles.Standard);
        var studentBody = PageBody(await HtmlAsync(student, "/Home/Listening"));
        Assert.Contains("data-yt=\"locked\"", studentBody, StringComparison.Ordinal);
        Assert.Contains("Dán link YouTube, TCT tạo lời thoại, chép chính tả và quiz cho bạn.", Decode(studentBody), StringComparison.Ordinal);
        Assert.Matches("href=\"/Account/Premium\"[^>]*>(?s:.)*?Mở khóa với Premium", Decode(studentBody));
        Assert.DoesNotContain("id=\"tlImport", studentBody, StringComparison.Ordinal);

        using var premium = Client(factory, PremiumId, Roles.Premium);
        var premiumBody = PageBody(await HtmlAsync(premium, "/Home/Listening"));
        Assert.Contains("data-yt=\"import\"", premiumBody, StringComparison.Ordinal);
        foreach (var id in new[] { "tlImport", "tlImportUrl", "tlImportSubmit", "tlImportHint", "tlImportError", "tlImportStatus" })
            Assert.Contains($"id=\"{id}\"", premiumBody, StringComparison.Ordinal);
        Assert.Contains("name=\"youtubeUrl\"", premiumBody, StringComparison.Ordinal);
        Assert.Contains("Tạo bài nghe", Decode(premiumBody), StringComparison.Ordinal);
        Assert.Contains("Chỉ hỗ trợ video tiếng Anh có phụ đề.", Decode(premiumBody), StringComparison.Ordinal);
        Assert.Matches("Bài của tôi: <strong id=\"tlMineCount\">1</strong>", Decode(premiumBody));

        // top row first: hero, then the card, then the level pills and the filters
        var hero = premiumBody.IndexOf("class=\"tl-hero\"", StringComparison.Ordinal);
        var card = premiumBody.IndexOf("class=\"tl-yt k-panel\"", StringComparison.Ordinal);
        var filters = premiumBody.IndexOf("id=\"tlLevels\"", StringComparison.Ordinal);
        Assert.True(hero > 0 && card > hero && filters > card, "hero, YouTube card, then the filters");

        // the old bottom import section is gone
        foreach (var removed in new[] { "tlImportOpen", "tlImportPanel", "tlImportTile", "Thêm từ YouTube", "tl-import-panel" })
            Assert.DoesNotContain(removed, Decode(premiumBody), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ level bands (pills)

    [Fact]
    public async Task Catalog_Bands_AreFourAtMost_EmptyBandsAreHidden_AndExactlyOneIsChecked()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, "/Home/Listening"));

        var groupStart = body.IndexOf("id=\"tlLevels\"", StringComparison.Ordinal);
        var groupEnd = body.IndexOf("class=\"tl-filters__side\"", groupStart, StringComparison.Ordinal);
        Assert.True(groupStart >= 0 && groupEnd > groupStart, "the band pills lead the filter row");
        var group = body[groupStart..groupEnd];
        Assert.Contains("role=\"radiogroup\"", body[(groupStart - 80)..groupEnd], StringComparison.Ordinal);

        // seeded: A1, A2, B1, C2 are public, the only B2 lesson is private: bands "Tất cả", A1–A2, B1, C1–C2
        Assert.Equal(4, Regex.Matches(group, "role=\"radio\"").Count);
        foreach (var key in new[] { "all", "A1–A2", "B1", "C1–C2" })
            Assert.Contains($"data-band=\"{key}\"", group, StringComparison.Ordinal);
        Assert.DoesNotContain("data-band=\"B2\"", group, StringComparison.Ordinal);
        Assert.Contains("A1–A2</button>", group, StringComparison.Ordinal);

        // exactly one pill is checked and tabbable
        Assert.Single(Regex.Matches(group, "aria-checked=\"true\""));
        Assert.Matches("data-band=\"all\"[^>]*aria-checked=\"true\"[^>]*tabindex=\"0\"", group);

        // the old big level cards are gone
        Assert.DoesNotContain("tl-level-card", body, StringComparison.Ordinal);
        // the shared layout dialog (_CreateFolderDialog) ships its own <style>; the catalog itself must not
        var ownMarkup = Regex.Replace(body, @"<dialog[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
        Assert.DoesNotContain("<style", ownMarkup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Catalog_Bands_ALevelInTheUrlChecksItsBand_AndAHiddenBandFallsBackToAll()
    {
        await using var factory = await CreateFactoryAsync();
        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var c2 = await HtmlAsync(guest, "/Home/Listening?level=C2");
        var c2Body = PageBody(c2);
        Assert.Matches("data-band=\"C1–C2\"[^>]*aria-checked=\"true\"[^>]*tabindex=\"0\"", c2Body);
        Assert.Matches("data-band=\"all\"[^>]*aria-checked=\"false\"[^>]*tabindex=\"-1\"", c2Body);
        var c2Config = Regex.Match(c2, "<script type=\"application/json\" id=\"tlConfig\">([\\s\\S]*?)</script>").Groups[1].Value;
        using (var c2Json = System.Text.Json.JsonDocument.Parse(c2Config))
        {
            Assert.Equal("C1–C2", c2Json.RootElement.GetProperty("selectedBand").GetString());
            Assert.Equal("C2", c2Json.RootElement.GetProperty("selectedLevel").GetString());
        }

        var a2 = PageBody(await HtmlAsync(guest, "/Home/Listening?level=A2"));
        Assert.Matches("data-band=\"A1–A2\"[^>]*aria-checked=\"true\"", a2);

        // B2 has only a private lesson: there is no B2 pill, so "Tất cả" stays checked instead of leaving none checked
        var b2 = PageBody(await HtmlAsync(guest, "/Home/Listening?level=B2"));
        Assert.Single(Regex.Matches(b2, "aria-checked=\"true\""));
        Assert.Matches("data-band=\"all\"[^>]*aria-checked=\"true\"", b2);
    }

    // ------------------------------------------------------------------ filters, shelves, cards

    [Fact]
    public async Task Catalog_Filters_TopicSelectListsTopicsAndStatusTogglesAreForSignedInUsersOnly()
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

        // three toggles (pressing the selected one clears it), no "all statuses" pill
        foreach (var label in new[] { "Chưa học", "Đang học", "Đã xong" })
            Assert.Contains(label, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Mọi trạng thái", text, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Matches(body, "data-status=\"(new|progress|done)\" aria-pressed=\"false\"").Count);
        Assert.DoesNotContain("data-status=\"all\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("tlTopicsToggle", body, StringComparison.Ordinal); // the chip cloud is gone (D5)
        Assert.Contains("id=\"tlEmpty\"", body, StringComparison.Ordinal);
        Assert.Contains("Không có bài nào khớp bộ lọc.", text, StringComparison.Ordinal);
        Assert.Contains("id=\"tlClearFilters\"", body, StringComparison.Ordinal);

        // one row: bands, then the status toggles and the topic select
        var bands = body.IndexOf("id=\"tlLevels\"", StringComparison.Ordinal);
        var statuses = body.IndexOf("id=\"tlStatuses\"", StringComparison.Ordinal);
        var topic2 = body.IndexOf("id=\"tlTopic\"", StringComparison.Ordinal);
        Assert.True(bands > 0 && statuses > bands && topic2 > statuses, "bands, statuses, topic");
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

        // the band grid exists once, empty and hidden, with its own "Xem thêm bài" button
        Assert.Matches("<section class=\"tl-shelf tl-band\" id=\"tlBand\"[^>]*hidden>", body);
        Assert.Contains("id=\"tlBandGrid\"", body, StringComparison.Ordinal);
        Assert.Matches("id=\"tlBandMore\" hidden>Xem thêm bài", text);

        // card hooks used by the script and by the older tests
        Assert.Matches($"data-id=\"{A1FullId}\"[^>]*data-status=\"progress\"", body);
        Assert.Matches($"data-id=\"{A2Id}\"[^>]*data-status=\"new\"", body);
        Assert.Contains($"href=\"/Home/Listening/Practice/{A1FullId}\"", body, StringComparison.Ordinal);
        Assert.Contains("Đang học", text, StringComparison.Ordinal);
        Assert.Matches("<progress class=\"tl-bar tl-card__bar\" max=\"3\" value=\"1\"", body);
        Assert.DoesNotContain("tl-play", body, StringComparison.Ordinal);           // no fake play icon on a card

        // no inline style anywhere in the page, no inline handlers, one h1
        Assert.DoesNotContain(" style=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--p:", body, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(body, "<h1[ >]"));
    }

    [Fact]
    public async Task Catalog_Cards_FinishedLessonShowsXongWithACheck_AndTheTopicHasATitle()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedAsync(factory, db =>
        {
            db.UserListeningProgresses.Add(new UserListeningProgress { UserId = StudentId, LessonId = A2Id, TranscriptCompleted = true, QuizCompleted = true, QuizScore = 70, CompletedAt = DateTime.UtcNow, LastAccessedAt = DateTime.UtcNow });
        });
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, "/Home/Listening"));

        Assert.Matches("tct-badge tct-badge--success tl-card__status\"><i class=\"fa-solid fa-check\"[^>]*></i> Xong</span>", body);
        Assert.Matches("<span class=\"tl-card__topic\" title=\"Daily\">Daily</span>", body);
    }

    // ------------------------------------------------------------------ "Bài nghe của tôi"

    [Fact]
    public async Task Catalog_MyLessons_PremiumSeesTheFormInTheTopCardAndTheirCardsInTheirOwnSection()
    {
        await using var factory = await CreateFactoryAsync();
        using var premium = Client(factory, PremiumId, Roles.Premium);
        var body = PageBody(await HtmlAsync(premium, "/Home/Listening"));
        var text = Decode(body);

        Assert.Contains("Bài nghe của tôi", text, StringComparison.Ordinal);
        Assert.Contains("id=\"tlImport\"", body, StringComparison.Ordinal);
        Assert.Matches("id=\"tlMine\" aria-labelledby=\"tl-mine-title\">", body);   // not hidden: the user has a lesson
        Assert.Contains("id=\"tlMineGrid\"", body, StringComparison.Ordinal);
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
        Assert.Contains("Dán link YouTube, TCT tạo lời thoại, chép chính tả và quiz cho bạn.", Decode(studentBody), StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/Premium\"", studentBody, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"tlImport", studentBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Bài nghe của tôi", Decode(studentBody), StringComparison.Ordinal); // no own lesson, no section

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

        Assert.Equal(5, json.RootElement.GetProperty("shelfSize").GetInt32());
        Assert.Equal(10, json.RootElement.GetProperty("gridSize").GetInt32());
        Assert.Equal("all", json.RootElement.GetProperty("selectedBand").GetString());
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

    // ------------------------------------------------------------------ practice tabs and contents (L5, plan 5.3-5.4)

    [Fact]
    public async Task Practice_HasFiveTabsWithTheirPanels_AndNoModeRadios()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, $"/Home/Listening/Practice/{A1FullId}"));

        foreach (var key in new[] { "transcript", "dictation", "fill", "quiz", "vocab" })
        {
            Assert.Contains($"id=\"tlTab-{key}\"", body, StringComparison.Ordinal);
            Assert.Contains($"id=\"tlPanel-{key}\"", body, StringComparison.Ordinal);
            Assert.Contains($"data-tl-tab=\"{key}\"", body, StringComparison.Ordinal);
        }

        // tab order follows the plan, labels are Vietnamese
        Assert.Matches("tlTab-transcript(?s:.)*Lời thoại(?s:.)*tlTab-dictation(?s:.)*Chép chính tả(?s:.)*tlTab-fill(?s:.)*Điền từ(?s:.)*tlTab-quiz(?s:.)*Quiz(?s:.)*tlTab-vocab(?s:.)*Từ vựng", body);
        Assert.Equal(5, Regex.Matches(body, "role=\"tab\"").Count);

        // the old mode radios are gone; their panels became tabs
        Assert.DoesNotContain("name=\"tlMode\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("tlModePanel", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Practice_TabsFollowTheContent_QuizAndVocabHiddenWhenEmpty_QuizKeptForAiGeneration()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);

        // transcript only: Lời thoại, Chép chính tả, Điền từ - and nothing else
        var plain = PageBody(await HtmlAsync(student, $"/Home/Listening/Practice/{A1PlainId}"));
        Assert.Equal(3, Regex.Matches(plain, "role=\"tab\"").Count);
        Assert.DoesNotContain("tlTab-quiz", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("tlTab-vocab", plain, StringComparison.Ordinal);

        // the owner of a private lesson without questions keeps the Quiz tab for the AI-generate empty state
        using var owner = Client(factory, PremiumId, Roles.Premium);
        var mine = PageBody(await HtmlAsync(owner, $"/Home/Listening/Practice/{PrivateId}"));
        Assert.Contains("tlTab-quiz", mine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Practice_Transcript_TimeIsTheSeekButton_TextCarriesNoNestedButton_AndTheToolbarIsComplete()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, $"/Home/Listening/Practice/{A1FullId}"));

        foreach (var id in new[] { "tlToggleVi", "tlToggleHide", "tlToggleScroll", "tlMarkDone", "tlLines", "tlLinesWrap" })
            Assert.Contains($"id=\"{id}\"", body, StringComparison.Ordinal);
        Assert.Contains("Bấm vào từ để tra nghĩa", body, StringComparison.Ordinal);

        // the clock is the only seek control of a line; the sentence text is left free for word buttons
        Assert.Matches("class=\"tl-line__seek tl-line__time\" aria-label=\"Nghe từ câu 1, 0:00\">0:00</button>", body);
        Assert.DoesNotMatch("<button[^>]*tl-line__seek[^>]*>\\s*<span class=\"tl-line__en\"", body);
        Assert.Matches("<span class=\"tl-line__en\" lang=\"en\">Line 1</span>", body);

        // the lesson vocabulary goes to the script so it can underline the words
        var html = await HtmlAsync(student, $"/Home/Listening/Practice/{A1FullId}");
        var config = Regex.Match(html, "<script type=\"application/json\" id=\"tlConfig\">((?s:.)*?)</script>").Groups[1].Value;
        using var json = System.Text.Json.JsonDocument.Parse(config);
        Assert.Equal(2, json.RootElement.GetProperty("vocab").GetArrayLength());
        Assert.Equal("word1", json.RootElement.GetProperty("vocab")[0].GetProperty("word").GetString());
        Assert.Equal("/api/listening/word/save", json.RootElement.GetProperty("urls").GetProperty("wordSave").GetString());
    }

    [Fact]
    public async Task Practice_Quiz_KeepsTheFormContract_AndAddsOneQuestionNavigation()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, $"/Home/Listening/Practice/{A1FullId}"));

        foreach (var id in new[] { "tlQuizForm", "tlQuizResult", "tlQuizLive", "tlQuizSubmit", "tlQuizCount", "tlQuizPrev", "tlQuizNext", "tlQuizHead", "tlQuizDots" })
            Assert.Contains($"id=\"{id}\"", body, StringComparison.Ordinal);
        // every question stays in the markup (all answers are still submitted together)
        Assert.Equal(2, Regex.Matches(body, "<fieldset class=\"tl-q k-panel\"").Count);
        Assert.Contains("Nộp bài", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ word popover, subtitles, phone (L6-L7, plan 5.5-5.7)

    [Fact]
    public async Task Practice_LoadsTheWordScriptBeforeThePracticeScript_AndCarriesTheLookupUrls()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var html = await HtmlAsync(student, $"/Home/Listening/Practice/{A1FullId}");

        var word = html.IndexOf("/js/listening-word.js", StringComparison.Ordinal);
        var practice = html.IndexOf("/js/listening-practice.js", StringComparison.Ordinal);
        Assert.True(word > 0 && practice > word, "listening-word.js must load before listening-practice.js");

        var config = Regex.Match(html, "<script type=\"application/json\" id=\"tlConfig\">((?s:.)*?)</script>").Groups[1].Value;
        using var json = System.Text.Json.JsonDocument.Parse(config);
        Assert.Equal("/api/listening/word", json.RootElement.GetProperty("urls").GetProperty("word").GetString());
        Assert.Equal("/Account/Premium", json.RootElement.GetProperty("urls").GetProperty("premium").GetString());
        Assert.False(json.RootElement.GetProperty("canLookupAnyWord").GetBoolean());

        using var premium = Client(factory, PremiumId, Roles.Premium);
        var premiumHtml = await HtmlAsync(premium, $"/Home/Listening/Practice/{A1FullId}");
        var premiumConfig = Regex.Match(premiumHtml, "<script type=\"application/json\" id=\"tlConfig\">((?s:.)*?)</script>").Groups[1].Value;
        using var premiumJson = System.Text.Json.JsonDocument.Parse(premiumConfig);
        Assert.True(premiumJson.RootElement.GetProperty("canLookupAnyWord").GetBoolean());
    }

    [Fact]
    public async Task Practice_SubtitleToggleAndOverlay_ExistOnlyForLessonsWithTimes_AndNeverInterceptTheVideo()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, $"/Home/Listening/Practice/{A1FullId}"));

        Assert.Contains("id=\"tlSubs\"", body, StringComparison.Ordinal);
        Assert.Contains("Phụ đề: Tắt", body, StringComparison.Ordinal);
        Assert.Contains("title=\"Phụ đề không hiện khi xem toàn màn hình.\"", body, StringComparison.Ordinal);
        // the overlay lives inside the video frame, after the iframe
        Assert.Matches("id=\"tlYoutube\"(?s:.)*?</iframe>\\s*<div class=\"tl-subs\" id=\"tlSubsOverlay\" hidden>", body);

        // phone: a back arrow to the catalog above the single h1
        Assert.Contains("class=\"tl-back\" href=\"/Home/Listening\"", body, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(body, "<h1"));
    }

    [Fact]
    public async Task Practice_VocabTab_OffersSaveAllToSignedInUsersOnly_AndKeepsTheReviewedButton()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, StudentId, Roles.Standard);
        var body = PageBody(await HtmlAsync(student, $"/Home/Listening/Practice/{A1FullId}"));
        Assert.Contains("id=\"tlVocabSaveAll\"", body, StringComparison.Ordinal);
        Assert.Contains("Lưu tất cả vào Bộ thẻ", body, StringComparison.Ordinal);
        Assert.Contains("id=\"tlVocabDone\"", body, StringComparison.Ordinal);
        Assert.Contains("Đánh dấu đã ôn", body, StringComparison.Ordinal);
        Assert.Contains("id=\"tlVocabSaveSummary\"", body, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(body, "data-tl-vocab-word=").Count);

        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var guestBody = PageBody(await HtmlAsync(guest, $"/Home/Listening/Practice/{A1FullId}"));
        Assert.DoesNotContain("tlVocabSaveAll", guestBody, StringComparison.Ordinal);
        Assert.Contains("id=\"tlVocabDone\"", guestBody, StringComparison.Ordinal);
    }
}
