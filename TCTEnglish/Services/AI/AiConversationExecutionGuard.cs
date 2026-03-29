using System;
using System.Collections.Concurrent;
using System.Threading;

namespace TCTEnglish.Services.AI;

public sealed class AiConversationExecutionGuard : IAiConversationExecutionGuard
{
    private readonly ConcurrentDictionary<Guid, byte> _activeConversationIds = new();

    public bool TryAcquire(Guid conversationId, out IDisposable? lease)
    {
        if (!_activeConversationIds.TryAdd(conversationId, 0))
        {
            lease = null;
            return false;
        }

        lease = new ReleaseLease(_activeConversationIds, conversationId);
        return true;
    }

    private sealed class ReleaseLease : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, byte> _activeConversationIds;
        private readonly Guid _conversationId;
        private int _disposed;

        public ReleaseLease(ConcurrentDictionary<Guid, byte> activeConversationIds, Guid conversationId)
        {
            _activeConversationIds = activeConversationIds;
            _conversationId = conversationId;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _activeConversationIds.TryRemove(_conversationId, out _);
        }
    }
}
