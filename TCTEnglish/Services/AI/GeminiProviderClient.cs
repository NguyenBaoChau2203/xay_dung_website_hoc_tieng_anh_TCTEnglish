using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.AI;

public sealed class GeminiProviderClient : IAiProviderClient
{
    private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private const string DefaultModel = "gemini-2.5-flash-lite";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiOptions _options;
    private readonly ILogger<GeminiProviderClient> _logger;

    public GeminiProviderClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options,
        ILogger<GeminiProviderClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiProviderReply> GenerateReplyAsync(int userId, IReadOnlyList<AiContextMessage> messages, CancellationToken ct)
    {
        if (messages == null || messages.Count == 0)
        {
            throw new ArgumentException("Context messages are required.", nameof(messages));
        }

        var maxRetries = _options.EffectiveRetryMaxAttempts;
        var maxAttempts = maxRetries + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = BuildRequest(messages);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.EffectiveRequestTimeoutSeconds));

                var httpClient = _httpClientFactory.CreateClient();
                using var response = await httpClient.SendAsync(request, timeoutCts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return await ParseSuccessResponseAsync(response, ct);
                }

                var exception = CreateHttpException(response.StatusCode);

                _logger.LogWarning(
                    "Gemini request failed with status code {statusCode} on attempt {attempt}/{maxAttempts}",
                    (int)response.StatusCode,
                    attempt,
                    maxAttempts);

                if (!exception.IsTransient || attempt == maxAttempts)
                {
                    throw exception;
                }
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                if (attempt == maxAttempts)
                {
                    throw new AiProviderException("AI provider timeout.", AiProviderException.ErrorCodeTimeout, true, ex);
                }
            }
            catch (HttpRequestException ex)
            {
                if (attempt == maxAttempts)
                {
                    throw new AiProviderException("Unable to reach AI provider.", AiProviderException.ErrorCodeNetwork, true, ex);
                }
            }

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
            await Task.Delay(delay, ct);
        }

        throw new AiProviderException("AI provider request failed.", AiProviderException.ErrorCodeUnknown, false);
    }

    private HttpRequestMessage BuildRequest(IReadOnlyList<AiContextMessage> messages)
    {
        var endpoint = BuildEndpointUri();
        var (systemInstruction, contents) = MapContents(messages);

        var payload = new GeminiGenerateContentRequest
        {
            SystemInstruction = systemInstruction,
            Contents = contents,
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = _options.Temperature,
                MaxOutputTokens = Math.Max(1, _options.MaxOutputTokens)
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        return request;
    }

    private async Task<AiProviderReply> ParseSuccessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var payload = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(body, JsonOptions);

        var assistantText = payload?.Candidates?
            .FirstOrDefault()?
            .Content?
            .Parts?
            .Select(x => x.Text?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Aggregate(string.Empty, (current, next) =>
                string.IsNullOrEmpty(current) ? next! : $"{current}\n{next}")
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(assistantText))
        {
            throw new AiProviderException("AI provider returned empty content.", AiProviderException.ErrorCodeEmptyResponse, false);
        }

        var promptTokens = payload?.UsageMetadata?.PromptTokenCount ?? 0;
        var completionTokens = payload?.UsageMetadata?.CandidatesTokenCount ?? 0;
        var totalTokens = payload?.UsageMetadata?.TotalTokenCount ?? (promptTokens + completionTokens);
        var model = string.IsNullOrWhiteSpace(payload?.ModelVersion) ? _options.Model : payload!.ModelVersion!;
        var requestId = response.Headers.TryGetValues("x-request-id", out var values)
            ? values.FirstOrDefault()
            : null;

        return new AiProviderReply(
            assistantText,
            promptTokens,
            completionTokens,
            totalTokens,
            model,
            requestId);
    }

    private Uri BuildEndpointUri()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new AiProviderException(
                "AI provider API key is missing.",
                AiProviderException.ErrorCodeInvalidConfiguration,
                false);
        }

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? DefaultBaseUrl
            : _options.BaseUrl.TrimEnd('/');

        var model = string.IsNullOrWhiteSpace(_options.Model)
            ? DefaultModel
            : _options.Model;

        var apiKeyQuery = $"?key={Uri.EscapeDataString(_options.ApiKey)}";

        var endpoint = $"{baseUrl}/models/{model}:generateContent{apiKeyQuery}";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            throw new AiProviderException("AI provider base URL is invalid.", AiProviderException.ErrorCodeInvalidConfiguration, false);
        }

        return endpointUri;
    }

    private static AiProviderException CreateHttpException(HttpStatusCode statusCode)
    {
        if (statusCode == HttpStatusCode.BadRequest)
        {
            return new AiProviderException(
                "AI provider request is invalid. Check model and request configuration.",
                AiProviderException.ErrorCodeInvalidConfiguration,
                false);
        }

        if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
        {
            return new AiProviderException(
                "AI provider authentication failed.",
                AiProviderException.ErrorCodeAuthentication,
                false);
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return new AiProviderException(
                "AI provider rate limit exceeded.",
                AiProviderException.ErrorCodeRateLimited,
                true);
        }

        if ((int)statusCode >= 500)
        {
            return new AiProviderException(
                "AI provider is temporarily unavailable.",
                AiProviderException.ErrorCodeProviderUnavailable,
                true);
        }

        return new AiProviderException(
            "AI provider request failed.",
            $"http_{(int)statusCode}",
            false);
    }

    private static GeminiContent MapToGeminiContent(AiContextMessage message)
    {
        var role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
            ? "model"
            : "user";

        var text = message.Content?.Trim() ?? string.Empty;
        IReadOnlyList<GeminiTextPart> parts = string.IsNullOrWhiteSpace(text)
            ? Array.Empty<GeminiTextPart>()
            : new[] { new GeminiTextPart { Text = text } };

        return new GeminiContent
        {
            Role = role,
            Parts = parts
        };
    }

    private static (GeminiSystemInstruction? systemInstruction, IReadOnlyList<GeminiContent> contents) MapContents(
        IReadOnlyList<AiContextMessage> messages)
    {
        GeminiSystemInstruction? systemInstruction = null;
        var contents = new List<GeminiContent>();

        foreach (var message in messages)
        {
            if (string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
            {
                if (systemInstruction == null)
                {
                    var systemText = message.Content?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(systemText))
                    {
                        systemInstruction = new GeminiSystemInstruction
                        {
                            Parts = [new GeminiTextPart { Text = systemText }]
                        };
                    }
                }

                continue;
            }

            var content = MapToGeminiContent(message);
            if (content.Parts.Count > 0)
            {
                contents.Add(content);
            }
        }

        return (systemInstruction, contents);
    }

    private sealed class GeminiGenerateContentRequest
    {
        [JsonPropertyName("system_instruction")]
        public GeminiSystemInstruction? SystemInstruction { get; init; }

        [JsonPropertyName("contents")]
        public IReadOnlyList<GeminiContent> Contents { get; init; } = [];

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig GenerationConfig { get; init; } = new();
    }

    private sealed class GeminiSystemInstruction
    {
        [JsonPropertyName("parts")]
        public IReadOnlyList<GeminiTextPart> Parts { get; init; } = [];
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("parts")]
        public IReadOnlyList<GeminiTextPart> Parts { get; init; } = [];
    }

    private sealed class GeminiTextPart
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; init; }
    }

    private sealed class GeminiGenerateContentResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }

        [JsonPropertyName("usageMetadata")]
        public GeminiUsageMetadata? UsageMetadata { get; set; }

        [JsonPropertyName("modelVersion")]
        public string? ModelVersion { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiCandidateContent? Content { get; set; }
    }

    private sealed class GeminiCandidateContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiCandidatePart>? Parts { get; set; }
    }

    private sealed class GeminiCandidatePart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class GeminiUsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [JsonPropertyName("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }

        [JsonPropertyName("totalTokenCount")]
        public int TotalTokenCount { get; set; }
    }
}
