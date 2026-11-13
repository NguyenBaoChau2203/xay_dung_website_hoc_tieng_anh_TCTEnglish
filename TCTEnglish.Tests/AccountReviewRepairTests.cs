using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTEnglish.Services.Accounts;
using TCTEnglish.Services.Learning;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;
using Moq;
using System.Net.Mail;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TCTVocabulary.Services;
using AngleSharp.Html.Parser;

namespace TCTEnglish.Tests;

public sealed class AccountReviewRepairTests
{
    private const string Password = "ReviewPassword1!";
    private static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

    [Fact]
    public async Task EmailChange_WithFakeMail_ReplacesAddressOnlyOnOneUseConfirmation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var id = await SeedUserAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        string? html = null;
        var mail = new Mock<IAppEmailSender>();
        mail.Setup(sender => sender.TrySendTrackedHtmlAsync("replacement@example.test", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => html = body).ReturnsAsync(EmailDispatchResult.Sent);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SmtpSettings:Host"] = "smtp.example.test", ["SmtpSettings:SenderEmail"] = "sender@example.test", ["SmtpSettings:Password"] = "synthetic"
        }).Build();
        var service = new AccountSecurityService(db, mail.Object, config, TimeProvider.System);
        var user = await db.Users.SingleAsync(row => row.UserId == id);
        var oldEmail = user.Email;
        Assert.True((await service.RequestEmailChangeAsync(user, "replacement@example.test", Password, null,
            "https://learning.example.test/Account/ConfirmEmailChange?token={0}")).Succeeded);
        Assert.Equal(oldEmail, (await db.Users.AsNoTracking().SingleAsync(row => row.UserId == id)).Email);
        var token = Regex.Match(WebUtility.HtmlDecode(html!), "token=([a-f0-9]+)").Groups[1].Value;
        Assert.NotEmpty(token);
        Assert.True((await service.ConfirmEmailChangeAsync(id, token)).Succeeded);
        Assert.False((await service.ConfirmEmailChangeAsync(id, token)).Succeeded);
        var saved = await db.Users.AsNoTracking().SingleAsync(row => row.UserId == id);
        Assert.Equal("replacement@example.test", saved.Email);
        Assert.NotNull(saved.EmailVerifiedAt);
        Assert.Equal(1, saved.SecurityVersion);
        Assert.All(await db.AccountSecurityChallenges.Where(row => row.UserId == id).ToListAsync(), challenge =>
        {
            Assert.NotEqual(token, challenge.TokenDigest);
            Assert.NotNull(challenge.ConsumedAtUtc);
        });
    }

    [Fact]
    public async Task PasswordChange_ExecutionStrategyRetryPersistsTheNewSecurityState()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var id = await SeedUserAsync(factory);
        using var scope = factory.Services.CreateScope();
        var original = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var interceptor = new FailUserSaveOnce();
        var options = new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(original.Database.GetDbConnection())
            .ReplaceService<IExecutionStrategyFactory, ReviewRetryFactory>().AddInterceptors(interceptor).Options;
        await using var db = new DbflashcardContext(options);
        var user = await db.Users.SingleAsync(row => row.UserId == id);
        var security = new AccountSecurityService(db, Mock.Of<IAppEmailSender>(), new ConfigurationBuilder().Build(), TimeProvider.System);
        var result = await security.ChangePasswordAsync(user, Password, null, "ChangedPassword2!", "ChangedPassword2!");
        Assert.True(result.Succeeded);
        Assert.True(interceptor.FailedOnce);
        var saved = await db.Users.AsNoTracking().SingleAsync(row => row.UserId == id);
        Assert.True(BCrypt.Net.BCrypt.Verify("ChangedPassword2!", saved.PasswordHash));
        Assert.Equal(1, saved.SecurityVersion);
    }

    private sealed class ReviewTransientException : Exception;
    [Fact]
    public async Task AdminSecurityEdit_ExecutionStrategyRetryPersistsRoleAndVersion()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var id = await SeedUserAsync(factory);
        using var scope = factory.Services.CreateScope();
        var original = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var interceptor = new FailUserSaveOnce();
        var options = new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(original.Database.GetDbConnection())
            .ReplaceService<IExecutionStrategyFactory, ReviewRetryFactory>().AddInterceptors(interceptor).Options;
        await using var db = new DbflashcardContext(options);
        var controller = ActivatorUtilities.CreateInstance<TCTVocabulary.Areas.Admin.Controllers.UserManagementController>(scope.ServiceProvider, db);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        var user = await db.Users.AsNoTracking().SingleAsync(row => row.UserId == id);
        Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.Edit(new TCTVocabulary.Areas.Admin.ViewModels.UpdateUserRequest
        {
            UserId = id, Email = user.Email, FullName = "Admin reviewed name", Role = Roles.Premium
        }));
        Assert.True(interceptor.FailedOnce);
        var saved = await db.Users.AsNoTracking().SingleAsync(row => row.UserId == id);
        Assert.Equal(Roles.Premium, saved.Role);
        Assert.Equal(1, saved.SecurityVersion);
        Assert.Equal("Admin reviewed name", saved.FullName);
    }

    private sealed class FailUserSaveOnce : DbCommandInterceptor
    {
        public bool FailedOnce { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!FailedOnce && command.CommandText.StartsWith("UPDATE \"Users\"", StringComparison.Ordinal))
            {
                FailedOnce = true;
                throw new ReviewTransientException();
            }
            return ValueTask.FromResult(result);
        }
    }
    public sealed class ReviewRetryFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new ReviewRetryStrategy(dependencies);
    }
    private sealed class ReviewRetryStrategy(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is ReviewTransientException;
    }

    [Fact]
    public async Task AvatarUpdate_CannotRestorePersonalDataAfterConcurrentDeletion()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var id = await SeedUserAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var avatar = new Mock<IAvatarUploadService>();
        avatar.Setup(service => service.StageAvatarAsync(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await db.Users.Where(row => row.UserId == id).ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.AnonymizedAt, DateTime.UtcNow).SetProperty(row => row.FullName, "Deleted account"));
                return "/images/avatars/review-staged.png";
            });
        var service = ActivatorUtilities.CreateInstance<AccountProfileService>(scope.ServiceProvider, avatar.Object);
        using var content = new MemoryStream(new byte[] { 1 });
        var result = await service.UpdateAsync(id, "Restored personal name", new Microsoft.AspNetCore.Http.FormFile(content, 0, 1, "Avatar", "fixture.png"));
        Assert.False(result.Succeeded);
        Assert.Equal("Deleted account", (await db.Users.AsNoTracking().SingleAsync(row => row.UserId == id)).FullName);
        avatar.Verify(store => store.DeleteOwnedAvatar("/images/avatars/review-staged.png"), Times.Once);
    }

    [Fact]
    public void PushConfiguration_RequiresAValidMatchingKeyPairAndContact()
    {
        var options = new OutboundDeliveryOptions { VapidPublicKey = "placeholder", VapidPrivateKey = "placeholder" };
        Assert.False(options.PushConfigured);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pair = key.ExportParameters(true);
        options.VapidPublicKey = Base64Url(new byte[] { 4 }.Concat(pair.Q.X!).Concat(pair.Q.Y!).ToArray());
        options.VapidPrivateKey = Base64Url(pair.D!);
        options.VapidSubject = "mailto:notifications@example.test";
        Assert.True(options.PushConfigured);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        options.VapidPrivateKey = Base64Url(other.ExportParameters(true).D!);
        Assert.False(options.PushConfigured);
    }

    [Theory]
    [InlineData("profile", "profileEmailInput")]
    [InlineData("settings", "profileEmailInput")]
    [InlineData("", "profileEmailInput")]
    public async Task EmailValidation_PreservesOnlyNonSecretInput(string surface, string inputId)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var id = await SeedUserAsync(factory);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, id, Roles.Standard);
        var html = await client.GetStringAsync("/Account/Profile?tab=security");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var response = await client.PostAsync("/Account/UpdateEmail", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token), ["ReturnSurface"] = surface,
            ["Email"] = "invalid-edited-address", ["CurrentPassword"] = "secret-do-not-echo", ["ProofCode"] = "proof-do-not-echo"
        }));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches($"id=\"{inputId}\"[^>]*value=\"invalid-edited-address\"", body);
        Assert.DoesNotContain("secret-do-not-echo", body);
        Assert.DoesNotContain("proof-do-not-echo", body);
        Assert.DoesNotContain("Email chính chủ đã xác thực", body);
    }

    [Fact]
    public async Task WeeklySummary_ClipsAndUnionsAcceptedIntervalsAtBusinessWeekBoundaries()
    {
        var clock = new ReviewTimeProvider { Now = new DateTimeOffset(2026, 11, 16, 2, 0, 0, TimeSpan.Zero) };
        string? sentHtml = null;
        var mail = new Mock<IAppEmailSender>();
        mail.Setup(sender => sender.TrySendTrackedHtmlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => sentHtml = body)
            .ReturnsAsync(EmailDispatchResult.Sent);
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            services.RemoveAll<IAppEmailSender>(); services.AddSingleton(mail.Object);
            services.Configure<OutboundDeliveryOptions>(options => options.PublicBaseUrl = "https://learning.example.test");
        });
        await factory.InitializeAsync();
        var id = await SeedUserAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        await db.Users.Where(row => row.UserId == id).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.EmailVerifiedAt, now));
        db.UserNotificationPreferences.Add(new UserNotificationPreference { UserId = id, WeeklyEmailOptIn = true, TimeZoneId = "Asia/Ho_Chi_Minh", UpdatedAtUtc = now });
        var session = new LearningActivitySession { SessionId = Guid.NewGuid(), UserId = id, Skill = "Speaking", Mode = "video", LeaseToken = "fixture", StartedAtUtc = now, LastAcceptedAtUtc = now, LeaseExpiresAtUtc = now };
        db.LearningActivitySessions.Add(session);
        var weekStart = new DateTime(2026, 11, 8, 17, 0, 0, DateTimeKind.Utc);
        var intervals = new[] { (weekStart.AddMinutes(-1), weekStart.AddMinutes(1)), (weekStart, weekStart.AddMinutes(1)), (weekStart.AddDays(7).AddMinutes(-1), weekStart.AddDays(7).AddMinutes(1)) };
        for (var i = 0; i < intervals.Length; i++)
            db.LearningActivitySegments.Add(new LearningActivitySegment { UserId = id, SessionId = session.SessionId, Skill = "Speaking", Sequence = i + 1, StartedAtUtc = intervals[i].Item1, EndedAtUtc = intervals[i].Item2, AcceptedSeconds = (int)(intervals[i].Item2 - intervals[i].Item1).TotalSeconds });
        db.OutboundDeliveries.Add(new OutboundDelivery { UserId = id, Channel = OutboundChannels.Email, SubscriptionKey = "email", PeriodKey = "weekly:2026-11-09", Status = OutboundDeliveryStatuses.Pending, CreatedAtUtc = now, NextAttemptAtUtc = now });
        await db.SaveChangesAsync();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IOutboundDeliveryProcessor>().DispatchDueAsync("week-worker"));
        Assert.NotNull(sentHtml);
        using var rendered = new HtmlParser().ParseDocument(sentHtml);
        var visibleText = Regex.Replace(rendered.Body!.TextContent, @"\s+", " ");
        Assert.Contains("Thời gian tương tác 2 phút", visibleText);
        Assert.Contains("Tuần 09/11 – 15/11 (giờ Việt Nam)", visibleText);
        Assert.Contains(rendered.QuerySelectorAll("a"), anchor =>
            anchor.GetAttribute("href") == "https://learning.example.test/Account/Settings"
            && anchor.TextContent.Contains("Quản lý hoặc tắt email tuần", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SmtpTransportConnectionLoss_IsUnknownRatherThanAutomaticallyRetryable()
    {
        var transport = new Mock<ISmtpMailTransport>();
        transport.Setup(sender => sender.SendAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SmtpException(SmtpStatusCode.GeneralFailure, "Connection lost after dispatch"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SmtpSettings:Host"] = "smtp.example.test", ["SmtpSettings:SenderEmail"] = "sender@example.test",
            ["SmtpSettings:Password"] = "synthetic-test-setting"
        }).Build();
        var sender = new SmtpAppEmailSender(config, transport.Object, NullLogger<SmtpAppEmailSender>.Instance);
        Assert.Equal(EmailDispatchResult.Unknown, await sender.TrySendTrackedHtmlAsync("recipient@example.test", "Test", "<p>Test</p>"));
    }

    [Fact]
    public async Task CleanupExhaustion_IsRecordedAsFailed()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var job = new AccountFileCleanupJob { FormerUserId = 999999, PublicUrl = "/images/avatars/unavailable.png", AttemptCount = 4, NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        db.AccountFileCleanupJobs.Add(job);
        await db.SaveChangesAsync();
        var processor = new AccountFileCleanupProcessor(db, Mock.Of<IAvatarUploadService>(), TimeProvider.System);
        Assert.Equal(0, await processor.RunOnceAsync());
        Assert.Equal(AccountFileCleanupStatuses.Failed, job.Status);
    }

    [Theory]
    [InlineData(null, "Standard", false)]
    [InlineData("1", "Standard", false)]
    [InlineData("0", "Admin", false)]
    [InlineData("0", "Standard", true)]
    public async Task SessionValidation_RequiresFreshVersionAndActualRole(string? version, string role, bool expected)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, TestDataIds.UserId.ToString()), new(ClaimTypes.Role, role) };
        if (version != null) claims.Add(new Claim(AccountSecurityClaims.Version, version));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
        Assert.Equal(expected, await AccountSessionValidator.IsValidAsync(scope.ServiceProvider.GetRequiredService<DbflashcardContext>(), principal, DateTime.UtcNow));
    }

    [Fact]
    public async Task Habits_SplitHourBoundaryAndClipTheSelectedDateWindow()
    {
        var clock = new ReviewTimeProvider();
        await using var factory = CreateFactory(clock);
        await factory.InitializeAsync();
        var userId = await SeedUserAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var session = new LearningActivitySession
        {
            SessionId = Guid.NewGuid(), UserId = userId, Skill = "Speaking", Mode = "video", LeaseToken = "fixture",
            StartedAtUtc = new DateTime(2026, 11, 4, 16, 59, 0, DateTimeKind.Utc), LastAcceptedAtUtc = clock.GetUtcNow().UtcDateTime,
            LeaseExpiresAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.LearningActivitySessions.Add(session);
        db.LearningActivitySegments.Add(new LearningActivitySegment
        {
            SessionId = session.SessionId, UserId = userId, Skill = "Speaking", Sequence = 1,
            StartedAtUtc = session.StartedAtUtc, EndedAtUtc = session.StartedAtUtc.AddMinutes(2), AcceptedSeconds = 120
        });
        await db.SaveChangesAsync();
        var habits = scope.ServiceProvider.GetRequiredService<ILearningHabitService>();
        Assert.StartsWith("1 phút", (await habits.BuildAsync(userId, 7)).EstimatedTimeText);
        for (var offset = 1; offset <= 3; offset++)
        {
            var start = new DateTime(2026, 11, 11, 12, 59, 0, DateTimeKind.Utc).AddDays(-offset);
            db.LearningActivitySegments.Add(new LearningActivitySegment
            {
                SessionId = session.SessionId, UserId = userId, Skill = "Speaking", Sequence = offset + 1,
                StartedAtUtc = start, EndedAtUtc = start.AddMinutes(10), AcceptedSeconds = 600
            });
        }
        await db.SaveChangesAsync();
        Assert.Equal("20:00–21:00", (await habits.BuildAsync(userId, 7)).UsualHourText);
    }

    [Fact]
    public async Task Profile_Exposes28DaySelection_AndPasswordProofCanReplaceCurrentPassword()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var html = await client.GetStringAsync("/Account/Profile?habitDays=28&tab=security");
        Assert.Contains("habitDays=28", html);
        var form = Regex.Match(html, "<form[^>]*action=\"/Account/ChangePassword\"[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.True(form.Success);
        Assert.Contains("name=\"ProofCode\"", form.Value);
        var current = Regex.Match(form.Value, "<input[^>]*name=\"CurrentPassword\"[^>]*>");
        Assert.DoesNotContain("required", current.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecurityProof_IsConsumedAcrossRequestScopes()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var userId = await SeedUserAsync(factory);
        await SeedChallengeAsync(factory, userId, AccountProofPurpose.Deletion, "one-use-proof");

        using (var first = factory.Services.CreateScope())
        {
            var db = first.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var user = await db.Users.SingleAsync(row => row.UserId == userId);
            Assert.True(await first.ServiceProvider.GetRequiredService<IAccountSecurityService>()
                .HasFreshProofAsync(user, null, "one-use-proof", AccountProofPurpose.Deletion));
        }

        using var second = factory.Services.CreateScope();
        var secondDb = second.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var secondUser = await secondDb.Users.SingleAsync(row => row.UserId == userId);
        Assert.False(await second.ServiceProvider.GetRequiredService<IAccountSecurityService>()
            .HasFreshProofAsync(secondUser, null, "one-use-proof", AccountProofPurpose.Deletion));
    }

    [Fact]
    public async Task PasswordChange_DoesNotTrustAUserLoadedBeforeAnotherSecurityChange()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var userId = await SeedUserAsync(factory);
        using var stale = factory.Services.CreateScope();
        var staleDb = stale.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var staleUser = await staleDb.Users.SingleAsync(row => row.UserId == userId);
        using (var other = factory.Services.CreateScope())
        {
            var db = other.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var newHash = BCrypt.Net.BCrypt.HashPassword("ChangedInAnotherSession1!");
            await db.Users.Where(row => row.UserId == userId).ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.PasswordHash, newHash)
                .SetProperty(row => row.SecurityVersion, row => row.SecurityVersion + 1));
        }

        var result = await stale.ServiceProvider.GetRequiredService<IAccountSecurityService>()
            .ChangePasswordAsync(staleUser, Password, null, "AttackerOverwrite1!", "AttackerOverwrite1!");
        Assert.False(result.Succeeded);
        using var verify = factory.Services.CreateScope();
        var stored = await verify.ServiceProvider.GetRequiredService<DbflashcardContext>().Users.AsNoTracking()
            .SingleAsync(row => row.UserId == userId);
        Assert.True(BCrypt.Net.BCrypt.Verify("ChangedInAnotherSession1!", stored.PasswordHash));
    }

    [Fact]
    public async Task Tracker_ZeroElapsedWallTime_CannotCreateStudySeconds()
    {
        var clock = new ReviewTimeProvider();
        await using var factory = CreateFactory(clock);
        await factory.InitializeAsync();
        var userId = await SeedUserAsync(factory);
        using var scope = factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<ILearningActivityTracker>();
        var session = await tracker.OpenSessionAsync(userId, "Flashcard", null);
        Assert.NotNull(session);
        var result = await tracker.AcceptCheckpointAsync(userId, new ActivityCheckpointRequest
        {
            SessionId = session.Value.SessionId, LeaseToken = session.Value.LeaseToken,
            Sequence = 1, CumulativeActiveSeconds = 30, Visible = true
        });
        Assert.Equal(0, result.AcceptedSeconds);
    }

    [Fact]
    public async Task PushSubscription_CannotTakeOverAnotherAccountsEndpoint()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var firstId = await SeedUserAsync(factory);
        var otherId = await SeedUserAsync(factory);
        const string endpoint = "https://fcm.googleapis.com/fcm/send/owner-bound-review";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.WebPushSubscriptions.Add(new WebPushSubscription
        {
            UserId = firstId, Endpoint = endpoint, EndpointHash = WebPushEndpointGuard.Hash(endpoint),
            P256dh = "original-key", Auth = "original-auth", CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var q = key.ExportParameters(false).Q;
        var encoded = new byte[] { 4 }.Concat(q.X!).Concat(q.Y!).ToArray();
        var input = new PushSubscriptionInput
        {
            Endpoint = endpoint, P256dh = Base64Url(encoded), Auth = Base64Url(RandomNumberGenerator.GetBytes(16))
        };

        var result = await scope.ServiceProvider.GetRequiredService<IWebPushSubscriptionService>()
            .SubscribeAsync(otherId, input);
        Assert.False(result.Succeeded);
        var stored = await db.WebPushSubscriptions.AsNoTracking().SingleAsync(row => row.EndpointHash == WebPushEndpointGuard.Hash(endpoint));
        Assert.Equal(firstId, stored.UserId);
        Assert.Equal("original-key", stored.P256dh);
        Assert.Null(stored.RevokedAtUtc);
    }

    [Fact]
    public async Task ExpiredDeliveryLease_BecomesUnknownInsteadOfStayingLeasedForever()
    {
        var clock = new ReviewTimeProvider();
        var transport = new RecordingPushTransport();
        await using var factory = CreateFactory(clock, transport);
        await factory.InitializeAsync();
        var userId = await SeedUserAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var delivery = AddPushDelivery(db, userId, clock, "Leased");
        delivery.LeaseOwner = "stopped-worker";
        delivery.LeaseUntilUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(-1);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IOutboundDeliveryProcessor>().DispatchDueAsync("replacement-worker");
        await db.Entry(delivery).ReloadAsync();
        Assert.Equal(OutboundDeliveryStatuses.Unknown, delivery.Status);
        Assert.Equal(0, transport.Sends);
    }

    [Theory]
    [InlineData("disabled", "Skipped")]
    [InlineData("completed", "Skipped")]
    [InlineData("expired", "Expired")]
    public async Task QueuedDailyDelivery_RechecksEligibilityBeforeSending(string reason, string expected)
    {
        var clock = new ReviewTimeProvider();
        var transport = new RecordingPushTransport();
        await using var factory = CreateFactory(clock, transport);
        await factory.InitializeAsync();
        var userId = await SeedUserAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        const string endpoint = "https://fcm.googleapis.com/fcm/send/queued-review";
        db.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = userId, DailyReminderEnabled = reason != "disabled", PushOptIn = true,
            ReminderMinutes = 13 * 60, TimeZoneId = "UTC", UpdatedAtUtc = now
        });
        db.WebPushSubscriptions.Add(new WebPushSubscription
        {
            UserId = userId, Endpoint = endpoint, EndpointHash = WebPushEndpointGuard.Hash(endpoint),
            P256dh = "key", Auth = "auth", CreatedAtUtc = now, LastSeenAtUtc = now
        });
        var delivery = AddPushDelivery(db, userId, clock, OutboundDeliveryStatuses.Pending);
        delivery.SubscriptionKey = WebPushEndpointGuard.Hash(endpoint);
        if (reason == "expired") delivery.PeriodKey = "daily:2026-11-10";
        if (reason == "completed")
        {
            db.UserGoals.Add(new UserGoal { UserId = userId, GoalArea = GoalArea.Speaking, TargetValue = 1, IsActive = true, CreatedAt = now, UpdatedAt = now });
            db.UserDailyActivities.Add(new UserDailyActivity { UserId = userId, ActivityDate = new DateTime(2026, 11, 11), SpeakingCompletedCount = 1 });
        }
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IOutboundDeliveryProcessor>().DispatchDueAsync("worker");
        await db.Entry(delivery).ReloadAsync();
        Assert.Equal(expected, delivery.Status);
        Assert.Equal(0, transport.Sends);
    }

    [Fact]
    public async Task ExistingAddress_CanBeVerifiedWithoutChangingLoginEmail()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var userId = await SeedUserAsync(factory);
        await SeedChallengeAsync(factory, userId, "VerifyEmail", "verify-current-address");
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, Roles.Standard);
        var html = await client.GetStringAsync("/Account/Profile?tab=security");
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);
        using var response = await client.PostAsync("/Account/VerifyCurrentEmail", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value),
            ["ProofCode"] = "verify-current-address", ["returnSurface"] = "profile"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<DbflashcardContext>().Users.AsNoTracking().SingleAsync(row => row.UserId == userId);
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.EndsWith("@example.test", user.Email, StringComparison.Ordinal);
    }

    private static TestWebApplicationFactory CreateFactory(ReviewTimeProvider clock, RecordingPushTransport? push = null)
        => new(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            if (push != null)
            {
                services.RemoveAll<IWebPushTransport>();
                services.AddSingleton<IWebPushTransport>(push);
            }
        });

    private static async Task<int> SeedUserAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = new User { Email = $"review-{Guid.NewGuid():N}@example.test", FullName = "Account Review", PasswordHash = PasswordHash, Role = Roles.Standard, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.UserId;
    }

    private static async Task SeedChallengeAsync(TestWebApplicationFactory factory, int userId, string purpose, string token)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.AccountSecurityChallenges.Add(new AccountSecurityChallenge
        {
            UserId = userId, Purpose = purpose, TokenDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant(),
            SecurityVersion = 0, CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        });
        await db.SaveChangesAsync();
    }

    private static OutboundDelivery AddPushDelivery(DbflashcardContext db, int userId, ReviewTimeProvider clock, string status)
    {
        var delivery = new OutboundDelivery
        {
            UserId = userId, Channel = OutboundChannels.Push, PeriodKey = "daily:2026-11-11", SubscriptionKey = "review-subscription",
            Status = status, NextAttemptAtUtc = clock.GetUtcNow().UtcDateTime, CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.OutboundDeliveries.Add(delivery);
        return delivery;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class ReviewTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 11, 13, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingPushTransport : IWebPushTransport
    {
        public int Sends { get; private set; }
        public Task<PushSendResult> SendAsync(string endpoint, string p256dh, string auth, string payloadJson, CancellationToken cancellationToken = default)
        {
            Sends++;
            return Task.FromResult(PushSendResult.Sent);
        }
    }
}
