using System.Net;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Accounts;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SpeakingRecentVideosIntegrationTests
{
    [Fact]
    public async Task FirstVisitRaceStillStoresTheLaterTimestampWhenAnotherInsertWinsBeforeExistenceCheck()
    {
        await using var factory = new TestWebApplicationFactory();
        var competingInsert = new CompetingVisitInsert();
        factory.SaveInterceptors.Add(competingInsert);
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var service = new SpeakingService(db, null!, NullLogger<SpeakingService>.Instance);
        var time = await service.RecordVideoVisitAsync(TestDataIds.UserId, TestDataIds.SpeakingVideoId);
        Assert.True(competingInsert.Inserted);
        Assert.True(time > DateTime.UtcNow.AddMinutes(-1));
    }

    private sealed class CompetingVisitInsert : DbCommandInterceptor
    {
        public bool Inserted { get; private set; }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (!Inserted && result == 0 && command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.Ordinal)
                && command.CommandText.Contains("UserSpeakingVideoVisits", StringComparison.Ordinal))
            {
                Inserted = true;
                await using var insert = command.Connection!.CreateCommand();
                insert.CommandText = "INSERT INTO UserSpeakingVideoVisits (UserId,VideoId,LastOpenedAtUtc) VALUES (101,701,'2000-01-01 00:00:00')";
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            return result;
        }
    }

    [Theory]
    [InlineData("/Speaking/Practice/701")]
    [InlineData("/Speaking/Practice?id=701")]
    public async Task PracticeGetDoesNotWrite_PostNeedsAntiforgery_AndReturnsServerUtc(string route)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, route);
        using (var scope = factory.Services.CreateScope())
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<DbflashcardContext>().UserSpeakingVideoVisits.ToListAsync());
        using var noToken = await client.PostAsync("/api/speaking/701/recent", null);
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/speaking/701/recent");
        request.Headers.Add("RequestVerificationToken", token);
        request.Content = JsonContent.Create(new { userId = TestDataIds.OutsiderUserId, lastOpenedAtUtc = "2099-01-01T00:00:00Z" });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.EndsWith("Z", body.GetProperty("lastOpenedAtUtc").GetString());
        Assert.True(body.GetProperty("lastOpenedAtUtc").GetDateTime() < DateTime.UtcNow.AddMinutes(1));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var row = Assert.Single(await db.UserSpeakingVideoVisits.ToListAsync());
            Assert.Equal(TestDataIds.UserId, row.UserId);
            Assert.Empty(await db.UserSpeakingProgresses.ToListAsync());
            Assert.Empty(await db.SpeakingAssessments.ToListAsync());
            Assert.Empty(await db.UserSpeakingVideoCompletions.ToListAsync());
            Assert.Empty(await db.LearningCompletionCredits.ToListAsync());
            Assert.Empty(await db.LearningActivitySessions.ToListAsync());
        }
        using var read = await client.GetAsync("/api/speaking/recent");
        Assert.True(read.Headers.CacheControl?.NoStore);
        Assert.Single((await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").EnumerateArray());
        using var other = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);
        var otherBody = await other.GetFromJsonAsync<JsonElement>("/api/speaking/recent");
        Assert.Empty(otherBody.GetProperty("data").EnumerateArray());
        using var page = await client.GetAsync("/Speaking?level=C2&search=no-such-lesson");
        var html = await page.Content.ReadAsStringAsync();
        Assert.True(page.Headers.CacheControl?.NoStore);
        Assert.Contains("data-recent-video-id=\"701\"", html);
        Assert.Contains("speaking-recent.js", html);
    }

    [Fact]
    public async Task GuestAndCrossOwnerOrLockedPrivateRequestsCannotWriteVisits()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var anonymous = await guest.GetAsync("/api/speaking/recent");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Speaking");
        int privateId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var video = new SpeakingVideo { Title = "secret lesson", YoutubeId = "secretabcde", OwnerUserId = TestDataIds.UserId,
                SpeakingSentences = [new() { Text = "secret", StartTime = 0, EndTime = 1 }] };
            db.SpeakingVideos.Add(video); await db.SaveChangesAsync(); privateId = video.Id;
        }
        foreach (var id in new[] { privateId, 999999 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/speaking/{id}/recent");
            request.Headers.Add("RequestVerificationToken", token);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var verify = factory.Services.CreateScope();
        Assert.Empty(await verify.ServiceProvider.GetRequiredService<DbflashcardContext>().UserSpeakingVideoVisits.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualAccountDeletionAndAnonymizationRemoveVisits(bool keepLedger)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await AssertAccountCleanupAsync(factory, keepLedger);
    }

    internal static async Task AssertAccountCleanupAsync(TestWebApplicationFactory factory, bool keepLedger)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = new User { Email = $"recent-{Guid.NewGuid():N}@example.invalid", FullName = "Synthetic history user",
            Role = Roles.Standard, PasswordHash = BCrypt.Net.BCrypt.HashPassword("SyntheticHistory1!") };
        db.Users.Add(user); await db.SaveChangesAsync();
        var videoId = await db.SpeakingVideos.Where(v => v.OwnerUserId == null).Select(v => v.Id).FirstAsync();
        db.UserSpeakingVideoVisits.AddRange(
            new() { UserId = user.UserId, VideoId = videoId, LastOpenedAtUtc = DateTime.UtcNow },
            new() { UserId = TestDataIds.OutsiderUserId, VideoId = videoId, LastOpenedAtUtc = DateTime.UtcNow });
        if (keepLedger)
        {
            var plan = new PremiumPlan { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic", Description = "Fixture", DurationDays = 1 };
            db.PaymentOrders.Add(new PaymentOrder { UserId = user.UserId, Plan = plan, OrderCode = Guid.NewGuid().ToString("N"),
                Provider = "fixture", Status = "Paid", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
        var result = await scope.ServiceProvider.GetRequiredService<IAccountDeletionService>()
            .DeleteAsync(user.UserId, "CONFIRM", "SyntheticHistory1!", null);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(!keepLedger, result.Removed);
        Assert.False(await db.UserSpeakingVideoVisits.AnyAsync(v => v.UserId == user.UserId));
        Assert.True(await db.UserSpeakingVideoVisits.AnyAsync(v => v.UserId == TestDataIds.OutsiderUserId));
        var retained = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == user.UserId);
        if (keepLedger) Assert.NotNull(retained?.AnonymizedAt); else Assert.Null(retained);
        // Allow the helper to run both paths against the same isolated provider fixture.
        await db.UserSpeakingVideoVisits.Where(v => v.UserId == TestDataIds.OutsiderUserId).ExecuteDeleteAsync();
    }
}
