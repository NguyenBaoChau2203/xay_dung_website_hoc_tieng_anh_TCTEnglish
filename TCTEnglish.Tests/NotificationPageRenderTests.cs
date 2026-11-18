using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// The rendered /Notification page (plan 5.1): one h1, day groups, filters with counts, real links and buttons,
/// empty states, the pager and the read-only reminder card. Text is asserted on the HTML-decoded page; Razor writes
/// non-ASCII letters as numeric entities.
/// </summary>
public sealed class NotificationPageRenderTests
{
    private const int Owner = TestDataIds.UserId;          // five notifications (four unread)
    private const int Empty = TestDataIds.MemberUserId;    // none
    private const string PageStart = "<div class=\"tct-theme tct-page tn-page\"";
    private const string PageEnd = "id=\"tnToastText\"";   // the last element of the page wrapper

    private static DateTime VietnamToUtc(DateTime local)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), NotificationDisplay.BusinessZone);

    private static DateTime VietnamToday
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NotificationDisplay.BusinessZone).Date;

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync(
        Action<IServiceCollection>? configure = null, Func<DbflashcardContext, Task>? seed = null)
    {
        var factory = new TestWebApplicationFactory(configure);
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        db.Notifications.AddRange(
            new Notification
            {
                UserId = Owner, Type = NotificationType.PremiumActivated, Title = "PAY-TODAY",
                Message = "Gói Premium năm có hiệu lực đến 17/11/2027.", RelatedUrl = "/Billing/Order/TCT-1",
                IconClass = "fa-solid fa-crown", CreatedAt = VietnamToUtc(VietnamToday)
            },
            new Notification
            {
                UserId = Owner, Type = NotificationType.GoalCompleted, Title = "GOAL-YESTERDAY",
                Message = "Goal message", RelatedUrl = "/Goals", IsRead = true,
                CreatedAt = VietnamToUtc(VietnamToday.AddDays(-1).AddHours(12))
            },
            new Notification
            {
                UserId = Owner, Type = NotificationType.AdminAnnouncement, Title = "SYS-EARLIER",
                Message = "System message", RelatedUrl = null, CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new Notification
            {
                UserId = Owner, Type = NotificationType.StreakRecord, Title = "🔥 Kỷ lục mới!",
                Message = "Streak message", IconClass = "fas fa-fire\" onclick=\"alert(1)",
                RelatedUrl = "//evil.example/steal", CreatedAt = DateTime.UtcNow.AddDays(-11)
            },
            new Notification
            {
                UserId = Owner, Type = NotificationType.BadgeEarned, Title = "<script>alert(1)</script>",
                Message = "<img src=x onerror=alert(2)>", RelatedUrl = "/Goals#badges", CreatedAt = DateTime.UtcNow.AddDays(-12)
            });
        if (seed != null)
        {
            await seed(db);
        }

        await db.SaveChangesAsync();
        return factory;
    }

    private static async Task<(string Html, string Raw)> GetPageAsync(TestWebApplicationFactory factory, string url, int user = Owner)
    {
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, user, Roles.Standard);
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        return (WebUtility.HtmlDecode(raw), raw);
    }

    /// <summary>The page block from the root wrapper to its last element (excludes the shell around it).</summary>
    private static string PageBlock(string html)
    {
        var start = html.IndexOf(PageStart, StringComparison.Ordinal);
        Assert.True(start >= 0, "page root not found");
        var end = html.IndexOf(PageEnd, start, StringComparison.Ordinal);
        Assert.True(end > start, "page script marker not found");
        return html[start..end];
    }

    private static string ItemOf(string block, string title)
    {
        var item = block.Split("<li class=\"tn-item", StringSplitOptions.None).Skip(1)
            .FirstOrDefault(p => p.Contains(title, StringComparison.Ordinal));
        Assert.NotNull(item);
        return item![..item.IndexOf("</li>", StringComparison.Ordinal)];
    }

    [Fact]
    public async Task Page_HasOneH1_TheClayWrapper_AndNoInlineScriptStyleOrFakeButton()
    {
        await using var factory = await CreateFactoryAsync();
        var (html, raw) = await GetPageAsync(factory, "/Notification");
        var block = PageBlock(html);
        // Structure is checked on the raw markup: the decoded text of a notification can legitimately contain "<script".
        var rawBlock = PageBlock(raw);

        Assert.Single(Regex.Matches(rawBlock, "<h1[ >]"));
        Assert.Contains(">Thông báo</h1>", block);
        Assert.DoesNotContain("style=", rawBlock);
        Assert.DoesNotMatch("<script(?![^>]*\\bsrc=)", rawBlock);
        Assert.DoesNotContain("role=\"button\"", rawBlock);
        Assert.DoesNotContain("<div role=", rawBlock);
        Assert.Contains("/css/notification-page.css", html);
        Assert.Contains("/js/notification-page.js", html);
        Assert.Contains("/js/notification-core.js", html);
        // The page adds no antiforgery token of its own; the shell's notification panel supplies the one the script reads.
        Assert.DoesNotContain("__RequestVerificationToken", rawBlock);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
    }

    [Fact]
    public async Task Page_GroupsByDay_InVietnamTime_AndOmitsEmptyGroups()
    {
        await using var factory = await CreateFactoryAsync();
        var (html, _) = await GetPageAsync(factory, "/Notification");
        var block = PageBlock(html);

        var today = block.IndexOf("id=\"tnGroup-today\"", StringComparison.Ordinal);
        var yesterday = block.IndexOf("id=\"tnGroup-yesterday\"", StringComparison.Ordinal);
        var earlier = block.IndexOf("id=\"tnGroup-earlier\"", StringComparison.Ordinal);
        Assert.True(today > 0 && yesterday > today && earlier > yesterday, "groups out of order");
        Assert.Contains(">Hôm nay</h2>", block);
        Assert.Contains(">Hôm qua</h2>", block);
        Assert.Contains(">Trước đó</h2>", block);
        var pay = block.IndexOf("PAY-TODAY", StringComparison.Ordinal);
        Assert.True(pay > today && pay < yesterday, "today's item belongs to the first group");
        Assert.Contains("Hôm qua, 12:00", block);   // the same text the bell and the API show

        var (onlyPayment, _) = await GetPageAsync(factory, "/Notification?category=payment");
        var paymentBlock = PageBlock(onlyPayment);
        Assert.Contains("tnGroup-today", paymentBlock);
        Assert.DoesNotContain("tnGroup-yesterday", paymentBlock);
        Assert.DoesNotContain("tnGroup-earlier", paymentBlock);
    }

    [Fact]
    public async Task Item_WithALink_HasARealAnchor_AndAnUnreadCardHasADotAndAButton()
    {
        await using var factory = await CreateFactoryAsync();
        var (html, _) = await GetPageAsync(factory, "/Notification");
        var block = PageBlock(html);

        var pay = ItemOf(block, "PAY-TODAY");
        Assert.Contains("is-unread", pay);
        Assert.Contains("<a class=\"tn-item__link\" href=\"/Billing/Order/TCT-1\" data-notification-link>Xem biên nhận", pay);
        Assert.Contains("<span class=\"visually-hidden\">Chưa đọc</span>", pay);
        Assert.Contains("data-notification-read", pay);
        Assert.Contains("aria-label=\"Đánh dấu đã đọc: PAY-TODAY\"", pay);
        Assert.Contains("tn-tone--accent", pay);
        Assert.Contains("fa-solid fa-crown", pay);
        Assert.Contains("<time datetime=\"", pay);

        var goal = ItemOf(block, "GOAL-YESTERDAY");   // already read
        Assert.DoesNotContain("is-unread", goal);
        Assert.DoesNotContain("data-notification-read", goal);
        Assert.DoesNotContain("Chưa đọc", goal);
        Assert.Contains("Xem mục tiêu", goal);
    }

    [Fact]
    public async Task Item_WithoutALink_IsAStaticCard_WithOnlyItsOwnMarkReadButton()
    {
        await using var factory = await CreateFactoryAsync();
        var (html, _) = await GetPageAsync(factory, "/Notification");

        var sys = ItemOf(PageBlock(html), "SYS-EARLIER");

        Assert.DoesNotContain("<a ", sys);
        Assert.DoesNotContain("data-notification-link", sys);
        Assert.Contains("data-notification-read", sys);
        Assert.Contains("tn-tone--primary", sys);
    }

    [Fact]
    public async Task Item_UnsafeData_IsNeverRenderedAsLinkOrMarkup()
    {
        await using var factory = await CreateFactoryAsync();
        var (html, raw) = await GetPageAsync(factory, "/Notification");

        var streak = ItemOf(PageBlock(html), "Kỷ lục mới!");
        Assert.DoesNotContain("🔥", streak);            // the emoji is cleaned from the title
        Assert.DoesNotContain("evil.example", streak);   // the //host link is dropped
        Assert.DoesNotContain("<a ", streak);
        Assert.DoesNotContain("onclick", streak);        // the bad stored icon falls back to the default
        Assert.Contains("fa-solid fa-fire", streak);

        // Titles and messages are encoded, never injected as markup.
        var rawBlock = PageBlock(raw);
        Assert.DoesNotContain("<script>alert(1)", rawBlock);
        Assert.DoesNotContain("<img src=x", rawBlock);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", rawBlock);
    }

    [Fact]
    public async Task Filters_MarkTheActiveOnesAndShowTheCounts()
    {
        await using var factory = await CreateFactoryAsync();

        var (all, _) = await GetPageAsync(factory, "/Notification");
        var allBlock = PageBlock(all);
        Assert.Contains("aria-label=\"Lọc thông báo\"", allBlock);
        Assert.Matches("<a class=\"tn-chip\" href=\"[^\"]*\"\\s+aria-current=\"true\">Tất cả</a>", allBlock);
        Assert.Contains("data-count=\"unread\" >4<", allBlock);   // 4 unread of 5
        Assert.Contains("data-count=\"badge\" >1<", allBlock);
        Assert.Contains("data-count=\"payment\" >1<", allBlock);
        Assert.Contains("data-count=\"system\" >1<", allBlock);
        Assert.Contains("data-count=\"learning\" >1<", allBlock);

        var (unread, _) = await GetPageAsync(factory, "/Notification?filter=unread");
        var unreadBlock = PageBlock(unread);
        Assert.Matches("(?s)aria-current=\"true\">\\s*Chưa đọc", unreadBlock);
        Assert.DoesNotContain("GOAL-YESTERDAY", unreadBlock);   // read items drop out of the unread filter

        var (both, _) = await GetPageAsync(factory, "/Notification?filter=unread&category=badge");
        var bothBlock = PageBlock(both);
        Assert.Matches("(?s)aria-current=\"true\">\\s*Chưa đọc", bothBlock);
        Assert.Matches("(?s)aria-current=\"true\">\\s*Huy hiệu", bothBlock);   // the two axes combine
        Assert.DoesNotContain("aria-current=\"true\">Tất cả", bothBlock);
    }

    [Fact]
    public async Task HeaderSummary_AndTheMarkAllButton_FollowTheUnreadCount()
    {
        await using var factory = await CreateFactoryAsync();
        var (html, _) = await GetPageAsync(factory, "/Notification");
        var block = PageBlock(html);

        Assert.Contains("Bạn có 4 thông báo chưa đọc.", block);
        Assert.Matches("id=\"tnMarkAll\"\\s+aria-label=\"Đánh dấu tất cả đã đọc\"\\s*>", block);   // visible: no hidden attribute

        var (none, _) = await GetPageAsync(factory, "/Notification", Empty);
        var emptyBlock = PageBlock(none);
        Assert.Contains("Bạn đã đọc hết thông báo.", emptyBlock);
        Assert.Matches("id=\"tnMarkAll\"[^>]*\\shidden", emptyBlock);
    }

    [Fact]
    public async Task EmptyStates_HaveTheirOwnTextAndNextStep()
    {
        await using var factory = await CreateFactoryAsync();

        var (none, _) = await GetPageAsync(factory, "/Notification", Empty);
        var noneBlock = PageBlock(none);
        Assert.Contains("Chưa có thông báo nào</h2>", noneBlock);
        Assert.Contains("Khi bạn học, đạt mục tiêu hay nhận huy hiệu, thông báo sẽ hiện ở đây.", noneBlock);
        Assert.Contains(">Bắt đầu học</a>", noneBlock);

        var (unreadEmpty, _) = await GetPageAsync(factory, "/Notification?filter=unread", Empty);
        var unreadBlock = PageBlock(unreadEmpty);
        Assert.Contains("Không còn thông báo chưa đọc", unreadBlock);
        Assert.Contains("Mọi thông báo đều đã được đọc. Hãy học tiếp để giữ chuỗi ngày học.", unreadBlock);
        Assert.Contains(">Học tiếp</a>", unreadBlock);
        Assert.Contains(">Xem tất cả thông báo</a>", unreadBlock);
        Assert.DoesNotMatch("id=\"tnEmptyUnread\"[^>]*\\shidden", unreadBlock);   // visible

        var (category, _) = await GetPageAsync(factory, "/Notification?category=system", Empty);
        Assert.Contains("Chưa có thông báo nào trong mục này.", PageBlock(category));
    }

    [Fact]
    public async Task UnreadFilterWithItems_CarriesAHiddenEmptyState_ForMarkAll()
    {
        await using var factory = await CreateFactoryAsync();

        var (html, _) = await GetPageAsync(factory, "/Notification?filter=unread");
        var block = PageBlock(html);

        Assert.Matches("id=\"tnEmptyUnread\"[^>]*\\shidden", block);
        Assert.Contains("id=\"tnLists\"", block);
    }

    [Fact]
    public async Task Pager_ShowsRealLinks_ForManyNotifications()
    {
        await using var factory = await CreateFactoryAsync(seed: db =>
        {
            for (var i = 0; i < 30; i++)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = Owner, Type = NotificationType.GoalProgress, Title = $"Many {i:00}", Message = "m",
                    CreatedAt = DateTime.UtcNow.AddDays(-20).AddMinutes(-i)
                });
            }

            return Task.CompletedTask;
        });

        var (first, _) = await GetPageAsync(factory, "/Notification");
        var firstBlock = PageBlock(first);
        Assert.Contains("aria-label=\"Phân trang thông báo\"", firstBlock);
        Assert.Contains("<span class=\"page-link\" aria-label=\"Trang trước\" aria-disabled=\"true\">", firstBlock);
        Assert.Matches("<a class=\"page-link\" aria-label=\"Trang sau\" href=\"[^\"]*page=2", firstBlock);

        var (second, _) = await GetPageAsync(factory, "/Notification?page=2");
        var secondBlock = PageBlock(second);
        Assert.Matches("aria-label=\"Trang 2\" aria-current=\"page\"", secondBlock);
        Assert.Matches("<a class=\"page-link\" aria-label=\"Trang trước\" href=\"[^\"]*page=1", secondBlock);
    }

    [Fact]
    public async Task SidePanel_ShowsTheStoredReminderValues_AndLinksToTheAccountSettings()
    {
        await using var factory = await CreateFactoryAsync(seed: db =>
        {
            db.UserNotificationPreferences.Add(new UserNotificationPreference
            {
                UserId = Owner, DailyReminderEnabled = true, ReminderMinutes = 20 * 60 + 30,
                PushOptIn = true, WeeklyEmailOptIn = false, UpdatedAtUtc = DateTime.UtcNow
            });
            return Task.CompletedTask;
        });

        var (html, _) = await GetPageAsync(factory, "/Notification");
        var block = PageBlock(html);

        Assert.Contains(">Nhắc học</h2>", block);
        Assert.Matches("(?s)Nhắc học mỗi ngày</dt>\\s*<dd class=\"tn-setting__value\">Bật · 20:30</dd>", block);
        Assert.Matches("(?s)Thông báo trên trình duyệt</dt>\\s*<dd class=\"tn-setting__value\">Bật</dd>", block);
        Assert.Matches("(?s)Email tổng kết tuần</dt>\\s*<dd class=\"tn-setting__value\">Tắt</dd>", block);
        Assert.Contains("href=\"/Account/Profile#section-notifications\"", block);
        Assert.Contains("Đổi cài đặt", block);
        Assert.Contains("Bạn sẽ nhận thông báo khi", block);
        Assert.Contains("Thanh toán Premium thành công", block);
        Assert.Contains("Thông báo đã đọc được giữ 30 ngày.", block);
    }

    [Fact]
    public async Task SidePanel_WithNoSavedPreferences_ShowsTheOffValues()
    {
        await using var factory = await CreateFactoryAsync();

        var (html, _) = await GetPageAsync(factory, "/Notification");

        Assert.Matches("(?s)Nhắc học mỗi ngày</dt>\\s*<dd class=\"tn-setting__value\">Tắt</dd>", PageBlock(html));
    }

    [Fact]
    public async Task SidePanel_WhenTheSettingsReadFails_KeepsOnlyTheButton_AndThePageStillRenders()
    {
        var failing = new Mock<INotificationPreferenceService>();
        failing.Setup(p => p.ReadAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("preferences are down"));
        await using var factory = await CreateFactoryAsync(configure: services =>
        {
            services.RemoveAll<INotificationPreferenceService>();
            services.AddSingleton(failing.Object);
        });

        var (html, _) = await GetPageAsync(factory, "/Notification");
        var block = PageBlock(html);

        Assert.Contains("PAY-TODAY", block);
        Assert.DoesNotContain("Nhắc học mỗi ngày", block);
        Assert.Contains("Đổi cài đặt", block);
        Assert.Contains("href=\"/Account/Profile#section-notifications\"", block);
    }
}
