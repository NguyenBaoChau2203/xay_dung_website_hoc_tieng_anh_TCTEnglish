using System.Collections.Generic;
using TCTEnglish.Models;

namespace TCTEnglish.Services.AI;

public sealed record AiConversationPageDto(
    IReadOnlyList<AiConversationSummaryDto> Items,
    bool HasMore);

public sealed record AiMessagePageDto(
    IReadOnlyList<AiMessage> Items,
    bool HasMore);
