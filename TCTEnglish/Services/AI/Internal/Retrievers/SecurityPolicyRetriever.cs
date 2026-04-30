namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class SecurityPolicyRetriever : IKnowledgeRetriever
{
    public bool CanHandle(UserIntent intent) => intent == UserIntent.OutOfScope;

    public Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var normalizedMessage = AiTextNormalizer.Normalize(userMessage);
        if (!IsSensitiveSecurityQuestion(normalizedMessage))
        {
            return Task.FromResult<IReadOnlyList<KnowledgeSnippet>>([]);
        }

        return Task.FromResult<IReadOnlyList<KnowledgeSnippet>>(
        [
            new(
                "security-policy",
                "reason=sensitive-platform-data",
                KnowledgeSnippetSources.SecurityPolicy,
                Priority: 10)
        ]);
    }

    private static bool IsSensitiveSecurityQuestion(string normalizedMessage)
    {
        return AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "connection string",
            "appsettings",
            "api key",
            "apikey",
            "secret",
            "hash secret",
            "oauth secret",
            "smtp password",
            "database password",
            "mat khau cua nguoi khac",
            "password cua nguoi khac",
            "password user",
            "reset token",
            "cookie",
            "session",
            "jwt",
            "ma nguon",
            "source code",
            "backend logic",
            "route admin",
            "admin route",
            "duong dan admin",
            "bang users",
            "du lieu nguoi khac",
            "tai khoan nguoi khac",
            "email nguoi khac",
            "hack",
            "bypass",
            "vuot quyen",
            "leo quyen",
            "doi role admin",
            "cap quyen admin",
            "grant admin");
    }
}
