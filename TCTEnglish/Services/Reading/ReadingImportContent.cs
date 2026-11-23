using System.Text.Json;
using TCTEnglish.Areas.Admin.ViewModels;
using TCTEnglish.Models.Reading;

namespace TCTEnglish.Services.Reading;

internal static class ReadingImportContent
{
    public static string Serialize(ReadingEditorViewModel reading)
    {
        var paragraphs = reading.Paragraphs.OrderBy(p => p.ParagraphOrder).ToList();
        var paragraphOrders = paragraphs.Where(p => p.Id.HasValue)
            .ToDictionary(p => p.Id!.Value, p => p.ParagraphOrder);
        int? OrderFor(int? id) => id.HasValue
            && paragraphOrders.TryGetValue(id.Value, out var order) ? order : null;

        // Database IDs, workbook join keys, workflow state and the cover asset are not lesson content.
        // In particular, the same ZIP cover can receive a different URL on each preview.
        return JsonSerializer.Serialize(new
        {
            Title = reading.Title.Trim(),
            Level = CefrLevels.Normalize(reading.Level),
            Description = Optional(reading.Description),
            Topic = Optional(reading.Topic),
            reading.EstimatedTimeMinutes,
            Content = paragraphs.Count == 0 ? reading.Content.Trim() : null,
            Paragraphs = paragraphs.Select(p => new { p.ParagraphOrder, Content = p.ParagraphContent.Trim() }),
            Questions = reading.Questions.OrderBy(q => q.OrderIndex).Select(q => new
            {
                q.OrderIndex,
                q.QuestionType,
                Text = q.QuestionText.Trim(),
                q.Points,
                Reference = Optional(q.ParagraphReference),
                Answer = Optional(q.CorrectAnswerText),
                Explanation = Optional(q.Explanation),
                Evidence = Optional(q.EvidenceText),
                EvidenceOrder = q.EvidenceParagraphOrder ?? OrderFor(q.EvidenceParagraphId),
                Paraphrase = Optional(q.Paraphrase),
                Options = q.Options.OrderBy(o => o.OptionOrder).Select(o => new
                {
                    o.OptionOrder,
                    Text = o.OptionText.Trim(),
                    o.IsCorrect,
                    MatchKey = Optional(o.MatchKey),
                    MatchSide = Optional(o.MatchSide)
                })
            }),
            Vocabulary = reading.Vocabulary.Select(v => JsonSerializer.Serialize(new
            {
                Word = v.Word.Trim(),
                Ipa = Optional(v.Ipa),
                PartOfSpeech = Optional(v.PartOfSpeech),
                Definition = Optional(v.EnglishDefinition),
                Meaning = Optional(v.VietnameseMeaning),
                Example = Optional(v.Example),
                ParagraphOrder = v.ParagraphOrder ?? OrderFor(v.ParagraphId)
            })).Order(StringComparer.Ordinal)
        });
    }

    private static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
