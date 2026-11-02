using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.Writing;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;
using Xunit.Abstractions;

namespace TCTEnglish.Tests;

/// <summary>Nine synthetic consumer requests at most; real OpenAI, isolated SQLite, explicit paid opt-in.</summary>
public sealed class OpenAiLiveFeatureAcceptanceTests(ITestOutputHelper output)
{
    [FeatureLiveFact]
    public async Task EveryTextConsumer_UsesRealLunaLow_AndLocalWritingRetainsItsThreeSecondBudget()
    {
        var meter = new Meter(output);
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<OpenAiProviderClient>();
            services.AddScoped<IAiProviderClient>(sp => new RecordingProvider(sp.GetRequiredService<OpenAiProviderClient>(), meter));
            services.PostConfigure<AiOptions>(o => o.RetryMaxAttempts = 0);
            // Keep unrelated image-provider credentials/network out of this paid OpenAI acceptance.
            services.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() =>
                new OpenAiProviderClientTests.Handler((_, _) => throw new InvalidOperationException("Image HTTP disabled for synthetic cloud acceptance.")));
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<DbflashcardContext>();

        meter.Label = "chat-first";
        var chat = services.GetRequiredService<IAiChatService>();
        var first = await chat.SendAsync(TestDataIds.UserId, null, "Give one short example of the English present simple tense.", default);
        Assert.False(string.IsNullOrWhiteSpace(first.Text));
        meter.Label = "chat-follow-up";
        var next = await chat.SendAsync(TestDataIds.UserId, first.ConversationId, "Make the same example negative.", default);
        Assert.Equal(first.ConversationId, next.ConversationId);

        meter.Label = "writing-feedback";
        var feedback = await services.GetRequiredService<IWritingAiEvaluationService>()
            .TryEvaluateSentenceAsync(new(1, "Tôi học tiếng Anh mỗi ngày.", "I study English every day.", "I study English daily."));
        Assert.NotNull(feedback);
        Assert.False(string.IsNullOrWhiteSpace(feedback.OverallFeedback));

        meter.Label = "vocabulary";
        var vocab = await services.GetRequiredService<IVocabSuggestService>().SuggestAsync("morning");
        Assert.True(vocab.Success, vocab.ErrorMessage);
        Assert.NotEmpty(vocab.Definitions);

        meter.Label = "reading-review";
        var passage = new ReadingPassage { Title = "Daily learning", Content = "I study English every morning. Practice helps me learn new words.", Level = "A1", CreatedAt = DateTime.UtcNow };
        db.ReadingPassages.Add(passage); await db.SaveChangesAsync();
        var reading = await services.GetRequiredService<IReadingTranslationService>().SubmitTranslationAsync(TestDataIds.UserId, passage.Id,
            "Học mỗi ngày", """[{"original":"I study English every morning. Practice helps me learn new words.","translated":"Tôi học tiếng Anh mỗi sáng. Luyện tập giúp tôi học từ mới."}]""");
        Assert.True(reading.Success);
        Assert.NotNull(reading.AiScore);
        Assert.InRange(reading.AiScore.Value, 0, 100);
        Assert.NotNull(reading.IsAiApproved);

        meter.Label = "writing-generation";
        var writing = services.GetRequiredService<IWritingService>();
        var generationRequest = new WritingCreateFromAiRequestViewModel
        {
            SourceText = "Tôi học tiếng Anh mỗi sáng. Luyện tập giúp tôi ghi nhớ các từ mới.",
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };
        var generated = await writing.CreateFromAiAsync(generationRequest, TestDataIds.AdminUserId);
        Assert.True(generated.Outcome == WritingCreateFromAiOutcome.Success, generated.ErrorCode + ": " + generated.ErrorMessage);
        var callsBeforeReplay = meter.Count;
        var replay = await writing.CreateFromAiAsync(generationRequest, TestDataIds.AdminUserId);
        Assert.True(replay.IsReplay);
        Assert.Equal(generated.ExerciseId, replay.ExerciseId);
        Assert.Equal(callsBeforeReplay, meter.Count);

