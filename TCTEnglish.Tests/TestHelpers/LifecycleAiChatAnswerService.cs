using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Rag;

namespace TCTEnglish.Tests.TestHelpers;

public sealed class LifecycleAiChatAnswerService : IAiChatAnswerService
{
    private readonly IAiProviderClient _provider;

    public LifecycleAiChatAnswerService(IAiProviderClient provider)
    {
        _provider = provider;
    }

    public async Task<AiChatAnswer> AnswerAsync(
        int userId,
        string userMessage,
        IReadOnlyList<AiContextMessage> contextMessages,
        CancellationToken ct)
    {
        var reply = await _provider.GenerateReplyAsync(userId, contextMessages, ct);
        return new AiChatAnswer(
            HybridRagOutcome.Answered,
            "full",
            reply.Text,
            [],
            reply.PromptTokens,
            reply.CompletionTokens,
            reply.TotalTokens,
            reply.Model,
            reply.RequestId);
    }
}

public static class ChatLifecycleTestServices
{
    public static void KeepChatLifecycleOnProviderStub(this IServiceCollection services)
    {
        services.RemoveAll<IAiChatAnswerService>();
        services.AddScoped<IAiChatAnswerService>(sp =>
            new LifecycleAiChatAnswerService(sp.GetRequiredService<IAiProviderClient>()));
    }
}
