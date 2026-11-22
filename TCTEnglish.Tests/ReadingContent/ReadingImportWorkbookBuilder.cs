using ClosedXML.Excel;
using TCTEnglish.Models.Reading;

namespace TCTEnglish.Tests.ReadingContent;

internal sealed record ReadingVocabRow(string Word, string Ipa, string Pos, string MeaningVi, string Example, int ParagraphNo);

internal sealed record ReadingAnswerRow(
    string QuestionId,
    string OptionId,
    string OptionContent,
    string IsCorrect,
    string? Explanation = null,
    string? EvidenceText = null,
    string? EvidenceParagraphId = null,
    string? Paraphrase = null,
    string? MatchKey = null,
    string? MatchSide = null);

internal sealed record ReadingQuestionRow(
    string QuestionId,
    int Order,
    string Type,
    string Content,
    string ParagraphReference,
    int Points = 1);

internal sealed record ReadingPack(
    string ReadingCode,
    string Title,
    string Cefr,
    string Topic,
    int DurationMinutes,
    string Description,
    IReadOnlyList<string> Paragraphs,
    IReadOnlyList<ReadingQuestionRow> Questions,
    IReadOnlyList<ReadingAnswerRow> Answers,
    IReadOnlyList<ReadingVocabRow> Vocabulary);

