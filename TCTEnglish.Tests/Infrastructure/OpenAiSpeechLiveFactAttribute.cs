using Xunit;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Explicit paid OpenAI speech opt-in. Discovery never performs network I/O.
/// Loopback Whisper flags must never enable this path.
/// </summary>
public sealed class OpenAiSpeechLiveFactAttribute : FactAttribute
{
    public const string FlagName = "TCT_OPENAI_SPEECH_LIVE";

    public OpenAiSpeechLiveFactAttribute()
    {
        if (!IsEnabled())
        {
            Skip = "Not run: set TCT_OPENAI_SPEECH_LIVE=1 with owner budget for paid OpenAI speech checks.";
        }
    }

    public static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable(FlagName), "1", StringComparison.Ordinal);
}
