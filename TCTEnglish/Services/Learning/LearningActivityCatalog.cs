using TCTVocabulary.Models;

namespace TCTEnglish.Services.Learning;

public static class LearningActivityCatalog
{
    public static bool TryMap(string? feature, string? mode, out string skill, out string storedMode)
    {
        skill = string.Empty;
        storedMode = string.Empty;
        if (string.IsNullOrWhiteSpace(feature))
        {
            return false;
        }

        switch (feature.Trim())
        {
            case "Flashcard":
            case "Quiz":
            case "WriteMode":
                skill = nameof(GoalArea.Vocabulary);
                storedMode = feature.Trim();
                return true;
            case "Speaking":
                skill = nameof(GoalArea.Speaking);
                storedMode = string.IsNullOrWhiteSpace(mode) ? "Speaking" : mode.Trim();
                return true;
            case "WritingPractice":
                skill = nameof(GoalArea.Writing);
                storedMode = "WritingPractice";
                return true;
            case "Listening":
                skill = nameof(GoalArea.Listening);
                storedMode = "Listening";
                return true;
            case "Reading":
                skill = nameof(GoalArea.Reading);
                storedMode = "Reading";
                return true;
            default:
                return false;
        }
    }
}
