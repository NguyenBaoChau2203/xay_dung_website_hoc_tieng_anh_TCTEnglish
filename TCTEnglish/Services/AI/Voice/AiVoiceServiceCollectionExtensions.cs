using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TCTEnglish.Services.AI.Voice;

public static class AiVoiceServiceCollectionExtensions
{
    public static IServiceCollection AddAiVoiceConversation(this IServiceCollection services)
    {
        services.AddOptions<AiVoiceOptions>().BindConfiguration(AiVoiceOptions.SectionName);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<AiVoiceEntitlementService>();
        services.TryAddScoped<AiVoiceTopicService>();
        services.TryAddScoped<AiVoiceQuotaService>();
        services.TryAddScoped<AiVoiceSessionService>();
        services.TryAddScoped<AiVoiceEventProcessor>();
        services.TryAddScoped<AiVoiceHistoryService>();
        services.TryAddScoped<AiVoiceEventStream>();
        services.TryAddSingleton<AiVoiceRuntimeRegistry>();
        services.TryAddSingleton<IVoiceSidebandFactory, ClientVoiceSidebandFactory>();
        services.AddHttpClient(OpenAiRealtimeVoiceClient.HttpClientName, c => c.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
        services.TryAddSingleton<IRealtimeVoiceClient, OpenAiRealtimeVoiceClient>();
        services.AddHostedService<TCTEnglish.Workers.AiVoiceSessionCleanupWorker>();
        return services;
    }
}
