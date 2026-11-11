using System.Net;
using System.Net.Mail;
using TCTEnglish.Services.Emails;

namespace TCTVocabulary.Services
{
    public class SmtpAppEmailSender : IAppEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ISmtpMailTransport _transport;
        private readonly ILogger<SmtpAppEmailSender> _logger;
        private readonly IHttpContextAccessor? _requestContext;

        public SmtpAppEmailSender(
            IConfiguration configuration,
            ISmtpMailTransport transport,
            ILogger<SmtpAppEmailSender> logger,
            IHttpContextAccessor? requestContext = null)
        {
            _configuration = configuration;
            _transport = transport;
            _logger = logger;
            _requestContext = requestContext;
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
            var fallbackRecipient = _configuration["SmtpSettings:SenderEmail"];
            var supportRecipient = _configuration["SmtpSettings:SupportEmail"];
            var toEmail = string.IsNullOrWhiteSpace(supportRecipient) ? fallbackRecipient : supportRecipient;

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogError("[Email Error] Missing support recipient email for contact form.");
                return false;
            }

            var mailSubject = $"[Liên hệ] {subject}";
            var body = TctEmailRenderer.RenderSupportContact(senderName, senderEmail, subject, message, ipAddress, userAgent);

            return await SendEmailAsync(toEmail, mailSubject, body, senderEmail);
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

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? replyToEmail = null, CancellationToken cancellationToken = default, bool surfaceTimeout = false)
        {
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
            catch (Exception ex)
            {
                _logger.LogWarning("[Email Error] Delivery failed ({Category}).", ex.GetType().Name);
                return false;
            }
        }
    }
}
