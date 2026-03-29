using System;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.ViewModels.AI;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI;

public sealed class AiChatService : IAiChatService
{
    private const string DefaultSystemPrompt = "Bạn là trợ lý học tiếng Anh cho người Việt.";

    private static readonly string[] BlockedContentPatterns =
    {
        "ignore previous instructions",
        "system prompt",
        "api key",
        "connection string",
        "password"
    };

    private readonly DbflashcardContext _context;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiProviderClient _providerClient;
    private readonly IAiConversationExecutionGuard _conversationExecutionGuard;
    private readonly AiOptions _options;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        DbflashcardContext context,
        IAiContextBuilder contextBuilder,
        IAiProviderClient providerClient,
        IAiConversationExecutionGuard conversationExecutionGuard,
        IOptions<AiOptions> options,
        ILogger<AiChatService> logger)
    {
        _context = context;
        _contextBuilder = contextBuilder;
        _providerClient = providerClient;
        _conversationExecutionGuard = conversationExecutionGuard;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatReplyDto> SendAsync(int userId, Guid conversationId, string message, CancellationToken ct)
    {
        var normalizedMessage = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            throw new ArgumentException("Message is required.", nameof(message));
        }

        if (normalizedMessage.Length > _options.MaxInputChars)
        {
            throw new ArgumentException($"Message exceeds {_options.MaxInputChars} characters.", nameof(message));
        }

        if (ContainsBlockedContent(normalizedMessage))
        {
            throw new ArgumentException("Nội dung yêu cầu không được hỗ trợ.", nameof(message));
        }

        using var conversationLease = AcquireConversationLease(conversationId);

        var conversation = await _context.AiConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' was not found.");

        var userMessage = new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = AiMessageRole.User,
            Content = normalizedMessage,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.AiMessages.Add(userMessage);
        await _context.SaveChangesAsync(ct);

        var history = await _context.AiMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.Id != userMessage.Id)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var contextResult = _contextBuilder.BuildContextMessages(
            DefaultSystemPrompt,
            normalizedMessage,
            history,
            _options);

        if (_options.RequestTokenBudget > 0 && contextResult.PlannedTotalTokens > _options.RequestTokenBudget)
        {
            await SaveRequestLogAsync(new AiRequestLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConversationId = conversationId,
                IsSuccess = false,
                ErrorCode = "request_token_budget_exceeded",
                RequestedAtUtc = DateTime.UtcNow
            }, ct);

            throw new ArgumentException("Yêu cầu vượt giới hạn token cho mỗi lượt chat.", nameof(message));
        }

        if (_options.DailyTokenBudgetPerUser > 0)
        {
            var usedTokensToday = await _context.AiMessages
                .AsNoTracking()
                .Where(x => x.Role == AiMessageRole.Assistant
                    && x.CreatedAtUtc >= DateTime.UtcNow.Date
                    && x.Conversation.UserId == userId)
                .SumAsync(x => (x.PromptTokens ?? 0) + (x.CompletionTokens ?? 0), ct);

            if (usedTokensToday + contextResult.PlannedTotalTokens > _options.DailyTokenBudgetPerUser)
            {
                await SaveRequestLogAsync(new AiRequestLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ConversationId = conversationId,
                    IsSuccess = false,
                    ErrorCode = "daily_token_budget_exceeded",
                    RequestedAtUtc = DateTime.UtcNow
                }, ct);

                throw new AiRateLimitException("Bạn đã đạt giới hạn sử dụng AI trong ngày.", "daily_token_budget_exceeded");
            }
        }

        var stopwatch = Stopwatch.StartNew();
        AiProviderReply aiReply;

        try
        {
            aiReply = await _providerClient.GenerateReplyAsync(contextResult.Messages, ct);
        }
        catch (AiProviderException ex)
        {
            stopwatch.Stop();
            await SaveRequestLogAsync(new AiRequestLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConversationId = conversationId,
                IsSuccess = false,
                ErrorCode = ex.ErrorCode,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                RequestedAtUtc = DateTime.UtcNow
            }, ct);

            _logger.LogWarning(
                ex,
                "AI provider request failed for conversation {conversationId}. ErrorCode {errorCode}",
                conversationId,
                ex.ErrorCode);

            throw;
        }

        stopwatch.Stop();

        var assistantMessage = new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = AiMessageRole.Assistant,
            Content = aiReply.Text,
            PromptTokens = aiReply.PromptTokens,
            CompletionTokens = aiReply.CompletionTokens,
            ModelName = aiReply.Model,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.AiMessages.Add(assistantMessage);
        _context.AiRequestLogs.Add(new AiRequestLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConversationId = conversationId,
            IsSuccess = true,
            PromptTokens = aiReply.PromptTokens,
            CompletionTokens = aiReply.CompletionTokens,
            TotalTokens = aiReply.TotalTokens,
            ModelName = aiReply.Model,
            LatencyMs = (int)stopwatch.ElapsedMilliseconds,
            RequestedAtUtc = DateTime.UtcNow
        });
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var requestId = string.IsNullOrWhiteSpace(aiReply.RequestId)
            ? Guid.NewGuid().ToString("N")
            : aiReply.RequestId;

        _logger.LogInformation(
            "AI chat reply completed for conversation {conversationId}. LatencyMs {latencyMs}. Model {model}. PromptTokens {promptTokens}. CompletionTokens {completionTokens}. TotalTokens {totalTokens}",
            conversationId,
            stopwatch.ElapsedMilliseconds,
            aiReply.Model,
            aiReply.PromptTokens,
            aiReply.CompletionTokens,
            aiReply.TotalTokens);

        return new ChatReplyDto(
            aiReply.Text,
            conversationId,
            new ChatUsageDto(aiReply.PromptTokens, aiReply.CompletionTokens, aiReply.TotalTokens, aiReply.Model),
            new ChatMetadataDto(requestId, (int)stopwatch.ElapsedMilliseconds));
    }

    private IDisposable AcquireConversationLease(Guid conversationId)
    {
        if (_conversationExecutionGuard.TryAcquire(conversationId, out var lease) && lease is not null)
        {
            return lease;
        }

        throw new AiConcurrentRequestException(
            "Cuộc hội thoại này đang có một yêu cầu AI khác đang xử lý. Vui lòng chờ phản hồi hiện tại hoàn tất.");
    }

    private static bool ContainsBlockedContent(string message)
    {
        foreach (var pattern in BlockedContentPatterns)
        {
            if (message.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task SaveRequestLogAsync(AiRequestLog requestLog, CancellationToken ct)
    {
        try
        {
            _context.AiRequestLogs.Add(requestLog);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to persist AI request observability log. ConversationId {conversationId}. ErrorCode {errorCode}",
                requestLog.ConversationId,
                requestLog.ErrorCode);
        }
    }
}
