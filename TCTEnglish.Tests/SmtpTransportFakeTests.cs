using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SmtpTransportFakeTests
{
    [Fact]
    public async Task DeliveryDeadline_CancelsStalledTransport_AndReturnsFailure()
    {
        var transport = new FakeSmtpTransport { Delay = Timeout.InfiniteTimeSpan };
        var sender = CreateSender(transport, timeoutSeconds: 1);
        var result = await sender.TrySendUnlockedNotificationAsync("user@example.com", true, default)
            .WaitAsync(TimeSpan.FromSeconds(4));
        Assert.False(result);
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task CallerCancellation_StopsBackgroundMailPromptly()
    {
        var transport = new FakeSmtpTransport { Delay = Timeout.InfiniteTimeSpan };
        var sender = CreateSender(transport, timeoutSeconds: 30);
        using var cts = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.TrySendUnlockedNotificationAsync("user@example.com", true, cts.Token).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(transport.CompletedSuccessfully);
    }
    [Fact]
    public async Task SendPasswordReset_WhenTransportCancels_ReturnsWithoutThrowing()
    {
        var transport = new FakeSmtpTransport { Delay = Timeout.InfiniteTimeSpan, CancelAfterMs = 50 };
        var sender = CreateSender(transport, timeoutSeconds: 5);

        var error = await Record.ExceptionAsync(() =>
            sender.SendPasswordResetAsync("user@example.com", "https://example.test/reset"));

        Assert.Null(error);
        Assert.True(transport.Attempts >= 1);
        Assert.False(transport.CompletedSuccessfully);
    }

    [Fact]
    public async Task SendContactMessage_WhenTransportFails_ReturnsFalse()
    {
        var transport = new FakeSmtpTransport { FailWith = new InvalidOperationException("smtp down") };
        var sender = CreateSender(transport, timeoutSeconds: 5);

        var result = await sender.SendContactMessageAsync(
            "Ada",
            "ada@example.com",
            "Hello",
            "Body",
            "127.0.0.1",
            "test-agent");

        Assert.False(result);
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task SendContactMessage_WhenTransportSucceeds_ReturnsTrue()
    {
        var transport = new FakeSmtpTransport();
        var sender = CreateSender(transport, timeoutSeconds: 5);

        var result = await sender.SendContactMessageAsync(
            "Ada",
            "ada@example.com",
            "Hello",
            "Body",
            "127.0.0.1",
            "test-agent");

        Assert.True(result);
        Assert.Equal(1, transport.Attempts);
        Assert.True(transport.CompletedSuccessfully);
        Assert.Contains("Ada", transport.LastBodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileBadges_QueryReturnsOnlyAwardedBadges_PerUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        int otherUserId;
        int badgeId;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var badge = await db.Badges.AsNoTracking().OrderBy(b => b.Id).FirstOrDefaultAsync();
            Assert.NotNull(badge);
            badgeId = badge.Id;

            var other = new User
            {
                Email = $"be6-{Guid.NewGuid():N}@example.com",
                PasswordHash = "hash",
                FullName = "BE6 Other",
                Role = Roles.Standard,
                Status = UserStatus.Online,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(other);
            await db.SaveChangesAsync();
            otherUserId = other.UserId;

            db.UserBadges.Add(new UserBadge
            {
                UserId = TestDataIds.UserId,
                BadgeId = badgeId,
                AwardedAt = DateTime.UtcNow.AddDays(-1)
            });
            db.UserBadges.Add(new UserBadge
            {
                UserId = otherUserId,
                BadgeId = badgeId,
                AwardedAt = DateTime.UtcNow.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var mine = await goals.GetEarnedBadgesForProfileAsync(TestDataIds.UserId);
        var theirs = await goals.GetEarnedBadgesForProfileAsync(otherUserId);

        Assert.Contains(mine, b => b.IsUnlocked);
        Assert.Contains(theirs, b => b.IsUnlocked);
        Assert.All(mine, b => Assert.True(b.IsUnlocked));
        Assert.All(theirs, b => Assert.True(b.IsUnlocked));
    }

    private static SmtpAppEmailSender CreateSender(FakeSmtpTransport transport, int timeoutSeconds)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmtpSettings:Host"] = "smtp.test.local",
                ["SmtpSettings:Port"] = "587",
                ["SmtpSettings:SenderEmail"] = "noreply@test.local",
                ["SmtpSettings:SenderName"] = "TCT Test",
                ["SmtpSettings:Password"] = "not-a-real-secret",
                ["SmtpSettings:SupportEmail"] = "support@test.local",
                ["SmtpSettings:TimeoutSeconds"] = timeoutSeconds.ToString()
            })
            .Build();

        return new SmtpAppEmailSender(config, transport, NullLogger<SmtpAppEmailSender>.Instance);
    }

    private sealed class FakeSmtpTransport : ISmtpMailTransport
    {
        public int Attempts { get; private set; }
        public bool CompletedSuccessfully { get; private set; }
        public string LastBodyHtml { get; private set; } = string.Empty;
        public TimeSpan Delay { get; init; } = TimeSpan.Zero;
        public int? CancelAfterMs { get; init; }
        public Exception? FailWith { get; init; }

        public async Task SendAsync(
            string host,
            int port,
            string username,
            string password,
            MailMessage message,
            CancellationToken cancellationToken)
        {
            Attempts++;
            LastBodyHtml = message.Body ?? string.Empty;

            if (FailWith is not null)
            {
                throw FailWith;
            }

            if (CancelAfterMs is int ms)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linked.CancelAfter(ms);
                await Task.Delay(Delay, linked.Token);
            }
            else if (Delay != TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CompletedSuccessfully = true;
        }
    }
}
