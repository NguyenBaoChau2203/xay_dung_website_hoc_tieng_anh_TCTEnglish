using System.Collections.Generic;

namespace TCTEnglish.ViewModels
{
    public class WritingIndexViewModel
    {
        public string SelectedLevelKey { get; set; } = string.Empty;
        public string SelectedLevelTitle { get; set; } = string.Empty;
        public List<WritingLevelCardViewModel> Levels { get; set; } = new();
        public List<WritingContentTypeCardViewModel> ContentTypes { get; set; } = new();
    }

    public class WritingLevelCardViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DurationText { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
        public string AccentColor { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class WritingContentTypeCardViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
        public string AccentColor { get; set; } = string.Empty;
    }

    public class WritingExerciseListViewModel
    {
        public string SelectedLevelKey { get; set; } = string.Empty;
        public string SelectedLevelTitle { get; set; } = string.Empty;
        public string SelectedContentTypeKey { get; set; } = string.Empty;
        public string SelectedContentTypeTitle { get; set; } = string.Empty;
        public string SelectedTopic { get; set; } = "all";
        public string SelectedStatus { get; set; } = "all";
        public bool ShowProgressMetadata { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int StartItemNumber { get; set; }
        public int EndItemNumber { get; set; }
        public List<WritingFilterOptionViewModel> TopicOptions { get; set; } = new();
        public List<WritingFilterOptionViewModel> StatusOptions { get; set; } = new();
        public List<int?> PageNumbers { get; set; } = new();
        public List<WritingExerciseCardViewModel> Exercises { get; set; } = new();
    }

    public class WritingExerciseDataViewModel
    {
        public string SelectedLevelKey { get; set; } = string.Empty;
        public string SelectedLevelTitle { get; set; } = string.Empty;
        public string SelectedContentTypeKey { get; set; } = string.Empty;
        public string SelectedContentTypeTitle { get; set; } = string.Empty;
        public string SelectedTopic { get; set; } = "all";
        public string SelectedStatus { get; set; } = "all";
        public bool ShowProgressMetadata { get; set; }
        public List<WritingFilterOptionViewModel> TopicOptions { get; set; } = new();
        public List<WritingFilterOptionViewModel> StatusOptions { get; set; } = new();
        public List<WritingExerciseCardViewModel> Exercises { get; set; } = new();
    }

    public class WritingFilterOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class WritingExerciseCardViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public int SentenceCount { get; set; }
        public int AttemptCount { get; set; }
        public int CompletedSentenceCount { get; set; }
        public string StatusKey { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string StartActionLabel { get; set; } = "Bắt đầu";
        public string LastAttemptedAtDisplay { get; set; } = string.Empty;
    }

    public class WritingPracticeDataViewModel
    {
        public string SelectedLevelKey { get; set; } = string.Empty;
        public string SelectedLevelTitle { get; set; } = string.Empty;
        public string SelectedContentTypeKey { get; set; } = string.Empty;
        public string SelectedContentTypeTitle { get; set; } = string.Empty;
        public int ExerciseId { get; set; }
        public string ExerciseTitle { get; set; } = string.Empty;
        public string ExercisePreviewText { get; set; } = string.Empty;
        public string ExerciseTopic { get; set; } = string.Empty;
        public int TotalSentenceCount { get; set; }
        public int CompletedSentenceCount { get; set; }
        public int AttemptCount { get; set; }
        public int ResumeSentenceId { get; set; }
        public string StatusKey { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string LastAttemptedAtDisplay { get; set; } = string.Empty;
        public List<WritingPracticeSentenceViewModel> Sentences { get; set; } = new();
        public List<WritingAttemptHistoryItemViewModel> RecentAttempts { get; set; } = new();
    }

    public class WritingPracticeViewModel
    {
        public string SelectedLevelKey { get; set; } = string.Empty;
        public string SelectedLevelTitle { get; set; } = string.Empty;
        public string SelectedContentTypeKey { get; set; } = string.Empty;
        public string SelectedContentTypeTitle { get; set; } = string.Empty;
        public string SelectedTopic { get; set; } = "all";
        public string SelectedStatus { get; set; } = "all";
        public int SelectedPage { get; set; } = 1;
        public int ExerciseId { get; set; }
        public string ExerciseTitle { get; set; } = string.Empty;
        public string ExercisePreviewText { get; set; } = string.Empty;
        public string ExerciseTopic { get; set; } = string.Empty;
        public int TotalSentenceCount { get; set; }
        public int CompletedSentenceCount { get; set; }
        public int AttemptCount { get; set; }
        public int ResumeSentenceId { get; set; }
        public string StatusKey { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string LastAttemptedAtDisplay { get; set; } = string.Empty;
        public List<WritingPracticeSentenceViewModel> Sentences { get; set; } = new();
        public List<WritingAttemptHistoryItemViewModel> RecentAttempts { get; set; } = new();
    }

    public class WritingPracticeSentenceViewModel
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public string VietnameseText { get; set; } = string.Empty;
        public string Placeholder { get; set; } = string.Empty;
        public bool BreakAfter { get; set; }
        public int AttemptCount { get; set; }
        public string LastSubmittedAnswer { get; set; } = string.Empty;
        public string AcceptedAnswer { get; set; } = string.Empty;
        public bool HasAccepted { get; set; }
        public bool? LastEvaluationPassed { get; set; }
    }

    public class WritingSentenceHintViewModel
    {
        public int ExerciseId { get; set; }
        public int SentenceId { get; set; }
        public int SentenceNumber { get; set; }
        public string HintTitle { get; set; } = string.Empty;
        public string HintText { get; set; } = string.Empty;
    }

    public class WritingSentenceEvaluationRequestViewModel
    {
        public int ExerciseId { get; set; }
        public int SentenceId { get; set; }
        public string UserAnswer { get; set; } = string.Empty;
    }

    public class WritingSentenceEvaluationViewModel
    {
        public int ExerciseId { get; set; }
        public int SentenceId { get; set; }
        public int SentenceNumber { get; set; }
        public bool Passed { get; set; }
        public bool CanAutoAdvance { get; set; }
        public bool UsedAi { get; set; }
        public string EvaluationSource { get; set; } = string.Empty;
        public string SummaryTitle { get; set; } = string.Empty;
        public string SummaryText { get; set; } = string.Empty;
        public string ReviewText { get; set; } = string.Empty;
        public string MeaningFeedback { get; set; } = string.Empty;
        public string GrammarFeedback { get; set; } = string.Empty;
        public string NaturalnessFeedback { get; set; } = string.Empty;
        public string WordChoiceFeedback { get; set; } = string.Empty;
        public string SuggestedRewrite { get; set; } = string.Empty;
        public int SentenceAttemptCount { get; set; }
        public int ExerciseAttemptCount { get; set; }
        public int CompletedSentenceCount { get; set; }
        public string ExerciseStatusKey { get; set; } = string.Empty;
        public string ExerciseStatusLabel { get; set; } = string.Empty;
    }

    public class WritingAttemptHistoryItemViewModel
    {
        public int SentenceId { get; set; }
        public int SentenceNumber { get; set; }
        public bool Passed { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string SubmittedAnswerPreview { get; set; } = string.Empty;
        public string EvaluationSource { get; set; } = string.Empty;
        public string TimestampDisplay { get; set; } = string.Empty;
    }
}
