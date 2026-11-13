using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Verifies the HTTP/Razor rendering and accessibility/security contracts for Account screens AU01-AU07.
/// Ensures single visible H1, form helpers, antiforgery tokens, hidden fields, and safe HTML encoding.
/// </summary>
public sealed class AccountUiRenderTests
{
    private static string Text(string html) => WebUtility.HtmlDecode(html);

    [Theory]
    [InlineData("/Account/Settings")]
    [InlineData("/Account/Profile?tab=security")]
    public async Task Settings_AnonymousVisitorsMustSignIn(string route)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AU01_Auth_RendersSemanticForms_Antiforgery_AndOneH1()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("account-auth-ui", html);
        Assert.Single(Regex.Matches(html, "<h1[\\s>]"));
        Assert.Contains("id=\"form-login\"", html);
        Assert.Contains("id=\"form-register\"", html);
        Assert.Contains("id=\"tab-login\"", html);
        Assert.Contains("id=\"tab-register\"", html);
        Assert.Contains("id=\"btn-login-submit\"", html);
        Assert.Contains("id=\"btn-register-submit\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("id=\"btn-google\"", html);
        Assert.Contains("id=\"btn-facebook\"", html);
    }

    [Fact]
    public async Task AU02_ForgotPassword_PreservesHtmlHelper_AndFormBinding()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync("/Account/ForgotPassword");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Single(Regex.Matches(html, "<h1[\\s>]"));
        Assert.Contains("id=\"form-forgot-password\"", html);
        Assert.Contains("id=\"btn-forgot-submit\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("name=\"Email\"", html);
        Assert.DoesNotContain("onsubmit=", html);
    }

    [Fact]
    public async Task AU03_ForgotPasswordConfirmation_RendersSemanticMarkup_AndOneH1()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var antiForgery = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Account/ForgotPassword");

        var form = new Dictionary<string, string>
        {
            ["Email"] = "learner@example.local",
            ["__RequestVerificationToken"] = antiForgery
        };
        using var postResponse = await client.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var html = await postResponse.Content.ReadAsStringAsync();

        Assert.Contains("account-auth-ui", html);
        Assert.Contains("recovery-card", html);
        Assert.Contains("recovery-hint", html);
        Assert.Single(Regex.Matches(html, "<h1[\\s>]"));
        Assert.Contains("Hãy kiểm tra email của bạn", Text(html));
        Assert.Contains("/Account/Login", html);
        Assert.Contains("/Account/ForgotPassword", html);
    }

    [Fact]
    public async Task AU04_ResetPassword_RetainsHiddenTokens_AndValidationSummary()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync("/Account/ResetPassword?token=test-token-123&email=test%40example.local");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Single(Regex.Matches(html, "<h1[\\s>]"));
        Assert.Contains("id=\"form-reset-password\"", html);
        Assert.Contains("id=\"btn-reset-submit\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("name=\"Token\"", html);
        Assert.Contains("name=\"Email\"", html);
        Assert.Contains("name=\"NewPassword\"", html);
        Assert.Contains("name=\"ConfirmPassword\"", html);
        Assert.Contains("test-token-123", html);
        Assert.DoesNotContain("onsubmit=", html);
    }

    [Fact]
    public async Task AU04_ResetPassword_WhenTokenInvalidOrExpired_RendersDirectLinkToForgotPassword()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        var antiForgery = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Account/ResetPassword?token=invalid-token&email=test%40example.local");

        var form = new Dictionary<string, string>
        {
            ["Email"] = "nonexistent@example.local",
            ["Token"] = "invalid-token",
            ["NewPassword"] = "NewPassword123",
            ["ConfirmPassword"] = "NewPassword123",
            ["__RequestVerificationToken"] = antiForgery
        };
        using var postResponse = await client.PostAsync("/Account/ResetPassword", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var html = await postResponse.Content.ReadAsStringAsync();

        Assert.Contains("/Account/ForgotPassword", html);
        Assert.Contains("Liên kết đặt lại mật khẩu không hợp lệ", html);
    }

    [Fact]
    public async Task AU05_Profile_RendersIdentity_Badges_AndAccountNav()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var userId = TestDataIds.UserId;
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, Roles.Standard);

        using var response = await client.GetAsync("/Account/Profile");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("account-ui", html);
        Assert.Single(Regex.Matches(html, "<h1[\\s>]"));
        Assert.Contains("id=\"accountSidebar\"", html);
        Assert.Contains("id=\"accountSidebarToggle\"", html);
        Assert.Contains("id=\"sidebarMobileToggle\"", html);
        Assert.Contains("id=\"avatarClickTarget\"", html);
        Assert.Contains("id=\"avatarFallback\"", html);
    }

    [Fact]
    public async Task AU06_Settings_RendersSections_AndSavedNotificationPreferences()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var userId = TestDataIds.UserId;
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, Roles.Standard);

        using var legacyResponse = await client.GetAsync("/Account/Settings");
        Assert.Equal(HttpStatusCode.Redirect, legacyResponse.StatusCode);
        Assert.Equal("/Account/Profile?tab=security", legacyResponse.Headers.Location?.OriginalString);

        using var response = await client.GetAsync(legacyResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("account-ui", html);
        Assert.Single(Regex.Matches(html, "<h1[\\s>]"));
        Assert.Contains("data-initial-tab=\"security\"", html);
        Assert.Contains("id=\"tab-security\"", html);
        Assert.Contains("id=\"section-linked\"", html);
        Assert.Contains("id=\"section-notifications\"", html);
        Assert.Contains("id=\"section-danger\"", html);
        Assert.Contains("id=\"profileEmailInput\"", html);
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
        var headerSettings = document.QuerySelector("#userDropdown .item-icon-box--settings")?.Closest("a");
        Assert.NotNull(headerSettings);
        Assert.Equal("/Account/Profile?tab=security", headerSettings.GetAttribute("href"));
        Assert.Contains("id=\"deleteAccountModal\"", html);
        Assert.Contains("id=\"deleteConfirmInput\"", html);
        Assert.Contains("id=\"deleteAccountBtn\"", html);
        Assert.Contains("id=\"prefPush\"", html);
        Assert.Contains("id=\"prefEmail\"", html);
        Assert.Contains("SaveNotificationPreferences", html);
        Assert.DoesNotContain("<fieldset disabled", html);
    }

    [Fact]
    public async Task AU07_AccessDenied_Renders403Explanation_AndSafeNavigation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var userId = TestDataIds.UserId;
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, Roles.Standard);

        using var response = await client.GetAsync("/Account/AccessDenied");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("account-ui", html);
        Assert.Single(Regex.Matches(html, "<h1[\\s>]"));
        Assert.Contains("Bạn không có quyền truy cập trang này", Text(html));
        Assert.Contains("/Account/Profile", html);
    }
}
