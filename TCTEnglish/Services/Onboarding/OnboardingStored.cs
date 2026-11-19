using System.Text.Json;

namespace TCTEnglish.Services.Onboarding;

/// <summary>Reads the small JSON lists stored on <c>UserOnboarding</c> (never throws on bad data) and short goal names for summaries.</summary>
public static class OnboardingStored
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The decks an onboarding created, in creation order.</summary>
    public static IReadOnlyList<OnboardingDeckRef> Decks(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return Array.Empty<OnboardingDeckRef>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<OnboardingDeckRef>>(json, Json) ?? new List<OnboardingDeckRef>();
        }
        catch (JsonException)
        {
            return Array.Empty<OnboardingDeckRef>();
        }
    }

    public static IReadOnlyList<string> Goals(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json, Json) ?? new List<string>()).Where(OnboardingCatalog.IsGoal).ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>The goal as one or two words for the banner line.</summary>
    public static string GoalShortTitle(string key) => key switch
    {
        "communication" => "Giao tiếp",
        "travel" => "Du lịch",
        "work" => "Công việc",
        "study" => "Học tập",
        "hobbies" => "Sở thích",
        "beginner" => "Từ vựng nền tảng",
        _ => key
    };

    /// <summary>"Du lịch · Giao tiếp · A2 — sửa, xóa hay thêm từ thoải mái, đây là bộ của riêng bạn."</summary>
    public static string BannerSummary(IReadOnlyList<string> goals, string? level)
    {
        var parts = goals.Select(GoalShortTitle).ToList();
        if (!string.IsNullOrEmpty(level))
        {
            parts.Add(level);
        }

        var lead = parts.Count == 0 ? string.Empty : string.Join(" · ", parts) + " — ";
        return lead + "sửa, xóa hay thêm từ thoải mái, đây là bộ của riêng bạn.";
    }
}
