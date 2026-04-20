using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.Services.AI;

namespace TCTEnglish.Tests.TestHelpers;

public sealed class StubAiProviderClient : IAiProviderClient
{
    private readonly Func<int, IReadOnlyList<AiContextMessage>, CancellationToken, Task<AiProviderReply>> _handler;
    private readonly Func<int, IReadOnlyList<AiContextMessage>, AiProviderRequestOptions?, CancellationToken, Task<AiProviderReply>>? _handlerWithOptions;

    public StubAiProviderClient(Func<int, IReadOnlyList<AiContextMessage>, CancellationToken, Task<AiProviderReply>> handler)
    {
        _handler = handler;
    }

    public StubAiProviderClient(Func<int, IReadOnlyList<AiContextMessage>, AiProviderRequestOptions?, CancellationToken, Task<AiProviderReply>> handler)
    {
        _handler = (_, _, _) => throw new InvalidOperationException("Option-aware handler is required for this stub instance.");
        _handlerWithOptions = handler;
    }

    public Task<AiProviderReply> GenerateReplyAsync(
        int userId,
        IReadOnlyList<AiContextMessage> messages,
        CancellationToken ct,
        AiProviderRequestOptions? requestOptions = null)
    {
        if (_handlerWithOptions is not null)
        {
            return _handlerWithOptions(userId, messages, requestOptions, ct);
        }

        return _handler(userId, messages, ct);
    }
}
