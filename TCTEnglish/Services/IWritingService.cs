using TCTEnglish.ViewModels;

namespace TCTVocabulary.Services
{
    public interface IWritingService
    {
        Task<WritingIndexViewModel> GetWritingIndexViewModelAsync(string? selectedLevel);
        Task<WritingExerciseDataViewModel> GetWritingExerciseDataAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            int? userId = null,
            string? status = null);
        Task<WritingExerciseListViewModel> GetWritingExerciseListViewModelAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            int page,
            int? userId = null,
            string? status = null);
        Task<WritingPracticeDataViewModel?> GetWritingPracticeDataAsync(int exerciseId, int userId);
        Task<WritingSentenceHintViewModel?> GetWritingSentenceHintAsync(int exerciseId, int sentenceId);
        Task<WritingSentenceEvaluationViewModel?> EvaluateWritingSentenceAsync(
            int exerciseId,
            int sentenceId,
            string userAnswer,
            int userId);
        Task<WritingPracticeViewModel?> GetWritingPracticeViewModelAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            int page,
            int? exerciseId,
            int userId,
            string? status = null);
    }
}
