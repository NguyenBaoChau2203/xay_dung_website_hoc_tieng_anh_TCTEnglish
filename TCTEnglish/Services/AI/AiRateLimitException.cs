namespace TCTEnglish.Services.AI;

public sealed class AiRateLimitException : Exception
{
    public AiRateLimitException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

