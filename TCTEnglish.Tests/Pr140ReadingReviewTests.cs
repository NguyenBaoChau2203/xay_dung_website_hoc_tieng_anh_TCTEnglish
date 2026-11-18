using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
using TCTEnglish.Controllers;
using TCTEnglish.Models.Reading;
using TCTEnglish.Services.Reading;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;
using TCTEnglish.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Services.Notifications;

namespace TCTEnglish.Tests;

public sealed class Pr140ReadingReviewTests
{
    [Fact]
    public async Task LegacySavedMatchingAnswer_ReloadsWithoutExposingKeys_AndStillScores()
    {
        await using var fixture = await Fixture.CreateAsync();
        var attempt = await fixture.Learning.StartAttemptAsync(101, 701, "Practice", null);
        await fixture.Learning.SaveAnswerAsync(101, attempt.Id, new SaveReadingAnswerRequest
        { QuestionId = 713, MatchingJson = "{\"secret-pair\":\"823,824\"}" });
        var study = await fixture.Learning.GetStudyAsync(101, 701);
        var saved = study!.Questions.Single(q => q.Id == 713).SavedMatchingJson;
        Assert.DoesNotContain("secret-pair", saved ?? "");
        var restored = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(saved!);
        Assert.Equal("824", restored!["823"]);
        Assert.True((await fixture.Learning.SubmitAttemptAsync(101, attempt.Id)).Details.Single(d => d.QuestionId == 713).IsCorrect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedOptionalFields_UpgradeWithoutFalseConflicts_AndPreserveRealLocalEdits(bool localEdit)
    {
        using var folder = new SharedDataPackageTests.TempFolder();
        await using var fixture = await Fixture.CreateAsync();
        var original = SharedDataPackageTests.Reading();
        var one = await SharedDataPackageTests.WriteAsync(folder.Path, SharedDataPackageTests.Package(1, original));
        var engine = new TCTEnglish.Infrastructure.SharedData.SharedDataEngine(fixture.Db, Path.Combine(folder.Path, "wwwroot"), new());
        await engine.ApplyAsync([one]);
        var row = await fixture.Db.ReadingPassages.SingleAsync(p => p.Title == "Shared reading");
        var originalId = row.Id;
        if (localEdit) { row.Description = "Unpublished local edit"; await fixture.Db.SaveChangesAsync(); }
        var next = original with { Values = new(original.Values) };
        next.Values["Description"] = System.Text.Json.JsonSerializer.SerializeToElement("Reviewed new description");
        next.Values["ExternalReadingKey"] = System.Text.Json.JsonSerializer.SerializeToElement("SHARED-READING");
        var two = await SharedDataPackageTests.WriteAsync(folder.Path, SharedDataPackageTests.Package(2, next));
        if (localEdit)
        {
            await Assert.ThrowsAsync<TCTEnglish.Infrastructure.SharedData.SharedDataException>(() => engine.ApplyAsync([one, two]));
            fixture.Db.ChangeTracker.Clear();
            Assert.Equal("Unpublished local edit", (await fixture.Db.ReadingPassages.FindAsync(originalId))!.Description);
        }
        else
        {
            await engine.ApplyAsync([one, two]);
            await engine.ApplyAsync([one, two]);
            Assert.Equal("Reviewed new description", (await fixture.Db.ReadingPassages.FindAsync(originalId))!.Description);
            Assert.Single(await fixture.Db.SharedDataBindings.ToListAsync());
        }
    }

    [Fact]
    public async Task Excel_EvidenceReference_UsesDestinationParagraphIds()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.ReadingParagraphs.Add(new ReadingParagraph
        { Id = 9901, ReadingPassageId = 701, ParagraphOrder = 1, ParagraphContent = "Unrelated existing paragraph" });
        await fixture.Db.SaveChangesAsync();
        var admin = new ReadingAdminService(fixture.Db, Mock.Of<IFileStorageService>());
        var excel = new ReadingExcelService(fixture.Db, admin);
        using var template = new MemoryStream(excel.BuildTemplate());
        var preview = await excel.ParseAndValidateAsync(template);
        Assert.Empty(preview.Errors);
        var result = await excel.ConfirmImportAsync(preview.ValidReadings, 101);
        Assert.Equal(1, result.SuccessCount);
        var passage = await admin.GetForEditAsync(result.Items.Single().PassageId!.Value);
        var question = passage!.Questions.Single(q => q.ExternalQuestionKey == "Q1");
        Assert.NotNull(question.EvidenceParagraphId);
        Assert.Equal(passage.Paragraphs.Single(p => p.ParagraphOrder == 2).Id, question.EvidenceParagraphId);
        using var exported = new MemoryStream(await excel.ExportReadingAsync(passage.Id));
        using var workbook = new ClosedXML.Excel.XLWorkbook(exported);
        Assert.Equal("2", workbook.Worksheet("Answers").Cell(2, 7).GetString());
    }

    [Fact]
    public async Task Assist_RejectsRapidRequestsBeforeCallingTheAssistService()
    {
        await using var fixture = await Fixture.CreateAsync();
        var assist = new Mock<IReadingAssistService>();
        var limiter = new Mock<TCTEnglish.Services.AI.IAiRequestRateLimiter>();
        var retry = 30;
        limiter.Setup(l => l.TryConsume(101, It.IsAny<string?>(), out retry)).Returns(false);
        var controller = new ReadingController(fixture.Learning, Mock.Of<IReadingTranslationService>(), assist.Object, limiter.Object)
        { ControllerContext = fixture.Controller().ControllerContext };
        var result = Assert.IsType<ObjectResult>(await controller.Assist(new ReadingAssistRequest { PassageId = 701, SelectedText = "word" }, CancellationToken.None));
        Assert.Equal(429, result.StatusCode);
        assist.Verify(a => a.AssistAsync(It.IsAny<int>(), It.IsAny<ReadingAssistRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Excel_RejectsExcessiveExpandedDataBeforeWorkbookParsing()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var zip = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            using var entry = archive.CreateEntry("xl/worksheets/sheet1.xml").Open();
            entry.Write(new byte[65 * 1024 * 1024]);
        }
        zip.Position = 0;
        var excel = new ReadingExcelService(fixture.Db, Mock.Of<IReadingAdminService>());
        var result = await excel.ParseAndValidateAsync(zip);
        Assert.Contains(result.Errors, e => e.Message.Contains("expanded size", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.ValidReadings);
    }

    [Theory]
    [InlineData("{\"823\":\"824\"}", true)]
    [InlineData("{\"secret-pair\":\"823,824\"}", true)]
    [InlineData("{\"823\":\"821\"}", false)]
    [InlineData("{\"823\":\"824\",\"999\":\"824\"}", false)]
    [InlineData("{\"823\":null}", false)]
    [InlineData("[]", false)]
    public async Task Matching_GradesCanonicalAndLegacyPairs_AndRejectsForeignOrExtraIds(string json, bool expectedCorrect)
    {
        await using var fixture = await Fixture.CreateAsync();
        var attempt = await fixture.Learning.StartAttemptAsync(101, 701, "Practice", null);
        await fixture.Learning.SaveAnswerAsync(101, attempt.Id, new SaveReadingAnswerRequest { QuestionId = 713, MatchingJson = json });
        var result = await fixture.Learning.SubmitAttemptAsync(101, attempt.Id);
        Assert.Equal(expectedCorrect, result.Details.Single(d => d.QuestionId == 713).IsCorrect);
    }

    [Fact]
    public async Task AdminDelete_PreservesPublishedPassageAndLearnerHistory()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Learning.StartAttemptAsync(101, 701, "Practice", null);
        var admin = new ReadingAdminService(fixture.Db, Mock.Of<IFileStorageService>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.DeleteAsync(701));
        Assert.NotNull(await fixture.Db.ReadingPassages.FindAsync(701));
        Assert.Single(await fixture.Db.ReadingAttempts.ToListAsync());
        Assert.Single(await fixture.Db.UserReadingHistories.ToListAsync());
    }

    [Fact]
    public async Task MediaZip_RejectsExpandedOversizeEntryBeforeSavingAnImage()
    {
        await using var zip = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            await using var entry = archive.CreateEntry("RD001.png").Open();
            await entry.WriteAsync(new byte[6 * 1024 * 1024]);
        }
        zip.Position = 0;
        var file = new FormFile(zip, 0, zip.Length, "mediaZip", "media.zip");
        var admin = new Mock<IReadingAdminService>();
        var warnings = new List<ReadingExcelWarning>();
        await ReadingImportMediaZip.ApplyAsync(file,
            [new TCTEnglish.Areas.Admin.ViewModels.ReadingEditorViewModel { ExternalReadingKey = "RD001" }], admin.Object, warnings);
        admin.Verify(a => a.SavePassageImageAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public async Task LegacySharedReading_LoadsAndStaysPublishedWithoutRewritingThePackage()
    {
        using var folder = new SharedDataPackageTests.TempFolder();
        await using var fixture = await Fixture.CreateAsync();
        var record = SharedDataPackageTests.Reading();
        var question = new TCTEnglish.Infrastructure.SharedData.SharedRecord("team.reading.question", "ReadingQuestion",
            new() { ["QuestionText"] = System.Text.Json.JsonSerializer.SerializeToElement("Choose"), ["OrderIndex"] = System.Text.Json.JsonSerializer.SerializeToElement(1) },
            new() { ["PassageId"] = record.Key });
        // These are the pre-PR package fields. Published v1 bytes must stay readable.
        var loaded = await SharedDataPackageTests.WriteAsync(folder.Path, SharedDataPackageTests.Package(1, record, question));
        var packages = await TCTEnglish.Infrastructure.SharedData.SharedDataFormat.LoadAsync(folder.Path);
        var originalHash = loaded.Release.Sha256;
        var engine = new TCTEnglish.Infrastructure.SharedData.SharedDataEngine(fixture.Db, Path.Combine(folder.Path, "wwwroot"), new());
        await engine.ApplyAsync(packages);
        fixture.Db.ChangeTracker.Clear();
        var imported = await fixture.Db.ReadingPassages.SingleAsync(p => p.Title == "Shared reading");
        Assert.Equal(ReadingStatuses.Published, imported.Status);
        var importedQuestion = await fixture.Db.ReadingQuestions.SingleAsync(q => q.PassageId == imported.Id);
        Assert.Equal(ReadingQuestionTypes.MultipleChoice, importedQuestion.QuestionType);
        Assert.Equal(1, importedQuestion.Points);
        await engine.ApplyAsync(packages);
        Assert.DoesNotContain(await engine.InspectAsync(packages), item => item.StartsWith("local-edit"));
        Assert.Equal(originalHash, TCTEnglish.Infrastructure.SharedData.SharedDataFormat.Hash(
            await File.ReadAllBytesAsync(Path.Combine(loaded.Directory, "package.json"))));
    }

    [Fact]
    public async Task DailyChallenge_CompletesOnce_OnTheInjectedBusinessDate()
    {
        await using var fixture = await Fixture.CreateAsync();
        var (controller, token) = fixture.DailyChallenge();
        Assert.IsType<JsonResult>(await controller.CheckAnswer(900, token));
        Assert.IsType<JsonResult>(await controller.CheckAnswer(900, token));
        var day = await fixture.Db.UserDailyActivities.SingleAsync();
        Assert.True(day.DailyChallengeCompleted);
        Assert.Equal(new DateTime(2030, 1, 2), day.ActivityDate);
        fixture.Goals.Verify(g => g.RecordLearningActivityAsync(101, It.IsAny<GoalsActivityUpdate>()), Times.Once);
    }

    [Fact]
    public async Task DailyChallenge_RollsBackGoalWritesWhenRecordingFails()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Goals.Setup(g => g.RecordLearningActivityAsync(101, It.IsAny<GoalsActivityUpdate>()))
            .Returns(async () =>
            {
                fixture.Db.UserDailyActivities.Add(new UserDailyActivity
                { UserId = 101, ActivityDate = new DateTime(2030, 1, 2), XpEarned = 15 });
                await fixture.Db.SaveChangesAsync();
                throw new InvalidOperationException("simulated failure after goals write");
            });
        var (controller, token) = fixture.DailyChallenge();
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CheckAnswer(900, token));
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.UserDailyActivities.ToListAsync());
    }

