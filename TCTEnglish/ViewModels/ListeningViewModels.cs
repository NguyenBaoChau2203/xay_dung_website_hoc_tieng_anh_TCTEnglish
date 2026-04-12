using System;
using System.Collections.Generic;

namespace TCTEnglish.ViewModels
{
    // ================================================================
    // Index page — lesson browser grouped by CEFR level
    // ================================================================

    public class ListeningIndexViewModel
    {
        /// <summary>Lessons grouped by CEFR level key (A1, A2, B1, B2, C1).</summary>
        public Dictionary<string, List<ListeningLessonCardViewModel>> LessonsByLevel { get; set; } = new();

        /// <summary>All distinct topic names for the filter UI.</summary>
        public List<string> Topics { get; set; } = new();

        /// <summary>Level metadata (display name, icon, description) ordered A1→C1.</summary>
        public List<ListeningLevelMetaViewModel> Levels { get; set; } = new();

        /// <summary>Currently active level filter (null = all).</summary>
        public string? SelectedLevel { get; set; }

        /// <summary>Currently active topic filter (null = all).</summary>
        public string? SelectedTopic { get; set; }
    }

    public class ListeningLevelMetaViewModel
    {
        public string Key { get; set; } = string.Empty;      // "A1", "B2" …
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
        public string AccentColor { get; set; } = string.Empty;
        public int LessonCount { get; set; }
    }

    // ================================================================
    // Lesson card — displayed on the index page
    // ================================================================

    public class ListeningLessonCardViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string? Duration { get; set; }
        public string? Speaker1Name { get; set; }
        public string? Speaker2Name { get; set; }
        public string? Speaker1Country { get; set; }
        public string? Speaker2Country { get; set; }
        public int TranscriptLineCount { get; set; }
        public int QuizQuestionCount { get; set; }
        public int VocabItemCount { get; set; }

        // Progress for the authenticated user (null = not started)
        public bool? TranscriptCompleted { get; set; }
        public bool? QuizCompleted { get; set; }
        public int? QuizScore { get; set; }
        public bool? VocabReviewed { get; set; }
    }

    // ================================================================
    // Practice page — full lesson detail
    // ================================================================

    public class ListeningPracticeViewModel
    {
        public int LessonId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string? YoutubeId { get; set; }
        public string? AudioUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Duration { get; set; }
        public string? Speaker1Name { get; set; }
        public string? Speaker2Name { get; set; }
        public string? Speaker1Country { get; set; }
        public string? Speaker2Country { get; set; }

        public List<ListeningTranscriptLineViewModel> TranscriptLines { get; set; } = new();
        public List<ListeningQuizQuestionViewModel> QuizQuestions { get; set; } = new();
        public List<ListeningVocabItemViewModel> VocabItems { get; set; } = new();

        // Current user's saved progress (null = guest / not started)
        public bool TranscriptCompleted { get; set; }
        public bool QuizCompleted { get; set; }
        public int? QuizScore { get; set; }
        public bool VocabReviewed { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    // ================================================================
    // Transcript line
    // ================================================================

    public class ListeningTranscriptLineViewModel
    {
        public int Id { get; set; }
        public int OrderIndex { get; set; }
        public string Speaker { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? VietnameseMeaning { get; set; }
        public double? StartTime { get; set; }
        public double? EndTime { get; set; }
    }

    // ================================================================
    // Quiz question
    // ================================================================

    public class ListeningQuizQuestionViewModel
    {
        public int Id { get; set; }
        public int OrderIndex { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        // CorrectAnswer NOT exposed on index — only returned after evaluation
    }

    // ================================================================
    // Vocabulary item
    // ================================================================

    public class ListeningVocabItemViewModel
    {
        public int Id { get; set; }
        public int OrderIndex { get; set; }
        public string Word { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public string? ExampleSentence { get; set; }
        public string? ImageUrl { get; set; }
    }

    // ================================================================
    // Quiz evaluation
    // ================================================================

    /// <summary>Posted body from client: question id → chosen answer ("A"/"B"/"C"/"D").</summary>
    public class ListeningQuizSubmitDto
    {
        public int LessonId { get; set; }
        public Dictionary<int, string> Answers { get; set; } = new(); // questionId → "A"/"B"/"C"/"D"
    }

    public class ListeningQuizResultViewModel
    {
        public int LessonId { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int ScorePercent { get; set; }
        public bool Passed { get; set; }
        public List<ListeningQuizAnswerResult> Answers { get; set; } = new();
    }

    public class ListeningQuizAnswerResult
    {
        public int QuestionId { get; set; }
        public int OrderIndex { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? UserAnswer { get; set; }
        public string CorrectAnswer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public string? Explanation { get; set; }
    }

    // ================================================================
    // Progress update DTO (POST body from JS)
    // ================================================================

    public class ListeningProgressUpdateDto
    {
        public bool? TranscriptCompleted { get; set; }
        public bool? QuizCompleted { get; set; }
        public int? QuizScore { get; set; }
        public bool? VocabReviewed { get; set; }
    }
}
