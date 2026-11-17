using TCTEnglish.ViewModels;

namespace TCTVocabulary.Services
{
    public interface IWritingService
    {
        Task<DateTime?> RecordExerciseVisitAsync(int userId, int exerciseId, CancellationToken ct = default);
        Task<IReadOnlyList<RecentWritingExerciseViewModel>> GetRecentExercisesAsync(int userId, int limit = 6, CancellationToken ct = default);
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
            string? status = null,
            int myPage = 1);
        Task<WritingCreateFromAiResultViewModel?> GetWritingGenerationAsync(int userId, Guid operationId, CancellationToken ct = default);
        Task<WritingCreateFromAiResultViewModel?> GetWritingGenerationByKeyAsync(int userId, string key, CancellationToken ct = default);
        Task<WritingGenerationQuotaViewModel?> GetWritingGenerationQuotaAsync(int userId, CancellationToken ct = default);
        Task<WritingCreateFromAiResultViewModel> ConfirmWritingTemplateAsync(int userId, Guid operationId, WritingTemplateConfirmation confirmation, CancellationToken ct = default);
        Task<WritingCreateFromAiResultViewModel> CancelWritingGenerationAsync(int userId, Guid operationId, CancellationToken ct = default);
        Task<WritingPracticeDataViewModel?> GetWritingPracticeDataAsync(int exerciseId, int userId);
        Task<WritingSentenceHintViewModel?> GetWritingSentenceHintAsync(int exerciseId, int sentenceId, int userId);
        Task<WritingSentenceEvaluationViewModel?> EvaluateWritingSentenceAsync(
            int exerciseId,
            int sentenceId,
            string userAnswer,
            int userId,
            CancellationToken cancellationToken = default);
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
