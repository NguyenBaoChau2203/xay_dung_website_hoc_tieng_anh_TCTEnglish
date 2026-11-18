using System.Net;
using System.Text.RegularExpressions;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// The bell in the shared layout and the panel partial (plan 5.2, decision D1): accessible button, hidden badges
/// without inline style, a labelled non-modal dialog with tabs, and the script order the panel depends on.
/// </summary>
public sealed class NotificationBellRenderTests
{
    private static async Task<string> GetShellAsync(string url = "/Notification")
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Between(string html, string start, string end)
    {
        var from = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"'{start}' not found");
        var to = html.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, $"'{end}' not found after '{start}'");
        return html[from..to];
    }

    [Fact]
    public async Task BellButton_IsAnAccessibleDisclosure_WithAHiddenBadgeAndNoInlineStyle()
    {
        var html = await GetShellAsync();

        var bell = WebUtility.HtmlDecode(Between(html, "<div class=\"notif-wrapper\">", "<div class=\"notif-panel\""));

        Assert.Matches("id=\"notificationBtn\"", bell);
        Assert.Contains("aria-label=\"Thông báo\"", bell);
        Assert.Contains("aria-haspopup=\"true\"", bell);
        Assert.Contains("aria-expanded=\"false\"", bell);
        Assert.Contains("aria-controls=\"notifPanel\"", bell);
        Assert.DoesNotContain("aria-haspopup=\"dialog\"", bell);
        Assert.DoesNotContain("style=", bell);
        // The badge is hidden with the attribute and not read twice (the button's name carries the count).
        Assert.Matches("<span class=\"notification-badge\" id=\"notificationBadge\" aria-hidden=\"true\" hidden>\\s*</span>", bell);
    }

    [Fact]
    public async Task SidebarBadge_HasNoInlineStyle_AndIsHiddenByDefault()
    {
        var html = await GetShellAsync();

        Assert.Matches("<span class=\"notification-badge notification-sidebar-badge\" id=\"sidebarNotifBadge\" aria-hidden=\"true\" hidden></span>", html);
        Assert.DoesNotMatch("id=\"sidebarNotifBadge\"[^>]*style=", html);
    }

    [Fact]
    public async Task Panel_IsALabelledNonModalDialog_WithTabsAListAndAFooterLink()
    {
        var html = await GetShellAsync();
        var panel = Between(html, "<div class=\"notif-panel\"", "id=\"notifStatus\"");

        Assert.Contains("role=\"dialog\"", panel);
        Assert.Contains("aria-modal=\"false\"", panel);
        Assert.Contains("aria-labelledby=\"notifTitle\"", panel);
        Assert.Matches("<h2 class=\"notif-header__title\" id=\"notifTitle\" tabindex=\"-1\">", panel);
        Assert.Contains("role=\"tablist\"", panel);
        Assert.Equal(2, Regex.Matches(panel, "role=\"tab\"").Count);
        Assert.Matches("id=\"notifTabAll\"\\s+aria-selected=\"true\"", panel);
        Assert.Matches("id=\"notifTabUnread\"\\s+aria-selected=\"false\" aria-controls=\"notifList\" tabindex=\"-1\"", panel);
        Assert.Contains("role=\"tabpanel\"", panel);
        Assert.Contains("id=\"notifLoadMoreBtn\" type=\"button\" hidden", panel);
        Assert.Contains("id=\"notifMarkAllBtn\" type=\"button\" hidden", panel);
        Assert.Matches("href=\"/Notification(/Index)?\">", panel);
        Assert.DoesNotContain("style=", panel);
        Assert.DoesNotContain("<h6", panel);
    }

    [Fact]
    public async Task PanelScripts_LoadTheCoreBeforeTheBellScript()
    {
        var html = await GetShellAsync();

        var core = html.IndexOf("/js/notification-core.js", StringComparison.Ordinal);
        var bell = html.IndexOf("/js/notifications.js", StringComparison.Ordinal);

        Assert.True(core > 0 && bell > core, "notification-core.js must come before notifications.js");
        Assert.Single(Regex.Matches(html, "/js/notification-core\\.js"));   // loaded once, also on the notification page
    }

    [Fact]
    public async Task TheBell_IsTheSameOnAnotherPage()
    {
        var html = await GetShellAsync("/Goals");

        Assert.Contains("id=\"notifPanel\"", html);
        Assert.Contains("aria-controls=\"notifPanel\"", html);
        Assert.True(html.IndexOf("/js/notification-core.js", StringComparison.Ordinal)
                    < html.IndexOf("/js/notifications.js", StringComparison.Ordinal));
    }
}
