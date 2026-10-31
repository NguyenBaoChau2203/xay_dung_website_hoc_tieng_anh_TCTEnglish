using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Writing;

public sealed class LocalWritingEngineOptions
{
    public const string SectionName = "Learning:WritingEngines";

    public bool GrammarEnabled { get; set; }
    public string LanguageToolBaseUrl { get; set; } = string.Empty;
    public int GrammarTimeoutSeconds { get; set; } = 2;
    public int MaxTextCharacters { get; set; } = 4000;

    public bool LocalLlmEnabled { get; set; }
    public string OllamaBaseUrl { get; set; } = string.Empty;
    public string OllamaModel { get; set; } = string.Empty;
    public int LlmFeedbackTimeoutSeconds { get; set; } = 3;
}

/// <summary>LanguageTool HTTP adapter. Default off. Never sets writing passed.</summary>
public sealed class LanguageToolGrammarChecker : ILocalGrammarChecker
{
    private readonly HttpClient _httpClient;
    private readonly LocalWritingEngineOptions _options;
    private readonly ILogger<LanguageToolGrammarChecker> _logger;

    public LanguageToolGrammarChecker(
        HttpClient httpClient,
        IOptions<LocalWritingEngineOptions> options,
        ILogger<LanguageToolGrammarChecker> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LocalGrammarCheckResult> CheckAsync(string text, CancellationToken cancellationToken)
    {
        if (!_options.GrammarEnabled || string.IsNullOrWhiteSpace(_options.LanguageToolBaseUrl))
        {
            return new LocalGrammarCheckResult
            {
                Available = false,
                UnavailableReason = "Grammar engine disabled or unconfigured."
            };
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new LocalGrammarCheckResult { Available = true };
        }

        if (text.Length > _options.MaxTextCharacters)
        {
            return new LocalGrammarCheckResult
            {
                Available = false,
                UnavailableReason = "Text exceeds grammar checker limit."
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.GrammarTimeoutSeconds, 1, 5)));

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["text"] = text,
                ["language"] = "en-US"
            });
            using var response = await _httpClient.PostAsync("/v2/check", content, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new LocalGrammarCheckResult
                {
                    Available = false,
                    UnavailableReason = "Grammar engine HTTP error."
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
            if (!doc.RootElement.TryGetProperty("matches", out var matches) || matches.ValueKind != JsonValueKind.Array)
            {
                return new LocalGrammarCheckResult
                {
                    Available = false,
                    UnavailableReason = "invalid_schema"
                };
            }

            var findings = new List<LocalGrammarFinding>();
            foreach (var match in matches.EnumerateArray())
            {
                if (match.ValueKind != JsonValueKind.Object)
                {
                    return new LocalGrammarCheckResult
                    {
                        Available = false,
                        UnavailableReason = "invalid_schema"
                    };
                }

                var message = match.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString() ?? string.Empty
                    : string.Empty;
                findings.Add(new LocalGrammarFinding
                {
                    Offset = match.TryGetProperty("offset", out var o) && o.TryGetInt32(out var ov) ? ov : 0,
                    Length = match.TryGetProperty("length", out var l) && l.TryGetInt32(out var lv) ? lv : 0,
                    Message = message,
                    RuleId = match.TryGetProperty("rule", out var rule)
                        && rule.ValueKind == JsonValueKind.Object
                        && rule.TryGetProperty("id", out var id)
                        && id.ValueKind == JsonValueKind.String
                        ? id.GetString() ?? string.Empty
                        : string.Empty
                });
            }

            return new LocalGrammarCheckResult { Available = true, Findings = findings };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LocalGrammarCheckResult
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
            _logger.LogWarning(ex, "LanguageTool check failed.");
            return new LocalGrammarCheckResult
            {
                Available = false,
                UnavailableReason = "Grammar engine unavailable."
            };
        }
    }
}
