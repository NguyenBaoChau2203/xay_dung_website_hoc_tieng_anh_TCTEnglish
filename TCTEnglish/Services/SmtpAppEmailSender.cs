using System.Net;
using System.Net.Mail;
using TCTEnglish.Services.Emails;
using Microsoft.AspNetCore.DataProtection;
using TCTEnglish.Services.Contact;
using TCTVocabulary.ViewModels;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace TCTVocabulary.Services
{
    public class SmtpAppEmailSender : IAppEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ISmtpMailTransport _transport;
        private readonly ILogger<SmtpAppEmailSender> _logger;
        private readonly IHttpContextAccessor? _requestContext;
        private readonly ContactOperationGate? _contactGate;

        public SmtpAppEmailSender(
            IConfiguration configuration,
            ISmtpMailTransport transport,
            ILogger<SmtpAppEmailSender> logger,
            IHttpContextAccessor? requestContext = null,
            IDataProtectionProvider? dataProtectionProvider = null)
        {
            _configuration = configuration;
            _transport = transport;
            _logger = logger;
            _requestContext = requestContext;
            _contactGate = dataProtectionProvider is null ? null : new ContactOperationGate(dataProtectionProvider);
        }

        public async Task SendBlockedNotificationAsync(string toEmail, string reason, DateTime? lockExpiry)
        {
            var subject = "Tài khoản của bạn đã bị khóa - TCT English";
            var body = TctEmailRenderer.RenderAccountBlocked(reason, lockExpiry);

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendUnlockedNotificationAsync(string toEmail, bool isAutoUnlock)
        {
            await TrySendUnlockedNotificationAsync(toEmail, isAutoUnlock, CancellationToken.None);
        }

        public Task<bool> TrySendUnlockedNotificationAsync(string toEmail, bool isAutoUnlock, CancellationToken ct)
        {
            var subject = "Tài khoản của bạn đã được mở khóa - TCT English";
            var body = TctEmailRenderer.RenderAccountUnlocked(isAutoUnlock);

            return SendEmailAsync(toEmail, subject, body, cancellationToken: ct);
        }

        // REFACTOR: Moved from AccountController.ForgotPassword — email logic belongs in the service layer
        public async Task SendPasswordResetAsync(string toEmail, string resetLink)
        {
            var subject = "Đặt lại mật khẩu - TCT English";
            var body = TctEmailRenderer.RenderPasswordReset(resetLink, 15);

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task<bool> SendContactMessageAsync(
            string senderName,
            string senderEmail,
            string subject,
            string message,
            string? ipAddress,
            string? userAgent)
        {
            if (ContactInput.HasLineBreak(senderName) || ContactInput.HasLineBreak(senderEmail) || ContactInput.HasLineBreak(subject))
                return false;
            return await SendContactCoreAsync(senderName.Trim(), senderEmail.Trim(), subject.Trim(), message.Trim(),
                ipAddress, userAgent, CancellationToken.None) == ContactSubmissionStatus.Accepted;
        }

        public string IssueContactNonce(string clientPartition) => _contactGate?.Issue(clientPartition) ?? string.Empty;

        public async Task<ContactSubmissionStatus> SubmitContactAsync(ContactFormViewModel model, string clientPartition,
            string? userAgent, CancellationToken cancellationToken = default)
        {
            if (!Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true))
                return ContactSubmissionStatus.InvalidInput;
            if (_contactGate is null) return ContactSubmissionStatus.ConfigurationUnavailable;
            var blocked = _contactGate.TryBegin(model.ContactNonce, clientPartition, out var operationId);
            if (blocked.HasValue) return blocked.Value;
            var started = Stopwatch.GetTimestamp();
            ContactSubmissionStatus result;
            try
            {
                result = await SendContactCoreAsync(model.Name, model.Email, model.SubjectDisplayName, model.Message,
                    clientPartition, userAgent, cancellationToken);
            }
            finally { _contactGate.Complete(operationId); }
            _logger.LogInformation("Contact {OperationId}: {Category} ({ElapsedMs}ms)", operationId, result,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return result;
        }

        private async Task<ContactSubmissionStatus> SendContactCoreAsync(string senderName, string senderEmail,
            string subject, string message, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(senderName) || senderName.Length > 100 || ContactInput.HasLineBreak(senderName)
                || !ContactInput.IsMailbox(senderEmail) || string.IsNullOrWhiteSpace(subject) || subject.Length > 50
                || ContactInput.HasLineBreak(subject) || string.IsNullOrWhiteSpace(message) || message.Length > 2000)
                return ContactSubmissionStatus.InvalidInput;
            var fallbackRecipient = _configuration["SmtpSettings:SenderEmail"];
            var supportRecipient = _configuration["SmtpSettings:SupportEmail"];
            var toEmail = string.IsNullOrWhiteSpace(supportRecipient) ? fallbackRecipient : supportRecipient;
            var host = _configuration["SmtpSettings:Host"];
            var port = _configuration["SmtpSettings:Port"];
            var senderDisplayName = _configuration["SmtpSettings:SenderName"] ?? "TCT English";
            if (!ContactInput.IsMailbox(toEmail) || !ContactInput.IsMailbox(fallbackRecipient)
                || string.IsNullOrWhiteSpace(host) || host.Length > 253 || host.Any(char.IsWhiteSpace)
                || Uri.CheckHostName(host) == UriHostNameType.Unknown
                || string.IsNullOrWhiteSpace(_configuration["SmtpSettings:Password"])
                || senderDisplayName.Length > 200 || ContactInput.HasLineBreak(senderDisplayName)
                || (!string.IsNullOrWhiteSpace(port) && (!int.TryParse(port, out var number) || number is < 1 or > 65535)))
            {
                _logger.LogWarning("Contact configuration unavailable (recipientPresent={RecipientPresent}, senderPresent={SenderPresent}, hostPresent={HostPresent})",
                    !string.IsNullOrWhiteSpace(toEmail), !string.IsNullOrWhiteSpace(fallbackRecipient), !string.IsNullOrWhiteSpace(host));
                return ContactSubmissionStatus.ConfigurationUnavailable;
            }
            if (cancellationToken.IsCancellationRequested || (_requestContext?.HttpContext?.RequestAborted.IsCancellationRequested ?? false))
                return ContactSubmissionStatus.Failed; // No transport invocation has occurred.
            var mailSubject = $"[Liên hệ] {subject}";
            var body = TctEmailRenderer.RenderSupportContact(senderName, senderEmail, subject, message,
                ContactInput.BoundDiagnostic(ipAddress, 64), ContactInput.BoundDiagnostic(userAgent, 512));
            try
            {
                var sent = await SendEmailAsync(toEmail!, mailSubject, body, senderEmail, cancellationToken,
                    surfaceTimeout: true, conservativeContact: true);
                return sent ? ContactSubmissionStatus.Accepted : ContactSubmissionStatus.Failed;
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
            {
                return ContactSubmissionStatus.Unknown;
            }
        }

        public async Task<EmailDispatchResult> TrySendTrackedHtmlAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            var smtpHost = _configuration["SmtpSettings:Host"];
            var senderEmail = _configuration["SmtpSettings:SenderEmail"];
            var smtpPassword = _configuration["SmtpSettings:Password"];
            if (string.IsNullOrWhiteSpace(smtpHost)
                || string.IsNullOrWhiteSpace(senderEmail)
                || string.IsNullOrWhiteSpace(smtpPassword))
            {
                _logger.LogError("[Email Error] SMTP settings are incomplete.");
                return EmailDispatchResult.NotConfigured;
            }

            try
            {
                var sent = await SendEmailAsync(toEmail, subject, htmlBody, cancellationToken: cancellationToken, surfaceTimeout: true);
                return sent ? EmailDispatchResult.Sent : EmailDispatchResult.Failed;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return EmailDispatchResult.Unknown;
            }
            catch (TimeoutException)
            {
                return EmailDispatchResult.Unknown;
            }
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? replyToEmail = null, CancellationToken cancellationToken = default, bool surfaceTimeout = false, bool conservativeContact = false)
        {
            var transportStarted = false;
            try
            {
                var smtpHost = _configuration["SmtpSettings:Host"];
                var senderEmail = _configuration["SmtpSettings:SenderEmail"];
                var senderName = _configuration["SmtpSettings:SenderName"] ?? "TCT English";
                var smtpPassword = _configuration["SmtpSettings:Password"];

                if (string.IsNullOrWhiteSpace(smtpHost)
                    || string.IsNullOrWhiteSpace(senderEmail)
                    || string.IsNullOrWhiteSpace(smtpPassword))
                {
                    _logger.LogError("[Email Error] SMTP settings are incomplete.");
                    return false;
                }

                var smtpPortRaw = _configuration["SmtpSettings:Port"];
                var smtpPort = int.TryParse(smtpPortRaw, out var parsedPort) ? parsedPort : 587;

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    IsBodyHtml = true,
                    Body = htmlBody
                };
                mailMessage.To.Add(toEmail);

                if (!string.IsNullOrWhiteSpace(replyToEmail))
                {
                    try
                    {
                        mailMessage.ReplyToList.Add(new MailAddress(replyToEmail.Trim()));
                    }
                    catch (FormatException)
                    {
                        _logger.LogWarning("[Email Warning] Invalid reply-to address.");
                    }
                }

                // Bound SMTP wait (.NET 9 SendMailAsync overload with CancellationToken).
                var timeoutSecondsRaw = _configuration["SmtpSettings:TimeoutSeconds"];
                var timeoutSeconds = int.TryParse(timeoutSecondsRaw, out var parsedTimeout) && parsedTimeout > 0
                    ? parsedTimeout
                    : 30;
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                    _requestContext?.HttpContext?.RequestAborted ?? CancellationToken.None);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 60)));
                transportStarted = true;
                await _transport.SendAsync(
                    smtpHost,
                    smtpPort,
                    senderEmail,
                    smtpPassword,
                    mailMessage,
                    timeoutCts.Token);

                _logger.LogInformation("[Email] Sent '{Subject}' to recipient (timeoutBound={TimeoutSeconds}s)", subject, timeoutSeconds);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[Email Error] Delivery timed out; outcome unknown.");
                if (surfaceTimeout)
                {
                    throw new TimeoutException("SMTP delivery timed out.");
                }

                return false;
            }
            catch (Exception ex) when (surfaceTimeout && (ex is IOException
                || ex is System.Net.Mail.SmtpException { StatusCode: System.Net.Mail.SmtpStatusCode.GeneralFailure }))
            {
                _logger.LogWarning("[Email Error] Transport lost its receipt; delivery outcome is unknown.");
                throw new TimeoutException("SMTP delivery outcome is unknown.", ex);
            }
            catch (Exception ex) when (conservativeContact && transportStarted
                && ex is not SmtpException { StatusCode: >= (SmtpStatusCode)400 and <= (SmtpStatusCode)599 })
            {
                _logger.LogWarning("Contact transport ended without a definite receipt; outcome unknown.");
                throw new TimeoutException("SMTP delivery outcome is unknown.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Email Error] Delivery failed ({Category}).", ex.GetType().Name);
                return false;
            }
        }
    }
}
