using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Speaking;

/// <summary>
/// OpenAI file transcription adapter (gpt-transcribe). Server-side key only; no client model URLs.
/// </summary>
public sealed class OpenAiSpeechTranscriber : ISpeechTranscriber
{
    public const string EngineName = "openai-audio";
    private const string TranscriptionsRelativePath = "audio/transcriptions";

    private readonly HttpClient _httpClient;
    private readonly SpeechProcessingOptions _options;
    private readonly IOpenAiSpeechCredentialResolver _credentials;
    private readonly ISpeechInferenceLimiter _limiter;
    private readonly ILogger<OpenAiSpeechTranscriber> _logger;

    public OpenAiSpeechTranscriber(
        HttpClient httpClient,
        IOptions<SpeechProcessingOptions> options,
        IOpenAiSpeechCredentialResolver credentials,
        ISpeechInferenceLimiter limiter,
        ILogger<OpenAiSpeechTranscriber> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _credentials = credentials;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<SpeechTranscriptionResult> TranscribeAsync(Stream wavPcm16Mono16kHz, CancellationToken cancellationToken)
    {
        if (!_credentials.TryResolve(out var credentials))
        {
            return NotReady("engine_not_ready", "ASR cloud chưa được cấu hình trên máy chủ.", model: _options.OpenAI.Model);
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
                return TimeoutResult(credentials.Model);
            }

            if (lease == null)
            {
                return Unavailable("engine_busy", "Hàng đợi nhận dạng đang đầy. Thử lại sau.", credentials.Model);
            }

            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(wavPcm16Mono16kHz);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(streamContent, "file", "speech.wav");
            content.Add(new StringContent(credentials.Model), "model");

            using var request = new HttpRequestMessage(HttpMethod.Post, TranscriptionsRelativePath)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.ApiKey);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                budget.Token);

            return await MapResponseAsync(response, credentials.Model, budget.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return TimeoutResult(credentials.Model);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OpenAI speech returned invalid JSON (status category only).");
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.", credentials.Model);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OpenAI speech HTTP request failed.");
            return Unavailable("engine_unavailable", "Không kết nối được dịch vụ nhận dạng.", credentials.Model);
        }
        catch (IOException ex)
        {
            // Body read after 200 headers can reset; map to unavailable, do not throw 500.
            _logger.LogWarning(ex, "OpenAI speech response body read failed.");
            return Unavailable("engine_unavailable", "Không kết nối được dịch vụ nhận dạng.", credentials.Model);
        }
        finally
        {
            if (lease != null)
            {
                await lease.DisposeAsync();
            }
        }
    }

    private async Task<SpeechTranscriptionResult> MapResponseAsync(
        HttpResponseMessage response,
        string model,
        CancellationToken cancellationToken)
    {
        var statusCode = response.StatusCode;
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return Unavailable("engine_busy", "ASR đang quá tải.", model);
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("OpenAI speech auth/model access rejected with status {StatusCode}.", (int)statusCode);
            return Unavailable("engine_auth", "Không có quyền dùng dịch vụ nhận dạng.", model);
        }

        if (statusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("OpenAI speech model/endpoint not found.");
            return Unavailable("engine_auth", "Không có quyền dùng dịch vụ nhận dạng.", model);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI speech failed with status {StatusCode}.", (int)statusCode);
            return Unavailable("engine_http_error", "Không thể nhận dạng giọng nói lúc này.", model);
        }

        var maxBytes = Math.Clamp(_options.OpenAI.MaxResponseBytes, 1024, 2 * 1024 * 1024);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var limited = new MemoryStream(maxBytes);
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                _logger.LogWarning("OpenAI speech response exceeded MaxResponseBytes.");
                return Unavailable("response_too_large", "ASR trả về dữ liệu quá lớn.", model);
            }

            await limited.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        limited.Position = 0;
        using var document = await JsonDocument.ParseAsync(limited, cancellationToken: cancellationToken);
        return MapJson(document.RootElement, model);
    }

    private SpeechTranscriptionResult MapJson(JsonElement root, string model)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.", model);
        }

        if (root.TryGetProperty("error", out _))
        {
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.", model);
        }

        if (!root.TryGetProperty("text", out var textProp))
        {
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.", model);
        }

        if (textProp.ValueKind != JsonValueKind.String)
        {
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.", model);
        }

        var text = CollapseSpeech(textProp.GetString());
        if (string.IsNullOrWhiteSpace(text))
        {
            return NoSpeechResult(model);
        }

        return new SpeechTranscriptionResult
        {
            Status = SpeechTranscriptionStatus.Ok,
            Transcript = text,
            Segments = Array.Empty<SpeechSegment>(),
            EngineName = EngineName,
            ModelIdentity = model
        };
    }

    private SpeechTranscriptionResult TimeoutResult(string model) => new()
    {
        Status = SpeechTranscriptionStatus.Timeout,
        EngineName = EngineName,
        ModelIdentity = model,
        ErrorCode = "engine_timeout",
        Message = "Nhận dạng vượt quá thời gian cho phép."
    };

    private static SpeechTranscriptionResult NotReady(string code, string message, string model) => new()
    {
        Status = SpeechTranscriptionStatus.NotReady,
        EngineName = EngineName,
        ModelIdentity = string.IsNullOrWhiteSpace(model) ? "gpt-transcribe" : model,
        ErrorCode = code,
        Message = message
    };

    private static SpeechTranscriptionResult Unavailable(string code, string message, string model) => new()
    {
        Status = SpeechTranscriptionStatus.Unavailable,
        EngineName = EngineName,
        ModelIdentity = model,
        ErrorCode = code,
        Message = message
    };

    private static SpeechTranscriptionResult NoSpeechResult(string model) => new()
    {
        Status = SpeechTranscriptionStatus.NoSpeech,
        Transcript = string.Empty,
        Segments = Array.Empty<SpeechSegment>(),
        EngineName = EngineName,
        ModelIdentity = model,
        ErrorCode = "no_speech",
        Message = "Chưa nhận được lời nói."
    };

    private static string CollapseSpeech(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
