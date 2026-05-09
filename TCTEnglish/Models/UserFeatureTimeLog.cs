namespace TCTVocabulary.Models;

/// <summary>
/// Ghi lại thời gian user học ở mỗi tính năng.
/// Một session = một lần vào trang và rời trang.
/// </summary>
public class UserFeatureTimeLog
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>
    /// Tên tính năng: Flashcard | Quiz | Speaking | Reading | Listening | Writing | Grammar
    /// </summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>Thời gian học (giây). Tối đa 3600s (1 giờ) để tránh data rác.</summary>
    public int DurationSeconds { get; set; }

    /// <summary>Ngày ghi nhận (local date — dùng BusinessDateHelper).</summary>
    public DateTime LoggedDate { get; set; }

    public virtual User User { get; set; } = null!;
}
