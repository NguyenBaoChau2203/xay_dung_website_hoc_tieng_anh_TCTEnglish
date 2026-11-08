namespace TCTEnglish.Services.AI;

/// <summary>
/// Embeddings transport settings. These defaults are code, not appsettings.
/// The text model, reasoning effort and response endpoint stay on <see cref="AiOptions"/>.
/// </summary>
public sealed class EmbeddingOptions
{
    public const string ModelName = "text-embedding-3-small";
    public const int Dimensions = 1536;
    public const string EncodingFormat = "float";
    public const int MaxInputsPerRequest = 64;

    public int MaxInputCharacters { get; init; } = 8_000;

    public int MaxResponseBytes { get; init; } = 8 * 1024 * 1024;

    public int RequestTimeoutSeconds { get; init; } = 30;

    public int RetryMaxAttempts { get; init; } = 1;

    public int EffectiveTimeoutSeconds => Math.Clamp(RequestTimeoutSeconds, 1, 120);

    public int EffectiveRetryMaxAttempts => Math.Clamp(RetryMaxAttempts, 0, 2);
}

/// <summary>
/// Operator-supplied ceiling for one explicit embedding rebuild.
/// Values above the cap are rejected before any HTTP call.
/// </summary>
public sealed class EmbeddingBudget
{
    public const int RequestCap = 12;
    public const int TokenCap = 40_000;

    private EmbeddingBudget(int maxRequests, int maxInputTokens)
    {
        MaxRequests = maxRequests;
        MaxInputTokens = maxInputTokens;
    }

    public int MaxRequests { get; }

    public int MaxInputTokens { get; }

    public static bool TryCreate(int maxRequests, int maxInputTokens, out EmbeddingBudget? budget, out string error)
    {
        budget = null;
        if (maxRequests < 1 || maxRequests > RequestCap)
        {
            error = "request-cap";
            return false;
        }

        if (maxInputTokens < 1 || maxInputTokens > TokenCap)
        {
            error = "token-cap";
            return false;
        }

        budget = new EmbeddingBudget(maxRequests, maxInputTokens);
        error = string.Empty;
        return true;
    }
}

public sealed record EmbeddingBatch(IReadOnlyList<float[]> Vectors, int PromptTokens, int TotalTokens);
