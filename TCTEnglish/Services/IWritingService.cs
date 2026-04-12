using TCTEnglish.ViewModels;

namespace TCTVocabulary.Services
{
    public interface IWritingService
    {
        Task<WritingIndexViewModel> GetWritingIndexViewModelAsync(string? selectedLevel);
        Task<WritingCreateFromAiResultViewModel> CreateFromAiAsync(
            WritingCreateFromAiRequestViewModel request,
            int userId,
            CancellationToken ct = default);
        Task<OperationResult> DeleteOwnedExerciseAsync(int exerciseId, int userId);
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
        Task<WritingSentenceHintViewModel?> GetWritingSentenceHintAsync(int exerciseId, int sentenceId, int userId);
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
