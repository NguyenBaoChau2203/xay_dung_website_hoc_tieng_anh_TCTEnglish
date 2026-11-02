using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace TCTEnglish.Tests;

/// <summary>Two small, synthetic, paid requests only after explicit owner opt-in.</summary>
public sealed class OpenAiLiveAcceptanceTests(ITestOutputHelper output)
{
    [CloudLiveFact]
    public async Task SyntheticTextAndJson_UseLunaLowAndReportActualUsage()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient, OpenAiProviderClient>();
        });
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAiProviderClient>();
        foreach (var prompt in new[] { "Reply with exactly OK.", "Return only this JSON object: {\"word\":\"hello\",\"meaning\":\"xin chào\"}." })
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var reply = await provider.GenerateReplyAsync(0, [new("user", prompt)], default,
                new() { MaxOutputTokens = 2048, RequestTimeoutSeconds = 30 });
            Assert.StartsWith("gpt-6-luna", reply.Model);
            Assert.True(reply.PromptTokens > 0 && reply.CompletionTokens > 0);
            Assert.InRange(reply.ReasoningTokens, 0, reply.CompletionTokens);
            if (prompt.Contains("JSON"))
            {
                using var json = JsonDocument.Parse(reply.Text);
                Assert.Equal("hello", json.RootElement.GetProperty("word").GetString());
            }
            output.WriteLine("Actual model={0}, input={1}, output={2}, reasoning(included)={3}, total={4}, latencyMs={5}",
                reply.Model, reply.PromptTokens, reply.CompletionTokens, reply.ReasoningTokens, reply.TotalTokens, clock.ElapsedMilliseconds);
        }
    }

    private sealed class CloudLiveFactAttribute : FactAttribute
    {
        public CloudLiveFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("TCT_OPENAI_LIVE") != "1")
                Skip = "Paid live OpenAI acceptance requires explicit TCT_OPENAI_LIVE=1 after setting the key locally.";
        }
    }
}
