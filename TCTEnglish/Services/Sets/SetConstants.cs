using System.Text;

namespace TCTEnglish.Services.Sets;

public static class SetVisibility
{
    public const string Private = "Private";
    public const string Public = "Public";

    public static bool IsValid(string? value) => value == Private || value == Public;

    public static string Normalize(string? value) => value == Public ? Public : Private;
}

public static class CardPartsOfSpeech
{
    public const int MaxCount = 6;

    private static readonly (string Code, string Label)[] Entries =
    {
        ("noun", "Danh từ"), ("verb", "Động từ"), ("adj", "Tính từ"), ("adv", "Trạng từ"),
        ("pron", "Đại từ"), ("prep", "Giới từ"), ("conj", "Liên từ"), ("interj", "Thán từ"),
        ("det", "Hạn định từ"), ("phrasal_verb", "Cụm động từ"), ("idiom", "Thành ngữ"),
        ("phrase", "Cụm từ"), ("num", "Số từ"),
    };

    public static IReadOnlyList<(string Code, string Label)> All => Entries;

    public static bool IsValid(string? code)
        => !string.IsNullOrEmpty(code) && Entries.Any(e => e.Code == code);

    public static string LabelOf(string code)
        => Entries.FirstOrDefault(e => e.Code == code).Label ?? code;

    /// <summary>Valid, deduplicated codes from a comma-separated string (max 6).</summary>
    public static IReadOnlyList<string> Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<string>();
        }

        return Filter(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static IReadOnlyList<string> Filter(IEnumerable<string?>? codes)
    {
        if (codes == null)
        {
            return Array.Empty<string>();
        }

        return codes
            .Select(c => c?.Trim().ToLowerInvariant())
            .Where(c => IsValid(c))
            .Select(c => c!)
            .Distinct()
            .Take(MaxCount)
            .ToList();
    }

    public static string? Join(IEnumerable<string?>? codes)
    {
        var list = Filter(codes);
        return list.Count == 0 ? null : string.Join(',', list);
    }
}

public static class SetTopics
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Gia đình", "Nhà cửa", "Ăn uống", "Mua sắm", "Du lịch", "Giao thông", "Công việc", "Kinh doanh",
        "Học tập", "Công nghệ", "Sức khỏe", "Thể thao", "Thiên nhiên", "Thời tiết", "Động vật", "Cảm xúc",
        "Tính cách", "Ngoại hình", "Thời gian", "Xã hội", "Văn hóa – Giải trí", "Giao tiếp hằng ngày", "Khác",
    };
}

public static class CefrLevels
{
    public static readonly IReadOnlyList<string> All = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };

    public static bool IsValid(string? value) => !string.IsNullOrEmpty(value) && All.Contains(value);
}

/// <summary>Stable cover hue for a set: h = (h*31 + c) mod 2^32 over the id's decimal digits.</summary>
public static class SetCoverColor
{
    public static readonly IReadOnlyList<string> Hues = new[] { "primary", "success", "warning", "danger", "info", "accent" };

    public static string For(int setId)
    {
        uint h = 0;
        foreach (var c in setId.ToString())
        {
            unchecked { h = h * 31 + c; }
        }

        return Hues[(int)(h % (uint)Hues.Count)];
    }
}

public static class SetNaming
{
    /// <summary>Strips characters outside letters, digits, space, underscore and hyphen.</summary>
    public static string ToFileName(string? name, string fallback = "bo-the")
    {
        var sb = new StringBuilder();
        foreach (var ch in name ?? string.Empty)
        {
            if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '_' || ch == '-')
            {
                sb.Append(ch);
            }
        }

        var cleaned = sb.ToString().Trim();
        return cleaned.Length == 0 ? fallback : cleaned;
    }
}
