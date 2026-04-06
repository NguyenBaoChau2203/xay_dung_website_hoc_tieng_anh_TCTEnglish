using TCTEnglish.ViewModels;

namespace TCTVocabulary.Services
{
    public interface IWritingService
    {
        Task<WritingIndexViewModel> GetWritingIndexViewModelAsync(string? selectedLevel);
        Task<WritingExerciseDataViewModel> GetWritingExerciseDataAsync(
            string? selectedLevel,
            string? contentType,
            string? topic);
        Task<WritingExerciseListViewModel> GetWritingExerciseListViewModelAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            int page);
        Task<WritingPracticeDataViewModel?> GetWritingPracticeDataAsync(int exerciseId);
        Task<WritingSentenceHintViewModel?> GetWritingSentenceHintAsync(int exerciseId, int sentenceId);
        Task<WritingSentenceEvaluationViewModel?> EvaluateWritingSentenceAsync(
            int exerciseId,
            int sentenceId,
            string userAnswer);
        Task<WritingPracticeViewModel?> GetWritingPracticeViewModelAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            int page,
            int? exerciseId);
    }
}
