using System;

namespace TCTEnglish.Services.AI;

public sealed class AiProviderException : Exception
{
    public const string ErrorCodeTimeout = "timeout";
    public const string ErrorCodeNetwork = "network";
    public const string ErrorCodeEmptyResponse = "empty_response";
    public const string ErrorCodeUnknown = "unknown";
    public const string ErrorCodeInvalidConfiguration = "invalid_configuration";
    public const string ErrorCodeAuthentication = "authentication";
    public const string ErrorCodeRateLimited = "rate_limited";
    public const string ErrorCodeProviderUnavailable = "provider_unavailable";
    public const string ErrorCodeIncompleteResponse = "incomplete_response";
    public const string ErrorCodeInvalidResponse = "invalid_response";
    public const string ErrorCodeRefusal = "refusal";
    public const string ErrorCodeQuota = "quota_exhausted";
    public const string ErrorCodeModelAccess = "model_access";
    public const string ErrorCodeRedirectRefused = "redirect_refused";
    public const string ErrorCodeBudgetExhausted = "budget_exhausted";
    public const string ErrorCodeCorpusStale = "corpus_stale";

    public AiProviderException(string message, string errorCode, bool isTransient, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? ErrorCodeUnknown : errorCode;
        IsTransient = isTransient;
    }

    public string ErrorCode { get; }

    public bool IsTransient { get; }

    public int RecordedPromptTokens { get; private set; }

    public int RecordedCompletionTokens { get; private set; }

    public int RecordedTotalTokens { get; private set; }

    public int RecordedUnknownTokens { get; private set; }

    public bool HasRecordedUsage { get; private set; }

    public AiProviderException WithRecordedUsage(int promptTokens, int completionTokens, int totalTokens, int unknownTokens = 0)
    {
        RecordedPromptTokens = Math.Max(0, promptTokens);
        RecordedCompletionTokens = Math.Max(0, completionTokens);
        RecordedTotalTokens = Math.Max(0, totalTokens);
        RecordedUnknownTokens = Math.Max(0, unknownTokens);
        HasRecordedUsage = RecordedPromptTokens > 0 || RecordedCompletionTokens > 0 || RecordedTotalTokens > 0;
        return this;
    }
}

