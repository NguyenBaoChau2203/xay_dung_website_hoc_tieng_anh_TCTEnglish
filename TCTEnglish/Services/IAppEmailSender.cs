namespace TCTVocabulary.Services
{
    public interface IAppEmailSender
    {
        Task SendBlockedNotificationAsync(string toEmail, string reason, DateTime? lockExpiry);
        Task SendUnlockedNotificationAsync(string toEmail, bool isAutoUnlock);
        Task SendPasswordResetAsync(string toEmail, string resetLink);
        Task<bool> SendContactMessageAsync(
            string senderName,
            string senderEmail,
            string subject,
            string message,
            string? ipAddress,
            string? userAgent);
    }
}
