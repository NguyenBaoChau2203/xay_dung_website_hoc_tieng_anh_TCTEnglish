namespace TCTEnglish.Services.AI.Voice;

public sealed class AiVoiceOptions
{
    public const string SectionName = "AI:Voice";
    public bool Enabled { get; set; }
    public string Model { get; set; } = "gpt-realtime-2.1";
    public string Voice { get; set; } = "marin";
    public int StandardDailySeconds { get; set; } = 180;
    public int PremiumDailySeconds { get; set; } = 900;
    public int StandardSessionSeconds { get; set; } = 180;
    public int PremiumSessionSeconds { get; set; } = 600;
    public int MaxConcurrentSessions { get; set; } = 10;
    public int SetupTimeoutSeconds { get; set; } = 30;
    public int IdleTimeoutSeconds { get; set; } = 60;
    public int CleanupMaxAttempts { get; set; } = 8;
}

public class VoiceException(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed class VoiceProviderRejectedException() : VoiceException("provider_rejected", 503);
