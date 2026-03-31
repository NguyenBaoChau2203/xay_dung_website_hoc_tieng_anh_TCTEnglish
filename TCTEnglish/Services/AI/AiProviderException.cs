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

    public AiProviderException(string message, string errorCode, bool isTransient, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? ErrorCodeUnknown : errorCode;
        IsTransient = isTransient;
    }

    public string ErrorCode { get; }

    public bool IsTransient { get; }
}

