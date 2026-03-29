using System;
using System.Threading;
using System.Threading.Tasks;

namespace TCTEnglish.Services.AI;

public interface IAiStreamingService
{
    Task<AiStreamingSession> StartStreamAsync(
        int userId,
        Guid conversationId,
        string message,
        string connectionId,
        string? ipAddress,
        CancellationToken connectionAborted);

    ValueTask StopStreamAsync(string connectionId, string streamId);

    ValueTask CancelAllStreamsAsync(string connectionId);
}
