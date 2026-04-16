using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsPhase5IntegrationTests
{
    private const int ReadingReplayPassageId = 99001;
    private const int ReadingReplayQuestionId = 99011;
    private const int ReadingReplayCorrectOptionId = 99021;
    private const int ReadingReplayWrongOptionId = 99022;
    private const int ListeningReplayLessonId = 99101;

    [Fact]
    public async Task GoalsPage_DoesNotShowDeferredAreas_AndRendersReadingListeningOptions()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Goals");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Reading, Listening hiện đang tạm hoãn", body, StringComparison.Ordinal);
        Assert.Contains("value=\"Reading\"", body, StringComparison.Ordinal);
        Assert.Contains("value=\"Listening\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateGoal_ReadingAreaSubmission_CreatesGoalSuccessfully()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Goals");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Goals/UpdateGoal")
        {
            Content = new StringContent(
                "GoalEditor.GoalArea=Reading&GoalEditor.TargetValue=5",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var readingGoal = await context.UserGoals
            .AsNoTracking()
            .FirstOrDefaultAsync(goal => goal.UserId == TestDataIds.UserId && goal.GoalArea == GoalArea.Reading && goal.IsActive);

        Assert.NotNull(readingGoal);
        Assert.Equal(5, readingGoal!.TargetValue);
    }

    [Fact]
    public async Task ReadingAndListeningPages_DoNotShowDeferredGateCopy()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var readingResponse = await client.GetAsync("/Home/Reading");
        var readingBody = await readingResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, readingResponse.StatusCode);
        Assert.DoesNotContain("Reading hiện đang ở trạng thái tạm hoãn trong hệ thống Goals/XP", readingBody, StringComparison.Ordinal);

        using var listeningResponse = await client.GetAsync("/Home/Listening");
        var listeningBody = await listeningResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, listeningResponse.StatusCode);
        Assert.DoesNotContain("Listening hiện đang ở trạng thái tạm hoãn trong hệ thống Goals/XP", listeningBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitReading_ReplayCompletion_DoesNotDoubleCountReadingActivity()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedReadingReplayPassageAsync(factory);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using (var studyResponse = await client.GetAsync($"/Reading/Study/{ReadingReplayPassageId}"))
        {
            Assert.Equal(HttpStatusCode.OK, studyResponse.StatusCode);
        }

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/Reading/SubmitReading")
        {
            Content = new StringContent(
                $"passageId={ReadingReplayPassageId}&answers[{ReadingReplayQuestionId}]={ReadingReplayCorrectOptionId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        using var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/Reading/SubmitReading")
        {
            Content = new StringContent(
                $"passageId={ReadingReplayPassageId}&answers[{ReadingReplayQuestionId}]={ReadingReplayWrongOptionId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        using var replayResponse = await client.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var today = BusinessDateHelper.Today;

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == TestDataIds.UserId && candidate.ActivityDate == today);

        var history = await context.UserReadingHistories
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == TestDataIds.UserId && candidate.ReadingPassageId == ReadingReplayPassageId);

        Assert.Equal(1, activity.ReadingCompletedCount);
        Assert.Equal(10, activity.XpEarned);
        Assert.Equal(10, activity.StreakXpAwarded);
        Assert.True(history.IsCompleted);
        Assert.Equal(0, history.Score);
    }

    [Fact]
    public async Task SaveListeningProgress_ReplayCompletion_DoesNotDoubleCountListeningActivity()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedListeningReplayLessonAsync(factory);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/Listening/Practice/{ListeningReplayLessonId}");

        using (var transcriptRequest = CreateListeningProgressRequest(antiForgeryToken, ListeningReplayLessonId, "{\"transcriptCompleted\":true}"))
        using (var transcriptResponse = await client.SendAsync(transcriptRequest))
        {
            Assert.Equal(HttpStatusCode.OK, transcriptResponse.StatusCode);
        }

        using (var quizRequest = CreateListeningProgressRequest(antiForgeryToken, ListeningReplayLessonId, "{\"quizCompleted\":true,\"quizScore\":100}"))
        using (var quizResponse = await client.SendAsync(quizRequest))
        {
            Assert.Equal(HttpStatusCode.OK, quizResponse.StatusCode);
        }

        using (var vocabRequest = CreateListeningProgressRequest(antiForgeryToken, ListeningReplayLessonId, "{\"vocabReviewed\":true}"))
        using (var vocabResponse = await client.SendAsync(vocabRequest))
        {
            Assert.Equal(HttpStatusCode.OK, vocabResponse.StatusCode);
        }

        using (var replayRequest = CreateListeningProgressRequest(antiForgeryToken, ListeningReplayLessonId, "{\"vocabReviewed\":true}"))
        using (var replayResponse = await client.SendAsync(replayRequest))
        {
            Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var today = BusinessDateHelper.Today;

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == TestDataIds.UserId && candidate.ActivityDate == today);

        var progress = await context.UserListeningProgresses
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == TestDataIds.UserId && candidate.LessonId == ListeningReplayLessonId);

        Assert.Equal(1, activity.ListeningCompletedCount);
        Assert.Equal(10, activity.XpEarned);
        Assert.Equal(10, activity.StreakXpAwarded);
        Assert.NotNull(progress.CompletedAt);
    }

    private static async Task SeedReadingReplayPassageAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var passage = new ReadingPassage
        {
            Id = ReadingReplayPassageId,
            Title = "Goals replay reading",
            Content = "Replay-safe reading passage for goals integration test.",
            Level = "A1",
            Topic = "Replay",
            CreatedAt = DateTime.UtcNow,
            IsPublished = true,
            Questions = new List<ReadingQuestion>
            {
                new()
                {
                    Id = ReadingReplayQuestionId,
                    OrderIndex = 1,
                    QuestionText = "Which option is correct?",
                    Options = new List<ReadingOption>
                    {
                        new()
                        {
                            Id = ReadingReplayCorrectOptionId,
                            OptionText = "Correct",
                            IsCorrect = true
                        },
                        new()
                        {
                            Id = ReadingReplayWrongOptionId,
                            OptionText = "Wrong",
                            IsCorrect = false
                        }
                    }
                }
            }
        };

        context.ReadingPassages.Add(passage);
        await context.SaveChangesAsync();
    }

    private static async Task SeedListeningReplayLessonAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var lesson = new ListeningLesson
        {
            Id = ListeningReplayLessonId,
            Title = "Goals replay listening",
            Level = "A1",
            Topic = "Replay",
            YoutubeId = "dQw4w9WgXcQ",
            IsPublished = true,
            CreatedAt = DateTime.UtcNow,
            TranscriptLines = new List<ListeningTranscriptLine>
            {
                new()
                {
                    OrderIndex = 1,
                    Speaker = "A",
                    Text = "Replay safe listening line",
                    VietnameseMeaning = "Dòng nghe để test replay"
                }
            },
            QuizQuestions = new List<ListeningQuizQuestion>
            {
                new()
                {
                    OrderIndex = 1,
                    QuestionText = "Pick A",
                    OptionA = "A",
                    OptionB = "B",
                    OptionC = "C",
                    OptionD = "D",
                    CorrectAnswer = "A"
                }
            },
            VocabItems = new List<ListeningVocabItem>
            {
                new()
                {
                    OrderIndex = 1,
                    Word = "Replay",
                    Definition = "Run again"
                }
            }
        };

        context.ListeningLessons.Add(lesson);
        await context.SaveChangesAsync();
    }

    private static HttpRequestMessage CreateListeningProgressRequest(string antiForgeryToken, int lessonId, string jsonBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/Home/Listening/SaveProgress/{lessonId}")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return request;
    }
}
