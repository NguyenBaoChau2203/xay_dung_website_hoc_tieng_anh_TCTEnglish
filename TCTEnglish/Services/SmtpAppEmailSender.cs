using System.Net;
using System.Net.Mail;

namespace TCTVocabulary.Services
{
    public class SmtpAppEmailSender : IAppEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpAppEmailSender> _logger;

        public SmtpAppEmailSender(IConfiguration configuration, ILogger<SmtpAppEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendBlockedNotificationAsync(string toEmail, string reason, DateTime? lockExpiry)
        {
            var expiryText = lockExpiry.HasValue && lockExpiry.Value < DateTime.MaxValue
                ? lockExpiry.Value.ToString("dd/MM/yyyy HH:mm:ss (UTC)")
                : "Vĩnh viễn";

            var subject = "Tài khoản của bạn đã bị khóa - TCT English";
            var body = $@"
                <div style='font-family: Inter, Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 40px; background-color: #f9fafe;'>
                    <div style='background-color: #ffffff; padding: 40px; border-radius: 16px; box-shadow: 0 4px 12px rgba(0,0,0,0.05);'>
                        <h2 style='color: #dc3545; font-size: 24px; margin-bottom: 16px;'>⚠️ Tài khoản đã bị khóa</h2>
                        <p style='color: #586380; font-size: 16px; line-height: 1.6;'>
                            Tài khoản của bạn trên <strong>TCT English</strong> đã bị khóa bởi quản trị viên.
                        </p>
                        <p style='color: #586380; font-size: 16px; line-height: 1.6;'>
                            <strong>Lý do:</strong> {reason}
                        </p>
                        <p style='color: #586380; font-size: 16px; line-height: 1.6;'>
                            <strong>Thời gian mở khóa:</strong> {expiryText}
                        </p>
                        <p style='color: #939bb4; font-size: 13px; margin-top: 24px; line-height: 1.5;'>
                            Nếu bạn cho rằng đây là nhầm lẫn, vui lòng liên hệ quản trị viên qua email hỗ trợ.
                        </p>
                    </div>
                </div>";

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendUnlockedNotificationAsync(string toEmail, bool isAutoUnlock)
        {
            var unlockType = isAutoUnlock ? "tự động (hết thời hạn khóa)" : "thủ công bởi quản trị viên";
            var subject = "Tài khoản của bạn đã được mở khóa - TCT English";
            var body = $@"
                <div style='font-family: Inter, Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 40px; background-color: #f9fafe;'>
                    <div style='background-color: #ffffff; padding: 40px; border-radius: 16px; box-shadow: 0 4px 12px rgba(0,0,0,0.05);'>
                        <h2 style='color: #198754; font-size: 24px; margin-bottom: 16px;'>✅ Tài khoản đã được mở khóa</h2>
                        <p style='color: #586380; font-size: 16px; line-height: 1.6;'>
                            Tài khoản của bạn trên <strong>TCT English</strong> đã được mở khóa {unlockType}.
                        </p>
                        <p style='color: #586380; font-size: 16px; line-height: 1.6;'>
                            Bạn có thể đăng nhập lại và tiếp tục sử dụng dịch vụ.
                        </p>
                    </div>
                </div>";

            await SendEmailAsync(toEmail, subject, body);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var smtpHost = _configuration["SmtpSettings:Host"];
                var smtpPort = int.Parse(_configuration["SmtpSettings:Port"] ?? "587");
                var senderEmail = _configuration["SmtpSettings:SenderEmail"];
                var senderName = _configuration["SmtpSettings:SenderName"] ?? "TCT English";
                var smtpPassword = _configuration["SmtpSettings:Password"];

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail!, senderName),
                    Subject = subject,
                    IsBodyHtml = true,
                    Body = htmlBody
                };
                mailMessage.To.Add(toEmail);

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(senderEmail, smtpPassword),
                    EnableSsl = true
                };
                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("[Email] Sent '{Subject}' to {Email}", subject, toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Email Error] Failed to send '{Subject}' to {Email}", subject, toEmail);
            }
        }
    }
}