internal static class ReadingImportWorkbookBuilder
{
    public static void WriteWorkbook(string outputPath, string cefr, IReadOnlyList<ReadingPack> packs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var workbook = new XLWorkbook();
        WriteGuide(workbook);

        var info = workbook.AddWorksheet("Reading_Info");
        WriteHeader(info, "ReadingCode", "Title", "CEFR", "Topic", "Duration", "Description", "ImageUrl");
        var passage = workbook.AddWorksheet("Passage");
        WriteHeader(passage, "ReadingCode", "PassageTitle", "ParagraphNo", "Content");
        var questions = workbook.AddWorksheet("Questions");
        WriteHeader(questions, "Question_ID", "ReadingCode", "Question_Order", "Question_Type", "Question_Content", "Paragraph_Reference", "Points");
        var answers = workbook.AddWorksheet("Answers");
        WriteHeader(answers, "Question_ID", "Option_ID", "Option_Content", "Is_Correct", "Explanation", "Evidence_Text", "Evidence_Paragraph_ID", "Paraphrase", "Match_Key", "Match_Side");
        var vocab = workbook.AddWorksheet("Vocabulary");
        WriteHeader(vocab, "ReadingCode", "Word", "Pronunciation", "PartOfSpeech", "Meaning", "Example", "ParagraphNo");

        var infoRow = 2;
        var passageRow = 2;
        var questionRow = 2;
        var answerRow = 2;
        var vocabRow = 2;

        foreach (var pack in packs)
        {
            if (!string.Equals(pack.Cefr, cefr, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Pack {pack.ReadingCode} CEFR {pack.Cefr} does not match workbook {cefr}.");
            if (pack.Paragraphs.Count < 2)
                throw new InvalidOperationException($"{pack.ReadingCode} needs ≥2 paragraphs.");

            info.Cell(infoRow, 1).Value = pack.ReadingCode;
            info.Cell(infoRow, 2).Value = pack.Title;
            info.Cell(infoRow, 3).Value = pack.Cefr;
            info.Cell(infoRow, 4).Value = pack.Topic;
            info.Cell(infoRow, 5).Value = pack.DurationMinutes;
            info.Cell(infoRow, 6).Value = pack.Description;
            info.Cell(infoRow, 7).Value = "";
            infoRow++;

            for (var i = 0; i < pack.Paragraphs.Count; i++)
            {
                passage.Cell(passageRow, 1).Value = pack.ReadingCode;
                passage.Cell(passageRow, 2).Value = pack.Title;
                passage.Cell(passageRow, 3).Value = i + 1;
                passage.Cell(passageRow, 4).Value = pack.Paragraphs[i];
                passageRow++;
            }

            foreach (var q in pack.Questions)
            {
                questions.Cell(questionRow, 1).Value = q.QuestionId;
                questions.Cell(questionRow, 2).Value = pack.ReadingCode;
                questions.Cell(questionRow, 3).Value = q.Order;
                questions.Cell(questionRow, 4).Value = q.Type;
                questions.Cell(questionRow, 5).Value = q.Content;
                questions.Cell(questionRow, 6).Value = q.ParagraphReference;
                questions.Cell(questionRow, 7).Value = q.Points;
                questionRow++;
            }

            foreach (var a in pack.Answers)
            {
                answers.Cell(answerRow, 1).Value = a.QuestionId;
                answers.Cell(answerRow, 2).Value = a.OptionId;
                answers.Cell(answerRow, 3).Value = a.OptionContent;
                answers.Cell(answerRow, 4).Value = a.IsCorrect;
                if (a.Explanation is not null) answers.Cell(answerRow, 5).Value = a.Explanation;
                if (a.EvidenceText is not null) answers.Cell(answerRow, 6).Value = a.EvidenceText;
                if (a.EvidenceParagraphId is not null) answers.Cell(answerRow, 7).Value = a.EvidenceParagraphId;
                if (a.Paraphrase is not null) answers.Cell(answerRow, 8).Value = a.Paraphrase;
                if (a.MatchKey is not null) answers.Cell(answerRow, 9).Value = a.MatchKey;
                if (a.MatchSide is not null) answers.Cell(answerRow, 10).Value = a.MatchSide;
                answerRow++;
            }

            foreach (var v in pack.Vocabulary)
            {
                vocab.Cell(vocabRow, 1).Value = pack.ReadingCode;
                vocab.Cell(vocabRow, 2).Value = v.Word;
                vocab.Cell(vocabRow, 3).Value = v.Ipa;
                vocab.Cell(vocabRow, 4).Value = v.Pos;
                vocab.Cell(vocabRow, 5).Value = v.MeaningVi;
                vocab.Cell(vocabRow, 6).Value = v.Example;
                vocab.Cell(vocabRow, 7).Value = v.ParagraphNo;
                vocabRow++;
            }
        }

        workbook.Worksheet("Guide").Position = 1;
        workbook.SaveAs(outputPath);
    }

    public static ReadingPack BuildStandardPack(
        string code,
        string title,
        string cefr,
        string topic,
        int duration,
        string description,
        IReadOnlyList<string> paragraphs,
        string mcQuestion,
        string mcCorrect,
        string mcWrong1,
        string mcWrong2,
        string tfStatement,
        bool tfIsTrue,
        string gapQuestion,
        string gapAnswer,
        string matchPrompt,
        (string Left, string Right, string Key)[] matchPairs,
        string shortQuestion,
        string shortAnswer,
        string evidenceParagraphId,
        string evidenceText,
        IReadOnlyList<ReadingVocabRow> vocabulary)
    {
        // Question_ID must be unique across the whole workbook (parser indexes Answers by Question_ID only).
        string Q(int n) => $"{code}-Q{n}";

        var questions = new List<ReadingQuestionRow>
        {
            new(Q(1), 1, ReadingQuestionTypes.MultipleChoice, mcQuestion, evidenceParagraphId),
            new(Q(2), 2, ReadingQuestionTypes.TrueFalse, tfStatement, evidenceParagraphId),
            new(Q(3), 3, ReadingQuestionTypes.GapFill, gapQuestion, evidenceParagraphId),
            new(Q(4), 4, ReadingQuestionTypes.Matching, matchPrompt, "1"),
            new(Q(5), 5, ReadingQuestionTypes.ShortAnswer, shortQuestion, evidenceParagraphId)
        };

        var answers = new List<ReadingAnswerRow>
        {
            new(Q(1), "A", mcCorrect, "TRUE", "See the passage evidence.", evidenceText, evidenceParagraphId, mcCorrect),
            new(Q(1), "B", mcWrong1, "FALSE"),
            new(Q(1), "C", mcWrong2, "FALSE"),
            new(Q(2), "TRUE", "True", tfIsTrue ? "TRUE" : "FALSE", "Check the stated fact.", evidenceText, evidenceParagraphId),
            new(Q(2), "FALSE", "False", tfIsTrue ? "FALSE" : "TRUE"),
            new(Q(3), "ANSWER", gapAnswer, "TRUE", "Fill the blank from the passage.", evidenceText, evidenceParagraphId),
        };

        foreach (var pair in matchPairs)
        {
            answers.Add(new(Q(4), "L" + pair.Key, pair.Left, "FALSE", "Match the pairs from the passage.", null, "1", null, pair.Key, "Left"));
            answers.Add(new(Q(4), "R" + pair.Key, pair.Right, "FALSE", null, null, null, null, pair.Key, "Right"));
        }

        answers.Add(new(Q(5), "ANSWER", shortAnswer, "TRUE", "Answer briefly from the passage.", evidenceText, evidenceParagraphId, shortAnswer));

        return new ReadingPack(code, title, cefr, topic, duration, description, paragraphs, questions, answers, vocabulary);
    }

    private static void WriteGuide(XLWorkbook workbook)
    {
        var guide = workbook.AddWorksheet("Guide");
        WriteHeader(guide, "Topic", "Instruction");
        guide.Cell(2, 1).Value = "Template";
        guide.Cell(2, 2).Value = "TCT Reading import pack — keep sheet names and headers exactly.";
        guide.Cell(3, 1).Value = "ReadingCode";
        guide.Cell(3, 2).Value = "Excel ReadingCode is a temporary join key (L1, L2). Import always assigns RD-{CEFR}-NNN.";
        guide.Cell(4, 1).Value = "Word count";
        guide.Cell(4, 2).Value = "Passage body only. Soft targets: A1 80–120; A2 120–180; B1 200–300; B2 300–450; C1 450–600; C2 550–750. See docs/content/reading-content-authoring-guidelines.md.";
        guide.Cell(5, 1).Value = "Duration";
        guide.Cell(5, 2).Value = "Suggested minutes: A1=6; A2=8; B1=12; B2=18; C1=22; C2=28. Difficulty is not length alone.";
        guide.Cell(6, 1).Value = "Evidence";
        guide.Cell(6, 2).Value = "Evidence_Paragraph_ID must match Passage ParagraphNo for the same ReadingCode.";
        guide.Cell(7, 1).Value = "Types";
        guide.Cell(7, 2).Value = string.Join(", ", ReadingQuestionTypes.All);
    }

    private static void WriteHeader(IXLWorksheet sheet, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;
    }
}
