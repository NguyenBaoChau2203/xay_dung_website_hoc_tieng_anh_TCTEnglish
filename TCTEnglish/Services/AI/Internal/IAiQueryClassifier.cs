namespace TCTEnglish.Services.AI.Internal;

public interface IAiQueryClassifier
{
    IntentClassification Classify(string userMessage);
}
