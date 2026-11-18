using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TCTEnglish.Services.Speaking;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

public sealed class SpeakingTranscriptDisplayTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 90)]
    [InlineData(false, 0)]
    [InlineData(false, 90)]
    [InlineData(true, 0, true)]
    [InlineData(true, 90, true)]
    [InlineData(false, 0, true)]
    [InlineData(false, 90, true)]
    public async Task PracticeRestoresOnlyTheOwnedConsumedAssessmentThatMatchesSavedProgress(bool isPrivate, double score, bool hasMetrics = false)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Users.AddRange(new User { UserId = 191, Email = "viewer@example.test", PasswordHash = "fixture", Role = "Premium" },
            new User { UserId = 192, Email = "other@example.test", PasswordHash = "fixture", Role = "Premium" });
        var video = new SpeakingVideo { Title = "Owned result", YoutubeId = "abcdefghijk", OwnerUserId = isPrivate ? 191 : null, CreatedAt = DateTime.UtcNow,
            SpeakingSentences = [new SpeakingSentence { Text = "Hello world", StartTime = 0, EndTime = 2, VietnameseMeaning = "Xin chào" }] };
        db.SpeakingVideos.Add(video); await db.SaveChangesAsync();
        // A receipt's submission TTL must not erase an already committed result.
        var sentence = video.SpeakingSentences.Single(); var committedAt = DateTime.UtcNow.AddHours(-2);
        db.UserSpeakingProgresses.Add(new() { UserId = 191, SentenceId = sentence.Id, TotalScore = score, AccuracyScore = score, PracticedAt = committedAt });
        SpeakingAssessment Receipt(int user, double receiptScore, DateTime at, bool consumed, string text) => new() {
            PublicId = Guid.NewGuid(), UserId = user, SentenceId = sentence.Id, TextMatchScore = receiptScore,
            CreatedAtUtc = at, ExpiresAtUtc = at.AddMinutes(10), ConsumedAtUtc = at, Consumed = consumed,
            Status = "Completed", SpeechDetected = true, TranscriptText = text, EngineName = "openai",
            FluencyScore = hasMetrics ? score : null, CompletenessScore = hasMetrics ? score : null,
            MetricVersion = hasMetrics ? 1 : null };
        db.SpeakingAssessments.AddRange(
            Receipt(191, score, committedAt, true, "Hello there"),
            Receipt(192, score, committedAt, true, "other users secret"),
            Receipt(191, score, committedAt, false, "uncommitted secret"),
            Receipt(191, score == 0 ? 20 : 40, committedAt.AddSeconds(1), true, "later different result"));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = new SpeakingService(db, Mock.Of<IYoutubeTranscriptService>(), NullLogger<SpeakingService>.Instance);
        var otherView = await service.GetSpeakingPracticeViewModelAsync(video.Id, 192);
        if (isPrivate) Assert.Null(otherView);
        else
        {
            var otherSentence = Assert.Single(otherView!.Sentences);
            Assert.Null(otherSentence.TotalScore);
            Assert.Null(otherSentence.Comparison);
        }
        var display = Assert.Single((await service.GetSpeakingPracticeViewModelAsync(video.Id, 191))!.Sentences);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(display, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.True(json.TryGetProperty("comparison", out var comparison), "Saved practice must include its word comparison.");
        Assert.True(comparison.GetProperty("available").GetBoolean());
        Assert.Contains("there", comparison.ToString());
        Assert.DoesNotContain("secret", comparison.ToString());
        Assert.DoesNotContain("later", comparison.ToString());
        Assert.Equal(hasMetrics, json.GetProperty("fluencyAvailable").GetBoolean());
        Assert.True(json.GetProperty("completenessAvailable").GetBoolean());
        Assert.Equal(hasMetrics ? score : 100, json.GetProperty("completenessScore").GetDouble());
        if (hasMetrics) Assert.Equal(score, json.GetProperty("fluencyScore").GetDouble());
        Assert.Equal(score, display.TotalScore);
        Assert.True(display.IsPracticed);
        Assert.Equal(4, await db.SpeakingAssessments.CountAsync());
    }

    [Fact]
    public async Task MultipleVideosAndSentencesKeepTheirOwnSavedResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new User { UserId = 191, Email = "viewer@example.test", PasswordHash = "fixture", Role = "Premium" });
        var videos = Enumerable.Range(0, 2).Select(i => new SpeakingVideo {
            Title = "Result fixture " + i, YoutubeId = i == 0 ? "abcdefghijk" : "abcdefghijl", OwnerUserId = i == 0 ? null : 191,
            CreatedAt = DateTime.UtcNow, SpeakingSentences = [
                new SpeakingSentence { Text = "The first lesson", StartTime = 0, EndTime = 2, VietnameseMeaning = string.Empty },
                new SpeakingSentence { Text = "The second lesson", StartTime = 2, EndTime = 4, VietnameseMeaning = string.Empty }
            ] }).ToList();
        db.SpeakingVideos.AddRange(videos); await db.SaveChangesAsync();
        var expected = new Dictionary<int, (double Score, string Token)>();
        foreach (var video in videos)
        foreach (var sentence in video.SpeakingSentences)
        {
            var at = DateTime.UtcNow.AddSeconds(sentence.Id); var score = sentence.Id * 10d;
            var token = "receipt" + sentence.Id;
            expected[sentence.Id] = (score, token);
            db.UserSpeakingProgresses.Add(new() { UserId = 191, SentenceId = sentence.Id, TotalScore = score, AccuracyScore = score, PracticedAt = at });
            db.SpeakingAssessments.Add(new() { PublicId = Guid.NewGuid(), UserId = 191, SentenceId = sentence.Id,
                TextMatchScore = score, CreatedAtUtc = at, ExpiresAtUtc = at.AddMinutes(1), ConsumedAtUtc = at,
                Consumed = true, Status = "Completed", TranscriptText = token, SpeechDetected = true });
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = new SpeakingService(db, Mock.Of<IYoutubeTranscriptService>(), NullLogger<SpeakingService>.Instance);
        foreach (var video in videos)
        foreach (var sentence in (await service.GetSpeakingPracticeViewModelAsync(video.Id, 191))!.Sentences)
        {
            Assert.Equal(expected[sentence.Id].Score, sentence.TotalScore);
            Assert.Contains(sentence.Comparison!.Operations, op => op.HypothesisToken == expected[sentence.Id].Token);
            Assert.DoesNotContain(sentence.Comparison.Operations, op => op.HypothesisToken.StartsWith("receipt") && op.HypothesisToken != expected[sentence.Id].Token);
        }
    }

    [Theory]
    [InlineData(">> Poor my baby. But today is special.", "Poor my baby. But today is special.")]
    [InlineData("Now listen carefully. >> Honey, I just came home from work.", "Now listen carefully. Honey, I just came home from work.")]
    [InlineData(">> Sure. Let's eat pasta at Jollibee. >> Okay, but before we go...", "Sure. Let's eat pasta at Jollibee. Okay, but before we go...")]
    [InlineData("  >>Hello!  >>  How are you?  ", "Hello! How are you?")]
    [InlineData(">> Hello.\r\n>> Goodbye!", "Hello.\r\nGoodbye!")]
    [InlineData("2 > 1; 8 >> 1; https://example.test/a>>b", "2 > 1; 8 >> 1; https://example.test/a>>b")]
    [InlineData("Don't change contractions, commas, or punctuation!", "Don't change contractions, commas, or punctuation!")]
    [InlineData(null, "")]
    public void SpeakerMarkersAreRemovedWithoutRewritingWordsOrOrdinaryOperators(string? input, string expected)
        => Assert.Equal(expected, TranscriptDisplayText.Format(input));

    [Fact]
    public async Task PracticeFormatsBothPublicAndOwnedPrivateCuesWhileKeepingSourceIdsTimingAndScores()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Users.AddRange(new User { UserId = 191, Email = "viewer@example.test", PasswordHash = "fixture", Role = "Premium" },
            new User { UserId = 192, Email = "other@example.test", PasswordHash = "fixture", Role = "Premium" });
        var video = new SpeakingVideo { Title = "Existing public", YoutubeId = "abcdefghijk", CreatedAt = DateTime.UtcNow,
            SpeakingSentences = [new SpeakingSentence { Text = ">> Sure. >> Okay, let's go!", StartTime = 12.3, EndTime = 18.4, VietnameseMeaning = "Được rồi." }] };
        var owned = new SpeakingVideo { Title = "Personal", YoutubeId = "abcdefghijl", CreatedAt = DateTime.UtcNow, OwnerUserId = 191,
            SpeakingSentences = [new SpeakingSentence { Text = ">> Private words.", StartTime = 1, EndTime = 4, VietnameseMeaning = string.Empty }] };
        db.SpeakingVideos.AddRange(video, owned); await db.SaveChangesAsync();
        var sentence = video.SpeakingSentences.Single();
        db.UserSpeakingProgresses.Add(new() { UserId = 191, SentenceId = sentence.Id, TotalScore = 91 }); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new SpeakingService(db, Mock.Of<IYoutubeTranscriptService>(), NullLogger<SpeakingService>.Instance);
        var model = (await service.GetSpeakingPracticeViewModelAsync(video.Id, 191))!;
        var display = Assert.Single(model.Sentences);
        Assert.Equal("Sure. Okay, let's go!", display.Text);
        Assert.Equal(sentence.Id, display.Id); Assert.Equal(12.3, display.StartTime); Assert.Equal(18.4, display.EndTime); Assert.Equal(91, display.TotalScore);
        Assert.Equal("Được rồi.", display.VietnameseMeaning);
        Assert.Equal("Private words.", Assert.Single((await service.GetSpeakingPracticeViewModelAsync(owned.Id,191))!.Sentences).Text);
        Assert.Null(await service.GetSpeakingPracticeViewModelAsync(owned.Id,192));
        Assert.Equal(">> Sure. >> Okay, let's go!", (await db.SpeakingSentences.AsNoTracking().SingleAsync(x=>x.Id==sentence.Id)).Text);
        Assert.Equal(91, (await db.UserSpeakingProgresses.AsNoTracking().SingleAsync()).TotalScore);
    }
}
