using System.Net.Mail;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TCTEnglish.Services.Contact;
using TCTEnglish.Services.Emails;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class ContactMailAndGateTests
{
    private static SmtpAppEmailSender Sender(ContactFakeTransport smtp, Dictionary<string, string?>? settings = null,
        IHttpContextAccessor? accessor = null, RecordingLogger? logger = null) => new(
        new ConfigurationBuilder().AddInMemoryCollection(settings ?? ContactBackendHttpTests.Settings()).Build(), smtp,
        logger ?? new RecordingLogger(), accessor, new EphemeralDataProtectionProvider());

    private static ContactFormViewModel Model(SmtpAppEmailSender sender, string client = "client-a") => new()
    {
        Name = "Nguyễn <An> & O'Neil", Email = "visitor@example.test", Subject = "bug",
        Message = "Đoạn 1 <script>alert('x')</script> & dấu tiếng Việt.\r\nĐoạn 2.", ContactNonce = sender.IssueContactNonce(client)
    };

    [Theory]
    [InlineData("general")]
    [InlineData("khieu-nai-khoa-tai-khoan")]
    [InlineData("bug")]
    [InlineData("feedback")]
    [InlineData("other")]
    public async Task AllFiveSubjectKeys_AreAccepted(string subject)
    {
        var smtp = new ContactFakeTransport();
        var sender = Sender(smtp);
        var model = Model(sender);
        model.Subject = "  " + subject.ToUpperInvariant() + "  ";
        Assert.Equal(subject, model.Subject);
        Assert.Equal(ContactSubmissionStatus.Accepted, await sender.SubmitContactAsync(model, "client-a", null));
        Assert.Contains(model.SubjectDisplayName, smtp.Body);
    }

    [Fact]
    public void ContactPreheader_EncodesExactlyOnce_AndAllParagraphSeparatorsRemainReadable()
    {
        var html = TctEmailRenderer.RenderSupportContact("Nguyễn <An> & O'Neil", "visitor@example.test", "Góp ý & lỗi",
            "Một\rHai\r\nBa\nBốn", null, null);
        var page = new HtmlParser().ParseDocument(html);
        Assert.Contains("Nguyễn <An> & O'Neil: Góp ý & lỗi", page.QuerySelector("body > div")!.TextContent);
        Assert.Contains("Một<br />Hai<br />Ba<br />Bốn", html);
        Assert.DoesNotContain("&amp;lt;An", html);
    }

    [Fact]
    public async Task Routing_UsesSupportThenSenderFallback_WithSingleReplyToAndEncodedHtml()
    {
        foreach (var fallback in new[] { false, true })
        {
            var settings = ContactBackendHttpTests.Settings();
            if (fallback) settings["SmtpSettings:SupportEmail"] = null;
            var smtp = new ContactFakeTransport();
            var logger = new RecordingLogger();
            var sender = Sender(smtp, settings, logger: logger);
            var model = Model(sender);
            var result = await sender.SubmitContactAsync(model, "client-a", new string('x', 512) + "UNBOUNDED_TAIL");
            Assert.Equal(ContactSubmissionStatus.Accepted, result);
            Assert.Equal(fallback ? "sender@example.test" : "support@example.test", smtp.To);
            Assert.Equal("sender@example.test", smtp.From);
            Assert.Equal(model.Email, smtp.ReplyTo);
            Assert.Contains("Nguyễn &lt;An&gt; &amp; O&#39;Neil", smtp.Body);
            Assert.Contains("<br />Đoạn 2.", smtp.Body);
            Assert.DoesNotContain("<script>", smtp.Body);
            Assert.DoesNotContain("UNBOUNDED_TAIL", smtp.Body);
            Assert.DoesNotContain(model.Email, string.Join("\n", logger.Messages));
            Assert.DoesNotContain("client-a", string.Join("\n", logger.Messages));
            Assert.DoesNotContain("Đoạn 1", string.Join("\n", logger.Messages));
        }
    }

    [Theory]
    [InlineData("SmtpSettings:SupportEmail", "first@example.test,second@example.test")]
    [InlineData("SmtpSettings:SupportEmail", "bad")]
    [InlineData("SmtpSettings:SupportEmail", "support@example.test\r\nBcc: second@example.test")]
    [InlineData("SmtpSettings:SenderEmail", "")]
    [InlineData("SmtpSettings:SenderEmail", "bad")]
    [InlineData("SmtpSettings:SenderEmail", "Sender <sender@example.test>")]
    [InlineData("SmtpSettings:Host", "")]
    [InlineData("SmtpSettings:Host", "smtp.example.test\r\n")]
    [InlineData("SmtpSettings:Password", "")]
    [InlineData("SmtpSettings:Port", "bad")]
    [InlineData("SmtpSettings:Port", "0")]
    [InlineData("SmtpSettings:Port", "65536")]
    [InlineData("SmtpSettings:SenderName", "TCT\r\nInjected")]
    public async Task MalformedOrMissingConfiguration_FailsClosedWithoutTransport(string key, string value)
    {
        var settings = ContactBackendHttpTests.Settings();
        settings[key] = value;
        var smtp = new ContactFakeTransport();
        var sender = Sender(smtp, settings);
        Assert.Equal(ContactSubmissionStatus.ConfigurationUnavailable, await sender.SubmitContactAsync(Model(sender), "client-a", null));
        Assert.Equal(0, smtp.Attempts);
    }

    [Fact]
    public async Task SmtpRejectionIsFailed_OtherPostDispatchExceptionsAreUnknown_AndNeverRetryAutomatically()
    {
        foreach (var failure in new Exception[]
        {
            new SmtpException(SmtpStatusCode.MailboxUnavailable), new SmtpException(SmtpStatusCode.ServiceNotAvailable),
            new IOException("synthetic private visitor@example.test"), new SmtpException(SmtpStatusCode.GeneralFailure),
            new OperationCanceledException(), new InvalidOperationException("lost receipt")
        })
        {
            var smtp = new ContactFakeTransport { Failure = failure };
            var logger = new RecordingLogger();
            var sender = Sender(smtp, logger: logger);
            var model = Model(sender);
            var result = await sender.SubmitContactAsync(model, "client-a", null);
            Assert.Equal(failure is SmtpException { StatusCode: SmtpStatusCode.MailboxUnavailable or SmtpStatusCode.ServiceNotAvailable }
                ? ContactSubmissionStatus.Failed : ContactSubmissionStatus.Unknown, result);
            Assert.Equal(ContactSubmissionStatus.Duplicate, await sender.SubmitContactAsync(model, "client-a", null));
            model.Message = "Changed payload";
            Assert.Equal(ContactSubmissionStatus.Duplicate, await sender.SubmitContactAsync(model, "client-a", null));
            Assert.Equal(1, smtp.Attempts);
            Assert.DoesNotContain("visitor@example.test", string.Join("\n", logger.Messages));
        }
    }

    [Fact]
    public async Task StalledDeadlineAndCancellationAfterDispatch_AreUnknown_AndReplayProtected()
    {
        for (var mode = 0; mode < 3; mode++)
        {
            var smtp = new ContactFakeTransport { Stall = true };
            using var cts = new CancellationTokenSource();
            var context = new DefaultHttpContext { RequestAborted = mode == 2 ? cts.Token : default };
            var sender = Sender(smtp, accessor: new HttpContextAccessor { HttpContext = context });
            var model = Model(sender);
            var task = sender.SubmitContactAsync(model, "client-a", null, mode == 1 ? cts.Token : default);
            await smtp.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if (mode != 0) cts.Cancel();
            Assert.Equal(ContactSubmissionStatus.Unknown, await task.WaitAsync(TimeSpan.FromSeconds(4)));
            Assert.Equal(ContactSubmissionStatus.Duplicate, await sender.SubmitContactAsync(model, "client-a", null));
            Assert.Equal(1, smtp.Attempts);
        }
    }

    [Fact]
    public async Task CancellationBeforeDispatch_IsDefiniteFailureWithZeroTransportCalls()
    {
        var smtp = new ContactFakeTransport();
        var sender = Sender(smtp);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Equal(ContactSubmissionStatus.Failed, await sender.SubmitContactAsync(Model(sender), "client-a", null, cts.Token));
        Assert.Equal(0, smtp.Attempts);
    }

    [Fact]
    public async Task ConcurrentSameNonceAndAlteredPayload_AllowOneTransportCall()
    {
        var smtp = new ContactFakeTransport { Stall = true };
        var settings = ContactBackendHttpTests.Settings();
        settings["SmtpSettings:TimeoutSeconds"] = "10";
        var sender = Sender(smtp, settings);
        var firstModel = Model(sender);
        var first = sender.SubmitContactAsync(firstModel, "client-a", null);
        await smtp.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var altered = Model(sender);
        altered.ContactNonce = firstModel.ContactNonce;
        altered.Message = "Altered concurrent payload";
        var replays = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => sender.SubmitContactAsync(altered, "client-a", null)));
        Assert.All(replays, result => Assert.Equal(ContactSubmissionStatus.Duplicate, result));
        smtp.Release.TrySetResult();
        Assert.Equal(ContactSubmissionStatus.Accepted, await first);
        Assert.Equal(1, smtp.Attempts);
    }

    [Fact]
    public async Task Throttle_IsolatesActualClientPartitions_AndWrongClientCannotUseNonce()
    {
        var smtp = new ContactFakeTransport();
        var sender = Sender(smtp);
        var wrong = Model(sender);
        Assert.Equal(ContactSubmissionStatus.InvalidOperation, await sender.SubmitContactAsync(wrong, "client-b", null));
        for (var i = 0; i < 4; i++)
            Assert.Equal(i < 3 ? ContactSubmissionStatus.Accepted : ContactSubmissionStatus.Throttled,
                await sender.SubmitContactAsync(Model(sender), "client-a", null));
        Assert.Equal(ContactSubmissionStatus.Accepted, await sender.SubmitContactAsync(Model(sender, "client-b"), "client-b", null));
        Assert.Equal(4, smtp.Attempts);
    }

    [Fact]
    public void Gate_RejectsExpiredFutureAndTamperedNonce_AndRateMetadataExpires()
    {
        var time = new ManualTime();
        var gate = new ContactOperationGate(new EphemeralDataProtectionProvider(), time);
        var expired = gate.Issue("a");
        time.Now += ContactOperationGate.NonceLifetime;
        Assert.Equal(ContactSubmissionStatus.InvalidOperation, gate.TryBegin(expired, "a", out _));
        var future = gate.Issue("a");
        time.Now -= TimeSpan.FromMinutes(1);
        Assert.Equal(ContactSubmissionStatus.InvalidOperation, gate.TryBegin(future, "a", out _));
        Assert.Equal(ContactSubmissionStatus.InvalidOperation, gate.TryBegin("tampered", "a", out _));
        for (var i = 0; i < 3; i++)
        {
            Assert.Null(gate.TryBegin(gate.Issue("a"), "a", out var id));
            gate.Complete(id);
        }
        Assert.Equal(ContactSubmissionStatus.Throttled, gate.TryBegin(gate.Issue("a"), "a", out _));
        time.Now += ContactOperationGate.RateWindow;
        Assert.Null(gate.TryBegin(gate.Issue("a"), "a", out _));
    }

    [Fact]
    public void Gate_OperationCapacityNeverEvictsPendingSends_AndCompletedMetadataExpires()
    {
        var time = new ManualTime();
        var gate = new ContactOperationGate(new EphemeralDataProtectionProvider(), time);
        var ids = new List<string>();
        for (var i = 0; i < ContactOperationGate.OperationCapacity; i++)
        {
            var client = "client-" + i / 3;
            Assert.Null(gate.TryBegin(gate.Issue(client), client, out var id));
            ids.Add(id);
        }
        time.Now += ContactOperationGate.NonceLifetime + TimeSpan.FromMinutes(2);
        Assert.Equal(ContactSubmissionStatus.Throttled, gate.TryBegin(gate.Issue("new"), "new", out _));
        gate.Complete(ids[0]);
        Assert.Null(gate.TryBegin(gate.Issue("new"), "new", out _));
        Assert.Equal(ContactSubmissionStatus.Throttled, gate.TryBegin(gate.Issue("another"), "another", out _));
    }

    [Fact]
    public void Gate_ClientCapacityIsBounded_AndExpiredWindowsFreeCapacity()
    {
        var time = new ManualTime();
        var gate = new ContactOperationGate(new EphemeralDataProtectionProvider(), time);
        for (var i = 0; i < ContactOperationGate.ClientCapacity; i++)
        {
            var client = "client-" + i;
            Assert.Null(gate.TryBegin(gate.Issue(client), client, out var id));
            gate.Complete(id);
        }
        Assert.Equal(ContactSubmissionStatus.Throttled, gate.TryBegin(gate.Issue("new"), "new", out _));
        time.Now += ContactOperationGate.RateWindow;
        Assert.Null(gate.TryBegin(gate.Issue("new"), "new", out _));
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingLogger : ILogger<SmtpAppEmailSender>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
