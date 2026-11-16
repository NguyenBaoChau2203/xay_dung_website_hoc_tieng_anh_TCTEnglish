using System.Net;
using System.Text.RegularExpressions;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// The public landing page ("Dem hoc"): static content, links and the signed-in variant.
/// Razor encodes non-ASCII letters as numeric entities, so text is asserted on the decoded HTML.
/// </summary>
public sealed class LandingPageTests
{
    private static async Task<(HttpStatusCode Status, string Html, string Decoded)> GetLandingAsync(bool signedIn)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = signedIn
            ? IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard)
            : IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync("/Home/Landing");
        var html = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, html, WebUtility.HtmlDecode(html));
    }

    private static IEnumerable<(string Href, string Text)> Anchors(string decodedHtml) =>
        Regex.Matches(decodedHtml, "<a\\b[^>]*?href=\"([^\"]*)\"[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
            .Select(m => (m.Groups[1].Value, Regex.Replace(m.Groups[2].Value, "<[^>]+>", " ").Trim()));

    [Fact]
    public async Task Landing_AnonymousVisitor_GetsThePageWithTitleMetaAndHeading()
    {
        var (status, html, decoded) = await GetLandingAsync(signedIn: false);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<title>TCT English — Học tiếng Anh mỗi ngày 15 phút</title>", decoded, StringComparison.Ordinal);
        Assert.Contains("Tiếng Anh ngấm dần,", decoded, StringComparison.Ordinal);
        Assert.Contains("mỗi ngày 15 phút.", decoded, StringComparison.Ordinal);
        Assert.Contains("name=\"description\"", decoded, StringComparison.Ordinal);
        Assert.Contains("TCT English tự xếp lịch ôn để bạn nhớ lâu.", decoded, StringComparison.Ordinal);
        Assert.Contains("property=\"og:title\"", decoded, StringComparison.Ordinal);
        Assert.Contains("name=\"twitter:card\" content=\"summary_large_image\"", decoded, StringComparison.Ordinal);
        Assert.Contains("name=\"theme-color\"", decoded, StringComparison.Ordinal);
        Assert.Matches("/css/landing\\.css\\?v=", html);
        Assert.Matches("/js/landing\\.js\\?v=", html);
    }

    [Fact]
    public async Task Landing_HasNoInlineStyleBlockNoCdnBootstrapAndNoDeadLinks()
    {
        var (_, html, decoded) = await GetLandingAsync(signedIn: false);

        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn.jsdelivr.net", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("font-awesome/6.5.1", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Playfair", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"#\"", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("dành cho nhân viên", decoded, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Landing_StartButtonsLeadToRegistrationNotLogin()
    {
        var (_, _, decoded) = await GetLandingAsync(signedIn: false);

        var starts = Anchors(decoded)
            .Where(a => a.Text.Contains("Bắt đầu miễn phí", StringComparison.Ordinal)
                        || a.Text.Contains("Tạo tài khoản", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(starts);
        Assert.All(starts, a => Assert.Equal("/Account/Register", a.Href));
    }

    [Fact]
    public async Task Landing_AnonymousNavigation_KeepsLoginLinkAndSectionAnchors()
    {
        var (_, _, decoded) = await GetLandingAsync(signedIn: false);
        var anchors = Anchors(decoded).ToList();

        Assert.Contains(anchors, a => a.Href == "/Account/Login" && a.Text == "Đăng nhập");
        Assert.Contains(anchors, a => a.Href == "#ky-nang" && a.Text == "Kỹ năng");
        Assert.Contains(anchors, a => a.Href == "#on-tap" && a.Text == "Cách ôn tập");
        Assert.Contains(anchors, a => a.Href == "#tro-ly-ai" && a.Text == "Trợ lý AI");
        Assert.Contains(anchors, a => a.Href == "/Home/Contact" && a.Text == "Liên hệ");
        Assert.Contains("href=\"#noi-dung\"", decoded, StringComparison.Ordinal);
        Assert.Contains("id=\"noi-dung\"", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Landing_SignedInVisitor_SeesEnterButtonAndNoLoginOrRegisterLinks()
    {
        var (status, _, decoded) = await GetLandingAsync(signedIn: true);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Vào trang học", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("Đăng nhập", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("/Account/Register", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("/Account/Login", decoded, StringComparison.Ordinal);
        Assert.Contains(Anchors(decoded), a => a.Href == "/Home/Index" && a.Text.Contains("Vào trang học", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Landing_Hero_ShowsTheRealProductShotWithSizesAltAndDecorativeChips()
    {
        var (_, html, decoded) = await GetLandingAsync(signedIn: false);

        var shot = Regex.Match(decoded, "<img\\b[^>]*hero-home-1120\\.webp[^>]*>", RegexOptions.Singleline);
        Assert.True(shot.Success, "hero product image missing");
        Assert.Contains("hero-home-2240.webp", shot.Value, StringComparison.Ordinal);
        Assert.Contains("fetchpriority=\"high\"", shot.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("loading=\"lazy\"", shot.Value, StringComparison.Ordinal);
        Assert.Matches("width=\"\\d+\"", shot.Value);
        Assert.Matches("height=\"\\d+\"", shot.Value);
        Assert.Contains("alt=\"Trang Từ vựng của TCT English: mục tiêu hôm nay, từ cần ôn và lịch ôn 7 ngày\"", shot.Value, StringComparison.Ordinal);

        // The four floating chips are decoration: hidden from assistive technology, no invented figures.
        var chips = Regex.Matches(decoded, "<div class=\"lp-chip[^\"]*\"[^>]*>", RegexOptions.Singleline).Select(m => m.Value).ToList();
        Assert.Equal(4, chips.Count);
        Assert.All(chips, c => Assert.Contains("aria-hidden=\"true\"", c, StringComparison.Ordinal));
        Assert.DoesNotContain("XP", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("Chuỗi 5 ngày", decoded, StringComparison.Ordinal);

        // Both hero images exist as static files under the app.
        Assert.Contains("/images/landing/hero-home-1120.webp", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Landing_Demo_RendersQuestionOneAsPlainMarkupForVisitorsWithoutJavaScript()
    {
        var (_, html, decoded) = await GetLandingAsync(signedIn: false);

        var card = Regex.Match(decoded, "<div class=\"lp-quiz\".*?<div class=\"lp-quiz-result\"", RegexOptions.Singleline);
        Assert.True(card.Success, "demo card missing");
        Assert.Contains("Học thử · câu 1/3", card.Value, StringComparison.Ordinal);
        Assert.Contains("đàm phán", card.Value, StringComparison.Ordinal);
        foreach (var option in new[] { "accomplish", "negotiate", "schedule", "confirm" })
        {
            Assert.Contains(option, card.Value, StringComparison.Ordinal);
        }

        // No JavaScript: options are plain text (no dead buttons) and a real link leads to registration.
        var options = Regex.Match(card.Value, "<ul class=\"lp-quiz-options\".*?</ul>", RegexOptions.Singleline).Value;
        Assert.DoesNotContain("<button", options, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Anchors(card.Value), a => a.Href == "/Account/Register" && a.Text == "Bắt đầu miễn phí");

        // Accessibility hooks the script relies on: a labelled option group and a polite live region.
        Assert.Contains("role=\"group\" aria-label=\"Chọn đáp án đúng\"", card.Value, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", card.Value, StringComparison.Ordinal);

        // The pure core is loaded before the page script, both deferred, with a cache-busting version.
        Assert.Matches("(?s)/js/landing-core\\.js\\?v=[^\"]+\"[^>]*defer.*?/js/landing\\.js\\?v=[^\"]+\"[^>]*defer", html);

        // The demo adds no network call of its own: no endpoint, fetch target or form on the card.
        Assert.DoesNotContain("<form", card.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Landing_Demo_SignedInVisitorIsSentToTheDashboardInsteadOfRegistration()
    {
        var (_, _, decoded) = await GetLandingAsync(signedIn: true);

        Assert.Contains("data-lp-cta-href=\"/Home/Index\"", decoded, StringComparison.Ordinal);
        Assert.Contains("data-lp-cta-label=\"Vào trang học\"", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("/Account/Register", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Landing_Features_ShowTheLessonShotTheReviewLadderTheFourSkillsAndTheAiExample()
    {
        var (_, html, decoded) = await GetLandingAsync(signedIn: false);

        // Lesson card: real screenshot, lazy, with dimensions and an honest alt text.
        var lesson = Regex.Match(decoded, "<img\\b[^>]*lesson-card-780\\.webp[^>]*>", RegexOptions.Singleline);
        Assert.True(lesson.Success, "lesson screenshot missing");
        Assert.Contains("lesson-card-1560.webp", lesson.Value, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", lesson.Value, StringComparison.Ordinal);
        Assert.Matches("width=\"\\d+\"", lesson.Value);
        Assert.Matches("height=\"\\d+\"", lesson.Value);
        Assert.Contains("alt=\"Câu hỏi luyện tập trong một bài học từ vựng của TCT English\"", lesson.Value, StringComparison.Ordinal);

        // Review ladder: an ordered list the reader can follow as text, plus a spoken summary.
        var ladder = Regex.Match(decoded, "<ol class=\"lp-ladder\".*?</ol>", RegexOptions.Singleline).Value;
        var steps = Regex.Matches(ladder, "<li[^>]*>(.*?)</li>", RegexOptions.Singleline)
            .Select(m => Regex.Replace(m.Groups[1].Value, "<[^>]+>", " ").Replace("\r", " ").Replace("\n", " ").Trim()).ToList();
        Assert.Equal(new[] { "1 ngày", "3 ngày", "7 ngày", "14 ngày", "30 ngày", "60 ngày" }, steps.Select(s => Regex.Replace(s, "\\s+", " ")));
        Assert.Contains("Khoảng cách ôn: 1, 3, 7, 14, 30 rồi 60 ngày.", decoded, StringComparison.Ordinal);
        Assert.Contains("id=\"on-tap\"", decoded, StringComparison.Ordinal);

        // The four skills and the goal tiles carry labels only: no invented counts.
        foreach (var skill in new[] { "Nghe", "Nói", "Viết", "Đọc" })
        {
            Assert.Matches($"<h3[^>]*>{skill}</h3>", decoded);
        }
        foreach (var tile in new[] { "Chuỗi ngày học", "Mục tiêu mỗi ngày", "Huy hiệu" })
        {
            Assert.Contains(tile, decoded, StringComparison.Ordinal);
        }

        // AI example: static, labelled as an example, in an element with the right id for the nav link.
        Assert.Contains("id=\"tro-ly-ai\"", decoded, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Ví dụ hội thoại với trợ lý AI\"", decoded, StringComparison.Ordinal);
        Assert.Contains("“Deadline” và “due date” khác nhau thế nào?", decoded, StringComparison.Ordinal);

        // Only the ladder bar heights may use an inline style, and only as a custom property.
        var inlineStyles = Regex.Matches(html, "\\sstyle=\"([^\"]*)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.All(inlineStyles, s => Assert.StartsWith("--lp-h:", s, StringComparison.Ordinal));
        Assert.Equal(6, inlineStyles.Count);

        // Claims that are not true today must not come back.
        Assert.DoesNotContain("AI chấm bài", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("phụ đề song ngữ", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sáu kỹ năng", decoded, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Landing_Faq_HasFiveNativeDetailsWithTheFirstOpenAndOnlyVerifiedAnswers()
    {
        var (_, _, decoded) = await GetLandingAsync(signedIn: false);

        Assert.Contains("<h2", decoded, StringComparison.Ordinal);
        Assert.Matches("<h2[^>]*>Câu hỏi thường gặp</h2>", decoded);

        var items = Regex.Matches(decoded, "<details\\b[^>]*>.*?</details>", RegexOptions.Singleline).Select(m => m.Value).ToList();
        Assert.Equal(5, items.Count);
        Assert.Contains("open", Regex.Match(items[0], "<details\\b[^>]*>").Value, StringComparison.Ordinal);
        Assert.All(items.Skip(1), i => Assert.DoesNotContain("open", Regex.Match(i, "<details\\b[^>]*>").Value, StringComparison.Ordinal));

        var questions = items.Select(i => Regex.Replace(Regex.Match(i, "<summary[^>]*>(.*?)</summary>", RegexOptions.Singleline).Groups[1].Value, "<[^>]+>", " ").Trim())
            .Select(q => Regex.Replace(q, "\\s+", " ")).ToList();
        Assert.Equal(new[]
        {
            "TCT English có miễn phí không?",
            "Mỗi ngày cần học bao lâu?",
            "Lịch ôn tập hoạt động thế nào?",
            "Tôi mới bắt đầu thì học được không?",
            "Có cần cài ứng dụng không?",
        }, questions);

        // Answers match what the product does today (checked against Pricing.cshtml and the code in N6/N7).
        Assert.Contains("1, 3, 7, 14, 30 rồi 60 ngày", items[2], StringComparison.Ordinal);
        Assert.Contains("Mục tiêu từ vựng mỗi ngày hiện là 10 từ.", items[1], StringComparison.Ordinal);
        Assert.Contains("có hạn mức mỗi ngày", items[0], StringComparison.Ordinal);
        Assert.Contains("A1", items[3], StringComparison.Ordinal);
        Assert.DoesNotContain("không giới hạn", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mục tiêu mặc định", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Landing_FinalCallToActionAndFooter_HaveRealLinksAndNoSocialPlaceholders()
    {
        var (_, _, decoded) = await GetLandingAsync(signedIn: false);
        var anchors = Anchors(decoded).ToList();

        Assert.Contains("Học những từ đầu tiên ngay tối nay.", decoded, StringComparison.Ordinal);
        Assert.Contains("Tài khoản miễn phí, học ngay trên trình duyệt.", decoded, StringComparison.Ordinal);

        var footer = Regex.Match(decoded, "<footer\\b.*?</footer>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(footer);
        Assert.Contains("© 2026 TCT English", footer, StringComparison.Ordinal);
        Assert.Contains("Học từ vựng, nghe, nói, đọc và viết tiếng Anh theo bài ngắn, có lịch ôn tập tự động.", footer, StringComparison.Ordinal);
        Assert.DoesNotContain("nhân viên", footer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fa-facebook", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("fa-github", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("fa-youtube", decoded, StringComparison.Ordinal);

        var inFooter = Anchors(footer).ToDictionary(a => a.Text, a => a.Href, StringComparer.Ordinal);
        Assert.Equal("/Vocabulary", inFooter["Từ vựng"]);
        Assert.Equal("/Home/Listening", inFooter["Nghe"]);
        Assert.Equal("/Speaking", inFooter["Nói"]);
        Assert.Equal("/Home/Writing", inFooter["Viết"]);
        Assert.Equal("/Account/Login?returnUrl=%2FHome%2FReading", inFooter["Đọc"]);
        Assert.Equal("/Account/Login", inFooter["Đăng nhập"]);
        Assert.Equal("/Account/Register", inFooter["Tạo tài khoản"]);
        Assert.Equal("/Premium", inFooter["Gói Premium"]);
        Assert.Equal("/Home/Introduction", inFooter["Giới thiệu"]);
        Assert.Equal("/Home/Contact", inFooter["Liên hệ"]);
        Assert.Equal("/Home/Privacypolicy", inFooter["Quyền riêng tư"]);
        Assert.Equal("/Home/Termsofuse", inFooter["Điều khoản"]);
        Assert.All(anchors, a => Assert.False(string.IsNullOrWhiteSpace(a.Href) || a.Href == "#", $"dead link: {a.Text}"));
    }

    [Fact]
    public async Task Landing_SignedInFooterAndFinalCallToAction_LeadToTheDashboard()
    {
        var (_, _, decoded) = await GetLandingAsync(signedIn: true);
        var footer = Regex.Match(decoded, "<footer\\b.*?</footer>", RegexOptions.Singleline).Value;
        var inFooter = Anchors(footer).ToDictionary(a => a.Text, a => a.Href, StringComparer.Ordinal);

        Assert.Equal("/Home/Index", inFooter["Vào trang học"]);
        Assert.Equal("/Premium", inFooter["Gói Premium"]);
        Assert.Equal("/Home/Reading", inFooter["Đọc"]);
        Assert.DoesNotContain("Tạo tài khoản", footer, StringComparison.Ordinal);
        Assert.DoesNotContain("Đăng nhập", decoded, StringComparison.Ordinal);
        var end = Regex.Match(decoded, "<section class=\"lp-end\".*?</section>", RegexOptions.Singleline).Value;
        Assert.Contains(Anchors(end), a => a.Href == "/Home/Index" && a.Text == "Vào trang học");
    }

    [Fact]
    public async Task Landing_ShareImage_IsAnAbsoluteUrlToAnExistingSmallPng()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Home/Landing"));
        var image = Regex.Match(html, "<meta property=\"og:image\" content=\"([^\"]+)\"").Groups[1].Value;

        Assert.Matches("^https?://[^/]+/images/landing/og-landing\\.png$", image);
        Assert.Contains("property=\"og:image:width\" content=\"1200\"", html, StringComparison.Ordinal);
        Assert.Contains("property=\"og:image:height\" content=\"630\"", html, StringComparison.Ordinal);
        Assert.Contains($"<meta name=\"twitter:image\" content=\"{image}\"", html, StringComparison.Ordinal);

        using var file = await client.GetAsync(new Uri(image).AbsolutePath);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        var bytes = await file.Content.ReadAsByteArrayAsync();
        Assert.InRange(bytes.Length, 10_000, 300 * 1024);

        // PNG header, then the IHDR width and height: 1200 x 630.
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        Assert.Equal((1200, 630), (width, height));
    }

    [Fact]
    public async Task Landing_Stylesheet_KeepsTheMotionOptInAndTheNoScriptFallback()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var css = await client.GetStringAsync("/css/landing.css");
        var html = await client.GetStringAsync("/Home/Landing");

        // Every keyframe animation lives inside the no-preference block; nothing animates for reduced motion.
        var noPreference = css.IndexOf("@media (prefers-reduced-motion: no-preference)", StringComparison.Ordinal);
        Assert.True(noPreference >= 0, "no-preference block missing");
        Assert.True(css.IndexOf("@keyframes", StringComparison.Ordinal) > noPreference, "a @keyframes rule sits outside the no-preference block");
        Assert.DoesNotContain("!important", css, StringComparison.Ordinal);
        Assert.Contains("@media (forced-colors: active)", css, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);

        // Nothing is hidden until a script runs: no reveal classes, and no inline visibility styles.
        Assert.DoesNotContain("class=\"reveal", html, StringComparison.Ordinal);
        Assert.DoesNotContain("IntersectionObserver", await client.GetStringAsync("/js/landing.js"), StringComparison.Ordinal);
        Assert.DoesNotContain("opacity:0", html.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Landing_RootServesThePageAndAnonymousDashboardRedirectsToIt()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        // The default route (/) maps to the Landing action.
        using var root = await client.GetAsync("/");
        var rootHtml = WebUtility.HtmlDecode(await root.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Contains("Tiếng Anh ngấm dần,", rootHtml, StringComparison.Ordinal);

        // An anonymous dashboard request lands on the site root (the landing page).
        using var dashboard = await client.GetAsync("/Home/Index");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        var location = dashboard.Headers.Location!;
        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        Assert.Contains(path, new[] { "/", "/Home/Landing" });
    }
}
