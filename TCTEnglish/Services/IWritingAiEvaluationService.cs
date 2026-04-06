using System.Threading;
using TCTEnglish.Services.AI;

namespace TCTVocabulary.Services
{
    public interface IWritingAiEvaluationService
    {
        Task<WritingAiEvaluationResult?> TryEvaluateSentenceAsync(
            WritingAiEvaluationRequest request,
            CancellationToken ct = default);
    }

    public sealed record WritingAiEvaluationRequest(
        int SentenceId,
        string VietnameseText,
        string ReferenceAnswer,
        string LearnerAnswer);

    public sealed record WritingAiEvaluationResult(
        bool Passed,
        string OverallFeedback,
        string MeaningFeedback,
        string GrammarFeedback,
        string NaturalnessFeedback,
        string WordChoiceFeedback,
        string SuggestedRewrite);
}
