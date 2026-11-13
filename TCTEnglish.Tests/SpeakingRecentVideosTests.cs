using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public class SpeakingRecentVideosTests
{
    private sealed class FakeYoutubeTranscriptService : IYoutubeTranscriptService
    {
        public Task<List<SpeakingSentence>> GetTranscriptAsync(string youtubeId)
            => Task.FromResult(new List<SpeakingSentence>());
        public Task<YoutubeTranscriptResult> GetTranscriptForSpeakingImportAsync(string youtubeId, CancellationToken ct = default)
            => Task.FromResult(new YoutubeTranscriptResult(new List<SpeakingSentence>(), false, null));
        public Task<YoutubeTranscriptResult> GetEnglishCaptionsOnlyAsync(string youtubeId, CancellationToken ct = default)
            => Task.FromResult(new YoutubeTranscriptResult(new List<SpeakingSentence>(), false, null));
        public Task<string?> GetVideoDurationAsync(string youtubeId)
            => Task.FromResult<string?>("01:00");
        public Task<YoutubeVideoMetadata?> GetVideoMetadataAsync(string youtubeId)
            => Task.FromResult<YoutubeVideoMetadata?>(new YoutubeVideoMetadata("Title", null));
    }

    private static async Task<DbflashcardContext> CreateSqliteContextAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseSqlite("Data Source=:memory:")
            .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>()
            .Options;
        var context = new DbflashcardContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task R01_GetRecentVideos_WithNoVisits_ReturnsEmpty()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = await CreateSqliteContextAsync(dbName);
        var service = new SpeakingService(context, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);

        var result = await service.GetRecentVideosAsync(userId: 101, limit: 6);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task R02_RecordVideoVisit_PublicVideo_CreatesSingleVisit()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = await CreateSqliteContextAsync(dbName);
        var user = new User { PasswordHash = "synthetic-only", UserId = 101, Email = "u1@test.com", Role = Roles.Standard, FullName = "U1" };
        var video = new SpeakingVideo
        {
            Id = 1,
            Title = "Public Video 1",
            YoutubeId = "abc1",
            Level = "A1",
            Topic = "General",
            OwnerUserId = null,
            SpeakingSentences = [new SpeakingSentence { Text = "Sentence 1", StartTime = 0, EndTime = 2 }]
        };
        context.Users.Add(user);
        context.SpeakingVideos.Add(video);
        await context.SaveChangesAsync();

        var service = new SpeakingService(context, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);
        var recordedAt = await service.RecordVideoVisitAsync(101, 1);

        Assert.NotNull(recordedAt);
        var visits = await context.UserSpeakingVideoVisits.Where(v => v.UserId == 101).ToListAsync();
        Assert.Single(visits);
        Assert.Equal(1, visits[0].VideoId);
        Assert.Equal(recordedAt.Value, visits[0].LastOpenedAtUtc);
    }

    [Fact]
    public async Task R03_Visit_A_B_A_Produces_A_B_Order_With_Single_Row_For_A()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = await CreateSqliteContextAsync(dbName);
        var user = new User { PasswordHash = "synthetic-only", UserId = 101, Email = "u1@test.com", Role = Roles.Standard, FullName = "U1" };
        var videoA = new SpeakingVideo { Id = 1, Title = "Video A", YoutubeId = "vidA", SpeakingSentences = [new SpeakingSentence { Text = "A", StartTime = 0, EndTime = 2 }] };
        var videoB = new SpeakingVideo { Id = 2, Title = "Video B", YoutubeId = "vidB", SpeakingSentences = [new SpeakingSentence { Text = "B", StartTime = 0, EndTime = 2 }] };
        context.Users.Add(user);
        context.SpeakingVideos.AddRange(videoA, videoB);
        await context.SaveChangesAsync();

        var service = new SpeakingService(context, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);

        // Open A
        var timeA1 = await service.RecordVideoVisitAsync(101, 1);
        await Task.Delay(10);
        // Open B
        var timeB = await service.RecordVideoVisitAsync(101, 2);
        await Task.Delay(10);
        // Open A again
        var timeA2 = await service.RecordVideoVisitAsync(101, 1);

        Assert.True(timeA2 > timeA1);
        Assert.True(timeA2 > timeB);

        var visits = await context.UserSpeakingVideoVisits.Where(v => v.UserId == 101).ToListAsync();
        Assert.Equal(2, visits.Count);

        var recents = await service.GetRecentVideosAsync(101, 6);
        Assert.Equal(2, recents.Count);
        Assert.Equal(1, recents[0].Id); // A is newest
        Assert.Equal(2, recents[1].Id); // B is second
    }

    [Fact]
    public async Task R04_More_Than_Six_Visits_Returns_Newest_Six()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = await CreateSqliteContextAsync(dbName);
        var user = new User { PasswordHash = "synthetic-only", UserId = 101, Email = "u1@test.com", Role = Roles.Standard, FullName = "U1" };
        context.Users.Add(user);

        for (int i = 1; i <= 8; i++)
        {
            context.SpeakingVideos.Add(new SpeakingVideo
            {
                Id = i,
                Title = $"Video {i}",
                YoutubeId = $"vid{i}",
                SpeakingSentences = [new SpeakingSentence { Text = $"Sentence {i}", StartTime = 0, EndTime = 2 }]
            });
        }
        await context.SaveChangesAsync();

        var service = new SpeakingService(context, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);
        for (int i = 1; i <= 8; i++)
        {
            await service.RecordVideoVisitAsync(101, i);
            await Task.Delay(5);
        }

        var recents = await service.GetRecentVideosAsync(101, 6);
        Assert.Equal(6, recents.Count);
        // Newest opened were 8, 7, 6, 5, 4, 3
        var ids = recents.Select(r => r.Id).ToList();
        Assert.Equal(new[] { 8, 7, 6, 5, 4, 3 }, ids);
    }

    [Fact]
    public async Task R05_Account_Isolation_UserB_Cannot_See_UserA_Visits()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = await CreateSqliteContextAsync(dbName);
        var userA = new User { PasswordHash = "synthetic-only", UserId = 101, Email = "uA@test.com", Role = Roles.Standard, FullName = "UA" };
        var userB = new User { PasswordHash = "synthetic-only", UserId = 102, Email = "uB@test.com", Role = Roles.Standard, FullName = "UB" };
        var video = new SpeakingVideo { Id = 1, Title = "Video 1", YoutubeId = "vid1", SpeakingSentences = [new SpeakingSentence { Text = "S", StartTime = 0, EndTime = 2 }] };
        context.Users.AddRange(userA, userB);
        context.SpeakingVideos.Add(video);
        await context.SaveChangesAsync();

        var service = new SpeakingService(context, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);
        await service.RecordVideoVisitAsync(101, 1);

        var recentsA = await service.GetRecentVideosAsync(101, 6);
        var recentsB = await service.GetRecentVideosAsync(102, 6);

        Assert.Single(recentsA);
        Assert.Empty(recentsB);
    }

    [Fact]
    public async Task R06_Private_Video_Access_Owner_Can_Visit_Other_User_Gets_NotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = await CreateSqliteContextAsync(dbName);
        var userA = new User { PasswordHash = "synthetic-only", UserId = 101, Email = "uA@test.com", Role = Roles.Premium, FullName = "UA" };
        var userB = new User { PasswordHash = "synthetic-only", UserId = 102, Email = "uB@test.com", Role = Roles.Premium, FullName = "UB" };
        var privateVideoA = new SpeakingVideo
        {
            Id = 1,
            Title = "Private Video A",
            YoutubeId = "privA",
            OwnerUserId = 101,
            SpeakingSentences = [new SpeakingSentence { Text = "S", StartTime = 0, EndTime = 2 }]
        };
        context.Users.AddRange(userA, userB);
        context.SpeakingVideos.Add(privateVideoA);
        await context.SaveChangesAsync();

        var service = new SpeakingService(context, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);

        // User A visits their own private video -> success
        var resultA = await service.RecordVideoVisitAsync(101, 1);
        Assert.NotNull(resultA);

        // User B attempts to visit User A's private video -> returns null (not found/inaccessible)
        var resultB = await service.RecordVideoVisitAsync(102, 1);
        Assert.Null(resultB);

        var visitsB = await context.UserSpeakingVideoVisits.Where(v => v.UserId == 102).ToListAsync();
        Assert.Empty(visitsB);
    }

    [Fact]
    public async Task R07_Role_Downgrade_Hides_Private_Visits_Before_Limiting()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = await CreateSqliteContextAsync(dbName);
        var user = new User { PasswordHash = "synthetic-only", UserId = 101, Email = "u1@test.com", Role = Roles.Premium, FullName = "U1" };
        context.Users.Add(user);

        // 1 private video owned by 101
        var privVideo = new SpeakingVideo { Id = 1, Title = "Private Video", YoutubeId = "priv", OwnerUserId = 101, SpeakingSentences = [new SpeakingSentence { Text = "P", StartTime = 0, EndTime = 2 }] };
        context.SpeakingVideos.Add(privVideo);

        // 6 public videos
        for (int i = 2; i <= 7; i++)
        {
            context.SpeakingVideos.Add(new SpeakingVideo { Id = i, Title = $"Public {i}", YoutubeId = $"pub{i}", SpeakingSentences = [new SpeakingSentence { Text = $"S{i}", StartTime = 0, EndTime = 2 }] });
        }
        await context.SaveChangesAsync();

        var service = new SpeakingService(context, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);
        // Visit public 2..7, then private 1 (so private is newest)
        for (int i = 2; i <= 7; i++)
        {
            await service.RecordVideoVisitAsync(101, i);
            await Task.Delay(5);
        }
        await service.RecordVideoVisitAsync(101, 1);

        // As Premium, newest 6 includes private video 1 and 5 public videos
        var premiumRecents = await service.GetRecentVideosAsync(101, 6);
        Assert.Equal(6, premiumRecents.Count);
        Assert.Equal(1, premiumRecents[0].Id);
        Assert.True(premiumRecents[0].IsPrivate);

        // Downgrade user to Standard
        user.Role = Roles.Standard;
        await context.SaveChangesAsync();

        // As Standard, private video 1 is hidden BEFORE Take(6); all 6 public videos fill the slots!
        var standardRecents = await service.GetRecentVideosAsync(101, 6);
        Assert.Equal(6, standardRecents.Count);
        Assert.DoesNotContain(standardRecents, r => r.Id == 1);
        Assert.Equal(new[] { 7, 6, 5, 4, 3, 2 }, standardRecents.Select(r => r.Id).ToArray());

        // Re-upgrade to Premium -> private video reappears at the top
        user.Role = Roles.Premium;
        await context.SaveChangesAsync();

        var reupgradedRecents = await service.GetRecentVideosAsync(101, 6);
        Assert.Equal(6, reupgradedRecents.Count);
        Assert.Equal(1, reupgradedRecents[0].Id);
    }

    [Fact]
    public async Task R08_Missing_Or_Inaccessible_Video_Returns_Null_And_Creates_No_Visit()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = await CreateSqliteContextAsync(dbName);
        var user = new User { PasswordHash = "synthetic-only", UserId = 101, Email = "u1@test.com", Role = Roles.Standard, FullName = "U1" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new SpeakingService(context, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);
        var result = await service.RecordVideoVisitAsync(101, 9999);

        Assert.Null(result);
        Assert.Empty(await context.UserSpeakingVideoVisits.ToListAsync());
    }

    [Fact]
    public async Task R10_Visit_Endpoint_Does_Not_Alter_Progress_Assessment_Or_Completions()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = await CreateSqliteContextAsync(dbName);
        var user = new User { PasswordHash = "synthetic-only", UserId = 101, Email = "u1@test.com", Role = Roles.Standard, FullName = "U1" };
        var video = new SpeakingVideo { Id = 1, Title = "Video 1", YoutubeId = "vid1", SpeakingSentences = [new SpeakingSentence { Text = "S", StartTime = 0, EndTime = 2 }] };
        context.Users.Add(user);
        context.SpeakingVideos.Add(video);
        await context.SaveChangesAsync();

        var service = new SpeakingService(context, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);
        await service.RecordVideoVisitAsync(101, 1);

        Assert.Empty(await context.UserSpeakingProgresses.ToListAsync());
        Assert.Empty(await context.SpeakingAssessments.ToListAsync());
        Assert.Empty(await context.UserSpeakingVideoCompletions.ToListAsync());
    }

    [Fact]
    public async Task R11_Deleting_Video_Removes_Visit_Rows()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = await CreateSqliteContextAsync(dbName);
        var user = new User { PasswordHash = "synthetic-only", UserId = 101, Email = "u1@test.com", Role = Roles.Premium, FullName = "U1" };
        var video = new SpeakingVideo { Id = 1, Title = "Video 1", YoutubeId = "vid1", OwnerUserId = 101, SpeakingSentences = [new SpeakingSentence { Text = "S", StartTime = 0, EndTime = 2 }] };
        context.Users.Add(user);
        context.SpeakingVideos.Add(video);
        await context.SaveChangesAsync();

        var service = new SpeakingService(context, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);
        await service.RecordVideoVisitAsync(101, 1);

        Assert.Single(await context.UserSpeakingVideoVisits.ToListAsync());

        var deleteResult = await service.DeleteOwnedVideoAsync(101, 1);
        Assert.True(deleteResult.IsSuccess);

        Assert.Empty(await context.UserSpeakingVideoVisits.ToListAsync());
    }

    [Fact]
    public async Task TiedDatesUseStableVideoId_AndAnOlderVisitCannotRegressTheStoredTime()
    {
        await using var db = await CreateSqliteContextAsync(Guid.NewGuid().ToString());
        db.Users.Add(new User { UserId = 101, Email = "stable@example.invalid", PasswordHash = "synthetic-only", Role = Roles.Standard });
        for (var i = 1; i <= 8; i++)
            db.SpeakingVideos.Add(new SpeakingVideo { Id = i, Title = $"V{i}", YoutubeId = $"stable{i}",
                SpeakingSentences = [new() { Text = "hello", StartTime = 0, EndTime = 1 }] });
        await db.SaveChangesAsync();
        var future = DateTime.UtcNow.AddHours(1);
        for (var i = 1; i <= 8; i++)
            db.UserSpeakingVideoVisits.Add(new() { UserId = 101, VideoId = i, LastOpenedAtUtc = future });
        await db.SaveChangesAsync();
        var service = new SpeakingService(db, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);
        Assert.Equal(future, await service.RecordVideoVisitAsync(101, 1));
        Assert.Equal(new[] { 8, 7, 6, 5, 4, 3 }, (await service.GetRecentVideosAsync(101, int.MaxValue)).Select(v => v.Id));
    }

    [Fact]
    public async Task UnknownDeletedBlockedUsersAndEmptyVideoCannotCreateAVisit()
    {
        await using var db = await CreateSqliteContextAsync(Guid.NewGuid().ToString());
        db.Users.Add(new User { UserId = 101, Email = "blocked@example.invalid", PasswordHash = "synthetic-only", Status = UserStatus.Blocked });
        db.SpeakingVideos.Add(new SpeakingVideo { Id = 1, Title = "Empty", YoutubeId = "empty" });
        await db.SaveChangesAsync();
        var service = new SpeakingService(db, new FakeYoutubeTranscriptService(), NullLogger<SpeakingService>.Instance);
        Assert.Null(await service.RecordVideoVisitAsync(9999, 1));
        Assert.Null(await service.RecordVideoVisitAsync(101, 1));
        var user = await db.Users.SingleAsync(); user.Status = UserStatus.Online;
        await db.SaveChangesAsync();
        Assert.Null(await service.RecordVideoVisitAsync(101, 1));
        Assert.Empty(await db.UserSpeakingVideoVisits.ToListAsync());
    }
}
