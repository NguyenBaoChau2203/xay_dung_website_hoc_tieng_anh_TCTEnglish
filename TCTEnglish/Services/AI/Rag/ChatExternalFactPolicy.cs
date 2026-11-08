using TCTEnglish.Services.AI.Internal;
using System.Text.RegularExpressions;

namespace TCTEnglish.Services.AI.Rag;

public static class ChatExternalFactPolicy
{
    public static bool IsUnscopedGenerationRequest(string? message)
    {
        var normalized = AiTextNormalizer.Normalize(message);
        // Explicit source requests remain eligible for authorized retrieval.
        // This is a task-scope refusal, never a test that evidence is sufficient.
        if (AiTextNormalizer.ContainsAny(normalized,
            "stored passage", "the passage", "the lesson", "saved card", "stored card",
            "trong bai", "trong bo", "trong the", "da luu"))
        {
            return false;
        }

        return Regex.IsMatch(normalized,
            @"^(?:(?:please )?do my .+ homework|(?:tell|give) me (?:a )?random fact .+|lam bai .+ nay giup toi|giai thich .+)$",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
    }

    public static bool IsExplicitExternalFactRequest(string? message)
    {
        var normalized = AiTextNormalizer.Normalize(message);
        if (string.IsNullOrWhiteSpace(normalized)
            || AsksForStoredMeaning(normalized)
            || AsksForOwnLearningData(normalized))
        {
            return false;
        }

        return HasCurrentWorldFactDemand(normalized)
            || AiTextNormalizer.ContainsAny(
            normalized,
            "gia vang",
            "gia usd",
            "ty gia",
            "chung khoan",
            "thoi tiet",
            "tin tuc",
            "bitcoin",
            "gia hom nay",
            "hom nay la bao nhieu");
    }

    private static bool AsksForStoredMeaning(string normalized)
    {
        return AiTextNormalizer.ContainsAny(
            normalized,
            "nghia",
            "dinh nghia",
            "meaning",
            "definition",
            "what does",
            "mean",
            "means",
            "la gi",
            "phien am",
            "vi du",
            "example");
    }

    private static bool AsksForOwnLearningData(string normalized)
    {
        return AiTextNormalizer.ContainsAny(
            normalized,
            "tien do",
            "progress",
            "streak",
            "tu vung",
            "muc tieu",
            "goal",
            "lop hoc",
            "speaking",
            "premium",
            "billing",
            "subscription",
            "hoc duoc",
            "hoc den dau");
    }

    private static bool HasCurrentWorldFactDemand(string normalized)
    {
        var aboutNow = AiTextNormalizer.ContainsAny(
            normalized,
            "today",
            "hom nay",
            "ngay hom nay",
            "right now",
            "bay gio",
            "hien tai",
            "currently",
            "luc nay");
        if (!aboutNow)
        {
            return false;
        }

        return AiTextNormalizer.ContainsAny(
            normalized,
            "what is",
            "what are",
            "how is",
            "how much",
            "how are",
            "bao nhieu",
            "the nao",
            "ra sao",
            "gia");
    }

}
