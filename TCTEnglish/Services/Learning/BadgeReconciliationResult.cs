namespace TCTEnglish.Services.Learning;

/// <summary>Operations-only receipt; never expose candidate codes through learner endpoints.</summary>
public sealed record BadgeReconciliationResult(int UserId, bool DryRun, IReadOnlyList<int> BadgeIds);
