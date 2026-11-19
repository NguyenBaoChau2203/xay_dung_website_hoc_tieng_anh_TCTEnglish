// Readiness pattern adapted from OpenAI sidebandReady.ts (MIT); see third_party notice.
using System.Text.Json;

namespace TCTEnglish.Services.AI.Voice;

public static class VoiceSessionConfiguration
{
    public static JsonElement Build(AiVoiceOptions options, string instructions) => JsonSerializer.SerializeToElement(new
    {
        type = "realtime",
        model = options.Model,
        instructions,
        tools = Array.Empty<object>(),
        tool_choice = "none",
        output_modalities = new[] { "audio" },
        max_output_tokens = 512,
        audio = new
        {
            input = new
            {
                // Let transcription detect language. Effective WebRTC ACKs may not echo
                // the optional language hint exactly; keep readiness independent of it.
                transcription = new { model = "gpt-transcribe" },
                turn_detection = new { type = "server_vad", threshold = 0.5, prefix_padding_ms = 300, silence_duration_ms = 600, create_response = true, interrupt_response = false }
            },
            output = new { voice = options.Voice }
        }
    });

    public static bool IsReady(JsonElement evt, JsonElement expected)
    {
        if (String(evt, "type") != "session.updated" || !evt.TryGetProperty("session", out var session)) return false;
        return ContainsExpected(session, expected);
    }

    // Provider ACKs include defaults. Every requested field must match, including empty tools.
    public static IReadOnlyList<string> MismatchedFields(JsonElement actual, JsonElement expected)
    {
        var fields = new List<string>();
        Compare(actual, expected, "");
        return fields;
        void Compare(JsonElement current, JsonElement requested, string path)
        {
            if (requested.ValueKind == JsonValueKind.Object && current.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in requested.EnumerateObject())
                {
                    var name = path.Length == 0 ? p.Name : path + "." + p.Name;
                    if (!current.TryGetProperty(p.Name, out var value)) fields.Add(name);
                    else Compare(value, p.Value, name);
                }
            }
            else if (!ContainsExpected(current, requested)) fields.Add(path.Length == 0 ? "session" : path);
        }
    }

    private static bool ContainsExpected(JsonElement actual, JsonElement expected)
    {
        if (actual.ValueKind != expected.ValueKind) return false;
        if (expected.ValueKind == JsonValueKind.Object)
            return expected.EnumerateObject().All(p => actual.TryGetProperty(p.Name, out var value) && ContainsExpected(value, p.Value));
        return JsonElement.DeepEquals(actual, expected);
    }

    public static string? String(JsonElement obj, string name) => obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
