using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TCTEnglish.Services.AI;

public sealed class AiStreamingService : IAiStreamingService
{
    private const int ChunkSize = 90;

    private static readonly ConcurrentDictionary<string, ActiveStreamRegistration> ActiveStreams = new();
    private static readonly ConcurrentDictionary<Guid, string> ActiveConversationStreams = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAiRequestRateLimiter _rateLimiter;
    private readonly ILogger<AiStreamingService> _logger;

    public AiStreamingService(
        IServiceScopeFactory scopeFactory,
        IAiRequestRateLimiter rateLimiter,
        ILogger<AiStreamingService> logger)
    {
        _scopeFactory = scopeFactory;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<AiStreamingSession> StartStreamAsync(
        int userId,
        Guid conversationId,
        string message,
        string connectionId,
        string? ipAddress,
        CancellationToken connectionAborted)
    {
        if (!_rateLimiter.TryConsume(userId, ipAddress, out _))
        {
            throw new AiRateLimitException(
                "Bạn đang gửi quá nhanh. Vui lòng thử lại sau.",
                "rate_limited");
        }

        var streamId = Guid.NewGuid().ToString("N");
        var streamKey = BuildStreamKey(connectionId, streamId);

        if (!ActiveConversationStreams.TryAdd(conversationId, streamKey))
        {
            throw new AiConcurrentRequestException(
                "Cuộc hội thoại này đang có một yêu cầu AI khác đang xử lý. Vui lòng chờ phản hồi hiện tại hoàn tất.");
        }

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(connectionAborted);
        var registration = new ActiveStreamRegistration(conversationId, connectionId, linkedCts);

        if (!ActiveStreams.TryAdd(streamKey, registration))
        {
            linkedCts.Dispose();
            ActiveConversationStreams.TryRemove(conversationId, out _);

            throw new AiConcurrentRequestException(
                "Không thể bắt đầu stream mới. Vui lòng thử lại.",
                "stream_conflict");
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var chatService = scope.ServiceProvider.GetRequiredService<IAiChatService>();
            var reply = await chatService.SendAsync(userId, conversationId, message, linkedCts.Token);

            return new AiStreamingSession(
                conversationId,
                streamId,
                SplitIntoChunks(reply.Text, ChunkSize).ToList(),
                reply.Usage,
                reply.Metadata,
                linkedCts.Token,
                () => CleanupAsync(streamKey));
        }
        catch
        {
            await CleanupAsync(streamKey);
            throw;
        }
    }

    public ValueTask StopStreamAsync(string connectionId, string streamId)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(streamId))
        {
            return ValueTask.CompletedTask;
        }

        var streamKey = BuildStreamKey(connectionId, streamId);
        if (ActiveStreams.TryGetValue(streamKey, out var registration))
        {
            registration.CancellationTokenSource.Cancel();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CancelAllStreamsAsync(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return ValueTask.CompletedTask;
        }

        var streamKeyPrefix = $"{connectionId}:";
        var streamKeys = ActiveStreams.Keys
            .Where(x => x.StartsWith(streamKeyPrefix, StringComparison.Ordinal))
            .ToList();

        foreach (var streamKey in streamKeys)
        {
            if (ActiveStreams.TryGetValue(streamKey, out var registration))
            {
                registration.CancellationTokenSource.Cancel();
            }
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask CleanupAsync(string streamKey)
    {
        if (!ActiveStreams.TryRemove(streamKey, out var registration))
        {
            return ValueTask.CompletedTask;
        }

        if (ActiveConversationStreams.TryGetValue(registration.ConversationId, out var activeStreamKey)
            && string.Equals(activeStreamKey, streamKey, StringComparison.Ordinal))
        {
            ActiveConversationStreams.TryRemove(registration.ConversationId, out _);
        }

        registration.CancellationTokenSource.Dispose();

        _logger.LogDebug(
            "AI stream cleaned up. ConversationId {conversationId}. ConnectionId {connectionId}. StreamKey {streamKey}",
            registration.ConversationId,
            registration.ConnectionId,
            streamKey);

        return ValueTask.CompletedTask;
    }

    private static IEnumerable<string> SplitIntoChunks(string text, int chunkSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        var offset = 0;
        while (offset < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - offset);
            yield return text.Substring(offset, length);
            offset += length;
        }
    }

    private static string BuildStreamKey(string connectionId, string streamId)
    {
        return $"{connectionId}:{streamId}";
    }

    private sealed record ActiveStreamRegistration(
        Guid ConversationId,
        string ConnectionId,
        CancellationTokenSource CancellationTokenSource);
}
