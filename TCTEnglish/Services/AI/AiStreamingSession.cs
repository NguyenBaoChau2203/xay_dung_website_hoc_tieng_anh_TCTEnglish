using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.ViewModels.AI;

namespace TCTEnglish.Services.AI;

public sealed class AiStreamingSession : IAsyncDisposable
{
    private readonly Func<ValueTask> _disposeAsync;

    internal AiStreamingSession(
        Guid conversationId,
        string streamId,
        IReadOnlyList<string> chunks,
        ChatUsageDto usage,
        ChatMetadataDto metadata,
        CancellationToken cancellationToken,
        Func<ValueTask> disposeAsync)
    {
        ConversationId = conversationId;
        StreamId = streamId;
        Chunks = chunks;
        Usage = usage;
        Metadata = metadata;
        CancellationToken = cancellationToken;
        _disposeAsync = disposeAsync;
    }

    public Guid ConversationId { get; }

    public string StreamId { get; }

    public IReadOnlyList<string> Chunks { get; }

    public ChatUsageDto Usage { get; }

    public ChatMetadataDto Metadata { get; }

    public CancellationToken CancellationToken { get; }

    public ValueTask DisposeAsync()
    {
        return _disposeAsync();
    }
}
