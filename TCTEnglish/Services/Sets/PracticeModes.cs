using TCTEnglish.ViewModels.Sets;

namespace TCTEnglish.Services.Sets;

/// <summary>Practice mode registry (mirrors MODE_INFO / countsFor in wwwroot/js/practice-core.js) and query normalization.</summary>
public static class PracticeModes
{
    public const string Learn = "learn";
    public const string Test = "test";
    public const string Match = "match";
    public const string Flashcard = "flashcard";
    public const string Quiz = "quiz";
    public const string Typing = "typing";
    public const string Listening = "listening";
    public const string Cloze = "cloze";

    public const string SourceAll = "all";
    public const string SourceDue = "due";
    public const string SourceHard = "hard";
    public const string SourceRecent = "recent";

    public const string ScopeMine = "mine";

    public const int MaxWords = 50;
    public const int FlashcardWholeSetCap = 500;
    public const int PoolSize = 200;

    public sealed record ModeInfo(string Key, string Label, string Hint, string Hue, string Icon, string TimeFeature, bool IsStudyGame);

    private static readonly ModeInfo[] Modes =
    {
        new(Learn, "Học", "Học theo vòng tới khi thuộc hết bộ từ.", "success", "fa-solid fa-graduation-cap", "Quiz", true),
        new(Test, "Kiểm tra", "Làm bài trộn nhiều dạng câu, chấm điểm cuối bài.", "primary", "fa-solid fa-clipboard-check", "Quiz", true),
        new(Match, "Ghép thẻ", "Ghép từ với nghĩa nhanh nhất có thể.", "warning", "fa-solid fa-shuffle", "Quiz", true),
        new(Flashcard, "Lật thẻ", "Lật qua lật lại, tự đánh dấu thuộc hay chưa.", "info", "fa-solid fa-clone", "Flashcard", true),
        new(Quiz, "Trắc nghiệm", "Chọn nghĩa đúng của từ, hoặc từ đúng với nghĩa.", "primary", "fa-solid fa-list-check", "Quiz", false),
        new(Typing, "Gõ từ", "Nhìn nghĩa, gõ lại từ.", "accent", "fa-solid fa-keyboard", "Quiz", false),
        new(Listening, "Nghe", "Nghe đọc rồi chọn từ đúng.", "info", "fa-solid fa-headphones", "Quiz", false),
        new(Cloze, "Điền từ", "Chọn từ còn thiếu trong câu ví dụ.", "danger", "fa-solid fa-pen-to-square", "Quiz", false),
    };

    public static IReadOnlyList<ModeInfo> All => Modes;

    public static IReadOnlyList<ModeInfo> StudyGames => Modes.Where(m => m.IsStudyGame).ToList();

    public static IReadOnlyList<ModeInfo> Skills => Modes.Where(m => !m.IsStudyGame).ToList();

    /// <summary>Canonical lowercase key, or null for an unknown mode.</summary>
    public static ModeInfo? Find(string? mode)
        => string.IsNullOrWhiteSpace(mode) ? null : Modes.FirstOrDefault(m => string.Equals(m.Key, mode.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Allowed values of "n" per mode (0 = whole set, flashcard only).</summary>
    public static IReadOnlyList<int> CountsFor(string mode) => mode switch
    {
        Flashcard => new[] { 20, 50, 0 },
        Match => new[] { 6, 12, 18 },
        Learn => new[] { 10, 20, 50 },
        _ => new[] { 10, 20, 30 },
    };

    public static readonly IReadOnlyList<string> TestTypes = new[] { "choice", "tf", "typing", "cloze" };
    public static readonly IReadOnlyList<string> DefaultTestTypes = new[] { "choice", "tf", "typing" };
    public static readonly IReadOnlyList<string> TestAsk = new[] { "both", "term", "meaning" };
    public static readonly IReadOnlyList<string> Sources = new[] { SourceAll, SourceDue, SourceHard, SourceRecent };

    /// <summary>Validates every field; unknown values fall back to defaults and never raise an error.</summary>
    public static PracticeQuery Normalize(string mode, PracticeQuery? raw)
    {
        raw ??= new PracticeQuery();
        var counts = CountsFor(mode);
        var q = new PracticeQuery
        {
            Set = raw.Set is > 0 ? raw.Set : null,
            N = counts.Contains(raw.N ?? -1) ? raw.N : counts[0],
            Source = Sources.Contains(raw.Source ?? string.Empty) ? raw.Source : SourceAll,
            Srs = raw.Srs == "1" && mode != Match ? "1" : null,
            From = raw.From is "set" or "hub" or "hard" ? raw.From : null,
            Words = ParseWords(raw.Words) is { Count: > 0 } ids ? string.Join(',', ids) : null,
        };
        q.Scope = q.Set.HasValue ? null : ScopeMine;

        if (mode == Test)
        {
            q.Types = ParseTypes(raw.Types);
            q.Ask = TestAsk.Contains(raw.Ask ?? string.Empty) ? raw.Ask : "both";
        }

        return q;
    }

    private static string ParseTypes(string? csv)
    {
        var picked = (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Where(x => TestTypes.Contains(x))
            .Distinct()
            .ToList();
        // Keep the canonical order so equal choices produce equal URLs.
        var ordered = TestTypes.Where(picked.Contains).ToList();
        return string.Join(',', ordered.Count == 0 ? DefaultTestTypes : ordered);
    }

    /// <summary>CSV of card ids -> distinct positive ids in the given order (max 50). Null/blank/invalid -> empty.</summary>
    public static List<int> ParseWords(string? csv)
    {
        var ids = new List<int>();
        if (string.IsNullOrWhiteSpace(csv))
        {
            return ids;
        }

        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var id) && id > 0 && !ids.Contains(id))
            {
                ids.Add(id);
                if (ids.Count >= MaxWords)
                {
                    break;
                }
            }
        }

        return ids;
    }

    public static string CountUnit(string mode) => mode switch
    {
        Flashcard => "thẻ",
        Match => "cặp",
        Learn => "từ",
        _ => "câu",
    };

    public static string CountLegend(string mode) => mode switch
    {
        Flashcard => "Số thẻ",
        Match => "Số cặp",
        Learn => "Số từ",
        _ => "Số câu",
    };
}
