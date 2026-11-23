using TCTVocabulary.Models;

namespace TCTEnglish.Areas.Admin.ViewModels;

internal static class ReadingEditorMapper
{
    public static ReadingEditorViewModel FromPassage(ReadingPassage passage) => new()
    {
        Id = passage.Id,
        Title = passage.Title,
        Description = passage.Description,
        Topic = passage.Topic,
        Level = passage.Level,
        EstimatedTimeMinutes = passage.EstimatedTimeMinutes,
        Content = passage.Content,
        ImageUrl = passage.ImageUrl,
        ExternalReadingKey = passage.ExternalReadingKey,
        Status = passage.Status,
        Paragraphs = passage.Paragraphs.OrderBy(p => p.ParagraphOrder).Select(p => new ReadingParagraphEditorViewModel
        {
            Id = p.Id,
            ExternalParagraphKey = p.ExternalParagraphKey,
            ParagraphOrder = p.ParagraphOrder,
            ParagraphContent = p.ParagraphContent
        }).ToList(),
        Questions = passage.Questions.OrderBy(q => q.OrderIndex).Select(q => new ReadingQuestionEditorViewModel
        {
            Id = q.Id,
            ExternalQuestionKey = q.ExternalQuestionKey,
            OrderIndex = q.OrderIndex,
            QuestionType = q.QuestionType,
            QuestionText = q.QuestionText,
            ParagraphReference = q.ParagraphReference,
            Points = q.Points,
            CorrectAnswerText = q.CorrectAnswerText,
            Explanation = q.Explanation,
            EvidenceText = q.EvidenceText,
            EvidenceParagraphId = q.EvidenceParagraphId,
            Paraphrase = q.Paraphrase,
            Options = q.Options.OrderBy(o => o.OptionOrder).Select(o => new ReadingOptionEditorViewModel
            {
                Id = o.Id,
                ExternalOptionKey = o.ExternalOptionKey,
                OptionText = o.OptionText,
                IsCorrect = o.IsCorrect,
                MatchKey = o.MatchKey,
                MatchSide = o.MatchSide,
                OptionOrder = o.OptionOrder
            }).ToList()
        }).ToList(),
        Vocabulary = passage.VocabularyItems.Select(v => new ReadingVocabularyEditorViewModel
        {
            Id = v.Id,
            ExternalVocabularyKey = v.ExternalVocabularyKey,
            Word = v.Word,
            Ipa = v.Ipa,
            PartOfSpeech = v.PartOfSpeech,
            EnglishDefinition = v.EnglishDefinition,
            VietnameseMeaning = v.VietnameseMeaning,
            Example = v.Example,
            ParagraphId = v.ParagraphId,
            ParagraphOrder = v.Paragraph?.ParagraphOrder
        }).ToList()
    };
}
