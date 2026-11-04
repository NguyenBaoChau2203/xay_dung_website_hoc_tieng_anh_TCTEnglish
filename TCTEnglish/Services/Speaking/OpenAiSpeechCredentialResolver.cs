using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Speaking;

public sealed class OpenAiSpeechCredentials
{
    public OpenAiSpeechCredentials(string apiKey, string source, string model, Uri baseAddress)
    {
        ApiKey = apiKey;
        Source = source;
        Model = model;
        BaseAddress = baseAddress;
    }

    public string ApiKey { get; }
    public string Source { get; }
    public string Model { get; }
    public Uri BaseAddress { get; }

    public override string ToString()
        => $"OpenAiSpeechCredentials {{ Source = {Source}, Model = {Model}, BaseAddress = {BaseAddress}, ApiKey = [REDACTED] }}";
}

public interface IOpenAiSpeechCredentialResolver
{
    /// <summary>Resolves a usable speech key without logging or copying the secret.</summary>
    bool TryResolve(out OpenAiSpeechCredentials credentials);
}

/// <summary>
/// Precedence: explicit speech env → explicit speech User Secrets/config →
/// AI__ApiKey → AI:ApiKey → OPENAI_API_KEY.
/// Explicit empty/invalid at any authority level does not fall through; only missing does.
/// </summary>
public sealed class OpenAiSpeechCredentialResolver(
    IConfiguration configuration,
    IOptions<SpeechProcessingOptions> options) : IOpenAiSpeechCredentialResolver
{
    public const string SpeechApiKeyEnv = "Learning__Speech__OpenAI__ApiKey";
    public const string SpeechApiKeyConfig = "Learning:Speech:OpenAI:ApiKey";
    public const string SharedAiEnv = "AI__ApiKey";
    public const string SharedAiConfig = "AI:ApiKey";
    public const string OpenAiEnv = "OPENAI_API_KEY";

    public bool TryResolve(out OpenAiSpeechCredentials credentials)
    {
        credentials = default!;
        var speech = options.Value;
        if (!speech.Enabled)
        {
            return false;
        }

        if (!TryBuildBaseAddress(speech.OpenAI.BaseUrl, out var baseAddress))
        {
            return false;
        }

        var modelEnv = Environment.GetEnvironmentVariable("Learning__Speech__OpenAI__Model");
        var model = !string.IsNullOrWhiteSpace(modelEnv)
            ? modelEnv.Trim()
            : string.IsNullOrWhiteSpace(speech.OpenAI.Model)
                ? "gpt-transcribe"
                : speech.OpenAI.Model.Trim();

        var envSpeech = Environment.GetEnvironmentVariable(SpeechApiKeyEnv);
        if (envSpeech != null)
        {
            return FinishExplicit(envSpeech, SpeechApiKeyEnv, model, baseAddress, out credentials);
        }

        // Missing key → null from Get; empty User Secret → ""; appsettings must omit ApiKey so null.
        var configSpeech = configuration[SpeechApiKeyConfig];
        if (configSpeech != null)
        {
            return FinishExplicit(configSpeech, "User Secrets/config speech", model, baseAddress, out credentials);
        }

        var envAi = Environment.GetEnvironmentVariable(SharedAiEnv);
        if (envAi != null)
        {
            return FinishExplicit(envAi, SharedAiEnv, model, baseAddress, out credentials);
        }

        var configAi = configuration[SharedAiConfig];
        if (configAi != null)
        {
            return FinishExplicit(configAi, "User Secrets AI:ApiKey", model, baseAddress, out credentials);
        }

        var openAi = Environment.GetEnvironmentVariable(OpenAiEnv);
        if (openAi != null)
        {
            return FinishExplicit(openAi, OpenAiEnv, model, baseAddress, out credentials);
        }

        return false;
    }

    private static bool FinishExplicit(
        string raw,
        string source,
        string model,
        Uri baseAddress,
        out OpenAiSpeechCredentials credentials)
    {
        credentials = default!;
        if (!IsUsableKey(raw))
        {
            return false;
        }

        credentials = new OpenAiSpeechCredentials(raw.Trim(), source, model, baseAddress);
        return true;
    }

    internal static bool IsUsableKey(string key) =>
        !string.IsNullOrWhiteSpace(key)
        && !key.Any(char.IsWhiteSpace)
        && !key.StartsWith("AIza", StringComparison.Ordinal);

    internal static bool TryBuildBaseAddress(string? baseUrl, out Uri baseAddress)
    {
        baseAddress = null!;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return false;
        }

        var normalized = baseUrl.Trim();
        if (!normalized.EndsWith('/'))
        {
            normalized += "/";
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps
            && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        baseAddress = uri;
        return true;
    }
}
