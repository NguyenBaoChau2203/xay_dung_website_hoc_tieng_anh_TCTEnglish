using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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

        /// <summary>Currently active level filter (null = all).</summary>
        public string? SelectedLevel { get; set; }

        /// <summary>Currently active topic filter (null = all).</summary>
        public string? SelectedTopic { get; set; }

        /// <summary>User's imported private lessons.</summary>
        public List<ListeningLessonCardViewModel> MyLessons { get; set; } = new();

        /// <summary>Whether the feature is locked for standard users.</summary>
        public bool IsMyLessonsSectionLocked { get; set; }

        /// <summary>All published public lessons, ignoring the level/topic filter.</summary>
        public int TotalLessons { get; set; }

        /// <summary>Public lessons the user finished (same population as <see cref="TotalLessons"/>).</summary>
        public int CompletedLessons { get; set; }

        /// <summary>Public lessons the user started but has not finished.</summary>
        public int InProgressLessons { get; set; }

        /// <summary>The usable, unfinished lesson the user opened most recently (public or own).</summary>
        public ListeningLessonCardViewModel? ContinueLesson { get; set; }

        public bool IsAuthenticated { get; set; }

        /// <summary>Premium or Admin: may import YouTube lessons.</summary>
        public bool CanImport { get; set; }
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
        public string? YoutubeId { get; set; }
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

        /// <summary>True when the lesson is finished (CompletedAt set, or every required section done).</summary>
        public bool IsCompleted { get; set; }

        /// <summary>"new" (no progress row), "progress" or "done".</summary>
        public string Status { get; set; } = "new";

        /// <summary>Required sections finished (0-3).</summary>
        public int StepsDone { get; set; }

        /// <summary>Sections that have content (0-3).</summary>
        public int StepsTotal { get; set; }

        public bool IsPrivate { get; set; }
        public bool IsLocked { get; set; }
        public string? ImportStatus { get; set; }
        public DateTime CreatedAt { get; set; }

        private static readonly Regex YoutubeIdPattern = new("^[A-Za-z0-9_-]{6,20}$", RegexOptions.Compiled);

        /// <summary>
        /// Safe thumbnail URL for the card: the stored URL when it is https or a local path, otherwise the YouTube
        /// thumbnail of a well-formed video id, otherwise null (the page shows a local placeholder).
        /// </summary>
        public string? ThumbnailSrc
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ThumbnailUrl)
                    && (ThumbnailUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        || (ThumbnailUrl.StartsWith("/", StringComparison.Ordinal) && !ThumbnailUrl.StartsWith("//", StringComparison.Ordinal))))
                {
                    return ThumbnailUrl;
                }

                if (!string.IsNullOrWhiteSpace(YoutubeId) && YoutubeIdPattern.IsMatch(YoutubeId))
                {
                    return $"https://img.youtube.com/vi/{YoutubeId}/hqdefault.jpg";
                }

                return null;
            }
        }

        /// <summary>Speaker names for the card, or null when there is nothing meaningful to show (imports use "Speaker").</summary>
        public string? SpeakerText
        {
            get
            {
                var first = IsRealSpeaker(Speaker1Name) ? Speaker1Name!.Trim() : null;
                var second = IsRealSpeaker(Speaker2Name) ? Speaker2Name!.Trim() : null;
                if (first == null) return second;
                return second == null ? first : $"{first} và {second}";
            }
        }

        private static bool IsRealSpeaker(string? name) =>
            !string.IsNullOrWhiteSpace(name) && !string.Equals(name.Trim(), "Speaker", StringComparison.OrdinalIgnoreCase);
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

        public bool IsOwnLesson { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsCompleted { get; set; }

        /// <summary>Premium/Admin; for quiz generation also requires an own lesson or Admin.</summary>
        public bool CanUseAi { get; set; }

        /// <summary>Premium/Admin and allowed to translate this lesson.</summary>
        public bool CanTranslate { get; set; }

        /// <summary>Premium/Admin: playback speed control.</summary>
        public bool CanUseSpeed { get; set; }

        private static readonly Regex VideoIdPattern = new("^[A-Za-z0-9_-]{6,20}$", RegexOptions.Compiled);

        /// <summary>The YouTube id when it is well formed (safe for a URL and for markup), otherwise null.</summary>
        public string? SafeYoutubeId =>
            !string.IsNullOrWhiteSpace(YoutubeId) && VideoIdPattern.IsMatch(YoutubeId) ? YoutubeId : null;

        /// <summary>The audio URL when it is https or a local path, otherwise null.</summary>
        public string? SafeAudioUrl =>
            !string.IsNullOrWhiteSpace(AudioUrl)
            && (AudioUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || (AudioUrl.StartsWith("/", StringComparison.Ordinal) && !AudioUrl.StartsWith("//", StringComparison.Ordinal)))
                ? AudioUrl
                : null;
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

        /// <summary>Best stored score for the signed-in user (null for callers without a user id).</summary>
        public int? BestScorePercent { get; set; }
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
