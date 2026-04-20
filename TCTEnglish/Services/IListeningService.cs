using System.Threading.Tasks;
using TCTEnglish.ViewModels;
using TCTVocabulary.Services;

namespace TCTEnglish.Services
{
    public interface IListeningService
    {
        /// <summary>
        /// Returns the index page view model with lessons grouped by CEFR level.
        /// Optional filters: level (e.g. "B1") and topic name.
        /// </summary>
        Task<ListeningIndexViewModel> GetIndexViewModelAsync(string? level, string? topic, int? userId = null);

        /// <summary>
        /// Returns the full practice page view model for a single lesson,
        /// including transcript, quiz questions and vocab items.
        /// User progress is included when userId is supplied.
        /// </summary>
        Task<ListeningPracticeViewModel?> GetPracticeViewModelAsync(int lessonId, int? userId);

        Task<OperationResult> GenerateQuizFromTranscriptAsync(int lessonId, int userId, CancellationToken ct = default);

        /// <summary>
        /// Grades a submitted quiz and returns per-question feedback.
        /// Does NOT persist progress — call SaveProgressAsync separately.
        /// </summary>
        Task<ListeningQuizResultViewModel?> EvaluateQuizAsync(ListeningQuizSubmitDto dto);

        /// <summary>
        /// Upserts the user's listening progress for a lesson.
        /// Enforces Anti-IDOR: userId must match the authenticated user.
        /// </summary>
        Task<bool> SaveProgressAsync(int userId, int lessonId, ListeningProgressUpdateDto dto);

        /// <summary>
        /// Import a YouTube video as a private listening lesson for Premium/Admin users
        /// </summary>
        Task<ListeningImportResult> CreateOwnedLessonAsync(int userId, string youtubeUrl, System.Threading.CancellationToken ct = default);

        /// <summary>
        /// Deletes a private listening lesson for Premium/Admin users
        /// </summary>
        Task<OperationResult> DeleteOwnedLessonAsync(int userId, int lessonId);

        /// <summary>
        /// Translates the transcript to Vietnamese using AI if not already translated.
        /// </summary>
        Task<OperationResult> TranslateTranscriptAsync(int lessonId, int userId, CancellationToken ct = default);
    }

    public class ListeningImportResult
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }
        public int? LessonId { get; private set; }
        public bool RequiresUpgrade { get; private set; }

        private ListeningImportResult() { }

        public static ListeningImportResult Success(int lessonId) => new() { IsSuccess = true, LessonId = lessonId };
        public static ListeningImportResult Invalid(string message) => new() { IsSuccess = false, ErrorMessage = message };
        public static ListeningImportResult UpgradeRequired(string message) => new() { IsSuccess = false, ErrorMessage = message, RequiresUpgrade = true };
    }
}
