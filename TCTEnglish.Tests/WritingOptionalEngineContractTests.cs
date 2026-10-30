using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Writing;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public class WritingOptionalEngineContractTests
{
    [Fact]
    public async Task GrammarAndLocalLlm_DefaultDisabled_DoNotBlock()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<LocalWritingEngineOptions>>().Value;
        Assert.False(options.GrammarEnabled);
        Assert.False(options.LocalLlmEnabled);

        var grammar = scope.ServiceProvider.GetRequiredService<ILocalGrammarChecker>();
        var llm = scope.ServiceProvider.GetRequiredService<ILocalWritingModel>();

        var grammarResult = await grammar.CheckAsync("This are wrong.", CancellationToken.None);
        var llmResult = await llm.GetFeedbackAsync("{}", CancellationToken.None);

        Assert.False(grammarResult.Available);
        Assert.False(llmResult.Available);
        Assert.False(string.IsNullOrWhiteSpace(grammarResult.UnavailableReason));
        Assert.False(string.IsNullOrWhiteSpace(llmResult.UnavailableReason));
    }

    [Fact]
    public async Task GrammarChecker_InternalTimeout_ReturnsUnavailable_WithoutThrowing()
    {
        var handler = new StallHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1") };
        var checker = new LanguageToolGrammarChecker(
            client,
            Options.Create(new LocalWritingEngineOptions
            {
                GrammarEnabled = true,
                LanguageToolBaseUrl = "http://127.0.0.1",
                GrammarTimeoutSeconds = 1
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguageToolGrammarChecker>.Instance);

        var result = await checker.CheckAsync("This are wrong.", CancellationToken.None);
        Assert.False(result.Available);
        Assert.Equal("timeout", result.UnavailableReason);
    }

    [Fact]
    public async Task GrammarChecker_CallerCancel_Propagates()
    {
        var handler = new StallHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1") };
        var checker = new LanguageToolGrammarChecker(
            client,
            Options.Create(new LocalWritingEngineOptions
            {
                GrammarEnabled = true,
                LanguageToolBaseUrl = "http://127.0.0.1",
                GrammarTimeoutSeconds = 5
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguageToolGrammarChecker>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checker.CheckAsync("Hello", cts.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task GrammarChecker_MissingMatches_IsInvalidSchema()
    {
        var handler = new JsonHandler("{\"language\":{\"name\":\"English\"}}");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1") };
        var checker = new LanguageToolGrammarChecker(
            client,
            Options.Create(new LocalWritingEngineOptions
            {
                GrammarEnabled = true,
                LanguageToolBaseUrl = "http://127.0.0.1"
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguageToolGrammarChecker>.Instance);

        var result = await checker.CheckAsync("This are wrong.", CancellationToken.None);
        Assert.False(result.Available);
        Assert.Equal("invalid_schema", result.UnavailableReason);
    }

    [Fact]
    public async Task Ollama_RawString_IsInvalid_AndAllowlistedJsonIsMapped()
    {
        Assert.False(OllamaWritingModel.TryParseAllowlistedFeedback("nice work", out _, out _));
        Assert.True(OllamaWritingModel.TryParseAllowlistedFeedback(
            "{\"summary\":\"Use a verb.\",\"suggestions\":[\"Try again.\"]}",
            out var summary,
            out var suggestions));
        Assert.Equal("Use a verb.", summary);
        Assert.Equal("Try again.", Assert.Single(suggestions));
    }

    [Fact]
    public async Task Evaluate_SharedBudget_StopsSlowCloudBeforeLaterEnginesFinish()
    {
        var cloud = new SlowCloud();
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.PostConfigure<WritingEvaluationOptions>(options =>
            {
                options.EvaluationMode = "Local";
                options.CloudEnhancementEnabled = true;
            });
            services.PostConfigure<LocalWritingEngineOptions>(options =>
            {
                options.GrammarEnabled = true;
                options.LanguageToolBaseUrl = "http://127.0.0.1";
                options.LocalLlmEnabled = true;
                options.OllamaBaseUrl = "http://127.0.0.1";
                options.OllamaModel = "test";
            });
            services.RemoveAll<IWritingAiEvaluationService>();
            services.AddSingleton<IWritingAiEvaluationService>(cloud);
            services.RemoveAll<ILocalGrammarChecker>();
            services.AddSingleton<ILocalGrammarChecker>(new SlowGrammar());
            services.RemoveAll<ILocalWritingModel>();
            services.AddSingleton<ILocalWritingModel>(new SlowModel());
        });
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TCTVocabulary.Models.DbflashcardContext>();
        var sentence = await context.WritingExerciseSentences.AsNoTracking().OrderBy(item => item.Id).FirstAsync();
        var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        var started = Stopwatch.StartNew();
        var evaluation = await service.EvaluateWritingSentenceAsync(
            sentence.WritingExerciseId,
            sentence.Id,
            sentence.EnglishMeaning,
            TestDataIds.UserId);
        started.Stop();

        Assert.NotNull(evaluation);
        Assert.True(evaluation!.Passed);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(5), $"Optional stages took {started.Elapsed}.");
        Assert.True(started.Elapsed > TimeSpan.FromSeconds(2), "The shared budget should include the slow cloud call.");
        Assert.Equal(1, cloud.Calls);
    }

    [Fact]
    public async Task EvaluateEndpoint_EnabledGrammar_ReturnsSourceText_AndDoesNotFlipLocalPass()
    {
        var spy = new SpyGrammar();
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<ILocalGrammarChecker>();
            services.AddSingleton<ILocalGrammarChecker>(spy);
        });
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = JsonContent.Create(new { exerciseId = 1, sentenceId = 1, userAnswer = "banana" })
        };
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, spy.Calls);
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("passed").GetBoolean());
        Assert.Contains("languagetool", data.GetProperty("grammarFeedback").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class JsonHandler : HttpMessageHandler
    {
        private readonly string _json;
        public JsonHandler(string json) => _json = json;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_json) });
    }

    private sealed class SlowCloud : IWritingAiEvaluationService
    {
        public int Calls;
        public async Task<WritingAiEvaluationResult?> TryEvaluateSentenceAsync(WritingAiEvaluationRequest request, CancellationToken ct = default)
        {
            Calls++;
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new WritingAiEvaluationResult(false, "slow", "slow", "slow", "slow", "slow", "slow");
        }
    }

    private sealed class SlowGrammar : ILocalGrammarChecker
    {
        public async Task<LocalGrammarCheckResult> CheckAsync(string text, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return new LocalGrammarCheckResult { Available = true };
        }
    }

    private sealed class SlowModel : ILocalWritingModel
    {
        public async Task<LocalWritingModelResult> GetFeedbackAsync(string promptPayload, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return new LocalWritingModelResult { Available = true, Summary = "slow" };
        }
    }

    private sealed class SpyGrammar : ILocalGrammarChecker
    {
        public int Calls;
        public Task<LocalGrammarCheckResult> CheckAsync(string text, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new LocalGrammarCheckResult
            {
                Available = true,
                Findings = new[] { new LocalGrammarFinding { Message = "Use a verb." } }
            });
        }
    }

    private sealed class StallHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
}
