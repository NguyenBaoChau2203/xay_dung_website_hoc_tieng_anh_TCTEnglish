using System;
using System.Collections.Generic;
using TCTEnglish.Models;
using TCTEnglish.Models.Reading;

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

        public string? Description { get; set; }

        public int? EstimatedTimeMinutes { get; set; }

        /// <summary>Workflow status. Published syncs with <see cref="IsPublished"/>.</summary>
        public string Status { get; set; } = ReadingStatuses.Draft;

        public bool IsPublished { get; set; }

        public int? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }

        public int? PublishedByUserId { get; set; }

        public string? ExternalReadingKey { get; set; }

        public virtual User? CreatedByUser { get; set; }

        public virtual User? PublishedByUser { get; set; }

        public virtual ICollection<ReadingQuestion> Questions { get; set; }
            = new List<ReadingQuestion>();

        public virtual ICollection<ReadingParagraph> Paragraphs { get; set; }
            = new List<ReadingParagraph>();

        public virtual ICollection<ReadingVocabularyItem> VocabularyItems { get; set; }
            = new List<ReadingVocabularyItem>();

        public virtual ICollection<UserReadingHistory> UserReadingHistories { get; set; }
            = new List<UserReadingHistory>();

        public virtual ICollection<ReadingUserTranslation> UserTranslations { get; set; }
            = new List<ReadingUserTranslation>();

        public virtual ICollection<ReadingAttempt> Attempts { get; set; }
            = new List<ReadingAttempt>();

        public virtual ICollection<ReadingAnnotation> Annotations { get; set; }
            = new List<ReadingAnnotation>();

        public virtual ICollection<ReadingReviewLog> ReviewLogs { get; set; }
            = new List<ReadingReviewLog>();

        public void SyncPublishedFlag()
        {
            IsPublished = string.Equals(Status, ReadingStatuses.Published, StringComparison.Ordinal);
        }
    }
}
