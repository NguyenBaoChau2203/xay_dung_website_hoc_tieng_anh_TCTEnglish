using System.Collections.Generic;
using TCTEnglish.Models.Reading;

namespace TCTVocabulary.Models
{
    public class ReadingQuestion
    {
        public int Id { get; set; }

        public int PassageId { get; set; }

        public string? ExternalQuestionKey { get; set; }

        public string QuestionText { get; set; } = null!;

        public string QuestionType { get; set; } = ReadingQuestionTypes.MultipleChoice;

        public int OrderIndex { get; set; }

        public int Points { get; set; } = 1;

        public string? ParagraphReference { get; set; }

        public string? CorrectAnswerText { get; set; }

        public string? Explanation { get; set; }

        public string? EvidenceText { get; set; }

        public int? EvidenceParagraphId { get; set; }

        public string? Paraphrase { get; set; }

        public virtual ReadingPassage Passage { get; set; } = null!;

        public virtual ReadingParagraph? EvidenceParagraph { get; set; }

        public virtual ICollection<ReadingOption> Options { get; set; }
            = new List<ReadingOption>();
    }
}
