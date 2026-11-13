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
