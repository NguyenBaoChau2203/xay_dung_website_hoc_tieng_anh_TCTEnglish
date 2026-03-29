using System.Collections.Generic;

namespace TCTEnglish.Services.AI;

public sealed record AiContextBuildResult(
    IReadOnlyList<AiContextMessage> Messages,
    int InputTokens,
    int ReservedOutputTokens,
    int PlannedTotalTokens,
    bool WasCurrentMessageTrimmed);

