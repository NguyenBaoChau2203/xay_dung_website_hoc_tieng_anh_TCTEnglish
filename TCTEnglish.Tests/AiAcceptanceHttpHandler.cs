using System.Text.Json;
using TCTEnglish.Services.AI;

namespace TCTEnglish.Tests;

/// <summary>
/// Acceptance-only transport wrapper shared by the production embedding/text
/// clients in a harness. Every retry traverses this handler. Nothing registers it
/// in the application or authorizes a live transport.
/// </summary>
public sealed class AiAcceptanceHttpHandler(AiAcceptanceRunGuard run, HttpMessageHandler transport) : DelegatingHandler(transport)
{
    private const int MaxBodyBytes = 8 * 1024 * 1024;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, run.Token);
        await run.HttpGate.WaitAsync(linked.Token);
        var admitted = false;
        var settled = false;
        HttpResponseMessage? response = null;
        try
        {
            if (request.Method != HttpMethod.Post || request.Content is null)
                throw new AiProviderException("Acceptance transport requires a supported POST.", AiProviderException.ErrorCodeInvalidConfiguration, false);

            var payload = await request.Content.ReadAsByteArrayAsync(linked.Token);
            var embeddings = request.RequestUri?.AbsolutePath.EndsWith("/embeddings", StringComparison.Ordinal) == true;
            var responses = request.RequestUri?.AbsolutePath.EndsWith("/responses", StringComparison.Ordinal) == true;
            if (!embeddings && !responses)
                throw new AiProviderException("Unsupported acceptance endpoint.", AiProviderException.ErrorCodeInvalidConfiguration, false);

            using var parsed = JsonDocument.Parse(payload);
            var root = parsed.RootElement;
            var upper = embeddings
                ? EmbeddingTokenBound.Sum(root.GetProperty("input").EnumerateArray().Select(value => value.GetString() ?? string.Empty))
                : checked(payload.Length + root.GetProperty("max_output_tokens").GetInt32());
            if (!run.TryAdmit(upper, int.MaxValue))
                throw new AiProviderException("Acceptance run allowance exhausted.", AiProviderException.ErrorCodeBudgetExhausted, false);
            admitted = true;

            response = await base.SendAsync(request, linked.Token);
            var bytes = await ReadBoundedAsync(response.Content, linked.Token);
            if (TryUsage(bytes, embeddings, out var known)) run.RecordKnown(known);
            else run.RecordUnknown();
            settled = true;

            var content = new ByteArrayContent(bytes);
            foreach (var header in response.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            response.Content.Dispose();
            response.Content = content;
            return response;
        }
        catch
        {
            if (admitted && !settled) run.RecordUnknown();
            response?.Dispose();
            throw;
        }
        finally
        {
            run.HttpGate.Release();
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength > MaxBodyBytes) throw new IOException("Acceptance response exceeds its limit.");
        await using var stream = await content.ReadAsStreamAsync(ct).WaitAsync(ct);
        using var buffer = new MemoryStream();
        var part = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(part, ct)) != 0)
        {
            if (buffer.Length + read > MaxBodyBytes) throw new IOException("Acceptance response exceeds its limit.");
            await buffer.WriteAsync(part.AsMemory(0, read), ct);
        }
        return buffer.ToArray();
    }

    private static bool TryUsage(byte[] bytes, bool embeddings, out int total)
    {
        total = 0;
        try
        {
            using var parsed = JsonDocument.Parse(bytes);
            var usage = parsed.RootElement.GetProperty("usage");
            var input = usage.GetProperty(embeddings ? "prompt_tokens" : "input_tokens").GetInt32();
            var output = embeddings ? 0 : usage.GetProperty("output_tokens").GetInt32();
            total = usage.GetProperty("total_tokens").GetInt32();
            return input >= 0 && output >= 0 && total > 0 && (long)input + output == total;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            return false;
        }
    }
}
