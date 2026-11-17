using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Pure display rules shared by the notification page, the bell panel and the JSON API.
/// Reference "now" is 2026-11-17 10:00 UTC = 17:00 in Vietnam (a Tuesday).
/// </summary>
public sealed class NotificationDisplayTests
{
    private static readonly TimeZoneInfo Zone = NotificationDisplay.BusinessZone;

    private static DateTime Utc(string iso) => DateTimeOffset.Parse(iso).UtcDateTime;

    private static Notification Make(
        NotificationType type,
        string title = "Tiêu đề",
        string message = "Nội dung",
        string? url = null,
        string? icon = null,
        bool isRead = false,
        string created = "2026-11-17T09:00:00Z") => new()
        {
            Id = 7,
            UserId = 1,
            Type = type,
            Title = title,
            Message = message,
            RelatedUrl = url,
            IconClass = icon,
            IsRead = isRead,
            // Stored values come back from the database with an unspecified kind.
            CreatedAt = DateTime.SpecifyKind(Utc(created), DateTimeKind.Unspecified)
        };

    // ── Category, tone and default icon ───────────────────────────────

    [Theory]
    [InlineData(NotificationType.StreakWarning, NotificationCategory.Learning, "streak", "fa-solid fa-fire-flame-curved")]
    [InlineData(NotificationType.StreakRecord, NotificationCategory.Learning, "streak", "fa-solid fa-fire")]
    [InlineData(NotificationType.GoalProgress, NotificationCategory.Learning, "success", "fa-solid fa-bullseye")]
    [InlineData(NotificationType.GoalCompleted, NotificationCategory.Learning, "success", "fa-solid fa-bullseye")]
    [InlineData(NotificationType.BadgeEarned, NotificationCategory.Badge, "warning", "fa-solid fa-medal")]
    [InlineData(NotificationType.BadgeNear, NotificationCategory.Badge, "warning", "fa-solid fa-medal")]
    [InlineData(NotificationType.PremiumActivated, NotificationCategory.Payment, "accent", "fa-solid fa-crown")]
    [InlineData(NotificationType.AdminAnnouncement, NotificationCategory.System, "primary", "fa-solid fa-bullhorn")]
    public void Build_MapsEveryType_ToCategoryToneAndDefaultIcon(
        NotificationType type, NotificationCategory category, string tone, string icon)
    {
        var item = NotificationDisplay.Build(Make(type), Utc("2026-11-17T10:00:00Z"), Zone);

        Assert.Equal(category, NotificationDisplay.CategoryOf(type));
        Assert.Equal(category.ToString().ToLowerInvariant(), item.Category);
        Assert.Equal(tone, item.Tone);
        Assert.Equal(icon, item.IconClass);
    }

    [Fact]
    public void Build_UnknownType_FallsBackToTheSystemCategoryAndABell()
    {
        var item = NotificationDisplay.Build(Make((NotificationType)99), Utc("2026-11-17T10:00:00Z"), Zone);

        Assert.Equal(NotificationCategory.System, NotificationDisplay.CategoryOf((NotificationType)99));
        Assert.Equal("system", item.Category);
        Assert.Equal("primary", item.Tone);
        Assert.Equal("fa-solid fa-bell", item.IconClass);
    }

