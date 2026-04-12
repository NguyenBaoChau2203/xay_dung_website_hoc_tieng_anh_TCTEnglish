using System.Threading.Tasks;
using TCTEnglish.ViewModels;

namespace TCTEnglish.Services
{
    public interface IListeningService
    {
        /// <summary>
        /// Returns the index page view model with lessons grouped by CEFR level.
        /// Optional filters: level (e.g. "B1") and topic name.
        /// </summary>
        Task<ListeningIndexViewModel> GetIndexViewModelAsync(string? level, string? topic);

        /// <summary>
        /// Returns the full practice page view model for a single lesson,
        /// including transcript, quiz questions and vocab items.
        /// User progress is included when userId is supplied.
        /// </summary>
        Task<ListeningPracticeViewModel?> GetPracticeViewModelAsync(int lessonId, int? userId);

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
    }
}
