using System;

namespace TCTEnglish.ViewModels.AI;

public sealed record ChatReplyDto(
    string Text,
    Guid ConversationId,
    string ConversationTitle,
    ChatUsageDto Usage,
    ChatMetadataDto Metadata,
    ChatGroundingDto? Grounding = null);

public sealed record ChatUsageDto(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    string Model);

public sealed record ChatMetadataDto(
    string RequestId,
    int LatencyMs);

