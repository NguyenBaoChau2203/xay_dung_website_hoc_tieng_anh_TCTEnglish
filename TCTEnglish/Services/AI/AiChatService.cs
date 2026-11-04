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
    private static readonly string DefaultSystemPrompt = AiSystemPrompts.PlatformGuide;

    private static readonly string[] BlockedContentPatterns =
    {
        "ignore previous instructions",
        "system prompt",
        "api key",
        "connection string",
        "password"
    };

    private readonly DbflashcardContext _context;
    private readonly IAiConversationService _conversationService;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiProviderClient _providerClient;
    private readonly IAiConversationExecutionGuard _conversationExecutionGuard;
    private readonly IAiDailyQuotaReservationService _quotaReservationService;
    private readonly AiOptions _options;
    private readonly ILogger<AiChatService> _logger;
    private readonly IAiTokenCounter _historyTokenCounter;

    public AiChatService(
        DbflashcardContext context,
        IAiConversationService conversationService,
        IAiContextBuilder contextBuilder,
        IAiProviderClient providerClient,
        IAiConversationExecutionGuard conversationExecutionGuard,
        IAiDailyQuotaReservationService quotaReservationService,
        IOptions<AiOptions> options,
        ILogger<AiChatService> logger,
        IAiTokenCounter? historyTokenCounter = null)
    {
        _context = context;
        _conversationService = conversationService;
        _contextBuilder = contextBuilder;
        _providerClient = providerClient;
        _conversationExecutionGuard = conversationExecutionGuard;
        _quotaReservationService = quotaReservationService;
        _options = options.Value;
        _logger = logger;
        _historyTokenCounter = historyTokenCounter ?? new SimpleAiTokenCounter();
    }

    public async Task<ChatReplyDto> SendAsync(int userId, Guid? conversationId, string message, CancellationToken ct)
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

        var normalizedRole = await GetNormalizedUserRoleAsync(userId, ct);
        var resolvedConversationId = conversationId.GetValueOrDefault();
        var hasConversationId = conversationId.HasValue && resolvedConversationId != Guid.Empty;

        AiConversation conversation;
        var isDraftConversation = false;
        if (hasConversationId)
        {
            conversation = await _context.AiConversations
                .FirstOrDefaultAsync(x => x.Id == resolvedConversationId && x.UserId == userId, ct)
                ?? throw new KeyNotFoundException($"Conversation '{resolvedConversationId}' was not found.");
        }
        else
        {
            conversation = await _conversationService.CreateConversationAsync(userId, normalizedMessage, ct);
            resolvedConversationId = conversation.Id;
            isDraftConversation = true;
        }

        using var conversationLease = AcquireConversationLease(resolvedConversationId);

        var userMessage = new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = resolvedConversationId,
            Role = AiMessageRole.User,
            Content = normalizedMessage,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.AiMessages.Add(userMessage);
        await _context.SaveChangesAsync(ct);

        AiQuotaReservation? reservation = null;
        var stopwatch = Stopwatch.StartNew();
        var replyCommitted = false;
        var providerReplyKnown = false;
        var knownActualTokens = 0;
        try
        {
            var historyNewestFirst = await LoadContextHistoryAsync(resolvedConversationId, userMessage.Id, normalizedMessage, ct);

            var contextResult = _contextBuilder.BuildContextMessages(
                DefaultSystemPrompt,
                normalizedMessage,
                historyNewestFirst,
                _options);

            if (_options.RequestTokenBudget > 0 && contextResult.PlannedTotalTokens > _options.RequestTokenBudget)
            {
                await SaveRequestLogAsync(new AiRequestLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ConversationId = resolvedConversationId,
                    IsSuccess = false,
                    ErrorCode = "request_token_budget_exceeded",
                    RequestedAtUtc = DateTime.UtcNow
                }, ct);

                if (isDraftConversation)
                {
                    await RollbackDraftConversationAsync(userId, resolvedConversationId, ct);
                }
                else
                {
                    await RollbackFailedUserMessageAsync(userMessage.Id, ct);
                }

                throw new ArgumentException("Yêu cầu vượt giới hạn token cho mỗi lượt chat.", nameof(message));
            }

            try
            {
                var questionLimit = normalizedRole == Roles.Standard ? 15 : 0;
                reservation = await _quotaReservationService.ReserveQuestionAsync(
                    userId,
                    contextResult.PlannedTotalTokens,
                    questionLimit,
                    _options.DailyTokenBudgetPerUser,
                    ct);
            }
            catch (AiRateLimitException)
            {
                await SaveRequestLogAsync(new AiRequestLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ConversationId = resolvedConversationId,
                    IsSuccess = false,
                    ErrorCode = "daily_quota_exceeded",
                    RequestedAtUtc = DateTime.UtcNow
                }, ct);

                if (isDraftConversation)
                {
                    await RollbackDraftConversationAsync(userId, resolvedConversationId, ct);
                }
                else
                {
                    await RollbackFailedUserMessageAsync(userMessage.Id, ct);
                }

                throw;
            }

            AiProviderReply aiReply;
            try
            {
                aiReply = await _providerClient.GenerateReplyAsync(userId, contextResult.Messages, ct);
            }
            catch (AiProviderException ex)
            {
                stopwatch.Stop();
                using var providerCleanupCts = ct.IsCancellationRequested
                    ? new CancellationTokenSource(TimeSpan.FromSeconds(5))
                    : null;
                var providerCleanupToken = providerCleanupCts?.Token ?? ct;

                if (reservation is not null)
                {
                    await _quotaReservationService.ReleaseAsync(reservation, providerCleanupToken);
                    reservation = null;
                }

                DiscardUncommittedReplyArtifacts();
                await SaveRequestLogAsync(new AiRequestLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ConversationId = resolvedConversationId,
                    IsSuccess = false,
                    ErrorCode = ex.ErrorCode,
                    LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                    RequestedAtUtc = DateTime.UtcNow
                }, providerCleanupToken);

                if (isDraftConversation)
                {
                    await RollbackDraftConversationAsync(userId, resolvedConversationId, providerCleanupToken);
                }
                else
                {
                    await RollbackFailedUserMessageAsync(userMessage.Id, providerCleanupToken);
                }

                _logger.LogWarning(
                    ex,
                    "AI provider request failed for conversation {conversationId}. ErrorCode {errorCode}",
                    resolvedConversationId,
                    ex.ErrorCode);

                throw;
            }

            stopwatch.Stop();
            // Provider returned a known reply — distinct from replyCommitted (DB durable).
            providerReplyKnown = true;
            knownActualTokens = aiReply.PromptTokens + aiReply.CompletionTokens;

            var assistantMessage = new AiMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = resolvedConversationId,
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
                ConversationId = resolvedConversationId,
                IsSuccess = true,
                PromptTokens = aiReply.PromptTokens,
                CompletionTokens = aiReply.CompletionTokens,
                TotalTokens = aiReply.TotalTokens,
                ModelName = aiReply.Model,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                RequestedAtUtc = DateTime.UtcNow
            });
            conversation.UpdatedAtUtc = DateTime.UtcNow;

            // Caller may already be cancelled after provider return; persist with a bounded token.
            using (var persistCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var persistToken = ct.IsCancellationRequested ? persistCts.Token : ct;
                await _context.SaveChangesAsync(persistToken);
            }

            replyCommitted = true;

            if (reservation is not null)
            {
                // Finalize independent of caller abort — reply is already durable.
                using var finalizeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await _quotaReservationService.SettleAsync(
                        reservation,
                        knownActualTokens,
                        finalizeCts.Token);
                }
                catch (Exception settleEx)
                {
                    // Do not turn a committed reply into HTTP 500 (JS retries same intent).
                    try
                    {
                        await _quotaReservationService.MarkKnownUsagePendingAsync(
                            reservation,
                            knownActualTokens,
                            finalizeCts.Token);
                    }
                    catch (Exception markEx)
                    {
                        _logger.LogWarning(
                            markEx,
                            "Could not mark known usage pending after settle failure for conversation {conversationId}.",
                            resolvedConversationId);
                    }

                    _logger.LogWarning(
                        settleEx,
                        "Quota settle failed after committed AI reply for conversation {conversationId}; leaving pending for reconciliation.",
                        resolvedConversationId);
                }

                reservation = null;
            }

            var requestId = string.IsNullOrWhiteSpace(aiReply.RequestId)
                ? Guid.NewGuid().ToString("N")
                : aiReply.RequestId;

            _logger.LogInformation(
                "AI chat reply completed for conversation {conversationId}. LatencyMs {latencyMs}. Model {model}. PromptTokens {promptTokens}. CompletionTokens {completionTokens}. TotalTokens {totalTokens}",
                resolvedConversationId,
                stopwatch.ElapsedMilliseconds,
                aiReply.Model,
                aiReply.PromptTokens,
                aiReply.CompletionTokens,
                aiReply.TotalTokens);

            // If the caller aborted after a durable known reply, still surface the reply (no retry storm).
            ct.ThrowIfCancellationRequested();

            return new ChatReplyDto(
                aiReply.Text,
                resolvedConversationId,
                conversation.Title,
                new ChatUsageDto(aiReply.PromptTokens, aiReply.CompletionTokens, aiReply.TotalTokens, aiReply.Model),
                new ChatMetadataDto(requestId, (int)stopwatch.ElapsedMilliseconds));
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var cleanupToken = cleanupCts.Token;

            if (providerReplyKnown && !replyCommitted)
            {
                // Persist known reply with independent token, then settle — never Release known usage.
                try
                {
                    await _context.SaveChangesAsync(cleanupToken);
                    replyCommitted = true;
                }
                catch (Exception persistEx)
                {
                    DiscardUncommittedReplyArtifacts();
                    _logger.LogWarning(
                        persistEx,
                        "Failed to persist known AI reply for conversation {conversationId} after cancel; discarding uncommitted artifacts.",
                        resolvedConversationId);

                    if (reservation is not null)
                    {
                        try
                        {
                            await _quotaReservationService.MarkKnownUsagePendingAsync(
                                reservation,
                                knownActualTokens,
                                cleanupToken);
                        }
                        catch (Exception markEx)
                        {
                            _logger.LogWarning(
                                markEx,
                                "Could not mark known usage pending for reservation {requestId}.",
                                reservation.RequestId);
                        }

                        reservation = null;
                    }
                }
            }

            if (reservation is not null)
            {
                if (replyCommitted || providerReplyKnown)
                {
                    // Known provider outcome — never Release a charged turn.
                    try
                    {
                        await _quotaReservationService.SettleAsync(
                            reservation,
                            replyCommitted ? knownActualTokens : 0,
                            cleanupToken);
                    }
                    catch (Exception settleEx)
                    {
                        try
                        {
                            await _quotaReservationService.MarkKnownUsagePendingAsync(
                                reservation,
                                knownActualTokens,
                                cleanupToken);
                        }
                        catch (Exception markEx)
                        {
                            _logger.LogWarning(
                                markEx,
                                "Post-cancel settle/mark failed for reservation {requestId}; leaving pending.",
                                reservation.RequestId);
                            _logger.LogWarning(settleEx, "Original settle failure for reservation {requestId}.", reservation.RequestId);
                        }
                    }
                }
                else
                {
                    await _quotaReservationService.ReleaseAsync(reservation, cleanupToken);
                }

                reservation = null;
            }

            if (!replyCommitted && !providerReplyKnown)
            {
                DiscardUncommittedReplyArtifacts();
                await SaveRequestLogAsync(new AiRequestLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ConversationId = resolvedConversationId,
                    IsSuccess = false,
                    ErrorCode = "cancelled",
                    LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                    RequestedAtUtc = DateTime.UtcNow
                }, cleanupToken);

                if (isDraftConversation)
                {
                    await RollbackDraftConversationAsync(userId, resolvedConversationId, cleanupToken);
                }
                else
                {
                    await RollbackFailedUserMessageAsync(userMessage.Id, cleanupToken);
                }
            }
            else if (!replyCommitted && providerReplyKnown)
            {
                // Persist failed; drop uncommitted assistant but keep user prompt only if we somehow
                // still have nothing durable — discard artifacts then roll back the user turn.
                DiscardUncommittedReplyArtifacts();
                if (isDraftConversation)
                {
                    await RollbackDraftConversationAsync(userId, resolvedConversationId, cleanupToken);
                }
                else
                {
                    await RollbackFailedUserMessageAsync(userMessage.Id, cleanupToken);
                }
            }

            _logger.LogInformation(
                "AI chat request canceled for conversation {conversationId} after {latencyMs}ms (replyCommitted={replyCommitted}, providerReplyKnown={providerReplyKnown})",
                resolvedConversationId,
                stopwatch.ElapsedMilliseconds,
                replyCommitted,
                providerReplyKnown);

            throw;
        }
        catch (Exception ex) when (providerReplyKnown && !replyCommitted)
        {
            // A storage failure after a known provider reply must not release usage or
            // leave Added assistant/log entities for a later unrelated SaveChanges.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            DiscardUncommittedReplyArtifacts();
            if (reservation is not null)
            {
                try { await _quotaReservationService.MarkKnownUsagePendingAsync(reservation, knownActualTokens, cleanup.Token); }
                catch (Exception markError) { _logger.LogWarning("Unable to preserve known usage ({Category}).", markError.GetType().Name); }
            }
            if (isDraftConversation) await RollbackDraftConversationAsync(userId, resolvedConversationId, cleanup.Token);
            else await RollbackFailedUserMessageAsync(userMessage.Id, cleanup.Token);
            throw new AiProviderException("AI đã xử lý yêu cầu nhưng chưa lưu được kết quả. Vui lòng tải lại lịch sử trước khi gửi một yêu cầu mới.",
                "reply_persistence_failed", false, ex);
        }
    }

    private async Task<List<AiMessage>> LoadContextHistoryAsync(Guid conversationId, Guid currentMessageId, string message, CancellationToken ct)
    {
        var inputBudget = Math.Min(_options.ModelContextLimit, _options.RequestTokenBudget > 0 ? _options.RequestTokenBudget : _options.ModelContextLimit);
        var remaining = Math.Max(0, inputBudget - _options.MaxOutputTokens
            - _historyTokenCounter.CountTokens(DefaultSystemPrompt) - _historyTokenCounter.CountTokens(message));
        var history = new List<AiMessage>(); DateTime? beforeTime = null; Guid beforeId = default;
        while (remaining > 0)
        {
            var query = _context.AiMessages.AsNoTracking().Where(m => m.ConversationId == conversationId && m.Id != currentMessageId);
            if (beforeTime.HasValue) query = query.Where(m => m.CreatedAtUtc < beforeTime.Value || (m.CreatedAtUtc == beforeTime.Value && m.Id.CompareTo(beforeId) < 0));
            var batch = await query.OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id).Take(50)
                .Select(m => new AiMessage { Id = m.Id, CreatedAtUtc = m.CreatedAtUtc, Role = m.Role, Content = m.Content }).ToListAsync(ct);
            if (batch.Count == 0) break;
            foreach (var item in batch)
            {
                // A small per-message envelope allowance also bounds empty/tiny messages.
                var cost = Math.Max(1, _historyTokenCounter.CountTokens(item.Content)) + 4;
                if (cost > remaining) { history.Reverse(); return history; }
                history.Add(item); remaining -= cost;
            }
            beforeTime = batch[^1].CreatedAtUtc; beforeId = batch[^1].Id;
            if (batch.Count < 50) break;
        }
        history.Reverse(); return history;
    }

    private void DiscardUncommittedReplyArtifacts()
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AiConversation && entry.State == EntityState.Modified)
            { entry.State = EntityState.Unchanged; continue; }
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            if (entry.Entity is AiMessage message && message.Role == AiMessageRole.Assistant)
            {
                entry.State = EntityState.Detached;
                continue;
            }

            if (entry.Entity is AiRequestLog log && log.IsSuccess)
            {
                entry.State = EntityState.Detached;
            }
        }
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

    private async Task<string> GetNormalizedUserRoleAsync(int userId, CancellationToken ct)
    {
        var role = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);

        return Roles.Normalize(role);
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

    private async Task RollbackDraftConversationAsync(int userId, Guid conversationId, CancellationToken ct)
    {
        try
        {
            var draftConversation = await _context.AiConversations
                .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId, ct);

            if (draftConversation is null)
            {
                return;
            }

            var draftMessages = await _context.AiMessages
                .Where(x => x.ConversationId == conversationId)
                .ToListAsync(ct);

            if (draftMessages.Count > 0)
            {
                _context.AiMessages.RemoveRange(draftMessages);
            }

            var draftRequestLogs = await _context.AiRequestLogs
                .Where(x => x.ConversationId == conversationId)
                .ToListAsync(ct);

            if (draftRequestLogs.Count > 0)
            {
                _context.AiRequestLogs.RemoveRange(draftRequestLogs);
            }

            _context.AiConversations.Remove(draftConversation);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to rollback draft AI conversation {conversationId} for user {userId} after provider failure.",
                conversationId,
                userId);
        }
    }

    private async Task RollbackFailedUserMessageAsync(Guid userMessageId, CancellationToken ct)
    {
        try
        {
            var failedMessage = await _context.AiMessages
                .FirstOrDefaultAsync(x => x.Id == userMessageId, ct);

            if (failedMessage is null)
            {
                return;
            }

            _context.AiMessages.Remove(failedMessage);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to rollback failed user AI prompt message {messageId} after provider failure.",
                userMessageId);
        }
    }

    public async Task<AiUsageInfo> GetDailyUsageAsync(int userId, CancellationToken ct)
    {
        var role = await GetNormalizedUserRoleAsync(userId, ct);
        if (role == Roles.Admin || role == Roles.Premium)
        {
            return new AiUsageInfo
            {
                IsUnlimited = true,
                DailyLimit = 0,
                RequestedToday = 0
            };
        }

        var day = DateTime.UtcNow.Date;
        var reservedUsage = await _context.AiUserDailyQuotas.AsNoTracking()
            .Where(q => q.UserId == userId && q.DayUtc == day)
            .Select(q => (int?)(q.SettledQuestions + q.PendingQuestions)).FirstOrDefaultAsync(ct);
        var successfulRequestsToday = reservedUsage ?? await _context.AiRequestLogs
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId
                && x.IsSuccess
                && x.RequestedAtUtc >= DateTime.UtcNow.Date, ct);

        return new AiUsageInfo
        {
            IsUnlimited = false,
            DailyLimit = 15,
            RequestedToday = successfulRequestsToday
        };
    }
}
