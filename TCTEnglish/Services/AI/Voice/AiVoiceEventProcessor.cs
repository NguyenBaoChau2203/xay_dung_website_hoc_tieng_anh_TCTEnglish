using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Voice;

public sealed class AiVoiceEventProcessor(DbflashcardContext db, AiVoiceQuotaService quota, TimeProvider clock)
{
    public Task<bool> HandleAsync(Guid sessionId, JsonElement evt, CancellationToken ct) => quota.TransactionAsync(async () =>
    {
        var session = await db.AiVoiceSessions.SingleOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session == null || session.State != "active" || session.HistoryDeleted || session.UserId == null) return false;
        var type = VoiceSessionConfiguration.String(evt, "type");
        // With sequential turns, residual VAD/noise during output is not evidence
        // that playback was interrupted. Only output completion reopens the turn.
        if (session.PlayingResponseId != null && type is "input_audio_buffer.speech_started" or "input_audio_buffer.speech_stopped") return true;
        var projected = VoicePublicEvent.Project(evt);
        var eventId = VoiceSessionConfiguration.String(evt, "event_id");
        if (projected != null && ValidId(eventId) && !await RememberAsync(sessionId, "state_event", eventId!, ct)) return false;
        if (projected != null)
        {
            if (projected.State != null) session.AgentState = projected.State;
            if (projected.Code != null) { session.CancelRequested = true; session.State = "closing"; session.CloseReason = "provider_error"; }
            session.LastActivityAtUtc = clock.GetUtcNow().UtcDateTime;
            session.Revision++;
        }
        if (type == "output_audio_buffer.started") session.PlayingResponseId = VoiceSessionConfiguration.String(evt, "response_id");
        if (type == "output_audio_buffer.cleared")
        {
            var responseId = session.PlayingResponseId;
            if (responseId != null)
            {
                await RememberAsync(sessionId, "interrupted", responseId, ct);
                var turns = await db.AiVoiceTurns.Where(x => x.SessionId == sessionId && x.ResponseId == responseId && x.Speaker == "assistant").ToListAsync(ct);
                foreach (var turn in turns) turn.Interrupted = true;
            }
            session.PlayingResponseId = null;
        }
        if (type == "output_audio_buffer.stopped") session.PlayingResponseId = null;
        if (type is "conversation.item.input_audio_transcription.completed" or "response.output_audio_transcript.done")
        {
            var itemId = VoiceSessionConfiguration.String(evt, "item_id");
            var text = VoiceSessionConfiguration.String(evt, "transcript");
            var responseId = VoiceSessionConfiguration.String(evt, "response_id");
            var speaker = type.StartsWith("conversation.", StringComparison.Ordinal) ? "user" : "assistant";
            if (ValidId(itemId) && text != null && !await db.AiVoiceTurns.AnyAsync(x => x.SessionId == sessionId && x.ItemId == itemId && x.Speaker == speaker, ct))
            {
                var interrupted = await db.AiVoiceReceipts.AnyAsync(x => x.SessionId == sessionId
                    && (x.Kind == "interrupted" && x.EventKey == responseId || x.Kind == "truncated" && x.EventKey == itemId), ct);
                db.AiVoiceTurns.Add(new() { SessionId = sessionId, ItemId = itemId!, ResponseId = responseId, Speaker = speaker,
                    Text = text[..Math.Min(text.Length, 12000)], Interrupted = interrupted, CreatedAtUtc = clock.GetUtcNow().UtcDateTime });
                session.Revision++;
            }
        }
        if (type == "conversation.item.truncated")
        {
            var itemId = VoiceSessionConfiguration.String(evt, "item_id");
            if (ValidId(itemId)) await RememberAsync(sessionId, "truncated", itemId!, ct);
            var turn = await db.AiVoiceTurns.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.ItemId == itemId && x.Speaker == "assistant", ct);
            if (turn != null) { turn.Interrupted = true; session.Revision++; }
        }
        if (type == "response.done" && evt.TryGetProperty("response", out var response))
        {
            var id = VoiceSessionConfiguration.String(response, "id");
            if (ValidId(id) && response.TryGetProperty("usage", out var usage) && await RememberAsync(sessionId, "usage", id!, ct))
            {
                var day = await db.AiVoiceUsageDays.SingleOrDefaultAsync(x => x.UserId == session.UserId && x.UtcDate == session.QuotaDayUtc, ct);
                if (day != null)
                {
                    day.InputAudioTokens += Tokens(usage, "input_token_details", "audio_tokens");
                    day.InputTextTokens += Tokens(usage, "input_token_details", "text_tokens");
                    day.OutputAudioTokens += Tokens(usage, "output_token_details", "audio_tokens");
                    day.OutputTextTokens += Tokens(usage, "output_token_details", "text_tokens");
                }
            }
            if (VoiceSessionConfiguration.String(response, "status") == "cancelled" && id != null)
            {
                await RememberAsync(sessionId, "interrupted", id, ct);
                var turns = await db.AiVoiceTurns.Where(x => x.SessionId == sessionId && x.ResponseId == id && x.Speaker == "assistant").ToListAsync(ct);
                foreach (var turn in turns) turn.Interrupted = true;
            }
        }
        return true;
    }, ct);

    private async Task<bool> RememberAsync(Guid sessionId, string kind, string key, CancellationToken ct)
    {
        if (!ValidId(key) || await db.AiVoiceReceipts.AnyAsync(x => x.SessionId == sessionId && x.Kind == kind && x.EventKey == key, ct)) return false;
        if (!db.AiVoiceReceipts.Local.Any(x => x.SessionId == sessionId && x.Kind == kind && x.EventKey == key))
            db.AiVoiceReceipts.Add(new() { SessionId = sessionId, Kind = kind, EventKey = key });
        return true;
    }
    private static bool ValidId(string? value) => value is { Length: > 0 and <= 200 };
    private static long Tokens(JsonElement usage, string details, string field) => usage.TryGetProperty(details, out var d)
        && d.TryGetProperty(field, out var n) && n.TryGetInt64(out var value) ? Math.Clamp(value, 0, 100_000_000) : 0;
}
