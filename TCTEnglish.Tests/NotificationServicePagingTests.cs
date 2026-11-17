using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// NotificationService against a relational (SQLite) database with a fixed clock:
/// category filter, keyset slices, counts, mark-all count, per-user creation, conflict handling
/// and the generator copy. Reference "now" is 2026-11-17 10:00 UTC (17:00 in Vietnam).
/// </summary>
public sealed class NotificationServicePagingTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _keeper;
    private readonly FailingSaveInterceptor _failing = new();

    public NotificationServicePagingTests()
    {
        _keeper = new SqliteConnection($"Data Source=file:notif-{Guid.NewGuid():N}?mode=memory&cache=shared");
        _keeper.Open();
    }

    public void Dispose() => _keeper.Dispose();

    // ── Fixtures ───────────────────────────────────────────────────────

    private DbflashcardContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseSqlite(_keeper)
            .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>()
            .AddInterceptors(_failing)
            .Options;

        var context = new DbflashcardContext(options);
        context.Database.EnsureCreated();
        if (!context.Users.Any())
        {
            context.Users.AddRange(
                new User { UserId = 1, Email = "one@example.com", PasswordHash = "x", Role = Roles.Standard },
                new User { UserId = 2, Email = "two@example.com", PasswordHash = "x", Role = Roles.Standard });
            context.SaveChanges();
        }

        return context;
    }

    private static NotificationService CreateService(
        DbflashcardContext context, ILogger<NotificationService>? logger = null, DateTime? now = null)
        => new(context, new LearningBusinessClock(new FixedTimeProvider(now ?? Now)),
            logger ?? NullLogger<NotificationService>.Instance);

    private static async Task<List<int>> AddAsync(
        DbflashcardContext context, int userId, params (NotificationType Type, bool Read, DateTime Created)[] rows)
    {
        var entities = rows.Select(r => new Notification
        {
            UserId = userId,
            Type = r.Type,
            Title = "T",
            Message = "M",
            IsRead = r.Read,
            CreatedAt = r.Created
        }).ToList();
        context.Notifications.AddRange(entities);
        await context.SaveChangesAsync();
        return entities.Select(e => e.Id).ToList();
    }

    private static DateTime At(int minutesAgo) => Now.AddMinutes(-minutesAgo);

    // ── Category filter and display model ──────────────────────────────

    [Fact]
    public async Task Page_FiltersByCategory_AndKeepsTheOverallUnreadCount()
    {
        await using var context = CreateContext();
        await AddAsync(context, 1,
            (NotificationType.StreakRecord, false, At(5)),
            (NotificationType.GoalCompleted, true, At(10)),
            (NotificationType.BadgeEarned, false, At(15)),
            (NotificationType.BadgeNear, true, At(20)),
            (NotificationType.PremiumActivated, false, At(25)),
            (NotificationType.AdminAnnouncement, false, At(30)));
        await AddAsync(context, 2, (NotificationType.BadgeEarned, false, At(1)));

        var service = CreateService(context);

        var badges = await service.GetNotificationsPageAsync(1, 1, 20, unreadOnly: false, NotificationCategory.Badge);
        Assert.Equal(2, badges.TotalCount);
        Assert.All(badges.Items, i => Assert.StartsWith("Badge", i.Type));
        Assert.Equal(4, badges.UnreadCount); // all of user 1's unread rows, not only badges

        var unreadLearning = await service.GetNotificationsPageAsync(1, 1, 20, unreadOnly: true, NotificationCategory.Learning);
        Assert.Single(unreadLearning.Items);
        Assert.Equal("StreakRecord", unreadLearning.Items[0].Type);

        var payment = await service.GetNotificationsPageAsync(1, 1, 20, false, NotificationCategory.Payment);
        Assert.Equal("PremiumActivated", Assert.Single(payment.Items).Type);

        var all = await service.GetNotificationsPageAsync(1, 1, 20);
        Assert.Equal(6, all.TotalCount);
    }

    [Fact]
    public async Task Page_BuildsItemsThroughTheDisplayModel()
    {
        await using var context = CreateContext();
        context.Notifications.Add(new Notification
        {
            UserId = 1,
            Type = NotificationType.StreakRecord,
            Title = "🔥 Kỷ lục mới!",
            Message = "Nội dung",
            IconClass = "fas fa-fire\" onclick=\"x",
            RelatedUrl = "//evil.example",
            CreatedAt = At(90)
        });
        context.Notifications.Add(new Notification
        {
            UserId = 1,
            Type = NotificationType.GoalCompleted,
            Title = "Mục tiêu",
            Message = "Nội dung",
            RelatedUrl = "/Goals",
            CreatedAt = At(5)
        });
        await context.SaveChangesAsync();

        var page = await CreateService(context).GetNotificationsPageAsync(1);

        var goal = page.Items[0];
        Assert.Equal("5 phút trước", goal.TimeText);
        Assert.Equal(goal.TimeText, goal.TimeAgo);
        Assert.Equal("/Goals", goal.Url);
        Assert.Equal("/Goals", goal.RelatedUrl);
        Assert.Equal("Xem mục tiêu", goal.LinkLabel);
        Assert.Equal("learning", goal.Category);
        Assert.Equal("today", goal.DayGroup);
        Assert.Equal(DateTimeKind.Utc, goal.CreatedAtUtc.Kind);

        var streak = page.Items[1];
        Assert.Equal("Kỷ lục mới!", streak.Title);
        Assert.Equal("fa-solid fa-fire", streak.IconClass); // bad stored icon replaced by the default
        Assert.Null(streak.Url);
        Assert.Null(streak.RelatedUrl);
        Assert.Null(streak.LinkLabel);
        Assert.Equal("1 giờ trước", streak.TimeText);
    }

    // ── Keyset slices ──────────────────────────────────────────────────

    [Fact]
    public async Task Slice_AfterAnInsertBetweenReads_HasNoDuplicateAndNoGap()
    {
        await using var context = CreateContext();
        var ids = await AddAsync(context, 1,
            (NotificationType.GoalProgress, false, At(50)),
            (NotificationType.GoalProgress, false, At(40)),
            (NotificationType.GoalProgress, false, At(30)),
            (NotificationType.GoalProgress, false, At(20)),
            (NotificationType.GoalProgress, false, At(10)));
        var service = CreateService(context);

        var first = await service.GetNotificationsSliceAsync(1, 2, false, null, null);
        Assert.Equal(new[] { ids[4], ids[3] }, first.Items.Select(i => i.Id));
        Assert.True(first.HasMore);
        Assert.NotNull(first.NextCursor);

        // A new notification arrives (and an old one is cleaned up) between the two reads.
        var newId = (await AddAsync(context, 1, (NotificationType.StreakRecord, false, At(1))))[0];
        context.Notifications.Remove(await context.Notifications.FirstAsync(n => n.Id == ids[0]));
        await context.SaveChangesAsync();

        var second = await service.GetNotificationsSliceAsync(
            1, 2, false, first.NextCursor!.Before, first.NextCursor.BeforeId);
        Assert.Equal(new[] { ids[2], ids[1] }, second.Items.Select(i => i.Id));
        Assert.DoesNotContain(newId, second.Items.Select(i => i.Id));
        Assert.False(second.HasMore);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task Slice_WithIdenticalTimestamps_ReturnsEveryRowExactlyOnce()
    {
        await using var context = CreateContext();
        var same = At(15);
        var ids = await AddAsync(context, 1,
            (NotificationType.GoalProgress, false, same),
            (NotificationType.GoalProgress, false, same),
            (NotificationType.GoalProgress, false, same),
            (NotificationType.GoalProgress, false, same),
            (NotificationType.GoalProgress, false, same));
        var service = CreateService(context);

        var seen = new List<int>();
        NotificationCursor? cursor = null;
        for (var guard = 0; guard < 10; guard++)
        {
            var slice = await service.GetNotificationsSliceAsync(
                1, 2, false, cursor?.Before, cursor?.BeforeId);
            seen.AddRange(slice.Items.Select(i => i.Id));
            if (!slice.HasMore)
            {
                break;
            }

            cursor = slice.NextCursor;
        }

        Assert.Equal(ids.OrderByDescending(i => i), seen);
    }

    [Fact]
    public async Task Slice_UnreadOnly_SkipsReadRows_AndReportsTheUnreadTotal()
    {
        await using var context = CreateContext();
        await AddAsync(context, 1,
            (NotificationType.GoalProgress, true, At(5)),
            (NotificationType.GoalProgress, false, At(10)),
            (NotificationType.GoalProgress, true, At(15)),
            (NotificationType.GoalProgress, false, At(20)));
        await AddAsync(context, 2, (NotificationType.GoalProgress, false, At(1)));

        var slice = await CreateService(context).GetNotificationsSliceAsync(1, 10, true, null, null);

        Assert.Equal(2, slice.Items.Count);
        Assert.All(slice.Items, i => Assert.False(i.IsRead));
        Assert.Equal(2, slice.UnreadCount);
        Assert.False(slice.HasMore);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(500, 50)]
    public async Task Slice_ClampsTheTakeToOneToFifty(int take, int expectedMax)
    {
        await using var context = CreateContext();
        await AddAsync(context, 1, Enumerable.Range(0, 60)
            .Select(i => (NotificationType.GoalProgress, false, At(i + 1))).ToArray());

        var slice = await CreateService(context).GetNotificationsSliceAsync(1, take, false, null, null);

        Assert.Equal(expectedMax, slice.Items.Count);
        Assert.True(slice.HasMore);
    }

    // ── Counts ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CategoryCounts_GroupUnreadAndTotalPerCategory_ForTheOwnerOnly()
    {
        await using var context = CreateContext();
        await AddAsync(context, 1,
            (NotificationType.StreakRecord, false, At(1)),
            (NotificationType.GoalCompleted, false, At(2)),
            (NotificationType.GoalProgress, true, At(3)),
            (NotificationType.BadgeEarned, false, At(4)),
            (NotificationType.PremiumActivated, true, At(5)),
            (NotificationType.AdminAnnouncement, false, At(6)));
        await AddAsync(context, 2,
            (NotificationType.BadgeEarned, false, At(1)),
            (NotificationType.AdminAnnouncement, false, At(2)));

        var counts = await CreateService(context).GetCategoryCountsAsync(1);

        Assert.Equal(6, counts.Total);
        Assert.Equal(4, counts.Unread);
        Assert.Equal(new NotificationCategoryCount(3, 2), counts.ByCategory[NotificationCategory.Learning]);
        Assert.Equal(new NotificationCategoryCount(1, 1), counts.ByCategory[NotificationCategory.Badge]);
        Assert.Equal(new NotificationCategoryCount(1, 0), counts.ByCategory[NotificationCategory.Payment]);
        Assert.Equal(new NotificationCategoryCount(1, 1), counts.ByCategory[NotificationCategory.System]);
    }

    [Fact]
    public async Task CategoryCounts_ForAUserWithNothing_AreZeroInEveryCategory()
    {
        await using var context = CreateContext();

        var counts = await CreateService(context).GetCategoryCountsAsync(1);

        Assert.Equal(0, counts.Total);
        Assert.Equal(0, counts.Unread);
        foreach (var category in Enum.GetValues<NotificationCategory>())
        {
            Assert.Equal(new NotificationCategoryCount(0, 0), counts.ByCategory[category]);
        }
    }

    // ── Mark all read ──────────────────────────────────────────────────

    [Fact]
    public async Task MarkAll_ReturnsTheUpdatedCount_AndLeavesOtherUsersAlone()
    {
        await using var context = CreateContext();
        await AddAsync(context, 1,
            (NotificationType.StreakRecord, false, At(1)),
            (NotificationType.BadgeEarned, false, At(2)),
            (NotificationType.BadgeNear, true, At(3)));
        await AddAsync(context, 2, (NotificationType.BadgeEarned, false, At(1)));
        var service = CreateService(context);

        var result = await service.MarkAllAsReadAsync(1, category: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
        Assert.Equal(0, await service.GetUnreadCountAsync(1));
        Assert.Equal(1, await service.GetUnreadCountAsync(2));

        var again = await service.MarkAllAsReadAsync(1, category: null);
        Assert.True(again.IsSuccess);
        Assert.Equal(0, again.Value);
    }

    [Fact]
    public async Task MarkAll_WithACategory_OnlyMarksThatCategory()
    {
        await using var context = CreateContext();
        await AddAsync(context, 1,
            (NotificationType.StreakRecord, false, At(1)),
            (NotificationType.GoalCompleted, false, At(2)),
            (NotificationType.BadgeEarned, false, At(3)),
            (NotificationType.PremiumActivated, false, At(4)));
        var service = CreateService(context);

        var result = await service.MarkAllAsReadAsync(1, NotificationCategory.Learning);

        Assert.Equal(2, result.Value);
        var counts = await service.GetCategoryCountsAsync(1);
        Assert.Equal(0, counts.ByCategory[NotificationCategory.Learning].Unread);
        Assert.Equal(1, counts.ByCategory[NotificationCategory.Badge].Unread);
        Assert.Equal(1, counts.ByCategory[NotificationCategory.Payment].Unread);
    }

    [Fact]
    public async Task MarkAll_TheOldSignatureStillMarksEverything()
    {
        await using var context = CreateContext();
        await AddAsync(context, 1,
            (NotificationType.StreakRecord, false, At(1)),
            (NotificationType.BadgeEarned, false, At(2)));
        var service = CreateService(context);

        OperationResult result = await service.MarkAllAsReadAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await service.GetUnreadCountAsync(1));
    }

    // ── CreateForUserAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CreateForUser_IsIdempotentByEventKey()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var first = await service.CreateForUserAsync(
            1, NotificationType.PremiumActivated, "Premium đã được kích hoạt",
            "Gói Premium năm có hiệu lực đến 17/11/2027.", "/Billing/Order/TCT-1", "fa-solid fa-crown", "premium:TCT-1");
        var second = await service.CreateForUserAsync(
            1, NotificationType.PremiumActivated, "Premium đã được kích hoạt",
            "Gói Premium năm có hiệu lực đến 17/11/2027.", "/Billing/Order/TCT-1", "fa-solid fa-crown", "premium:TCT-1");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var row = await context.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal(NotificationType.PremiumActivated, row.Type);
        Assert.Equal("premium:TCT-1", row.EventKey);
        Assert.Equal("/Billing/Order/TCT-1", row.RelatedUrl);
        Assert.Equal("fa-solid fa-crown", row.IconClass);
        Assert.False(row.IsRead);
        Assert.Equal(Now, row.CreatedAt);
    }

    [Fact]
    public async Task CreateForUser_AnotherUserWithTheSameKey_GetsHisOwnRow()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.CreateForUserAsync(1, NotificationType.PremiumActivated, "T", "M", null, null, "premium:X");
        await service.CreateForUserAsync(2, NotificationType.PremiumActivated, "T", "M", null, null, "premium:X");

        Assert.Equal(2, await context.Notifications.CountAsync());
    }

    [Theory]
    [InlineData("", "Nội dung", "/Goals", "k1")]
    [InlineData("   ", "Nội dung", "/Goals", "k1")]
    [InlineData("Tiêu đề", "", "/Goals", "k1")]
    [InlineData("Tiêu đề", "Nội dung", "//evil.example", "k1")]
    [InlineData("Tiêu đề", "Nội dung", "https://evil.example", "k1")]
    [InlineData("Tiêu đề", "Nội dung", "javascript:alert(1)", "k1")]
    [InlineData("Tiêu đề", "Nội dung", "/Goals", "")]
    [InlineData("Tiêu đề", "Nội dung", "/Goals", "   ")]
    public async Task CreateForUser_RejectsBlankFieldsAndUnsafeLinks_AndStoresNothing(
        string title, string message, string? url, string key)
    {
        await using var context = CreateContext();

        var result = await CreateService(context)
            .CreateForUserAsync(1, NotificationType.AdminAnnouncement, title, message, url, null, key);

        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Equal(0, await context.Notifications.CountAsync());
    }

    [Fact]
    public async Task CreateForUser_RejectsTooLongValues()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var longTitle = await service.CreateForUserAsync(
            1, NotificationType.AdminAnnouncement, new string('a', 201), "M", null, null, "k1");
        var longMessage = await service.CreateForUserAsync(
            1, NotificationType.AdminAnnouncement, "T", new string('a', 1001), null, null, "k2");
        var longKey = await service.CreateForUserAsync(
            1, NotificationType.AdminAnnouncement, "T", "M", null, null, new string('k', 161));
        var longUrl = await service.CreateForUserAsync(
            1, NotificationType.AdminAnnouncement, "T", "M", "/" + new string('a', 500), null, "k4");
        var maxed = await service.CreateForUserAsync(
            1, NotificationType.AdminAnnouncement, new string('a', 200), new string('b', 1000), null, null, "k5");

        Assert.Equal(OperationStatus.Invalid, longTitle.Status);
        Assert.Equal(OperationStatus.Invalid, longMessage.Status);
        Assert.Equal(OperationStatus.Invalid, longKey.Status);
        Assert.Equal(OperationStatus.Invalid, longUrl.Status);
        Assert.True(maxed.IsSuccess);
        Assert.Equal(1, await context.Notifications.CountAsync());
    }

    [Theory]
    [InlineData("<i class=x>")]
    [InlineData("fas fa-fire\" onclick=\"x")]
    [InlineData("fa-brands fa-github")]
    public async Task CreateForUser_DropsAnIconThatIsNotAnAllowedClass(string icon)
    {
        await using var context = CreateContext();

        var result = await CreateService(context)
            .CreateForUserAsync(1, NotificationType.AdminAnnouncement, "T", "M", null, icon, "k1");

        Assert.True(result.IsSuccess);
        Assert.Null((await context.Notifications.AsNoTracking().SingleAsync()).IconClass);
    }

    [Fact]
    public async Task CreateForUser_ForAUserThatDoesNotExist_ReturnsAFailureWithoutThrowing()
    {
        await using var context = CreateContext();
        var logger = new ListLogger<NotificationService>();

        var result = await CreateService(context, logger)
            .CreateForUserAsync(999, NotificationType.PremiumActivated, "T", "M", null, null, "premium:Z");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await context.Notifications.CountAsync());
        Assert.Empty(context.ChangeTracker.Entries<Notification>());
    }

    // ── Conflict handling ──────────────────────────────────────────────

    public static IEnumerable<object[]> UniqueConflicts()
    {
        yield return new object[] { new Func<Exception>(() => MySqlFault(1062, "Duplicate entry '1-premium:A' for key 'IX_Notifications_UserId_EventKey'")) };
        yield return new object[] { new Func<Exception>(() => new SqliteException(
            "SQLite Error 19: 'UNIQUE constraint failed: Notifications.UserId, Notifications.EventKey'.", 19)) };
    }

    [Theory]
    [MemberData(nameof(UniqueConflicts))]
    public async Task TryCreate_AUniqueConflict_IsAnIgnoredDuplicate_WithoutAWarning(Func<Exception> makeInner)
    {
        await using var context = CreateContext();
        var logger = new ListLogger<NotificationService>();
        var service = CreateService(context, logger);
        _failing.Throw = new DbUpdateException("Save failed", makeInner());

        var result = await service.CreateForUserAsync(1, NotificationType.PremiumActivated, "T", "M", null, null, "premium:A");

        _failing.Throw = null;
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Empty(context.ChangeTracker.Entries<Notification>());
    }

    [Theory]
    [InlineData(1452)] // foreign key
    [InlineData(1213)] // deadlock
    [InlineData(1205)] // lock wait timeout
    public async Task TryCreate_AnotherDatabaseError_IsLoggedAsAWarning_NotHiddenAndNotThrown(int number)
    {
        await using var context = CreateContext();
        var logger = new ListLogger<NotificationService>();
        var service = CreateService(context, logger);
        var badgeId = await EnsureBadgeAsync(context);
        _failing.Throw = new DbUpdateException("Save failed", MySqlFault(number, "Some failure"));

        var created = await service.CreateForUserAsync(
            1, NotificationType.PremiumActivated, "Tiêu đề bí mật", "Nội dung bí mật", null, null, "premium:B");
        await service.GenerateBadgeNotificationAsync(1, badgeId); // callers in Goals must keep working

        _failing.Throw = null;
        Assert.False(created.IsSuccess);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
        // The warning names the type and the user, never the notification text.
        var warning = logger.Entries.First(e => e.Level == LogLevel.Warning).Message;
        Assert.Contains("PremiumActivated", warning);
        Assert.Contains("1", warning);
        Assert.DoesNotContain("Tiêu đề bí mật", warning);
        Assert.DoesNotContain("Nội dung bí mật", warning);
        Assert.Empty(context.ChangeTracker.Entries<Notification>());
    }

    // ── Generator copy (Vietnamese, no emoji, same event keys) ─────────

    [Fact]
    public async Task StreakRecord_UsesVietnameseCopyWithoutEmoji_AndTheSameKey()
    {
        await using var context = CreateContext();

        await CreateService(context).GenerateStreakNotificationsAsync(
            1, new StreakUpdateResult { DidIncrease = true, CurrentStreak = 5, PreviousLongestStreak = 4 });

        var row = await context.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal("Kỷ lục mới: chuỗi 5 ngày học", row.Title);
        Assert.Equal("Đây là chuỗi ngày học dài nhất của bạn.", row.Message);
        Assert.Equal("streak-record:1:2026-11-17:5", row.EventKey);
        Assert.Equal(NotificationType.StreakRecord, row.Type);
        Assert.Equal("/Goals", row.RelatedUrl);
        AssertPlainVietnamese(row);
    }

    [Fact]
    public async Task GoalCompleted_UsesVietnameseCopyWithoutEmoji_AndTheSameKey()
    {
        await using var context = CreateContext();
        await SeedGoalAsync(context, GoalArea.Vocabulary, target: 20, cardsReviewed: 20);

        await CreateService(context).GenerateGoalNotificationsAsync(1, GoalsActivityRecordResult.Success(1));

        var row = await context.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal("Hoàn thành mục tiêu Từ vựng", row.Title);
        Assert.Equal("Bạn đã hoàn thành mục tiêu từ vựng hôm nay. Giữ nhịp này nhé!", row.Message);
        Assert.Equal("goal:1:Vocabulary:2026-11-17:100", row.EventKey);
        Assert.Equal(NotificationType.GoalCompleted, row.Type);
        AssertPlainVietnamese(row);
    }

    [Fact]
    public async Task GoalProgress_UsesVietnameseCopyWithoutEmoji_AndTheSameKey()
    {
        await using var context = CreateContext();
        await SeedGoalAsync(context, GoalArea.Vocabulary, target: 20, cardsReviewed: 17);

        await CreateService(context).GenerateGoalNotificationsAsync(1, GoalsActivityRecordResult.Success(1));

        var row = await context.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal("Sắp đạt mục tiêu Từ vựng", row.Title);
        Assert.Equal("Bạn đã đạt 85% mục tiêu từ vựng hôm nay, cố thêm chút nữa!", row.Message);
        Assert.Equal("goal:1:Vocabulary:2026-11-17:80", row.EventKey);
        Assert.Equal(NotificationType.GoalProgress, row.Type);
        AssertPlainVietnamese(row);
    }

    [Fact]
    public async Task BadgeEarned_UsesVietnameseCopyWithoutEmoji_AndTheSameKey()
    {
        await using var context = CreateContext();
        var badgeId = await EnsureBadgeAsync(context);
        var badge = await context.Badges.AsNoTracking().FirstAsync(b => b.Id == badgeId);

        await CreateService(context).GenerateBadgeNotificationAsync(1, badgeId);

        var row = await context.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal($"Huy hiệu mới: {badge.Name}", row.Title);
        Assert.Equal($"Bạn vừa nhận huy hiệu \"{badge.Name}\".", row.Message);
        Assert.Equal($"badge:1:{badgeId}", row.EventKey);
        Assert.Equal(NotificationType.BadgeEarned, row.Type);
        Assert.Equal("/Goals#badges", row.RelatedUrl);
        AssertPlainVietnamese(row);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static void AssertPlainVietnamese(Notification row)
    {
        Assert.Equal(row.Title, NotificationDisplay.CleanTitle(row.Title)); // nothing left to strip
        Assert.DoesNotContain("Streak", row.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Streak", row.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(row.Message, c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherSymbol
            || char.IsSurrogate(c));
    }

    private static async Task SeedGoalAsync(DbflashcardContext context, GoalArea area, int target, int cardsReviewed)
    {
        context.UserGoals.Add(new UserGoal
        {
            UserId = 1, GoalArea = area, TargetValue = target, IsActive = true,
            CreatedAt = Now, UpdatedAt = Now
        });
        context.UserDailyActivities.Add(new UserDailyActivity
        {
            UserId = 1, ActivityDate = new DateTime(2026, 11, 17), CardsReviewed = cardsReviewed
        });
        await context.SaveChangesAsync();
    }

    private static async Task<int> EnsureBadgeAsync(DbflashcardContext context)
    {
        var existing = await context.Badges.AsNoTracking().OrderBy(b => b.Id).FirstOrDefaultAsync();
        if (existing != null)
        {
            return existing.Id;
        }

        var badge = new Badge
        {
            Code = "test-badge", Name = "Khởi động", Description = "Mô tả",
            IconClass = "fas fa-seedling", MetricType = BadgeMetricType.TotalDaysActive, ThresholdValue = 1
        };
        context.Badges.Add(badge);
        await context.SaveChangesAsync();
        return badge.Id;
    }

    private static MySqlException MySqlFault(int number, string message)
    {
        var ctor = typeof(MySqlException).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(MySqlErrorCode), typeof(string)],
            modifiers: null)
            ?? throw new InvalidOperationException("MySqlException(MySqlErrorCode, string) is unavailable.");
        return (MySqlException)ctor.Invoke([(MySqlErrorCode)number, message]);
    }

    private sealed class FixedTimeProvider(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public Exception? Throw { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => Throw is { } failure ? throw failure : base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