    [Theory]
    [InlineData("fas fa-fire", "fas fa-fire")]
    [InlineData("fa-solid fa-trophy", "fa-solid fa-trophy")]
    [InlineData("far fa-star", "far fa-star")]
    [InlineData("fa-regular fa-heart", "fa-regular fa-heart")]
    public void Build_KeepsAStoredIconThatMatchesTheAllowedShape(string stored, string expected)
    {
        var item = NotificationDisplay.Build(
            Make(NotificationType.BadgeEarned, icon: stored), Utc("2026-11-17T10:00:00Z"), Zone);

        Assert.Equal(expected, item.IconClass);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fas fa-fire\" onclick=\"alert(1)")]
    [InlineData("<i class=x>")]
    [InlineData("fas")]
    [InlineData("fa-brands fa-github")]
    [InlineData("fas fa-Fire")]
    [InlineData("fas fa-fire extra")]
    public void Build_ReplacesABadStoredIconWithTheDefault(string stored)
    {
        var item = NotificationDisplay.Build(
            Make(NotificationType.BadgeEarned, icon: stored), Utc("2026-11-17T10:00:00Z"), Zone);

        Assert.Equal("fa-solid fa-medal", item.IconClass);
    }

    // ── Title cleaning ─────────────────────────────────────────────────

    [Theory]
    [InlineData("🔥 Kỷ lục mới!", "Kỷ lục mới!")]
    [InlineData("🎯 Hoàn thành mục tiêu!", "Hoàn thành mục tiêu!")]
    [InlineData("📈 Gần đến đích rồi!", "Gần đến đích rồi!")]
    [InlineData("🏅 Huy hiệu mới!", "Huy hiệu mới!")]
    [InlineData("⚠️ Streak sắp mất!", "Streak sắp mất!")]
    [InlineData("🔜 Gần mở khóa huy hiệu!", "Gần mở khóa huy hiệu!")]
    [InlineData("🔥🔥  Hai biểu tượng", "Hai biểu tượng")]
    [InlineData("  Khoảng trắng đầu", "Khoảng trắng đầu")]
    public void CleanTitle_StripsLeadingEmojiSymbolsAndSpaces(string raw, string expected)
    {
        Assert.Equal(expected, NotificationDisplay.CleanTitle(raw));
    }

    [Theory]
    [InlineData("Đạt 100% mục tiêu")]
    [InlineData("123 từ mới")]
    [InlineData("[Thử nghiệm] Bảo trì hệ thống")]
    [InlineData("\"Bền bỉ\" đã mở khóa")]
    [InlineData("Ôn tập hôm nay 🔥")]
    [InlineData("Ăn mừng chuỗi ngày học")]
    public void CleanTitle_KeepsLettersDigitsPunctuationAndTrailingEmoji(string raw)
    {
        Assert.Equal(raw, NotificationDisplay.CleanTitle(raw));
    }

    [Theory]
    [InlineData("🎉", "🎉")]
    [InlineData("  ", "")]
    [InlineData("", "")]
    public void CleanTitle_NeverReturnsAnEmptyTitleForNonBlankInput(string raw, string expected)
    {
        Assert.Equal(expected, NotificationDisplay.CleanTitle(raw));
    }

    [Fact]
    public void CleanTitle_NullIsEmpty()
    {
        Assert.Equal(string.Empty, NotificationDisplay.CleanTitle(null!));
    }

    [Fact]
    public void Build_UsesTheCleanTitle()
    {
        var item = NotificationDisplay.Build(
            Make(NotificationType.StreakRecord, title: "🔥 Kỷ lục mới!"), Utc("2026-11-17T10:00:00Z"), Zone);

        Assert.Equal("Kỷ lục mới!", item.Title);
    }

    // ── Time text (Vietnam time) ───────────────────────────────────────

    [Theory]
    // Same instant, a few seconds ago, and a small clock skew in the future.
    [InlineData("2026-11-17T10:00:00Z", "2026-11-17T09:59:30Z", "Vừa xong")]
    [InlineData("2026-11-17T10:00:00Z", "2026-11-17T10:00:40Z", "Vừa xong")]
    // Minutes.
    [InlineData("2026-11-17T10:00:00Z", "2026-11-17T09:59:00Z", "1 phút trước")]
    [InlineData("2026-11-17T10:00:00Z", "2026-11-17T09:15:00Z", "45 phút trước")]
    // Hours on the same Vietnam day (17:00 now; 14:30 and 00:30 are today).
    [InlineData("2026-11-17T10:00:00Z", "2026-11-17T07:30:00Z", "2 giờ trước")]
    [InlineData("2026-11-17T10:00:00Z", "2026-11-16T17:30:00Z", "16 giờ trước")]
    // Yesterday in Vietnam: 2026-11-16 23:50 VN.
    [InlineData("2026-11-17T10:00:00Z", "2026-11-16T16:50:00Z", "Hôm qua, 23:50")]
    [InlineData("2026-11-17T10:00:00Z", "2026-11-16T03:00:00Z", "Hôm qua, 10:00")]
    // Across Vietnam midnight a short gap is still minutes (now = 00:10 VN on the 18th).
    [InlineData("2026-11-17T17:10:00Z", "2026-11-17T16:50:00Z", "20 phút trước")]
    // Two to six days back: weekday and time. 17 Nov 2026 is a Tuesday.
    [InlineData("2026-11-17T10:00:00Z", "2026-11-15T14:15:00Z", "Chủ nhật, 21:15")]
    [InlineData("2026-11-17T10:00:00Z", "2026-11-14T03:00:00Z", "Thứ Bảy, 10:00")]
    [InlineData("2026-11-17T10:00:00Z", "2026-11-11T03:00:00Z", "Thứ Tư, 10:00")]
    // Seven days or more: a date.
    [InlineData("2026-11-17T10:00:00Z", "2026-11-10T03:00:00Z", "10/11/2026")]
    [InlineData("2026-11-17T10:00:00Z", "2026-01-01T00:00:00Z", "01/01/2026")]
    // A UTC evening that is already the next Vietnam day: 16:30 UTC on the 9th = 23:30 VN on the 9th.
    [InlineData("2026-11-17T10:00:00Z", "2026-11-09T16:30:00Z", "09/11/2026")]
    public void TimeText_FollowsTheVietnamTimeBands(string now, string created, string expected)
    {
        Assert.Equal(expected, NotificationDisplay.TimeText(Utc(created), Utc(now), Zone));
    }

    [Fact]
    public void TimeText_TreatsAnUnspecifiedKindAsUtc()
    {
        var created = DateTime.SpecifyKind(Utc("2026-11-16T16:50:00Z"), DateTimeKind.Unspecified);

        Assert.Equal("Hôm qua, 23:50", NotificationDisplay.TimeText(created, Utc("2026-11-17T10:00:00Z"), Zone));
    }

    // ── Day groups ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-11-17T10:00:00Z", "2026-11-17T09:59:00Z", "today")]
    [InlineData("2026-11-17T10:00:00Z", "2026-11-16T17:00:00Z", "today")]     // 00:00 VN on the 17th
    [InlineData("2026-11-17T10:00:00Z", "2026-11-16T16:59:00Z", "yesterday")] // 23:59 VN on the 16th
    [InlineData("2026-11-17T10:00:00Z", "2026-11-15T17:00:00Z", "yesterday")] // 00:00 VN on the 16th
    [InlineData("2026-11-17T10:00:00Z", "2026-11-15T16:59:00Z", "earlier")]
    [InlineData("2026-11-17T10:00:00Z", "2026-01-01T00:00:00Z", "earlier")]
    [InlineData("2026-11-17T10:00:00Z", "2026-11-17T12:00:00Z", "today")]     // future clock skew
    [InlineData("2026-11-17T17:10:00Z", "2026-11-17T16:50:00Z", "yesterday")] // 23:50 VN, now 00:10 VN next day
    public void DayGroup_UsesVietnamCalendarDays(string now, string created, string expected)
    {
        Assert.Equal(expected, NotificationDisplay.DayGroup(Utc(created), Utc(now), Zone));
    }

    // ── Link and label ─────────────────────────────────────────────────

    [Theory]
    [InlineData(NotificationType.BadgeEarned, "/Goals#badges", "Xem huy hiệu")]
    [InlineData(NotificationType.GoalCompleted, "/Goals", "Xem mục tiêu")]
    [InlineData(NotificationType.PremiumActivated, "/Billing/Order/TCT-20261117-ABC", "Xem biên nhận")]
    [InlineData(NotificationType.StreakWarning, "/Home/Index", "Học ngay")]
    [InlineData(NotificationType.AdminAnnouncement, "/Vocabulary/Index", "Xem chi tiết")]
    public void Build_LabelsASafeLink(NotificationType type, string url, string label)
    {
        var item = NotificationDisplay.Build(Make(type, url: url), Utc("2026-11-17T10:00:00Z"), Zone);

        Assert.Equal(url, item.Url);
        Assert.Equal(label, item.LinkLabel);
    }

    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("https://evil.example/x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("Goals")]
    [InlineData("")]
    [InlineData(null)]
    public void Build_DropsAnUnsafeOrMissingLinkAndItsLabel(string? url)
    {
        var item = NotificationDisplay.Build(
            Make(NotificationType.AdminAnnouncement, url: url), Utc("2026-11-17T10:00:00Z"), Zone);

        Assert.Null(item.Url);
        Assert.Null(item.LinkLabel);
    }

    // ── Whole item ─────────────────────────────────────────────────────

    [Fact]
    public void Build_CarriesTheRemainingFieldsAndAUtcTimestamp()
    {
        var item = NotificationDisplay.Build(
            Make(NotificationType.GoalCompleted, message: "Bạn đã hoàn thành.", isRead: true,
                created: "2026-11-17T09:00:00Z"),
            Utc("2026-11-17T10:00:00Z"), Zone);

        Assert.Equal(7, item.Id);
        Assert.Equal("Bạn đã hoàn thành.", item.Message);
        Assert.True(item.IsRead);
        Assert.Equal(DateTimeKind.Utc, item.CreatedAtUtc.Kind);
        Assert.Equal(Utc("2026-11-17T09:00:00Z"), item.CreatedAtUtc);
        Assert.Equal("1 giờ trước", item.TimeText);
        Assert.Equal("today", item.DayGroup);
    }
}

