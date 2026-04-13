using System;

namespace TCTVocabulary.Models
{
    public class UserReadingHistory
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int ReadingPassageId { get; set; }

        public DateTime ViewedAt { get; set; }

        public bool IsCompleted { get; set; } = false;

        public int? Score { get; set; }

        public virtual User User { get; set; } = null!;

        public virtual ReadingPassage ReadingPassage { get; set; } = null!;
    }
}