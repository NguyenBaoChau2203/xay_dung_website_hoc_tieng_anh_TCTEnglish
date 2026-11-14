using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.Services.Vocabulary;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// The rebuilt system Vocabulary module. Scenario data (system user = "system@tct.local"):
///  - set 302 (folder 202): card 402 (no topic), 14 cards "Animals" (1001-1014), 6 cards "Colors" (1101-1106)
///    => lessons: 1 = [402], 2 = Animals 1/2 (10), 3 = Animals 2/2 (4), 4 = Colors (6)
///  - set 390 (folder 290, "#food #a1"): 5 cards "Food" (1201-1205)
/// </summary>
public sealed class VocabularyIntegrationTests
{
    private const int SecondFolderId = 290;
    private const int SecondSetId = 390;
    private const int EmptySetId = 391;
    private const int ImageSetId = 392;

    // ── routes and access ────────────────────────────────────────────────

    [Fact]
    public async Task LegacyAndNewRoutes_ReturnOkForAStandardUser()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var routes = new[]
        {
            "/Vocabulary",
            "/Vocabulary/Index",
            $"/Vocabulary/FolderDetail?folderId={TestDataIds.SystemFolderId}",
            $"/Vocabulary/Detail?setId={TestDataIds.SystemSetId}",
            $"/Vocabulary/Topics?setId={TestDataIds.SystemSetId}",
            $"/Vocabulary/TopicDetail?setId={TestDataIds.SystemSetId}",
            $"/Vocabulary/TopicDetail?setId={TestDataIds.SystemSetId}&topic=Animals",
            $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}",
            $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&lesson=2",
            $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&topic=Colors&index=3",
            $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&mode=review",
            "/Vocabulary/Review",
            $"/Vocabulary/Review?setId={TestDataIds.SystemSetId}"
        };

        foreach (var route in routes)
        {
            using var response = await client.GetAsync(route);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Expected 200 for '{route}' but got {(int)response.StatusCode} ({response.StatusCode}).");
        }
    }

    [Fact]
    public async Task UserOwnedSetsAndFolders_AreNotFoundOnEveryRoute()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var routes = new[]
        {
            $"/Vocabulary/FolderDetail?folderId={TestDataIds.UserFolderId}",
            $"/Vocabulary/Detail?setId={TestDataIds.UserSetId}",
            $"/Vocabulary/Topics?setId={TestDataIds.UserSetId}",
            $"/Vocabulary/TopicDetail?setId={TestDataIds.UserSetId}",
            $"/Vocabulary/Study?setId={TestDataIds.UserSetId}",
            $"/Vocabulary/Study?setId={TestDataIds.UserSetId}&mode=review",
            $"/Vocabulary/Review?setId={TestDataIds.UserSetId}",
            "/Vocabulary/Detail?setId=999999",
            "/Vocabulary/FolderDetail?folderId=999999"
        };

        foreach (var route in routes)
        {
            using var response = await client.GetAsync(route);
            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound,
                $"Expected 404 for '{route}' but got {(int)response.StatusCode} ({response.StatusCode}).");
        }
    }

    [Theory]
    [InlineData("/Vocabulary")]
    [InlineData("/Vocabulary/Index")]
    [InlineData("/Vocabulary/Detail?setId=302")]
    [InlineData("/Vocabulary/Study?setId=302")]
    [InlineData("/Vocabulary/Review")]
    public async Task Anonymous_IsDeniedAndNeverSeesCourseContent(string route)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();

        // The real site redirects to the login page; the test host's scheme answers 401. Never 200.
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized,
            $"Expected a redirect or 401 for '{route}' but got {(int)response.StatusCode}.");
        Assert.DoesNotContain("Sprint One System Set", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedLinksToTheHome_KeepTheCanonicalIndexUrl()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await client.GetStringAsync("/Home/Index");

        Assert.Contains("href=\"/Vocabulary/Index\"", html, StringComparison.Ordinal);
    }

    // ── home ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Home_ShowsSystemFoldersAndSets_WithRealCounts()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Vocabulary/Index"));

        Assert.Contains("System Sprint Folder", html, StringComparison.Ordinal);
        Assert.Contains("Second System Folder", html, StringComparison.Ordinal);
        Assert.Contains("Sprint One System Set", html, StringComparison.Ordinal);
        Assert.Contains("Second System Set", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sprint One User Set", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sprint One Folder", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_StatsReflectOnlyTheCurrentUsersProgress()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory,
            (TestDataIds.UserId, 402, "Mastered", now.AddDays(1), now),
            (TestDataIds.UserId, 1001, "Reviewing", now.AddHours(-1), now),
            (TestDataIds.UserId, 1002, "Learning", now.AddMinutes(-5), now),
            (TestDataIds.UserId, 1003, "Learning", now.AddDays(1), now.AddDays(-3)),
            (TestDataIds.OutsiderUserId, 1005, "Mastered", now.AddDays(-1), now));
        await SetStreakAsync(factory, TestDataIds.UserId, 4);

        var home = await GetHomeAsync(factory, TestDataIds.UserId);

        Assert.Equal(33, home.Stats.TotalCards); // 21 in set 302, 5 in set 390, 7 image cards
        Assert.Equal(4, home.Stats.LearnedCards);
        Assert.Equal(1, home.Stats.KnownCards);
        Assert.Equal(2, home.Stats.DueCards);
        Assert.Equal(3, home.Stats.LearnedToday);
        Assert.Equal(4, home.Stats.Streak);
        Assert.Equal("/Vocabulary/Review", home.DueUrl);
    }

    [Fact]
    public async Task Home_HasNoDueLinkWhenNothingIsDue()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory, (TestDataIds.UserId, 402, "Mastered", now.AddDays(1), now));
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var home = await GetHomeAsync(factory, TestDataIds.UserId);
        var html = await client.GetStringAsync("/Vocabulary/Index");

        Assert.Null(home.DueUrl);
        Assert.DoesNotContain("href=\"/Vocabulary/Review\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_NextPointsToTheFirstUnfinishedLessonOfTheLastStudiedCourse()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        var progress = new List<(int, int, string, DateTime?, DateTime?)>
        {
            (TestDataIds.UserId, 402, "Mastered", now.AddDays(1), now.AddMinutes(-30))
        };
        progress.AddRange(Enumerable.Range(1001, 10).Select(id =>
            (TestDataIds.UserId, id, "Reviewing", (DateTime?)now.AddDays(1), (DateTime?)now.AddMinutes(-20 + (id - 1001)))));
        await SeedProgressAsync(factory, progress.ToArray());

        var home = await GetHomeAsync(factory, TestDataIds.UserId);

        Assert.NotNull(home.Next);
        Assert.Equal(TestDataIds.SystemSetId, home.Next!.SetId);
        Assert.Equal(3, home.Next.LessonNumber);
        Assert.Equal("Bài 3 · Animals · phần 2", home.Next.LessonTitle);
        Assert.Equal(4, home.Next.NewWordCount);
        Assert.Equal($"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&lesson=3", home.Next.Url);
    }

    [Fact]
    public async Task Home_NextFallsBackToTheFirstCourseWithAnUnfinishedLesson()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory, Enumerable.Range(1201, 5)
            .Select(id => (TestDataIds.UserId, id, "Reviewing", (DateTime?)now.AddDays(1), (DateTime?)now))
            .ToArray());

        var home = await GetHomeAsync(factory, TestDataIds.UserId);

        Assert.NotNull(home.Next);
        Assert.Equal(TestDataIds.SystemSetId, home.Next!.SetId);
        Assert.Equal(1, home.Next.LessonNumber);
        Assert.Equal(1, home.Next.NewWordCount);
    }

    [Fact]
    public async Task Home_NextIsNullWhenEveryCourseIsFinished()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        var ids = new[] { 402 }.Concat(Enumerable.Range(1001, 14)).Concat(Enumerable.Range(1101, 6))
            .Concat(Enumerable.Range(1201, 5)).Concat(Enumerable.Range(3001, 7));
        await SeedProgressAsync(factory, ids.Select(id => (TestDataIds.UserId, id, "Mastered", (DateTime?)now.AddDays(1), (DateTime?)now)).ToArray());

        var home = await GetHomeAsync(factory, TestDataIds.UserId);

        Assert.Null(home.Next);
        Assert.All(home.Collections.SelectMany(collection => collection.Courses).Where(course => course.WordCount > 0),
            course => Assert.Equal("done", course.Status));
    }

    [Fact]
    public async Task Home_CourseCardsCarryRealCountsLevelIconAndStatus()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory,
            (TestDataIds.UserId, 1201, "Mastered", now.AddDays(1), now),
            (TestDataIds.UserId, 1202, "Learning", now.AddMinutes(-1), now));

        var home = await GetHomeAsync(factory, TestDataIds.UserId);
        var courses = home.Collections.SelectMany(collection => collection.Courses).ToDictionary(course => course.SetId);

        var second = courses[SecondSetId];
        Assert.Equal(5, second.WordCount);
        Assert.Equal(1, second.LessonCount);
        Assert.Equal(2, second.LearnedCount);
        Assert.Equal(1, second.KnownCount);
        Assert.Equal(1, second.DueCount);
        Assert.Equal(40, second.ProgressPercent);
        Assert.Equal("progress", second.Status);
        Assert.Equal("A1", second.Level);
        Assert.Equal("food", second.IconKey);

        var first = courses[TestDataIds.SystemSetId];
        Assert.Equal(21, first.WordCount);
        Assert.Equal(4, first.LessonCount);
        Assert.Null(first.Level);
        Assert.Equal("book", first.IconKey);
        Assert.Equal("new", first.Status);

        var empty = courses[EmptySetId];
        Assert.Equal(0, empty.WordCount);
        Assert.Equal(0, empty.LessonCount);
        Assert.Equal(0, empty.ProgressPercent);
        Assert.Equal("new", empty.Status);

        Assert.Equal(new[] { "A1" }, home.Levels);
    }

    [Fact]
    public async Task FolderDetail_ShowsOnlyThatCollection()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Vocabulary/FolderDetail?folderId={TestDataIds.SystemFolderId}"));
        var filtered = await GetHomeAsync(factory, TestDataIds.UserId, TestDataIds.SystemFolderId);

        Assert.Contains("System Sprint Folder", html, StringComparison.Ordinal);
        Assert.Contains("Sprint One System Set", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Second System Folder", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Second System Set", html, StringComparison.Ordinal);
        Assert.Equal(TestDataIds.SystemFolderId, filtered.FilterFolderId);
        Assert.Equal("System Sprint Folder", filtered.FilterFolderName);
        Assert.Equal(TestDataIds.SystemFolderId, Assert.Single(filtered.Collections).FolderId);
    }

    [Fact]
    public async Task Home_RendersTheNextLessonButtonDueButtonAndCourseCards()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory,
            (TestDataIds.UserId, 402, "Learning", now.AddMinutes(-1), now),
            (TestDataIds.UserId, 1201, "Reviewing", now.AddDays(1), now));
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await client.GetStringAsync("/Vocabulary/Index");
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains("BÀI TIẾP THEO", text, StringComparison.Ordinal);
        Assert.Contains("Bài 2 · Animals · phần 1", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/Vocabulary/Study?setId=302&amp;lesson=2\"", html, StringComparison.Ordinal);
        Assert.Contains("Học tiếp", text, StringComparison.Ordinal);
        Assert.Contains("Ôn 1 từ đến hạn", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/Vocabulary/Review\"", html, StringComparison.Ordinal);
        Assert.Contains("Hôm nay bạn đã học 2 từ", text, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Matches(html, "<article[^>]*data-tv-course").Count);
        Assert.Contains("data-status=\"progress\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Mở khóa Sprint One System Set\"", text, StringComparison.Ordinal);
        Assert.Contains("1/21 từ", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_HidesTheLevelFilterWhenThereAreFewerThanTwoLevels_AndOmitsAZeroStreak()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await client.GetStringAsync("/Vocabulary/Index");

        Assert.DoesNotContain("data-tv-filter=\"level\"", html, StringComparison.Ordinal);
        Assert.Contains("data-tv-filter=\"status\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("chuỗi học", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.Contains("Hôm nay bạn chưa học từ nào", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    // ── course ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Course_LessonsAndCountsFollowTheBuilder()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory,
            (TestDataIds.UserId, 402, "Mastered", now.AddDays(1), now),
            (TestDataIds.UserId, 1001, "Reviewing", now.AddDays(1), now),
            (TestDataIds.UserId, 1002, "Learning", now.AddMinutes(-1), now));

        var course = await GetCourseAsync(factory, TestDataIds.UserId, TestDataIds.SystemSetId);

        Assert.Equal(21, course.Course.WordCount);
        Assert.Equal(4, course.Course.LessonCount);
        Assert.Equal(21, course.Words.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, course.Lessons.Select(lesson => lesson.Number).ToArray());
        Assert.Equal(new[] { 1, 10, 4, 6 }, course.Lessons.Select(lesson => lesson.WordCount).ToArray());
        Assert.Equal(new[] { "done", "progress", "new", "new" }, course.Lessons.Select(lesson => lesson.State).ToArray());
        Assert.Equal("Bài 2 · Animals · phần 1", course.Lessons[1].Title);
        Assert.Equal($"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&lesson=2", course.Lessons[1].Url);

        // The recommended lesson is the first one that is not done, and only it carries the preview chips.
        Assert.Equal(new[] { false, true, false, false }, course.Lessons.Select(lesson => lesson.IsNext).ToArray());
        Assert.Equal(5, course.Lessons[1].PreviewTerms.Count);
        Assert.Equal(5, course.Lessons[1].MoreCount);
        Assert.All(course.Lessons.Where(lesson => !lesson.IsNext), lesson => Assert.Empty(lesson.PreviewTerms));
        Assert.Equal(2, course.NextLesson!.Number);
        Assert.False(course.CourseComplete);

        Assert.Equal("known", course.Words.Single(word => word.CardId == 402).State);
        Assert.Equal("learning", course.Words.Single(word => word.CardId == 1001).State);
        Assert.True(course.Words.Single(word => word.CardId == 1002).IsDue);
        Assert.Equal("new", course.Words.Single(word => word.CardId == 1101).State);
        Assert.Equal(4, course.Words.Single(word => word.CardId == 1101).LessonNumber);
        Assert.Equal("Colors", course.Words.Single(word => word.CardId == 1101).Topic);
        Assert.Equal($"/Practice?set={TestDataIds.SystemSetId}", course.PracticeUrl);
        Assert.Equal($"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&mode=review", course.DueUrl);
    }

    [Fact]
    public async Task Course_CompleteWhenEveryWordIsLearned()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory, Enumerable.Range(1201, 5)
            .Select(id => (TestDataIds.UserId, id, "Mastered", (DateTime?)now.AddDays(1), (DateTime?)now)).ToArray());

        var course = await GetCourseAsync(factory, TestDataIds.UserId, SecondSetId);

        Assert.True(course.CourseComplete);
        Assert.Null(course.NextLesson);
        Assert.Null(course.DueUrl);
        Assert.Equal("done", course.Course.Status);
    }

    [Fact]
    public async Task TopicsAndTopicDetail_RenderTheCourseViewWithTheRightTab()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var detail = await client.GetStringAsync($"/Vocabulary/Detail?setId={TestDataIds.SystemSetId}");
        var topics = await client.GetStringAsync($"/Vocabulary/Topics?setId={TestDataIds.SystemSetId}");
        var words = await client.GetStringAsync($"/Vocabulary/TopicDetail?setId={TestDataIds.SystemSetId}");
        var filtered = await client.GetStringAsync($"/Vocabulary/TopicDetail?setId={TestDataIds.SystemSetId}&topic=Animals");

        Assert.Contains("data-active-tab=\"lessons\"", detail, StringComparison.Ordinal);
        Assert.Contains("data-active-tab=\"lessons\"", topics, StringComparison.Ordinal);
        Assert.Contains("data-active-tab=\"words\"", words, StringComparison.Ordinal);
        Assert.Contains("data-active-tab=\"words\"", filtered, StringComparison.Ordinal);
        Assert.Contains("data-topic-filter=\"Animals\"", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain("data-topic-filter=\"Animals\"", words, StringComparison.Ordinal);
        Assert.Contains("Sprint One System Set", WebUtility.HtmlDecode(detail), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Course_RendersHeroTabsLessonRowsAndWordRows()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory,
            (TestDataIds.UserId, 402, "Mastered", now.AddDays(1), now),
            (TestDataIds.UserId, 1001, "Learning", now.AddMinutes(-3), now));
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await client.GetStringAsync($"/Vocabulary/Detail?setId={TestDataIds.SystemSetId}");
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains("Sprint One System Set", text, StringComparison.Ordinal);
        Assert.Contains("Học tiếp bài 2", text, StringComparison.Ordinal);
        Assert.Contains("Ôn 1 từ", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/Practice?set=302\"", html, StringComparison.Ordinal);
        Assert.Contains("Luyện bằng trò chơi", text, StringComparison.Ordinal);

        // ARIA tabs: lessons selected on the server, words hidden.
        Assert.Contains("role=\"tablist\"", html, StringComparison.Ordinal);
        Assert.Matches("id=\"tvTabLessons\"[^>]*aria-selected=\"true\"", html);
        Assert.Matches("id=\"tvTabWords\"[^>]*aria-selected=\"false\"", html);
        Assert.Matches("id=\"tvPanelWords\"[^>]*hidden", html);

        // One row per lesson; exactly one is the current step; only it has the preview chips.
        Assert.Equal(4, Regex.Matches(html, "<article[^>]*tv-lesson").Count);
        Assert.Single(Regex.Matches(html, "aria-current=\"step\""));
        Assert.Contains("Bài 2 · Animals · phần 1", text, StringComparison.Ordinal);
        Assert.Contains("+5", text, StringComparison.Ordinal);

        // Every word has a row; words with an example get a disclosure button.
        Assert.Equal(21, Regex.Matches(html, "<tbody[^>]*data-tv-word").Count);
        Assert.Contains("data-tv-example", html, StringComparison.Ordinal);
        Assert.Contains("Cần ôn", text, StringComparison.Ordinal);
        Assert.DoesNotContain("tabindex=\"0\" hidden", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Course_ShowsTheEmptyStateAndNoStudyButtonsWhenThereAreNoWords()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var text = WebUtility.HtmlDecode(await client.GetStringAsync($"/Vocabulary/Detail?setId={EmptySetId}"));

        Assert.Contains("Khóa này chưa có từ nào.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Học tiếp", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Bắt đầu học", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Luyện bằng trò chơi", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Course_ACompleteCourseOffersARestartInsteadOfContinue()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory, Enumerable.Range(1201, 5)
            .Select(id => (TestDataIds.UserId, id, "Mastered", (DateTime?)now.AddDays(1), (DateTime?)now)).ToArray());
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await client.GetStringAsync($"/Vocabulary/Detail?setId={SecondSetId}");
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains("Ôn lại từ đầu", text, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Vocabulary/Study?setId={SecondSetId}&amp;lesson=1\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Học tiếp bài", text, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-current=\"step\"", html, StringComparison.Ordinal);
    }

    // ── lesson payload ───────────────────────────────────────────────────

    [Fact]
    public async Task Lesson_LearnContainsOnlyTheUnlearnedWordsOfThatLesson()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory, Enumerable.Range(1001, 4)
            .Select(id => (TestDataIds.UserId, id, "Reviewing", (DateTime?)now.AddDays(1), (DateTime?)now)).ToArray());
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var config = await GetLessonConfigAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&lesson=2");

        Assert.Equal("learn", config.GetProperty("kind").GetString());
        Assert.Equal(2, config.GetProperty("lessonNumber").GetInt32());
        Assert.Equal("Bài 2 · Animals · phần 1", config.GetProperty("lessonTitle").GetString());
        Assert.Equal(Enumerable.Range(1005, 6), Ids(config, "words"));
        Assert.Equal($"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&lesson=3", config.GetProperty("nextLessonUrl").GetString());
        Assert.Equal(3, config.GetProperty("nextLessonNumber").GetInt32());
        Assert.Equal($"/Vocabulary/Detail?setId={TestDataIds.SystemSetId}", config.GetProperty("courseUrl").GetString());
        Assert.Equal(JsonValueKind.Null, config.GetProperty("emptyReason").ValueKind);
    }

    [Fact]
    public async Task Lesson_ADoneLessonIsRelearnedWithAllItsWords()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory, Enumerable.Range(1011, 4)
            .Select(id => (TestDataIds.UserId, id, "Mastered", (DateTime?)now.AddDays(1), (DateTime?)now)).ToArray());
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var config = await GetLessonConfigAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&lesson=3");

        Assert.Equal("relearn", config.GetProperty("kind").GetString());
        Assert.Equal(Enumerable.Range(1011, 4), Ids(config, "words"));
    }

    [Fact]
    public async Task Lesson_WithoutAnArgumentOpensTheNextLessonAndOutOfRangeFallsBackToIt()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory, (TestDataIds.UserId, 402, "Mastered", now.AddDays(1), now));
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var next = await GetLessonConfigAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}");
        var outOfRange = await GetLessonConfigAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&lesson=99");
        var zero = await GetLessonConfigAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&lesson=0");

        foreach (var config in new[] { next, outOfRange, zero })
        {
            Assert.Equal(2, config.GetProperty("lessonNumber").GetInt32());
            Assert.Equal("learn", config.GetProperty("kind").GetString());
        }
    }

    [Fact]
    public async Task Lesson_ALegacyTopicMapsToItsFirstLesson()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var colors = await GetLessonConfigAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&topic=Colors&index=4");
        var animals = await GetLessonConfigAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&topic=animals");

        Assert.Equal(4, colors.GetProperty("lessonNumber").GetInt32());
        Assert.Equal(Enumerable.Range(1101, 6), Ids(colors, "words"));
        Assert.Equal(2, animals.GetProperty("lessonNumber").GetInt32());
    }

    [Fact]
    public async Task Lesson_ReviewHasOnlyDueWordsOldestFirstAtMostTwenty()
    {
        await using var factory = await CreateFactoryAsync();
        await SeedExtraCardsAsync(factory, TestDataIds.SystemSetId, Enumerable.Range(2001, 25).ToArray(), "Extra");
        var now = DateTime.UtcNow;
        var progress = Enumerable.Range(2001, 25)
            // card 2001 is the oldest due, 2025 the newest due
            .Select(id => (TestDataIds.UserId, id, "Learning", (DateTime?)now.AddHours(-(100 - (id - 2001))), (DateTime?)now))
            .ToList();
        progress.Add((TestDataIds.UserId, 1001, "Reviewing", now.AddDays(2), now)); // learned but not due
        await SeedProgressAsync(factory, progress.ToArray());
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var config = await GetLessonConfigAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&mode=review");

        Assert.Equal("review", config.GetProperty("kind").GetString());
        Assert.Equal(Enumerable.Range(2001, 20), Ids(config, "words"));
        Assert.DoesNotContain(1001, Ids(config, "words"));
        Assert.Contains(1001, Ids(config, "pool")); // learned but not due: only a distractor
    }

    [Fact]
    public async Task Lesson_PoolExcludesSessionWordsAndStaysInTheSystemScope()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var config = await GetLessonConfigAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}&lesson=2");

        var words = Ids(config, "words").ToHashSet();
        var pool = Ids(config, "pool").ToList();
        Assert.NotEmpty(pool);
        Assert.Empty(words.Intersect(pool));
        Assert.DoesNotContain(TestDataIds.UserCardId, pool);
        Assert.True(pool.Count <= 60);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var systemCardIds = await context.Cards.Where(card => card.Set.Owner!.Email == "system@tct.local").Select(card => card.CardId).ToListAsync();
        Assert.All(pool, id => Assert.Contains(id, systemCardIds));
    }

    [Fact]
    public async Task Lesson_PoolIsToppedUpFromOtherCoursesWhenTheCourseIsTooSmall()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var config = await GetLessonConfigAsync(client, $"/Vocabulary/Study?setId={SecondSetId}");

        Assert.Equal(Enumerable.Range(1201, 5), Ids(config, "words"));
        var pool = Ids(config, "pool").ToList();
        Assert.True(pool.Count >= 3);
        Assert.Empty(pool.Intersect(Ids(config, "words")));
        Assert.DoesNotContain(TestDataIds.UserCardId, pool);
    }

    [Fact]
    public async Task Lesson_ImageUrlsAreLimitedToHttpsOrLocalPaths_AndTheJsonCannotCloseTheScript()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await client.GetStringAsync($"/Vocabulary/Study?setId={ImageSetId}");
        var config = ReadLessonConfig(html);
        var images = config.GetProperty("words").EnumerateArray()
            .ToDictionary(
                word => word.GetProperty("term").GetString()!,
                word => word.GetProperty("imageUrl").ValueKind == JsonValueKind.Null ? null : word.GetProperty("imageUrl").GetString());

        Assert.Equal("https://cdn.example.test/dog.png", images["https"]);
        Assert.Equal("/img/local.png", images["local"]);
        Assert.Null(images["http"]);
        Assert.Null(images["protocol-relative"]);
        Assert.Null(images["javascript"]);
        Assert.Null(images["blank"]);
        Assert.Contains(images.Keys, key => key.StartsWith("</script>", StringComparison.Ordinal)); // survives as data
        Assert.DoesNotContain("</script><img", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html, StringComparison.Ordinal);
    }

    // ── review across courses and empty states ───────────────────────────

    [Fact]
    public async Task Review_CollectsDueWordsAcrossCourses()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory,
            (TestDataIds.UserId, 402, "Learning", now.AddHours(-3), now),
            (TestDataIds.UserId, 1201, "Learning", now.AddHours(-5), now),
            (TestDataIds.UserId, 1001, "Reviewing", now.AddHours(4), now));
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var config = await GetLessonConfigAsync(client, "/Vocabulary/Review");

        Assert.Equal("review", config.GetProperty("kind").GetString());
        Assert.Equal(new[] { 1201, 402 }, Ids(config, "words").ToArray());
        Assert.Equal("Ôn tập", config.GetProperty("setName").GetString());
        Assert.Equal("/Vocabulary/Index", config.GetProperty("courseUrl").GetString());
        Assert.DoesNotContain(1201, Ids(config, "pool"));
    }

    [Fact]
    public async Task EmptyStates_StillRenderTheAntiforgeryToken()
    {
        await using var factory = await CreateFactoryAsync();
        var now = DateTime.UtcNow;
        await SeedProgressAsync(factory, Enumerable.Range(1201, 5)
            .Select(id => (TestDataIds.UserId, id, "Mastered", (DateTime?)now.AddDays(1), (DateTime?)now)).ToArray());
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var expectations = new Dictionary<string, string>
        {
            [$"/Vocabulary/Study?setId={EmptySetId}"] = "no-cards",
            [$"/Vocabulary/Study?setId={SecondSetId}"] = "complete",
            [$"/Vocabulary/Study?setId={SecondSetId}&mode=review"] = "nothing-due",
            ["/Vocabulary/Review"] = "nothing-due",
            [$"/Vocabulary/Review?setId={SecondSetId}"] = "nothing-due"
        };

        foreach (var (route, reason) in expectations)
        {
            var html = await client.GetStringAsync(route);
            Assert.Contains("__RequestVerificationToken", html, StringComparison.Ordinal);
            Assert.Equal(reason, ReadLessonConfig(html).GetProperty("emptyReason").GetString());
            var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, route);
            Assert.False(string.IsNullOrWhiteSpace(token), route);
        }
    }

    // ── recording contract (unchanged endpoint) ──────────────────────────

    [Fact]
    public async Task Record_FromTheLessonPageToken_StillCreditsGoalsForASystemCard()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/LearningApi/record")
        {
            Content = new StringContent(
                $$"""{"cardId":{{TestDataIds.SystemCardId}},"masteryLevel":"good","timestamp":"2026-11-13T00:00:00Z"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("RequestVerificationToken", token);

        using var response = await client.SendAsync(request);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.True(body.TryGetProperty("xpEarned", out _));
        Assert.True(body.TryGetProperty("nextReviewDate", out _));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var progress = await context.LearningProgresses.AsNoTracking()
            .SingleAsync(row => row.UserId == TestDataIds.UserId && row.CardId == TestDataIds.SystemCardId);
        var activity = await context.UserDailyActivities.AsNoTracking()
            .SingleAsync(row => row.UserId == TestDataIds.UserId && row.ActivityDate == BusinessDateHelper.Today);
        Assert.Equal("Reviewing", progress.Status);
        Assert.Equal(1, activity.CardsReviewed);
        Assert.Equal(1, activity.NewCardsLearned);

        // The page the learner sees afterwards counts the word as learned.
        var course = await GetCourseAsync(factory, TestDataIds.UserId, TestDataIds.SystemSetId);
        Assert.Equal("learning", course.Words.Single(word => word.CardId == TestDataIds.SystemCardId).State);
    }

    // ── page contract ────────────────────────────────────────────────────

    [Theory]
    [InlineData("/Vocabulary/Index", "tv-page--home")]
    [InlineData("/Vocabulary/FolderDetail?folderId=202", "tv-page--home")]
    [InlineData("/Vocabulary/Detail?setId=302", "tv-page--course")]
    [InlineData("/Vocabulary/Study?setId=302", "tv-page--lesson")]
    [InlineData("/Vocabulary/Review", "tv-page--lesson")]
    public async Task Pages_FollowTheSharedPageContract(string route, string pageClass)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await client.GetStringAsync(route);

        Assert.Contains("class=\"tct-theme tct-page tv-page " + pageClass, html, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "<h1[\\s>]"));
        Assert.Contains("/css/study-kit.css", html, StringComparison.Ordinal);
        Assert.Contains("/css/vocabulary.css", html, StringComparison.Ordinal);
        Assert.DoesNotContain("vocabulary-system.css", html, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex("\\son[a-z]+\\s*=", RegexOptions.IgnoreCase), StripScriptBodies(html));

        if (pageClass == "tv-page--lesson")
        {
            Assert.Contains("tp-page tp-page--session", html, StringComparison.Ordinal);
            Assert.Contains("id=\"tvLesson\"", html, StringComparison.Ordinal);
            Assert.Matches("<script[^>]*src=\"/js/time-tracker\\.js\"[^>]*data-feature=\"Flashcard\"|<script[^>]*data-feature=\"Flashcard\"[^>]*src=\"/js/time-tracker\\.js\"", html);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static string StripScriptBodies(string html) =>
        Regex.Replace(html, "<script\\b[^>]*>.*?</script>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedScenarioAsync(factory);
        return factory;
    }

    private static async Task SeedScenarioAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var systemUserId = await context.Users.Where(user => user.Email == "system@tct.local").Select(user => user.UserId).SingleAsync();

        context.Folders.Add(new Folder { FolderId = SecondFolderId, UserId = systemUserId, FolderName = "Second System Folder" });
        await context.SaveChangesAsync();

        context.Sets.AddRange(
            new Set { SetId = SecondSetId, OwnerId = systemUserId, FolderId = SecondFolderId, SetName = "Second System Set", Description = "#food #a1", CreatedAt = DateTime.UtcNow },
            new Set { SetId = EmptySetId, OwnerId = systemUserId, FolderId = SecondFolderId, SetName = "Empty System Set", Description = "#basic", CreatedAt = DateTime.UtcNow },
            new Set { SetId = ImageSetId, OwnerId = systemUserId, FolderId = SecondFolderId, SetName = "Image System Set", Description = "#common", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        context.Cards.AddRange(Enumerable.Range(1001, 14).Select(id => NewCard(id, TestDataIds.SystemSetId, "Animals")));
        context.Cards.AddRange(Enumerable.Range(1101, 6).Select(id => NewCard(id, TestDataIds.SystemSetId, "Colors")));
        context.Cards.AddRange(Enumerable.Range(1201, 5).Select(id => NewCard(id, SecondSetId, "Food")));

        var images = new (string Term, string? Url)[]
        {
            ("https", "https://cdn.example.test/dog.png"),
            ("local", "/img/local.png"),
            ("http", "http://cdn.example.test/dog.png"),
            ("protocol-relative", "//evil.example.test/x.png"),
            ("javascript", "javascript:alert(1)"),
            ("blank", "   "),
            ("</script><img src=x onerror=alert(1)>", null)
        };
        var imageId = 3001;
        foreach (var (term, url) in images)
        {
            var card = NewCard(imageId, ImageSetId, "Images");
            card.Term = term;
            card.Definition = "def-" + imageId++;
            card.ImageUrl = url;
            context.Cards.Add(card);
        }

        await context.SaveChangesAsync();
    }

    private static Card NewCard(int id, int setId, string? topic) => new()
    {
        CardId = id,
        SetId = setId,
        Term = "word" + id,
        Definition = "nghia " + id,
        Phonetic = "/w" + id + "/",
        Example = "Example sentence " + id + ".",
        ExampleTranslation = "Cau vi du " + id + ".",
        Topic = topic
    };

    private static async Task SeedExtraCardsAsync(TestWebApplicationFactory factory, int setId, int[] ids, string topic)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        context.Cards.AddRange(ids.Select(id => NewCard(id, setId, topic)));
        await context.SaveChangesAsync();
    }

    private static async Task SeedProgressAsync(
        TestWebApplicationFactory factory,
        params (int UserId, int CardId, string Status, DateTime? Next, DateTime? Last)[] rows)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        context.LearningProgresses.AddRange(rows.Select(row => new LearningProgress
        {
            UserId = row.UserId,
            CardId = row.CardId,
            Status = row.Status,
            NextReviewDate = row.Next,
            LastReviewedDate = row.Last,
            RepetitionCount = 1
        }));
        await context.SaveChangesAsync();
    }

    private static async Task SetStreakAsync(TestWebApplicationFactory factory, int userId, int streak)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(row => row.UserId == userId);
        user.Streak = streak;
        await context.SaveChangesAsync();
    }

    private static async Task<TCTVocabulary.ViewModels.VocabularyHomeViewModel> GetHomeAsync(
        TestWebApplicationFactory factory, int userId, int? folderId = null)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStudyService>();
        var model = await service.GetVocabularyHomeAsync(userId, folderId);
        Assert.NotNull(model);
        return model!;
    }

    private static async Task<TCTVocabulary.ViewModels.VocabularyCourseViewModel> GetCourseAsync(
        TestWebApplicationFactory factory, int userId, int setId)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStudyService>();
        var model = await service.GetVocabularyCourseAsync(setId, userId);
        Assert.NotNull(model);
        return model!;
    }

    private static async Task<JsonElement> GetLessonConfigAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ReadLessonConfig(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement ReadLessonConfig(string html)
    {
        var match = Regex.Match(html, "<script id=\"tvLesson\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline);
        Assert.True(match.Success, "The lesson config script is missing.");
        using var document = JsonDocument.Parse(match.Groups[1].Value);
        return document.RootElement.Clone();
    }

    private static IEnumerable<int> Ids(JsonElement config, string property) =>
        config.GetProperty(property).EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToList();
}
