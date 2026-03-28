using TCTEnglish.ViewModels;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services
{
    public interface IStudyService
    {
        Task<StudyViewModel?> GetStudyViewModelAsync(int setId, int? userId = null);
        Task<OperationResult> UpdateCardProgressAsync(int cardId, bool isKnown, int userId);
        Task<WritingIndexViewModel> GetWritingIndexViewModelAsync(string? selectedLevel);
        Task<WritingExerciseDataViewModel> GetWritingExerciseDataAsync(
            string? selectedLevel,
            string? contentType,
            string? topic);
        Task<WritingExerciseListViewModel> GetWritingExerciseListViewModelAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            string? status,
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
            string? status,
            int page,
            int? exerciseId);
        Task<VocabularyIndexViewModel> GetVocabularyIndexViewModelAsync(int currentUserId);
        Task<VocabularySetDetailViewModel?> GetVocabularySetDetailViewModelAsync(int setId, int currentUserId);
        Task<VocabularyTopicsViewModel?> GetVocabularyTopicsViewModelAsync(int setId, int currentUserId);
        Task<VocabularyTopicDetailViewModel?> GetVocabularyTopicDetailViewModelAsync(int setId, int currentUserId, string? topic);
        Task<StudyViewModel?> GetVocabularyStudyViewModelAsync(int setId, int currentUserId, string? topic, int index, string mode);
        Task<VocabularyFolderDetailViewModel?> GetVocabularyFolderDetailViewModelAsync(int folderId, int currentUserId);
        Task<bool> TryIncrementVocabularySetViewCountAsync(int setId);
    }
}
