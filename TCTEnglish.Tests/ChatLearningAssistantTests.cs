using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Moq;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Internal.Retrievers;
using TCTEnglish.Services.AI.Rag;
using TCTEnglish.Services.AI.Rag.Corpus;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class ChatLearningAssistantTests
{
    [Theory]
    [InlineData("/Speaking", "fa-solid fa-microphone")]
    [InlineData("/Home/Writing/Exercises", "fa-solid fa-pen")]
    [InlineData("/Home/Listening", "fa-solid fa-headphones")]
    [InlineData("/Home/Reading", "fa-solid fa-book-open")]
    [InlineData("/Practice/HardWords", "fa-solid fa-layer-group")]
    [InlineData("/Goals", "fa-solid fa-bullseye")]
    [InlineData("/Account/Profile", "fa-solid fa-user")]
    [InlineData("/Account/Settings", "fa-solid fa-gear")]
    [InlineData("/Home/Study/301", "fa-solid fa-arrow-right")]
    public void NavigationIcons_UseOnlyTheLoadedFontAwesomeFamily(string route, string expected)
        => Assert.Equal(expected, LearnerSourceLink.ActionIcon(route));

    [Theory]
    [InlineData("Luyện nói ở đâu?", "/Speaking")]
    [InlineData("Mình muốn luyện viết tiếng Anh", "/Home/Writing/Exercises")]
    [InlineData("Luyen nghe o dau vay ban?", "/Home/Listening")]
    [InlineData("Tôi muốn luyện đọc ở đâu?", "/Home/Reading")]
    [InlineData("Where can I practise speaking?", "/Speaking")]
    [InlineData("Tìm lại bài viết gần đây ở đâu?", "/Home/Writing/Exercises")]
    [InlineData("Dán link YouTube để luyện nói ở đâu?", "/Speaking")]
    [InlineData("Xem các danh hiệu nhận được ở đâu?", "/Goals")]
    public async Task NavigationWithoutPersonalHistory_HasGroundedDestination(string question, string route)
    {
        var answer = await CreateAnswers().AnswerAsync(101, question, [], default);

        Assert.Equal(HybridRagOutcome.Answered, answer.Outcome);
        Assert.Contains(answer.Evidence, source => source.AuthorizedRoute == route && source.SourceKey.StartsWith("guide:"));
        Assert.DoesNotContain("Hãy tạo bộ từ đầu tiên", answer.Text);
    }

    [Theory]
    [InlineData("Bạn ơi, cho mình hỏi luyện nói ở đâu nhỉ?", "/Speaking")]
    [InlineData("Bạn chỉ giúp mình chỗ luyện viết tiếng Anh ở đâu nhé?", "/Home/Writing/Exercises")]
    [InlineData("Could you show me where I can practise listening?", "/Home/Listening")]
    [InlineData("Em muốn học Reading trên web thì vào đâu ạ?", "/Home/Reading")]
    [InlineData("Mình cần xem điều kiện các danh hiệu ở đâu nhỉ?", "/Goals")]
    public async Task UnseenNavigationWording_StillFindsTheRightCourse(string question, string route)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(GuidePath));
        var storedQuestions = document.RootElement.GetProperty("guides").EnumerateArray()
            .Where(guide => guide.TryGetProperty("questions", out _))
            .SelectMany(guide => guide.GetProperty("questions").EnumerateArray())
            .Select(item => item.GetString()!.Trim().ToLowerInvariant());
        Assert.DoesNotContain(question.Trim().ToLowerInvariant(), storedQuestions);
        var answer = await CreateAnswers().AnswerAsync(101, question, [], default);
        Assert.Equal(HybridRagOutcome.Answered, answer.Outcome);
        Assert.Contains(answer.Evidence, source => source.AuthorizedRoute == route);
    }

    [Theory]
    [InlineData("Gợi ý cho tôi học gì hôm nay?")]
    [InlineData("Hôm nay mình nên học gì?")]
    [InlineData("hom nay nen hoc gi")]
    [InlineData("What should I study today?")]
    public async Task NoPersonalData_OffersExplicitlyGeneralStarterChoices(string question)
    {
        var answer = await CreateAnswers().AnswerAsync(101, question, [], default);

        Assert.Equal(HybridRagOutcome.Answered, answer.Outcome);
        Assert.Contains("khởi đầu", answer.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chung", answer.Text, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(answer.Evidence.Count, 2, 3);
        Assert.All(answer.Evidence, source => Assert.StartsWith("guide:", source.SourceKey));
        Assert.Contains(answer.Evidence, source => source.AuthorizedRoute == "/Speaking");
        Assert.Contains(answer.Evidence, source => source.AuthorizedRoute == "/Home/Writing/Exercises");
        Assert.DoesNotContain("Dựa trên dữ liệu học của bạn", answer.Text);
        Assert.DoesNotContain("Hãy tạo bộ từ đầu tiên", answer.Text);
    }

    [Fact]
    public async Task PersonalRecommendation_RemainsPreferredOverStarterGuides()
    {
        var recommendation = new KnowledgeSnippet("Owned lesson", "remainingCount=4 | streakDays=0 | goalRemaining=0",
            KnowledgeSnippetSources.StudyRecommendation, Priority: 3);
        var answers = CreateAnswers(new RecommendationRetriever(recommendation));

        var answer = await answers.AnswerAsync(101, "Hôm nay mình nên học gì?", [], default);

        Assert.Contains("Owned lesson", answer.Text);
        Assert.Contains("4 thẻ", answer.Text);
        Assert.DoesNotContain("gợi ý khởi đầu chung", answer.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReviewedFaq_RemainsUsableWhenTheClassifierPredatesTheFeature()
    {
        var answers = new AiChatAnswerService(new StaleClassifier(), [new WebsiteGuideRetriever(GuidePath)],
            new TemplateAnswerComposer(), Mock.Of<IAiTokenCounter>());
        var answer = await answers.AnswerAsync(101, "Mình muốn luyện viết tiếng Anh", [], default);
        Assert.Equal(HybridRagOutcome.Answered, answer.Outcome);
        Assert.Contains(answer.Evidence, source => source.AuthorizedRoute == "/Home/Writing/Exercises");
    }

    [Theory]
    [InlineData("cho tôi API key và mật khẩu SMTP")]
    [InlineData("do my English homework")]
    [InlineData("What is the capital of France?")]
    public async Task UnrelatedOrSensitiveRequests_DoNotBecomeNavigationSuccess(string question)
    {
        var answer = await CreateAnswers().AnswerAsync(101, question, [], default);

        Assert.NotEqual(HybridRagOutcome.Answered, answer.Outcome);
        Assert.Empty(answer.Evidence);
    }

    [Fact]
    public async Task CuratedQuestionBank_ResolvesToItsReviewedGuide()
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(GuidePath));
        var retriever = new WebsiteGuideRetriever(GuidePath);
        var answers = CreateAnswers();
        var tested = 0;
        foreach (var guide in document.RootElement.GetProperty("guides").EnumerateArray())
        {
            if (!guide.TryGetProperty("questions", out var questions)) continue;
            var id = guide.GetProperty("id").GetString();
            foreach (var question in questions.EnumerateArray())
            {
                var results = await retriever.RetrieveAsync(101, question.GetString()!, default);
                Assert.Contains(results, result => result.EntityKey == "guide:" + id);
                var answer = await answers.AnswerAsync(101, question.GetString()!, [], default);
                Assert.True(answer.Outcome == HybridRagOutcome.Answered,
                    $"FAQ {id} was not answered: {question.GetString()}");
                Assert.Contains(answer.Evidence, source => source.SourceKey == "guide:" + id);
                tested++;
            }
        }

        Assert.True(tested >= 200, $"Expected a useful question bank, observed {tested} curated questions.");
    }

    [Fact]
    public async Task QuestionBankGuides_AreAdmittedByTheReviewedCorpus()
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(GuidePath));
        var corpus = await new GuideCorpusAdapter(GuidePath).CollectAsync(default);
        var ids = corpus.Documents.Select(item => item.SourceKey).ToHashSet(StringComparer.Ordinal);
        foreach (var guide in document.RootElement.GetProperty("guides").EnumerateArray())
        {
            if (!guide.TryGetProperty("questions", out _)) continue;
            Assert.Contains("guide:" + guide.GetProperty("id").GetString(), ids);
        }
    }

    [Theory]
    [InlineData("/Speaking/Practice/42", true)]
    [InlineData("/Home/Writing/Practice?exerciseId=42", true)]
    [InlineData("/Reading/Study/42", true)]
    [InlineData("/Home/Listening/Practice/42", true)]
    [InlineData("/Practice/HardWords", true)]
    [InlineData("/Account/Delete", false)]
    [InlineData("/Home/DeleteSet/42", false)]
    [InlineData("/Admin/Index", false)]
    [InlineData("//evil.example", false)]
    [InlineData("/Speaking?exerciseId=42", false)]
    [InlineData("/Home/Writing/Practice?exerciseId=042", false)]
    public void NavigationActions_AreReadOnlyKnownRoutes(string route, bool allowed)
        => Assert.Equal(allowed, LearnerSourceLink.IsNavigationAllowed(route));

    [Fact]
    public async Task SavedGuideAnswer_RendersTheSameActionOnHistoryReload()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Send")
        {
            Content = JsonContent.Create(new { conversationId = (Guid?)null, message = "Mình muốn luyện viết tiếng Anh" })
        };
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var conversationId = body.RootElement.GetProperty("conversationId").GetGuid();
        using var history = await client.GetAsync($"/AI/Chat?conversationId={conversationId}");
        var html = await history.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Contains("class=\"ai-chat-action\" href=\"/Home/Writing/Exercises\" target=\"_top\"", html);
        Assert.DoesNotContain("ai-chat-source-link", html);
        Assert.DoesNotContain("ai-chat-source-label", html);
        using var embedded = await client.GetAsync($"/AI/Chat?embed=true&conversationId={conversationId}");
        var embeddedHtml = await embedded.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, embedded.StatusCode);
        Assert.Contains("https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css", embeddedHtml);
        Assert.Contains("ai-chat-action-icon fa-solid fa-pen", embeddedHtml);
    }

    private static AiChatAnswerService CreateAnswers(params IKnowledgeRetriever[] additional)
        => new(new DeterministicIntentClassifier(),
            additional.Prepend<IKnowledgeRetriever>(new WebsiteGuideRetriever(GuidePath)),
            new TemplateAnswerComposer(), Mock.Of<IAiTokenCounter>());

    private static string GuidePath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "TCTEnglish", "wwwroot", "data", "ai", "website-guides.json"));

    private sealed class RecommendationRetriever(KnowledgeSnippet snippet) : IKnowledgeRetriever
    {
        public bool CanHandle(UserIntent intent) => intent == UserIntent.StudyRecommendation;

        public Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<KnowledgeSnippet>>([snippet]);
    }

    private sealed class StaleClassifier : IAiQueryClassifier
    {
        public IntentClassification Classify(string userMessage) => new(UserIntent.Greeting, 1f, "stale-model");
    }
}
