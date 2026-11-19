// Call creation, Location validation and readiness adapted from the pinned OpenAI example (MIT).
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;

namespace TCTEnglish.Services.AI.Voice;

public sealed partial class OpenAiRealtimeVoiceClient(
    IHttpClientFactory clients,
    IOpenAiSpeechCredentialResolver credentials,
    IOptions<AiVoiceOptions> options,
    IVoiceSidebandFactory sockets,
    ILogger<OpenAiRealtimeVoiceClient>? logger = null) : IRealtimeVoiceClient
{
    public const string HttpClientName = "ai-voice-realtime";
    private static readonly Uri Origin = new("https://api.openai.com");
    public bool IsConfigured => options.Value.Enabled && credentials.TryResolve(out _);

    public async Task<VoiceCall> CreateCallAsync(string offer, string instructions, string safetyIdentifier,
        Func<string, Task> persistCall, CancellationToken ct)
    {
        VoiceSdpPolicy.Validate(offer);
        ct.ThrowIfCancellationRequested();
        using var request = Request(HttpMethod.Post, "/v1/realtime/calls");
        request.Headers.Add("OpenAI-Safety-Identifier", safetyIdentifier);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(offer), "sdp");
        form.Add(new StringContent(VoiceSessionConfiguration.Build(options.Value, instructions).GetRawText(), Encoding.UTF8, "application/json"), "session");
        request.Content = form;
        // Once dispatched, browser cancellation must not discard Location. Independent finite timeout.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var response = await clients.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode is >= 400 and < 500 && response.StatusCode != HttpStatusCode.RequestTimeout)
                throw new VoiceProviderRejectedException();
            throw new VoiceException("provider_unavailable", 502);
        }
        var callId = ParseCallId(response.Headers.Location?.OriginalString);
        try { await persistCall(callId); }
        catch
        {
            // DB failure must not abandon an allocated call silently. The existing reservation
            // stays pending if either the persistence or this bounded fallback is uncertain.
            try { await HangupAsync(callId, CancellationToken.None); } catch { }
            throw;
        }
        await response.Content.LoadIntoBufferAsync(VoiceSdpPolicy.MaxBytes, timeout.Token);
        var answer = await response.Content.ReadAsStringAsync(timeout.Token);
        VoiceSdpPolicy.Validate(answer);
        return new(answer);
    }

    public async Task<IVoiceSideband> AttachAsync(string callId, string instructions, CancellationToken ct)
    {
        ValidateCallId(callId);
        IVoiceSideband? sideband = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            sideband = await sockets.ConnectAsync(new Uri("wss://api.openai.com/v1/realtime?call_id=" + callId), Key(), timeout.Token);
            var config = VoiceSessionConfiguration.Build(options.Value, instructions);
            // Model is chosen at creation and cannot be updated. ACK still verifies its
            // effective value alongside every mutable field requested by this server.
            var update = config.EnumerateObject().Where(p => p.Name != "model").ToDictionary(p => p.Name, p => p.Value);
            await sideband.SendAsync(JsonSerializer.SerializeToElement(new { type = "session.update", session = update }), timeout.Token);
            await WaitForReadyAsync(sideband, config, timeout.Token, diagnostic =>
                logger?.LogWarning("Voice readiness configuration mismatch: {Fields}; effective model: {Model}; transcription: {Transcription}; voice: {Voice}",
                    string.Join(",", diagnostic.Fields), diagnostic.Model, diagnostic.TranscriptionModel, diagnostic.Voice));
            return sideband;
        }
        catch { if (sideband != null) await sideband.DisposeAsync(); throw; }
    }

    public static async Task WaitForReadyAsync(IVoiceSideband sideband, JsonElement config, CancellationToken ct,
        Action<VoiceReadinessDiagnostic>? reportMismatch = null)
    {
        while (true)
        {
            var evt = await sideband.ReceiveAsync(ct);
            if (VoiceSessionConfiguration.String(evt, "type") == "error") throw new VoiceException("provider_configuration_failed", 502);
            if (VoiceSessionConfiguration.IsReady(evt, config)) return;
            if (VoiceSessionConfiguration.String(evt, "type") == "session.updated" && evt.TryGetProperty("session", out var session))
            {
                // Only static requested field names and a bounded public model identifier.
                // Never log instructions, call/session IDs, transcripts or raw events.
                reportMismatch?.Invoke(new(VoiceSessionConfiguration.MismatchedFields(session, config),
                    PublicValue(session, "model"), PublicValue(session, "audio", "input", "transcription", "model"),
                    PublicValue(session, "audio", "output", "voice")));
            }
        }
    }

    public async Task HangupAsync(string callId, CancellationToken ct)
    {
        ValidateCallId(callId);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var request = Request(HttpMethod.Post, $"/v1/realtime/calls/{callId}/hangup");
        using var response = await clients.CreateClient(HttpClientName).SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            throw new VoiceException("hangup_pending", 503);
    }

    public static string ParseCallId(string? location)
    {
        if (location == null || !Uri.TryCreate(Origin, location, out var uri)
            || uri.Scheme != "https" || uri.Host != Origin.Host || !uri.IsDefaultPort
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new VoiceException("invalid_provider_location", 502);
        var match = CallPath().Match(uri.AbsolutePath);
        if (!match.Success) throw new VoiceException("invalid_provider_location", 502);
        return match.Groups[1].Value;
    }

    private static void ValidateCallId(string value)
    {
        if (!CallId().IsMatch(value)) throw new VoiceException("invalid_provider_call", 502);
    }
    private string Key()
    {
        if (!credentials.TryResolve(out var value)) throw new VoiceException("voice_unavailable", 503);
        return value.ApiKey;
    }
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(Origin, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key());
        return request;
    }
    [GeneratedRegex(@"^/v1/realtime/calls/(rtc_[A-Za-z0-9_-]{1,180})$")]
    private static partial Regex CallPath();
    [GeneratedRegex(@"^rtc_[A-Za-z0-9_-]{1,180}$")]
    private static partial Regex CallId();
    [GeneratedRegex(@"^[a-z0-9][a-z0-9.-]{0,79}$")]
    private static partial Regex PublicModel();
    private static string? PublicValue(JsonElement value, params string[] path)
    {
        foreach (var name in path)
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out value)) return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return text != null && PublicModel().IsMatch(text) ? text : null;
    }

}

