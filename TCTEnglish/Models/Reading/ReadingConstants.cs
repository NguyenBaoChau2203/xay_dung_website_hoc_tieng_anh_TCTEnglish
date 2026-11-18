namespace TCTEnglish.Models.Reading;

public static class ReadingStatuses
{
    public const string Draft = "Draft";
    public const string ValidationFailed = "ValidationFailed";
    public const string PendingReview = "PendingReview";
    public const string RevisionRequired = "RevisionRequired";
    public const string Rejected = "Rejected";
    public const string Approved = "Approved";
    public const string Published = "Published";
    public const string Archived = "Archived";

    public static readonly string[] All =
    [
        Draft, ValidationFailed, PendingReview, RevisionRequired,
        Rejected, Approved, Published, Archived
    ];
}

public static class ReadingQuestionTypes
{
    public const string MultipleChoice = "MultipleChoice";
    public const string TrueFalse = "TrueFalse";
    public const string GapFill = "GapFill";
    public const string Matching = "Matching";
    public const string ShortAnswer = "ShortAnswer";

    public static readonly string[] All =
    [
        MultipleChoice, TrueFalse, GapFill, Matching, ShortAnswer
    ];

    public static bool IsValid(string? type) =>
        !string.IsNullOrWhiteSpace(type) &&
        All.Contains(type.Trim(), StringComparer.Ordinal);
}

public static class CefrLevels
{
    public static readonly string[] All = ["A1", "A2", "B1", "B2", "C1", "C2"];

    public static string Normalize(string? level, string fallback = "A1")
    {
        var normalized = level?.Trim().ToUpperInvariant();
        return All.Contains(normalized) ? normalized! : fallback;
    }

    public static bool IsValid(string? level) =>
        !string.IsNullOrWhiteSpace(level) &&
        All.Contains(level.Trim().ToUpperInvariant());

    public static int Ordinal(string? level)
    {
        var normalized = Normalize(level);
        return Array.IndexOf(All, normalized);
    }

    /// <summary>Student may access a passage when student level is at least the passage level.</summary>
    public static bool CanAccess(string? studentLevel, string? passageLevel) =>
        Ordinal(studentLevel) >= Ordinal(passageLevel);
}
