using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TCTEnglish.Services.AI.Voice;
using TCTEnglish.ViewModels.AI.Voice;
using TCTVocabulary.Controllers;

namespace TCTEnglish.Controllers;

[Authorize]
[ApiController]
[AutoValidateAntiforgeryToken]
[VoiceApiBoundary]
[Route("api/ai/voice")]
public sealed class AiVoiceApiController(AiVoiceTopicService topics, AiVoiceSessionService sessions,
    AiVoiceHistoryService history, AiVoiceEventStream events) : BaseController
{
    [HttpGet("topics")]
    public async Task<IActionResult> Topics(CancellationToken ct) => Ok(await topics.ListAsync(GetCurrentUserId(), ct));
    [HttpPost("topics")]
    public async Task<IActionResult> CreateTopic([FromBody] VoiceTopicInput input, CancellationToken ct)
        => Ok(new { id = await topics.SaveAsync(GetCurrentUserId(), null, input, ct) });
    [HttpPut("topics/{id:int}")]
    public async Task<IActionResult> UpdateTopic(int id, [FromBody] VoiceTopicInput input, CancellationToken ct)
        => Ok(new { id = await topics.SaveAsync(GetCurrentUserId(), id, input, ct) });
    [HttpDelete("topics/{id:int}")]
    public async Task<IActionResult> DeleteTopic(int id, CancellationToken ct)
    { await topics.DeleteAsync(GetCurrentUserId(), id, ct); return Ok(new { success = true }); }
    [HttpPost("sessions")]
    [RequestSizeLimit(300 * 1024)]
    public async Task<IActionResult> Start([FromBody] VoiceStartInput input, CancellationToken ct)
        => Ok(await sessions.StartAsync(GetCurrentUserId(), input, ct));
    [HttpPost("sessions/{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var closed = await sessions.CloseAsync(GetCurrentUserId(), id, "stopped", ct);
        return StatusCode(closed ? 200 : 202, new { closed, cleanupPending = !closed });
    }
    [HttpGet("sessions/{id:guid}/events")]
    public Task Events(Guid id, CancellationToken ct) => events.StreamAsync(HttpContext, GetCurrentUserId(), id, ct);
    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await history.ListAsync(GetCurrentUserId(), page, ct));
    [HttpGet("history/{id:guid}")]
    public async Task<IActionResult> HistoryDetail(Guid id, CancellationToken ct)
        => Ok(await history.DetailAsync(GetCurrentUserId(), id, ct));
    [HttpDelete("history/{id:guid}")]
    public async Task<IActionResult> DeleteHistory(Guid id, CancellationToken ct)
    { await history.DeleteAsync(GetCurrentUserId(), id, ct); return Ok(new { success = true }); }
}

public sealed class VoiceApiBoundaryAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!HttpMethods.IsGet(request.Method))
        {
            var origin = request.Headers.Origin.ToString();
            if (request.Headers["Sec-Fetch-Site"] == "cross-site" || origin.Length > 0 &&
                (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != request.Scheme || uri.Authority != request.Host.Value
                    || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0))
            { context.Result = new BadRequestObjectResult(new { errorCode = "invalid_origin" }); return; }
        }
        var executed = await next();
        if (executed.Exception is VoiceException voice)
        { executed.ExceptionHandled = true; executed.Result = new ObjectResult(new { errorCode = voice.Code }) { StatusCode = voice.Status }; }
        else if (executed.Exception is HttpRequestException or System.Net.WebSockets.WebSocketException or OperationCanceledException)
        { executed.ExceptionHandled = true; executed.Result = new ObjectResult(new { errorCode = "voice_unavailable" }) { StatusCode = 503 }; }
    }
}
