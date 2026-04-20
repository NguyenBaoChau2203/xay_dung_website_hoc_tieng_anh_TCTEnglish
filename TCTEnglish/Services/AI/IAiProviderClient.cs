using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TCTEnglish.Services.AI;

public interface IAiProviderClient
{
    Task<AiProviderReply> GenerateReplyAsync(
        int userId,
        IReadOnlyList<AiContextMessage> messages,
        CancellationToken ct,
        AiProviderRequestOptions? requestOptions = null);
}

