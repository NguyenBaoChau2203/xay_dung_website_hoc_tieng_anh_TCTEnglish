using TCTEnglish.Services.AI;

namespace TCTEnglish.Services.AI.Rag;

public interface IAiChatAnswerService
{
    Task<AiChatAnswer> AnswerAsync(
        int userId,
        string userMessage,
        IReadOnlyList<AiContextMessage> contextMessages,
        CancellationToken ct);
}
