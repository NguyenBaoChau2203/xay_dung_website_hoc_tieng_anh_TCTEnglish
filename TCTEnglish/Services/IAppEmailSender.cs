namespace TCTVocabulary.Services
{
    public enum EmailDispatchResult
    {
        NotConfigured = 0,
        Sent = 1,
        Failed = 2,
        Unknown = 3
    }

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

        Task<EmailDispatchResult> TrySendTrackedHtmlAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EmailDispatchResult.NotConfigured);
    }
}
