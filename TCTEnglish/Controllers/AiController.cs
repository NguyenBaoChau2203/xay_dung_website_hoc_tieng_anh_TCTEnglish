using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.ViewModels.AI;
using TCTVocabulary.Controllers;

namespace TCTEnglish.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
[Route("AI")]
public sealed class AiController : BaseController
{
    private readonly IAiConversationService _conversationService;
    private readonly IAiChatService _chatService;
    private readonly IAiObservabilityService _observabilityService;
    private readonly IAiRequestRateLimiter _rateLimiter;
    private readonly ILogger<AiController> _logger;

    public AiController(
        IAiConversationService conversationService,
        IAiChatService chatService,
        IAiObservabilityService observabilityService,
        IAiRequestRateLimiter rateLimiter,
        ILogger<AiController> logger)
    {
        _conversationService = conversationService;
        _chatService = chatService;
        _observabilityService = observabilityService;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    [HttpGet("Chat")]
    public async Task<IActionResult> Chat(Guid? conversationId, bool embed = false, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var currentConversationId = conversationId ?? (await _conversationService.CreateConversationAsync(userId, null, ct)).Id;

        IReadOnlyList<AiMessage> messages;
        try
        {
            messages = await _conversationService.GetMessagesByConversationAsync(userId, currentConversationId, ct);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var viewModel = new AiChatPageViewModel
        {
            ConversationId = currentConversationId,
            Messages = messages.Select(MapToViewModel).ToList(),
            IsEmbedded = embed
        };

        if (embed)
        {
            return View("ChatEmbed", viewModel);
        }

        return View(viewModel);
    }

    [HttpPost("Chat/Send")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send([FromBody] SendChatRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        if (!_rateLimiter.TryConsume(userId, HttpContext.Connection.RemoteIpAddress?.ToString(), out var retryAfterSeconds))
        {
            Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "Bạn đang gửi quá nhanh. Vui lòng thử lại sau.",
                retryAfterSeconds
            });
        }

        try
        {
            var result = await _chatService.SendAsync(userId, request.ConversationId, request.Message, ct);
            return Json(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (AiRateLimitException ex)
        {
            _logger.LogInformation(
                "AI limit reached for user {userId}. ErrorCode {errorCode}",
                userId,
                ex.ErrorCode);

            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = ex.Message });
        }
        catch (AiConcurrentRequestException ex)
        {
            return Conflict(new
            {
                error = ex.Message,
                errorCode = ex.ErrorCode
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (AiProviderException ex)
        {
            _logger.LogWarning(ex, "AI provider failed for conversation {conversationId}", request.ConversationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Xin lỗi, hệ thống AI đang bận. Vui lòng thử lại."
            });
        }
    }

    [HttpGet("Observability")]
    public async Task<IActionResult> Observability([FromQuery] int days = 7, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var snapshot = await _observabilityService.GetUserSnapshotAsync(userId, days, ct);
        return Json(snapshot);
    }

    private static AiChatMessageViewModel MapToViewModel(AiMessage message)
    {
        return new AiChatMessageViewModel
        {
            Id = message.Id,
            Role = message.Role switch
            {
                AiMessageRole.System => "system",
                AiMessageRole.Assistant => "assistant",
                _ => "user"
            },
            Content = message.Content,
            CreatedAtUtc = message.CreatedAtUtc
        };
    }
}
