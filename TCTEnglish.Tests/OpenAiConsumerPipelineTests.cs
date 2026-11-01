using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI;
using TCTEnglish.Models;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>Real domain services + DI + Responses serializer/parser, with no external HTTP.</summary>
public class OpenAiConsumerPipelineTests
{
    private const string Feedback = """{"passed":true,"overallFeedback":"Câu rõ ràng.","meaningFeedback":"Đúng ý.","grammarFeedback":"Đúng ngữ pháp.","naturalnessFeedback":"Tự nhiên.","wordChoiceFeedback":"Phù hợp.","suggestedRewrite":""}""";
    private const string Generation = """{"detectedSourceLanguage":"vi","suggestedTitle":"A daily update","suggestedTopic":"Business","suggestedLevel":"beginner","suggestedContentType":"emails","previewText":"Chúng ta cần chốt lịch trong tuần này.","sentences":[{"vietnameseText":"Chúng ta cần chốt lịch trong tuần này.","englishMeaning":"We need to finalize the schedule this week.","breakAfter":true}]}""";

    [Fact]
    public async Task Chat_ContinuesConversation_AndPersistsActualUsageThroughRealAdapter()
    {
        var calls = 0;
        await using var factory = Factory(async (request, ct) =>
        {
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(2048, doc.RootElement.GetProperty("max_output_tokens").GetInt32());
            if (++calls == 2) Assert.Contains(doc.RootElement.GetProperty("input").EnumerateArray(), x => x.GetProperty("role").GetString() == "assistant");
            return OpenAiProviderClientTests.Response(OpenAiProviderClientTests.Completed("Practice every day."));
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAiChatService>();
        var first = await service.SendAsync(TestDataIds.UserId, null, "Explain present simple", default);
        await service.SendAsync(TestDataIds.UserId, first.ConversationId, "Give another example", default);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var replies = await db.AiMessages.AsNoTracking().Where(x => x.Role == AiMessageRole.Assistant).ToListAsync();
        Assert.Equal(2, replies.Count);
        Assert.All(replies, x => { Assert.Equal(12, x.PromptTokens); Assert.Equal(20, x.CompletionTokens); });
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task WritingFeedbackAndVocabulary_ParseTheirOwnJsonContracts()
    {
        var calls = 0;
        await using var factory = Factory(async (request, ct) =>
        {
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var cap = doc.RootElement.GetProperty("max_output_tokens").GetInt32();
            Assert.Contains(cap, new[] { 2048, 1536 });
            calls++;
            return OpenAiProviderClientTests.Response(OpenAiProviderClientTests.Completed(cap == 2048 ? Feedback
                : """{"phonetic":"/wɜːd/","definitions":[{"partOfSpeech":"noun","definition":"từ"}],"example":"This is a word.","exampleTranslation":"Đây là một từ."}"""));
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var feedback = await scope.ServiceProvider.GetRequiredService<IWritingAiEvaluationService>()
            .TryEvaluateSentenceAsync(new(1, "Tôi học mỗi ngày.", "I study daily.", "I study every day."));
        Assert.NotNull(feedback);
        Assert.True(feedback.Passed);
        var vocab = await scope.ServiceProvider.GetRequiredService<IVocabSuggestService>().SuggestAsync("word");
        Assert.True(vocab.Success);
        Assert.Equal("từ", Assert.Single(vocab.Definitions).Definition);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task AppStreaming_KeepsNinetyCharacterChunks_ThroughRealResponsesAdapter()
    {
        var text = new string('a', 225);
        await using var factory = TextFactory(text);
        await factory.InitializeAsync();
        Guid conversationId;
        using (var scope = factory.Services.CreateScope())
            conversationId = (await scope.ServiceProvider.GetRequiredService<IAiConversationService>()
                .CreateConversationAsync(TestDataIds.UserId, "Practice", default)).Id;
        await using var session = await factory.Services.GetRequiredService<IAiStreamingService>()
            .StartStreamAsync(TestDataIds.UserId, conversationId, "Give an example", Guid.NewGuid().ToString("N"), "127.0.0.1", default);
        Assert.Equal(new[] { 90, 90, 45 }, session.Chunks.Select(x => x.Length));
        Assert.Equal(text, string.Concat(session.Chunks));
    }

    [Fact]
    public async Task Vocabulary_WhenCloudUnavailable_ReturnsFailureWithoutEscapingException()
    {
        await using var factory = Factory((_, _) => Task.FromResult(OpenAiProviderClientTests.Response("{}", HttpStatusCode.Unauthorized)));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IVocabSuggestService>().SuggestAsync("word");
        Assert.False(result.Success);
        Assert.NotEmpty(result.ErrorMessage!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WritingGeneration_ReplayOrIncomplete_DoesNotDuplicateOrPersistPartialExercise(bool incomplete)
    {
        var calls = 0;
        await using var factory = Factory(async (request, ct) =>
        {
            calls++;
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(6144, doc.RootElement.GetProperty("max_output_tokens").GetInt32());
            var response = OpenAiProviderClientTests.Completed(Generation);
            if (incomplete) response = response.Replace("\"status\":\"completed\"", "\"status\":\"incomplete\"");
            return OpenAiProviderClientTests.Response(response);
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var before = await db.WritingExercises.CountAsync();
        var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        var request = new WritingCreateFromAiRequestViewModel { SourceText = "Chúng ta cần chốt lịch trong tuần này. " + Guid.NewGuid(), IdempotencyKey = Guid.NewGuid().ToString("N") };
        var result = await service.CreateFromAiAsync(request, TestDataIds.AdminUserId);
        if (incomplete)
        {
            Assert.Equal(WritingCreateFromAiOutcome.Failed, result.Outcome);
            Assert.Equal("incomplete_response", result.ErrorCode);
            Assert.Equal(before, await db.WritingExercises.CountAsync());
            Assert.False((await db.WritingGenerationLogs.SingleAsync()).IsSuccess);
        }
        else
        {
            Assert.Equal(WritingCreateFromAiOutcome.Success, result.Outcome);
            var replay = await service.CreateFromAiAsync(request, TestDataIds.AdminUserId);
            Assert.True(replay.IsReplay);
            Assert.Equal(result.ExerciseId, replay.ExerciseId);
            Assert.Equal(before + 1, await db.WritingExercises.CountAsync());
        }
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ReadingReview_MapsScoreFeedbackApprovalThroughAdapter()
    {
        await using var factory = TextFactory("""{"score":85,"feedback":"Bản dịch đúng ý và tự nhiên.","isApproved":true}""");
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var passage = new ReadingPassage { Title = "A day", Content = "I study every day.", Level = "A1", CreatedAt = DateTime.UtcNow };
        db.ReadingPassages.Add(passage); await db.SaveChangesAsync();
        var result = await scope.ServiceProvider.GetRequiredService<IReadingTranslationService>().SubmitTranslationAsync(
            TestDataIds.UserId, passage.Id, "Một ngày", """[{"original":"I study every day.","translated":"Tôi học mỗi ngày."}]""");
        Assert.True(result.Success);
        Assert.Equal(85, result.AiScore);
        Assert.True(result.IsAiApproved);
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(true, "auth")]
    [InlineData(true, "slow")]
    [InlineData(true, "success")]
    public async Task LocalWriting_CloudFailureOrThreeSecondBudget_PreservesAttemptAndLocalDecision(bool enabled, string cloud)
    {
        var calls = 0;
        await using var factory = Factory(async (_, ct) =>
        {
            calls++;
            if (cloud == "slow") await Task.Delay(Timeout.Infinite, ct);
            return cloud == "auth" ? OpenAiProviderClientTests.Response("{}", HttpStatusCode.Unauthorized)
                : OpenAiProviderClientTests.Response(OpenAiProviderClientTests.Completed(Feedback.Replace("\"passed\":true", "\"passed\":false")));
        });
        // Options belong to this isolated host and are set before the scoped services resolve.
        var options = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TCTEnglish.Services.Writing.WritingEvaluationOptions>>().Value;
        options.EvaluationMode = "Local"; options.CloudEnhancementEnabled = enabled; options.CloudEnhancementTimeoutSeconds = 3;
        if (cloud == "missing") factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value.ApiKey = "";
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var sentence = await db.WritingExerciseSentences.FirstAsync();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var result = await scope.ServiceProvider.GetRequiredService<IWritingService>().EvaluateWritingSentenceAsync(
            sentence.WritingExerciseId, sentence.Id, sentence.EnglishMeaning, TestDataIds.UserId);
        Assert.NotNull(result); Assert.True(result.Passed);
        Assert.Equal(enabled && cloud == "success", result.UsedAi);
        if (cloud == "slow") Assert.InRange(clock.Elapsed.TotalSeconds, 2.8, 5.5);
        Assert.Equal(enabled && cloud != "missing" ? 1 : 0, calls);
        var attempt = await db.UserWritingAttempts.AsNoTracking().SingleAsync(a => a.UserId == TestDataIds.UserId && a.WritingExerciseSentenceId == sentence.Id);
        Assert.True(attempt.Passed);
        Assert.Equal(sentence.EnglishMeaning, attempt.SubmittedAnswer);
        var progress = await db.UserWritingSentenceProgresses.AsNoTracking().SingleAsync(p => p.UserId == TestDataIds.UserId && p.SentenceId == sentence.Id);
        Assert.True(progress.IsPassed);
        Assert.Equal(1, progress.AttemptCount);
        var restored = await scope.ServiceProvider.GetRequiredService<IWritingService>().GetWritingPracticeDataAsync(sentence.WritingExerciseId, TestDataIds.UserId);
        Assert.NotNull(restored);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Listening_QuizAndTranslation_UseArrayContractsAndRejectCountMismatch(bool wrongCount)
    {
        await using var factory = Factory(async (request, ct) =>
        {
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var cap = doc.RootElement.GetProperty("max_output_tokens").GetInt32();
            var text = cap == 4096 ? """[{"question":"When do we study?","optionA":"Every day","optionB":"Never","optionC":"Once","optionD":"At midnight","correctAnswer":"A","explanation":"Bài nói every day."}]"""
                : wrongCount ? "[]" : "[\"Tôi học mỗi ngày.\"]";
            return OpenAiProviderClientTests.Response(OpenAiProviderClientTests.Completed(text));
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var lesson = new ListeningLesson { Title = "Study", Level = "A1", Topic = "Daily", TranscriptLines = [new() { OrderIndex = 0, Text = "I study every day.", Speaker = "A" }] };
        db.ListeningLessons.Add(lesson); await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<TCTEnglish.Services.IListeningService>();
        Assert.Equal(OperationStatus.Success, (await service.GenerateQuizFromTranscriptAsync(lesson.Id, TestDataIds.AdminUserId)).Status);
        var result = await service.TranslateTranscriptAsync(lesson.Id, TestDataIds.AdminUserId);
        Assert.Equal(wrongCount ? OperationStatus.Invalid : OperationStatus.Success, result.Status);
        db.ChangeTracker.Clear();
        Assert.Equal(wrongCount ? null : "Tôi học mỗi ngày.", (await db.ListeningTranscriptLines.SingleAsync()).VietnameseMeaning);
        Assert.Single(await db.ListeningQuizQuestions.ToListAsync());
    }

    internal static TestWebApplicationFactory TextFactory(string text) => Factory((_, _) => Task.FromResult(OpenAiProviderClientTests.Response(OpenAiProviderClientTests.Completed(text))));

    [Fact]
    public async Task Listening_LaterBatchMismatch_DoesNotLeaveTrackedPartialTranslations()
    {
        var calls = 0;
        await using var factory = Factory((_, _) => Task.FromResult(OpenAiProviderClientTests.Response(
            OpenAiProviderClientTests.Completed(++calls == 1 ? JsonSerializer.Serialize(Enumerable.Repeat("Bản dịch", 30)) : "[]"))));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var lesson = new ListeningLesson { Title = "Batch", Level = "A1", Topic = "Daily", TranscriptLines = Enumerable.Range(0, 31).Select(i => new ListeningTranscriptLine { OrderIndex = i, Speaker = "A", Text = "I study every day." }).ToList() };
        db.ListeningLessons.Add(lesson); await db.SaveChangesAsync();
        var result = await scope.ServiceProvider.GetRequiredService<TCTEnglish.Services.IListeningService>().TranslateTranscriptAsync(lesson.Id, TestDataIds.AdminUserId);
        Assert.Equal(OperationStatus.Invalid, result.Status);
        Assert.All(lesson.TranscriptLines, line => Assert.Null(line.VietnameseMeaning));
        Assert.False(db.ChangeTracker.HasChanges());
    }
    internal static TestWebApplicationFactory Factory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => new(services =>
    {
        services.RemoveAll<IAiProviderClient>();
        services.AddScoped<IAiProviderClient, OpenAiProviderClient>();
        services.PostConfigure<AiOptions>(o => { o.ApiKey = "fixture-key"; o.Model = "gpt-6-luna"; o.BaseUrl = "https://api.openai.com/v1"; o.ReasoningEffort = "low"; o.MaxOutputTokens = 2048; o.RetryMaxAttempts = 0; });
        services.AddHttpClient(OpenAiProviderClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new OpenAiProviderClientTests.Handler(send));
        services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName)
            .ConfigurePrimaryHttpMessageHandler(() => new OpenAiProviderClientTests.Handler((_, _) => throw new InvalidOperationException("Unexpected outbound HTTP in offline test.")));
    });
}
