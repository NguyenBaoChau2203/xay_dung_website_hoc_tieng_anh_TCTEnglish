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
