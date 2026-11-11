using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTEnglish.Services.Accounts;
using TCTEnglish.Services.Learning;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AccountFunctionalCompletionTests
{
    [Fact]
    public void DisplayCode_PadsAtLeastEightDigits_AndDoesNotTruncate()
    {
        Assert.Equal("TCT-00000042", AccountDisplayCode.Format(42));
        Assert.Equal("TCT-123456789", AccountDisplayCode.Format(123456789));
    }

    [Fact]
    public async Task Profile_ShowsStableServerCode_AndDropsPlaceholderClaims()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Account/Profile");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("TCT-00000101", html, StringComparison.Ordinal);
        Assert.DoesNotContain("2.5x", html, StringComparison.Ordinal);
        Assert.DoesNotContain("AI Spaced Repetition", html, StringComparison.Ordinal);
        Assert.DoesNotContain("an toàn 100%", html, StringComparison.Ordinal);
        Assert.DoesNotContain("8892", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangePassword_SocialMarker_DoesNotBypassCurrentPassword()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var email = $"social-{Guid.NewGuid():N}@test.local";
        var userId = await SeedUserAsync(factory, email, "SOCIAL_LOGIN_marker");
        using var scope = factory.Services.CreateScope();
        var security = scope.ServiceProvider.GetRequiredService<IAccountSecurityService>();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await db.Users.SingleAsync(row => row.UserId == userId);

        var result = await security.ChangePasswordAsync(user, "anything", null, "NewPassword1", "NewPassword1");

        Assert.False(result.Succeeded);
        var stored = await db.Users.AsNoTracking().SingleAsync(row => row.UserId == userId);
        Assert.Equal("SOCIAL_LOGIN_marker", stored.PasswordHash);
    }

    [Fact]
    public async Task EmailChange_WithoutSmtp_DoesNotReplaceLoginEmail()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var email = $"local-{Guid.NewGuid():N}@test.local";
        var userId = await SeedUserAsync(factory, email, BCrypt.Net.BCrypt.HashPassword("CorrectHorse1"));
        using var scope = factory.Services.CreateScope();
        var security = scope.ServiceProvider.GetRequiredService<IAccountSecurityService>();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await db.Users.SingleAsync(row => row.UserId == userId);

        var result = await security.RequestEmailChangeAsync(user, "new-" + email, "CorrectHorse1", null, "https://localhost/Account/ConfirmEmailChange?token={0}");

        Assert.False(result.Succeeded);
        var stored = await db.Users.AsNoTracking().SingleAsync(row => row.UserId == userId);
        Assert.Equal(email, stored.Email);
        Assert.Null(stored.EmailVerifiedAt);
    }

    [Fact]
    public async Task Habits_SeventeenOfTwentyGoalDays_Is85Percent_AndTodayIsExcluded()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var userId = await SeedUserAsync(factory, $"habit-{Guid.NewGuid():N}@test.local", BCrypt.Net.BCrypt.HashPassword("CorrectHorse1"));
        using var scope = factory.Services.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<LearningBusinessClock>();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var today = clock.BusinessDate;
        var baseline = clock.GetUtcStartOfBusinessDate(today.AddDays(-21));
        db.UserGoalTargetRevisions.Add(new UserGoalTargetRevision
        {
            UserId = userId,
            GoalArea = (int)GoalArea.Vocabulary,
            TargetValue = 10,
            IsActive = true,
            RevisionNumber = 1,
            EffectiveFromUtc = baseline,
            RecordedAtUtc = baseline
        });
        for (var offset = 1; offset <= 20; offset++)
        {
            var day = today.AddDays(-offset);
            db.UserDailyActivities.Add(new UserDailyActivity
            {
                UserId = userId,
                ActivityDate = day,
                CardsReviewed = offset <= 17 ? 10 : 1
            });
        }

        await db.SaveChangesAsync();
        var habits = scope.ServiceProvider.GetRequiredService<ILearningHabitService>();
        var snapshot = await habits.BuildAsync(userId, 28);

        Assert.True(snapshot.GoalRateKnown);
        Assert.Equal(17, snapshot.GoalDaysMet);
        Assert.Equal(20, snapshot.GoalDaysEligible);
        Assert.Equal(85, snapshot.GoalRatePercent);
    }

    [Fact]
    public async Task Tracker_RetryDoesNotDoubleCount_AndSecondTabDoesNotAddWallTime()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var userId = await SeedUserAsync(factory, $"track-{Guid.NewGuid():N}@test.local", BCrypt.Net.BCrypt.HashPassword("CorrectHorse1"));
        using var scope = factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<ILearningActivityTracker>();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var opened = await tracker.OpenSessionAsync(userId, "Flashcard", null);
        Assert.NotNull(opened);
        var session = await db.LearningActivitySessions.SingleAsync(row => row.SessionId == opened!.Value.SessionId);
        session.LastAcceptedAtUtc = DateTime.UtcNow.AddSeconds(-30);
        await db.SaveChangesAsync();

        var first = await tracker.AcceptCheckpointAsync(userId, new ActivityCheckpointRequest
        {
            SessionId = opened.Value.SessionId,
            Sequence = 1,
            CumulativeActiveSeconds = 30,
            LeaseToken = opened.Value.LeaseToken,
            Visible = true
        });
        var replay = await tracker.AcceptCheckpointAsync(userId, new ActivityCheckpointRequest
        {
            SessionId = opened.Value.SessionId,
            Sequence = 1,
            CumulativeActiveSeconds = 30,
            LeaseToken = opened.Value.LeaseToken,
            Visible = true
        });
        var other = await tracker.OpenSessionAsync(userId, "Quiz", null);
        var overlapped = await tracker.AcceptCheckpointAsync(userId, new ActivityCheckpointRequest
        {
            SessionId = other!.Value.SessionId,
            Sequence = 1,
            CumulativeActiveSeconds = 30,
            LeaseToken = other.Value.LeaseToken,
            Visible = true
        });

        var total = await db.LearningActivitySegments.Where(row => row.UserId == userId).SumAsync(row => row.AcceptedSeconds);
        Assert.True(first.AcceptedSeconds > 0);
        Assert.True(replay.Duplicate);
        Assert.Equal(0, overlapped.AcceptedSeconds);
        Assert.Equal(first.AcceptedSeconds, total);
        Assert.Equal(0, await db.LearningCompletionCredits.CountAsync(row => row.UserId == userId));
    }

    [Fact]
    public void PushEndpointGuard_RejectsPrivateAndOffAllowlistUrls()
    {
        Assert.False(WebPushEndpointGuard.TryNormalize("http://fcm.googleapis.com/fcm/send/a", out _, out _));
        Assert.False(WebPushEndpointGuard.TryNormalize("https://user:pass@fcm.googleapis.com/fcm/send/a", out _, out _));
        Assert.False(WebPushEndpointGuard.TryNormalize("https://example.com/push", out _, out _));
        Assert.False(WebPushEndpointGuard.TryNormalize("https://127.0.0.1/push", out _, out _));
    }

    [Fact]
    public async Task Delivery_SecondWorkerDoesNotSendTwice_AndOptOutSkips()
    {
        var transport = new RecordingPushTransport();
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IWebPushTransport>();
            services.AddSingleton<IWebPushTransport>(transport);
        });
        await factory.InitializeAsync();
        var userId = await SeedUserAsync(factory, $"push-{Guid.NewGuid():N}@test.local", BCrypt.Net.BCrypt.HashPassword("CorrectHorse1"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var endpoint = "https://fcm.googleapis.com/fcm/send/account-test";
        db.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = userId,
            DailyReminderEnabled = true,
            PushOptIn = true,
            ReminderMinutes = DateTime.UtcNow.Hour * 60 + DateTime.UtcNow.Minute,
            TimeZoneId = "UTC",
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.WebPushSubscriptions.Add(new WebPushSubscription
        {
            UserId = userId,
            Endpoint = endpoint,
            EndpointHash = WebPushEndpointGuard.Hash(endpoint),
            P256dh = "key",
            Auth = "auth",
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var processor = scope.ServiceProvider.GetRequiredService<IOutboundDeliveryProcessor>();
        await processor.ScheduleAsync();
        var first = await processor.DispatchDueAsync("worker-a");
        var second = await processor.DispatchDueAsync("worker-b");

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(1, transport.Sends);

        var preference = await db.UserNotificationPreferences.SingleAsync(row => row.UserId == userId);
        preference.PushOptIn = false;
        var extra = new OutboundDelivery
        {
            UserId = userId,
            Channel = OutboundChannels.Push,
            PeriodKey = "daily:extra",
            SubscriptionKey = WebPushEndpointGuard.Hash(endpoint),
            Status = OutboundDeliveryStatuses.Pending,
            NextAttemptAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.OutboundDeliveries.Add(extra);
        await db.SaveChangesAsync();
        await processor.DispatchDueAsync("worker-c");
        var skipped = await db.OutboundDeliveries.AsNoTracking().SingleAsync(row => row.PeriodKey == "daily:extra");
        Assert.Equal(OutboundDeliveryStatuses.Skipped, skipped.Status);
        Assert.Equal(1, transport.Sends);
    }

    [Fact]
    public async Task Delete_WithoutConfirm_KeepsAccount()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var email = $"delete-{Guid.NewGuid():N}@test.local";
        var userId = await SeedUserAsync(factory, email, BCrypt.Net.BCrypt.HashPassword("CorrectHorse1"));
        using var scope = factory.Services.CreateScope();
        var deletion = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var result = await deletion.DeleteAsync(userId, "nope", "CorrectHorse1", null);

        Assert.False(result.Succeeded);
        Assert.NotNull(await db.Users.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId && row.Email == email));
    }

    [Fact]
    public async Task Delete_WithConfirmAndPassword_RemovesSyntheticUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var email = $"gone-{Guid.NewGuid():N}@test.local";
        var userId = await SeedUserAsync(factory, email, BCrypt.Net.BCrypt.HashPassword("CorrectHorse1"));
        using var scope = factory.Services.CreateScope();
        var deletion = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.UserDailyActivities.Add(new UserDailyActivity { UserId = userId, ActivityDate = DateTime.UtcNow.Date, CardsReviewed = 2 });
        await db.SaveChangesAsync();

        var result = await deletion.DeleteAsync(userId, "CONFIRM", "CorrectHorse1", null);

        Assert.True(result.Succeeded, result.Error);
        Assert.Null(await db.Users.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId));
        Assert.Equal(0, await db.UserDailyActivities.CountAsync(row => row.UserId == userId));
    }

    private static async Task<int> SeedUserAsync(TestWebApplicationFactory factory, string email, string passwordHash)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = new User
        {
            Email = email,
            PasswordHash = passwordHash,
            FullName = "Synthetic Learner",
            Role = Roles.Standard,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.UserId;
    }

    private sealed class RecordingPushTransport : IWebPushTransport
    {
        public int Sends { get; private set; }

        public Task<PushSendResult> SendAsync(string endpoint, string p256dh, string auth, string payloadJson, CancellationToken cancellationToken = default)
        {
            Sends++;
            Assert.DoesNotContain("auth", payloadJson, StringComparison.Ordinal);
            return Task.FromResult(PushSendResult.Sent);
        }
    }
}
