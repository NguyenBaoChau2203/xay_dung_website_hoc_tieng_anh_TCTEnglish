namespace TCTEnglish.Services.Admin;

/// <summary>Vietnamese words for stored subscription statuses, shown on the admin payment pages. Read only: no state logic.</summary>
public static class AdminBillingLabels
{
    public static string SubscriptionStatus(string? status)
    {
        return (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "active" => "Đang hoạt động",
            "expired" => "Đã hết hạn",
            "cancelled" => "Đã hủy",
            "revoked" => "Đã thu hồi",
            "none" or "" => "Chưa có gói",
            var other => other,
        };
    }
}
