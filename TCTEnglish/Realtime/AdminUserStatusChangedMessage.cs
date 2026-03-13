using TCTVocabulary.Models;

namespace TCTVocabulary.Realtime
{
    public sealed class AdminUserStatusChangedMessage
    {
        public int UserId { get; init; }
        public int PreviousStatus { get; init; }
        public int Status { get; init; }
        public string StatusLabel { get; init; } = string.Empty;
        public string StatusBadgeClass { get; init; } = string.Empty;
        public string StatusIconClass { get; init; } = string.Empty;
        public bool CanUnlock { get; init; }

        public static AdminUserStatusChangedMessage Create(
            int userId,
            UserStatus previousStatus,
            UserStatus currentStatus)
        {
            var (badgeClass, iconClass, label) = GetStatusPresentation(currentStatus);

            return new AdminUserStatusChangedMessage
            {
                UserId = userId,
                PreviousStatus = (int)previousStatus,
                Status = (int)currentStatus,
                StatusLabel = label,
                StatusBadgeClass = badgeClass,
                StatusIconClass = iconClass,
                CanUnlock = currentStatus == UserStatus.Blocked
            };
        }

        private static (string BadgeClass, string IconClass, string Label) GetStatusPresentation(UserStatus status)
        {
            return status switch
            {
                UserStatus.Online => ("badge bg-success", "bi-check-circle-fill", "Online"),
                UserStatus.Blocked => ("badge bg-danger", "bi-x-circle-fill", "Blocked"),
                _ => ("badge bg-secondary", "bi-moon-fill", "Offline")
            };
        }
    }
}
