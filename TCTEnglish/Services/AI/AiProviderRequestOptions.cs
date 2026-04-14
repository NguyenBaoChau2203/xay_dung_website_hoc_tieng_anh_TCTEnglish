namespace TCTEnglish.Services.AI;

public sealed class AiProviderRequestOptions
{
    public int? MaxOutputTokens { get; init; }

    public int? RequestTimeoutSeconds { get; init; }

    public double? Temperature { get; init; }
}
