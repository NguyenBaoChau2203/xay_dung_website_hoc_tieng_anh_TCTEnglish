using System;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.AI.Rag;
using TCTEnglish.ViewModels.AI;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI;

public sealed class AiChatService : IAiChatService
{
    private static readonly string DefaultSystemPrompt = AiSystemPrompts.PlatformGuide;

    /// <summary>
    /// After the assistant row and quota settlement are durable, display projection is a
    /// bounded reauthorization of at most 12 stored sources. This matches the existing
    /// five-second post-acceptance budget used for persistence and quota finalization.
    /// Expiry returns the unverified notice and leaves the saved turn for a later reload.
    /// </summary>
    public static readonly TimeSpan AcceptedTurnProjectionBudget = TimeSpan.FromSeconds(5);

    private readonly DbflashcardContext _context;
    private readonly IAiConversationService _conversationService;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiChatAnswerService _answerService;
    private readonly IAiConversationExecutionGuard _conversationExecutionGuard;
    private readonly IAiDailyQuotaReservationService _quotaReservationService;
    private readonly AiOptions _options;
    private readonly ILogger<AiChatService> _logger;
    private readonly IAiTokenCounter _historyTokenCounter;
    private readonly IGroundedChatPipeline? _groundedPipeline;
    private readonly IGroundingCitationService? _grounding;

