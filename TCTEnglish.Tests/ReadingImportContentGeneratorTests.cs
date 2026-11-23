using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models.Reading;
using TCTEnglish.Services.Reading;
using TCTEnglish.Tests.ReadingContent;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class ReadingImportContentGeneratorTests
{
    public static string OutputDirectory
    {
        get
        {
            var root = FindRepoRoot();
            return Path.Combine(root, ".appdata", "local", "reading-imports");
        }
    }

    [Fact]
    public async Task Generate_AllCefrImportWorkbooks_ParseWithZeroFailures()
    {
        Directory.CreateDirectory(OutputDirectory);
        var db = CreateContext(nameof(Generate_AllCefrImportWorkbooks_ParseWithZeroFailures));
        var excel = new ReadingExcelService(db, CreateAdmin(db));

        foreach (var (cefr, packs) in ReadingLevelPacks.AllByCefr)
        {
            Assert.Equal(2, packs.Count);
            var path = Path.Combine(OutputDirectory, $"TCT_Reading_Import_{cefr}.xlsx");
            ReadingImportWorkbookBuilder.WriteWorkbook(path, cefr, packs);
            Assert.True(File.Exists(path), path);

            await using var stream = File.OpenRead(path);
            var preview = await excel.ParseAndValidateAsync(stream);
            Assert.True(preview.HasImportableReadings, cefr + ": " + string.Join("; ", preview.Errors.Select(e => e.Message)));
            Assert.Empty(preview.FailedReadings);
            Assert.Empty(preview.Errors);
            Assert.Equal(2, preview.ValidReadings.Count);
            Assert.Equal($"RD-{cefr}-001", preview.ValidReadings[0].ExternalReadingKey);
            Assert.Equal($"RD-{cefr}-002", preview.ValidReadings[1].ExternalReadingKey);
            Assert.Equal("L1", preview.ValidReadings[0].ImportFileReadingCode);
            Assert.Equal("L2", preview.ValidReadings[1].ImportFileReadingCode);
            var (minWords, maxWords, duration) = WordBand(cefr);
            Assert.All(preview.ValidReadings, r =>
            {
                Assert.Equal(cefr, r.Level);
                Assert.Equal(duration, r.EstimatedTimeMinutes);
                Assert.True(r.Paragraphs.Count >= 2);
                Assert.Equal(5, r.Questions.Count);
                Assert.Contains(r.Questions, q => q.QuestionType == ReadingQuestionTypes.MultipleChoice);
                Assert.Contains(r.Questions, q => q.QuestionType == ReadingQuestionTypes.TrueFalse);
                Assert.Contains(r.Questions, q => q.QuestionType == ReadingQuestionTypes.GapFill);
                Assert.Contains(r.Questions, q => q.QuestionType == ReadingQuestionTypes.Matching);
                Assert.Contains(r.Questions, q => q.QuestionType == ReadingQuestionTypes.ShortAnswer);
                Assert.True(r.Vocabulary.Count >= 5);
                var words = CountPassageWords(r.Paragraphs.Select(p => p.ParagraphContent));
                Assert.True(words >= minWords && words <= maxWords,
                    $"{cefr}/{r.Title}: passage words {words} outside {minWords}-{maxWords}");
            });
        }

        var files = Directory.GetFiles(OutputDirectory, "TCT_Reading_Import_*.xlsx");
        Assert.Equal(6, files.Length);
    }

    [Fact]
    public async Task ConfirmImport_A1Workbook_CreatesTwoDrafts()
    {
        // Ensure workbooks exist (generation is deterministic).
        await Generate_AllCefrImportWorkbooks_ParseWithZeroFailures();

        var path = Path.Combine(OutputDirectory, "TCT_Reading_Import_A1.xlsx");
        await using var db = CreateContext(nameof(ConfirmImport_A1Workbook_CreatesTwoDrafts));
        db.Users.Add(new User
        {
            UserId = 1,
            Email = "admin-import@test.local",
            PasswordHash = "x",
            Role = Roles.Admin,
            ReadingLevel = "C2"
        });
        await db.SaveChangesAsync();

        var excel = new ReadingExcelService(db, CreateAdmin(db));
        await using (var stream = File.OpenRead(path))
        {
            var preview = await excel.ParseAndValidateAsync(stream);
            Assert.Equal(2, preview.ValidReadings.Count);
            var confirm = await excel.ConfirmImportAsync(preview.ValidReadings, actorUserId: 1);
            Assert.Equal(2, confirm.SuccessCount);
            Assert.Equal(0, confirm.FailedCount);
        }

        var drafts = await db.ReadingPassages.AsNoTracking()
            .Where(p => p.ExternalReadingKey!.StartsWith("RD-A1-"))
            .OrderBy(p => p.ExternalReadingKey)
            .ToListAsync();
        Assert.Equal(2, drafts.Count);
        Assert.Equal("RD-A1-001", drafts[0].ExternalReadingKey);
        Assert.Equal("RD-A1-002", drafts[1].ExternalReadingKey);
        Assert.All(drafts, p =>
        {
            Assert.Equal(ReadingStatuses.Draft, p.Status);
            Assert.False(p.IsPublished);
            Assert.Equal("A1", p.Level);
        });

        // Re-import matches the existing drafts even when preview proposes new codes.
        await using (var stream = File.OpenRead(path))
        {
            var preview2 = await excel.ParseAndValidateAsync(stream);
            Assert.Equal("RD-A1-003", preview2.ValidReadings[0].ExternalReadingKey);
            Assert.Equal("RD-A1-004", preview2.ValidReadings[1].ExternalReadingKey);
            Assert.Equal("L1", preview2.ValidReadings[0].ImportFileReadingCode);
            Assert.Equal("L2", preview2.ValidReadings[1].ImportFileReadingCode);
            var confirm2 = await excel.ConfirmImportAsync(preview2.ValidReadings, actorUserId: 1);
            Assert.Equal(0, confirm2.SuccessCount);
            Assert.Equal(2, confirm2.SkippedCount);
            Assert.Equal(drafts.Select(p => p.Id), confirm2.Items.Select(item => item.PassageId!.Value));
        }

        var allA1 = await db.ReadingPassages.AsNoTracking()
            .Where(p => p.ExternalReadingKey!.StartsWith("RD-A1-"))
            .OrderBy(p => p.ExternalReadingKey)
            .Select(p => p.ExternalReadingKey)
            .ToListAsync();
        Assert.Equal(["RD-A1-001", "RD-A1-002"], allA1);
    }

    private static (int Min, int Max, int Duration) WordBand(string cefr) => cefr.ToUpperInvariant() switch
    {
        "A1" => (80, 120, 6),
        "A2" => (120, 180, 8),
        "B1" => (200, 300, 12),
        "B2" => (300, 450, 18),
        "C1" => (450, 600, 22),
        "C2" => (550, 750, 28),
        _ => throw new ArgumentOutOfRangeException(nameof(cefr), cefr, null)
    };

    private static int CountPassageWords(IEnumerable<string?> paragraphs)
    {
        var text = string.Join(' ', paragraphs.Where(p => !string.IsNullOrWhiteSpace(p)));
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TCTEnglish.sln")) ||
                File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static ReadingAdminService CreateAdmin(DbflashcardContext db) =>
        new(db, new LocalFileStorageService(Path.Combine(Path.GetTempPath(), "tct-reading-import-gen")));

    private static DbflashcardContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new DbflashcardContext(options);
    }
}
