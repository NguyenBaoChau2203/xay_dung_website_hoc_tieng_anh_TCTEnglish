using System.Collections.Generic;
using TCTEnglish.Models;

namespace TCTEnglish.Services.AI;

public interface IAiContextBuilder
{
    AiContextBuildResult BuildContextMessages(
        string systemPrompt,
        string currentUserMessage,
        IReadOnlyList<AiMessage> history,
        AiOptions options);
}

