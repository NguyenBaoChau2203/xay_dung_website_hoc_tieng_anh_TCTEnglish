using System;

namespace TCTEnglish.Services.AI;

public interface IAiConversationExecutionGuard
{
    bool TryAcquire(Guid conversationId, out IDisposable? lease);
}
