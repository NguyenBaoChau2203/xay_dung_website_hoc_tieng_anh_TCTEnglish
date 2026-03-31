using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.Services.AI;

namespace TCTEnglish.Tests.TestHelpers;

public sealed class StubAiProviderClient : IAiProviderClient
{
    private readonly Func<IReadOnlyList<AiContextMessage>, CancellationToken, Task<AiProviderReply>> _handler;

    public StubAiProviderClient(Func<IReadOnlyList<AiContextMessage>, CancellationToken, Task<AiProviderReply>> handler)
    {
        _handler = handler;
    }

    public Task<AiProviderReply> GenerateReplyAsync(IReadOnlyList<AiContextMessage> messages, CancellationToken ct)
    {
        return _handler(messages, ct);
    }
}
