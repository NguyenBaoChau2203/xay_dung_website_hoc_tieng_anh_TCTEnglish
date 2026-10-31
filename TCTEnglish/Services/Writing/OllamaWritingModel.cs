using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Writing;

/// <summary>Ollama /api/chat adapter for Writing feedback only. Default off. Does not grade pass/fail.</summary>
public sealed class OllamaWritingModel : ILocalWritingModel
{
    private readonly HttpClient _httpClient;
    private readonly LocalWritingEngineOptions _options;
    private readonly ILogger<OllamaWritingModel> _logger;

    public OllamaWritingModel(
        HttpClient httpClient,
        IOptions<LocalWritingEngineOptions> options,
        ILogger<OllamaWritingModel> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LocalWritingModelResult> GetFeedbackAsync(string promptPayload, CancellationToken cancellationToken)
    {
        if (!_options.LocalLlmEnabled
            || string.IsNullOrWhiteSpace(_options.OllamaBaseUrl)
            || string.IsNullOrWhiteSpace(_options.OllamaModel))
        {
            return new LocalWritingModelResult
            {
                Available = false,
                UnavailableReason = "Local LLM disabled or unconfigured."
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.LlmFeedbackTimeoutSeconds, 1, 10)));

            var body = new
            {
                model = _options.OllamaModel,
                stream = false,
                messages = new[]
                {
                    new { role = "system", content = "Return compact JSON feedback only. Never invent pass/fail." },
                    new { role = "user", content = promptPayload }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            using var response = await _httpClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new LocalWritingModelResult
                {
                    Available = false,
                    UnavailableReason = "Local LLM HTTP error."
                };
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("message", out var message)
                || !message.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.String)
            {
                return new LocalWritingModelResult
                {
                    Available = false,
                    UnavailableReason = "invalid_schema"
                };
            }

            var contentText = content.GetString() ?? string.Empty;
            if (!TryParseAllowlistedFeedback(contentText, out var summary, out var suggestions))
            {
                return new LocalWritingModelResult
                {
                    Available = false,
                    UnavailableReason = "invalid_schema"
                };
            }

            return new LocalWritingModelResult
            {
                Available = true,
                Summary = summary,
                Suggestions = suggestions
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LocalWritingModelResult
            {
                Available = false,
                UnavailableReason = "timeout"
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama writing feedback failed.");
            return new LocalWritingModelResult
            {
                Available = false,
                UnavailableReason = "Local LLM unavailable."
            };
        }
    }

    public static bool TryParseAllowlistedFeedback(string content, out string summary, out IReadOnlyList<string> suggestions)
    {
        summary = string.Empty;
        suggestions = Array.Empty<string>();
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (doc.RootElement.TryGetProperty("summary", out var summaryProp))
            {
                if (summaryProp.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                summary = summaryProp.GetString() ?? string.Empty;
                if (summary.Length > 500)
                {
                    summary = summary[..500];
                }
            }

            if (doc.RootElement.TryGetProperty("suggestions", out var suggestionsProp))
            {
                if (suggestionsProp.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var items = new List<string>();
                foreach (var item in suggestionsProp.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String || items.Count >= 5)
                    {
                        return false;
                    }

                    var text = item.GetString() ?? string.Empty;
                    items.Add(text.Length > 200 ? text[..200] : text);
                }

                suggestions = items;
            }

            return summary.Length > 0 || suggestions.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
