using System.Net;
using System.Net.Mail;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class ContactBackendHttpTests
{
    [Fact]
    public async Task ExistingProductionRegistration_ResolvesProtectedNonceWithoutNewDi()
    {
        await using var factory = new TestWebApplicationFactory();
        var sender = factory.Services.GetRequiredService<IAppEmailSender>();
        Assert.IsType<SmtpAppEmailSender>(sender);
        Assert.NotEmpty(sender.IssueContactNonce("synthetic-client"));
    }
    [Fact]
    public async Task UnknownPostedSubject_IsRejectedWithoutMail()
    {
        var smtp = new ContactFakeTransport();
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        var fields = await Form(client);
        fields["Subject"] = "tampered";
        var response = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, smtp.Attempts);
        Assert.Contains("Subject", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task KnownSmtpRejection_PreservesPostedTextWithoutRedirect()
    {
        var smtp = new ContactFakeTransport { Failure = new SmtpException(SmtpStatusCode.MailboxUnavailable) };
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        var fields = await Form(client);
        fields["Name"] = "  Nguyễn <An>  ";
        fields["Message"] = "  Đoạn một & hai.\r\nNội dung tiếp theo.  ";
        var response = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var page = await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync());
        Assert.Equal("Nguyễn <An>", page.QuerySelector("#Name")!.GetAttribute("value"));
        Assert.Equal(fields["Message"].Trim(), page.QuerySelector("#Message")!.TextContent.Trim());
        Assert.Equal(1, smtp.Attempts);
        fields["ContactNonce"] = page.QuerySelector("#ContactNonce")!.GetAttribute("value")!;
        smtp.Failure = null;
        Assert.Equal(HttpStatusCode.Found, (await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(2, smtp.Attempts);
    }

    [Fact]
    public async Task MissingAndInvalidAntiforgery_DoNotSend()
    {
        var smtp = new ContactFakeTransport();
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        var fields = await Form(client);
        fields.Remove("__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields))).StatusCode);
        fields["__RequestVerificationToken"] = "invalid";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(0, smtp.Attempts);
    }

    [Fact]
    public async Task AnonymousAppealAndUnknownGetSubject_AreAvailableWithoutDispatchOrFakePublicDetails()
    {
        var smtp = new ContactFakeTransport();
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        foreach (var subject in new[] { "khieu-nai-khoa-tai-khoan", "unknown", "" })
        {
            var response = await client.GetAsync("/Home/Contact?subject=" + subject);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            var html = await response.Content.ReadAsStringAsync();
            var page = await new HtmlParser().ParseDocumentAsync(html);
            Assert.Equal(subject.StartsWith("khieu") ? subject : "general", page.QuerySelector("#Subject option[selected]")!.GetAttribute("value"));
            Assert.NotEmpty(page.QuerySelector("#ContactNonce")!.GetAttribute("value")!);
            Assert.DoesNotContain("support.tctenglish@gmail.com", html);
            Assert.DoesNotContain("+84 123 456 789", page.QuerySelector(".contact-grid")!.TextContent);
            Assert.DoesNotContain("24 giờ", html);
            Assert.DoesNotContain("sender@example.test", html);
            Assert.DoesNotContain("support@example.test", html);
        }
        Assert.Equal(0, smtp.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Success_UsesPrgAndClearsForm_AndReplayCannotSendChangedContent(bool authenticated)
    {
        var smtp = new ContactFakeTransport();
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        if (authenticated) client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, "1");
        var fields = await Form(client, "khieu-nai-khoa-tai-khoan");
        fields["To"] = "attacker@example.test";
        fields["From"] = "attacker@example.test";
        fields["State"] = "Ready";
        var sent = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Found, sent.StatusCode);
        Assert.DoesNotContain("visitor", sent.Headers.Location!.ToString());
        Assert.Contains("subject=khieu-nai-khoa-tai-khoan", sent.Headers.Location.ToString());
        var completed = await client.GetAsync(sent.Headers.Location);
        var page = await new HtmlParser().ParseDocumentAsync(await completed.Content.ReadAsStringAsync());
        Assert.Equal("", page.QuerySelector("#Name")!.GetAttribute("value"));
        Assert.Equal("", page.QuerySelector("#Message")!.TextContent.Trim());
        await client.GetAsync(sent.Headers.Location);
        foreach (var changed in new[] { false, true })
        {
            if (changed) fields["Message"] = "Different content";
            var replay = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
            Assert.Contains("data-contact-state=\"Duplicate\"", await replay.Content.ReadAsStringAsync());
        }
        Assert.Equal(1, smtp.Attempts);
        Assert.Equal("support@example.test", smtp.To);
        Assert.Equal("sender@example.test", smtp.From);
        Assert.Equal(fields["Email"], smtp.ReplyTo);
    }

    [Fact]
    public async Task InvalidInputs_AreRejectedAndEncoded_WithoutAnyMail()
    {
        var smtp = new ContactFakeTransport();
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        var original = await Form(client);
        var bad = new (string Key, string Value)[]
        {
            ("Name", ""), ("Name", " \t "), ("Name", new string('A', 101)), ("Name", "An\r\nInjected"),
            ("Email", ""), ("Email", " \t "), ("Email", new string('x', 255)), ("Email", "bad"),
            ("Email", "one@example.test,two@example.test"), ("Email", "Name <one@example.test>"),
            ("Email", "one@example.test\r\nBcc: two@example.test"), ("Email", "one@example.test\r\n"),
            ("Subject", ""), ("Subject", "tampered"), ("Subject", new string('x', 51)), ("Subject", "general\r\n"),
            ("Message", ""), ("Message", " \r\n\t"), ("Message", new string('ế', 2001))
        };
        foreach (var (key, value) in bad)
        {
            var fields = new Dictionary<string, string>(original) { [key] = value };
            fields["Message"] = key == "Message" ? value : "<script>alert('x')</script> & nội dung";
            var response = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("data-contact-state=\"Invalid\"", html);
            Assert.DoesNotContain("<script>alert('x')</script>", html);
            Assert.Equal(0, smtp.Attempts);
        }
    }

    [Fact]
    public async Task TrimmedExactLimitsAndPercentEncodedVietnamese_AreAccepted()
    {
        var smtp = new ContactFakeTransport();
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        var fields = await Form(client);
        fields["Name"] = "  " + new string('ễ', 100) + "  ";
        fields["Email"] = new string('a', 64) + "@" + new string('a', 63) + "." + new string('b', 63) + "." + new string('c', 61);
        Assert.Equal(254, fields["Email"].Length);
        fields["Message"] = "  " + new string('ế', 2000) + "  ";
        using var content = new FormUrlEncodedContent(fields);
        Assert.InRange((await content.ReadAsByteArrayAsync()).Length, 18000, 32768);
        Assert.Equal(HttpStatusCode.Found, (await client.PostAsync("/Home/Contact", content)).StatusCode);
        Assert.Equal(1, smtp.Attempts);
    }

    [Fact]
    public async Task UnknownDelivery_PreservesTextAndNonce_AndDoesNotResend()
    {
        var smtp = new ContactFakeTransport { Failure = new IOException("lost receipt") };
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        var fields = await Form(client);
        var response = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var page = await new HtmlParser().ParseDocumentAsync(html);
        Assert.Contains("data-contact-state=\"Unknown\"", html);
        Assert.Equal(fields["Message"], page.QuerySelector("#Message")!.TextContent.Trim());
        Assert.Equal(fields["ContactNonce"], page.QuerySelector("#ContactNonce")!.GetAttribute("value"));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(1, smtp.Attempts);
    }

    [Fact]
    public async Task ConfigurationUnavailable_PreservesFormAndSendsNothing()
    {
        var smtp = new ContactFakeTransport();
        var settings = Settings();
        settings["SmtpSettings:Host"] = null;
        await using var factory = CreateFactory(smtp, settings);
        await factory.InitializeAsync();
        using var client = Client(factory);
        var fields = await Form(client);
        var response = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-contact-state=\"Unavailable\"", html);
        var page = await new HtmlParser().ParseDocumentAsync(html);
        Assert.Equal(fields["Email"], page.QuerySelector("#Email")!.GetAttribute("value"));
        Assert.NotEqual(fields["ContactNonce"], page.QuerySelector("#ContactNonce")!.GetAttribute("value"));
        Assert.Equal(0, smtp.Attempts);
    }

    [Fact]
    public async Task OversizedFormAndUserAgent_AreRejectedBeforeSmtp()
    {
        var smtp = new ContactFakeTransport();
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        var fields = await Form(client);
        fields["Message"] = new string('x', 40000);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields))).StatusCode);
        fields["Message"] = "Nội dung hợp lệ";
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", new string('x', 2049));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields))).StatusCode);
        client.DefaultRequestHeaders.Remove("User-Agent");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Large-Header", new string('x', 16385));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(0, smtp.Attempts);
    }

    [Fact]
    public async Task HttpThrottle_StopsFourthOperation_AndInvalidNonceCannotSend()
    {
        var smtp = new ContactFakeTransport();
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        client.DefaultRequestHeaders.Add("X-Contact-Test-Remote", "198.51.100.1");
        var invalid = await Form(client);
        invalid["ContactNonce"] = "tampered";
        var rejected = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(invalid));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(0, smtp.Attempts);
        for (var i = 0; i < 4; i++)
        {
            var fields = await Form(client);
            var response = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields));
            Assert.Equal(i < 3 ? HttpStatusCode.Found : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        Assert.Equal(3, smtp.Attempts);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.2");
        var spoofed = await Form(client);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(spoofed))).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Contact-Test-Remote");
        client.DefaultRequestHeaders.Add("X-Contact-Test-Remote", "198.51.100.2");
        var independent = await Form(client);
        Assert.Equal(HttpStatusCode.Found, (await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(independent))).StatusCode);
        Assert.Equal(4, smtp.Attempts);
    }

    [Fact]
    public async Task ConcurrentHttpPost_RejectsReplayWhileFirstDispatchIsPending()
    {
        var smtp = new ContactFakeTransport { Stall = true };
        var settings = Settings();
        settings["SmtpSettings:TimeoutSeconds"] = "10";
        await using var factory = CreateFactory(smtp, settings);
        await factory.InitializeAsync();
        using var client = Client(factory);
        var fields = await Form(client);
        var first = client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields));
        await smtp.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fields["Message"] = "Altered payload while sending";
        var second = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        smtp.Release.TrySetResult();
        Assert.Equal(HttpStatusCode.Found, (await first).StatusCode);
        Assert.Equal(1, smtp.Attempts);
    }

    [Fact]
    public async Task ContactPage_RendersClaymorphismComponents_AndAccessibilitySemantics()
    {
        var smtp = new ContactFakeTransport();
        await using var factory = CreateFactory(smtp);
        await factory.InitializeAsync();
        using var client = Client(factory);
        var response = await client.GetAsync("/Home/Contact");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Directory.CreateDirectory(".tmp/contact-refresh");
        await File.WriteAllTextAsync(".tmp/contact-refresh/contact-rendered.html", html);
        var page = await new HtmlParser().ParseDocumentAsync(html);

        // Claymorphism theme & container
        var pageContainer = page.QuerySelector(".contact-page");
        Assert.NotNull(pageContainer);
        Assert.True(pageContainer.ClassList.Contains("tct-theme"));
        Assert.True(pageContainer.ClassList.Contains("tct-page"));

        // Single h1 via Hero Banner
        var h1s = page.QuerySelectorAll("h1");
        Assert.Single(h1s);
        Assert.Contains("Liên hệ", h1s[0].TextContent);

        // Banner rendered (without breadcrumbs as requested by owner)
        var banner = page.QuerySelector(".contact-banner");
        Assert.NotNull(banner);
        Assert.Null(page.QuerySelector(".tct-breadcrumb"));

        // Contact grid and cards
        Assert.NotNull(page.QuerySelector(".contact-grid"));
        Assert.NotNull(page.QuerySelector(".info-card.tct-clay-surface"));
        Assert.NotNull(page.QuerySelector(".form-card.tct-clay-surface"));
        Assert.Null(page.QuerySelector(".info-quick-actions"));
        Assert.DoesNotContain("24/7", pageContainer!.TextContent);

        // Form attributes & accessibility
        var form = page.QuerySelector("#contactForm");
        Assert.NotNull(form);
        Assert.Equal("post", form.GetAttribute("method")?.ToLowerInvariant());

        var nameInput = page.QuerySelector("#Name");
        Assert.NotNull(nameInput);
        Assert.Equal("100", nameInput.GetAttribute("maxlength"));
        Assert.Equal("true", nameInput.GetAttribute("aria-required"));

        var emailInput = page.QuerySelector("#Email");
        Assert.NotNull(emailInput);
        Assert.Equal("254", emailInput.GetAttribute("maxlength"));
        Assert.Equal("email", emailInput.GetAttribute("type")?.ToLowerInvariant());
        Assert.Equal("true", emailInput.GetAttribute("aria-required"));

        var subjectSelect = page.QuerySelector("#Subject");
        Assert.NotNull(subjectSelect);
        Assert.Equal("true", subjectSelect.GetAttribute("aria-required"));

        var messageArea = page.QuerySelector("#Message");
        Assert.NotNull(messageArea);
        Assert.Equal("2000", messageArea.GetAttribute("maxlength"));
        Assert.Equal("true", messageArea.GetAttribute("aria-required"));

        // Character counter
        Assert.NotNull(page.QuerySelector("#messageCounter"));
        Assert.NotNull(page.QuerySelector("#messageCurrentCount"));

        // Submit button with loading elements
        var submitBtn = page.QuerySelector("#contactSubmitBtn");
        Assert.NotNull(submitBtn);
        Assert.NotNull(page.QuerySelector("#contactSubmitBtn .btn-send-default"));
        Assert.NotNull(page.QuerySelector("#contactSubmitBtn .btn-send-loading"));

        // Bootstrap Accordion for FAQs
        var accordion = page.QuerySelector("#contactFaqAccordion.accordion");
        Assert.NotNull(accordion);
        var accordionButtons = page.QuerySelectorAll("#contactFaqAccordion .accordion-button");
        Assert.Equal(4, accordionButtons.Length);
        Assert.DoesNotContain("TCT có miễn phí", html);
    }

    internal static HttpClient Client(TestWebApplicationFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
    });

    internal static async Task<Dictionary<string, string>> Form(HttpClient client, string subject = "general")
    {
        var response = await client.GetAsync("/Home/Contact?subject=" + subject);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync());
        var fields = page.QuerySelectorAll("#contactForm input[type=hidden]")
            .ToDictionary(e => e.GetAttribute("name")!, e => e.GetAttribute("value") ?? "");
        fields["Name"] = "Nguyễn An";
        fields["Email"] = "visitor@example.test";
        fields["Subject"] = subject;
        fields["Message"] = "Cần hỗ trợ.\r\nXin cảm ơn!";
        return fields;
    }

    internal static Dictionary<string, string?> Settings() => new()
    {
        ["SmtpSettings:Host"] = "smtp.example.test", ["SmtpSettings:Port"] = "587",
        ["SmtpSettings:SenderEmail"] = "sender@example.test", ["SmtpSettings:SenderName"] = "TCT Test",
        ["SmtpSettings:Password"] = "synthetic-test-only", ["SmtpSettings:SupportEmail"] = "support@example.test",
        ["SmtpSettings:TimeoutSeconds"] = "1"
    };

    internal static TestWebApplicationFactory CreateFactory(ContactFakeTransport smtp, Dictionary<string, string?>? settings = null) => new(services =>
    {
        services.AddSingleton<IStartupFilter, TestRemoteAddressFilter>();
        services.RemoveAll<IAppEmailSender>();
        services.AddSingleton<IAppEmailSender>(sp => new SmtpAppEmailSender(
            new ConfigurationBuilder().AddInMemoryCollection(settings ?? Settings()).Build(), smtp,
            NullLogger<SmtpAppEmailSender>.Instance,
            sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            sp.GetRequiredService<IDataProtectionProvider>()));
    });

    // TestServer has no network peers. Populate Connection, never a production forwarded-header path.
    private sealed class TestRemoteAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                if (IPAddress.TryParse(context.Request.Headers["X-Contact-Test-Remote"].ToString(), out var address))
                    context.Connection.RemoteIpAddress = address;
                await continuation();
            });
            next(app);
        };
    }
}

internal sealed class ContactFakeTransport : ISmtpMailTransport
{
    private int _attempts;
    public int Attempts => _attempts;
    public Exception? Failure { get; set; }
    public string? To { get; private set; }
    public string? From { get; private set; }
    public string? ReplyTo { get; private set; }
    public string? Body { get; private set; }
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Stall { get; init; }

    public async Task SendAsync(string host, int port, string username, string password, MailMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _attempts);
        To = Assert.Single(message.To).Address;
        From = message.From!.Address;
        ReplyTo = message.ReplyToList.SingleOrDefault()?.Address;
        Assert.Empty(message.CC);
        Assert.Empty(message.Bcc);
        Body = message.Body;
        Entered.TrySetResult();
        if (Stall) await Release.Task.WaitAsync(cancellationToken);
        if (Failure is not null) throw Failure;
    }
}
