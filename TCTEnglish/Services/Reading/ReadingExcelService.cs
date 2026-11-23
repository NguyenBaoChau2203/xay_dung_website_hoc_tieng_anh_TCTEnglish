using ClosedXML.Excel;
using System.IO.Compression;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Areas.Admin.ViewModels;
using TCTEnglish.Infrastructure.SharedData;
using TCTEnglish.Models.Reading;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Reading;

public sealed class ReadingExcelService : IReadingExcelService
{
    private static readonly string[] RequiredSheets =
        ["Reading_Info", "Passage", "Questions", "Answers", "Vocabulary"];

    private readonly DbflashcardContext _db;
    private readonly IReadingAdminService _admin;

    public ReadingExcelService(DbflashcardContext db, IReadingAdminService admin)
    {
        _db = db;
        _admin = admin;
    }

    public byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();
        BuildGuideSheet(workbook);

        var info = workbook.AddWorksheet("Reading_Info");
        WriteHeader(info, "ReadingCode", "Title", "CEFR", "Topic", "Duration", "Description", "ImageUrl");
        info.Cell(2, 1).Value = "L1";
        info.Cell(2, 2).Value = "The Importance of Trees";
        info.Cell(2, 3).Value = "A1";
        info.Cell(2, 4).Value = "Environment";
        info.Cell(2, 5).Value = 15;
        info.Cell(2, 6).Value = "Sample reading covering all five question types.";
        info.Cell(2, 7).Value = "";
        ApplyDataSheetLayout(info, freezeRows: 1, wrapColumns: [6, 7]);
        ApplyListValidation(info.Range(2, 3, 200, 3), CefrLevels.All);

        var passage = workbook.AddWorksheet("Passage");
        WriteHeader(passage, "ReadingCode", "PassageTitle", "ParagraphNo", "Content");
        passage.Cell(2, 1).Value = "L1";
        passage.Cell(2, 2).Value = "The Importance of Trees";
        passage.Cell(2, 3).Value = 1;
        passage.Cell(2, 4).Value = "Trees play an important role in our environment.";
        passage.Cell(3, 1).Value = "L1";
        passage.Cell(3, 2).Value = "The Importance of Trees";
        passage.Cell(3, 3).Value = 2;
        passage.Cell(3, 4).Value = "They provide oxygen and help reduce air pollution.";
        ApplyDataSheetLayout(passage, freezeRows: 1, wrapColumns: [4]);

        var questions = workbook.AddWorksheet("Questions");
        WriteHeader(questions, "Question_ID", "ReadingCode", "Question_Order", "Question_Type", "Question_Content", "Paragraph_Reference", "Points");
        WriteQuestionSample(questions, 2, "L1", "L1-Q1", 1, ReadingQuestionTypes.MultipleChoice, "What do trees provide?", "2", 1);
        WriteQuestionSample(questions, 3, "L1", "L1-Q2", 2, ReadingQuestionTypes.TrueFalse, "Trees help reduce air pollution.", "2", 1);
        WriteQuestionSample(questions, 4, "L1", "L1-Q3", 3, ReadingQuestionTypes.GapFill, "They provide _____ and help reduce air pollution.", "2", 1);
        WriteQuestionSample(questions, 5, "L1", "L1-Q4", 4, ReadingQuestionTypes.Matching, "Match each English word with its Vietnamese meaning.", "1", 1);
        WriteQuestionSample(questions, 6, "L1", "L1-Q5", 5, ReadingQuestionTypes.ShortAnswer, "In one short phrase, what gas do trees provide?", "2", 1);
        ApplyDataSheetLayout(questions, freezeRows: 1, wrapColumns: [5]);
        ApplyListValidation(questions.Range(2, 4, 200, 4), ReadingQuestionTypes.All);

        var answers = workbook.AddWorksheet("Answers");
        WriteHeader(answers, "Question_ID", "Option_ID", "Option_Content", "Is_Correct", "Explanation", "Evidence_Text", "Evidence_Paragraph_ID", "Paraphrase", "Match_Key", "Match_Side");
        WriteAnswerSample(answers, 2, "L1-Q1", "A", "Oxygen", "TRUE", "Paragraph 2 mentions oxygen.", "They provide oxygen and help reduce air pollution.", "2", "Trees give oxygen.", null, null);
        WriteAnswerSample(answers, 3, "L1-Q1", "B", "Plastic", "FALSE", null, null, null, null, null, null);
        WriteAnswerSample(answers, 4, "L1-Q1", "C", "Noise", "FALSE", null, null, null, null, null, null);
        WriteAnswerSample(answers, 5, "L1-Q2", "TRUE", "True", "TRUE", "Paragraph 2 states trees help reduce air pollution.", "They provide oxygen and help reduce air pollution.", "2", null, null, null);
        WriteAnswerSample(answers, 6, "L1-Q2", "FALSE", "False", "FALSE", null, null, null, null, null, null);
        WriteAnswerSample(answers, 7, "L1-Q3", "ANSWER", "oxygen", "TRUE", "Fill the blank with the gas trees provide.", "They provide oxygen and help reduce air pollution.", "2", null, null, null);
        WriteAnswerSample(answers, 8, "L1-Q4", "Left1", "Environment", "FALSE", "Match words from paragraph 1.", null, "1", null, "p1", "Left");
        WriteAnswerSample(answers, 9, "L1-Q4", "Right1", "Môi trường", "FALSE", null, null, null, null, "p1", "Right");
        WriteAnswerSample(answers, 10, "L1-Q4", "Left2", "Trees", "FALSE", null, null, null, null, "p2", "Left");
        WriteAnswerSample(answers, 11, "L1-Q4", "Right2", "Cây cối", "FALSE", null, null, null, null, "p2", "Right");
        WriteAnswerSample(answers, 12, "L1-Q5", "ANSWER", "oxygen", "TRUE", "Accept the gas name from paragraph 2.", "They provide oxygen and help reduce air pollution.", "2", "Trees provide oxygen.", null, null);
        ApplyDataSheetLayout(answers, freezeRows: 1, wrapColumns: [3, 5, 6, 8]);
        ApplyListValidation(answers.Range(2, 4, 500, 4), ["TRUE", "FALSE"]);
        ApplyListValidation(answers.Range(2, 10, 500, 10), ["Left", "Right"]);

