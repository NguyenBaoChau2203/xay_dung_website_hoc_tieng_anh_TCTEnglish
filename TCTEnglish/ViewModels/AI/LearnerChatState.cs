namespace TCTEnglish.ViewModels.AI;

/// <summary>
/// Learner-facing labels for an accepted chat turn. These strings are encoded
/// as text. They are not model HTML and they do not include source keys.
/// </summary>
public static class LearnerChatState
{
    public const string Answered = "Đã trả lời từ nguồn được phép xem";
    public const string NeedsClarification = "Cần làm rõ thêm";
    public const string InsufficientEvidence = "Chưa đủ căn cứ";
    public const string OutOfScope = "Ngoài phạm vi trợ lý";
    public const string ProviderUnavailable = "Trợ lý tạm thời không trả lời được";
    public const string AccessDenied = "Không còn quyền xem nguồn này";
    public const string QuotaExceeded = "Đã hết lượt hỏi hôm nay";
    public const string Cancelled = "Đã dừng câu trả lời";
    public const string SourceUnavailable = "Tạm thời không xem được nguồn";
    public const string Unverified = "Chưa xác minh được nguồn";
    public const string Changed = "Nguồn đã thay đổi sau khi trả lời";
    public const string Snapshot = "Số liệu tại thời điểm trả lời";
    public const string MissingTitle = "Nguồn đã lưu";

    public static string? Label(string? outcome, string? status)
    {
        if (string.Equals(status, "unavailable", StringComparison.Ordinal))
        {
            return SourceUnavailable;
        }

        if (string.Equals(status, "unverified", StringComparison.Ordinal))
        {
            return Unverified;
        }

        if (string.Equals(status, "changed", StringComparison.Ordinal))
        {
            return Changed;
        }

        if (string.Equals(status, "snapshot", StringComparison.Ordinal))
        {
            return Snapshot;
        }

        return outcome switch
        {
            "answered" => Answered,
            "needs_clarification" => NeedsClarification,
            "insufficient_evidence" => InsufficientEvidence,
            "out_of_scope" => OutOfScope,
            "provider_unavailable" => ProviderUnavailable,
            "access_denied" => AccessDenied,
            "quota_exceeded" => QuotaExceeded,
            "cancelled" => Cancelled,
            _ => null
        };
    }

    public static bool ShowFreshness(string? freshness)
        => freshness is "changed" or "snapshot";

    public static string FreshnessLabel(string? freshness)
        => freshness switch
        {
            "changed" => Changed,
            "snapshot" => Snapshot,
            _ => string.Empty
        };
}
