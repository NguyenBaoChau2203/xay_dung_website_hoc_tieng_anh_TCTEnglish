using TCTEnglish.Models.Reading;

namespace TCTEnglish.ViewModels;

public class QuestionViewModel
{
    public int Id { get; set; }

    public string QuestionText { get; set; } = "";

    public string QuestionType { get; set; } = ReadingQuestionTypes.MultipleChoice;

    public int Points { get; set; } = 1;

    public List<OptionViewModel> Options { get; set; } = new();

    public string? SavedAnswerText { get; set; }

    public int? SavedSelectedOptionId { get; set; }

    public string? SavedMatchingJson { get; set; }

    public bool IsFlagged { get; set; }
}