        var vocab = workbook.AddWorksheet("Vocabulary");
        WriteHeader(vocab, "ReadingCode", "Word", "Pronunciation", "PartOfSpeech", "Meaning", "Example", "ParagraphNo");
        vocab.Cell(2, 1).Value = "L1";
        vocab.Cell(2, 2).Value = "Environment";
        vocab.Cell(2, 3).Value = "/ɪnˈvaɪrənmənt/";
        vocab.Cell(2, 4).Value = "Noun";
        vocab.Cell(2, 5).Value = "Môi trường";
        vocab.Cell(2, 6).Value = "Protect the environment.";
        vocab.Cell(2, 7).Value = 1;
        vocab.Cell(3, 1).Value = "L1";
        vocab.Cell(3, 2).Value = "Oxygen";
        vocab.Cell(3, 3).Value = "/ˈɒksɪdʒən/";
        vocab.Cell(3, 4).Value = "Noun";
        vocab.Cell(3, 5).Value = "Oxy";
        vocab.Cell(3, 6).Value = "Trees provide oxygen.";
        vocab.Cell(3, 7).Value = 2;
        ApplyDataSheetLayout(vocab, freezeRows: 1, wrapColumns: [5, 6]);

        workbook.Worksheet("Guide").Position = 1;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void BuildGuideSheet(XLWorkbook workbook)
    {
        var guide = workbook.AddWorksheet("Guide");
        WriteHeader(guide, "Topic", "Instruction");

        var rows = new (string Topic, string Instruction)[]
        {
            ("Template", "TCT Reading Excel Template v2 — keep sheet names and header row exactly."),
            ("Sheets", "Reading_Info, Passage, Questions, Answers, Vocabulary (+ this Guide)."),
            ("CEFR", string.Join(", ", CefrLevels.All)),
            ("Question types", string.Join(", ", ReadingQuestionTypes.All)),
            ("Learner labels", "MultipleChoice=Trắc nghiệm; TrueFalse=Đúng/Sai; GapFill=Điền khuyết; Matching=Ghép cặp; ShortAnswer=Trả lời ngắn"),
            ("Import result", "Import creates Draft only. Students see Published after Admin review."),
            ("ReadingCode", "Excel ReadingCode is a temporary join key only (e.g. L1, L2). Unique per reading in the file. Import always assigns RD-{CEFR}-NNN."),
            ("Word count", "Count Passage body words only (not Title/Questions/Answers/Vocabulary). Soft targets: A1 80–120; A2 120–180; B1 200–300; B2 300–450; C1 450–600; C2 550–750. See docs/content/reading-content-authoring-guidelines.md."),
            ("Duration", "Suggested Duration (minutes, midpoint): A1=6; A2=8; B1=12; B2=18; C1=22; C2=28. Difficulty is not length alone."),
            ("Partial import", "Valid readings are saved as Draft. Identical readings and failed rows are skipped. The final code is assigned on confirmation."),
            ("Vocabulary", "Optional. Meaning = Vietnamese meaning (required when a vocab row exists)."),
            ("ImageUrl", "Optional: /images/..., /uploads/..., or https://..."),
            ("Media ZIP", "Optional ZIP at Import: root files {file ReadingCode}.jpg|png|webp|gif (ZIP wins over ImageUrl)."),
            ("MultipleChoice", "Answers: Option_ID A/B/C…; one row Is_Correct=TRUE."),
            ("TrueFalse", "Answers: Option_ID TRUE/FALSE (or True/False labels); one Is_Correct=TRUE."),
            ("GapFill", "Answers: one row Option_ID=ANSWER, Is_Correct=TRUE, Option_Content=accepted text."),
            ("Matching", "Answers: pairs share Match_Key; Match_Side Left|Right; Is_Correct usually FALSE."),
            ("ShortAnswer", "Answers: one row Option_ID=ANSWER, Is_Correct=TRUE, Option_Content=accepted text."),
            ("Evidence", "Evidence_Paragraph_ID uses Passage ParagraphNo (not a database id)."),
            ("Sample", "Sheet data for L1 is a complete sample with all five question types — replace or extend for real content.")
        };

        for (var i = 0; i < rows.Length; i++)
        {
            guide.Cell(i + 2, 1).Value = rows[i].Topic;
            guide.Cell(i + 2, 2).Value = rows[i].Instruction;
        }

        guide.Column(1).Width = 18;
        guide.Column(2).Width = 96;
        guide.Column(2).Style.Alignment.WrapText = true;
        guide.SheetView.FreezeRows(1);
        guide.Rows(2, rows.Length + 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
    }

    private static void WriteQuestionSample(
        IXLWorksheet sheet, int row, string readingCode, string questionId, int order, string type, string content, string paragraphRef, int points)
    {
        sheet.Cell(row, 1).Value = questionId;
        sheet.Cell(row, 2).Value = readingCode;
        sheet.Cell(row, 3).Value = order;
        sheet.Cell(row, 4).Value = type;
        sheet.Cell(row, 5).Value = content;
        sheet.Cell(row, 6).Value = paragraphRef;
        sheet.Cell(row, 7).Value = points;
    }

    private static void WriteAnswerSample(
        IXLWorksheet sheet,
        int row,
        string questionId,
        string optionId,
        string optionContent,
        string isCorrect,
        string? explanation,
        string? evidenceText,
        string? evidenceParagraphId,
        string? paraphrase,
        string? matchKey,
        string? matchSide)
    {
        sheet.Cell(row, 1).Value = questionId;
        sheet.Cell(row, 2).Value = optionId;
        sheet.Cell(row, 3).Value = optionContent;
        sheet.Cell(row, 4).Value = isCorrect;
        if (explanation is not null) sheet.Cell(row, 5).Value = explanation;
        if (evidenceText is not null) sheet.Cell(row, 6).Value = evidenceText;
        if (evidenceParagraphId is not null) sheet.Cell(row, 7).Value = evidenceParagraphId;
        if (paraphrase is not null) sheet.Cell(row, 8).Value = paraphrase;
        if (matchKey is not null) sheet.Cell(row, 9).Value = matchKey;
        if (matchSide is not null) sheet.Cell(row, 10).Value = matchSide;
    }

    private static void ApplyDataSheetLayout(IXLWorksheet sheet, int freezeRows, int[]? wrapColumns = null)
    {
        sheet.SheetView.FreezeRows(freezeRows);
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF5");
        sheet.Row(1).Style.Font.Bold = true;
        var lastCol = sheet.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 1;
        for (var col = 1; col <= lastCol; col++)
        {
            var header = sheet.Cell(1, col).GetString();
            sheet.Column(col).Width = header switch
            {
                "Content" or "Question_Content" or "Explanation" or "Evidence_Text" or "Paraphrase" or "Description" or "Example" => 36,
                "Option_Content" or "Meaning" or "Title" or "PassageTitle" => 28,
                "ReadingCode" or "Question_ID" or "Option_ID" or "CEFR" or "Match_Key" or "Match_Side" => 14,
                _ => 16
            };
        }

        if (wrapColumns is null) return;
        foreach (var col in wrapColumns)
            sheet.Column(col).Style.Alignment.WrapText = true;
    }

    private static void ApplyListValidation(IXLRange range, IReadOnlyList<string> options)
    {
        var list = $"\"{string.Join(",", options)}\"";
        range.CreateDataValidation().List(list, true);
    }

    public async Task<byte[]> ExportReadingAsync(int passageId, CancellationToken ct = default)
    {
        var passage = await _db.ReadingPassages.AsNoTracking()
            .Include(p => p.Paragraphs)
            .Include(p => p.Questions).ThenInclude(q => q.Options)
            .Include(p => p.VocabularyItems).ThenInclude(v => v.Paragraph)
            .FirstOrDefaultAsync(p => p.Id == passageId, ct) ?? throw new ReadingNotFoundException();

        var readingKey = passage.ExternalReadingKey ?? $"RD-{passage.Id}";
        using var workbook = new XLWorkbook();

        var info = workbook.AddWorksheet("Reading_Info");
        WriteHeader(info, "ReadingCode", "Title", "CEFR", "Topic", "Duration", "Description", "ImageUrl");
        info.Cell(2, 1).Value = readingKey;
        info.Cell(2, 2).Value = passage.Title;
        info.Cell(2, 3).Value = passage.Level;
        info.Cell(2, 4).Value = passage.Topic;
        info.Cell(2, 5).Value = passage.EstimatedTimeMinutes;
        info.Cell(2, 6).Value = passage.Description;
        info.Cell(2, 7).Value = passage.ImageUrl;

        var passageSheet = workbook.AddWorksheet("Passage");
        WriteHeader(passageSheet, "ReadingCode", "PassageTitle", "ParagraphNo", "Content");
        var pRows = passage.Paragraphs.OrderBy(p => p.ParagraphOrder).ToList();
        if (pRows.Count == 0)
        {
            passageSheet.Cell(2, 1).Value = readingKey;
            passageSheet.Cell(2, 2).Value = passage.Title;
            passageSheet.Cell(2, 3).Value = 1;
            passageSheet.Cell(2, 4).Value = passage.Content;
        }
        else
        {
            for (var i = 0; i < pRows.Count; i++)
            {
                var row = i + 2;
                passageSheet.Cell(row, 1).Value = readingKey;
                passageSheet.Cell(row, 2).Value = passage.Title;
                passageSheet.Cell(row, 3).Value = pRows[i].ParagraphOrder;
                passageSheet.Cell(row, 4).Value = pRows[i].ParagraphContent;
            }
        }

        var questions = workbook.AddWorksheet("Questions");
        WriteHeader(questions, "Question_ID", "ReadingCode", "Question_Order", "Question_Type", "Question_Content", "Paragraph_Reference", "Points");
        var answers = workbook.AddWorksheet("Answers");
        WriteHeader(answers, "Question_ID", "Option_ID", "Option_Content", "Is_Correct", "Explanation", "Evidence_Text", "Evidence_Paragraph_ID", "Paraphrase", "Match_Key", "Match_Side");
        var answerRow = 2;
        var qList = passage.Questions.OrderBy(q => q.OrderIndex).ToList();
        for (var i = 0; i < qList.Count; i++)
        {
            var q = qList[i];
            var qKey = q.ExternalQuestionKey ?? $"Q{q.Id}";
            var row = i + 2;
            questions.Cell(row, 1).Value = qKey;
            questions.Cell(row, 2).Value = readingKey;
            questions.Cell(row, 3).Value = q.OrderIndex;
            questions.Cell(row, 4).Value = q.QuestionType;
            questions.Cell(row, 5).Value = q.QuestionText;
            questions.Cell(row, 6).Value = q.ParagraphReference;
            questions.Cell(row, 7).Value = q.Points;

            if (q.Options.Count == 0 && !string.IsNullOrWhiteSpace(q.CorrectAnswerText))
            {
                answers.Cell(answerRow, 1).Value = qKey;
                answers.Cell(answerRow, 2).Value = "ANSWER";
                answers.Cell(answerRow, 3).Value = q.CorrectAnswerText;
                answers.Cell(answerRow, 4).Value = "TRUE";
                answers.Cell(answerRow, 5).Value = q.Explanation;
                answers.Cell(answerRow, 6).Value = q.EvidenceText;
                answers.Cell(answerRow, 7).Value = passage.Paragraphs.FirstOrDefault(p => p.Id == q.EvidenceParagraphId)?.ParagraphOrder.ToString();
                answers.Cell(answerRow, 8).Value = q.Paraphrase;
                answerRow++;
            }
            else
            {
                foreach (var opt in q.Options.OrderBy(o => o.OptionOrder))
                {
                    answers.Cell(answerRow, 1).Value = qKey;
                    answers.Cell(answerRow, 2).Value = opt.ExternalOptionKey ?? opt.Id.ToString();
                    answers.Cell(answerRow, 3).Value = opt.OptionText;
                    answers.Cell(answerRow, 4).Value = opt.IsCorrect ? "TRUE" : "FALSE";
                    answers.Cell(answerRow, 5).Value = q.Explanation;
                    answers.Cell(answerRow, 6).Value = q.EvidenceText;
                    answers.Cell(answerRow, 7).Value = passage.Paragraphs.FirstOrDefault(p => p.Id == q.EvidenceParagraphId)?.ParagraphOrder.ToString();
                    answers.Cell(answerRow, 8).Value = q.Paraphrase;
                    answers.Cell(answerRow, 9).Value = opt.MatchKey;
                    answers.Cell(answerRow, 10).Value = opt.MatchSide;
                    answerRow++;
                }
            }
        }

        var vocab = workbook.AddWorksheet("Vocabulary");
        WriteHeader(vocab, "ReadingCode", "Word", "Pronunciation", "PartOfSpeech", "Meaning", "Example", "ParagraphNo");
        var vList = passage.VocabularyItems.ToList();
        for (var i = 0; i < vList.Count; i++)
        {
            var v = vList[i];
            var row = i + 2;
            vocab.Cell(row, 1).Value = readingKey;
            vocab.Cell(row, 2).Value = v.Word;
            vocab.Cell(row, 3).Value = v.Ipa;
            vocab.Cell(row, 4).Value = v.PartOfSpeech;
            vocab.Cell(row, 5).Value = v.VietnameseMeaning;
            vocab.Cell(row, 6).Value = v.Example;
            vocab.Cell(row, 7).Value = v.Paragraph?.ParagraphOrder ?? v.ParagraphId;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ReadingExcelPreviewResult> ParseAndValidateAsync(Stream stream, CancellationToken ct = default)
    {
        var result = new ReadingExcelPreviewResult();
        try
        {
            ValidateWorkbookArchive(stream, ct);
            using var workbook = new XLWorkbook(stream);
            foreach (var sheet in RequiredSheets)
            {
                if (!workbook.Worksheets.Any(ws => ws.Name.Equals(sheet, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Errors.Add(Err(null, sheet, 0, "", null,
                        "Missing required sheet.",
                        "Add the sheet with the exact name from the template."));
                }
            }

            if (result.Errors.Count > 0) return result;

            var infoSheet = workbook.Worksheet("Reading_Info");
            var passageSheet = workbook.Worksheet("Passage");
            var questionsSheet = workbook.Worksheet("Questions");
            var answersSheet = workbook.Worksheet("Answers");
            var vocabSheet = workbook.Worksheet("Vocabulary");

            ValidateRequiredHeaders(infoSheet, "Reading_Info",
                ["ReadingCode", "Title", "CEFR", "Topic", "Duration"], result.Errors);
            ValidateRequiredHeaders(passageSheet, "Passage",
                ["ReadingCode", "PassageTitle", "ParagraphNo", "Content"], result.Errors);
            ValidateRequiredHeaders(questionsSheet, "Questions",
                ["Question_ID", "ReadingCode", "Question_Order", "Question_Type", "Question_Content"], result.Errors);
            ValidateRequiredHeaders(answersSheet, "Answers",
                ["Question_ID"], result.Errors);
            ValidateRequiredHeaders(vocabSheet, "Vocabulary",
                ["ReadingCode", "Word", "Meaning"], result.Errors);

            if (result.Errors.Count > 0) return result;

            var infoRows = DataRows(infoSheet).ToList();
            if (infoRows.Count == 0)
            {
                result.Errors.Add(Err(null, "Reading_Info", 2, "ReadingCode", null,
                    "Missing reading info row.",
                    "Add at least one data row under the header."));
                return result;
            }

            var occupiedKeys = (await _db.ReadingPassages.AsNoTracking()
                .Where(p => p.ExternalReadingKey != null)
                .Select(p => p.ExternalReadingKey!)
                .ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var fileCodesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var answersByQuestion = BuildAnswerIndex(answersSheet);
            var passagesByCode = GroupSheetRows(passageSheet, "ReadingCode");
            var questionsByCode = GroupSheetRows(questionsSheet, "ReadingCode");
            var vocabByCode = GroupSheetRows(vocabSheet, "ReadingCode");

            foreach (var infoRow in infoRows)
            {
                var fileReadingCode = Cell(infoSheet, infoRow, "ReadingCode")?.Trim() ?? "";
                var title = Cell(infoSheet, infoRow, "Title");
                var cefr = Cell(infoSheet, infoRow, "CEFR");
                var topic = Cell(infoSheet, infoRow, "Topic");
                var durationRaw = Cell(infoSheet, infoRow, "Duration");
                var description = Cell(infoSheet, infoRow, "Description");
                var imageUrlRaw = Cell(infoSheet, infoRow, "ImageUrl");

                var readingErrors = new List<ReadingExcelError>();

                if (string.IsNullOrWhiteSpace(fileReadingCode))
                {
                    readingErrors.Add(Err(null, "Reading_Info", infoRow, "ReadingCode", fileReadingCode,
                        "ReadingCode is required.",
                        "Enter a temporary join key unique in this file (e.g. L1). The system assigns RD-{CEFR}-NNN on import."));
                    result.FailedReadings.Add(new ReadingExcelFailedReading
                    {
                        ReadingCode = "(blank)",
                        Title = title,
                        Errors = readingErrors
                    });
                    result.Errors.AddRange(readingErrors);
                    continue;
                }

                if (!fileCodesSeen.Add(fileReadingCode))
                {
                    readingErrors.Add(Err(fileReadingCode, "Reading_Info", infoRow, "ReadingCode", fileReadingCode,
                        "ReadingCode is duplicated in this file.",
                        "Use a unique temporary join key per reading in the workbook (e.g. L1, L2)."));
                    result.FailedReadings.Add(new ReadingExcelFailedReading
                    {
                        ReadingCode = fileReadingCode,
                        Title = title,
                        Errors = readingErrors
                    });
                    result.Errors.AddRange(readingErrors);
                    continue;
                }

                // Group Passage/Questions/Vocab by the temporary code written in the file.
                var readingCode = fileReadingCode;

                if (string.IsNullOrWhiteSpace(title))
                {
                    readingErrors.Add(Err(fileReadingCode, "Reading_Info", infoRow, "Title", title,
                        "Title is required.",
                        "Fill Title."));
                }

                if (!CefrLevels.IsValid(cefr))
                {
                    readingErrors.Add(Err(fileReadingCode, "Reading_Info", infoRow, "CEFR", cefr,
                        "CEFR must be A1, A2, B1, B2, C1, or C2.",
                        "Use a valid CEFR code."));
                }

                if (string.IsNullOrWhiteSpace(topic))
                {
                    readingErrors.Add(Err(fileReadingCode, "Reading_Info", infoRow, "Topic", topic,
                        "Topic is required.",
                        "Fill Topic."));
                }

                if (!int.TryParse(durationRaw, out var duration) || duration <= 0)
                {
                    readingErrors.Add(Err(fileReadingCode, "Reading_Info", infoRow, "Duration", durationRaw,
                        "Duration must be a positive integer (minutes).",
                        "Enter a whole number greater than 0."));
                }

                var level = CefrLevels.IsValid(cefr) ? CefrLevels.Normalize(cefr) : "A1";
                string allocatedCode;
                if (CefrLevels.IsValid(cefr))
                {
                    allocatedCode = AllocateNextOfficialReadingCode(level, occupiedKeys);
                    occupiedKeys.Add(allocatedCode);
                    result.Warnings.Add(new ReadingExcelWarning
                    {
                        ReadingCode = allocatedCode,
                        Sheet = "Reading_Info",
                        Row = infoRow,
                        Column = "ReadingCode",
                        Message = $"File ReadingCode '{fileReadingCode}' assigned system code '{allocatedCode}'. Excel codes are join keys only; the system always assigns RD-{{CEFR}}-NNN."
                    });
                }
                else
                {
                    allocatedCode = fileReadingCode;
                }

                string? imageUrl = null;
                if (!string.IsNullOrWhiteSpace(imageUrlRaw))
                {
                    imageUrl = ReadingAdminService.NormalizeImageUrl(imageUrlRaw);
                    if (imageUrl == null)
                    {
                        result.Warnings.Add(new ReadingExcelWarning
                        {
                            ReadingCode = allocatedCode,
                            Sheet = "Reading_Info",
                            Row = infoRow,
                            Column = "ImageUrl",
                            Message = "ImageUrl ignored — use /images/..., /uploads/..., or http(s)://..."
                        });
                    }
                }

                var preview = new ReadingEditorViewModel
                {
                    ExternalReadingKey = allocatedCode,
                    ImportFileReadingCode = fileReadingCode,
                    Title = title?.Trim() ?? "",
                    Description = description,
                    Topic = topic?.Trim(),
                    Level = level,
                    EstimatedTimeMinutes = duration > 0 ? duration : null,
                    ImageUrl = imageUrl
                };

                ParsePassageRows(readingCode, preview.Title, passagesByCode.GetValueOrDefault(readingCode), passageSheet, preview, readingErrors);
                ParseQuestionRows(readingCode, questionsByCode.GetValueOrDefault(readingCode), questionsSheet, answersByQuestion, answersSheet, preview, readingErrors);
                ParseVocabularyRows(readingCode, vocabByCode.GetValueOrDefault(readingCode), vocabSheet, preview, readingErrors, result.Warnings);

                if (preview.Questions.Count == 0 && readingErrors.All(e => e.Sheet != "Questions"))
                {
                    readingErrors.Add(Err(fileReadingCode, "Questions", 0, "Question_ID", null,
                        "At least one question is required.",
                        "Add question rows for this ReadingCode."));
                }

                foreach (var q in preview.Questions)
                {
                    var hasAnswer = q.Options.Count > 0 || !string.IsNullOrWhiteSpace(q.CorrectAnswerText);
                    if (!hasAnswer)
                    {
                        readingErrors.Add(Err(fileReadingCode, "Answers", 0, "Question_ID", q.ExternalQuestionKey,
                            $"Question {q.ExternalQuestionKey} has no valid answer.",
                            "Add Answers rows for this Question_ID."));
                    }

                    if (q.QuestionType == ReadingQuestionTypes.Matching)
                    {
                        foreach (var msg in ReadingAdminService.ValidateMatchingEditorOptions(q.OrderIndex, q.Options))
                        {
                            readingErrors.Add(Err(fileReadingCode, "Answers", 0, "Match_Key", q.ExternalQuestionKey,
                                msg,
                                "Set Match_Key and Match_Side (Left|Right) for each Matching option pair."));
                        }
                    }
                }

                if (readingErrors.Count > 0)
                {
                    result.FailedReadings.Add(new ReadingExcelFailedReading
                    {
                        ReadingCode = allocatedCode,
                        Title = preview.Title,
                        Errors = readingErrors
                    });
                    result.Errors.AddRange(readingErrors);
                }
                else
                {
                    result.ValidReadings.Add(preview);
                }
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Errors.Add(Err(null, "", 0, "", null,
                "Invalid Excel file: " + ex.Message,
                "Upload a valid .xlsx created from the official template."));
            return result;
        }
    }

    public byte[] BuildErrorReport(IEnumerable<ReadingExcelError> errors)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Errors");
        WriteHeader(sheet, "ReadingCode", "Sheet", "Row", "Column", "Value", "Message", "FixHint");
        var row = 2;
        foreach (var err in errors)
        {
            sheet.Cell(row, 1).Value = err.ReadingCode;
            sheet.Cell(row, 2).Value = err.Sheet;
            sheet.Cell(row, 3).Value = err.Row;
            sheet.Cell(row, 4).Value = err.Column;
            sheet.Cell(row, 5).Value = err.Value;
            sheet.Cell(row, 6).Value = err.Message;
            sheet.Cell(row, 7).Value = err.FixHint;
            row++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ReadingExcelImportConfirmResult> ConfirmImportAsync(
        IReadOnlyList<ReadingEditorViewModel> models,
        int actorUserId,
        CancellationToken ct = default)
    {
        var result = new ReadingExcelImportConfirmResult();
        var hasWorkbookReadings = models.Any(model => !string.IsNullOrWhiteSpace(model.ImportFileReadingCode));

        // Keep content checks, code allocation and inserts under the same connection-scoped
        // MariaDB lock used by catalog imports.
        await using var catalogLock = hasWorkbookReadings && _db.Database.IsMySql()
            ? await SharedDataDatabaseLock.AcquireAsync(_db, ct)
            : null;

        var occupiedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hasWorkbookReadings)
        {
            var storedCodes = await _db.ReadingPassages.AsNoTracking()
                .Where(p => p.ExternalReadingKey != null)
                .Select(p => p.ExternalReadingKey!)
                .ToListAsync(ct);
            occupiedCodes.UnionWith(storedCodes);
        }

        foreach (var model in models)
        {
            var code = model.ExternalReadingKey?.Trim() ?? "";
            var item = new ReadingExcelImportItemResult
            {
                ReadingCode = code,
                Title = model.Title
            };

            if (string.IsNullOrWhiteSpace(code))
            {
                item.Status = "Skipped";
                item.Message = "Missing ReadingCode.";
                result.Items.Add(item);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(model.ImportFileReadingCode))
            {
                var existing = await FindMatchingReadingAsync(model, ct);
                if (existing != null)
                {
                    item.ReadingCode = existing.ExternalReadingKey ?? $"RD-{existing.Id}";
                    item.PassageId = existing.Id;
                    item.Status = "Skipped";
                    item.Message = "An identical reading already exists; no draft was created.";
                    result.Items.Add(item);
                    continue;
                }

                // Preview codes are provisional. Another import can take the code before confirmation.
                code = AllocateNextOfficialReadingCode(CefrLevels.Normalize(model.Level), occupiedCodes);
                item.ReadingCode = code;
                model.ExternalReadingKey = code;
            }
            else if (await _db.ReadingPassages.AsNoTracking()
                .AnyAsync(p => p.ExternalReadingKey != null && p.ExternalReadingKey == code, ct))
            {
                item.Status = "Skipped";
                item.Message = "ReadingCode already exists; not overwritten.";
                result.Items.Add(item);
                continue;
            }

            var useTx = _db.Database.IsRelational();
            await using var tx = useTx ? await _db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                var created = await _admin.CreateDraftAsync(model, actorUserId, ct);
                if (tx != null) await tx.CommitAsync(ct);
                occupiedCodes.Add(code);
                item.Status = "Success";
                item.PassageId = created.Id;
                item.Message = "Imported as Draft.";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                item.Status = "Failed";
                item.Message = ex.Message;
            }

            result.Items.Add(item);
        }

        return result;
    }

    private async Task<ReadingPassage?> FindMatchingReadingAsync(ReadingEditorViewModel model, CancellationToken ct)
    {
        var title = model.Title.Trim();
        var level = CefrLevels.Normalize(model.Level);
        var shape = ReadingImportContent.Serialize(model);
        var candidates = await _db.ReadingPassages.AsNoTracking()
            .Where(p => p.Title == title && p.Level == level)
            .Include(p => p.Paragraphs)
            .Include(p => p.Questions).ThenInclude(q => q.Options)
            .Include(p => p.VocabularyItems).ThenInclude(v => v.Paragraph)
            .AsSplitQuery()
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        return candidates.FirstOrDefault(p =>
            ReadingImportContent.Serialize(ReadingEditorMapper.FromPassage(p)) == shape);
    }

    private static void ParsePassageRows(
        string readingCode,
        string expectedTitle,
        List<int>? rows,
        IXLWorksheet sheet,
        ReadingEditorViewModel preview,
        List<ReadingExcelError> errors)
    {
        if (rows == null || rows.Count == 0)
        {
            errors.Add(Err(readingCode, "Passage", 0, "ReadingCode", readingCode,
                "No Passage rows for this ReadingCode.",
                "Add at least one paragraph row."));
            return;
        }

        var seenOrders = new HashSet<int>();
        string? firstTitle = null;
        foreach (var row in rows)
        {
            var passageTitle = Cell(sheet, row, "PassageTitle");
            var orderRaw = Cell(sheet, row, "ParagraphNo");
            var content = Cell(sheet, row, "Content");

            if (string.IsNullOrWhiteSpace(passageTitle))
            {
                errors.Add(Err(readingCode, "Passage", row, "PassageTitle", passageTitle,
                    "PassageTitle is required.",
                    "Fill PassageTitle."));
            }
            else
            {
                firstTitle ??= passageTitle.Trim();
                if (!string.Equals(firstTitle, passageTitle.Trim(), StringComparison.Ordinal))
                {
                    errors.Add(Err(readingCode, "Passage", row, "PassageTitle", passageTitle,
                        "PassageTitle must be consistent within the same ReadingCode.",
                        "Use the same PassageTitle for all paragraphs of this reading."));
                }

                if (!string.IsNullOrWhiteSpace(expectedTitle) &&
                    !string.Equals(expectedTitle, passageTitle.Trim(), StringComparison.Ordinal))
                {
                    errors.Add(Err(readingCode, "Passage", row, "PassageTitle", passageTitle,
                        "PassageTitle must match Reading_Info Title.",
                        "Align PassageTitle with Title."));
                }
            }

            if (!int.TryParse(orderRaw, out var order) || order <= 0)
            {
                errors.Add(Err(readingCode, "Passage", row, "ParagraphNo", orderRaw,
                    "ParagraphNo must be a positive integer.",
                    "Use 1, 2, 3, ..."));
                continue;
            }

            if (!seenOrders.Add(order))
            {
                errors.Add(Err(readingCode, "Passage", row, "ParagraphNo", orderRaw,
                    "Duplicate ParagraphNo within ReadingCode.",
                    "Each paragraph number must be unique."));
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                errors.Add(Err(readingCode, "Passage", row, "Content", content,
                    "Content is required.",
                    "Fill paragraph Content."));
                continue;
            }

            preview.Paragraphs.Add(new ReadingParagraphEditorViewModel
            {
                ExternalParagraphKey = $"P{order}",
                ParagraphOrder = order,
                ParagraphContent = content
            });
        }

        preview.Content = string.Join("\n\n",
            preview.Paragraphs.OrderBy(p => p.ParagraphOrder).Select(p => p.ParagraphContent));
    }

    private static void ParseQuestionRows(
        string readingCode,
        List<int>? rows,
        IXLWorksheet questionsSheet,
        Dictionary<string, List<int>> answersByQuestion,
        IXLWorksheet answersSheet,
        ReadingEditorViewModel preview,
        List<ReadingExcelError> errors)
    {
        if (rows == null) return;

        var questionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var qId = Cell(questionsSheet, row, "Question_ID");
            var qContent = Cell(questionsSheet, row, "Question_Content");
            var qType = Cell(questionsSheet, row, "Question_Type");
            var orderRaw = Cell(questionsSheet, row, "Question_Order");
            var pointsRaw = Cell(questionsSheet, row, "Points");

            if (string.IsNullOrWhiteSpace(qId))
            {
                errors.Add(Err(readingCode, "Questions", row, "Question_ID", qId,
                    "Question_ID is required.",
                    "Fill Question_ID."));
                continue;
            }

            if (!questionIds.Add(qId))
            {
                errors.Add(Err(readingCode, "Questions", row, "Question_ID", qId,
                    "Duplicate Question_ID within ReadingCode.",
                    "Use unique Question_ID values."));
            }

            if (string.IsNullOrWhiteSpace(qContent))
            {
                errors.Add(Err(readingCode, "Questions", row, "Question_Content", qContent,
                    "Question_Content is required.",
                    "Fill Question_Content."));
            }

            if (!ReadingQuestionTypes.IsValid(qType))
            {
                errors.Add(Err(readingCode, "Questions", row, "Question_Type", qType,
                    "Question_Type is invalid.",
                    "Use: " + string.Join(", ", ReadingQuestionTypes.All)));
            }

            var question = new ReadingQuestionEditorViewModel
            {
                ExternalQuestionKey = qId,
                OrderIndex = int.TryParse(orderRaw, out var qo) ? qo : preview.Questions.Count + 1,
                QuestionType = ReadingQuestionTypes.IsValid(qType) ? qType! : ReadingQuestionTypes.MultipleChoice,
                QuestionText = qContent ?? "",
                ParagraphReference = Cell(questionsSheet, row, "Paragraph_Reference"),
                Points = int.TryParse(pointsRaw, out var pts) && pts > 0 ? pts : 1
            };

            if (answersByQuestion.TryGetValue(qId, out var answerRows))
            {
                foreach (var aRow in answerRows)
                {
                    var optionContent = Cell(answersSheet, aRow, "Option_Content");
                    var isCorrect = IsTrue(Cell(answersSheet, aRow, "Is_Correct"));
                    question.Explanation ??= Cell(answersSheet, aRow, "Explanation");
                    question.EvidenceText ??= Cell(answersSheet, aRow, "Evidence_Text");
                    question.Paraphrase ??= Cell(answersSheet, aRow, "Paraphrase");
                    var evidenceOrder = Cell(answersSheet, aRow, "Evidence_Paragraph_ID");
                    if (!string.IsNullOrWhiteSpace(evidenceOrder))
                    {
                        if (!int.TryParse(evidenceOrder, out var paragraphNo)
                            || !preview.Paragraphs.Any(p => p.ParagraphOrder == paragraphNo)
                            || (question.EvidenceParagraphOrder.HasValue && question.EvidenceParagraphOrder != paragraphNo))
                            errors.Add(Err(readingCode, "Answers", aRow, "Evidence_Paragraph_ID", evidenceOrder,
                                "Evidence paragraph is missing or inconsistent.", "Use one Passage ParagraphNo for this question."));
                        else question.EvidenceParagraphOrder = paragraphNo;
                    }

                    if (question.QuestionType is ReadingQuestionTypes.GapFill or ReadingQuestionTypes.ShortAnswer)
                    {
                        if (isCorrect || string.Equals(Cell(answersSheet, aRow, "Option_ID"), "ANSWER", StringComparison.OrdinalIgnoreCase))
                            question.CorrectAnswerText = optionContent;
                    }
                    else
                    {
                        var matchKey = Cell(answersSheet, aRow, "Match_Key");
                        var matchSide = Cell(answersSheet, aRow, "Match_Side");
                        question.Options.Add(new ReadingOptionEditorViewModel
                        {
                            ExternalOptionKey = Cell(answersSheet, aRow, "Option_ID"),
                            OptionText = optionContent ?? "",
                            IsCorrect = isCorrect,
                            MatchKey = string.IsNullOrWhiteSpace(matchKey) ? null : matchKey.Trim(),
                            MatchSide = string.IsNullOrWhiteSpace(matchSide) ? null : matchSide.Trim(),
                            OptionOrder = question.Options.Count + 1
                        });
                    }
                }
            }

            preview.Questions.Add(question);
        }
    }

    private static void ParseVocabularyRows(
        string readingCode,
        List<int>? rows,
        IXLWorksheet sheet,
        ReadingEditorViewModel preview,
        List<ReadingExcelError> errors,
        List<ReadingExcelWarning> warnings)
    {
        if (rows == null || rows.Count == 0) return;

        var paragraphOrders = preview.Paragraphs.Select(p => p.ParagraphOrder).ToHashSet();
        var seenWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var word = Cell(sheet, row, "Word");
            var meaning = Cell(sheet, row, "Meaning");
            var paragraphNoRaw = Cell(sheet, row, "ParagraphNo");

            if (string.IsNullOrWhiteSpace(word))
            {
                errors.Add(Err(readingCode, "Vocabulary", row, "Word", word,
                    "Word is required.",
                    "Fill Word or remove the empty row."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(meaning))
            {
                errors.Add(Err(readingCode, "Vocabulary", row, "Meaning", meaning,
                    "Meaning is required.",
                    "Fill Vietnamese Meaning."));
                continue;
            }

            if (!seenWords.Add(word.Trim()))
            {
                warnings.Add(new ReadingExcelWarning
                {
                    ReadingCode = readingCode,
                    Sheet = "Vocabulary",
                    Row = row,
                    Column = "Word",
                    Message = $"Duplicate word '{word.Trim()}' in the same reading (kept; not auto-deleted)."
                });
            }

            int? paragraphOrder = null;
            if (!string.IsNullOrWhiteSpace(paragraphNoRaw))
            {
                if (!int.TryParse(paragraphNoRaw, out var pNo) || pNo <= 0)
                {
                    errors.Add(Err(readingCode, "Vocabulary", row, "ParagraphNo", paragraphNoRaw,
                        "ParagraphNo must be a positive integer when provided.",
                        "Use a ParagraphNo that exists on the Passage sheet."));
                }
                else if (!paragraphOrders.Contains(pNo))
                {
                    errors.Add(Err(readingCode, "Vocabulary", row, "ParagraphNo", paragraphNoRaw,
                        "ParagraphNo does not exist in Passage for this ReadingCode.",
                        "Match ParagraphNo to a Passage row."));
                }
                else
                {
                    paragraphOrder = pNo;
                }
            }

            preview.Vocabulary.Add(new ReadingVocabularyEditorViewModel
            {
                Word = word.Trim(),
                Ipa = Cell(sheet, row, "Pronunciation"),
                PartOfSpeech = Cell(sheet, row, "PartOfSpeech"),
                VietnameseMeaning = meaning.Trim(),
                Example = Cell(sheet, row, "Example"),
                ParagraphOrder = paragraphOrder
            });
        }
    }

    private static Dictionary<string, List<int>> BuildAnswerIndex(IXLWorksheet answersSheet)
    {
        var map = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in DataRows(answersSheet))
        {
            var qId = Cell(answersSheet, row, "Question_ID");
            if (string.IsNullOrWhiteSpace(qId)) continue;
            if (!map.TryGetValue(qId, out var list))
            {
                list = new List<int>();
                map[qId] = list;
            }

            list.Add(row);
        }

        return map;
    }

    private static Dictionary<string, List<int>> GroupSheetRows(IXLWorksheet sheet, string codeColumn)
    {
        var map = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in DataRows(sheet))
        {
            var code = Cell(sheet, row, codeColumn);
            if (string.IsNullOrWhiteSpace(code)) continue;
            if (!map.TryGetValue(code, out var list))
            {
                list = new List<int>();
                map[code] = list;
            }

            list.Add(row);
        }

        return map;
    }

    private static void ValidateRequiredHeaders(IXLWorksheet sheet, string sheetName, string[] required, List<ReadingExcelError> errors)
    {
        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString().Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var col in required)
        {
            if (!headers.Contains(col))
            {
                errors.Add(Err(null, sheetName, 1, col, null,
                    $"Missing required column '{col}'.",
                    "Restore the column name from the official template."));
            }
        }
    }

    private static ReadingExcelError Err(
        string? readingCode, string sheet, int row, string column, string? value, string message, string fixHint) =>
        new()
        {
            ReadingCode = readingCode,
            Sheet = sheet,
            Row = row,
            Column = column,
            Value = value,
            Message = message,
            FixHint = fixHint
        };

    private static void WriteHeader(IXLWorksheet sheet, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;
    }

    /// <summary>
    /// Next official import key: RD-{CEFR}-NNN (3-digit), based on occupied DB + in-batch keys.
    /// Excel ReadingCode values are temporary join keys only.
    /// </summary>
    public static string AllocateNextOfficialReadingCode(string cefr, ISet<string> occupied)
    {
        var level = CefrLevels.Normalize(cefr);
        var prefix = $"RD-{level}-";
        var max = 0;
        foreach (var key in occupied)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var suffix = key[prefix.Length..];
            if (int.TryParse(suffix, out var n) && n > max)
                max = n;
        }

        return $"{prefix}{(max + 1).ToString("D3")}";
    }

    private static IEnumerable<int> DataRows(IXLWorksheet sheet)
    {
        var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= last; row++)
        {
            if (sheet.Row(row).IsEmpty()) continue;
            yield return row;
        }
    }

    private static void ValidateWorkbookArchive(Stream stream, CancellationToken ct)
    {
        if (!stream.CanSeek || stream.Length - stream.Position > 10 * 1024 * 1024)
            throw new InvalidDataException("Workbook exceeds compressed size limit (10MB).");
        var position = stream.Position;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            if (archive.Entries.Count > 512 || archive.Entries.Sum(e => (double)e.Length) > 64 * 1024 * 1024)
                throw new InvalidDataException("Workbook exceeds expanded size limit (64MB).");
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.Length > 16 * 1024 * 1024)
                    throw new InvalidDataException("Workbook entry exceeds expanded size limit (16MB).");
                if (!entry.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) || !entry.FullName.EndsWith(".xml", StringComparison.Ordinal)) continue;
                using var xml = XmlReader.Create(entry.Open(), new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 16 * 1024 * 1024, CloseInput = true });
                var cells = 0;
                while (xml.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    if (xml.NodeType != XmlNodeType.Element) continue;
                    if (xml.LocalName == "c" && ++cells > 50000)
                        throw new InvalidDataException("Workbook exceeds cell limit (50000 per sheet).");
                    if (xml.LocalName == "row" && int.TryParse(xml.GetAttribute("r"), out var row) && row > 10001)
                        throw new InvalidDataException("Workbook exceeds row limit (10000 data rows per sheet).");
                }
            }
        }
        stream.Position = position;
    }

    private static string? Cell(IXLWorksheet sheet, int row, string columnName)
    {
        var headerRow = sheet.Row(1);
        var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var col = 1; col <= lastCol; col++)
        {
            if (string.Equals(headerRow.Cell(col).GetString().Trim(), columnName, StringComparison.OrdinalIgnoreCase))
            {
                var value = sheet.Cell(row, col).GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
        }

        return null;
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase);
}
