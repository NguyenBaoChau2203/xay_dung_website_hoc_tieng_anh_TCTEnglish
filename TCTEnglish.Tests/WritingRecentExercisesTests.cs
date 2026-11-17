using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTEnglish.Models;
using TCTEnglish.Services.Accounts;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingRecentExercisesTests
{
    [Fact]
    public async Task TemporaryRecentReadFailureKeepsCatalogAndOffersRealRetry()
    {
        await using var factory = new TestWebApplicationFactory();
        var failure = new RecentReadFailure(); factory.SaveInterceptors.Add(failure);
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        failure.Enabled = true;
        var html = await client.GetStringAsync("/Home/Writing/Exercises");
        Assert.Contains("Chưa tải được lịch sử", WebUtility.HtmlDecode(html));
        Assert.Contains("exerciseId=1", html);
        failure.Enabled = false;
        var data = await client.GetFromJsonAsync<JsonElement>("/api/writing/recent");
        Assert.True(data.GetProperty("success").GetBoolean());
    }
    private sealed class RecentReadFailure : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("UserWritingExerciseVisits", StringComparison.Ordinal))
                throw new Microsoft.Data.Sqlite.SqliteException("Controlled recent read failure", 1);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task CompletedExerciseRemainsReplayableAndUsesOnlyCurrentUsersAcceptedProgress()
    {
        await using var factory = new TestWebApplicationFactory(); await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var exercise = new WritingExercise { Title = "Completed fixture", Level = "advanced", ContentType = "stories", Topic = "Fixture",
            SourceType = "fixture", PreviewText = "Fixture", IsPublished = true, CreatedAt = DateTime.UtcNow,
            WritingExerciseSentences = [new() { SortOrder = 1, VietnameseText = "Xin chào", EnglishMeaning = "Hello!" }] };
        db.Add(exercise); await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        Assert.True((await service.EvaluateWritingSentenceAsync(exercise.Id, exercise.WritingExerciseSentences.Single().Id, "Hello!", TestDataIds.UserId))!.Passed);
        var before = await RewardSnapshot(db);
        await service.RecordExerciseVisitAsync(TestDataIds.UserId, exercise.Id);
        var recent = Assert.Single(await service.GetRecentExercisesAsync(TestDataIds.UserId));
        Assert.Equal("completed", recent.StatusKey); Assert.Equal("Luyện lại", recent.StartActionLabel);
        Assert.Equal(1, recent.CompletedSentenceCount);
        await service.RecordExerciseVisitAsync(TestDataIds.OutsiderUserId, exercise.Id);
        var other = Assert.Single(await service.GetRecentExercisesAsync(TestDataIds.OutsiderUserId));
        Assert.Equal("not-started", other.StatusKey); Assert.Equal(0, other.AttemptCount);
        Assert.Equal(before, await RewardSnapshot(db));
    }
    [Fact]
    public async Task FirstInsertBetweenUpdateAndExistenceCheckKeepsNewestTime()
    {
        var clock = new GoalsStage03RegressionTests.TestClock(new DateTime(2026, 11, 15, 2, 0, 0));
        await using var factory = new TestWebApplicationFactory(s => GoalsStage03RegressionTests.Configure(s, clock));
        var race = new CompetingInsert(); factory.SaveInterceptors.Add(race);
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IWritingService>().RecordExerciseVisitAsync(TestDataIds.UserId, 1);
        Assert.True(race.Inserted);
        Assert.Equal(clock.Now, stored);
    }

    internal sealed class CompetingInsert : DbCommandInterceptor
    {
        public bool Inserted { get; private set; }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (!Inserted && result == 0 && command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.Ordinal)
                && command.CommandText.Contains("UserWritingExerciseVisits", StringComparison.Ordinal))
            {
                Inserted = true;
                await using var insert = command.Connection!.CreateCommand();
                insert.Transaction = command.Transaction;
                insert.CommandText = "INSERT INTO UserWritingExerciseVisits (UserId,WritingExerciseId,LastOpenedAtUtc) VALUES (@user,@exercise,'2000-01-01 00:00:00')";
                foreach (var (part, target) in new[] { ("userId", "@user"), ("exerciseId", "@exercise") })
                {
                    var original = command.Parameters.Cast<DbParameter>().Single(p => p.ParameterName.Contains(part, StringComparison.Ordinal));
                    var parameter = insert.CreateParameter(); parameter.ParameterName = target; parameter.Value = original.Value;
                    insert.Parameters.Add(parameter);
                }
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            return result;
        }
    }

    [Fact]
    public async Task GenuineInsertFailurePropagatesAndRollsBack()
    {
        await using var factory = new TestWebApplicationFactory(); await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER recent_failure BEFORE INSERT ON UserWritingExerciseVisits BEGIN SELECT RAISE(ABORT, 'controlled visit failure'); END");
        await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<IWritingService>().RecordExerciseVisitAsync(TestDataIds.UserId, 1));
        Assert.Empty(await db.UserWritingExerciseVisits.AsNoTracking().ToListAsync());
    }
    internal static async Task<string> RewardSnapshot(DbflashcardContext db)
    {
        var tables = new[] { "Users", "UserWritingAttempts", "UserWritingExerciseProgresses", "UserWritingSentenceProgresses",
            "UserDailyActivities", "UserGoals", "UserBadges", "LearningCompletionCredits", "LearningNotificationOutbox", "Notifications" };
        await db.Database.OpenConnectionAsync();
        var rows = new List<string>();
        foreach (var table in tables)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT * FROM {table}";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                rows.Add(table + ":" + string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))));
        }
        return string.Join("\n", rows.OrderBy(r => r, StringComparer.Ordinal));
    }

    internal static async Task AssertOrderAccessAndIsolation(TestWebApplicationFactory factory, GoalsStage03RegressionTests.TestClock clock)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        var exercises = Enumerable.Range(0, 9).Select(i => new WritingExercise
        {
            Title = "<script>hostile</script> " + new string('x', 100) + i, Level = i % 2 == 0 ? "advanced" : "intermediate",
            ContentType = i % 2 == 0 ? "stories" : "reports", Topic = "Synthetic", SourceType = "fixture", PreviewText = "Keep",
            IsPublished = true, CreatedAt = clock.Now,
            WritingExerciseSentences = [new() { SortOrder = 1, VietnameseText = "Xin chào", EnglishMeaning = "Hello!" }]
        }).ToList();
        db.WritingExercises.AddRange(exercises); await db.SaveChangesAsync();
        var rewards = await RewardSnapshot(db);
        for (var i = 0; i < 7; i++)
        {
            clock.Now = clock.Now.AddSeconds(1);
            Assert.NotNull(await service.RecordExerciseVisitAsync(TestDataIds.UserId, exercises[i].Id));
        }
        Assert.Equal(exercises.Take(7).Reverse().Take(6).Select(e => e.Id), (await service.GetRecentExercisesAsync(TestDataIds.UserId)).Select(e => e.Id));
        clock.Now = clock.Now.AddSeconds(1);
        await service.RecordExerciseVisitAsync(TestDataIds.UserId, exercises[0].Id);
        var recent = await service.GetRecentExercisesAsync(TestDataIds.UserId);
        Assert.Equal(new[] { exercises[0].Id, exercises[6].Id }, recent.Take(2).Select(e => e.Id));
        Assert.Equal(7, await db.UserWritingExerciseVisits.CountAsync());
        Assert.Empty(await service.GetRecentExercisesAsync(TestDataIds.OutsiderUserId));
        Assert.Equal(rewards, await RewardSnapshot(db));
        // Remove access to newer rows: filtering must happen BEFORE Take(6).
        exercises[6].IsPublished = false;
        exercises[5].UserId = TestDataIds.OutsiderUserId;
        await db.SaveChangesAsync();
        recent = await service.GetRecentExercisesAsync(TestDataIds.UserId);
        Assert.Equal(5, recent.Count);
        Assert.DoesNotContain(recent, r => r.Id == exercises[6].Id || r.Id == exercises[5].Id);
        Assert.Null(await service.RecordExerciseVisitAsync(TestDataIds.UserId, exercises[5].Id));
        Assert.Null(await service.RecordExerciseVisitAsync(TestDataIds.UserId, exercises[6].Id));
        // Eligible owner's unpublished private lesson; deliberate Admin override.
        exercises[7].UserId = TestDataIds.UserId; exercises[7].IsPublished = false;
        var user = await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
        user.Role = Roles.Premium; await db.SaveChangesAsync();
        Assert.NotNull(await service.RecordExerciseVisitAsync(user.UserId, exercises[7].Id));
        Assert.NotNull(await service.RecordExerciseVisitAsync(TestDataIds.AdminUserId, exercises[5].Id));
        Assert.Equal(exercises[5].Id, Assert.Single(await service.GetRecentExercisesAsync(TestDataIds.AdminUserId)).Id);
        user.Role = Roles.Standard; await db.SaveChangesAsync();
        Assert.DoesNotContain(await service.GetRecentExercisesAsync(user.UserId), r => r.Id == exercises[7].Id);
        user.Status = UserStatus.Blocked; await db.SaveChangesAsync();
        Assert.Empty(await service.GetRecentExercisesAsync(user.UserId));
        Assert.Null(await service.RecordExerciseVisitAsync(user.UserId, exercises[0].Id));
        user.Status = UserStatus.Offline; await db.SaveChangesAsync();
        await db.WritingExerciseSentences.Where(s => s.WritingExerciseId == exercises[0].Id).ExecuteDeleteAsync();
        Assert.DoesNotContain(await service.GetRecentExercisesAsync(user.UserId), r => r.Id == exercises[0].Id);
        Assert.Null(await service.RecordExerciseVisitAsync(user.UserId, exercises[0].Id));
        await db.UserWritingExerciseVisits.Where(v => v.UserId == user.UserId).ExecuteUpdateAsync(s => s.SetProperty(v => v.LastOpenedAtUtc, clock.Now));
        var tied = await service.GetRecentExercisesAsync(user.UserId);
        Assert.Equal(tied.Select(r => r.Id).OrderDescending(), tied.Select(r => r.Id));
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, user.UserId, Roles.Standard);
        var html = await client.GetStringAsync("/Home/Writing/Exercises?level=beginner&contentType=emails&topic=missing&status=completed&page=9&myPage=9");
        Assert.Contains("data-recent-exercise-id=\"" + exercises[4].Id + "\"", html);
        Assert.Contains("&lt;script&gt;hostile&lt;/script&gt;", html);
        using var opened = await client.GetAsync(tied.First().PracticeUrl);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
    }

    [Fact]
    public async Task OrderLimitAccessIsolationFiltersAndCrossLevelLinks()
    {
        var clock = new GoalsStage03RegressionTests.TestClock(new DateTime(2026, 11, 15, 1, 0, 0));
        await using var factory = new TestWebApplicationFactory(s => GoalsStage03RegressionTests.Configure(s, clock));
        await factory.InitializeAsync();
        await AssertOrderAccessAndIsolation(factory, clock);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealAccountCleanup(bool keepLedger)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await AssertCleanup(factory, keepLedger);
    }

    internal static async Task AssertCleanup(TestWebApplicationFactory factory, bool keepLedger, bool concurrent = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = new User { Email = Guid.NewGuid() + "@example.invalid", Role = Roles.Standard,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("RecentFixture1!") };
        db.Users.Add(user); await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        await service.RecordExerciseVisitAsync(user.UserId, 1);
        if (keepLedger)
            db.PaymentOrders.Add(new PaymentOrder { UserId = user.UserId,
                Plan = new PremiumPlan { Code = Guid.NewGuid().ToString("N"), Name = "Fixture", Description = "Fixture", DurationDays = 1 },
                OrderCode = Guid.NewGuid().ToString("N"), Provider = "fixture", Status = "Paid",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var deletion = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(user.UserId, "CONFIRM", "RecentFixture1!", null);
        if (concurrent)
        {
            using var racingScope = factory.Services.CreateScope();
            var visit = racingScope.ServiceProvider.GetRequiredService<IWritingService>().RecordExerciseVisitAsync(user.UserId, 1);
            await Task.WhenAll(deletion, visit).WaitAsync(TimeSpan.FromSeconds(30));
        }
        var result = await deletion;
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(!keepLedger, result.Removed);
        Assert.False(await db.UserWritingExerciseVisits.AnyAsync(v => v.UserId == user.UserId));
        Assert.Null(await service.RecordExerciseVisitAsync(user.UserId, 1));
    }
    [Fact]
    public async Task VisitWithoutSubmission_IsPersonal_ReadOnlyGets_DoNotGrantProgress_AndNeedsCsrf()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        const string practice = "/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1";
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, practice);
        using var before = await client.GetAsync("/api/writing/recent");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Empty((await before.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").EnumerateArray());
        Assert.True(before.Headers.CacheControl?.NoStore);
        using var missing = await client.PostAsync("/api/writing/1/recent", null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/writing/1/recent");
        request.Headers.Add("RequestVerificationToken", token);
        request.Content = JsonContent.Create(new { userId = TestDataIds.OutsiderUserId, lastOpenedAtUtc = "2099-01-01" });
        using var saved = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.EndsWith("Z", (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lastOpenedAtUtc").GetString());
        var data = (await client.GetFromJsonAsync<JsonElement>("/api/writing/recent")).GetProperty("data");
        var row = Assert.Single(data.EnumerateArray());
        Assert.Equal(1, row.GetProperty("id").GetInt32());
        Assert.Equal(0, row.GetProperty("attemptCount").GetInt32());
        Assert.Equal("not-started", row.GetProperty("statusKey").GetString());
        using var other = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);
        Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/writing/recent")).GetProperty("data").EnumerateArray());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Empty(await db.UserWritingAttempts.ToListAsync());
        Assert.Empty(await db.UserWritingExerciseProgresses.ToListAsync());
        Assert.Empty(await db.LearningCompletionCredits.ToListAsync());
    }

    [Fact]
    public async Task GuestCatalogHasNoPersonalSectionOrRequests()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var html = await client.GetStringAsync("/Home/Writing/Exercises");
        Assert.DoesNotContain("data-writing-recent-list", html);
        using var response = await client.GetAsync("/api/writing/recent");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
