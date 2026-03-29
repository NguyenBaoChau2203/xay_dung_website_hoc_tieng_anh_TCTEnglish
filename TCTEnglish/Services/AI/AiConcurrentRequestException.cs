using System;

namespace TCTEnglish.Services.AI;

public sealed class AiConcurrentRequestException : Exception
{
    public const string DefaultErrorCode = "conversation_busy";

    public AiConcurrentRequestException(string message, string errorCode = DefaultErrorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
