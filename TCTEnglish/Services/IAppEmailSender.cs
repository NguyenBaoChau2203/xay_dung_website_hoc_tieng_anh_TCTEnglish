namespace TCTVocabulary.Services
{
    public interface IAppEmailSender
    {
        Task SendBlockedNotificationAsync(string toEmail, string reason, DateTime? lockExpiry);
        Task SendUnlockedNotificationAsync(string toEmail, bool isAutoUnlock);
        // REFACTOR: Moved password reset email from AccountController into the email service
        Task SendPasswordResetAsync(string toEmail, string resetLink);
    }
}
