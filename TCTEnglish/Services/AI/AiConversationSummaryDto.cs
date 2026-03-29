using System;

namespace TCTEnglish.Services.AI;

public sealed record AiConversationSummaryDto(
    Guid Id,
    string Title,
    DateTime UpdatedAtUtc,
    DateTime CreatedAtUtc);

