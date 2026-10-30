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
        var text = root.TryGetProperty("text", out var textProp)
            ? textProp.GetString() ?? string.Empty
            : string.Empty;

        var segments = new List<SpeechSegment>();
        if (root.TryGetProperty("segments", out var segmentsProp) && segmentsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in segmentsProp.EnumerateArray())
            {
                var segText = item.TryGetProperty("text", out var st) ? st.GetString() ?? string.Empty : string.Empty;
                var start = item.TryGetProperty("start", out var ss) && ss.TryGetDouble(out var startVal) ? startVal : 0d;
                var end = item.TryGetProperty("end", out var es) && es.TryGetDouble(out var endVal) ? endVal : start;
                segments.Add(new SpeechSegment
                {
                    StartSeconds = start,
                    EndSeconds = end,
                    Text = segText.Trim()
                });
            }
        }

        var collapsed = Collapse(text);
        if (string.IsNullOrWhiteSpace(collapsed) && segments.Count == 0)
        {
            return new SpeechTranscriptionResult
            {
                Status = SpeechTranscriptionStatus.NoSpeech,
                Transcript = string.Empty,
                Segments = segments,
                EngineName = "whisper.cpp",
                ModelIdentity = _options.ModelIdentity,
                ErrorCode = "no_speech",
                Message = "Chưa nhận được lời nói."
            };
        }

        if (string.IsNullOrWhiteSpace(collapsed))
        {
            collapsed = Collapse(string.Join(' ', segments.Select(s => s.Text)));
        }

        return new SpeechTranscriptionResult
        {
            Status = string.IsNullOrWhiteSpace(collapsed)
                ? SpeechTranscriptionStatus.NoSpeech
                : SpeechTranscriptionStatus.Ok,
            Transcript = collapsed,
            Segments = segments,
            EngineName = "whisper.cpp",
            ModelIdentity = _options.ModelIdentity
        };
    }

    private static string Collapse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
