namespace TCTEnglish.Services.AI.Internal;

public sealed class MlNetIntentClassifierOptions
{
    public const string SectionName = "AI:IntentClassifier";

    public string SeedDatasetPath { get; set; } = "Services/AI/Internal/Data/intent-samples.seed.csv";

    public string ModelArtifactPath { get; set; } = "Services/AI/Internal/Data/intent-classifier-model.zip";
}
