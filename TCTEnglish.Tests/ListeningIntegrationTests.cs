using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Listening module: access control, quiz persistence, completion rule, index progress and the
/// Admin-only AI rule for shared lessons (plan 2026-11-13-listening-redesign, findings B1-B5, B8, B14, B17).
/// </summary>
public sealed class ListeningIntegrationTests
{
    private const int PremiumOwnerId = 9101;
    private const int StandardOwnerId = 9102;

    private const int PublicFullId = 91001;            // A1, transcript + 2 questions + vocab
    private const int PublicTranscriptOnlyId = 91002;  // B1, transcript only (no quiz, no vocab)
    private const int PublicTranscriptQuizId = 91003;  // A1, transcript + 1 question
    private const int UnpublishedId = 91004;           // public but not published
    private const int PremiumPrivateId = 91005;        // private, owner is Premium
    private const int StandardPrivateId = 91006;       // private, owner was downgraded to Standard
    private const int PrivateNoQuizId = 91008;         // private, owner is Premium, transcript only

    private const string PrivateTopic = "PRIVATE-TOPIC-91005";

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedAsync(factory);
        return factory;
    }

    private static HttpClient Client(TestWebApplicationFactory factory, int userId, string role)
        => IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, role);

    private static async Task SeedAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        db.Users.AddRange(
            new User { UserId = PremiumOwnerId, Email = "premium-owner@test.local", PasswordHash = "hash", FullName = "Premium Owner", Role = Roles.Premium, Status = UserStatus.Online, CreatedAt = DateTime.UtcNow },
            new User { UserId = StandardOwnerId, Email = "standard-owner@test.local", PasswordHash = "hash", FullName = "Standard Owner", Role = Roles.Standard, Status = UserStatus.Online, CreatedAt = DateTime.UtcNow });

        db.ListeningLessons.AddRange(
            Lesson(PublicFullId, "Public full", "A1", "Daily", published: true, owner: null, lines: 2, questions: 2, vocab: 1),
            Lesson(PublicTranscriptOnlyId, "Public transcript only", "B1", "Travel", published: true, owner: null, lines: 2, questions: 0, vocab: 0),
            Lesson(PublicTranscriptQuizId, "Public transcript and quiz", "A1", "Daily", published: true, owner: null, lines: 2, questions: 1, vocab: 0),
            Lesson(UnpublishedId, "Unpublished", "A1", "Daily", published: false, owner: null, lines: 2, questions: 1, vocab: 1),
            Lesson(PremiumPrivateId, "Premium private", "B1", PrivateTopic, published: true, owner: PremiumOwnerId, lines: 2, questions: 1, vocab: 0),
            Lesson(StandardPrivateId, "Standard private", "B1", PrivateTopic, published: true, owner: StandardOwnerId, lines: 2, questions: 1, vocab: 0),
            Lesson(PrivateNoQuizId, "Premium private no quiz", "B1", PrivateTopic, published: true, owner: PremiumOwnerId, lines: 2, questions: 0, vocab: 0));
        await db.SaveChangesAsync();
    }

    private static ListeningLesson Lesson(int id, string title, string level, string topic, bool published, int? owner, int lines, int questions, int vocab)
    {
        var lesson = new ListeningLesson
        {
            Id = id,
            Title = title,
            Level = level,
            Topic = topic,
            YoutubeId = "dQw4w9WgXcQ",
            IsPublished = published,
            OwnerUserId = owner,
            CreatedAt = DateTime.UtcNow,
        };
        for (var i = 0; i < lines; i++)
            lesson.TranscriptLines.Add(new ListeningTranscriptLine { OrderIndex = i + 1, Speaker = "A", Text = $"Line {i + 1} of {title}", StartTime = i * 4, EndTime = i * 4 + 3 });
        for (var i = 0; i < questions; i++)
            lesson.QuizQuestions.Add(new ListeningQuizQuestion
            {
                OrderIndex = i + 1,
                QuestionText = $"Question {i + 1}",
                OptionA = "alpha",
                OptionB = "bravo",
                OptionC = "charlie",
                OptionD = "delta",
                CorrectAnswer = i % 2 == 0 ? "A" : "B",
                Explanation = "SECRET-EXPLANATION",
            });
        for (var i = 0; i < vocab; i++)
            lesson.VocabItems.Add(new ListeningVocabItem { OrderIndex = i + 1, Word = $"word{i + 1}", Definition = "meaning" });
        return lesson;
    }

    private static async Task<T> WithDbAsync<T>(TestWebApplicationFactory factory, Func<DbflashcardContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<DbflashcardContext>());
    }

    private static Task<List<(int Id, string Correct)>> QuestionsAsync(TestWebApplicationFactory factory, int lessonId)
        => WithDbAsync(factory, async db => (await db.ListeningQuizQuestions.AsNoTracking()
            .Where(q => q.LessonId == lessonId).OrderBy(q => q.OrderIndex)
            .Select(q => new { q.Id, q.CorrectAnswer }).ToListAsync()).Select(q => (q.Id, q.CorrectAnswer)).ToList());

    private static Task<UserListeningProgress?> ProgressAsync(TestWebApplicationFactory factory, int userId, int lessonId)
        => WithDbAsync(factory, db => db.UserListeningProgresses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lessonId));

    private static async Task<string> TokenAsync(HttpClient client)
        => await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/Listening/Practice/{PublicFullId}");

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, string json, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (token != null) request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, IDictionary<string, string> form, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        if (token != null) request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    private static string AnswersJson(int lessonId, IEnumerable<(int Id, string Answer)> answers)
        => "{\"lessonId\":" + lessonId + ",\"answers\":{" + string.Join(",", answers.Select(a => $"\"{a.Id}\":\"{a.Answer}\"")) + "}}";

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, int lessonId, string json, string token)
        => PostJsonAsync(client, $"/Home/Listening/SaveProgress/{lessonId}", json, token);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    // ------------------------------------------------------------------
    // Access (L-R5, B17)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Pages_PublicLesson_AreOpenToGuestsAndSignedInUsers()
    {
        await using var factory = await CreateFactoryAsync();

        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/Home/Listening")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/Home/Listening/Practice/{PublicFullId}")).StatusCode);

        using var student = Client(factory, TestDataIds.UserId, Roles.Standard);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync("/Home/Listening")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/Home/Listening/Practice/{PublicFullId}")).StatusCode);
    }

    [Fact]
    public async Task Pages_UnpublishedAndForeignPrivateLessons_Return404()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, TestDataIds.UserId, Roles.Standard);
        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/Home/Listening/Practice/{UnpublishedId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/Home/Listening/Practice/{PremiumPrivateId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/Home/Listening/Practice/{PremiumPrivateId}")).StatusCode);
    }

    [Fact]
    public async Task Pages_PrivateLesson_OwnerOpensIt_AndADowngradedOwnerGets404()
    {
        await using var factory = await CreateFactoryAsync();

        using var premiumOwner = Client(factory, PremiumOwnerId, Roles.Premium);
        Assert.Equal(HttpStatusCode.OK, (await premiumOwner.GetAsync($"/Home/Listening/Practice/{PremiumPrivateId}")).StatusCode);

        using var downgradedOwner = Client(factory, StandardOwnerId, Roles.Standard);
        Assert.Equal(HttpStatusCode.NotFound, (await downgradedOwner.GetAsync($"/Home/Listening/Practice/{StandardPrivateId}")).StatusCode);
    }

    // ------------------------------------------------------------------
    // B2 / B14 / L-R4: EvaluateQuiz
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(PremiumPrivateId, TestDataIds.UserId)]   // another user's private lesson
    [InlineData(UnpublishedId, TestDataIds.UserId)]      // unpublished lesson
    [InlineData(StandardPrivateId, StandardOwnerId)]     // own private lesson after a downgrade
    public async Task EvaluateQuiz_LessonTheUserMayNotUse_Returns404AndLeaksNoAnswers(int lessonId, int userId)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, userId, Roles.Standard);
        var token = await TokenAsync(client);
        var questions = await QuestionsAsync(factory, lessonId);

        using var response = await PostJsonAsync(client, "/Home/Listening/EvaluateQuiz",
            AnswersJson(lessonId, questions.Select(q => (q.Id, "A"))), token);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("correctAnswer", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET-EXPLANATION", body, StringComparison.Ordinal);
        Assert.Null(await ProgressAsync(factory, userId, lessonId));
    }

    [Fact]
    public async Task EvaluateQuiz_UsableLesson_GradesPersistsAndKeepsTheBestScore()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, TestDataIds.UserId, Roles.Standard);
        var token = await TokenAsync(client);
        var questions = await QuestionsAsync(factory, PublicFullId);
        var foreignQuestion = (await QuestionsAsync(factory, PublicTranscriptQuizId)).Single();

        // All correct (lower-case letters are accepted) + an answer for a question of another lesson (ignored).
        var correct = questions.Select(q => (q.Id, q.Correct.ToLowerInvariant())).Append((foreignQuestion.Id, "A"));
        using (var response = await PostJsonAsync(client, "/Home/Listening/EvaluateQuiz", AnswersJson(PublicFullId, correct), token))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var data = (await JsonAsync(response)).GetProperty("data");
            Assert.Equal(2, data.GetProperty("totalQuestions").GetInt32());
            Assert.Equal(2, data.GetProperty("correctCount").GetInt32());
            Assert.Equal(100, data.GetProperty("scorePercent").GetInt32());
            Assert.Equal(100, data.GetProperty("bestScorePercent").GetInt32());
            Assert.Equal(2, data.GetProperty("answers").GetArrayLength());
        }

        var stored = await ProgressAsync(factory, TestDataIds.UserId, PublicFullId);
        Assert.NotNull(stored);
        Assert.True(stored!.QuizCompleted);
        Assert.Equal(100, stored.QuizScore);

        // A worse retry (one invalid letter, one wrong) keeps the best score.
        var worse = questions.Select((q, i) => (q.Id, i == 0 ? "E" : "D"));
        using (var response = await PostJsonAsync(client, "/Home/Listening/EvaluateQuiz", AnswersJson(PublicFullId, worse), token))
        {
            var data = (await JsonAsync(response)).GetProperty("data");
            Assert.Equal(0, data.GetProperty("scorePercent").GetInt32());
            Assert.Equal(100, data.GetProperty("bestScorePercent").GetInt32());
        }

        Assert.Equal(100, (await ProgressAsync(factory, TestDataIds.UserId, PublicFullId))!.QuizScore);
    }

    // ------------------------------------------------------------------
    // B3 / B14: SaveProgress
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(PremiumPrivateId)]
    [InlineData(UnpublishedId)]
    public async Task SaveProgress_LessonTheUserMayNotUse_Returns404AndCreatesNoRow(int lessonId)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, TestDataIds.UserId, Roles.Standard);
        var token = await TokenAsync(client);

        using var response = await SaveAsync(client, lessonId, "{\"transcriptCompleted\":true}", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await ProgressAsync(factory, TestDataIds.UserId, lessonId));
    }

    [Fact]
    public async Task SaveProgress_ClientQuizScore_IsClampedAndNeverLowersTheBest()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, TestDataIds.UserId, Roles.Standard);
        var token = await TokenAsync(client);

        using (await SaveAsync(client, PublicFullId, "{\"quizCompleted\":true,\"quizScore\":150}", token)) { }
        Assert.Equal(100, (await ProgressAsync(factory, TestDataIds.UserId, PublicFullId))!.QuizScore);

        using (await SaveAsync(client, PublicFullId, "{\"quizScore\":-5}", token)) { }
        Assert.Equal(100, (await ProgressAsync(factory, TestDataIds.UserId, PublicFullId))!.QuizScore);
    }

    // ------------------------------------------------------------------
    // B1 / L-R3: completion rule + exactly one Goals credit
    // ------------------------------------------------------------------

    private static Task<int> ListeningCreditsAsync(TestWebApplicationFactory factory, int userId)
        => WithDbAsync(factory, async db => (await db.UserDailyActivities.AsNoTracking()
            .Where(a => a.UserId == userId && a.ActivityDate == BusinessDateHelper.Today)
            .Select(a => (int?)a.ListeningCompletedCount).FirstOrDefaultAsync()) ?? 0);

    [Fact]
    public async Task Completion_RequiresEverySectionThatHasContent_AndCreditsOncePerLesson()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, TestDataIds.UserId, Roles.Standard);
        var token = await TokenAsync(client);
        var userId = TestDataIds.UserId;

        // transcript + quiz + vocab: only complete after all three.
        using (await SaveAsync(client, PublicFullId, "{\"transcriptCompleted\":true}", token)) { }
        Assert.Null((await ProgressAsync(factory, userId, PublicFullId))!.CompletedAt);

        var fullQuestions = await QuestionsAsync(factory, PublicFullId);
        using (var evaluate = await PostJsonAsync(client, "/Home/Listening/EvaluateQuiz", AnswersJson(PublicFullId, fullQuestions.Select(q => (q.Id, q.Correct))), token))
            Assert.Equal(HttpStatusCode.OK, evaluate.StatusCode);
        Assert.Null((await ProgressAsync(factory, userId, PublicFullId))!.CompletedAt);
        Assert.Equal(0, await ListeningCreditsAsync(factory, userId));

        using (await SaveAsync(client, PublicFullId, "{\"vocabReviewed\":true}", token)) { }
        Assert.NotNull((await ProgressAsync(factory, userId, PublicFullId))!.CompletedAt);
        Assert.Equal(1, await ListeningCreditsAsync(factory, userId));

        using (await SaveAsync(client, PublicFullId, "{\"vocabReviewed\":true}", token)) { }
        Assert.Equal(1, await ListeningCreditsAsync(factory, userId));

        // transcript only: complete once the transcript is marked.
        using (await SaveAsync(client, PublicTranscriptOnlyId, "{\"transcriptCompleted\":true}", token)) { }
        Assert.NotNull((await ProgressAsync(factory, userId, PublicTranscriptOnlyId))!.CompletedAt);
        Assert.Equal(2, await ListeningCreditsAsync(factory, userId));
        using (await SaveAsync(client, PublicTranscriptOnlyId, "{\"transcriptCompleted\":true}", token)) { }
        Assert.Equal(2, await ListeningCreditsAsync(factory, userId));

        // transcript + quiz (no vocab): complete after evaluating the quiz, no vocabReviewed needed.
        using (await SaveAsync(client, PublicTranscriptQuizId, "{\"transcriptCompleted\":true}", token)) { }
        Assert.Null((await ProgressAsync(factory, userId, PublicTranscriptQuizId))!.CompletedAt);
        var quizQuestions = await QuestionsAsync(factory, PublicTranscriptQuizId);
        using (var evaluate = await PostJsonAsync(client, "/Home/Listening/EvaluateQuiz", AnswersJson(PublicTranscriptQuizId, quizQuestions.Select(q => (q.Id, "D"))), token))
            Assert.Equal(HttpStatusCode.OK, evaluate.StatusCode);
        Assert.NotNull((await ProgressAsync(factory, userId, PublicTranscriptQuizId))!.CompletedAt);
        Assert.Equal(3, await ListeningCreditsAsync(factory, userId));
    }

    // ------------------------------------------------------------------
    // B4 / B5: index data
    // ------------------------------------------------------------------

    [Fact]
    public async Task Index_LoadsProgressStatusAndOverviewNumbers()
    {
        await using var factory = await CreateFactoryAsync();
        var userId = TestDataIds.UserId;
        var now = DateTime.UtcNow;
        await WithDbAsync(factory, async db =>
        {
            db.UserListeningProgresses.AddRange(
                new UserListeningProgress { UserId = userId, LessonId = PublicFullId, TranscriptCompleted = true, LastAccessedAt = now.AddMinutes(-5) },
                new UserListeningProgress { UserId = userId, LessonId = PublicTranscriptOnlyId, TranscriptCompleted = true, CompletedAt = now.AddDays(-1), LastAccessedAt = now.AddDays(-1) });
            await db.SaveChangesAsync();
            return 0;
        });

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IListeningService>();

        var vm = await service.GetIndexViewModelAsync(null, null, userId);
        var cards = vm.LessonsByLevel.Values.SelectMany(c => c).ToDictionary(c => c.Id);

        Assert.Equal("progress", cards[PublicFullId].Status);
        Assert.Equal(1, cards[PublicFullId].StepsDone);
        Assert.Equal(3, cards[PublicFullId].StepsTotal);
        Assert.False(cards[PublicFullId].IsCompleted);
        Assert.Equal("done", cards[PublicTranscriptOnlyId].Status);
        Assert.True(cards[PublicTranscriptOnlyId].IsCompleted);
        Assert.Equal(1, cards[PublicTranscriptOnlyId].StepsDone);
        Assert.Equal(1, cards[PublicTranscriptOnlyId].StepsTotal);
        Assert.Equal("new", cards[PublicTranscriptQuizId].Status);
        Assert.Equal(0, cards[PublicTranscriptQuizId].StepsDone);
        Assert.Equal(2, cards[PublicTranscriptQuizId].StepsTotal);

        Assert.Equal(3, vm.TotalLessons);
        Assert.Equal(1, vm.CompletedLessons);
        Assert.Equal(1, vm.InProgressLessons);
        Assert.Equal(PublicFullId, vm.ContinueLesson?.Id);
        Assert.True(vm.IsAuthenticated);
        Assert.False(vm.CanImport);

        // The overview ignores the level/topic filter.
        var filtered = await service.GetIndexViewModelAsync("B1", null, userId);
        Assert.Equal(3, filtered.TotalLessons);
        Assert.Equal(1, filtered.CompletedLessons);
        Assert.Equal(PublicTranscriptOnlyId, Assert.Single(filtered.LessonsByLevel.Values.SelectMany(c => c)).Id);

        // Guests see no progress.
        var guest = await service.GetIndexViewModelAsync(null, null, null);
        Assert.False(guest.IsAuthenticated);
        Assert.False(guest.CanImport);
        Assert.Null(guest.ContinueLesson);
        Assert.All(guest.LessonsByLevel.Values.SelectMany(c => c), c => Assert.Equal("new", c.Status));
    }

    [Fact]
    public async Task Index_PremiumUser_CanImport_AndContinuesAnOwnPrivateLesson()
    {
        await using var factory = await CreateFactoryAsync();
        await WithDbAsync(factory, async db =>
        {
            db.UserListeningProgresses.Add(new UserListeningProgress { UserId = PremiumOwnerId, LessonId = PremiumPrivateId, TranscriptCompleted = true, LastAccessedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return 0;
        });

        using var scope = factory.Services.CreateScope();
        var vm = await scope.ServiceProvider.GetRequiredService<IListeningService>().GetIndexViewModelAsync(null, null, PremiumOwnerId);

        Assert.True(vm.CanImport);
        Assert.Equal(PremiumPrivateId, vm.ContinueLesson?.Id);
        Assert.Equal(2, vm.MyLessons.Count);
        Assert.Equal(3, vm.TotalLessons); // private lessons never count toward the public overview
    }

    [Fact]
    public async Task Index_TopicFilter_NeverListsPrivateTopics()
    {
        await using var factory = await CreateFactoryAsync();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IListeningService>();

        foreach (int? userId in new int?[] { null, TestDataIds.UserId, PremiumOwnerId })
        {
            var vm = await service.GetIndexViewModelAsync(null, null, userId);
            Assert.DoesNotContain(PrivateTopic, vm.Topics);
            Assert.Equal(new[] { "Daily", "Travel" }, vm.Topics);
        }

        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var html = await (await guest.GetAsync("/Home/Listening")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(PrivateTopic, html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Index page contract (L4)
    // ------------------------------------------------------------------

    private static async Task<string> IndexHtmlAsync(HttpClient client, string url = "/Home/Listening")
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task IndexPage_RendersTheSharedWrapperConfigAndNoLegacyHooks()
    {
        await using var factory = await CreateFactoryAsync();
        using var student = Client(factory, TestDataIds.UserId, Roles.Standard);
        var html = await IndexHtmlAsync(student);
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains("class=\"tct-theme tct-page tl-page tl-page--index", html, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<h1[ >]"));
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"tlConfig\"", html, StringComparison.Ordinal);
        Assert.Contains("/js/listening-core.js", html, StringComparison.Ordinal);
        Assert.Contains("/js/listening-index.js", html, StringComparison.Ordinal);
        Assert.Contains("/css/study-kit.css", html, StringComparison.Ordinal);
        Assert.Contains("/css/listening.css", html, StringComparison.Ordinal);
        Assert.DoesNotContain("speaking.css", html, StringComparison.Ordinal);
        Assert.DoesNotContain("listening-index.css", html, StringComparison.Ordinal);
        Assert.DoesNotContain("placehold.co", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("English Listening Lab", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Update hàng tuần", text, StringComparison.Ordinal);
        Assert.Contains("Tiến độ nghe của bạn", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndexPage_ShowsStatusMarkersFromRealProgress()
    {
        await using var factory = await CreateFactoryAsync();
        await WithDbAsync(factory, async db =>
        {
            db.UserListeningProgresses.AddRange(
                new UserListeningProgress { UserId = TestDataIds.UserId, LessonId = PublicFullId, TranscriptCompleted = true, LastAccessedAt = DateTime.UtcNow },
                new UserListeningProgress { UserId = TestDataIds.UserId, LessonId = PublicTranscriptOnlyId, TranscriptCompleted = true, CompletedAt = DateTime.UtcNow, LastAccessedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return 0;
        });
        using var student = Client(factory, TestDataIds.UserId, Roles.Standard);
        var html = await IndexHtmlAsync(student);

        Assert.Matches($"data-id=\"{PublicFullId}\"[^>]*data-status=\"progress\"", html);
        Assert.Matches($"data-id=\"{PublicTranscriptOnlyId}\"[^>]*data-status=\"done\"", html);
        Assert.Matches($"data-id=\"{PublicTranscriptQuizId}\"[^>]*data-status=\"new\"", html);
    }

    [Fact]
    public async Task IndexPage_DeepLink_PreselectsButStillRendersEveryLesson()
    {
        await using var factory = await CreateFactoryAsync();
        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var html = await IndexHtmlAsync(guest, "/Home/Listening?level=b1&topic=Travel");

        Assert.Contains("\"selectedLevel\":\"B1\"", html, StringComparison.Ordinal);
        Assert.Contains("\"selectedTopic\":\"Travel\"", html, StringComparison.Ordinal);
        Assert.Contains($"data-id=\"{PublicFullId}\"", html, StringComparison.Ordinal); // an A1 lesson is still in the page
        Assert.Contains($"data-id=\"{PublicTranscriptOnlyId}\"", html, StringComparison.Ordinal);

        var invalid = await IndexHtmlAsync(guest, "/Home/Listening?level=Z9&topic=Nope");
        Assert.Contains("\"selectedLevel\":\"all\"", invalid, StringComparison.Ordinal);
        Assert.Contains("\"selectedTopic\":\"all\"", invalid, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndexPage_GuestSeesTheSignInNotice_StandardSeesTheLockedPanel_PremiumSeesTheImportForm()
    {
        await using var factory = await CreateFactoryAsync();

        using var guest = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var guestText = WebUtility.HtmlDecode(await IndexHtmlAsync(guest));
        Assert.Contains("Đăng nhập để lưu tiến độ và thêm bài nghe của riêng bạn.", guestText, StringComparison.Ordinal);
        Assert.DoesNotContain("Tiến độ nghe của bạn", guestText, StringComparison.Ordinal);
        Assert.DoesNotContain("tlImport", guestText, StringComparison.Ordinal);

        using var student = Client(factory, TestDataIds.UserId, Roles.Standard);
        var studentHtml = await IndexHtmlAsync(student);
        Assert.Contains("Thêm bài nghe từ YouTube là tính năng Premium.", WebUtility.HtmlDecode(studentHtml), StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"tlImport\"", studentHtml, StringComparison.Ordinal);

        using var premium = Client(factory, PremiumOwnerId, Roles.Premium);
        var premiumHtml = await IndexHtmlAsync(premium);
        Assert.Contains("id=\"tlImport\"", premiumHtml, StringComparison.Ordinal);
        Assert.Contains("data-mine-id=\"" + PremiumPrivateId + "\"", premiumHtml, StringComparison.Ordinal);
        Assert.Contains("data-tl-delete=\"" + PremiumPrivateId + "\"", premiumHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-mine-id=\"" + StandardPrivateId + "\"", premiumHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndexPage_DowngradedOwner_SeesALockedPrivateCardWithoutDeleteOrLink()
    {
        await using var factory = await CreateFactoryAsync();
        using var owner = Client(factory, StandardOwnerId, Roles.Standard);
        var html = await IndexHtmlAsync(owner);

        Assert.Contains("data-mine-id=\"" + StandardPrivateId + "\"", html, StringComparison.Ordinal);
        Assert.Contains("tl-card--locked", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-tl-delete", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"/Home/Listening/Practice/{StandardPrivateId}", html, StringComparison.Ordinal);
        Assert.Contains("/Account/Premium", html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Practice view model flags
    // ------------------------------------------------------------------

    [Fact]
    public async Task PracticeViewModel_ExposesPermissionFlagsAndCompletion()
    {
        await using var factory = await CreateFactoryAsync();
        await WithDbAsync(factory, async db =>
        {
            db.UserListeningProgresses.Add(new UserListeningProgress { UserId = TestDataIds.UserId, LessonId = PublicTranscriptOnlyId, TranscriptCompleted = true, CompletedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return 0;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IListeningService>();

        var guest = await service.GetPracticeViewModelAsync(PublicFullId, null);
        Assert.NotNull(guest);
        Assert.False(guest!.IsAuthenticated);
        Assert.False(guest.CanUseAi);
        Assert.False(guest.CanTranslate);
        Assert.False(guest.CanUseSpeed);
        Assert.False(guest.IsOwnLesson);

        var student = await service.GetPracticeViewModelAsync(PublicTranscriptOnlyId, TestDataIds.UserId);
        Assert.True(student!.IsAuthenticated);
        Assert.True(student.IsCompleted);
        Assert.False(student.CanUseAi);
        Assert.False(student.CanUseSpeed);

        var premiumOwn = await service.GetPracticeViewModelAsync(PrivateNoQuizId, PremiumOwnerId);
        Assert.True(premiumOwn!.IsOwnLesson);
        Assert.True(premiumOwn.CanUseAi);
        Assert.True(premiumOwn.CanTranslate);
        Assert.True(premiumOwn.CanUseSpeed);

        var premiumOnPublic = await service.GetPracticeViewModelAsync(PublicTranscriptOnlyId, PremiumOwnerId);
        Assert.False(premiumOnPublic!.IsOwnLesson);
        Assert.False(premiumOnPublic.CanUseAi);      // shared lessons: Admin only (L-R6)
        Assert.True(premiumOnPublic.CanTranslate);   // current rule kept
        Assert.True(premiumOnPublic.CanUseSpeed);

        var adminOnPublic = await service.GetPracticeViewModelAsync(PublicTranscriptOnlyId, TestDataIds.AdminUserId);
        Assert.True(adminOnPublic!.CanUseAi);
        Assert.True(adminOnPublic.CanTranslate);
    }

    // ------------------------------------------------------------------
    // B8: AI writes on shared lessons need Admin
    // ------------------------------------------------------------------

    [Fact]
    public async Task GenerateQuiz_PremiumNonAdmin_OnPublicLesson_Returns400AndWritesNothing()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, PremiumOwnerId, Roles.Premium);
        var token = await TokenAsync(client);

        using var response = await PostFormAsync(client, "/Home/Listening/My/GenerateQuiz",
            new Dictionary<string, string> { ["lessonId"] = PublicTranscriptOnlyId.ToString() }, token);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("quản trị viên", WebUtility.HtmlDecode(JsonDocument.Parse(body).RootElement.GetProperty("message").GetString()!), StringComparison.Ordinal);
        Assert.Equal(0, await WithDbAsync(factory, db => db.ListeningQuizQuestions.CountAsync(q => q.LessonId == PublicTranscriptOnlyId)));
    }

    [Fact]
    public async Task GenerateQuiz_PremiumOwner_OnOwnPrivateLesson_IsNotRejectedForTheAdminOnlyReason()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, PremiumOwnerId, Roles.Premium);
        var token = await TokenAsync(client);

        using var response = await PostFormAsync(client, "/Home/Listening/My/GenerateQuiz",
            new Dictionary<string, string> { ["lessonId"] = PrivateNoQuizId.ToString() }, token);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("quản trị viên", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Antiforgery
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("/Home/Listening/EvaluateQuiz", "json")]
    [InlineData("/Home/Listening/SaveProgress/91001", "json")]
    [InlineData("/Home/Listening/My/Create", "form")]
    [InlineData("/Home/Listening/My/Delete", "form")]
    [InlineData("/Home/Listening/My/GenerateQuiz", "form")]
    [InlineData("/Home/Listening/Translate/91001", "form")]
    public async Task Posts_WithoutAntiforgeryToken_Return400(string url, string kind)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Client(factory, PremiumOwnerId, Roles.Premium);

        using var response = kind == "json"
            ? await PostJsonAsync(client, url, "{\"lessonId\":91001,\"answers\":{},\"transcriptCompleted\":true}", token: null)
            : await PostFormAsync(client, url, new Dictionary<string, string> { ["lessonId"] = "91001", ["youtubeUrl"] = "https://youtu.be/dQw4w9WgXcQ" }, token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
