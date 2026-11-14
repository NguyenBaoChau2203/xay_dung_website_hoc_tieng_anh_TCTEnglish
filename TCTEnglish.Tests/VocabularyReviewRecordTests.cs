using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// POST /Vocabulary/Review/Record and the spaced-repetition read models of the system Vocabulary pages.
/// System card 402 lives in set 302; user card 401 lives in the user's own set 301.
/// </summary>
public sealed class VocabularyReviewRecordTests
{
    private const int OutsiderSetId = 395;
    private const int OutsiderCardId = 4501;
    private const int ManyCardsStart = 5001;
    private static readonly string RecordUrl = "/Vocabulary/Review/Record";

    // ── outcomes on a new word ───────────────────────────────────────────

    [Theory]
    [InlineData("again", 0, "Learning", "Ôn lại sau 10 phút", 10)]
    [InlineData("hard", 1, "Reviewing", "Ôn lại sau 1 ngày", 24 * 60)]
    [InlineData("good", 1, "Reviewing", "Ôn lại sau 1 ngày", 24 * 60)]
    [InlineData("known", 4, "Mastered", "Ôn lại sau 14 ngày", 14 * 24 * 60)]
    public async Task NewWord_StoresTheLevelStatusAndDateForEachOutcome(
        string outcome, int level, string status, string label, int minutes)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var before = DateTime.UtcNow;

        var (code, body) = await PostAsync(client, await TokenAsync(client), TestDataIds.SystemCardId, outcome);

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Equal(TestDataIds.SystemCardId, body.GetProperty("cardId").GetInt32());
        Assert.Equal(level, body.GetProperty("level").GetInt32());
        Assert.Equal(label, body.GetProperty("nextReviewLabel").GetString());
        Assert.False(body.GetProperty("demoted").GetBoolean());
        Assert.True(body.TryGetProperty("stage", out _));
        Assert.True(body.TryGetProperty("stageLabel", out _));
        Assert.True(body.TryGetProperty("xpEarned", out _));
        Assert.True(body.TryGetProperty("streak", out _));

