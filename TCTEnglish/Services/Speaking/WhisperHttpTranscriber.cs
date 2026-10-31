using System.Net;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Speaking;

/// <summary>
/// HTTP adapter for whisper.cpp server /inference only. Never accepts client model URLs.
/// </summary>
public sealed class WhisperHttpTranscriber : ISpeechTranscriber
{
    private readonly HttpClient _httpClient;
    private readonly SpeechProcessingOptions _options;
    private readonly ISpeechInferenceLimiter _limiter;
    private readonly ILogger<WhisperHttpTranscriber> _logger;

    public WhisperHttpTranscriber(
        HttpClient httpClient,
        IOptions<SpeechProcessingOptions> options,
        ISpeechInferenceLimiter limiter,
        ILogger<WhisperHttpTranscriber> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<SpeechTranscriptionResult> TranscribeAsync(Stream wavPcm16Mono16kHz, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return NotReady("engine_not_ready", "ASR nội bộ chưa được cấu hình trên máy chủ.");
        }

        var budgetSeconds = Math.Clamp(_options.InferenceTimeoutSeconds, 1, 60);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));

        IAsyncDisposable? lease = null;
        try
        {
            try
            {
                lease = await _limiter.AcquireAsync(budget.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return TimeoutResult();
            }

            if (lease == null)
            {
                return Unavailable("engine_busy", "Hàng đợi nhận dạng đang đầy. Thử lại sau.");
            }

            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(wavPcm16Mono16kHz);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            content.Add(streamContent, "file", "speech.wav");
            content.Add(new StringContent("verbose_json"), "response_format");
            content.Add(new StringContent("en"), "language");

            var path = string.IsNullOrWhiteSpace(_options.WhisperInferencePath)
                ? "/inference"
                : _options.WhisperInferencePath;
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
            using var response = await _httpClient.SendAsync(request, budget.Token);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return Unavailable("engine_busy", "ASR đang quá tải.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Whisper inference failed with status {StatusCode}.", (int)response.StatusCode);
                return Unavailable("engine_http_error", "Không thể nhận dạng giọng nói lúc này.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(budget.Token);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: budget.Token);
            return MapVerboseJson(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return TimeoutResult();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Whisper inference returned invalid JSON.");
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Whisper HTTP request failed.");
            return Unavailable("engine_unavailable", "Không kết nối được ASR nội bộ.");
        }
        finally
        {
            if (lease != null)
            {
                await lease.DisposeAsync();
            }
        }
    }

    private SpeechTranscriptionResult TimeoutResult() => new()
    {
        Status = SpeechTranscriptionStatus.Timeout,
        EngineName = "whisper.cpp",
        ModelIdentity = _options.ModelIdentity,
        ErrorCode = "engine_timeout",
        Message = "Nhận dạng vượt quá thời gian cho phép."
    };

    private SpeechTranscriptionResult NotReady(string code, string message) => new()
    {
        Status = SpeechTranscriptionStatus.NotReady,
        EngineName = "whisper.cpp",
        ModelIdentity = _options.ModelIdentity,
        ErrorCode = code,
        Message = message
    };

    private SpeechTranscriptionResult Unavailable(string code, string message) => new()
    {
        Status = SpeechTranscriptionStatus.Unavailable,
        EngineName = "whisper.cpp",
        ModelIdentity = _options.ModelIdentity,
        ErrorCode = code,
        Message = message
    };

    private SpeechTranscriptionResult MapVerboseJson(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.");
        }

        // Error payloads and non-transcript objects must not look like silence.
        if (root.TryGetProperty("error", out _))
        {
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.");
        }

        var hasText = root.TryGetProperty("text", out var textProp);
        var hasSegments = root.TryGetProperty("segments", out var segmentsProp);
        if (!hasText && !hasSegments)
        {
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.");
        }

        string? text = null;
        if (hasText)
        {
            if (textProp.ValueKind == JsonValueKind.String)
            {
                text = textProp.GetString() ?? string.Empty;
            }
            else
            {
                return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.");
            }
        }

        var segments = new List<SpeechSegment>();
        if (hasSegments)
        {
            if (segmentsProp.ValueKind != JsonValueKind.Array)
            {
                return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.");
            }

            foreach (var item in segmentsProp.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.");
                }

                if (!TryReadSegment(item, out var segment))
                {
                    return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.");
                }

                segments.Add(segment);
            }
        }

        // Silence requires an explicit string text field. Segment-only empty payloads are invalid.
        if (text is null && segments.Count == 0)
        {
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.");
        }

        var collapsed = CollapseSpeech(text);
        if (string.IsNullOrWhiteSpace(collapsed) && segments.Count == 0)
        {
            return NoSpeechResult(segments);
        }

        if (string.IsNullOrWhiteSpace(collapsed))
        {
            collapsed = CollapseSpeech(string.Join(' ', segments.Select(s => s.Text)));
        }

        if (string.IsNullOrWhiteSpace(collapsed))
        {
            return NoSpeechResult(segments);
        }

        return new SpeechTranscriptionResult
        {
            Status = SpeechTranscriptionStatus.Ok,
            Transcript = collapsed,
            Segments = segments,
            EngineName = "whisper.cpp",
            ModelIdentity = _options.ModelIdentity
        };
    }

    private SpeechTranscriptionResult NoSpeechResult(IReadOnlyList<SpeechSegment> segments)
        => new()
        {
            Status = SpeechTranscriptionStatus.NoSpeech,
            Transcript = string.Empty,
            Segments = segments,
            EngineName = "whisper.cpp",
            ModelIdentity = _options.ModelIdentity,
            ErrorCode = "no_speech",
            Message = "Chưa nhận được lời nói."
        };

    private static bool TryReadSegment(JsonElement item, out SpeechSegment segment)
    {
        segment = default!;

        string segText;
        if (!item.TryGetProperty("text", out var textNode))
        {
            segText = string.Empty;
        }
        else if (textNode.ValueKind == JsonValueKind.String)
        {
            segText = textNode.GetString() ?? string.Empty;
        }
        else if (textNode.ValueKind == JsonValueKind.Null)
        {
            segText = string.Empty;
        }
        else
        {
            return false;
        }

        var start = 0d;
        if (item.TryGetProperty("start", out var startNode)
            && !TryReadFiniteNumber(startNode, out start))
        {
            return false;
        }

        var end = start;
        if (item.TryGetProperty("end", out var endNode)
            && !TryReadFiniteNumber(endNode, out end))
        {
            return false;
        }

        if (end < start)
        {
            return false;
        }

        segment = new SpeechSegment
        {
            StartSeconds = start,
            EndSeconds = end,
            Text = segText.Trim()
        };
        return true;
    }

    private static bool TryReadFiniteNumber(JsonElement node, out double value)
    {
        value = 0d;
        if (node.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        return node.TryGetDouble(out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }

    /// <summary>
    /// Collapse whitespace and strip engine-only silence markers (e.g. whisper.cpp [BLANK_AUDIO]).
    /// Marker-only payloads become empty → NoSpeech; real speech tokens are preserved.
    /// </summary>
    private static string CollapseSpeech(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var tokens = value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !IsEngineNonSpeechMarker(token));

        return string.Join(' ', tokens);
    }

    private static bool IsEngineNonSpeechMarker(string token)
    {
        var trimmed = token.Trim().Trim(',', '.', '!', '?', ';', ':');
        return trimmed.Equals("[BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("BLANK_AUDIO", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("[NO_SPEECH]", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("[SILENCE]", StringComparison.OrdinalIgnoreCase);
    }
}
