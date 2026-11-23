using Microsoft.EntityFrameworkCore;
using Moq;
using TCTEnglish.Areas.Admin.ViewModels;
using TCTEnglish.Models.Reading;
using TCTEnglish.Services.Reading;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class ReadingModuleTests
{
    [Theory]
    [InlineData("A1", "A1", true)]
    [InlineData("B1", "A2", true)]
    [InlineData("A1", "B1", false)]
    [InlineData("C2", "C1", true)]
    public void CefrLevels_CanAccess_MatchesMatrix(string student, string passage, bool expected)
    {
        Assert.Equal(expected, CefrLevels.CanAccess(student, passage));
    }

    [Fact]
    public void ExcelTemplate_ContainsRequiredSheetsAndPromptColumns()
    {
        var db = CreateContext(nameof(ExcelTemplate_ContainsRequiredSheetsAndPromptColumns));
        var admin = CreateAdmin(db);
        var excel = new ReadingExcelService(db, admin);
        var bytes = excel.BuildTemplate();
        Assert.True(bytes.Length > 1000);

        using var stream = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        foreach (var sheet in new[] { "Reading_Info", "Passage", "Questions", "Answers", "Vocabulary", "Guide" })
            Assert.Contains(workbook.Worksheets, ws => ws.Name == sheet);

        Assert.Equal("Guide", workbook.Worksheet(1).Name);
        Assert.Equal("Topic", workbook.Worksheet("Guide").Cell(1, 1).GetString());
        Assert.Equal("Instruction", workbook.Worksheet("Guide").Cell(1, 2).GetString());

        Assert.Equal("ReadingCode", workbook.Worksheet("Reading_Info").Cell(1, 1).GetString());
        Assert.Equal("CEFR", workbook.Worksheet("Reading_Info").Cell(1, 3).GetString());
        Assert.Equal("ImageUrl", workbook.Worksheet("Reading_Info").Cell(1, 7).GetString());
        Assert.Equal("PassageTitle", workbook.Worksheet("Passage").Cell(1, 2).GetString());
        Assert.Equal("Meaning", workbook.Worksheet("Vocabulary").Cell(1, 5).GetString());
        Assert.Equal("ReadingCode", workbook.Worksheet("Questions").Cell(1, 2).GetString());
        Assert.Equal("Match_Key", workbook.Worksheet("Answers").Cell(1, 9).GetString());
        Assert.Equal("Match_Side", workbook.Worksheet("Answers").Cell(1, 10).GetString());

        var questionTypes = workbook.Worksheet("Questions").RowsUsed()
            .Skip(1)
            .Select(r => r.Cell(4).GetString())
            .ToHashSet(StringComparer.Ordinal);
        foreach (var type in ReadingQuestionTypes.All)
            Assert.Contains(type, questionTypes);

        Assert.Equal(1, workbook.Worksheet("Questions").SheetView.SplitRow);
        Assert.Equal(1, workbook.Worksheet("Answers").SheetView.SplitRow);
    }

    [Fact]
    public async Task ParseTemplate_YieldsValidDraftCandidate_WithParagraphsAndVocab()
    {
        var db = CreateContext(nameof(ParseTemplate_YieldsValidDraftCandidate_WithParagraphsAndVocab));
        var admin = CreateAdmin(db);
        var excel = new ReadingExcelService(db, admin);
        using var stream = new MemoryStream(excel.BuildTemplate());
        var preview = await excel.ParseAndValidateAsync(stream);

        Assert.True(preview.HasImportableReadings);
        Assert.Empty(preview.FailedReadings);
        var reading = Assert.Single(preview.ValidReadings);
        Assert.Equal("RD-A1-001", reading.ExternalReadingKey);
        Assert.Equal("L1", reading.ImportFileReadingCode);
        Assert.Equal(2, reading.Paragraphs.Count);
        Assert.Equal(5, reading.Questions.Count);
        Assert.Contains(reading.Questions, q => q.QuestionType == ReadingQuestionTypes.MultipleChoice);
        Assert.Contains(reading.Questions, q => q.QuestionType == ReadingQuestionTypes.TrueFalse);
        Assert.Contains(reading.Questions, q => q.QuestionType == ReadingQuestionTypes.GapFill);
        Assert.Contains(reading.Questions, q => q.QuestionType == ReadingQuestionTypes.ShortAnswer);
        var matching = Assert.Single(reading.Questions, q => q.QuestionType == ReadingQuestionTypes.Matching);
        Assert.Contains(matching.Options, o => o.MatchKey == "p1" && o.MatchSide == "Left");
        Assert.Contains(matching.Options, o => o.MatchKey == "p1" && o.MatchSide == "Right");
        Assert.Contains(matching.Options, o => o.MatchKey == "p2" && o.MatchSide == "Left");
        Assert.Contains(matching.Options, o => o.MatchKey == "p2" && o.MatchSide == "Right");
        Assert.Equal(2, reading.Vocabulary.Count);
        Assert.Equal("Môi trường", reading.Vocabulary[0].VietnameseMeaning);
        Assert.Equal(1, reading.Vocabulary[0].ParagraphOrder);
        var gap = Assert.Single(reading.Questions, q => q.QuestionType == ReadingQuestionTypes.GapFill);
        Assert.Equal("oxygen", gap.CorrectAnswerText);
        var shortAnswer = Assert.Single(reading.Questions, q => q.QuestionType == ReadingQuestionTypes.ShortAnswer);
        Assert.Equal("oxygen", shortAnswer.CorrectAnswerText);
    }

    [Fact]
    public async Task Parse_PartialImport_KeepsValidReading_WhenSiblingInvalid()
    {
        var db = CreateContext(nameof(Parse_PartialImport_KeepsValidReading_WhenSiblingInvalid));
        var admin = CreateAdmin(db);
        var excel = new ReadingExcelService(db, admin);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(excel.BuildTemplate()));
        var info = workbook.Worksheet("Reading_Info");
        info.Cell(3, 1).Value = "RD_BAD";
        info.Cell(3, 2).Value = "Bad Title";
        info.Cell(3, 3).Value = "Z9"; // invalid CEFR
        info.Cell(3, 4).Value = "Topic";
        info.Cell(3, 5).Value = 10;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var preview = await excel.ParseAndValidateAsync(stream);

        Assert.True(preview.HasImportableReadings);
        Assert.Single(preview.ValidReadings);
        Assert.Equal("RD-A1-001", preview.ValidReadings[0].ExternalReadingKey);
        Assert.Equal("L1", preview.ValidReadings[0].ImportFileReadingCode);
        Assert.Contains(preview.FailedReadings, f => f.ReadingCode == "RD_BAD");
    }

    [Fact]
    public void AllocateNextOfficialReadingCode_IncrementsPerCefr()
    {
        var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "RD-A1-001", "RD-A1-002", "RD-B1-001" };
        Assert.Equal("RD-A1-003", ReadingExcelService.AllocateNextOfficialReadingCode("A1", occupied));
        Assert.Equal("RD-B1-002", ReadingExcelService.AllocateNextOfficialReadingCode("B1", occupied));
        Assert.Equal("RD-A2-001", ReadingExcelService.AllocateNextOfficialReadingCode("A2", new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Parse_AlwaysAssignsOfficialReadingCode_EvenWhenFileKeyFree()
    {
        var db = CreateContext(nameof(Parse_AlwaysAssignsOfficialReadingCode_EvenWhenFileKeyFree));
        var admin = CreateAdmin(db);
        var excel = new ReadingExcelService(db, admin);
        using var stream = new MemoryStream(excel.BuildTemplate());
        var preview = await excel.ParseAndValidateAsync(stream);

        Assert.True(preview.HasImportableReadings);
        var reading = Assert.Single(preview.ValidReadings);
        Assert.Equal("RD-A1-001", reading.ExternalReadingKey);
        Assert.Equal("L1", reading.ImportFileReadingCode);
        Assert.Contains(preview.Warnings, w =>
            w.Column == "ReadingCode" &&
            w.Message.Contains("assigned system code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Parse_AssignsNextOfficialCode_WhenCefrSequenceOccupied()
    {
        var db = CreateContext(nameof(Parse_AssignsNextOfficialCode_WhenCefrSequenceOccupied));
        db.ReadingPassages.Add(new ReadingPassage
        {
            Title = "Existing",
            Content = "c",
            Level = "A1",
            ExternalReadingKey = "RD-A1-001",
            Status = ReadingStatuses.Draft,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var admin = CreateAdmin(db);
        var excel = new ReadingExcelService(db, admin);
        using var stream = new MemoryStream(excel.BuildTemplate());
        var preview = await excel.ParseAndValidateAsync(stream);

        Assert.True(preview.HasImportableReadings);
        var reading = Assert.Single(preview.ValidReadings);
        Assert.Equal("RD-A1-002", reading.ExternalReadingKey);
        Assert.Equal("L1", reading.ImportFileReadingCode);
        Assert.Contains(preview.Warnings, w =>
            w.Column == "ReadingCode" &&
            w.Message.Contains("RD-A1-002", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Parse_DuplicateFileReadingCode_FailsSecondReading()
    {
        var db = CreateContext(nameof(Parse_DuplicateFileReadingCode_FailsSecondReading));
        var admin = CreateAdmin(db);
        var excel = new ReadingExcelService(db, admin);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(excel.BuildTemplate()));
        var info = workbook.Worksheet("Reading_Info");
        info.Cell(3, 1).Value = "L1";
        info.Cell(3, 2).Value = "The Importance of Trees";
        info.Cell(3, 3).Value = "A1";
        info.Cell(3, 4).Value = "Environment";
        info.Cell(3, 5).Value = 10;
        info.Cell(3, 6).Value = "Duplicate file key";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var preview = await excel.ParseAndValidateAsync(stream);

        Assert.True(preview.HasImportableReadings);
        Assert.Single(preview.ValidReadings);
        Assert.Equal("RD-A1-001", preview.ValidReadings[0].ExternalReadingKey);
        Assert.Contains(preview.FailedReadings, f => f.ReadingCode == "L1");
        Assert.Contains(preview.Errors, e => e.Message.Contains("duplicated in this file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Parse_PassageDuplicateParagraphNo_FailsReading()
    {
        var db = CreateContext(nameof(Parse_PassageDuplicateParagraphNo_FailsReading));
        var admin = CreateAdmin(db);
        var excel = new ReadingExcelService(db, admin);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(excel.BuildTemplate()));
        var passage = workbook.Worksheet("Passage");
        passage.Cell(3, 3).Value = 1; // same ParagraphNo as row 2

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var preview = await excel.ParseAndValidateAsync(stream);

        Assert.False(preview.HasImportableReadings);
        Assert.Contains(preview.Errors, e => e.Column == "ParagraphNo");
    }

    [Fact]
    public async Task Parse_VocabularyWithoutMeaning_Fails_EmptyVocabSheet_Ok()
    {
        var db = CreateContext(nameof(Parse_VocabularyWithoutMeaning_Fails_EmptyVocabSheet_Ok));
        var admin = CreateAdmin(db);
        var excel = new ReadingExcelService(db, admin);

        using (var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(excel.BuildTemplate())))
        {
            workbook.Worksheet("Vocabulary").Cell(2, 5).Value = ""; // Meaning
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            var bad = await excel.ParseAndValidateAsync(stream);
            Assert.False(bad.HasImportableReadings);
            Assert.Contains(bad.Errors, e => e.Column == "Meaning");
        }

        using (var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(excel.BuildTemplate())))
        {
            var vocab = workbook.Worksheet("Vocabulary");
            // Template v2 ships two sample vocab rows; clear all data rows for empty-sheet case.
            var last = vocab.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = last; row >= 2; row--)
                vocab.Row(row).Delete();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            var ok = await excel.ParseAndValidateAsync(stream);
            Assert.True(ok.HasImportableReadings);
            Assert.Empty(ok.ValidReadings[0].Vocabulary);
        }
    }

    [Fact]
    public async Task Library_FiltersByStudentLevel_AndPublishedOnly()
    {
        await using var db = CreateContext(nameof(Library_FiltersByStudentLevel_AndPublishedOnly));
        db.Users.Add(new User
        {
            UserId = 1,
            Email = "a1@test.local",
            PasswordHash = "x",
            Role = Roles.Standard,
            ReadingLevel = "A1"
        });
        db.ReadingPassages.AddRange(
            new ReadingPassage
            {
                Title = "A1 Pub",
                Content = "c",
                Level = "A1",
                Status = ReadingStatuses.Published,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            },
            new ReadingPassage
            {
                Title = "B1 Pub",
                Content = "c",
                Level = "B1",
                Status = ReadingStatuses.Published,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            },
            new ReadingPassage
            {
                Title = "A1 Draft",
                Content = "c",
                Level = "A1",
                Status = ReadingStatuses.Draft,
                IsPublished = false,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var goals = new Mock<IGoalsService>();
        var learning = new ReadingLearningService(db, goals.Object);
        var list = await learning.GetLibraryAsync(1);

        Assert.Single(list);
        Assert.Equal("A1 Pub", list[0].Title);
    }

    [Fact]
    public async Task Study_OverLevel_ReturnsNull()
    {
        await using var db = CreateContext(nameof(Study_OverLevel_ReturnsNull));
        db.Users.Add(new User { UserId = 2, Email = "a1b@test.local", PasswordHash = "x", Role = Roles.Standard, ReadingLevel = "A1" });
        db.ReadingPassages.Add(new ReadingPassage
        {
            Id = 10,
            Title = "Hard",
            Content = "c",
            Level = "C1",
            Status = ReadingStatuses.Published,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var goals = new Mock<IGoalsService>();
        var learning = new ReadingLearningService(db, goals.Object);
        var study = await learning.GetStudyAsync(2, 10);
        Assert.Null(study);
    }

    [Fact]
    public async Task Library_AdminBypassesReadingLevelGate()
    {
        await using var db = CreateContext(nameof(Library_AdminBypassesReadingLevelGate));
        db.Users.Add(new User
        {
            UserId = 41,
            Email = "admin-lib@test.local",
            PasswordHash = "x",
            Role = Roles.Admin,
            ReadingLevel = "A1"
        });
        db.ReadingPassages.AddRange(
            new ReadingPassage
            {
                Title = "A1 Pub Admin",
                Content = "c",
                Level = "A1",
                Status = ReadingStatuses.Published,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            },
            new ReadingPassage
            {
                Title = "B1 Pub Admin",
                Content = "c",
                Level = "B1",
                Status = ReadingStatuses.Published,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            },
            new ReadingPassage
            {
                Title = "C2 Pub Admin",
                Content = "c",
                Level = "C2",
                Status = ReadingStatuses.Published,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            },
            new ReadingPassage
            {
                Title = "A1 Draft Admin",
                Content = "c",
                Level = "A1",
                Status = ReadingStatuses.Draft,
                IsPublished = false,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var goals = new Mock<IGoalsService>();
        var learning = new ReadingLearningService(db, goals.Object);
        var list = await learning.GetLibraryAsync(41);

        Assert.Equal(3, list.Count);
        Assert.Contains(list, x => x.Title == "A1 Pub Admin");
        Assert.Contains(list, x => x.Title == "B1 Pub Admin");
        Assert.Contains(list, x => x.Title == "C2 Pub Admin");
        Assert.DoesNotContain(list, x => x.Title == "A1 Draft Admin");
    }

    [Fact]
    public async Task Study_AdminOverLevel_ReturnsModel()
    {
        await using var db = CreateContext(nameof(Study_AdminOverLevel_ReturnsModel));
        db.Users.Add(new User
        {
            UserId = 42,
            Email = "admin-study@test.local",
            PasswordHash = "x",
            Role = Roles.Admin,
            ReadingLevel = "A1"
        });
        db.ReadingPassages.Add(new ReadingPassage
        {
            Id = 42,
            Title = "Hard Admin",
            Content = "c",
            Level = "C1",
            Status = ReadingStatuses.Published,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var goals = new Mock<IGoalsService>();
        var learning = new ReadingLearningService(db, goals.Object);
        var study = await learning.GetStudyAsync(42, 42);

        Assert.NotNull(study);
        Assert.Equal("Hard Admin", study!.Title);
        Assert.Equal("C1", study.Level);
    }

    [Fact]
    public async Task Admin_CanApproveOwnReading()
    {
        await using var db = CreateContext(nameof(Admin_CanApproveOwnReading));
        db.Users.Add(new User { UserId = 5, Email = "admin@test.local", PasswordHash = "x", Role = Roles.Admin, ReadingLevel = "C2" });
        var passage = new ReadingPassage
        {
            Title = "Review me",
            Content = "Paragraph",
            Level = "A1",
            Status = ReadingStatuses.PendingReview,
            IsPublished = false,
            CreatedByUserId = 5,
            CreatedAt = DateTime.UtcNow,
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Q?",
                    QuestionType = ReadingQuestionTypes.MultipleChoice,
                    OrderIndex = 1,
                    Points = 1,
                    Explanation = "Because",
                    Options =
                    {
                        new ReadingOption { OptionText = "A", IsCorrect = true, OptionOrder = 1 },
                        new ReadingOption { OptionText = "B", IsCorrect = false, OptionOrder = 2 }
                    }
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        var admin = CreateAdmin(db);
        await admin.ApproveAsync(passage.Id, 5);

        var updated = await db.ReadingPassages.AsNoTracking().FirstAsync(p => p.Id == passage.Id);
        Assert.Equal(ReadingStatuses.Approved, updated.Status);
    }

    [Fact]
    public async Task NonAdminAuthor_CannotApproveOwnReading()
    {
        await using var db = CreateContext(nameof(NonAdminAuthor_CannotApproveOwnReading));
        db.Users.Add(new User { UserId = 6, Email = "author@test.local", PasswordHash = "x", Role = Roles.Standard, ReadingLevel = "B1" });
        var passage = new ReadingPassage
        {
            Title = "Blocked",
            Content = "Paragraph",
            Level = "A1",
            Status = ReadingStatuses.PendingReview,
            IsPublished = false,
            CreatedByUserId = 6,
            CreatedAt = DateTime.UtcNow,
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Q?",
                    QuestionType = ReadingQuestionTypes.MultipleChoice,
                    OrderIndex = 1,
                    Points = 1,
                    Explanation = "Because",
                    Options =
                    {
                        new ReadingOption { OptionText = "A", IsCorrect = true, OptionOrder = 1 },
                        new ReadingOption { OptionText = "B", IsCorrect = false, OptionOrder = 2 }
                    }
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        var admin = CreateAdmin(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => admin.ApproveAsync(passage.Id, 6));
        Assert.Contains("cannot review their own", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OtherAdmin_CanApproveAndPublish()
    {
        await using var db = CreateContext(nameof(OtherAdmin_CanApproveAndPublish));
        db.Users.AddRange(
            new User { UserId = 7, Email = "author@test.local", PasswordHash = "x", Role = Roles.Admin, ReadingLevel = "C2" },
            new User { UserId = 8, Email = "reviewer@test.local", PasswordHash = "x", Role = Roles.Admin, ReadingLevel = "C2" });
        var passage = new ReadingPassage
        {
            Title = "Ready",
            Content = "Paragraph",
            Level = "A2",
            Status = ReadingStatuses.PendingReview,
            CreatedByUserId = 7,
            CreatedAt = DateTime.UtcNow,
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Q?",
                    QuestionType = ReadingQuestionTypes.TrueFalse,
                    OrderIndex = 1,
                    Points = 1,
                    Explanation = "Evidence based",
                    Options =
                    {
                        new ReadingOption { OptionText = "True", IsCorrect = true, OptionOrder = 1 },
                        new ReadingOption { OptionText = "False", IsCorrect = false, OptionOrder = 2 }
                    }
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        var admin = CreateAdmin(db);
        await admin.ApproveAsync(passage.Id, 8);
        await admin.PublishAsync(passage.Id, 8);

        var updated = await db.ReadingPassages.AsNoTracking().FirstAsync(p => p.Id == passage.Id);
        Assert.Equal(ReadingStatuses.Published, updated.Status);
        Assert.True(updated.IsPublished);
        Assert.Equal(8, updated.PublishedByUserId);
    }

    [Fact]
    public async Task ImportConfirm_CreatesDraftWithQuestionsAndParagraphLinkedVocab()
    {
        await using var db = CreateContext(nameof(ImportConfirm_CreatesDraftWithQuestionsAndParagraphLinkedVocab));
        db.Users.Add(new User { UserId = 9, Email = "imp@test.local", PasswordHash = "x", Role = Roles.Admin, ReadingLevel = "C2" });
        await db.SaveChangesAsync();

        var admin = CreateAdmin(db);
        var excel = new ReadingExcelService(db, admin);
        var model = new ReadingEditorViewModel
        {
            ExternalReadingKey = "RD-IMP-001",
            Title = "Imported",
            Topic = "Greetings",
            Level = "A1",
            EstimatedTimeMinutes = 10,
            Content = "Hello world",
            Paragraphs =
            {
                new ReadingParagraphEditorViewModel { ParagraphOrder = 1, ParagraphContent = "Hello world", ExternalParagraphKey = "P1" }
            },
            Questions =
            {
                new ReadingQuestionEditorViewModel
                {
                    OrderIndex = 1,
                    QuestionType = ReadingQuestionTypes.MultipleChoice,
                    QuestionText = "Greeting?",
                    Explanation = "It says hello",
                    Options =
                    {
                        new ReadingOptionEditorViewModel { OptionText = "Hello", IsCorrect = true, OptionOrder = 1 },
                        new ReadingOptionEditorViewModel { OptionText = "Bye", IsCorrect = false, OptionOrder = 2 }
                    }
                }
            },
            Vocabulary =
            {
                new ReadingVocabularyEditorViewModel
                {
                    Word = "hello",
                    VietnameseMeaning = "xin chào",
                    ParagraphOrder = 1
                }
            }
        };

        var confirm = await excel.ConfirmImportAsync([model], 9);
        Assert.Equal(1, confirm.SuccessCount);
        var id = confirm.Items[0].PassageId!.Value;
        var passage = await admin.GetForEditAsync(id);
        Assert.NotNull(passage);
        Assert.Equal(ReadingStatuses.Draft, passage!.Status);
        Assert.False(passage.IsPublished);
        Assert.Single(passage.Questions);
        Assert.Single(passage.VocabularyItems);
        Assert.NotNull(passage.VocabularyItems.First().ParagraphId);
        Assert.Equal(1, passage.VocabularyItems.First().Paragraph!.ParagraphOrder);
    }

    [Fact]
    public async Task ImportConfirm_SkipsAnIdenticalWorkbookAndKeepsChangedContent()
    {
        await using var db = CreateContext(nameof(ImportConfirm_SkipsAnIdenticalWorkbookAndKeepsChangedContent));
        db.Users.Add(new User { UserId = 29, Email = "reading-import@test.local", PasswordHash = "x", Role = Roles.Admin });
        await db.SaveChangesAsync();

        var excel = new ReadingExcelService(db, CreateAdmin(db));
        var workbook = excel.BuildTemplate();
        using var firstStream = new MemoryStream(workbook);
        var firstPreview = await excel.ParseAndValidateAsync(firstStream);
        var first = Assert.Single((await excel.ConfirmImportAsync(firstPreview.ValidReadings, 29)).Items);
        Assert.Equal("Success", first.Status);

        using var retryStream = new MemoryStream(workbook);
        var retryPreview = await excel.ParseAndValidateAsync(retryStream);
        var repeated = Assert.Single((await excel.ConfirmImportAsync(retryPreview.ValidReadings, 29)).Items);
        Assert.Equal("Skipped", repeated.Status);
        Assert.Equal(first.ReadingCode, repeated.ReadingCode);
        Assert.Equal(first.PassageId, repeated.PassageId);

        var changed = Assert.Single(retryPreview.ValidReadings);
        changed.ExternalReadingKey = first.ReadingCode; // A concurrent preview can have an older proposed code.
        changed.Questions[0].Explanation = "A materially different explanation.";
        var added = Assert.Single((await excel.ConfirmImportAsync([changed], 29)).Items);
        Assert.Equal("Success", added.Status);
        Assert.NotEqual(first.ReadingCode, added.ReadingCode);
        Assert.Equal(2, await db.ReadingPassages.CountAsync());
    }

    [Fact]
    public async Task ImportConfirm_SkipsExistingReadingCode_DoesNotOverwrite()
    {
        await using var db = CreateContext(nameof(ImportConfirm_SkipsExistingReadingCode_DoesNotOverwrite));
        db.Users.Add(new User { UserId = 19, Email = "imp2@test.local", PasswordHash = "x", Role = Roles.Admin, ReadingLevel = "C2" });
        db.ReadingPassages.Add(new ReadingPassage
        {
            Title = "Keep me",
            Content = "original",
            Level = "A1",
            ExternalReadingKey = "RD-KEEP",
            Status = ReadingStatuses.Draft,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var admin = CreateAdmin(db);
        var excel = new ReadingExcelService(db, admin);
        var confirm = await excel.ConfirmImportAsync(
        [
            new ReadingEditorViewModel
            {
                ExternalReadingKey = "RD-KEEP",
                Title = "Overwrite attempt",
                Level = "A1",
                Content = "new",
                Paragraphs = { new ReadingParagraphEditorViewModel { ParagraphOrder = 1, ParagraphContent = "new" } },
                Questions =
                {
                    new ReadingQuestionEditorViewModel
                    {
                        QuestionText = "Q",
                        QuestionType = ReadingQuestionTypes.ShortAnswer,
                        CorrectAnswerText = "a",
                        Explanation = "e"
                    }
                }
            }
        ], 19);

        Assert.Equal(1, confirm.SkippedCount);
        var kept = await db.ReadingPassages.AsNoTracking().SingleAsync(p => p.ExternalReadingKey == "RD-KEEP");
        Assert.Equal("Keep me", kept.Title);
        Assert.Equal("original", kept.Content);
    }

    [Fact]
    public async Task Study_RendersStructuredParagraphsAndVocabulary()
    {
        await using var db = CreateContext(nameof(Study_RendersStructuredParagraphsAndVocabulary));
        db.Users.Add(new User { UserId = 21, Email = "stu2@test.local", PasswordHash = "x", Role = Roles.Standard, ReadingLevel = "A2" });
        var passage = new ReadingPassage
        {
            Title = "Trees",
            Content = "P1\n\nP2",
            Level = "A1",
            Status = ReadingStatuses.Published,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow,
            Paragraphs =
            {
                new ReadingParagraph { ParagraphOrder = 1, ParagraphContent = "Trees help us." },
                new ReadingParagraph { ParagraphOrder = 2, ParagraphContent = "Trees give oxygen." }
            },
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Q",
                    QuestionType = ReadingQuestionTypes.ShortAnswer,
                    CorrectAnswerText = "oxygen",
                    Explanation = "p2",
                    OrderIndex = 1,
                    Points = 1
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        db.ReadingVocabularyItems.Add(new ReadingVocabularyItem
        {
            ReadingPassageId = passage.Id,
            Word = "oxygen",
            VietnameseMeaning = "khí oxy",
            ParagraphId = passage.Paragraphs.First(p => p.ParagraphOrder == 2).Id
        });
        await db.SaveChangesAsync();

        var learning = new ReadingLearningService(db, new Mock<IGoalsService>().Object);
        var study = await learning.GetStudyAsync(21, passage.Id);
        Assert.NotNull(study);
        Assert.Equal(2, study!.Paragraphs.Count);
        Assert.Equal("Trees give oxygen.", study.Paragraphs[1].Content);
        Assert.Single(study.Vocabulary);
        Assert.Equal(2, study.Vocabulary[0].ParagraphNo);
    }

    [Fact]
    public async Task SubmitAttempt_ScoresAndHidesAnswerKeyUntilSubmit()
    {
        await using var db = CreateContext(nameof(SubmitAttempt_ScoresAndHidesAnswerKeyUntilSubmit));
        db.Users.Add(new User { UserId = 11, Email = "stu@test.local", PasswordHash = "x", Role = Roles.Standard, ReadingLevel = "B1" });
        var passage = new ReadingPassage
        {
            Title = "Quiz",
            Content = "Text",
            Level = "A1",
            Status = ReadingStatuses.Published,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow,
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Q1",
                    QuestionType = ReadingQuestionTypes.MultipleChoice,
                    OrderIndex = 1,
                    Points = 1,
                    Explanation = "Why",
                    EvidenceText = "Text",
                    Options =
                    {
                        new ReadingOption { OptionText = "Right", IsCorrect = true, OptionOrder = 1 },
                        new ReadingOption { OptionText = "Wrong", IsCorrect = false, OptionOrder = 2 }
                    }
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        var goals = new Mock<IGoalsService>();
        goals.Setup(g => g.BuildReadingCompletionActivityUpdate()).Returns(new GoalsActivityUpdate());
        goals.Setup(g => g.RecordLearningActivityAsync(It.IsAny<int>(), It.IsAny<GoalsActivityUpdate>()))
            .ReturnsAsync(GoalsActivityRecordResult.Success(1));

        var learning = new ReadingLearningService(db, goals.Object);
        var study = await learning.GetStudyAsync(11, passage.Id);
        Assert.NotNull(study);
        Assert.All(study!.Questions, q => Assert.All(q.Options, o => Assert.False(string.IsNullOrEmpty(o.OptionText))));
        // Study view model must not expose IsCorrect
        Assert.DoesNotContain(study.Questions.SelectMany(q => q.Options), o => o.GetType().GetProperty("IsCorrect") != null);

        var attempt = await learning.StartAttemptAsync(11, passage.Id, "Practice", null);
        var correctId = passage.Questions.First().Options.First(o => o.IsCorrect).Id;
        await learning.SaveAnswerAsync(11, attempt.Id, new SaveReadingAnswerRequest
        {
            QuestionId = passage.Questions.First().Id,
            SelectedOptionId = correctId
        });
        var result = await learning.SubmitAttemptAsync(11, attempt.Id);
        Assert.Equal(1, result.CorrectCount);
        Assert.Equal("Why", result.Details[0].Explanation);
    }

    [Fact]
    public async Task SubmitAttempt_ScoresGapFillMatchingAndShortAnswer()
    {
        await using var db = CreateContext(nameof(SubmitAttempt_ScoresGapFillMatchingAndShortAnswer));
        db.Users.Add(new User { UserId = 31, Email = "typed@test.local", PasswordHash = "x", Role = Roles.Standard, ReadingLevel = "B1" });
        var passage = new ReadingPassage
        {
            Title = "Typed quiz",
            Content = "Text",
            Level = "A1",
            Status = ReadingStatuses.Published,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow,
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Gap",
                    QuestionType = ReadingQuestionTypes.GapFill,
                    CorrectAnswerText = "oxygen",
                    Explanation = "gap-why",
                    OrderIndex = 1,
                    Points = 1
                },
                new ReadingQuestion
                {
                    QuestionText = "Short",
                    QuestionType = ReadingQuestionTypes.ShortAnswer,
                    CorrectAnswerText = "Trees give oxygen",
                    Explanation = "short-why",
                    OrderIndex = 2,
                    Points = 1
                },
                new ReadingQuestion
                {
                    QuestionText = "Match",
                    QuestionType = ReadingQuestionTypes.Matching,
                    Explanation = "match-why",
                    OrderIndex = 3,
                    Points = 1,
                    Options =
                    {
                        new ReadingOption { OptionText = "Cat", MatchKey = "p1", MatchSide = "Left", OptionOrder = 1 },
                        new ReadingOption { OptionText = "Mèo", MatchKey = "p1", MatchSide = "Right", OptionOrder = 2 },
                        new ReadingOption { OptionText = "Dog", MatchKey = "p2", MatchSide = "Left", OptionOrder = 3 },
                        new ReadingOption { OptionText = "Chó", MatchKey = "p2", MatchSide = "Right", OptionOrder = 4 }
                    }
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        var goals = new Mock<IGoalsService>();
        goals.Setup(g => g.BuildReadingCompletionActivityUpdate()).Returns(new GoalsActivityUpdate());
        goals.Setup(g => g.RecordLearningActivityAsync(It.IsAny<int>(), It.IsAny<GoalsActivityUpdate>()))
            .ReturnsAsync(GoalsActivityRecordResult.Success(1));

        var learning = new ReadingLearningService(db, goals.Object);
        var attempt = await learning.StartAttemptAsync(31, passage.Id, "Practice", null);

        var gap = passage.Questions.Single(q => q.QuestionType == ReadingQuestionTypes.GapFill);
        var shortQ = passage.Questions.Single(q => q.QuestionType == ReadingQuestionTypes.ShortAnswer);
        var matchQ = passage.Questions.Single(q => q.QuestionType == ReadingQuestionTypes.Matching);
        var cat = matchQ.Options.Single(o => o.OptionText == "Cat");
        var meo = matchQ.Options.Single(o => o.OptionText == "Mèo");
        var dog = matchQ.Options.Single(o => o.OptionText == "Dog");
        var cho = matchQ.Options.Single(o => o.OptionText == "Chó");

        await learning.SaveAnswerAsync(31, attempt.Id, new SaveReadingAnswerRequest
        {
            QuestionId = gap.Id,
            AnswerText = "  Oxygen "
        });
        await learning.SaveAnswerAsync(31, attempt.Id, new SaveReadingAnswerRequest
        {
            QuestionId = shortQ.Id,
            AnswerText = "trees give oxygen"
        });

        var matchingJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [cat.Id.ToString()] = meo.Id.ToString(),
            [dog.Id.ToString()] = cho.Id.ToString()
        });
        await learning.SaveAnswerAsync(31, attempt.Id, new SaveReadingAnswerRequest
        {
            QuestionId = matchQ.Id,
            MatchingJson = matchingJson
        });

        var result = await learning.SubmitAttemptAsync(31, attempt.Id);
        Assert.Equal(3, result.CorrectCount);
        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Details, d => Assert.True(d.IsCorrect));
        Assert.Equal("oxygen", result.Details.Single(d => d.QuestionId == gap.Id).CorrectAnswerText);
    }

    [Fact]
    public async Task GetStudyAsync_RestoresSavedMatchingJsonOnOpenAttempt()
    {
        await using var db = CreateContext(nameof(GetStudyAsync_RestoresSavedMatchingJsonOnOpenAttempt));
        db.Users.Add(new User { UserId = 32, Email = "restore@test.local", PasswordHash = "x", Role = Roles.Standard, ReadingLevel = "B1" });
        var passage = new ReadingPassage
        {
            Title = "Restore match",
            Content = "Text",
            Level = "A1",
            Status = ReadingStatuses.Published,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow,
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Match",
                    QuestionType = ReadingQuestionTypes.Matching,
                    Explanation = "why",
                    OrderIndex = 1,
                    Points = 1,
                    Options =
                    {
                        new ReadingOption { OptionText = "A", MatchKey = "k1", MatchSide = "Left", OptionOrder = 1 },
                        new ReadingOption { OptionText = "1", MatchKey = "k1", MatchSide = "Right", OptionOrder = 2 }
                    }
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        var learning = new ReadingLearningService(db, new Mock<IGoalsService>().Object);
        var attempt = await learning.StartAttemptAsync(32, passage.Id, "Practice", null);
        var left = passage.Questions.Single().Options.Single(o => o.MatchSide == "Left");
        var right = passage.Questions.Single().Options.Single(o => o.MatchSide == "Right");
        var matchingJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [left.Id.ToString()] = right.Id.ToString()
        });
        await learning.SaveAnswerAsync(32, attempt.Id, new SaveReadingAnswerRequest
        {
            QuestionId = passage.Questions.Single().Id,
            MatchingJson = matchingJson
        });

        var study = await learning.GetStudyAsync(32, passage.Id);
        Assert.NotNull(study);
        Assert.Equal(attempt.Id, study!.AttemptId);
        Assert.Equal(matchingJson, study.Questions.Single().SavedMatchingJson);
    }

    [Fact]
    public async Task UpdateDraft_WithoutQuestionsBound_PreservesQuestionsAndVocabulary()
    {
        await using var db = CreateContext(nameof(UpdateDraft_WithoutQuestionsBound_PreservesQuestionsAndVocabulary));
        db.Users.Add(new User { UserId = 41, Email = "admin41@test.local", PasswordHash = "x", Role = Roles.Admin });
        var passage = new ReadingPassage
        {
            Title = "Keep Q",
            Content = "Body",
            Level = "A1",
            Status = ReadingStatuses.Draft,
            IsPublished = false,
            CreatedByUserId = 41,
            CreatedAt = DateTime.UtcNow,
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Keep me",
                    QuestionType = ReadingQuestionTypes.MultipleChoice,
                    Explanation = "why",
                    OrderIndex = 1,
                    Points = 1,
                    Options =
                    {
                        new ReadingOption { OptionText = "A", IsCorrect = true, OptionOrder = 1 },
                        new ReadingOption { OptionText = "B", IsCorrect = false, OptionOrder = 2 }
                    }
                }
            },
            VocabularyItems =
            {
                new ReadingVocabularyItem { Word = "tree", VietnameseMeaning = "cây" }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        var admin = CreateAdmin(db);
        await admin.UpdateDraftAsync(passage.Id, new ReadingEditorViewModel
        {
            Title = "Keep Q updated",
            Content = "Body updated",
            Level = "A1",
            QuestionsBound = false,
            VocabularyBound = false,
            Questions = new List<ReadingQuestionEditorViewModel>(),
            Vocabulary = new List<ReadingVocabularyEditorViewModel>()
        }, 41);

        var reloaded = await admin.GetForEditAsync(passage.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Keep Q updated", reloaded!.Title);
        Assert.Single(reloaded.Questions);
        Assert.Equal("Keep me", reloaded.Questions.Single().QuestionText);
        Assert.Single(reloaded.VocabularyItems);
        Assert.Equal("tree", reloaded.VocabularyItems.Single().Word);
    }

    [Fact]
    public async Task UpdateDraft_WithQuestionsBound_ReplacesTypedQuestions()
    {
        await using var db = CreateContext(nameof(UpdateDraft_WithQuestionsBound_ReplacesTypedQuestions));
        db.Users.Add(new User { UserId = 42, Email = "admin42@test.local", PasswordHash = "x", Role = Roles.Admin });
        var passage = new ReadingPassage
        {
            Title = "Replace Q",
            Content = "Body",
            Level = "A1",
            Status = ReadingStatuses.Draft,
            CreatedByUserId = 42,
            CreatedAt = DateTime.UtcNow,
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Old MC",
                    QuestionType = ReadingQuestionTypes.MultipleChoice,
                    Explanation = "old",
                    OrderIndex = 1,
                    Options =
                    {
                        new ReadingOption { OptionText = "X", IsCorrect = true, OptionOrder = 1 },
                        new ReadingOption { OptionText = "Y", IsCorrect = false, OptionOrder = 2 }
                    }
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        var admin = CreateAdmin(db);
        await admin.UpdateDraftAsync(passage.Id, new ReadingEditorViewModel
        {
            Title = "Replace Q",
            Content = "Body",
            Level = "A1",
            QuestionsBound = true,
            Questions =
            {
                new ReadingQuestionEditorViewModel
                {
                    OrderIndex = 1,
                    QuestionType = ReadingQuestionTypes.GapFill,
                    QuestionText = "Fill ___",
                    CorrectAnswerText = "oxygen",
                    Explanation = "gap",
                    Points = 1
                },
                new ReadingQuestionEditorViewModel
                {
                    OrderIndex = 2,
                    QuestionType = ReadingQuestionTypes.Matching,
                    QuestionText = "Match",
                    Explanation = "match",
                    Points = 1,
                    Options =
                    {
                        new ReadingOptionEditorViewModel { OptionText = "Cat", MatchKey = "p1", MatchSide = "Left", OptionOrder = 1 },
                        new ReadingOptionEditorViewModel { OptionText = "Mèo", MatchKey = "p1", MatchSide = "Right", OptionOrder = 2 }
                    }
                }
            }
        }, 42);

        var reloaded = await admin.GetForEditAsync(passage.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded!.Questions.Count);
        Assert.Contains(reloaded.Questions, q => q.QuestionType == ReadingQuestionTypes.GapFill && q.CorrectAnswerText == "oxygen");
        var match = Assert.Single(reloaded.Questions, q => q.QuestionType == ReadingQuestionTypes.Matching);
        Assert.Empty(ReadingAdminService.ValidateMatchingOptions(match.OrderIndex, match.Options));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenLearnerAttemptsExist()
    {
        await using var db = CreateContext(nameof(DeleteAsync_ThrowsWhenLearnerAttemptsExist));
        db.Users.Add(new User { UserId = 51, Email = "delete-learner@test.local", PasswordHash = "x", Role = Roles.Standard, ReadingLevel = "A1" });
        var passage = new ReadingPassage
        {
            Title = "Delete with answers",
            Content = "Body",
            Level = "A1",
            Status = ReadingStatuses.Draft,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Q1",
                    QuestionType = ReadingQuestionTypes.MultipleChoice,
                    Explanation = "why",
                    OrderIndex = 1,
                    Points = 1,
                    Options =
                    {
                        new ReadingOption { OptionText = "A", IsCorrect = true, OptionOrder = 1 },
                        new ReadingOption { OptionText = "B", IsCorrect = false, OptionOrder = 2 }
                    }
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        db.ReadingAttempts.Add(new ReadingAttempt
        {
            UserId = 51,
            ReadingPassageId = passage.Id,
            Mode = "Practice",
            StartedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var passageId = passage.Id;
        var admin = CreateAdmin(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => admin.DeleteAsync(passageId));
        Assert.Contains("lưu trữ", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await db.ReadingPassages.FindAsync(passageId));
    }

    [Fact]
    public async Task ReplaceQuestionsAsync_PreservesLearnerAnswers()
    {
        await using var db = CreateContext(nameof(ReplaceQuestionsAsync_PreservesLearnerAnswers));
        db.Users.Add(new User { UserId = 52, Email = "replace-learner@test.local", PasswordHash = "x", Role = Roles.Standard, ReadingLevel = "A1" });
        var passage = new ReadingPassage
        {
            Title = "Replace with answers",
            Content = "Body",
            Level = "A1",
            Status = ReadingStatuses.RevisionRequired,
            IsPublished = false,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Old Q",
                    QuestionType = ReadingQuestionTypes.MultipleChoice,
                    Explanation = "old",
                    OrderIndex = 1,
                    Points = 1,
                    Options =
                    {
                        new ReadingOption { OptionText = "A", IsCorrect = true, OptionOrder = 1 },
                        new ReadingOption { OptionText = "B", IsCorrect = false, OptionOrder = 2 }
                    }
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        var oldQuestionId = passage.Questions.First().Id;
        var optionId = passage.Questions.First().Options.First(o => o.IsCorrect).Id;
        var attempt = new ReadingAttempt
        {
            UserId = 52,
            ReadingPassageId = passage.Id,
            Mode = "Practice",
            StartedAt = DateTime.UtcNow
        };
        db.ReadingAttempts.Add(attempt);
        await db.SaveChangesAsync();
        db.ReadingStudentAnswers.Add(new ReadingStudentAnswer
        {
            AttemptId = attempt.Id,
            QuestionId = oldQuestionId,
            SelectedOptionId = optionId,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var admin = CreateAdmin(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => admin.ReplaceQuestionsAsync(passage.Id, new List<ReadingQuestionEditorViewModel>
        {
            new()
            {
                OrderIndex = 1,
                QuestionType = ReadingQuestionTypes.ShortAnswer,
                QuestionText = "New short",
                CorrectAnswerText = "answer",
                Explanation = "new",
                Points = 1
            }
        }));

        Assert.Contains("lịch sử", error.Message, StringComparison.OrdinalIgnoreCase);
        var originalQuestion = await db.ReadingQuestions.AsNoTracking().SingleAsync(q => q.PassageId == passage.Id);
        Assert.Equal(oldQuestionId, originalQuestion.Id);
        Assert.Equal("Old Q", originalQuestion.QuestionText);
        var answer = await db.ReadingStudentAnswers.AsNoTracking().SingleAsync(a => a.AttemptId == attempt.Id);
        Assert.Equal(oldQuestionId, answer.QuestionId);
        Assert.Equal(optionId, answer.SelectedOptionId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.UpdateDraftAsync(passage.Id,
            new ReadingEditorViewModel { Title = "Changed title", QuestionsBound = true }, actorUserId: 1));
        var originalTitle = await db.ReadingPassages.Where(p => p.Id == passage.Id).Select(p => p.Title).SingleAsync();
        Assert.Equal("Replace with answers", originalTitle);
    }

    [Fact]
    public void ValidateMatchingOptions_RejectsMissingSideOrUnbalancedKey()
    {
        var bad = new List<ReadingOption>
        {
            new() { OptionText = "A", MatchKey = "p1", MatchSide = "Left" },
            new() { OptionText = "B", MatchKey = "p1", MatchSide = "Left" }
        };
        var errors = ReadingAdminService.ValidateMatchingOptions(1, bad).ToList();
        Assert.Contains(errors, e => e.Contains("exactly one Left and one Right", StringComparison.Ordinal));

        var good = new List<ReadingOption>
        {
            new() { OptionText = "A", MatchKey = "p1", MatchSide = "Left" },
            new() { OptionText = "B", MatchKey = "p1", MatchSide = "Right" }
        };
        Assert.Empty(ReadingAdminService.ValidateMatchingOptions(1, good));
    }

    private static ReadingAdminService CreateAdmin(DbflashcardContext db) =>
        new ReadingAdminService(db, new LocalFileStorageService(Path.Combine(Path.GetTempPath(), "tct-reading-tests")));

    private static DbflashcardContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new DbflashcardContext(options);
    }
}
