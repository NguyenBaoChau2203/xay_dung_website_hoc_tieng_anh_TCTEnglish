using Microsoft.AspNetCore.Http;
using TCTEnglish.Areas.Admin.ViewModels;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Reading;

public interface IReadingAdminService
{
    Task<List<ReadingPassageListItemViewModel>> ListAsync(string? search, string? status, CancellationToken ct = default);
    Task<ReadingPassage?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<ReadingPassage> CreateDraftAsync(ReadingEditorViewModel model, int actorUserId, CancellationToken ct = default);
    Task UpdateDraftAsync(int id, ReadingEditorViewModel model, int actorUserId, CancellationToken ct = default);
    /// <summary>Persist a reading card image under /images/reading/ and return its public URL.</summary>
    Task<string> SavePassageImageAsync(IFormFile file, CancellationToken ct = default);
    Task ReplaceQuestionsAsync(int passageId, IReadOnlyList<ReadingQuestionEditorViewModel> questions, CancellationToken ct = default);
    Task ReplaceVocabularyAsync(int passageId, IReadOnlyList<ReadingVocabularyEditorViewModel> items, CancellationToken ct = default);
    Task SubmitForReviewAsync(int passageId, int actorUserId, CancellationToken ct = default);
    Task RequestRevisionAsync(int passageId, int actorUserId, string reason, CancellationToken ct = default);
    Task RejectAsync(int passageId, int actorUserId, string reason, CancellationToken ct = default);
    Task ApproveAsync(int passageId, int actorUserId, CancellationToken ct = default);
    Task PublishAsync(int passageId, int actorUserId, CancellationToken ct = default);
    Task ArchiveAsync(int passageId, int actorUserId, CancellationToken ct = default);
    Task DeleteAsync(int passageId, CancellationToken ct = default);
    IReadOnlyList<string> ValidateForReview(ReadingPassage passage);
}
