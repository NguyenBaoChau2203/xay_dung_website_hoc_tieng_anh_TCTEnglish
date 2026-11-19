// Allowlist adapted from publicEventProjection.ts / publicEvents.ts (OpenAI, MIT).
using System.Text.Json;

namespace TCTEnglish.Services.AI.Voice;

public sealed record VoicePublicEvent(string Type, string? State = null, string? Code = null)
{
    public static VoicePublicEvent? Project(JsonElement evt) => VoiceSessionConfiguration.String(evt, "type") switch
    {
        "input_audio_buffer.speech_started" => new("app.agent.state", "listening"),
        "input_audio_buffer.speech_stopped" or "response.created" => new("app.agent.state", "thinking"),
        "output_audio_buffer.started" => new("app.agent.state", "speaking"),
        "output_audio_buffer.stopped" or "output_audio_buffer.cleared" => new("app.agent.state", "listening"),
        "error" => new("app.error", Code: "VOICE_SESSION_ERROR"),
        "response.done" when evt.TryGetProperty("response", out var response)
            && VoiceSessionConfiguration.String(response, "status") is "failed" or "incomplete"
            => new("app.error", Code: "VOICE_RESPONSE_ERROR"),
        _ => null
    };
}