public sealed record VoiceReadinessDiagnostic(IReadOnlyList<string> Fields, string? Model, string? TranscriptionModel, string? Voice);

public sealed class ClientVoiceSidebandFactory : IVoiceSidebandFactory
{
    public async Task<IVoiceSideband> ConnectAsync(Uri uri, string apiKey, CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "Bearer " + apiKey);
        try { await socket.ConnectAsync(uri, ct); return new SocketSideband(socket); }
        catch { socket.Dispose(); throw; }
    }

    private sealed class SocketSideband(ClientWebSocket socket) : IVoiceSideband
    {
        public async Task SendAsync(JsonElement message, CancellationToken ct) =>
            await socket.SendAsync(Encoding.UTF8.GetBytes(message.GetRawText()), WebSocketMessageType.Text, true, ct);
        public async Task<JsonElement> ReceiveAsync(CancellationToken ct)
        {
            using var payload = new MemoryStream();
            var buffer = new byte[8192];
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType != WebSocketMessageType.Text || payload.Length + result.Count > 1024 * 1024)
                    throw new VoiceException("sideband_disconnected", 502);
                payload.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            using var document = JsonDocument.Parse(payload.ToArray());
            return document.RootElement.Clone();
        }
        public ValueTask DisposeAsync() { socket.Abort(); socket.Dispose(); return ValueTask.CompletedTask; }
    }
}
