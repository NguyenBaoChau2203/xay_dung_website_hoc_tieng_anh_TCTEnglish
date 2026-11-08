using TCTEnglish.Services.AI.Internal;

namespace TCTEnglish.Services.AI.Rag;

public static class ChatDisclosurePolicy
{
    public const string RefusalText = "Nội dung yêu cầu không được hỗ trợ.";

    public static bool IsPromptOrCredentialExtraction(string? message)
    {
        var normalized = AiTextNormalizer.Normalize(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (ContainsPromptOrPlatformSecret(normalized))
        {
            return true;
        }

        if (IsOwnPasswordHelp(normalized))
        {
            return false;
        }

        return AiTextNormalizer.ContainsAny(normalized, "password", "mat khau", "credential");
    }

    private static bool IsOwnPasswordHelp(string normalized)
    {
        var aboutOwnPassword = AiTextNormalizer.ContainsAny(normalized, "password", "mat khau");
        var asksForHelp = AiTextNormalizer.ContainsAny(
            normalized,
            "how",
            "change",
            "doi",
            "reset",
            "forgot",
            "quen",
            "cach",
            "huong dan",
            "where",
            "o dau");
        var asksForSomeoneElse = AiTextNormalizer.ContainsAny(
            normalized,
            "nguoi khac",
            "cua user",
            "database",
            "smtp",
            "api key",
            "admin");

        return aboutOwnPassword && asksForHelp && !asksForSomeoneElse;
    }

    private static bool ContainsPromptOrPlatformSecret(string normalized)
    {
        return AiTextNormalizer.ContainsAny(
            normalized,
            "ignore previous instructions",
            "system prompt",
            "api key",
            "apikey",
            "connection string",
            "appsettings",
            "smtp password",
            "database password",
            "hash secret",
            "oauth secret",
            "reset token",
            "mat khau cua nguoi khac",
            "password cua nguoi khac",
            "password user");
    }
}
