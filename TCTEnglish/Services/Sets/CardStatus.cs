using TCTVocabulary.Models;

namespace TCTEnglish.Services.Sets;

/// <summary>Per-viewer word status (priority: suspended > hard > learned > learning > new).</summary>
public static class CardStatus
{
    public const string Suspended = "suspended";
    public const string Hard = "hard";
    public const string Learned = "learned";
    public const string Learning = "learning";
    public const string New = "new";

    public const int HardWrongThreshold = 3;

    public static readonly IReadOnlyList<string> Keys = new[] { New, Learning, Learned, Hard, Suspended };

    public static bool IsValid(string? key) => key != null && Keys.Contains(key);

    public static string LabelOf(string key) => key switch
    {
        Suspended => "Đang tạm ẩn",
        Hard => "Hay quên",
        Learned => "Đã thuộc",
        Learning => "Đang học",
        _ => "Mới",
    };

    /// <summary>In-memory classification of one progress row (null = never studied).</summary>
    public static string Classify(bool isSuspended, int? wrongCount, string? status, bool hasProgress)
    {
        if (!hasProgress)
        {
            return New;
        }

        if (isSuspended)
        {
            return Suspended;
        }

        if ((wrongCount ?? 0) >= HardWrongThreshold)
        {
            return Hard;
        }

        if (status == "Mastered")
        {
            return Learned;
        }

        if (status == "Learning" || status == "Reviewing")
        {
            return Learning;
        }

        return New;
    }

    public static string Classify(LearningProgress? progress)
        => progress == null ? New : Classify(progress.IsSuspended, progress.WrongCount, progress.Status, true);
}
