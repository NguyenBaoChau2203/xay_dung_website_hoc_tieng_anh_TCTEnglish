using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.AI;

/// <summary>Text-only Responses transport. One deadline covers every attempt, body and backoff.</summary>
public sealed class OpenAiProviderClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AiOptions> options,
    ILogger<OpenAiProviderClient> logger) : IAiProviderClient
{
    public const string HttpClientName = "OpenAI";
    private const int MaxResponseBytes = 1024 * 1024;
    private readonly AiOptions _options = options.Value;

    public async Task<AiProviderReply> GenerateReplyAsync(int userId, IReadOnlyList<AiContextMessage> messages,
        CancellationToken ct, AiProviderRequestOptions? requestOptions = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0) throw new ArgumentException("Context messages are required.", nameof(messages));
        ct.ThrowIfCancellationRequested();
        var endpoint = ValidateConfiguration();
        if (messages.Any(m => m.Role is not ("system" or "developer" or "user" or "assistant")))
            throw new ArgumentException("Unsupported context role.", nameof(messages));

        var maxOutput = Math.Clamp(requestOptions?.MaxOutputTokens ?? _options.MaxOutputTokens, 1, 16384);
        var payload = JsonSerializer.Serialize(new
        {
            model = _options.Model,
            reasoning = new { effort = _options.ReasoningEffort },
            max_output_tokens = maxOutput,
            store = false,
            input = messages.Select(m => new { role = m.Role, content = m.Content ?? string.Empty })
        });
        var timeout = TimeSpan.FromSeconds(Math.Clamp(requestOptions?.RequestTimeoutSeconds ?? _options.EffectiveRequestTimeoutSeconds, 1, 120));
        var clock = Stopwatch.StartNew();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(timeout);
        var maxAttempts = Math.Clamp(_options.EffectiveRetryMaxAttempts, 0, 3) + 1;
        var client = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                AiProviderException failure;
                TimeSpan? retryAfter = null;
                try
                {
                    budget.Token.ThrowIfCancellationRequested();
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token);
                    var body = await ReadBoundedBodyAsync(response.Content, budget.Token);
                    budget.Token.ThrowIfCancellationRequested();
                    if (response.IsSuccessStatusCode)
                    {
                        var reply = ParseReply(body, response);
                        budget.Token.ThrowIfCancellationRequested();
                        if (clock.Elapsed >= timeout) throw TimeoutFailure();
                        return reply;
                    }
                    failure = HttpFailure(response.StatusCode, body);
                    retryAfter = response.Headers.RetryAfter?.Delta
                        ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
                    logger.LogWarning("OpenAI request failed: status {Status}, code {Code}, attempt {Attempt}/{MaxAttempts}.",
                        (int)response.StatusCode, failure.ErrorCode, attempt, maxAttempts);
                }
                catch (HttpRequestException)
                {
                    // Do not retain exception text: custom transports can include sensitive request details.
                    failure = new("Unable to reach OpenAI. Check the network and endpoint.", AiProviderException.ErrorCodeNetwork, true);
                }
                catch (IOException)
                {
                    failure = new("Unable to read the OpenAI response.", AiProviderException.ErrorCodeNetwork, true);
                }

                if (!failure.IsTransient || attempt == maxAttempts) throw failure;
                var delay = retryAfter ?? TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                // Do not violate Retry-After or start an attempt that cannot fit in this request's budget.
                if (delay >= timeout - clock.Elapsed) throw failure;
                await Task.Delay(delay, budget.Token);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { throw TimeoutFailure(); }
        throw new AiProviderException("OpenAI request failed.", AiProviderException.ErrorCodeUnknown, false);
    }

    private Uri ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new AiProviderException("OpenAI key is missing. Set AI:ApiKey in local User Secrets or AI__ApiKey in the environment.", AiProviderException.ErrorCodeInvalidConfiguration, false);
        if (_options.ApiKey.Any(char.IsWhiteSpace) || _options.ApiKey.StartsWith("AIza", StringComparison.Ordinal)
            || _options.Model != "gpt-6-luna" || _options.ReasoningEffort != "low")
            throw new AiProviderException("Configure AI:Model=gpt-6-luna and AI:ReasoningEffort=low with a valid OpenAI key.", AiProviderException.ErrorCodeInvalidConfiguration, false);
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new AiProviderException("AI:BaseUrl is missing. Use the shared OpenAI default.", AiProviderException.ErrorCodeInvalidConfiguration, false);
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        if (!baseUrl.EndsWith("/responses", StringComparison.Ordinal)) baseUrl += "/responses";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback))
            || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || endpoint.Host.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            throw new AiProviderException("AI:BaseUrl must be an OpenAI-compatible HTTPS endpoint (HTTP is allowed only on loopback). Remove legacy Google overrides.", AiProviderException.ErrorCodeInvalidConfiguration, false);
        return endpoint;
    }

    private static async Task<byte[]> ReadBoundedBodyAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength > MaxResponseBytes) throw InvalidResponse();
        await using var stream = await content.ReadAsStreamAsync(ct).WaitAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (output.Length + read > MaxResponseBytes) throw InvalidResponse();
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return output.ToArray();
    }

    private AiProviderReply ParseReply(byte[] body, HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw InvalidResponse();
            var status = String(root, "status");
            if (status == "incomplete")
                throw new AiProviderException("OpenAI output was incomplete. No partial result was accepted.", AiProviderException.ErrorCodeIncompleteResponse, false);
            if (status != "completed" || (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)) throw InvalidResponse();
            if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array) throw InvalidResponse();
            var text = new StringBuilder();
            foreach (var item in output.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) throw InvalidResponse();
                if (String(item, "type") != "message") continue; // Never expose reasoning/tool items.
                if (String(item, "role") != "assistant") throw InvalidResponse();
                if (item.TryGetProperty("status", out var itemStatus) && itemStatus.GetString() != "completed") throw InvalidResponse();
                if (!item.TryGetProperty("content", out var parts) || parts.ValueKind != JsonValueKind.Array) throw InvalidResponse();
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.ValueKind != JsonValueKind.Object) throw InvalidResponse();
                    var type = String(part, "type");
                    if (type == "refusal") throw new AiProviderException("OpenAI declined this request.", AiProviderException.ErrorCodeRefusal, false);
                    if (type != "output_text") continue;
                    text.Append(String(part, "text") ?? throw InvalidResponse());
                }
            }
            if (string.IsNullOrWhiteSpace(text.ToString()))
                throw new AiProviderException("OpenAI returned empty content.", AiProviderException.ErrorCodeEmptyResponse, false);
            var input = 0; var completion = 0; var total = 0; var reasoning = 0;
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind != JsonValueKind.Null)
            {
                if (usage.ValueKind != JsonValueKind.Object) throw InvalidResponse();
                input = Tokens(usage, "input_tokens");
                completion = Tokens(usage, "output_tokens");
                total = usage.TryGetProperty("total_tokens", out _) ? Tokens(usage, "total_tokens") : checked(input + completion);
                if (usage.TryGetProperty("output_tokens_details", out var details) && details.ValueKind != JsonValueKind.Null)
                    reasoning = Tokens(details, "reasoning_tokens");
                if (reasoning > completion) throw InvalidResponse();
            }
            var requestId = response.Headers.TryGetValues("x-request-id", out var values) ? values.FirstOrDefault() : null;
            return new(text.ToString().Trim(), input, completion, total, String(root, "model") ?? _options.Model, requestId)
            { ReasoningTokens = reasoning };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or OverflowException)
        { throw InvalidResponse(); }
    }

    private static string? String(JsonElement root, string name) => root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int Tokens(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object) throw InvalidResponse();
        if (!root.TryGetProperty(name, out var value)) return 0;
        return value.TryGetInt32(out var count) && count >= 0 ? count : throw InvalidResponse();
    }

    private static AiProviderException HttpFailure(HttpStatusCode status, byte[] body)
    {
        string? code = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                code = String(error, "code") ?? String(error, "type");
        }
        catch (JsonException) { }
        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new("OpenAI credentials or permissions were rejected. Check the local key and account access.", AiProviderException.ErrorCodeAuthentication, false),
            HttpStatusCode.NotFound => new("OpenAI model or endpoint is unavailable. Verify access to gpt-6-luna.", AiProviderException.ErrorCodeModelAccess, false),
            HttpStatusCode.TooManyRequests when code is "insufficient_quota" or "billing_hard_limit_reached" => new("OpenAI account quota is exhausted. Check API billing and limits.", AiProviderException.ErrorCodeQuota, false),
            HttpStatusCode.TooManyRequests => new("OpenAI rate limit reached. Try again later.", AiProviderException.ErrorCodeRateLimited, true),
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => new("OpenAI rejected the request configuration.", AiProviderException.ErrorCodeInvalidConfiguration, false),
            _ when (int)status >= 500 => new("OpenAI is temporarily unavailable.", AiProviderException.ErrorCodeProviderUnavailable, true),
            _ => new("OpenAI request failed.", AiProviderException.ErrorCodeUnknown, false)
        };
    }
    private static AiProviderException TimeoutFailure() => new("OpenAI request exceeded its total deadline.", AiProviderException.ErrorCodeTimeout, true);
    private static AiProviderException InvalidResponse() => new("OpenAI returned an invalid response.", AiProviderException.ErrorCodeInvalidResponse, false);
}