public sealed class NotificationLinksTests
{
    [Theory]
    [InlineData("/Goals")]
    [InlineData("/Goals#badges")]
    [InlineData("/Premium?plan=year")]
    [InlineData("/Billing/Order/TCT-20261117090720219-05786385")]
    [InlineData("/")]
    [InlineData("/Vocabulary/Index")]
    public void ToSafeLocal_AcceptsALocalPath(string raw)
    {
        Assert.Equal(raw, NotificationLinks.ToSafeLocal(raw));
        Assert.True(NotificationLinks.IsValidLocalPath(raw));
    }

    [Fact]
    public void ToSafeLocal_TrimsSurroundingWhitespace()
    {
        Assert.Equal("/Goals", NotificationLinks.ToSafeLocal("  /Goals \t"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Goals")]
    [InlineData("./Goals")]
    [InlineData("../Goals")]
    [InlineData("//evil.example")]
    [InlineData("//evil.example/path")]
    [InlineData("/\\evil.example")]
    [InlineData("/\\/evil.example")]
    [InlineData("\\\\evil.example")]
    [InlineData("\t//evil.example")]
    [InlineData("https://evil.example")]
    [InlineData("http://evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("/ok\nlinebreak")]
    [InlineData("/ok\rreturn")]
    [InlineData("/ok\ttab")]
    [InlineData("/ok\0nul")]
    public void ToSafeLocal_RejectsEverythingThatIsNotASingleSlashPath(string? raw)
    {
        Assert.Null(NotificationLinks.ToSafeLocal(raw));
        Assert.False(NotificationLinks.IsValidLocalPath(raw));
    }

    [Fact]
    public void ToSafeLocal_CutsAVeryLongPathAtFiveHundredCharacters()
    {
        var raw = "/" + new string('a', 600);

        var safe = NotificationLinks.ToSafeLocal(raw);

        Assert.NotNull(safe);
        Assert.Equal(500, safe!.Length);
        Assert.StartsWith("/aaa", safe);
    }

    [Fact]
    public void IsValidLocalPath_RejectsAPathLongerThanTheColumn()
    {
        Assert.True(NotificationLinks.IsValidLocalPath("/" + new string('a', 499)));
        Assert.False(NotificationLinks.IsValidLocalPath("/" + new string('a', 500)));
    }
}
