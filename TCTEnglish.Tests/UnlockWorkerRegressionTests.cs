using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TCTEnglish.Hubs;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.Workers;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class UnlockWorkerRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredLocks_CommitBeforeNotifications_ContinueOnFailure_AndDoNotRepeat(bool broadcastFails)
    {
        var sender = new NoticeSender();
        await using var factory = new TestWebApplicationFactory(s => { s.RemoveAll<IAppEmailSender>(); s.AddSingleton<IAppEmailSender>(sender); });
        await factory.InitializeAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            foreach (var id in new[] { TestDataIds.UserId, TestDataIds.OutsiderUserId, TestDataIds.AdminUserId })
            { var user = await db.Users.SingleAsync(u => u.UserId == id); user.Status = UserStatus.Blocked; user.LockReason = "fixture"; user.LockExpiry = id == TestDataIds.AdminUserId ? DateTime.MaxValue : DateTime.UtcNow.AddMinutes(-1); }
            await db.SaveChangesAsync();
        }
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(broadcastFails ? Task.FromException(new InvalidOperationException("fake hub failure")) : Task.CompletedTask);
        var clients = new Mock<IHubClients>(); clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<ClassChatHub>>(); hub.SetupGet(h => h.Clients).Returns(clients.Object);
        using var worker = new AutoUnlockWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<AutoUnlockWorker>.Instance, hub.Object, new ConfigurationBuilder().Build());
        await worker.ProcessExpiredLocksAsync(default);
        await worker.ProcessExpiredLocksAsync(default);
        Assert.Equal(2, sender.Attempts);
        using var verify = factory.Services.CreateScope(); var context = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.NotEqual(UserStatus.Blocked, (await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId)).Status);
        Assert.Equal(UserStatus.Blocked, (await context.Users.SingleAsync(u => u.UserId == TestDataIds.AdminUserId)).Status);
    }

    private sealed class NoticeSender : IAppEmailSender
    {
        public int Attempts { get; private set; }
        public Task SendBlockedNotificationAsync(string email, string reason, DateTime? expiry) => Task.CompletedTask;
        public Task SendUnlockedNotificationAsync(string email, bool automatic) => Task.CompletedTask;
        public Task SendPasswordResetAsync(string email, string link) => Task.CompletedTask;
        public Task<bool> SendContactMessageAsync(string name, string email, string subject, string message, string? ip, string? agent) => Task.FromResult(true);
        public Task<bool> TrySendUnlockedNotificationAsync(string email, bool automatic, CancellationToken ct)
        { Attempts++; return Attempts == 1 ? Task.FromException<bool>(new InvalidOperationException("fake mail failure")) : Task.FromResult(true); }
    }
}
