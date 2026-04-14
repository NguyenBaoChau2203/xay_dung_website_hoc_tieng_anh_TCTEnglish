namespace TCTEnglish.Services.AI.Internal;

public sealed record MlNetTrainingResult(
    bool Success,
    int SampleCount,
    int IntentCount,
    float MicroAccuracy,
    float MacroAccuracy,
    string? ErrorMessage,
    TimeSpan Duration);
