namespace TCTEnglish.Services.AI.Internal;

public sealed record IntentClassification(
    UserIntent Intent,
    float Confidence,
    string ClassifierName);
