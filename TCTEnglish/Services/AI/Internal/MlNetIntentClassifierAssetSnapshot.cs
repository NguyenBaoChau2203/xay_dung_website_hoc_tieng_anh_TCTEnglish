namespace TCTEnglish.Services.AI.Internal;

public sealed record MlNetIntentClassifierAssetSnapshot(
    string SeedDatasetAbsolutePath,
    bool SeedDatasetExists,
    string ModelArtifactAbsolutePath,
    bool ModelArtifactExists);
