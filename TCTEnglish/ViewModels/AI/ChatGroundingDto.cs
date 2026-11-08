using TCTEnglish.Models;
using TCTEnglish.Services.AI.Rag;

namespace TCTEnglish.ViewModels.AI;

public sealed record ChatGroundingDto(
    int SchemaVersion,
    string? Outcome,
    string? Scope,
    DateTime? CapturedAtUtc,
    string Status,
    string? Notice,
    IReadOnlyList<ChatGroundingSourceDto> Sources,
    string? StateLabel = null);

public sealed record ChatGroundingSourceDto(
    string SourceKey,
    string SourceType,
    string SentenceId,
    string? Title,
    string? Route,
    string Freshness);

public static class ChatGroundingMapper
{
    public static ChatGroundingDto? From(AiMessageGrounding? grounding)
    {
        if (grounding is null)
        {
            return null;
        }

        return new ChatGroundingDto(
            grounding.SchemaVersion,
            grounding.Outcome,
            grounding.Scope,
            grounding.CapturedAtUtc,
            grounding.Status,
            grounding.Notice,
            grounding.Sources.Select(source => new ChatGroundingSourceDto(
                source.SourceKey,
                source.SourceType,
                source.SentenceId,
                source.Title,
                LearnerSourceLink.IsAllowed(source.Route) ? source.Route : null,
                source.Freshness)).ToList(),
            LearnerChatState.Label(grounding.Outcome, grounding.Status));
    }
}
