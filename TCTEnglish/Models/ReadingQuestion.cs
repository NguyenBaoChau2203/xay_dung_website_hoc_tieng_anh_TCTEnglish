using System.Collections.Generic;

namespace TCTVocabulary.Models
{
    public class ReadingQuestion
    {
        public int Id { get; set; }

        public int PassageId { get; set; }

        public string QuestionText { get; set; } = null!;

        public int OrderIndex { get; set; }

        public virtual ReadingPassage Passage { get; set; } = null!;

        public virtual ICollection<ReadingOption> Options { get; set; }
            = new List<ReadingOption>();
    }
}