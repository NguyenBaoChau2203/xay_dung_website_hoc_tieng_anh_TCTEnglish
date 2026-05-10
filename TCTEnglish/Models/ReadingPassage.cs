using System;
using System.Collections.Generic;
using TCTEnglish.Models;

namespace TCTVocabulary.Models
{
    public class ReadingPassage
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;

        public string Level { get; set; } = null!; // A1, A2, B1...

        public string? Topic { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsPublished { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<ReadingQuestion> Questions { get; set; }
            = new List<ReadingQuestion>();

        public virtual ICollection<UserReadingHistory> UserReadingHistories { get; set; }
            = new List<UserReadingHistory>();

        public virtual ICollection<ReadingUserTranslation> UserTranslations { get; set; }
            = new List<ReadingUserTranslation>();
    }
}