using System;
using TCTVocabulary.Models;

namespace TCTEnglish.Models;

public enum TranslationVoteType
{
    Like = 1,
    Dislike = -1
}

public class ReadingTranslationVote
{
    public int Id { get; set; }

    public int TranslationId { get; set; }

    public int UserId { get; set; }

    public TranslationVoteType VoteType { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // ─── Navigation ───────────────────────────────────────────────────────────
    public virtual ReadingUserTranslation Translation { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
