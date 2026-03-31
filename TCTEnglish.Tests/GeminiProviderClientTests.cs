using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.TestHelpers;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GeminiProviderClientTests
{
    [Fact]
    public async Task GenerateReplyAsync_Success_ReturnsParsedReply()
    {
        var capturedRequestUri = default(Uri);
        var capturedRequestBody = string.Empty;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            capturedRequestUri = request.RequestUri;
            capturedRequestBody = await request.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "modelVersion": "gemini-2.5-flash-lite",
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          { "text": "Xin chao" },
                          { "text": "Ban can gi?" }
                        ]
                      }
                    }
                  ],
                  "usageMetadata": {
                    "promptTokenCount": 11,
                    "candidatesTokenCount": 9,
                    "totalTokenCount": 20
                  }
                }
                """, Encoding.UTF8, "application/json")
            };
        });

        var provider = CreateClient(handler, new AiOptions
        {
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
            ApiKey = "test-key",
            Model = "gemini-2.5-flash-lite",
            RetryMaxAttempts = 0,
            RequestTimeoutSeconds = 5
        });

        var result = await provider.GenerateReplyAsync(
            [
                new AiContextMessage("system", "You are a tutor"),
                new AiContextMessage("user", "Hi"),
                new AiContextMessage("assistant", "Hello")
            ],
            CancellationToken.None);

        Assert.NotNull(capturedRequestUri);
        Assert.Contains("models/gemini-2.5-flash-lite:generateContent", capturedRequestUri!.ToString());
        Assert.Contains("key=test-key", capturedRequestUri.ToString());

        using var requestDocument = JsonDocument.Parse(capturedRequestBody);
        var root = requestDocument.RootElement;
        Assert.Equal("You are a tutor", root.GetProperty("system_instruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal("user", root.GetProperty("contents")[0].GetProperty("role").GetString());
        Assert.Equal("Hi", root.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal("model", root.GetProperty("contents")[1].GetProperty("role").GetString());
        Assert.Equal("Hello", root.GetProperty("contents")[1].GetProperty("parts")[0].GetProperty("text").GetString());

        Assert.Equal("Xin chao\nBan can gi?", result.Text);
        Assert.Equal(11, result.PromptTokens);
        Assert.Equal(9, result.CompletionTokens);
        Assert.Equal(20, result.TotalTokens);
        Assert.Equal("gemini-2.5-flash-lite", result.Model);
    }

    [Fact]
    public async Task GenerateReplyAsync_RequestBody_UsesSystemInstructionAndExcludesSystemFromContents()
    {
        var capturedRequestBody = string.Empty;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            capturedRequestBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "candidates": [
                    { "content": { "parts": [ { "text": "ok" } ] } }
                  ]
                }
                """, Encoding.UTF8, "application/json")
            };
        });

        var provider = CreateClient(handler);

        _ = await provider.GenerateReplyAsync(
            [
                new AiContextMessage("system", "System rule"),
                new AiContextMessage("user", "First"),
                new AiContextMessage("assistant", "Second")
            ],
            CancellationToken.None);

        using var requestDocument = JsonDocument.Parse(capturedRequestBody);
        var root = requestDocument.RootElement;

        Assert.Equal("System rule", root.GetProperty("system_instruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal(2, root.GetProperty("contents").GetArrayLength());
        Assert.DoesNotContain("\"role\":\"system\"", capturedRequestBody);
    }

    [Fact]
    public async Task GenerateReplyAsync_EmptyResponse_ThrowsAiProviderException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "candidates": [
                    { "content": { "parts": [ { "text": "   " } ] } }
                  ]
                }
                """, Encoding.UTF8, "application/json")
            }));

        var provider = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.GenerateReplyAsync([new AiContextMessage("user", "hello")], CancellationToken.None));

        Assert.Equal(AiProviderException.ErrorCodeEmptyResponse, ex.ErrorCode);
        Assert.False(ex.IsTransient);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GenerateReplyAsync_AuthFailures_ThrowAuthenticationException(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            }));

        var provider = CreateClient(handler, new AiOptions
        {
            RetryMaxAttempts = 0,
            RequestTimeoutSeconds = 5,
            ApiKey = "test-key"
        });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.GenerateReplyAsync([new AiContextMessage("user", "hello")], CancellationToken.None));

        Assert.Equal(AiProviderException.ErrorCodeAuthentication, ex.ErrorCode);
        Assert.False(ex.IsTransient);
    }

    [Fact]
    public async Task GenerateReplyAsync_Status429_ThrowsTransientRateLimitException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            }));

        var provider = CreateClient(handler, new AiOptions
        {
            RetryMaxAttempts = 0,
            RequestTimeoutSeconds = 5,
            ApiKey = "test-key"
        });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.GenerateReplyAsync([new AiContextMessage("user", "hello")], CancellationToken.None));

        Assert.Equal(AiProviderException.ErrorCodeRateLimited, ex.ErrorCode);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task GenerateReplyAsync_Status503_ThrowsTransientProviderUnavailableException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            }));

        var provider = CreateClient(handler, new AiOptions
        {
            RetryMaxAttempts = 0,
            RequestTimeoutSeconds = 5,
            ApiKey = "test-key"
        });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.GenerateReplyAsync([new AiContextMessage("user", "hello")], CancellationToken.None));

        Assert.Equal(AiProviderException.ErrorCodeProviderUnavailable, ex.ErrorCode);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task GenerateReplyAsync_Timeout_ThrowsTimeoutException()
    {
        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });

        var provider = CreateClient(handler, new AiOptions
        {
            RetryMaxAttempts = 0,
            RequestTimeoutSeconds = 1,
            ApiKey = "test-key"
        });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.GenerateReplyAsync([new AiContextMessage("user", "hello")], CancellationToken.None));

        Assert.Equal(AiProviderException.ErrorCodeTimeout, ex.ErrorCode);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task GenerateReplyAsync_NetworkFailure_ThrowsNetworkException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("network failed"));

        var provider = CreateClient(handler, new AiOptions
        {
            RetryMaxAttempts = 0,
            RequestTimeoutSeconds = 5,
            ApiKey = "test-key"
        });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.GenerateReplyAsync([new AiContextMessage("user", "hello")], CancellationToken.None));

        Assert.Equal(AiProviderException.ErrorCodeNetwork, ex.ErrorCode);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task GenerateReplyAsync_DefaultModel_UsesGeminiModelPath()
    {
        var capturedRequestUri = default(Uri);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "candidates": [
                    { "content": { "parts": [ { "text": "ok" } ] } }
                  ]
                }
                """, Encoding.UTF8, "application/json")
            });
        });

        var provider = CreateClient(handler, new AiOptions
        {
            ApiKey = "test-key",
            BaseUrl = string.Empty,
            Model = string.Empty,
            RetryMaxAttempts = 0,
            RequestTimeoutSeconds = 5
        });

        _ = await provider.GenerateReplyAsync([new AiContextMessage("user", "hello")], CancellationToken.None);

        Assert.NotNull(capturedRequestUri);
        Assert.Contains("models/gemini-2.5-flash-lite:generateContent", capturedRequestUri!.ToString());
        Assert.DoesNotContain("gpt-", capturedRequestUri.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static GeminiProviderClient CreateClient(HttpMessageHandler handler, AiOptions? options = null)
    {
        var httpClient = new HttpClient(handler);
        return new GeminiProviderClient(
            new StubHttpClientFactory(httpClient),
            Options.Create(options ?? new AiOptions
            {
                BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                ApiKey = "test-key",
                Model = "gemini-2.5-flash-lite",
                RetryMaxAttempts = 0,
                RequestTimeoutSeconds = 5
            }),
            NullLogger<GeminiProviderClient>.Instance);
    }
}
