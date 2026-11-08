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
        IReadOnlyList<AiMessage> messages = [];
        IReadOnlyList<AiConversationSummaryDto> conversations = [];
        var hasMoreConversations = false;
        var hasMoreMessages = false;
        DateTime? oldestConversationUpdatedAtUtc = null;
        Guid? oldestConversationId = null;
        DateTime? oldestMessageCreatedAtUtc = null;
        Guid? oldestMessageId = null;

        if (!embed)
        {
            var conversationPage = await _conversationService.GetConversationsPageAsync(
                userId,
                AiConversationService.DefaultConversationPageSize,
                null,
                null,
                ct);
            conversations = conversationPage.Items;
            hasMoreConversations = conversationPage.HasMore;
            if (conversations.Count > 0)
            {
                var oldestConversation = conversations[^1];
                oldestConversationUpdatedAtUtc = oldestConversation.UpdatedAtUtc;
                oldestConversationId = oldestConversation.Id;
            }
        }

        if (conversationId.HasValue)
        {
            try
            {
                var messagePage = await _conversationService.GetMessagesPageAsync(
                    userId,
                    conversationId.Value,
                    AiConversationService.DefaultMessagePageSize,
                    null,
                    null,
                    ct);
                messages = messagePage.Items;
                hasMoreMessages = messagePage.HasMore;
                if (messages.Count > 0)
                {
                    var oldestMessage = messages[0];
                    oldestMessageCreatedAtUtc = oldestMessage.CreatedAtUtc;
                    oldestMessageId = oldestMessage.Id;
                }
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        var activeConversation = conversationId.HasValue
            ? conversations.FirstOrDefault(x => x.Id == conversationId.Value)
            : null;
        var currentConversationTitle = activeConversation?.Title
            ?? (conversationId.HasValue ? "Đoạn chat hiện tại" : "Bản nháp mới");
        var currentConversationStatus = conversationId.HasValue
            ? "Đang mở từ lịch sử hội thoại."
            : "Sẽ tạo sau khi bạn gửi tin nhắn đầu tiên.";

        var isStandardPlan = !User.IsInRole(TCTVocabulary.Models.Roles.Premium) && !User.IsInRole(TCTVocabulary.Models.Roles.Admin);

        var viewModel = new AiChatPageViewModel
        {
            ConversationId = conversationId,
            Messages = messages.Select(MapToViewModel).ToList(),
            Conversations = conversations.Select(MapToConversationSummaryViewModel).ToList(),
            CurrentConversationTitle = currentConversationTitle,
            CurrentConversationStatus = currentConversationStatus,
            IsEmbedded = embed,
            IsStandardPlan = isStandardPlan,
            HasMoreConversations = hasMoreConversations,
            HasMoreMessages = hasMoreMessages,
            OldestConversationUpdatedAtUtc = oldestConversationUpdatedAtUtc,
            OldestConversationId = oldestConversationId,
            OldestMessageCreatedAtUtc = oldestMessageCreatedAtUtc,
            OldestMessageId = oldestMessageId
        };

        if (embed)
        {
            return View("ChatEmbed", viewModel);
        }

        return View(viewModel);
    }

    [HttpGet("Chat/Conversations")]
    public async Task<IActionResult> ConversationsPage(
        [FromQuery] int pageSize = AiConversationService.DefaultConversationPageSize,
        [FromQuery] DateTime? beforeUpdatedAtUtc = null,
        [FromQuery] Guid? beforeId = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var page = await _conversationService.GetConversationsPageAsync(
            userId,
            pageSize,
            beforeUpdatedAtUtc,
            beforeId,
            ct);

        return Json(new
        {
            items = page.Items.Select(MapToConversationSummaryViewModel),
            hasMore = page.HasMore
        });
    }

    [HttpGet("Chat/Messages")]
    public async Task<IActionResult> MessagesPage(
        [FromQuery] Guid conversationId,
        [FromQuery] int pageSize = AiConversationService.DefaultMessagePageSize,
        [FromQuery] DateTime? beforeCreatedAtUtc = null,
        [FromQuery] Guid? beforeId = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        try
        {
            var page = await _conversationService.GetMessagesPageAsync(
                userId,
                conversationId,
                pageSize,
                beforeCreatedAtUtc,
                beforeId,
                ct);

            return Json(new
            {
                items = page.Items.Select(MapToViewModel),
                hasMore = page.HasMore
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
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
                error = AiProviderErrorMessages.For(ex.ErrorCode)
            });
        }
    }

    [HttpPost("Chat/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromForm] Guid conversationId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var success = await _conversationService.DeleteConversationAsync(userId, conversationId, ct);

        if (!success)
        {
            return NotFound();
        }

        return Ok(new { success = true });
    }

    [HttpGet("Observability")]
    public async Task<IActionResult> Observability([FromQuery] int days = 7, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var snapshot = await _observabilityService.GetUserSnapshotAsync(userId, days, ct);
        return Json(snapshot);
    }

    [HttpGet("Chat/Usage")]
    public async Task<IActionResult> Usage(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var usage = await _chatService.GetDailyUsageAsync(userId, ct);
        return Json(usage);
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
            CreatedAtUtc = message.CreatedAtUtc,
            Grounding = ChatGroundingMapper.From(message.Grounding)
        };
    }

    private static AiConversationSummaryViewModel MapToConversationSummaryViewModel(AiConversationSummaryDto conversation)
    {
        return new AiConversationSummaryViewModel
        {
            Id = conversation.Id,
            Title = conversation.Title,
            UpdatedAtUtc = conversation.UpdatedAtUtc
        };
    }
}
