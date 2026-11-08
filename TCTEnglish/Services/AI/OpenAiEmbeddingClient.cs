using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.AI;

/// <summary>
/// POST /embeddings for text-embedding-3-small. Official contract: CreateEmbeddingRequest
/// requires model and input; encoding_format float; dimensions for embedding-3.
/// The response is object "list" with indexed embedding objects and usage prompt_tokens
/// plus total_tokens. This client does not send reasoning, temperature or text-model fields.
/// One deadline covers the body, retries and backoff. Redirects are not followed.
/// </summary>
public sealed class OpenAiEmbeddingClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AiOptions> textOptions,
    IOptions<EmbeddingOptions> embeddingOptions,
    ILogger<OpenAiEmbeddingClient> logger) : IOpenAiEmbeddingClient
{
    public const string HttpClientName = "OpenAIEmbeddings";

    private readonly AiOptions _text = textOptions.Value;
    private readonly EmbeddingOptions _embedding = embeddingOptions.Value;

    public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct)
    {
        return EmbedAsync(inputs, new EmbeddingInvocation(), ct);
    }

    public async Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, EmbeddingInvocation invocation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(invocation);
        ct.ThrowIfCancellationRequested();
        ValidateInputs(inputs);
        var endpoint = ValidateEndpoint();
        var payload = JsonSerializer.Serialize(new
        {
            model = EmbeddingOptions.ModelName,
            input = inputs,
            encoding_format = EmbeddingOptions.EncodingFormat,
            dimensions = EmbeddingOptions.Dimensions
        });
        var timeout = TimeSpan.FromSeconds(_embedding.EffectiveTimeoutSeconds);
        var clock = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        var maxAttempts = _embedding.EffectiveRetryMaxAttempts + 1;
        var client = httpClientFactory.CreateClient(HttpClientName);
        var ledger = invocation.Budget;
        var inputTokenUpperBound = EmbeddingTokenBound.Sum(inputs);

        async Task ReportAsync()
        {
            if (invocation.RecordUsage is null || ledger is null) return;
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await invocation.RecordUsage(ledger.KnownPromptTokens, ledger.UncertainPromptTokenUpperBound, cleanup.Token))
                throw new AiProviderException("The chat turn no longer owns its embedding stage.", AiProviderException.ErrorCodeCorpusStale, false);
        }

        try
        {
            AiProviderException? previous = null;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (invocation.ConfirmCurrent is not null && !await invocation.ConfirmCurrent(deadline.Token))
                {
                    throw new AiProviderException(
                        "Embeddings were not sent because the approved corpus changed.",
                        AiProviderException.ErrorCodeCorpusStale,
                        false);
                }

                deadline.Token.ThrowIfCancellationRequested();
                if (ledger is not null && !ledger.TryReserveAttempt(inputTokenUpperBound))
                {
                    if (previous is not null)
                    {
                        throw previous;
                    }

                    throw new AiProviderException(
                        "Embeddings were not sent because the request allowance is exhausted.",
                        AiProviderException.ErrorCodeBudgetExhausted,
                        false);
                }

                AiProviderException failure;
                TimeSpan? retryAfter = null;
                try
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _text.ApiKey.Trim());
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                    if ((int)response.StatusCode is >= 300 and < 400)
                    {
                        failure = new AiProviderException(
                            "OpenAI embeddings endpoint returned a redirect, which was not followed.",
                            AiProviderException.ErrorCodeRedirectRefused,
                            false);
                    }
                    else
                    {
                        var body = await ReadBoundedBodyAsync(response.Content, deadline.Token);
                        RecordResponseUsage(body, ledger);
                        await ReportAsync();
                        deadline.Token.ThrowIfCancellationRequested();
                        if (response.IsSuccessStatusCode)
                        {
                            var batch = ParseBatch(body, inputs.Count);
                            deadline.Token.ThrowIfCancellationRequested();
                            if (clock.Elapsed >= timeout)
                            {
                                throw TimeoutFailure();
                            }

                            return batch;
                        }

                        failure = HttpFailure(response.StatusCode, body);
                        retryAfter = response.Headers.RetryAfter?.Delta
                            ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
                    }

                    logger.LogWarning(
                        "OpenAI embeddings request failed: status {Status}, code {Code}, attempt {Attempt}/{MaxAttempts}.",
                        (int)response.StatusCode,
                        failure.ErrorCode,
                        attempt,
                        maxAttempts);
                }
                catch (HttpRequestException)
                {
                    failure = new AiProviderException(
                        "Unable to reach the OpenAI embeddings endpoint.",
                        AiProviderException.ErrorCodeNetwork,
                        true);
                }
                catch (IOException)
                {
                    failure = new AiProviderException(
                        "Unable to read the OpenAI embeddings response.",
                        AiProviderException.ErrorCodeNetwork,
                        true);
                }
                catch (AiProviderException)
                {
                    ledger?.MarkPendingUsageUnknown();
                    await ReportAsync();
                    throw;
                }
                catch (OperationCanceledException)
                {
                    ledger?.MarkPendingUsageUnknown();
                    await ReportAsync();
                    throw;
                }

                if (ledger is not null)
                {
                    ledger.MarkPendingUsageUnknown();
                }
                await ReportAsync();

                previous = failure;
                if (!failure.IsTransient || attempt == maxAttempts || (ledger is not null && !ledger.CanAttempt))
                {
                    throw failure;
                }

                var delay = retryAfter ?? TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                if (delay >= timeout - clock.Elapsed)
                {
                    throw failure;
                }

                await Task.Delay(delay, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ledger?.MarkPendingUsageUnknown();
            await ReportAsync();
            throw;
        }
        catch (OperationCanceledException)
        {
            ledger?.MarkPendingUsageUnknown();
            await ReportAsync();
            throw TimeoutFailure();
        }

        throw new AiProviderException("OpenAI embeddings request failed.", AiProviderException.ErrorCodeUnknown, false);
    }

    private void ValidateInputs(IReadOnlyList<string> inputs)
    {
        if (inputs.Count < 1 || inputs.Count > EmbeddingOptions.MaxInputsPerRequest)
        {
            throw new AiProviderException(
                "The embeddings batch size is outside the supported bound.",
                AiProviderException.ErrorCodeInvalidConfiguration,
                false);
        }

        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Length > _embedding.MaxInputCharacters)
            {
                throw new AiProviderException(
                    "An embeddings input is empty or longer than the supported bound.",
                    AiProviderException.ErrorCodeInvalidConfiguration,
                    false);
            }
        }
    }

    private Uri ValidateEndpoint()
    {
        if (string.IsNullOrWhiteSpace(_text.ApiKey)
            || _text.ApiKey.Any(char.IsWhiteSpace)
            || _text.ApiKey.StartsWith("AIza", StringComparison.Ordinal))
        {
            throw new AiProviderException(
                "OpenAI key is missing or unusable. Set AI:ApiKey in local User Secrets or AI__ApiKey in the environment.",
                AiProviderException.ErrorCodeInvalidConfiguration,
                false);
        }

        if (string.IsNullOrWhiteSpace(_text.BaseUrl))
        {
            throw new AiProviderException(
                "AI:BaseUrl is missing. Use the shared OpenAI default.",
                AiProviderException.ErrorCodeInvalidConfiguration,
                false);
        }

        var baseUrl = _text.BaseUrl.Trim().TrimEnd('/');
        if (baseUrl.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = baseUrl[..^"/responses".Length].TrimEnd('/');
        }

        if (baseUrl.EndsWith("/embeddings", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = baseUrl[..^"/embeddings".Length].TrimEnd('/');
        }

        baseUrl += "/embeddings";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback))
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment)
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || endpoint.Host.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new AiProviderException(
                "AI:BaseUrl must be an OpenAI-compatible HTTPS endpoint (HTTP is allowed only on loopback).",
                AiProviderException.ErrorCodeInvalidConfiguration,
                false);
        }

        return endpoint;
    }

    private async Task<byte[]> ReadBoundedBodyAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength > _embedding.MaxResponseBytes)
        {
            throw InvalidResponse();
        }

        await using var stream = await content.ReadAsStreamAsync(ct).WaitAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (output.Length + read > _embedding.MaxResponseBytes)
            {
                throw InvalidResponse();
            }

            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        return output.ToArray();
    }

    private static void RecordResponseUsage(byte[] body, EmbeddingOperationBudget? ledger)
    {
        if (ledger is null) return;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object
                && usage.TryGetProperty("prompt_tokens", out var prompt) && prompt.TryGetInt32(out var known)
                && usage.TryGetProperty("total_tokens", out var total) && total.TryGetInt32(out var totalCount)
                && known > 0 && totalCount == known
                && !usage.TryGetProperty("output_tokens", out _) && !usage.TryGetProperty("completion_tokens", out _))
            {
                // Charge information is independent of subsequent vector validation.
                ledger.RecordKnownUsage(known);
                return;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or OverflowException)
        {
        }
        ledger.MarkPendingUsageUnknown();
    }

    private static EmbeddingBatch ParseBatch(byte[] body, int inputCount)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || String(root, "object") != "list"
                || String(root, "model") != EmbeddingOptions.ModelName
                || !root.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array
                || data.GetArrayLength() != inputCount
                || !root.TryGetProperty("usage", out var usage)
                || usage.ValueKind != JsonValueKind.Object)
            {
                throw InvalidResponse();
            }

            if (usage.TryGetProperty("output_tokens", out _) || usage.TryGetProperty("completion_tokens", out _))
            {
                throw InvalidResponse();
            }

            var prompt = RequiredTokens(usage, "prompt_tokens");
            var total = RequiredTokens(usage, "total_tokens");
            if (prompt != total || prompt < 1)
            {
                throw InvalidResponse();
            }

            var vectors = new float[inputCount][];
            var seen = new bool[inputCount];
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || String(item, "object") != "embedding"
                    || !item.TryGetProperty("index", out var indexElement)
                    || !indexElement.TryGetInt32(out var index)
                    || index < 0
                    || index >= inputCount
                    || seen[index]
                    || !item.TryGetProperty("embedding", out var embedding)
                    || embedding.ValueKind != JsonValueKind.Array
                    || embedding.GetArrayLength() != EmbeddingOptions.Dimensions)
                {
                    throw InvalidResponse();
                }

                var vector = new float[EmbeddingOptions.Dimensions];
                double sum = 0;
                var component = 0;
                foreach (var value in embedding.EnumerateArray())
                {
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
                    {
                        throw InvalidResponse();
                    }

                    var converted = (float)number;
                    if (!float.IsFinite(converted))
                    {
                        throw InvalidResponse();
                    }

                    vector[component++] = converted;
                    sum += converted * (double)converted;
                }

                if (component != EmbeddingOptions.Dimensions || !double.IsFinite(sum) || sum == 0d)
                {
                    throw InvalidResponse();
                }

                seen[index] = true;
                vectors[index] = vector;
            }

            if (seen.Any(slot => !slot))
            {
                throw InvalidResponse();
            }

            return new EmbeddingBatch(vectors, prompt, total);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or OverflowException)
        {
            throw InvalidResponse();
        }
    }

    private static AiProviderException HttpFailure(HttpStatusCode status, byte[] body)
    {
        string? code = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object)
            {
                code = String(error, "code") ?? String(error, "type");
            }
        }
        catch (JsonException)
        {
        }

        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new AiProviderException(
                "OpenAI embeddings credentials or permissions were rejected.",
                AiProviderException.ErrorCodeAuthentication,
                false),
            HttpStatusCode.NotFound => new AiProviderException(
                "The embeddings model or endpoint is unavailable.",
                AiProviderException.ErrorCodeModelAccess,
                false),
            HttpStatusCode.TooManyRequests when code is "insufficient_quota" or "billing_hard_limit_reached" => new AiProviderException(
                "OpenAI embeddings account quota is exhausted.",
                AiProviderException.ErrorCodeQuota,
                false),
            HttpStatusCode.TooManyRequests => new AiProviderException(
                "OpenAI embeddings rate limit reached.",
                AiProviderException.ErrorCodeRateLimited,
                true),
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => new AiProviderException(
                "OpenAI rejected the embeddings request configuration.",
                AiProviderException.ErrorCodeInvalidConfiguration,
                false),
            _ when (int)status >= 500 => new AiProviderException(
                "OpenAI embeddings is temporarily unavailable.",
                AiProviderException.ErrorCodeProviderUnavailable,
                true),
            _ => new AiProviderException("OpenAI embeddings request failed.", AiProviderException.ErrorCodeUnknown, false)
        };
    }

    private static string? String(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int RequiredTokens(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetInt32(out var count) || count < 0)
        {
            throw InvalidResponse();
        }

        return count;
    }

    private static AiProviderException TimeoutFailure()
    {
        return new AiProviderException(
            "OpenAI embeddings request exceeded its total deadline.",
            AiProviderException.ErrorCodeTimeout,
            true);
    }

    private static AiProviderException InvalidResponse()
    {
        return new AiProviderException(
            "OpenAI embeddings returned an invalid response.",
            AiProviderException.ErrorCodeInvalidResponse,
            false);
    }
}
