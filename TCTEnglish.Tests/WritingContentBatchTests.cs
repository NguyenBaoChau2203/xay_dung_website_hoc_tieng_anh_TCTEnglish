using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TCTEnglish.Infrastructure.SharedData;
using TCTEnglish.Infrastructure.WritingContent;
using TCTEnglish.Services.Writing;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingContentBatchTests
{
    private static string Repo => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
    private static List<WritingContentLesson> Read(string level = "beginner") => JsonSerializer.Deserialize<List<WritingContentLesson>>(
        File.ReadAllText(Path.Combine(Repo, $"data/writing/2026-11-14/{level}.json")), WritingContentBatch.Json)!;

    [Theory]
    [InlineData("beginner", 288)]
    [InlineData("intermediate", 432)]
    [InlineData("advanced", 432)]
    public void Batch_IsComplete_AndAllReferencesAndVariantsUseCurrentPolicy(string level, int pairs)
    {
        var lessons = Read(level); WritingContentBatch.Validate(lessons, level);
        Assert.Equal(pairs, lessons.Sum(x => x.Sentences.Count));
        Assert.Equal(36, lessons.Sum(x => x.Sentences.Sum(s => s.ApprovedAlternatives.Count)));
        foreach (var lesson in lessons)
        {
            var row = WritingContentBatch.Create(lesson, TestDataIds.AdminUserId);
            WritingContentBatch.AssertMatches(lesson, row);
            // Near-copy variants are detected even when word order is slightly changed.
            Assert.True(WritingContentBatch.Similarity(WritingContentBatch.Text(lesson), WritingContentBatch.Text(lesson) + " One extra sentence.") > .75);
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-level")]
    [InlineData("order")]
    [InlineData("empty-english")]
    [InlineData("vietnamese-only")]
    [InlineData("duplicate-variant")]
    [InlineData("near-duplicate")]
    public void ValidatorRejectsIncompleteOrCorruptedContent(string fault)
    {
        var lessons = Read(); var first = lessons[0];
        switch (fault)
        {
            case "missing": lessons.RemoveAt(0); break;
            case "wrong-level": lessons[0] = first with { Level = "advanced" }; break;
            case "order": first.Sentences[0] = first.Sentences[0] with { SortOrder = 7 }; break;
            case "empty-english": first.Sentences[0] = first.Sentences[0] with { EnglishMeaning = "" }; break;
            case "vietnamese-only": first.Sentences[0] = first.Sentences[0] with { EnglishMeaning = first.Sentences[0].VietnameseText }; break;
            case "duplicate-variant": first.Sentences[0].ApprovedAlternatives.Add(first.Sentences[0].EnglishMeaning); break;
            case "near-duplicate": lessons[1] = first with { Key = lessons[1].Key, Title = lessons[1].Title }; break;
        }
        Assert.Throws<InvalidDataException>(() => WritingContentBatch.Validate(lessons, "beginner"));
    }

    [Fact]
    public void ResumeComparisonRejectsPrivateUnpublishedAndSentenceEdits()
    {
        var lesson = Read()[0]; var row = WritingContentBatch.Create(lesson, TestDataIds.AdminUserId);
        row.UserId = TestDataIds.UserId;
        Assert.Throws<InvalidDataException>(() => WritingContentBatch.AssertMatches(lesson, row));
        row.UserId = null; row.IsPublished = false;
        Assert.Throws<InvalidDataException>(() => WritingContentBatch.AssertMatches(lesson, row));
        row.IsPublished = true; row.WritingExerciseSentences.First().VietnameseText += " Nội dung riêng.";
        Assert.Throws<InvalidDataException>(() => WritingContentBatch.AssertMatches(lesson, row));
    }

    [Fact]
    public void AuthoredBatchesHaveDistinctContentAndMatchReservedManifestIdentities()
    {
        var lessons = WritingContentBatch.Levels.Where(level => File.Exists(Path.Combine(Repo,
            $"data/writing/2026-11-14/{level}.json"))).SelectMany(level => Read(level)).ToList();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo, "data/writing/2026-11-14/manifest.json")));
        var slots = manifest.RootElement.GetProperty("slots").EnumerateArray().ToList();
        Assert.Equal(108, slots.Count);
        Assert.Equal(108, slots.Select(s => s.GetProperty("key").GetString()).Distinct().Count());
        Assert.Equal(108, lessons.Count);
        foreach (var level in WritingContentBatch.Levels)
            foreach (var format in WritingContentBatch.Formats)
                Assert.Equal(6, lessons.Count(l => l.Level == level && l.ContentType == format));
        Assert.Equal(lessons.Count, lessons.Select(l => WritingContentBatch.Normalize(l.Title)).Distinct().Count());
        var references = lessons.SelectMany(l => l.Sentences).Select(s => WritingContentBatch.Normalize(s.EnglishMeaning)).ToList();
        Assert.Equal(references.Count, references.Distinct().Count());
        foreach (var lesson in lessons)
        {
            var slot = slots.Single(s => s.GetProperty("key").GetString() == lesson.Key);
            Assert.Equal(lesson.Level, slot.GetProperty("level").GetString());
            Assert.Equal(lesson.ContentType, slot.GetProperty("contentType").GetString());
            Assert.Equal(lesson.Title, slot.GetProperty("title").GetString());
            Assert.Equal(lesson.Topic, slot.GetProperty("topic").GetString());
            Assert.Equal(SharedDataFormat.Digest(lesson), slot.GetProperty("contentSha256").GetString());
            foreach (var other in lessons.Where(l => l.Level != lesson.Level))
                Assert.True(WritingContentBatch.Similarity(WritingContentBatch.Text(lesson), WritingContentBatch.Text(other)) < .75,
                    $"Cross-batch near-duplicate: {lesson.Key} / {other.Key}");
        }
    }

    [Theory]
    [InlineData("beginner")]
    [InlineData("intermediate")]
    [InlineData("advanced")]
    public async Task CanonicalPackagesContainOnlySelectedPublicContentAndMappedApprover(string level)
    {
        var lessons = Read(level); var all = await SharedDataFormat.LoadAsync(Path.Combine(Repo, "data/shared"));
        var selected = all.Where(p => lessons.Any(l => l.Key == p.Package.RootKey)).ToList();
        Assert.Equal(36, selected.Count);
        foreach (var lesson in lessons)
        {
            var package = selected.Single(p => p.Package.RootKey == lesson.Key).Package;
            Assert.Equal(1, package.Version); Assert.Empty(package.DependsOn);
            Assert.Empty(package.Assets); Assert.Empty(package.DeleteKeys);
            Assert.Equal(1 + lesson.Sentences.Count + lesson.Sentences.Sum(s => s.ApprovedAlternatives.Count), package.Records.Count);
            Assert.All(package.Records, r => Assert.Contains(r.Kind, new[] { "WritingExercise", "WritingExerciseSentence", "WritingAcceptedAnswer" }));
            var root = package.Records.Single(r => r.Kind == "WritingExercise");
            Assert.True(root.Values["IsPublished"].GetBoolean());
            Assert.False(root.Values.ContainsKey("UserId")); Assert.Empty(root.References);
            var sentences = package.Records.Where(r => r.Kind == "WritingExerciseSentence").OrderBy(r => r.Values["SortOrder"].GetInt32()).ToList();
            Assert.Equal(lesson.Sentences.Select(s => s.VietnameseText), sentences.Select(s => s.Values["VietnameseText"].GetString()));
            Assert.Equal(lesson.Sentences.Select(s => s.EnglishMeaning), sentences.Select(s => s.Values["EnglishMeaning"].GetString()));
            Assert.Equal(lesson.Sentences.Select(s => s.BreakAfter), sentences.Select(s => s.Values["BreakAfter"].GetBoolean()));
            var variant = package.Records.Single(r => r.Kind == "WritingAcceptedAnswer");
            Assert.Equal("owner:system", variant.References["ApprovedByUserId"]);
            Assert.Equal(lesson.Sentences.SelectMany(s => s.ApprovedAlternatives).Single(), variant.Values["AnswerText"].GetString());
            Assert.Equal(WritingAssessmentPolicy.ComputeNormalizedHash(variant.Values["AnswerText"].GetString()), variant.Values["NormalizedHash"].GetString());
        }
    }

    [Theory]
    [InlineData("beginner", 8)]
    [InlineData("intermediate", 12)]
    [InlineData("advanced", 12)]
    public async Task SixFormatPilots_ArePublic_PersistProgress_CompleteOnce_AndAcceptVariants(string level, int sentenceCount)
    {
        await using var factory = new TestWebApplicationFactory(services => services.AddSingleton<IOptions<WritingEvaluationOptions>>(
            Options.Create(new WritingEvaluationOptions { EvaluationMode = "Local", CloudEnhancementEnabled = false })));
        await factory.InitializeAsync();
        var pilots = Read(level).GroupBy(x => x.ContentType).Select(g => g.First()).ToList();
        var ids = new List<int>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var rows = pilots.Select(l => WritingContentBatch.Create(l, TestDataIds.AdminUserId)).ToList();
            db.WritingExercises.AddRange(rows); await db.SaveChangesAsync(); ids.AddRange(rows.Select(x => x.Id));
        }
        using var anonymous = factory.CreateClient();
        for (var i = 0; i < pilots.Count; i++)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
            var row = await db.WritingExercises.AsNoTracking().Include(x => x.WritingExerciseSentences)
                .ThenInclude(s => s.WritingAcceptedAnswers).SingleAsync(x => x.Id == ids[i]);
            WritingContentBatch.AssertMatches(pilots[i], row);
            var practice = await service.GetWritingPracticeDataAsync(row.Id, TestDataIds.UserId);
            Assert.Equal(sentenceCount, practice!.Sentences.Count);
            Assert.Equal(pilots[i].Sentences.Select(s => s.VietnameseText), practice.Sentences.Select(s => s.VietnameseText));
            using var response = await anonymous.GetAsync("/Home/Writing/Exercises/Data?level=" + level + "&contentType=" + row.ContentType);
            response.EnsureSuccessStatusCode();
            using var publicData = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Contains(publicData.RootElement.GetProperty("exercises").EnumerateArray(), x => x.GetProperty("id").GetInt32() == row.Id);
            foreach (var sentence in row.WritingExerciseSentences.OrderBy(s => s.SortOrder))
            {
                var answer = sentence.WritingAcceptedAnswers.FirstOrDefault()?.AnswerText ?? sentence.EnglishMeaning;
                var result = await service.EvaluateWritingSentenceAsync(row.Id, sentence.Id, answer, TestDataIds.UserId);
                Assert.True(result!.Passed);
                Assert.False(result.UsedAi);
                Assert.Equal(sentence.SortOrder == sentenceCount ? "completed" : "in-progress", result.ExerciseStatusKey);
            }
            var final = row.WritingExerciseSentences.Single(s => s.SortOrder == sentenceCount);
            Assert.True((await service.EvaluateWritingSentenceAsync(row.Id, final.Id, final.EnglishMeaning, TestDataIds.UserId))!.Passed);
            var reload = await service.GetWritingPracticeDataAsync(row.Id, TestDataIds.UserId);
            Assert.Equal(sentenceCount, reload!.CompletedSentenceCount);
            Assert.Equal("completed", reload.StatusKey);
            // The seed catalog can already occupy page one; new lessons must also be reachable on later pages.
            var catalog = await service.GetWritingExerciseListViewModelAsync(level, row.ContentType, "all", 1, TestDataIds.UserId);
            if (!catalog.Exercises.Any(x => x.Id == row.Id))
                catalog = await service.GetWritingExerciseListViewModelAsync(level, row.ContentType, "all", catalog.TotalPages, TestDataIds.UserId);
            Assert.Contains(catalog.Exercises, x => x.Id == row.Id && x.SentenceCount == sentenceCount && !x.IsPrivate);
        }
        using var verify = factory.Services.CreateScope();
        var stored = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(6, await stored.LearningCompletionCredits.CountAsync(c => c.UserId == TestDataIds.UserId));
        Assert.Equal(6 * sentenceCount, await stored.UserWritingSentenceProgresses.CountAsync(p => p.UserId == TestDataIds.UserId && ids.Contains(p.WritingExerciseId)));
    }
}
