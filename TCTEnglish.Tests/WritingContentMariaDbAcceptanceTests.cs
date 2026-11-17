using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Infrastructure.SharedData;
using TCTEnglish.Infrastructure.WritingContent;
using TCTEnglish.Services.Writing;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class WritingContentMariaDbAcceptanceTests
{
    [MariaDbAcceptanceFact]
    public async Task All108CanonicalLessons_UpgradeFreshRecipient_RepeatAndPreserveExistingPersonalRows()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID"));
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        await using (var migrate = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options))
        {
            await MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(migrate.Database.GetDbConnection(), target.Database, run);
            await migrate.Database.MigrateAsync();
            await migrate.Database.MigrateAsync();
            Assert.False(migrate.Database.HasPendingModelChanges());
        }
        await using var factory = new MariaDbAcceptanceFactory(connection, run,
            services => services.PostConfigure<WritingEvaluationOptions>(o => o.CloudEnhancementEnabled = false));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.WritingExercises.Add(new WritingExercise
        {
            Title = "Stage 07 private preservation fixture", UserId = TestDataIds.UserId,
            Level = "beginner", ContentType = "emails", IsPublished = false, SourceType = "private-test",
            Topic = "Preservation", PreviewText = "Private fixture; canonical imports must preserve it.",
            CreatedAt = DateTime.UtcNow,
            WritingExerciseSentences = [new WritingExerciseSentence
            {
                SortOrder = 1, VietnameseText = "Nội dung riêng tư cần giữ nguyên.", EnglishMeaning = "Preserve this private content."
            }]
        });
        await db.SaveChangesAsync();
        Assert.True((await scope.ServiceProvider.GetRequiredService<IWritingService>()
            .EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId))!.Passed);
        var before = await SnapshotAsync(db);
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var lessons = new List<WritingContentLesson>();
        foreach (var level in WritingContentBatch.Levels)
            lessons.AddRange(JsonSerializer.Deserialize<List<WritingContentLesson>>(
                await File.ReadAllTextAsync(Path.Combine(repo, "data", "writing", "2026-11-14", level + ".json")), WritingContentBatch.Json)!);
        var keys = lessons.Select(l => l.Key).ToHashSet(StringComparer.Ordinal);
        var packages = (await SharedDataFormat.LoadAsync(Path.Combine(repo, "data", "shared")))
            .Where(p => p.Package.RootKey != null && keys.Contains(p.Package.RootKey)).ToList();
        Assert.Equal(108, packages.Count);
        Assert.All(packages, p => Assert.Empty(p.Package.DependsOn));
        var scratch = Path.Combine(repo, ".appdata", "local", "temp", "writing07-provider-" + Guid.NewGuid().ToString("N"));
        var engine = new SharedDataEngine(db, scratch, new());
        await engine.ApplyAsync(packages);
        db.ChangeTracker.Clear();
        var imported = await db.WritingExercises.AsNoTracking()
            .Where(x => x.SourceType == "original-20261114")
            .Include(x => x.WritingExerciseSentences).ThenInclude(x => x.WritingAcceptedAnswers).ToListAsync();
        Assert.Equal(108, imported.Count);
        Assert.Equal(1152, imported.Sum(x => x.WritingExerciseSentences.Count));
        Assert.Equal(108, imported.Sum(x => x.WritingExerciseSentences.Sum(s => s.WritingAcceptedAnswers.Count)));
        Assert.All(imported, x => { Assert.Null(x.UserId); Assert.True(x.IsPublished); });
        foreach (var level in WritingContentBatch.Levels)
            foreach (var format in WritingContentBatch.Formats)
                Assert.Equal(6, imported.Count(x => x.Level == level && x.ContentType == format));
        foreach (var lesson in lessons)
        {
            var row = Assert.Single(imported.Where(x => x.Title == lesson.Title));
            WritingContentBatch.AssertMatches(lesson, row);
            foreach (var sentence in row.WritingExerciseSentences)
            {
                Assert.True(WritingAssessmentPolicy.Assess(sentence.EnglishMeaning, sentence.EnglishMeaning).Passed);
                foreach (var answer in sentence.WritingAcceptedAnswers)
                    Assert.True(WritingAssessmentPolicy.Assess(sentence.EnglishMeaning, answer.AnswerText,
                        sentence.WritingAcceptedAnswers.Select(a => a.AnswerText)).Passed);
            }
        }
        var first = await SnapshotAsync(db);
        foreach (var table in before)
        {
            var remaining = first[table.Key].ToList();
            foreach (var hash in table.Value)
                Assert.True(remaining.Remove(hash), "An existing row changed in " + table.Key);
        }
        await engine.ApplyAsync(packages);
        db.ChangeTracker.Clear();
        Assert.Equal(SharedDataFormat.Digest(first), SharedDataFormat.Digest(await SnapshotAsync(db)));
    }

    private static async Task<Dictionary<string, List<string>>> SnapshotAsync(DbflashcardContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        var tables = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SHOW TABLES";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        }
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var table in tables.Order(StringComparer.Ordinal))
        {
            Assert.All(table, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c == '_'));
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM `{table}`";
            await using var reader = await command.ExecuteReaderAsync();
            var hashes = new List<string>();
            while (await reader.ReadAsync())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                hashes.Add(SharedDataFormat.Hash(JsonSerializer.SerializeToUtf8Bytes(values)));
            }
            result[table] = hashes.Order(StringComparer.Ordinal).ToList();
        }
        return result;
    }
}