        var row = await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.SystemCardId);
        Assert.NotNull(row);
        Assert.Equal(level, row!.RepetitionCount);
        Assert.Equal(status, row.Status);
        AssertNear(before.AddMinutes(minutes), row.NextReviewDate);
        Assert.Equal(outcome == "again" ? 1 : 0, row.WrongCount ?? 0);
    }

    [Fact]
    public async Task OutcomeNames_AreCaseInsensitive()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var (code, body) = await PostAsync(client, await TokenAsync(client), TestDataIds.SystemCardId, "GOOD");

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(1, body.GetProperty("level").GetInt32());
    }

    // ── early review, promotion, demotion ────────────────────────────────

    [Fact]
    public async Task GoodTwiceInARow_SecondIsAnEarlyReviewAndChangesNothing()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var token = await TokenAsync(client);

        await PostAsync(client, token, TestDataIds.SystemCardId, "good");
        var first = await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.SystemCardId);
        var (code, body) = await PostAsync(client, token, TestDataIds.SystemCardId, "good");
        var second = await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.SystemCardId);

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(body.GetProperty("promoted").GetBoolean());
        Assert.Equal(1, body.GetProperty("level").GetInt32());
        Assert.Equal("Lịch ôn giữ nguyên", body.GetProperty("nextReviewLabel").GetString());
        Assert.Equal(first!.RepetitionCount, second!.RepetitionCount);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.NextReviewDate, second.NextReviewDate);
    }

    [Fact]
    public async Task ADueWord_IsPromotedOneLevel()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedProgressAsync(factory, TestDataIds.SystemCardId, "Reviewing", 2, DateTime.UtcNow.AddHours(-2));
        using var client = CreateClient(factory);
        var before = DateTime.UtcNow;

        var (code, body) = await PostAsync(client, await TokenAsync(client), TestDataIds.SystemCardId, "good");

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(body.GetProperty("promoted").GetBoolean());
        Assert.Equal(3, body.GetProperty("level").GetInt32());
        Assert.Equal("Ôn lại sau 7 ngày", body.GetProperty("nextReviewLabel").GetString());
        var row = await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.SystemCardId);
        Assert.Equal(3, row!.RepetitionCount);
        Assert.Equal("Reviewing", row.Status);
        AssertNear(before.AddDays(7), row.NextReviewDate);
    }

    [Fact]
    public async Task Again_DemotesToLevelZeroAndCountsTheMiss_EvenBeforeTheDueDate()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedProgressAsync(factory, TestDataIds.SystemCardId, "Reviewing", 3, DateTime.UtcNow.AddDays(5));
        using var client = CreateClient(factory);
        var before = DateTime.UtcNow;

        var (code, body) = await PostAsync(client, await TokenAsync(client), TestDataIds.SystemCardId, "again");

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(body.GetProperty("demoted").GetBoolean());
        Assert.False(body.GetProperty("promoted").GetBoolean());
        Assert.Equal(0, body.GetProperty("level").GetInt32());
        Assert.Equal("Ôn lại sau 10 phút", body.GetProperty("nextReviewLabel").GetString());
        var row = await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.SystemCardId);
        Assert.Equal(0, row!.RepetitionCount);
        Assert.Equal("Learning", row.Status);
        Assert.Equal(1, row.WrongCount);
        AssertNear(before.AddMinutes(10), row.NextReviewDate);
    }

    [Fact]
    public async Task ALegacyMasteredRow_IsReadAsLevelFourAndNormalisedOnTheNextRecord()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedProgressAsync(factory, TestDataIds.SystemCardId, "Mastered", 1, DateTime.UtcNow.AddMinutes(-1));
        using var client = CreateClient(factory);

        var (_, body) = await PostAsync(client, await TokenAsync(client), TestDataIds.SystemCardId, "good");

        Assert.Equal(5, body.GetProperty("level").GetInt32());
        var row = await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.SystemCardId);
        Assert.Equal(5, row!.RepetitionCount);
        Assert.Equal("Mastered", row.Status);
    }

    [Fact]
    public async Task ASuspendedRow_IsLeftUntouched()
    {
        await using var factory = await CreateFactoryAsync();
        var next = DateTime.UtcNow.AddDays(-1);
        await SeedProgressAsync(factory, TestDataIds.SystemCardId, "Reviewing", 2, next, suspended: true);
        using var client = CreateClient(factory);

        var (code, body) = await PostAsync(client, await TokenAsync(client), TestDataIds.SystemCardId, "good");

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(body.GetProperty("promoted").GetBoolean());
        Assert.Equal(2, body.GetProperty("level").GetInt32());
        var row = await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.SystemCardId);
        Assert.Equal(2, row!.RepetitionCount);
        Assert.Equal(next, row.NextReviewDate);
        Assert.Null(await LoadActivityAsync(factory));
    }

    // ── Goals ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GoalsActivity_IsRecordedOnceForANewWord_AndNotForAnEarlyReview()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var token = await TokenAsync(client);

        var (_, first) = await PostAsync(client, token, TestDataIds.SystemCardId, "good");
        await PostAsync(client, token, TestDataIds.SystemCardId, "good");
        await PostAsync(client, token, TestDataIds.SystemCardId, "hard");

        var activity = await LoadActivityAsync(factory);
        Assert.NotNull(activity);
        Assert.Equal(1, activity!.CardsReviewed);
        Assert.Equal(1, activity.NewCardsLearned);
        Assert.True(first.GetProperty("xpEarned").GetInt32() > 0);
    }

    // ── access and validation ────────────────────────────────────────────

    [Fact]
    public async Task AUserOwnedCard_IsNotFound_AndNothingIsWritten()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var (code, _) = await PostAsync(client, await TokenAsync(client), TestDataIds.UserCardId, "good");

        Assert.Equal(HttpStatusCode.NotFound, code);
        Assert.Null(await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.UserCardId));
    }

    [Fact]
    public async Task AnotherUsersPrivateSetCard_IsNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var (code, _) = await PostAsync(client, await TokenAsync(client), OutsiderCardId, "good");

        Assert.Equal(HttpStatusCode.NotFound, code);
        Assert.Null(await LoadProgressAsync(factory, TestDataIds.UserId, OutsiderCardId));
    }

    [Fact]
    public async Task AMissingCard_IsNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var (code, _) = await PostAsync(client, await TokenAsync(client), 999_999, "good");

        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    [Fact]
    public async Task AMissingAntiforgeryToken_IsRejectedAndNothingIsWritten()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var (code, _) = await PostAsync(client, null, TestDataIds.SystemCardId, "good");

        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Null(await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.SystemCardId));
    }

    [Theory]
    [InlineData("""{"cardId":402,"outcome":"easy"}""")]
    [InlineData("""{"cardId":402,"outcome":"perfect"}""")]
    [InlineData("""{"cardId":402,"outcome":"1"}""")]
    [InlineData("""{"cardId":402,"outcome":" good"}""")]
    [InlineData("""{"cardId":402,"outcome":""}""")]
    [InlineData("""{"cardId":402}""")]
    [InlineData("""{"cardId":0,"outcome":"good"}""")]
    [InlineData("""{"cardId":-3,"outcome":"good"}""")]
    [InlineData("""{"outcome":"good"}""")]
    [InlineData("""{"cardId":"abc","outcome":"good"}""")]
    [InlineData("""not json""")]
    public async Task InvalidBodies_AreBadRequests(string json)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var (code, _) = await PostRawAsync(client, await TokenAsync(client), json);

        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Null(await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.SystemCardId));
    }

    [Fact]
    public async Task AnEmptyBody_IsABadRequest()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Post, RecordUrl) { Content = new StringContent(string.Empty, Encoding.UTF8, "application/json") };
        request.Headers.Add("RequestVerificationToken", await TokenAsync(client));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_IsDenied()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Post, RecordUrl)
        {
            Content = new StringContent("""{"cardId":402,"outcome":"good"}""", Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.SystemCardId));
    }

    [Fact]
    public async Task TheLegacyEndpoint_StillWorksForAPersonalCard()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var token = await TokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/LearningApi/record")
        {
            Content = new StringContent(
                $$"""{"cardId":{{TestDataIds.UserCardId}},"masteryLevel":"good","timestamp":"2026-11-14T00:00:00Z"}""",
                Encoding.UTF8, "application/json")
        };
        request.Headers.Add("RequestVerificationToken", token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = await LoadProgressAsync(factory, TestDataIds.UserId, TestDataIds.UserCardId);
        Assert.Equal("Reviewing", row!.Status);
    }

    // ── read models ──────────────────────────────────────────────────────

    [Fact]
    public async Task Home_ReportsMemoryForecastAndDueFigures()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        // 4 stages, one of each; three of them due now or overdue, one in three days.
        await SeedProgressAsync(factory, 402, "Learning", 0, now.AddMinutes(-3));
        await SeedProgressAsync(factory, ManyCardsStart, "Reviewing", 2, now.AddDays(-2));
        await SeedProgressAsync(factory, ManyCardsStart + 1, "Mastered", 5, now.AddDays(3));
        await SeedProgressAsync(factory, ManyCardsStart + 2, "Mastered", 6, now.AddMinutes(-1));
        await SeedProgressAsync(factory, ManyCardsStart + 3, "Reviewing", 3, now.AddDays(-1), suspended: true);

        var home = await GetHomeAsync(factory);

        Assert.Equal(3, home.DueCount);
        Assert.Equal(1, home.DueMinutes); // ceil(3 x 15 s / 60) = 1
        Assert.Equal(5, home.LearnedCount);
        Assert.Equal(10, home.DailyTarget);
        Assert.Equal(1, home.Memory.New);
        Assert.Equal(2, home.Memory.Growing); // level 2 and the suspended level 3
        Assert.Equal(1, home.Memory.Strong);
        Assert.Equal(1, home.Memory.Long);
        Assert.Equal(5, home.Memory.Total);
        Assert.Equal(7, home.Forecast.Count);
        Assert.True(home.Forecast[0].IsToday);
        Assert.Equal("Hôm nay", home.Forecast[0].Label);
        Assert.Equal(3, home.Forecast[0].Count); // overdue words fall on today; the suspended one is not counted
        Assert.Equal(100, home.Forecast[0].HeightPercent);
        Assert.Equal(1, home.Forecast[3].Count);
        Assert.All(home.Forecast.Skip(1), day => Assert.False(day.IsToday));
        Assert.Null(home.NextDueLabel); // something is due, so no "next due" hint
    }

    [Fact]
    public async Task Home_WithNothingDue_NamesTheNextDueDate()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedProgressAsync(factory, 402, "Mastered", 4, DateTime.UtcNow.AddDays(14).AddHours(2));

        var home = await GetHomeAsync(factory);

        Assert.Equal(0, home.DueCount);
        Assert.NotNull(home.NextDueLabel);
        Assert.StartsWith("Sau ", home.NextDueLabel, StringComparison.Ordinal);
        Assert.All(home.Forecast, day => Assert.Equal(0, day.Count));
    }

    [Fact]
    public async Task Home_ForANewLearner_HasZeroedSrsFigures()
    {
        await using var factory = await CreateFactoryAsync();

        var home = await GetHomeAsync(factory);

        Assert.Equal(0, home.DueCount);
        Assert.Equal(0, home.TodayCount);
        Assert.Equal(0, home.Memory.Total);
        Assert.Equal(7, home.Forecast.Count);
        Assert.All(home.Forecast, day => Assert.Equal(0, day.HeightPercent));
        Assert.All(home.Collections.SelectMany(collection => collection.Courses), course => Assert.False(string.IsNullOrEmpty(course.ArtKey)));
    }

    [Fact]
    public async Task Course_ShowsLevelsUpcomingWordsAndDueLessons()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory, ManyCardsStart, "Reviewing", 2, now.AddHours(-1));
        await SeedProgressAsync(factory, ManyCardsStart + 1, "Mastered", 5, now.AddDays(3));
        await SeedProgressAsync(factory, ManyCardsStart + 2, "Reviewing", 1, now.AddDays(1));

        var course = await GetCourseAsync(factory, TestDataIds.SystemSetId);

        var first = course.Words.Single(word => word.CardId == ManyCardsStart);
        Assert.Equal(2, first.Level);
        Assert.Equal("growing", first.Stage);
        Assert.Equal("Đang nhớ", first.StageLabel);
        Assert.True(first.IsDue);
        Assert.Equal("Hôm nay", first.NextReviewLabel);
        var untouched = course.Words.First(word => word.Level == null);
        Assert.Equal("none", untouched.Stage);
        Assert.Equal("Chưa học", untouched.NextReviewLabel);

        Assert.Equal(3, course.Memory.Total);
        Assert.Equal(new[] { "Hôm nay", "Ngày mai" }, course.Upcoming.Take(2).Select(item => item.Label).ToArray());
        Assert.StartsWith("Sau ", course.Upcoming[2].Label, StringComparison.Ordinal);
        Assert.Equal(3, course.Upcoming.Count);
        Assert.Equal(1, course.Lessons.Sum(lesson => lesson.DueCount));
        Assert.All(course.Lessons, lesson => Assert.Equal(lesson.WordCount, lesson.WordStages.Count));
    }

    [Fact]
    public async Task Review_IsCappedAtTwentyOldestFirst_AndReportsTheRemainder()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            await SeedProgressAsync(factory, ManyCardsStart + i, "Reviewing", 1, now.AddMinutes(-100 + i));
        }

        using var scope = factory.Services.CreateScope();
        var review = await scope.ServiceProvider.GetRequiredService<IStudyService>().GetVocabularyReviewAsync(TestDataIds.UserId);

        Assert.Equal(20, review.Words.Count);
        Assert.Equal(5, review.RemainingDue);
        Assert.Equal(Enumerable.Range(ManyCardsStart, 20), review.Words.Select(word => word.Id));
        Assert.All(review.Words, word => Assert.Equal(1, word.Level));
        Assert.All(review.Words, word => Assert.Equal("new", word.Stage));
    }

    [Fact]
    public async Task Review_WithNothingDue_NamesTheNextDueDate()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedProgressAsync(factory, ManyCardsStart, "Reviewing", 2, DateTime.UtcNow.AddDays(1));

        using var scope = factory.Services.CreateScope();
        var review = await scope.ServiceProvider.GetRequiredService<IStudyService>().GetVocabularyReviewAsync(TestDataIds.UserId);

        Assert.Equal("nothing-due", review.EmptyReason);
        Assert.Equal("Ngày mai", review.NextDueLabel);
        Assert.Equal(0, review.RemainingDue);
    }

    [Fact]
    public async Task LessonConfig_CarriesLevelStageAndEmojiPerWord()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedProgressAsync(factory, ManyCardsStart + 4, "Mastered", 5, DateTime.UtcNow.AddDays(-1));

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStudyService>();
        var review = await service.GetVocabularyLessonAsync(TestDataIds.SystemSetId, TestDataIds.UserId, null, null, review: true);

        var word = Assert.Single(review!.Words);
        Assert.Equal(5, word.Level);
        Assert.Equal("strong", word.Stage);
        Assert.Equal("Nhớ tốt", word.StageLabel);
        Assert.Equal("🐶", word.Emoji); // "Dog" is in the emoji dictionary
        var json = JsonDocument.Parse(review.ToConfigJson()).RootElement;
        Assert.Equal(0, json.GetProperty("remainingDue").GetInt32());
        Assert.Equal(5, json.GetProperty("words")[0].GetProperty("level").GetInt32());
    }

    // ── home page states (plan 5.1) ──────────────────────────────────────

    [Fact]
    public async Task HomePage_NewLearner_ShowsTheStartStateAndAPrimaryLessonButton()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync("/Vocabulary/Index");
        var text = System.Net.WebUtility.HtmlDecode(html);

        Assert.Contains("Bắt đầu với 5 từ đầu tiên nhé", text, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Hôm nay đã học 0 trên 10 từ\"", text, StringComparison.Ordinal);
        Assert.Contains("Hôm nay không có từ nào cần ôn", text, StringComparison.Ordinal);
        Assert.Contains("Học bài đầu tiên để bắt đầu theo dõi trí nhớ.", text, StringComparison.Ordinal);
        Assert.Contains("Chưa có lịch ôn. Học một bài để hệ thống xếp lịch cho bạn.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ôn ngay", text, StringComparison.Ordinal);
        Assert.Matches("class=\"btn btn-lg tv-bcard__go btn-primary\"", html);
        Assert.DoesNotContain("Chuỗi ", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomePage_WithWordsDue_PutsTheReviewFirstAndTheLessonButtonSoft()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedProgressAsync(factory, 402, "Reviewing", 2, DateTime.UtcNow.AddHours(-1));
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync("/Vocabulary/Index");

        Assert.Contains("Ôn ngay", System.Net.WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.Matches("class=\"btn btn-lg tv-bcard__go k-btn--soft\"", html);
        Assert.Contains("Khoảng 1 phút", System.Net.WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.Contains("class=\"tv-chipdue\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomePage_WithNothingDue_NamesTheNextDueWord_AndHasNoReviewButton()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedProgressAsync(factory, 402, "Mastered", 4, DateTime.UtcNow.AddDays(1));
        using var client = CreateClient(factory);

        var text = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/Vocabulary/Index"));

        Assert.Contains("Hôm nay không có từ nào cần ôn", text, StringComparison.Ordinal);
        Assert.Contains("Từ tiếp theo đến hạn: Ngày mai", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ôn ngay", text, StringComparison.Ordinal);
        Assert.Contains("1 từ đã học", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomePage_WhenTheDailyTargetIsReached_SaysSo_AndWhenEveryLessonIsDone_OffersNoLessonButton()
    {
        await using var factory = await CreateFactoryAsync();
        List<int> allIds;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            allIds = await context.Cards.Where(card => card.Set.Owner!.Email == "system@tct.local").Select(card => card.CardId).ToListAsync();
        }

        foreach (var id in allIds)
        {
            await SeedProgressAsync(factory, id, "Mastered", 4, DateTime.UtcNow.AddDays(10), last: DateTime.UtcNow);
        }

        using var client = CreateClient(factory);
        var text = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/Vocabulary/Index"));

        Assert.True(allIds.Count >= 10);
        Assert.Contains("Đã đạt mục tiêu hôm nay!", text, StringComparison.Ordinal);
        Assert.Contains("Bạn đã học hết các bài hiện có", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Học bài mới", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomePage_FolderView_HasNoSrsBannerOrPanels()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync($"/Vocabulary/FolderDetail?folderId={TestDataIds.SystemFolderId}");

        Assert.DoesNotContain("tv-banner", html, StringComparison.Ordinal);
        Assert.DoesNotContain("tv-memory", html, StringComparison.Ordinal);
        Assert.Contains("data-tv-course", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CourseCards_CarryTheIllustrationAndNoInlineColours()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync("/Vocabulary/Index");

        Assert.Contains("class=\"tv-art\"", html, StringComparison.Ordinal);
        Assert.Contains("tv-cover tv-cover--", html, StringComparison.Ordinal);
        // Only custom properties (--tv-*) may be set inline on the page's own elements; never a colour.
        Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex("<[^>]*class=\"[^\"]*\btv-[^\"]*\"[^>]*style=\"[^\"]*(#[0-9a-f]{3,8}|rgb|color)", System.Text.RegularExpressions.RegexOptions.IgnoreCase), html);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static HttpClient CreateClient(TestWebApplicationFactory factory) =>
        IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

    private static Task<string> TokenAsync(HttpClient client) =>
        IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}");

    private static Task<(HttpStatusCode Code, JsonElement Body)> PostAsync(
        HttpClient client, string? token, int cardId, string outcome) =>
        PostRawAsync(client, token, $$"""{"cardId":{{cardId}},"outcome":"{{outcome}}"}""");

    private static async Task<(HttpStatusCode Code, JsonElement Body)> PostRawAsync(HttpClient client, string? token, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, RecordUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (token != null)
        {
            request.Headers.Add("RequestVerificationToken", token);
        }

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        JsonElement body = default;
        if (response.StatusCode == HttpStatusCode.OK && text.Length > 0)
        {
            using var document = JsonDocument.Parse(text);
            body = document.RootElement.Clone();
        }

        return (response.StatusCode, body);
    }

    private static void AssertNear(DateTime expected, DateTime? actual)
    {
        Assert.NotNull(actual);
        Assert.True(
            Math.Abs((actual!.Value - expected).TotalSeconds) < 90,
            $"Expected about {expected:O} but was {actual:O}.");
    }

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var systemUserId = await context.Users.Where(user => user.Email == "system@tct.local").Select(user => user.UserId).SingleAsync();

        // Thirty extra system cards in set 302 (card 5001 is "Dog" for the emoji lookup) ...
        for (var i = 0; i < 30; i++)
        {
            context.Cards.Add(new Card
            {
                CardId = ManyCardsStart + i,
                SetId = TestDataIds.SystemSetId,
                Term = i == 4 ? "Dog" : "term" + i,
                Definition = "nghia " + i,
                Topic = "Animals"
            });
        }

        // ... and a private set of another user.
        context.Sets.Add(new Set
        {
            SetId = OutsiderSetId,
            OwnerId = TestDataIds.OutsiderUserId,
            SetName = "Outsider private set",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        context.Cards.Add(new Card { CardId = OutsiderCardId, SetId = OutsiderSetId, Term = "secret", Definition = "bí mật" });
        await context.SaveChangesAsync();
        _ = systemUserId;
        return factory;
    }

    private static async Task SeedProgressAsync(
        TestWebApplicationFactory factory, int cardId, string status, int repetitionCount, DateTime next, bool suspended = false, DateTime? last = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        context.LearningProgresses.Add(new LearningProgress
        {
            UserId = TestDataIds.UserId,
            CardId = cardId,
            Status = status,
            RepetitionCount = repetitionCount,
            NextReviewDate = next,
            LastReviewedDate = last ?? DateTime.UtcNow.AddDays(-1),
            IsSuspended = suspended
        });
        await context.SaveChangesAsync();
    }

    private static async Task<LearningProgress?> LoadProgressAsync(TestWebApplicationFactory factory, int userId, int cardId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        return await context.LearningProgresses.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId && row.CardId == cardId);
    }

    private static async Task<UserDailyActivity?> LoadActivityAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        return await context.UserDailyActivities.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == TestDataIds.UserId && row.ActivityDate == BusinessDateHelper.Today);
    }

    private static async Task<TCTVocabulary.ViewModels.VocabularyHomeViewModel> GetHomeAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var model = await scope.ServiceProvider.GetRequiredService<IStudyService>().GetVocabularyHomeAsync(TestDataIds.UserId);
        Assert.NotNull(model);
        return model!;
    }

    private static async Task<TCTVocabulary.ViewModels.VocabularyCourseViewModel> GetCourseAsync(TestWebApplicationFactory factory, int setId)
    {
        using var scope = factory.Services.CreateScope();
        var model = await scope.ServiceProvider.GetRequiredService<IStudyService>().GetVocabularyCourseAsync(setId, TestDataIds.UserId);
        Assert.NotNull(model);
        return model!;
    }
}
