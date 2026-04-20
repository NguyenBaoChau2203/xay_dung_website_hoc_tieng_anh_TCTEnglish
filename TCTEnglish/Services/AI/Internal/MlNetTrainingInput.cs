namespace TCTEnglish.Services.AI.Internal;

public sealed class MlNetTrainingInput
{
    public string Text { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}
