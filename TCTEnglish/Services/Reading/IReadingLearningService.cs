using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Reading;

public interface IReadingLearningService
{
    Task<string> GetUserReadingLevelAsync(int userId, CancellationToken ct = default);
    Task<List<ReadingListViewModel>> GetLibraryAsync(int userId, CancellationToken ct = default);
    Task<ReadingStudyViewModel?> GetStudyAsync(int userId, int passageId, CancellationToken ct = default);
    Task<ReadingAttempt> StartAttemptAsync(int userId, int passageId, string mode, int? timeLimitSeconds, CancellationToken ct = default);
    Task SaveAnswerAsync(int userId, int attemptId, SaveReadingAnswerRequest request, CancellationToken ct = default);
    Task<ReadingSubmitResultViewModel> SubmitAttemptAsync(int userId, int attemptId, CancellationToken ct = default);
    Task SaveAnnotationAsync(int userId, ReadingAnnotationRequest request, CancellationToken ct = default);
    Task DeleteAnnotationAsync(int userId, int annotationId, CancellationToken ct = default);
    Task<List<ReadingAnnotation>> GetAnnotationsAsync(int userId, int passageId, CancellationToken ct = default);
    Task<ReadingVocabularyItem?> LookupWordAsync(int userId, int passageId, string word, CancellationToken ct = default);
    Task<StudentVocabulary> SaveToNotebookAsync(
        int userId,
        int? passageId,
        int? vocabItemId,
        string word,
        ReadingNotebookFields? fields = null,
        CancellationToken ct = default);
    Task<List<ReadingNotebookEntryViewModel>> GetNotebookAsync(int userId, string? search, CancellationToken ct = default);
    Task DeleteNotebookEntryAsync(int userId, int id, CancellationToken ct = default);
    Task<ReadingProgressViewModel> GetProgressAsync(int userId, CancellationToken ct = default);
}

public sealed class SaveReadingAnswerRequest
{
    public int QuestionId { get; set; }
    public int? SelectedOptionId { get; set; }
    public string? AnswerText { get; set; }
    public string? MatchingJson { get; set; }
    public bool IsFlagged { get; set; }
}

public sealed class ReadingAnnotationRequest
{
    public int PassageId { get; set; }
    public string AnnotationType { get; set; } = "Highlight";
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public string SelectedText { get; set; } = "";
    public string? NoteText { get; set; }
    public string Color { get; set; } = "yellow";
}

public sealed class ReadingSubmitResultViewModel
{
    public int AttemptId { get; set; }
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public int CorrectCount { get; set; }
    public int TotalCount { get; set; }
    public List<ReadingQuestionResultViewModel> Details { get; set; } = new();
}

public sealed class ReadingQuestionResultViewModel
{
    public int QuestionId { get; set; }
    public bool IsCorrect { get; set; }
    public string? CorrectOptionText { get; set; }
    public string? CorrectAnswerText { get; set; }
    public string? Explanation { get; set; }
    public string? EvidenceText { get; set; }
    public string? Paraphrase { get; set; }
    public int? EvidenceParagraphId { get; set; }
}

public sealed class ReadingProgressViewModel
{
    public int CompletedCount { get; set; }
    public double AverageScorePercent { get; set; }
    public int TotalStudySeconds { get; set; }
    public double AccuracyPercent { get; set; }
    public List<ReadingAttemptHistoryItem> History { get; set; } = new();
    public Dictionary<string, double> AccuracyByQuestionType { get; set; } = new();
    public List<ReadingScorePoint> ScoreOverTime { get; set; } = new();
}

public sealed class ReadingAttemptHistoryItem
{
    public int AttemptId { get; set; }
    public int PassageId { get; set; }
    public string Title { get; set; } = "";
    public string Level { get; set; } = "";
    public int? Score { get; set; }
    public int? MaxScore { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public sealed class ReadingScorePoint
{
    public DateTime Date { get; set; }
    public double Percent { get; set; }
}
