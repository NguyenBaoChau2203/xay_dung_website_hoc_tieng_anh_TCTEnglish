using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI.Resilience;

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

        var endpoint = _httpClient.BaseAddress is { } baseAddress
            ? new Uri(baseAddress, TranscriptionsRelativePath).AbsoluteUri
            : TranscriptionsRelativePath;
        using var outageLease = AiOutageAmbient.Current.TryEnter(
            AiProviderFamily.Speech,
            endpoint,
            credentials.Model,
            credentials.ApiKey);
        if (!outageLease.Admitted)
        {
            return Unavailable("circuit_open", "Dịch vụ đánh giá đang gián đoạn.", credentials.Model);
        }

        IAsyncDisposable? limiterLease = null;
        var sent = false;
        try
        {
            try
            {
                limiterLease = await _limiter.AcquireAsync(budget.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                outageLease.Release();
                throw;
            }
            catch (OperationCanceledException)
            {
                outageLease.Release();
                return TimeoutResult(credentials.Model);
            }

            if (limiterLease == null)
            {
                outageLease.Release();
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

            sent = true;
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                budget.Token);

            var mapped = await MapResponseAsync(response, credentials.Model, budget.Token);
            if (mapped.Result.Status is SpeechTranscriptionStatus.Ok or SpeechTranscriptionStatus.NoSpeech)
            {
                outageLease.CompleteSuccess();
            }
            else if (mapped.TransientOutage)
            {
                outageLease.CompleteTransient(mapped.RetryAfter);
            }
            else
            {
                outageLease.CompleteNonTransient();
            }

            return mapped.Result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outageLease.Release();
            throw;
        }
        catch (OperationCanceledException)
        {
            if (sent) outageLease.CompleteTransient(null);
            else outageLease.Release();
            return TimeoutResult(credentials.Model);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OpenAI speech returned invalid JSON (status category only).");
            outageLease.CompleteNonTransient();
            return Unavailable("invalid_schema", "ASR trả về dữ liệu không đọc được.", credentials.Model);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OpenAI speech HTTP request failed.");
            outageLease.CompleteTransient(null);
            return Unavailable("engine_unavailable", "Không kết nối được dịch vụ nhận dạng.", credentials.Model);
        }
        catch (IOException ex)
        {
            // Body read after 200 headers can reset; map to unavailable, do not throw 500.
            _logger.LogWarning(ex, "OpenAI speech response body read failed.");
            outageLease.CompleteTransient(null);
            return Unavailable("engine_unavailable", "Không kết nối được dịch vụ nhận dạng.", credentials.Model);
        }
        finally
        {
            if (limiterLease != null)
            {
                await limiterLease.DisposeAsync();
            }
        }
    }

    private readonly record struct MappedSpeech(SpeechTranscriptionResult Result, bool TransientOutage, TimeSpan? RetryAfter);

    private async Task<MappedSpeech> MapResponseAsync(
        HttpResponseMessage response,
        string model,
        CancellationToken cancellationToken)
    {
        var statusCode = response.StatusCode;
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            var billingQuota = await IsBillingQuotaAsync(response, cancellationToken);
            if (billingQuota)
            {
                return new MappedSpeech(
                    Unavailable("quota_exhausted", "Tính năng AI tạm thời chưa sẵn sàng.", model),
                    false,
                    null);
            }

            var retryAfter = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
            var retrySeconds = retryAfter is { } delay && delay > TimeSpan.Zero
                ? (int)Math.Clamp(Math.Ceiling(delay.TotalSeconds), 1, 120)
                : (int?)null;
            return new MappedSpeech(
                Unavailable("engine_busy", "ASR đang quá tải.", model, retrySeconds),
                true,
                retryAfter);
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("OpenAI speech auth/model access rejected with status {StatusCode}.", (int)statusCode);
            return new MappedSpeech(Unavailable("engine_auth", "Không có quyền dùng dịch vụ nhận dạng.", model), false, null);
        }

        if (statusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("OpenAI speech model/endpoint not found.");
            return new MappedSpeech(Unavailable("engine_auth", "Không có quyền dùng dịch vụ nhận dạng.", model), false, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI speech failed with status {StatusCode}.", (int)statusCode);
            return new MappedSpeech(
                Unavailable("engine_http_error", "Không thể nhận dạng giọng nói lúc này.", model),
                (int)statusCode >= 500,
                null);
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
                return new MappedSpeech(Unavailable("response_too_large", "ASR trả về dữ liệu quá lớn.", model), false, null);
            }

            await limited.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        limited.Position = 0;
        using var document = await JsonDocument.ParseAsync(limited, cancellationToken: cancellationToken);
        return new MappedSpeech(MapJson(document.RootElement, model), false, null);
    }

    private const int MaxErrorMetadataBytes = 8192;

    private static async Task<bool> IsBillingQuotaAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var (text, oversized) = await ReadBoundedUtf8Async(response.Content, MaxErrorMetadataBytes, cancellationToken);
            if (oversized || string.IsNullOrEmpty(text))
            {
                return false;
            }

            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("error", out var error)
                || error.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var code = error.TryGetProperty("code", out var codeValue) && codeValue.ValueKind == JsonValueKind.String
                ? codeValue.GetString()
                : null;
            var type = error.TryGetProperty("type", out var typeValue) && typeValue.ValueKind == JsonValueKind.String
                ? typeValue.GetString()
                : null;
            return code is "insufficient_quota" or "billing_hard_limit_reached"
                || type is "insufficient_quota" or "billing_hard_limit_reached";
        }
        catch (Exception ex) when (ex is JsonException or IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    private static async Task<(string Text, bool Oversized)> ReadBoundedUtf8Async(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[maxBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        if (total > maxBytes)
        {
            return (string.Empty, true);
        }

        return (Encoding.UTF8.GetString(buffer, 0, total), false);
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

    private static SpeechTranscriptionResult Unavailable(string code, string message, string model, int? retryAfterSeconds = null) => new()
    {
        Status = SpeechTranscriptionStatus.Unavailable,
        EngineName = EngineName,
        ModelIdentity = model,
        ErrorCode = code,
        Message = message,
        RetryAfterSeconds = retryAfterSeconds
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
