using System;

namespace TCTEnglish.Services.AI;

public sealed class AiProviderException : Exception
{
    public AiProviderException(string message, string errorCode, bool isTransient, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
    }

    public string ErrorCode { get; }

    public bool IsTransient { get; }
}

