using System.Net.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class TestHostSmtpIsolationTests
{
    [Fact]
    public async Task DefaultFactory_BlocksRealDeliveryEvenWhenDevelopmentHasSmtpSecrets()
    {
        await using var factory = new TestWebApplicationFactory();
        var transport = factory.Services.GetRequiredService<ISmtpMailTransport>();
        // Assert the network boundary before invoking it, so even a regression
        // fails without sending to an external SMTP service.
        Assert.IsNotType<SmtpClientMailTransport>(transport);
        using var mail = new MailMessage("synthetic@example.test", "recipient@example.test");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.SendAsync("must-not-be-contacted.example.test", 587,
                "synthetic", "not-a-real-password", mail, default));
    }

    [Fact]
    public async Task ExplicitFakeTransport_CanStillModelSuccessfulDelivery()
    {
        var fake = new RecordingTransport();
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<ISmtpMailTransport>();
            services.AddSingleton<ISmtpMailTransport>(fake);
        });
        var transport = factory.Services.GetRequiredService<ISmtpMailTransport>();
        using var mail = new MailMessage("synthetic@example.test", "recipient@example.test");
        await transport.SendAsync("fake.example.test", 587, "synthetic", "synthetic", mail, default);
        Assert.Equal(1, fake.Sends);
    }

    private sealed class RecordingTransport : ISmtpMailTransport
    {
        public int Sends { get; private set; }
        public Task SendAsync(string host, int port, string username, string password,
            MailMessage message, CancellationToken cancellationToken)
        {
            Sends++;
            return Task.CompletedTask;
        }
    }
}
