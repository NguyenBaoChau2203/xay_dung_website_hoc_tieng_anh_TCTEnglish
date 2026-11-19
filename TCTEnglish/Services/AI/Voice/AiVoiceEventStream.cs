using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using TCTEnglish.ViewModels.AI.Voice;

namespace TCTEnglish.Services.AI.Voice;

public sealed class AiVoiceEventStream(IServiceScopeFactory scopes)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task StreamAsync(HttpContext http, int userId, Guid id, CancellationToken ct)
    {
        using (var scope = scopes.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AiVoiceHistoryService>().OwnedAsync(userId, id, ct);
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        long revision = -1;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var scope = scopes.CreateScope();
                var history = scope.ServiceProvider.GetRequiredService<AiVoiceHistoryService>();
                var row = await history.OwnedAsync(userId, id, ct);
                if (revision != row.Revision)
                {
                    revision = row.Revision;
                    var detail = await history.DetailAsync(userId, id, ct);
                    await SendAsync(http, new { type = "app.session.snapshot", state = row.AgentState,
                        status = row.State, expiresAtUtc = row.ExpiresAtUtc, turns = detail.Turns, revision }, ct);
                }
                if (row.State is "closed" or "closing")
                {
                    await SendAsync(http, new { type = "app.session.closed", code = row.CloseReason }, ct);
                    break;
                }
                await http.Response.WriteAsync(": heartbeat\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
                await Task.Delay(700, ct);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or VoiceException) { }
        finally
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AiVoiceSessionService>().CloseAsync(userId, id, "event_stream_closed", CancellationToken.None);
            }
            catch { /* Worker uses the durable deadline and heartbeat after a DB/network failure. */ }
        }
    }
    private static async Task SendAsync(HttpContext http, object message, CancellationToken ct)
    {
        await http.Response.WriteAsync("data: " + JsonSerializer.Serialize(message, Json) + "\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }
}
