using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services
{
    public interface IStudyService
    {
        /// <param name="mode">"due" returns due reviews (not suspended, next review passed) then up to 20 new cards.</param>
        Task<StudyViewModel?> GetStudyViewModelAsync(int setId, int? userId = null, string? mode = null);
        Task<OperationResult> UpdateCardProgressAsync(int cardId, bool isKnown, int userId);
        Task<VocabularyIndexViewModel> GetVocabularyIndexViewModelAsync(int currentUserId);
        Task<VocabularySetDetailViewModel?> GetVocabularySetDetailViewModelAsync(int setId, int currentUserId);
        Task<VocabularyTopicsViewModel?> GetVocabularyTopicsViewModelAsync(int setId, int currentUserId);
        Task<VocabularyTopicDetailViewModel?> GetVocabularyTopicDetailViewModelAsync(int setId, int currentUserId, string? topic);
        Task<StudyViewModel?> GetVocabularyStudyViewModelAsync(int setId, int currentUserId, string? topic, int index, string mode);
        Task<VocabularyFolderDetailViewModel?> GetVocabularyFolderDetailViewModelAsync(int folderId, int currentUserId);
        Task<bool> TryIncrementVocabularySetViewCountAsync(int setId);

        // Rebuilt Vocabulary module. A null result means the folder or set is not a system folder or set.
        Task<VocabularyHomeViewModel?> GetVocabularyHomeAsync(int userId, int? folderId = null);
        Task<VocabularyCourseViewModel?> GetVocabularyCourseAsync(int setId, int userId);
        Task<VocabularyLessonViewModel?> GetVocabularyLessonAsync(int setId, int userId, int? lesson, string? topic, bool review);
        Task<VocabularyLessonViewModel> GetVocabularyReviewAsync(int userId);
    }
}
