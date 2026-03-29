using System;

namespace TCTEnglish.Services.AI;

public sealed class SimpleAiTokenCounter : IAiTokenCounter
{
    public int CountTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4d));
    }
}