    public AiChatService(
        DbflashcardContext context,
        IAiConversationService conversationService,
        IAiContextBuilder contextBuilder,
        IAiChatAnswerService answerService,
        IAiConversationExecutionGuard conversationExecutionGuard,
        IAiDailyQuotaReservationService quotaReservationService,
        IOptions<AiOptions> options,
        ILogger<AiChatService> logger,
        IAiTokenCounter? historyTokenCounter = null,
        IGroundedChatPipeline? groundedPipeline = null,
        IGroundingCitationService? grounding = null)
    {
        _context = context;
        _conversationService = conversationService;
        _contextBuilder = contextBuilder;
        _answerService = answerService;
        _conversationExecutionGuard = conversationExecutionGuard;
        _quotaReservationService = quotaReservationService;
        _options = options.Value;
        _logger = logger;
        _historyTokenCounter = historyTokenCounter ?? new SimpleAiTokenCounter();
        _groundedPipeline = groundedPipeline;
        _grounding = grounding;
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

        if (ChatDisclosurePolicy.IsPromptOrCredentialExtraction(normalizedMessage))
        {
            throw new ArgumentException(ChatDisclosurePolicy.RefusalText, nameof(message));
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
        AiTurnStageJournal? stageJournal = null;
        var stopwatch = Stopwatch.StartNew();
        var replyCommitted = false;
        var providerReplyKnown = false;
        var knownActualTokens = 0;
        try
        {
            var historyNewestFirst = await LoadContextHistoryAsync(userId, resolvedConversationId, userMessage.Id, normalizedMessage, ct);

            var contextResult = _contextBuilder.BuildContextMessages(
                DefaultSystemPrompt,
                normalizedMessage,
                historyNewestFirst,
                _options);

            var historyTurns = historyNewestFirst
                .OrderBy(message => message.CreatedAtUtc)
                .ThenBy(message => message.Id)
                .Select(message => new GroundedHistoryTurn(MapHistoryRole(message.Role), message.Content ?? string.Empty))
                .ToList();
            ChatTurnBudgetDecision? groundedBudget = null;
            var reserveTokens = contextResult.PlannedTotalTokens;
            var groundedBudgetExceeded = false;
            if (_groundedPipeline is not null && GroundedAnswerComposer.ShouldRetrieve(normalizedMessage, historyTurns))
            {
                var generationAttempts = Math.Clamp(_options.EffectiveRetryMaxAttempts, 0, 3) + 1;
                var embeddingAttempts = new EmbeddingOptions().EffectiveRetryMaxAttempts + 1;
                if (!ChatTurnBudget.TryCreate(
                        GroundedAnswerComposer.RetrievalQuery(normalizedMessage, historyTurns),
                        contextResult.PlannedTotalTokens,
                        _options.MaxOutputTokens,
                        _options.RequestTokenBudget,
                        out var decision,
                        generationAttempts,
                        embeddingAttempts,
                        _options.Model,
                        _options.ReasoningEffort))
                {
                    groundedBudgetExceeded = true;
                }
                else
                {
                    groundedBudget = decision;
                    reserveTokens = decision.ReservedTokens;
                }
            }

            if (groundedBudgetExceeded || (_options.RequestTokenBudget > 0 && contextResult.PlannedTotalTokens > _options.RequestTokenBudget))
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
                    reserveTokens,
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
            AiChatAnswer? acceptedAnswer = null;
            long answerMs = 0;
            long projectMs = 0;
            try
            {
                AiChatAnswer answer;
                var answerWatch = Stopwatch.StartNew();
                if (_groundedPipeline is null)
                {
                    answer = await _answerService.AnswerAsync(userId, normalizedMessage, contextResult.Messages, ct);
                }
                else
                {
                    stageJournal = new AiTurnStageJournal(_quotaReservationService, reservation);
                    var execution = await _groundedPipeline.ExecuteAsync(new GroundedTurnInput(
                        userId,
                        normalizedMessage,
                        contextResult.Messages,
                        historyTurns,
                        groundedBudget,
                        stageJournal), ct);
                    answer = execution.Answer;
                }

                answerWatch.Stop();
                answerMs = answerWatch.ElapsedMilliseconds;

                acceptedAnswer = answer;
                aiReply = new AiProviderReply(
                    answer.Text,
                    answer.PromptTokens,
                    answer.CompletionTokens,
                    answer.TotalTokens,
                    answer.Model,
                    answer.RequestId);
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
                    if (stageJournal is { HasCharge: true })
                    {
                        await _quotaReservationService.SettleAsync(reservation, stageJournal.Charge, providerCleanupToken);
                    }
                    else
                    {
                        await _quotaReservationService.ReleaseAsync(reservation, providerCleanupToken);
                    }

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
                    "AI provider request failed for conversation {conversationId}. ErrorCode {errorCode}",
                    resolvedConversationId,
                    AiChatDiagnostics.SafeErrorCode(ex.ErrorCode));

                throw;
            }

            stopwatch.Stop();
            // Provider returned a known reply — distinct from replyCommitted (DB durable).
            providerReplyKnown = true;
            knownActualTokens = aiReply.PromptTokens + aiReply.CompletionTokens;
            string? groundingMetadata = null;
            if (_grounding is not null && acceptedAnswer is not null)
            {
                groundingMetadata = _grounding.Capture(acceptedAnswer);
            }

            var assistantMessage = new AiMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = resolvedConversationId,
                Role = AiMessageRole.Assistant,
                Content = aiReply.Text,
                PromptTokens = aiReply.PromptTokens,
                CompletionTokens = aiReply.CompletionTokens,
                ModelName = aiReply.Model,
                GroundingMetadata = groundingMetadata,
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

            // A durable reply stays returned after the caller aborts. Projection below must not
            // turn that acceptance into a failed send, a second assistant, or another settlement.
            ChatGroundingDto? grounding = null;
            var replyText = aiReply.Text;
            if (_grounding is not null)
            {
                try
                {
                    var projected = new AiMessage
                    {
                        Id = assistantMessage.Id,
                        Role = AiMessageRole.Assistant,
                        Content = assistantMessage.Content,
                        GroundingMetadata = assistantMessage.GroundingMetadata,
                        CreatedAtUtc = assistantMessage.CreatedAtUtc
                    };
                    // Independent of the caller token: an already-aborted request must not skip
                    // reauthorization, and a stalled read must not hold the conversation lease.
                    using var projectionCts = new CancellationTokenSource(AcceptedTurnProjectionBudget);
                    var projectWatch = Stopwatch.StartNew();
                    await _grounding.ProjectAsync(userId, [projected], GroundingProjectionMode.Display, projectionCts.Token);
                    projectWatch.Stop();
                    projectMs = projectWatch.ElapsedMilliseconds;
                    grounding = ChatGroundingMapper.From(projected.Grounding);
                    replyText = projected.Content;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Display projection failed after the AI reply was committed for conversation {conversationId}. ErrorCode {errorCode}",
                        resolvedConversationId,
                        AiChatDiagnostics.SafeErrorCode(ex is OperationCanceledException ? "cancelled" : "projection_failed"));
                    replyText = GroundingNotices.UnverifiedContext;
                    grounding = new ChatGroundingDto(
                        0,
                        null,
                        null,
                        null,
                        "unverified",
                        GroundingNotices.UnverifiedContext,
                        [],
                        LearnerChatState.Label(null, "unverified"));
                }
            }

            _logger.LogInformation(
                "{ChatDiagnostic}",
                AiChatDiagnostics.FormatCompletion(
                    resolvedConversationId,
                    requestId,
                    acceptedAnswer?.Outcome,
                    acceptedAnswer?.RetrievalMode,
                    acceptedAnswer?.Evidence,
                    answerMs,
                    projectMs,
                    stopwatch.ElapsedMilliseconds,
                    aiReply.PromptTokens,
                    aiReply.CompletionTokens,
                    aiReply.TotalTokens,
                    aiReply.Model));

            return new ChatReplyDto(
                replyText,
                resolvedConversationId,
                conversation.Title,
                new ChatUsageDto(aiReply.PromptTokens, aiReply.CompletionTokens, aiReply.TotalTokens, aiReply.Model),
                new ChatMetadataDto(requestId, (int)stopwatch.ElapsedMilliseconds),
                grounding);
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
                else if (stageJournal is { HasCharge: true })
                {
                    await _quotaReservationService.SettleAsync(reservation, stageJournal.Charge, cleanupToken);
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

    private static string MapHistoryRole(AiMessageRole role)
        => role switch
        {
            AiMessageRole.System => "system",
            AiMessageRole.Assistant => "assistant",
            _ => "user"
        };

    private async Task<List<AiMessage>> LoadContextHistoryAsync(int userId, Guid conversationId, Guid currentMessageId, string message, CancellationToken ct)
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
                .Select(m => new AiMessage { Id = m.Id, CreatedAtUtc = m.CreatedAtUtc, Role = m.Role, Content = m.Content, GroundingMetadata = m.GroundingMetadata }).ToListAsync(ct);
            if (batch.Count == 0) break;
            if (_grounding is not null)
            {
                await _grounding.ProjectAsync(userId, batch, GroundingProjectionMode.ProviderContext, ct);
            }

            foreach (var item in batch)
            {
                // Budget the projected text, including a replacement notice, before any return.
                var cost = Math.Max(1, _historyTokenCounter.CountTokens(item.Content)) + 4;
                if (cost > remaining) { history.Reverse(); return history; }
                history.Add(item); remaining -= cost;
            }
            beforeTime = batch[^1].CreatedAtUtc; beforeId = batch[^1].Id;
            if (batch.Count < 50) break;
        }
        history.Reverse();
        return history;
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
