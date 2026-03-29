using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.AI;

public sealed class OpenAiProviderClient : IAiProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiProviderClient> _logger;

    public OpenAiProviderClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options,
        ILogger<OpenAiProviderClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiProviderReply> GenerateReplyAsync(IReadOnlyList<AiContextMessage> messages, CancellationToken ct)
    {
        if (messages == null || messages.Count == 0)
        {
            throw new ArgumentException("Context messages are required.", nameof(messages));
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new AiProviderException("AI provider is not configured.", "missing_api_key", false);
        }

        var maxRetries = Math.Max(0, _options.RetryMaxAttempts);
        var maxAttempts = maxRetries + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = BuildRequest(messages);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)));

                var httpClient = _httpClientFactory.CreateClient();
                using var response = await httpClient.SendAsync(request, timeoutCts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return await ParseSuccessResponseAsync(response, ct);
                }

                var errorCode = $"http_{(int)response.StatusCode}";
                var isTransient = IsTransient(response.StatusCode);

                _logger.LogWarning(
                    "OpenAI request failed with status code {statusCode} on attempt {attempt}/{maxAttempts}",
                    (int)response.StatusCode,
                    attempt,
                    maxAttempts);

                if (!isTransient || attempt == maxAttempts)
                {
                    throw new AiProviderException("AI provider request failed.", errorCode, isTransient);
                }
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                if (attempt == maxAttempts)
                {
                    throw new AiProviderException("AI provider timeout.", "timeout", true, ex);
                }
            }
            catch (HttpRequestException ex)
            {
                if (attempt == maxAttempts)
                {
                    throw new AiProviderException("Unable to reach AI provider.", "network", true, ex);
                }
            }

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
            await Task.Delay(delay, ct);
        }

        throw new AiProviderException("AI provider request failed.", "unknown", false);
    }

    private HttpRequestMessage BuildRequest(IReadOnlyList<AiContextMessage> messages)
    {
        var endpoint = BuildEndpointUri();
        var payload = new OpenAiChatCompletionRequest(
            _options.Model,
            _options.Temperature,
            Math.Max(1, _options.MaxOutputTokens),
            messages.Select(x => new OpenAiChatMessage(x.Role, x.Content ?? string.Empty)).ToList());

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return request;
    }

    private async Task<AiProviderReply> ParseSuccessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var payload = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(body, JsonOptions);

        var assistantText = payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? string.Empty;
        var promptTokens = payload?.Usage?.PromptTokens ?? 0;
        var completionTokens = payload?.Usage?.CompletionTokens ?? 0;
        var totalTokens = payload?.Usage?.TotalTokens ?? (promptTokens + completionTokens);
        var model = string.IsNullOrWhiteSpace(payload?.Model) ? _options.Model : payload.Model;

        if (string.IsNullOrWhiteSpace(assistantText))
        {
            throw new AiProviderException("AI provider returned empty content.", "empty_response", false);
        }

        var requestId = response.Headers.TryGetValues("x-request-id", out var values)
            ? values.FirstOrDefault()
            : payload?.Id;

        return new AiProviderReply(
            assistantText,
            promptTokens,
            completionTokens,
            totalTokens,
            model!,
            requestId);
    }

    private Uri BuildEndpointUri()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://api.openai.com/v1"
            : _options.BaseUrl.TrimEnd('/');

        return new Uri($"{baseUrl}/chat/completions", UriKind.Absolute);
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
    }

    private sealed record OpenAiChatCompletionRequest(
        string Model,
        double Temperature,
        int MaxTokens,
        IReadOnlyList<OpenAiChatMessage> Messages)
    {
        public string model { get; init; } = Model;
        public double temperature { get; init; } = Temperature;
        public int max_tokens { get; init; } = MaxTokens;
        public IReadOnlyList<OpenAiChatMessage> messages { get; init; } = Messages;
    }

    private sealed record OpenAiChatMessage(string Role, string Content)
    {
        public string role { get; init; } = Role;
        public string content { get; init; } = Content;
    }

    private sealed class OpenAiChatCompletionResponse
    {
        public string? Id { get; set; }
        public string? Model { get; set; }
        public List<OpenAiChoice>? Choices { get; set; }
        public OpenAiUsage? Usage { get; set; }

        public string? id { set => Id = value; }
        public string? model { set => Model = value; }
        public List<OpenAiChoice>? choices { set => Choices = value; }
        public OpenAiUsage? usage { set => Usage = value; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiChoiceMessage? Message { get; set; }

        public OpenAiChoiceMessage? message { set => Message = value; }
    }

    private sealed class OpenAiChoiceMessage
    {
        public string? Content { get; set; }

        public string? content { set => Content = value; }
    }

    private sealed class OpenAiUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }

        public int prompt_tokens { set => PromptTokens = value; }
        public int completion_tokens { set => CompletionTokens = value; }
        public int total_tokens { set => TotalTokens = value; }
    }
}
