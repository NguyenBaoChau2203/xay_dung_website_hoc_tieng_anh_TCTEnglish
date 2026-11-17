using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Admin announcements (plan 4.4): recipients, one-batch idempotency, the optional local link, transactional
/// chunks and the history grouped by batch with its sender. Reference "now" is 2026-11-17 10:00 UTC.
/// </summary>
public sealed class AdminAnnouncementTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _keeper;

    public AdminAnnouncementTests()
    {
        _keeper = new SqliteConnection($"Data Source=file:announce-{Guid.NewGuid():N}?mode=memory&cache=shared");
        _keeper.Open();
    }

    public void Dispose() => _keeper.Dispose();

    // ── Fixtures ───────────────────────────────────────────────────────

    private DbflashcardContext CreateContext(bool seedUsers = true)
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseSqlite(_keeper)
            .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>()
            .Options;

        var context = new DbflashcardContext(options);
        context.Database.EnsureCreated();
        if (seedUsers && !context.Users.Any())
        {
            context.Users.AddRange(
                new User { UserId = 1, Email = "admin@example.com", PasswordHash = "x", Role = Roles.Admin, FullName = "Quản trị A" },
                new User { UserId = 2, Email = "std@example.com", PasswordHash = "x", Role = Roles.Standard },
                new User { UserId = 3, Email = "premium@example.com", PasswordHash = "x", Role = Roles.Premium },
                new User { UserId = 4, Email = "blocked@example.com", PasswordHash = "x", Role = Roles.Standard, Status = UserStatus.Blocked },
                new User
                {
                    UserId = 5, Email = "gone@example.com", PasswordHash = "x", Role = Roles.Standard,
                    AnonymizedAt = Now.AddDays(-3)
                },
                new User { UserId = 6, Email = "admin2@example.com", PasswordHash = "x", Role = Roles.Admin });
            context.SaveChanges();
        }

        return context;
    }

    private static NotificationService CreateService(DbflashcardContext context, DateTime? now = null)
        => new(context, new LearningBusinessClock(new FixedTimeProvider(now ?? Now)),
            NullLogger<NotificationService>.Instance);

    private static AdminAnnouncementRequest Request(
        Guid? batch = null, int admin = 1, string title = "Bảo trì", string message = "Hệ thống bảo trì tối nay.",
        string? url = null)
        => new(title, message, url, admin, batch ?? Guid.NewGuid());

    private static string Key(int admin, Guid batch) => $"announce:{admin}:{batch:N}";

    // ── Recipients ─────────────────────────────────────────────────────

    [Fact]
    public async Task Send_SkipsBlockedAndAnonymisedAccounts_AndIncludesAdmins()
    {
        await using var context = CreateContext();
        var batch = Guid.NewGuid();

        var result = await CreateService(context).SendAdminAnnouncementAsync(Request(batch, url: "/Vocabulary/Index"));

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Recipients);
        Assert.False(result.Value.AlreadySent);
        var rows = await context.Notifications.AsNoTracking().ToListAsync();
        Assert.Equal(new[] { 1, 2, 3, 6 }, rows.Select(r => r.UserId).OrderBy(i => i));
        Assert.All(rows, row =>
        {
            Assert.Equal(NotificationType.AdminAnnouncement, row.Type);
            Assert.Equal(Key(1, batch), row.EventKey);
            Assert.Equal("Bảo trì", row.Title);
            Assert.Equal("Hệ thống bảo trì tối nay.", row.Message);
            Assert.Equal("/Vocabulary/Index", row.RelatedUrl);
            Assert.Equal("fa-solid fa-bullhorn", row.IconClass);
            Assert.False(row.IsRead);
            Assert.Equal(Now, row.CreatedAt);
        });
    }

    [Fact]
    public async Task Send_TheResultCountMatchesTheRowsWritten_ForManyRecipients()
    {
        await using var context = CreateContext();
        context.Users.AddRange(Enumerable.Range(100, 1200).Select(i => new User
        {
            UserId = i, Email = $"bulk{i}@example.com", PasswordHash = "x", Role = Roles.Standard
        }));
        await context.SaveChangesAsync();

        var result = await CreateService(context).SendAdminAnnouncementAsync(Request());

        Assert.Equal(1204, result.Value!.Recipients); // 4 active seed users + 1200
        Assert.Equal(1204, await context.Notifications.CountAsync());
    }

    [Fact]
    public async Task Send_WhenNobodyCanReceiveIt_IsASuccessWithZeroRecipients()
    {
        await using var context = CreateContext(seedUsers: false);

        var result = await CreateService(context).SendAdminAnnouncementAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.Recipients);
        Assert.Equal(0, await context.Notifications.CountAsync());
    }

    // ── Idempotency ────────────────────────────────────────────────────

    [Fact]
    public async Task Send_TheSameBatchTwice_LandsOnce()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var batch = Guid.NewGuid();

        var first = await service.SendAdminAnnouncementAsync(Request(batch));
        var second = await service.SendAdminAnnouncementAsync(Request(batch));

        Assert.False(first.Value!.AlreadySent);
        Assert.True(second.IsSuccess);
        Assert.True(second.Value!.AlreadySent);
        Assert.Equal(first.Value.Recipients, second.Value.Recipients);
        Assert.Equal(4, await context.Notifications.CountAsync());
    }

    [Fact]
    public async Task Send_ANewBatchWithTheSameText_IsASeparateSend()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.SendAdminAnnouncementAsync(Request());
        var again = await service.SendAdminAnnouncementAsync(Request());

        Assert.False(again.Value!.AlreadySent);
        Assert.Equal(8, await context.Notifications.CountAsync());
    }

    [Fact]
    public async Task Send_AConflictWithAnotherRequestsRows_RollsBackTheWholeBatch()
    {
        await using var context = CreateContext();
        var batch = Guid.NewGuid();
        // A racing request already wrote this batch for one learner (the sender's own row is not there yet).
        context.Notifications.Add(new Notification
        {
            UserId = 3, Type = NotificationType.AdminAnnouncement, Title = "x", Message = "y",
            EventKey = Key(1, batch), CreatedAt = Now
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).SendAdminAnnouncementAsync(Request(batch));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.AlreadySent);
        Assert.Equal(1, await context.Notifications.CountAsync()); // nothing partial was kept
        Assert.DoesNotContain(context.ChangeTracker.Entries<Notification>(), e => e.State == EntityState.Added);
    }

    // ── Validation ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("//evil.example")]
    [InlineData("https://evil.example/x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("Premium")]
    [InlineData("/\\evil.example")]
    public async Task Send_AnUnsafeLink_IsRejectedWithTheVietnameseMessage_AndNothingIsSent(string url)
    {
        await using var context = CreateContext();

        var result = await CreateService(context).SendAdminAnnouncementAsync(Request(url: url));

        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.Equal("Chỉ dùng đường dẫn bắt đầu bằng / trong TCT English.", result.ErrorMessage);
        Assert.Equal(0, await context.Notifications.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Send_ABlankLink_MeansNoLink(string? url)
    {
        await using var context = CreateContext();

        var result = await CreateService(context).SendAdminAnnouncementAsync(Request(url: url));

        Assert.True(result.IsSuccess);
        Assert.All(await context.Notifications.AsNoTracking().ToListAsync(), n => Assert.Null(n.RelatedUrl));
    }

    [Fact]
    public async Task Send_TrimsTitleAndMessage()
    {
        await using var context = CreateContext();

        await CreateService(context).SendAdminAnnouncementAsync(Request(title: "  Tiêu đề  ", message: "\tNội dung\n"));

        var row = await context.Notifications.AsNoTracking().FirstAsync();
        Assert.Equal("Tiêu đề", row.Title);
        Assert.Equal("Nội dung", row.Message);
    }

    [Theory]
    [InlineData("", "Nội dung")]
    [InlineData("   ", "Nội dung")]
    [InlineData("Tiêu đề", "")]
    [InlineData("Tiêu đề", "   ")]
    public async Task Send_BlankTitleOrMessage_IsInvalid(string title, string message)
    {
        await using var context = CreateContext();

        var result = await CreateService(context).SendAdminAnnouncementAsync(Request(title: title, message: message));

        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.Equal(0, await context.Notifications.CountAsync());
    }

    [Fact]
    public async Task Send_EnforcesTheColumnLengths_AndTheBatchId()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var longTitle = await service.SendAdminAnnouncementAsync(Request(title: new string('a', 201)));
        var longMessage = await service.SendAdminAnnouncementAsync(Request(message: new string('a', 1001)));
        var noBatch = await service.SendAdminAnnouncementAsync(Request(batch: Guid.Empty));
        var maxed = await service.SendAdminAnnouncementAsync(
            Request(title: new string('a', 200), message: new string('b', 1000)));

        Assert.Equal(OperationStatus.Invalid, longTitle.Status);
        Assert.Equal(OperationStatus.Invalid, longMessage.Status);
        Assert.Equal(OperationStatus.Invalid, noBatch.Status);
        Assert.True(maxed.IsSuccess);
    }

    [Fact]
    public async Task TheOldSignature_StillSendsToEveryActiveAccount()
    {
        await using var context = CreateContext();

        OperationResult result = await CreateService(context).CreateAdminAnnouncementAsync("Tiêu đề", "Nội dung", 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, await context.Notifications.CountAsync());
    }

    // ── History ────────────────────────────────────────────────────────

    private async Task SeedHistoryAsync(DbflashcardContext context)
    {
        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();
        await CreateService(context, Now.AddHours(-5)).SendAdminAnnouncementAsync(
            Request(batch1, admin: 1, title: "T1", message: "M1", url: "/Premium"));
        await CreateService(context, Now.AddHours(-1)).SendAdminAnnouncementAsync(
            Request(batch2, admin: 6, title: "T2", message: "M2"));

        // Two readers of the first send.
        foreach (var row in await context.Notifications.Where(n => n.EventKey == Key(1, batch1) && n.UserId <= 2).ToListAsync())
        {
            row.IsRead = true;
        }

        // Legacy rows (no event key): the same text sent twice, 20 seconds apart, 60 days ago.
        var legacyA = Now.AddDays(-60);
        var legacyB = legacyA.AddSeconds(20);
        context.Notifications.AddRange(
            Legacy(1, legacyA, true), Legacy(2, legacyA, true), Legacy(3, legacyA, true),
            Legacy(1, legacyB, false), Legacy(2, legacyB, true));
        await context.SaveChangesAsync();
    }

    private static Notification Legacy(int userId, DateTime created, bool read) => new()
    {
        UserId = userId, Type = NotificationType.AdminAnnouncement, Title = "Cũ", Message = "Thông báo cũ",
        IsRead = read, EventKey = null, CreatedAt = created
    };

    [Fact]
    public async Task History_GroupsByBatch_WithSenderRecipientsAndReads_ThenLegacyRows()
    {
        await using var context = CreateContext();
        await SeedHistoryAsync(context);

        var page = await CreateService(context).GetAnnouncementHistoryAsync(1, 10);

        Assert.Equal(4, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(new[] { "T2", "T1", "Cũ", "Cũ" }, page.Items.Select(i => i.Title));

        var t2 = page.Items[0];
        Assert.Equal(6, t2.SenderId);
        Assert.Equal("Quản trị viên", t2.SenderName);   // sender has no full name
        Assert.Equal(4, t2.Recipients);
        Assert.Equal(0, t2.ReadCount);
        Assert.False(t2.IsLegacy);
        Assert.False(t2.CopiesMayBeCleaned);
        Assert.Equal(Now.AddHours(-1), t2.CreatedAtUtc);

        var t1 = page.Items[1];
        Assert.Equal(1, t1.SenderId);
        Assert.Equal("Quản trị A", t1.SenderName);
        Assert.Equal(4, t1.Recipients);
        Assert.Equal(2, t1.ReadCount);
        Assert.Equal("/Premium", t1.Url);

        // Legacy: two different sends of the same text stay separate, newest first.
        var newerLegacy = page.Items[2];
        var olderLegacy = page.Items[3];
        Assert.True(newerLegacy.IsLegacy && olderLegacy.IsLegacy);
        Assert.Equal(2, newerLegacy.Recipients);
        Assert.Equal(1, newerLegacy.ReadCount);
        Assert.Equal(3, olderLegacy.Recipients);
        Assert.Equal(3, olderLegacy.ReadCount);
        Assert.Null(newerLegacy.SenderId);
        Assert.Equal("Quản trị viên", newerLegacy.SenderName);
        Assert.True(newerLegacy.CopiesMayBeCleaned);   // older than the 30-day retention
    }

    [Fact]
    public async Task History_PagesInTheDatabase_KeyedFirstThenLegacy_AndClampsThePage()
    {
        await using var context = CreateContext();
        await SeedHistoryAsync(context);
        var service = CreateService(context);

        var first = await service.GetAnnouncementHistoryAsync(1, 2);
        var second = await service.GetAnnouncementHistoryAsync(2, 2);
        var beyond = await service.GetAnnouncementHistoryAsync(99, 2);

        Assert.Equal(2, first.TotalPages);
        Assert.Equal(new[] { "T2", "T1" }, first.Items.Select(i => i.Title));
        Assert.Equal(new[] { true, true }, second.Items.Select(i => i.IsLegacy));
        Assert.Equal(2, beyond.Page);
        Assert.Equal(second.Items.Select(i => i.CreatedAtUtc), beyond.Items.Select(i => i.CreatedAtUtc));
    }

    [Fact]
    public async Task History_WithNoAnnouncements_IsAnEmptyFirstPage()
    {
        await using var context = CreateContext();

        var page = await CreateService(context).GetAnnouncementHistoryAsync(3, 20);

        Assert.Empty(page.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task History_IgnoresOtherNotificationTypes_AndKeysThatAreNotAnnouncements()
    {
        await using var context = CreateContext();
        context.Notifications.AddRange(
            new Notification
            {
                UserId = 2, Type = NotificationType.GoalCompleted, Title = "Goal", Message = "m",
                EventKey = "goal:2:Vocabulary:2026-11-17:100", CreatedAt = Now
            },
            new Notification
            {
                UserId = 2, Type = NotificationType.AdminAnnouncement, Title = "Keyed", Message = "m",
                EventKey = "something-else:1", CreatedAt = Now
            });
        await context.SaveChangesAsync();

        var page = await CreateService(context).GetAnnouncementHistoryAsync(1, 20);

        // A non-announce key on an announcement row is still listed (as its own group), a goal row never is.
        Assert.DoesNotContain(page.Items, i => i.Title == "Goal");
        Assert.Contains(page.Items, i => i.Title == "Keyed");
    }

    private sealed class FixedTimeProvider(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }
}

/// <summary>The admin form over HTTP: batch id, double submit, link rule, role and antiforgery.</summary>
public sealed class AdminAnnouncementControllerTests
{
    private const string CreateUrl = "/Admin/NotificationManagement/Create";

    private static async Task<(TestWebApplicationFactory Factory, HttpClient Admin, string Token)> StartAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, CreateUrl);
        return (factory, admin, token);
    }

    private static FormUrlEncodedContent Form(string token, Guid batch, string title = "Bảo trì", string message = "Tối nay",
        string url = "")
        => new(new Dictionary<string, string>
        {
            ["Title"] = title,
            ["Message"] = message,
            ["Url"] = url,
            ["BatchId"] = batch.ToString(),
            ["__RequestVerificationToken"] = token
        });

    private static async Task<int> AnnouncementRowsAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        return await db.Notifications.CountAsync(n => n.Type == NotificationType.AdminAnnouncement);
    }

    [Fact]
    public async Task TheFormCarriesABatchIdAndALinkField()
    {
        var (factory, admin, _) = await StartAsync();
        await using var _ = factory;
        using var __ = admin;

        var html = System.Net.WebUtility.HtmlDecode(await admin.GetStringAsync(CreateUrl));

        Assert.Matches("name=\"BatchId\"[^>]*value=\"[0-9a-fA-F-]{36}\"|value=\"[0-9a-fA-F-]{36}\"[^>]*name=\"BatchId\"", html);
        Assert.Contains("name=\"Url\"", html);
        Assert.Contains("Đường dẫn trong TCT English, ví dụ /Premium hoặc /Vocabulary", html);
    }

    [Fact]
    public async Task ADoubleSubmit_SendsOnce_AndSaysSo()
    {
        var (factory, admin, token) = await StartAsync();
        await using var _ = factory;
        using var __ = admin;
        var batch = Guid.NewGuid();

        using var first = await admin.PostAsync(CreateUrl, Form(token, batch, url: "/Vocabulary/Index"));
        var afterFirst = await AnnouncementRowsAsync(factory);
        using var second = await admin.PostAsync(CreateUrl, Form(token, batch, url: "/Vocabulary/Index"));
        var afterSecond = await AnnouncementRowsAsync(factory);
        var history = System.Net.WebUtility.HtmlDecode(await admin.GetStringAsync("/Admin/NotificationManagement/History"));

        Assert.Equal(System.Net.HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, second.StatusCode);
        Assert.True(afterFirst > 0);
        Assert.Equal(afterFirst, afterSecond);
        Assert.Contains("Thông báo này đã được gửi trước đó.", history);
    }

    [Theory]
    [InlineData("//evil.example")]
    [InlineData("https://evil.example")]
    [InlineData("javascript:alert(1)")]
    public async Task AnUnsafeLink_ReRendersTheFormWithTheError_AndSendsNothing(string url)
    {
        var (factory, admin, token) = await StartAsync();
        await using var _ = factory;
        using var __ = admin;

        using var response = await admin.PostAsync(CreateUrl, Form(token, Guid.NewGuid(), url: url));
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Chỉ dùng đường dẫn bắt đầu bằng / trong TCT English.", html);
        Assert.Equal(0, await AnnouncementRowsAsync(factory));
    }

    [Fact]
    public async Task AMissingBatchId_ReRendersTheFormWithAFreshOne_AndSendsNothing()
    {
        var (factory, admin, token) = await StartAsync();
        await using var _ = factory;
        using var __ = admin;

        using var response = await admin.PostAsync(CreateUrl, Form(token, Guid.Empty));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("00000000-0000-0000-0000-000000000000", html);
        Assert.Equal(0, await AnnouncementRowsAsync(factory));
    }

    [Fact]
    public async Task ALearner_CannotOpenOrPostTheForm()
    {
        var (factory, admin, token) = await StartAsync();
        await using var _ = factory;
        using var __ = admin;
        using var learner = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var get = await learner.GetAsync(CreateUrl);
        using var post = await learner.PostAsync(CreateUrl, Form(token, Guid.NewGuid()));

        Assert.True(get.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Redirect);
        Assert.True(post.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Redirect
            or System.Net.HttpStatusCode.BadRequest);
        Assert.Equal(0, await AnnouncementRowsAsync(factory));
    }

    [Fact]
    public async Task APostWithoutTheAntiforgeryToken_Is400()
    {
        var (factory, admin, _) = await StartAsync();
        await using var _1 = factory;
        using var __ = admin;

        using var response = await admin.PostAsync(CreateUrl, Form(string.Empty, Guid.NewGuid()));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await AnnouncementRowsAsync(factory));
    }

    [Fact]
    public async Task TheForm_UsesPlainVietnameseCopy_ForTheBellAndHasNoMarkupBuiltFromInput()
    {
        var (factory, admin, _) = await StartAsync();
        await using var _1 = factory;
        using var __ = admin;

        var html = System.Net.WebUtility.HtmlDecode(await admin.GetStringAsync(CreateUrl));

        Assert.Contains("Thông báo sẽ hiện như thế này trong chuông thông báo của người dùng:", html);
        Assert.Contains("(trừ tài khoản bị khóa hoặc đã xóa)", html);
        Assert.DoesNotContain("dropdown của người dùng", html);
        Assert.DoesNotContain("<h6", html);
        Assert.DoesNotMatch("\\.innerHTML\\s*=", html);   // the preview writes text with textContent only
        Assert.Contains("id=\"modalPreviewUrl\"", html);
    }

    [Fact]
    public async Task History_ShowsVietnamTime_TheSender_TheReadCount_AndMarksLegacyRows()
    {
        var (factory, admin, _) = await StartAsync();
        await using var _1 = factory;
        using var __ = admin;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var batch = Guid.NewGuid();
            db.Notifications.AddRange(
                new Notification
                {
                    UserId = TestDataIds.UserId, Type = NotificationType.AdminAnnouncement, Title = "KEYED-TITLE",
                    Message = "Keyed message", RelatedUrl = "/Premium", IsRead = true,
                    EventKey = $"announce:{TestDataIds.AdminUserId}:{batch:N}", CreatedAt = new DateTime(2026, 11, 17, 2, 30, 0, DateTimeKind.Utc)
                },
                new Notification
                {
                    UserId = TestDataIds.OutsiderUserId, Type = NotificationType.AdminAnnouncement, Title = "KEYED-TITLE",
                    Message = "Keyed message", RelatedUrl = "/Premium", IsRead = false,
                    EventKey = $"announce:{TestDataIds.AdminUserId}:{batch:N}", CreatedAt = new DateTime(2026, 11, 17, 2, 30, 0, DateTimeKind.Utc)
                },
                new Notification
                {
                    UserId = TestDataIds.UserId, Type = NotificationType.AdminAnnouncement, Title = "LEGACY-TITLE",
                    Message = "Legacy message", IsRead = true, EventKey = null,
                    CreatedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc)
                });
            await db.SaveChangesAsync();
        }

        var html = System.Net.WebUtility.HtmlDecode(await admin.GetStringAsync("/Admin/NotificationManagement/History"));

        foreach (var column in new[] { "Tiêu đề", "Nội dung", "Thời gian", "Người gửi", "Đã đọc", "Số người nhận" })
        {
            Assert.Matches($"<th scope=\"col\"[^>]*>{column}</th>", html);
        }

        Assert.Contains("17/11/2026 09:30", html);          // 02:30 UTC is 09:30 in Vietnam
        Assert.Contains("01/05/2026 17:00", html);
        Assert.Contains("1/2", html);                         // one of two recipients has read the keyed batch
        Assert.Contains("/Premium", html);
        Assert.Contains("(dữ liệu cũ)", html);
        Assert.Contains("còn 1 bản lưu", html);              // a legacy group is older than the retention period
        Assert.Contains("Quản trị viên", html);              // legacy rows carry no sender
        Assert.Contains("<caption class=\"visually-hidden\">", html);
        Assert.True(html.IndexOf("KEYED-TITLE", StringComparison.Ordinal) < html.IndexOf("LEGACY-TITLE", StringComparison.Ordinal),
            "keyed batches are listed before legacy rows");
    }
}
