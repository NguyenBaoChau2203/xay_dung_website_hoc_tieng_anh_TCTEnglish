using System;

namespace TCTVocabulary.Models;

public class WritingAcceptedAnswer
{
    public int Id { get; set; }
    public int WritingExerciseSentenceId { get; set; }
    public string AnswerText { get; set; } = null!;
    /// <summary>SHA-256 hex of normalized answer (64 ASCII chars).</summary>
    public string NormalizedHash { get; set; } = null!;
    public int? ApprovedByUserId { get; set; }
    public DateTime ApprovedAtUtc { get; set; }

    public WritingExerciseSentence WritingExerciseSentence { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
}
