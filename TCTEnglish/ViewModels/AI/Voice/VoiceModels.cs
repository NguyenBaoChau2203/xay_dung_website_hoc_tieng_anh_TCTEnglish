using System.ComponentModel.DataAnnotations;

namespace TCTEnglish.ViewModels.AI.Voice;

public sealed class VoiceTopicInput
{
    [Required, StringLength(120, MinimumLength = 1)] public string Title { get; set; } = "";
    [Required, StringLength(2000, MinimumLength = 1)] public string Scenario { get; set; } = "";
    [Required, StringLength(120, MinimumLength = 1)] public string AiRole { get; set; } = "";
    [Required, StringLength(120, MinimumLength = 1)] public string LearnerRole { get; set; } = "";
    [Required, RegularExpression("^(A1|A2|B1|B2|C1|C2)$")] public string Level { get; set; } = "A2";
}

public sealed class VoiceStartInput
{
    public Guid SessionId { get; set; }
    [Required, RegularExpression("^(catalog|custom)$")] public string TopicKind { get; set; } = "catalog";
    [Range(1, int.MaxValue)] public int TopicId { get; set; }
    [Required, StringLength(256 * 1024)] public string OfferSdp { get; set; } = "";
}

public sealed record VoiceTopicVm(int Id, string Kind, string Title, string Scenario, string AiRole, string LearnerRole, string Level);
public sealed record VoiceTopicsVm(IReadOnlyList<VoiceTopicVm> Catalog, IReadOnlyList<VoiceTopicVm> Custom, bool CanCreateCustom, bool Available, int DailySeconds, int UsedSeconds, int ReservedSeconds);
public sealed record VoiceStartVm(Guid SessionId, string AnswerSdp, DateTime ExpiresAtUtc);
public sealed record VoiceHistoryVm(Guid Id, string Title, DateTime CreatedAtUtc, string State, string? CloseReason, string AccountingState);
public sealed record VoiceTurnVm(long Id, string Speaker, string Text, bool Interrupted);
public sealed record VoiceHistoryDetailVm(VoiceHistoryVm Session, IReadOnlyList<VoiceTurnVm> Turns);
public sealed record VoicePageVm(bool Available, bool Inline = false);
