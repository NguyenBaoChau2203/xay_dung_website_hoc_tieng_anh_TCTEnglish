using System.Text.Json;

namespace TCTEnglish.Services.AI.Voice;

public sealed record VoiceCall(string AnswerSdp);

public interface IVoiceSideband : IAsyncDisposable
{
    Task SendAsync(JsonElement message, CancellationToken ct);
    Task<JsonElement> ReceiveAsync(CancellationToken ct);
}

public interface IVoiceSidebandFactory
{
    Task<IVoiceSideband> ConnectAsync(Uri uri, string apiKey, CancellationToken ct);
}

public interface IRealtimeVoiceClient
{
    bool IsConfigured { get; }
    Task<VoiceCall> CreateCallAsync(string offer, string instructions, string safetyIdentifier, Func<string, Task> persistCall, CancellationToken ct);
    Task<IVoiceSideband> AttachAsync(string callId, string instructions, CancellationToken ct);
    Task HangupAsync(string callId, CancellationToken ct);
}
