namespace TCTVocabulary.Services
{
    public interface IAppEmailSender
    {
        Task SendBlockedNotificationAsync(string toEmail, string reason, DateTime? lockExpiry);
        Task SendUnlockedNotificationAsync(string toEmail, bool isAutoUnlock);
        async Task<bool> TrySendUnlockedNotificationAsync(string toEmail, bool isAutoUnlock, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await SendUnlockedNotificationAsync(toEmail, isAutoUnlock);
            return true;
        }
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