    [Theory]
    [InlineData(702, "concealed")]
    [InlineData(703, "advanced")]
    public async Task Lookup_DeniesUnpublishedAndOverLevelSources(int passageId, string word)
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.IsAssignableFrom<NotFoundResult>(await fixture.Controller().Lookup(passageId, word));
    }

    [Theory]
    [InlineData(702, 802)]
    [InlineData(703, 803)]
    [InlineData(701, 802)]
    public async Task Notebook_DeniesInaccessibleOrMismatchedVocabulary(int passageId, int vocabularyId)
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Controller().SaveNotebook(new SaveNotebookApiRequest
        { PassageId = passageId, VocabItemId = vocabularyId, Word = "requested" });
        Assert.IsAssignableFrom<NotFoundResult>(result);
        Assert.Empty(await fixture.Db.StudentVocabularies.ToListAsync());
    }

    [Theory]
    [InlineData(712, null)]
    [InlineData(711, 822)]
    public async Task SaveAnswer_CannotAttachAnotherPassagesQuestionOrOption(int questionId, int? optionId)
    {
        await using var fixture = await Fixture.CreateAsync();
        var attempt = await fixture.Learning.StartAttemptAsync(101, 701, "Practice", null);
        var result = await fixture.Controller().SaveAnswer(new SaveReadingAnswerApiRequest
        { AttemptId = attempt.Id, QuestionId = questionId, SelectedOptionId = optionId });
        Assert.IsAssignableFrom<NotFoundResult>(result);
        Assert.Empty(await fixture.Db.ReadingStudentAnswers.ToListAsync());
    }

    [Fact]
    public async Task SaveAnswer_RechecksAccessAfterThePassageIsWithdrawn()
    {
        await using var fixture = await Fixture.CreateAsync();
        var attempt = await fixture.Learning.StartAttemptAsync(101, 701, "Practice", null);
        var passage = await fixture.Db.ReadingPassages.FindAsync(701);
        passage!.IsPublished = false;
        passage.Status = ReadingStatuses.Archived;
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Controller().SaveAnswer(new SaveReadingAnswerApiRequest
        { AttemptId = attempt.Id, QuestionId = 711, SelectedOptionId = 821 });
        Assert.IsAssignableFrom<NotFoundResult>(result);
        Assert.Empty(await fixture.Db.ReadingStudentAnswers.ToListAsync());
    }

    [Fact]
    public async Task SubmitAnotherUsersAttempt_Returns404()
    {
        await using var fixture = await Fixture.CreateAsync();
        var attempt = await fixture.Learning.StartAttemptAsync(102, 701, "Practice", null);
        Assert.IsAssignableFrom<NotFoundResult>(await fixture.Controller().SubmitAttempt(attempt.Id));
    }

    [Fact]
    public async Task Completion_AndGoals_RollBackTogetherWhenGoalsFails()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Goals.Setup(g => g.RecordLearningActivityAsync(101, It.IsAny<GoalsActivityUpdate>()))
            .ThrowsAsync(new InvalidOperationException("simulated goal failure"));
        var attempt = await fixture.Learning.StartAttemptAsync(101, 701, "Practice", null);
        await fixture.Learning.SaveAnswerAsync(101, attempt.Id, new SaveReadingAnswerRequest
        { QuestionId = 711, SelectedOptionId = 821 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Learning.SubmitAttemptAsync(101, attempt.Id));
        fixture.Db.ChangeTracker.Clear();
        Assert.False((await fixture.Db.ReadingAttempts.FindAsync(attempt.Id))!.IsSubmitted);
        Assert.False((await fixture.Db.UserReadingHistories.SingleAsync(h => h.UserId == 101)).IsCompleted);
        Assert.Null((await fixture.Db.ReadingStudentAnswers.SingleAsync()).IsCorrect);
    }

    [Fact]
    public async Task RejectedGoals_CannotLeaveTheAttemptCompleted()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Goals.Setup(g => g.RecordLearningActivityAsync(101, It.IsAny<GoalsActivityUpdate>()))
            .ReturnsAsync(GoalsActivityRecordResult.Invalid("rejected"));
        var attempt = await fixture.Learning.StartAttemptAsync(101, 701, "Practice", null);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => fixture.Learning.SubmitAttemptAsync(101, attempt.Id));
        fixture.Db.ChangeTracker.Clear();
        Assert.False((await fixture.Db.ReadingAttempts.FindAsync(attempt.Id))!.IsSubmitted);
    }

    [Fact]
    public async Task RetriedSubmit_ReturnsTheSavedScore_WithoutAnotherGoalsCredit()
    {
        await using var fixture = await Fixture.CreateAsync();
        var attempt = await fixture.Learning.StartAttemptAsync(101, 701, "Practice", null);
        await fixture.Learning.SaveAnswerAsync(101, attempt.Id, new SaveReadingAnswerRequest
        { QuestionId = 711, SelectedOptionId = 821 });
        var original = await fixture.Learning.SubmitAttemptAsync(101, attempt.Id);
        var retry = await fixture.Learning.SubmitAttemptAsync(101, attempt.Id);
        Assert.Equal(original.Score, retry.Score);
        Assert.Equal(original.MaxScore, retry.MaxScore);
        fixture.Goals.Verify(g => g.RecordLearningActivityAsync(101, It.IsAny<GoalsActivityUpdate>()), Times.Once);
    }

    [Fact]
    public async Task Study_DoesNotPublishTheMatchingAnswerKey()
    {
        await using var fixture = await Fixture.CreateAsync();
        var model = await fixture.Learning.GetStudyAsync(101, 701);
        var matching = Assert.Single(model!.Questions, q => q.QuestionType == ReadingQuestionTypes.Matching);
        Assert.All(matching.Options, option => Assert.True(string.IsNullOrEmpty(option.MatchKey)));
        Assert.Contains(matching.Options, option => option.MatchSide == "Left");
        Assert.Contains(matching.Options, option => option.MatchSide == "Right");
    }

    [Fact]
    public async Task NotebookCsv_NeutralizesSpreadsheetFormulas()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.StudentVocabularies.Add(new StudentVocabulary
        { UserId = 101, Word = "=1+1", VietnameseMeaning = "\t@SUM(1,2)", CreatedAt = DateTime.UtcNow });
        await fixture.Db.SaveChangesAsync();
        var result = Assert.IsType<FileContentResult>(await fixture.Controller().ExportNotebook());
        var csv = System.Text.Encoding.UTF8.GetString(result.FileContents);
        Assert.Contains("\"'=1+1\"", csv);
        Assert.Contains("\"'\t@SUM(1,2)\"", csv);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public DbflashcardContext Db { get; }
        public Mock<IGoalsService> Goals { get; } = new();
        public ReadingLearningService Learning { get; }

        private Fixture(SqliteConnection connection, DbflashcardContext db)
        {
            _connection = connection;
            Db = db;
            Goals.Setup(g => g.BuildReadingCompletionActivityUpdate())
                .Returns(new GoalsActivityUpdate { ReadingCompletedCount = 1, XpEarned = 10 });
            Goals.Setup(g => g.RecordLearningActivityAsync(It.IsAny<int>(), It.IsAny<GoalsActivityUpdate>()))
                .ReturnsAsync(GoalsActivityRecordResult.Success(1));
            Learning = new ReadingLearningService(db, Goals.Object);
        }

        public ReadingController Controller() => new(Learning, Mock.Of<IReadingTranslationService>(), Mock.Of<IReadingAssistService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, "101"), new Claim(ClaimTypes.Role, Roles.Standard)
                    ], "ReviewTest"))
                }
            }
        };

        public (TCTVocabulary.Controllers.HomeController Controller, string Token) DailyChallenge()
        {
            var provider = new EphemeralDataProtectionProvider();
            var token = WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(
                provider.CreateProtector("HomeController.DailyChallenge.v1")
                    .Protect($"101|900|{DateTime.UtcNow.AddMinutes(10).Ticks}")));
            var controller = new TCTVocabulary.Controllers.HomeController(Db, Mock.Of<IAppEmailSender>(), Goals.Object,
                provider, NullLogger<TCTVocabulary.Controllers.HomeController>.Instance,
                new LearningBusinessClock(new FixedClock())) { ControllerContext = Controller().ControllerContext };
            return (controller, token);
        }

        private sealed class FixedClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(2030, 1, 1, 18, 0, 0, TimeSpan.Zero);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection)
                .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>().Options);
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(new User { UserId = 101, Email = "review101@example.test", PasswordHash = "synthetic", FullName = "Review A", Role = Roles.Standard, ReadingLevel = "A1" },
                new User { UserId = 102, Email = "review102@example.test", PasswordHash = "synthetic", FullName = "Review B", Role = Roles.Standard, ReadingLevel = "A1" });
            var visible = new ReadingPassage
            {
                Id = 701, Title = "Visible", Content = "Authorized content", Level = "A1", IsPublished = true,
                Status = ReadingStatuses.Published, CreatedAt = DateTime.UtcNow,
                Questions = [new ReadingQuestion { Id = 711, QuestionText = "Choose", QuestionType = ReadingQuestionTypes.MultipleChoice, Points = 1,
                    Options = [new ReadingOption { Id = 821, OptionText = "Yes", IsCorrect = true }] },
                    new ReadingQuestion { Id = 713, QuestionText = "Match", QuestionType = ReadingQuestionTypes.Matching, Points = 1,
                        Options = [new ReadingOption { Id = 823, OptionText = "Cat", MatchSide = "Left", MatchKey = "secret-pair" },
                            new ReadingOption { Id = 824, OptionText = "Mèo", MatchSide = "Right", MatchKey = "secret-pair" }] }]
            };
            db.ReadingPassages.AddRange(visible, new ReadingPassage
            {
                Id = 702, Title = "Hidden", Content = "Private", Level = "A1", IsPublished = false,
                Status = ReadingStatuses.Draft, CreatedAt = DateTime.UtcNow,
                Questions = [new ReadingQuestion { Id = 712, QuestionText = "Secret question", Points = 1,
                    Options = [new ReadingOption { Id = 822, OptionText = "Private option", IsCorrect = true }] }],
                VocabularyItems = [new ReadingVocabularyItem { Id = 802, Word = "concealed", EnglishDefinition = "SECRET_UNPUBLISHED_DEFINITION", VietnameseMeaning = "Hidden" }]
            }, new ReadingPassage
            {
                Id = 703, Title = "Above level", Content = "B2 content", Level = "B2", IsPublished = true,
                Status = ReadingStatuses.Published, CreatedAt = DateTime.UtcNow,
                VocabularyItems = [new ReadingVocabularyItem { Id = 803, Word = "advanced", VietnameseMeaning = "B2 meaning" }]
            });
            await db.SaveChangesAsync();
            return new Fixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