        var lesson = new ListeningLesson
        {
            Title = "A learning routine", Level = "A1", Topic = "Daily life",
            TranscriptLines =
            [
                new() { OrderIndex = 0, Speaker = "Anna", Text = "I study English every morning before work." },
                new() { OrderIndex = 1, Speaker = "Ben", Text = "I read a short story and write down three new words every evening." }
            ]
        };
        db.ListeningLessons.Add(lesson); await db.SaveChangesAsync();
        var listening = services.GetRequiredService<TCTEnglish.Services.IListeningService>();
        meter.Label = "listening-quiz";
        var quiz = await listening.GenerateQuizFromTranscriptAsync(lesson.Id, TestDataIds.AdminUserId);
        Assert.True(quiz.Status == OperationStatus.Success, quiz.ErrorMessage);
        Assert.NotEmpty(await db.ListeningQuizQuestions.Where(q => q.LessonId == lesson.Id).ToListAsync());
        meter.Label = "listening-translation";
        var translation = await listening.TranslateTranscriptAsync(lesson.Id, TestDataIds.AdminUserId);
        Assert.True(translation.Status == OperationStatus.Success, translation.ErrorMessage);
        Assert.All(lesson.TranscriptLines, line => Assert.False(string.IsNullOrWhiteSpace(line.VietnameseMeaning)));

        // Optional live call may time out: local pass/attempt/progress must still be saved.
        meter.Label = "writing-optional-3s";
        var writingOptions = services.GetRequiredService<IOptions<WritingEvaluationOptions>>().Value;
        writingOptions.EvaluationMode = "Local"; writingOptions.CloudEnhancementEnabled = true; writingOptions.CloudEnhancementTimeoutSeconds = 3;
        var sentence = await db.WritingExerciseSentences.FirstAsync();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var local = await writing.EvaluateWritingSentenceAsync(sentence.WritingExerciseId, sentence.Id, sentence.EnglishMeaning, TestDataIds.UserId);
            Assert.NotNull(local); Assert.True(local.Passed);
            Assert.True(clock.Elapsed.TotalSeconds < 5, "Optional cloud feedback exceeded its bounded save path.");
            Assert.True(await db.UserWritingAttempts.AnyAsync(a => a.UserId == TestDataIds.UserId && a.WritingExerciseSentenceId == sentence.Id && a.Passed));
            Assert.True(await db.UserWritingSentenceProgresses.AnyAsync(p => p.UserId == TestDataIds.UserId && p.SentenceId == sentence.Id && p.IsPassed));
            output.WriteLine("Local writing: passed={0}, cloudFeedback={1}, saveLatencyMs={2}", local.Passed, local.UsedAi, clock.ElapsedMilliseconds);
        }
        finally { writingOptions.CloudEnhancementEnabled = false; }
        Assert.Equal(9, meter.Count);
    }

    private sealed class Meter(ITestOutputHelper output)
    {
        public int Count;
        public string Label = "unknown";
        public void Report(AiProviderReply reply, long milliseconds) => output.WriteLine(
            "LIVE {0}: model={1}, effort=low, input={2}, output={3}, reasoning(included)={4}, total={5}, latencyMs={6}",
            Label, reply.Model, reply.PromptTokens, reply.CompletionTokens, reply.ReasoningTokens, reply.TotalTokens, milliseconds);
        public void Cancelled(long milliseconds) => output.WriteLine("LIVE {0}: cancelled at {1}ms; provider usage unavailable for this cancelled request.", Label, milliseconds);
    }

    private sealed class RecordingProvider(OpenAiProviderClient provider, Meter meter) : IAiProviderClient
    {
        public async Task<AiProviderReply> GenerateReplyAsync(int userId, IReadOnlyList<AiContextMessage> messages, CancellationToken ct, AiProviderRequestOptions? requestOptions = null)
        {
            Assert.True(++meter.Count <= 9, "Synthetic live request cap exceeded.");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var reply = await provider.GenerateReplyAsync(userId, messages, ct, requestOptions);
                Assert.StartsWith("gpt-6-luna", reply.Model);
                meter.Report(reply, clock.ElapsedMilliseconds);
                return reply;
            }
            catch (OperationCanceledException) { meter.Cancelled(clock.ElapsedMilliseconds); throw; }
        }
    }

    private sealed class FeatureLiveFactAttribute : FactAttribute
    {
        public FeatureLiveFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("TCT_OPENAI_FEATURE_LIVE") != "1")
                Skip = "Paid per-consumer acceptance requires explicit TCT_OPENAI_FEATURE_LIVE=1 and a local key; never enabled by ordinary discovery.";
        }
    }
}
