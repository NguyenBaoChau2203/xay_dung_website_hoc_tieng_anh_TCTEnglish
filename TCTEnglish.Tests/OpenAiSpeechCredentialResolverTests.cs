using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class OpenAiSpeechCredentialResolverTests : IDisposable
{
    private readonly List<string> _cleared = new();

    public void Dispose()
    {
        foreach (var name in _cleared.Distinct(StringComparer.Ordinal))
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void MissingKey_IsNotConfigured_WithoutFallingThrough()
    {
        Clear(OpenAiSpeechCredentialResolver.SpeechApiKeyEnv);
        Clear(OpenAiSpeechCredentialResolver.SharedAiEnv);
        Clear(OpenAiSpeechCredentialResolver.OpenAiEnv);
        var resolver = Create(new Dictionary<string, string?>());
        Assert.False(resolver.TryResolve(out _));
    }

    [Fact]
    public void ExplicitEmptySpeechEnv_DoesNotUseSharedKey()
    {
        Clear(OpenAiSpeechCredentialResolver.OpenAiEnv);
        Set(OpenAiSpeechCredentialResolver.SpeechApiKeyEnv, "");
        Set(OpenAiSpeechCredentialResolver.SharedAiEnv, "sk-shared-should-not-use");
        var resolver = Create(new Dictionary<string, string?>());
        Assert.False(resolver.TryResolve(out _));
    }

    [Fact]
    public void ExplicitInvalidSpeechEnv_DoesNotUseSharedKey()
    {
        Set(OpenAiSpeechCredentialResolver.SpeechApiKeyEnv, "AIza-legacy");
        Set(OpenAiSpeechCredentialResolver.SharedAiEnv, "sk-shared-should-not-use");
        var resolver = Create(new Dictionary<string, string?>());
        Assert.False(resolver.TryResolve(out _));
    }

    [Fact]
    public void SharedAiEnv_IsUsedWhenSpeechUnset()
    {
        Clear(OpenAiSpeechCredentialResolver.SpeechApiKeyEnv);
        Clear(OpenAiSpeechCredentialResolver.OpenAiEnv);
        Set(OpenAiSpeechCredentialResolver.SharedAiEnv, "sk-from-ai-env");
        var resolver = Create(new Dictionary<string, string?>());
        Assert.True(resolver.TryResolve(out var credentials));
        Assert.Equal("sk-from-ai-env", credentials.ApiKey);
        Assert.Equal(OpenAiSpeechCredentialResolver.SharedAiEnv, credentials.Source);
        Assert.Equal("gpt-transcribe", credentials.Model);
    }

    [Fact]
    public void OpenAiEnv_IsLastResortSharedFallback()
    {
        Clear(OpenAiSpeechCredentialResolver.SpeechApiKeyEnv);
        Clear(OpenAiSpeechCredentialResolver.SharedAiEnv);
        Set(OpenAiSpeechCredentialResolver.OpenAiEnv, "sk-from-openai-env");
        var resolver = Create(new Dictionary<string, string?>());
        Assert.True(resolver.TryResolve(out var credentials));
        Assert.Equal("sk-from-openai-env", credentials.ApiKey);
        Assert.Equal(OpenAiSpeechCredentialResolver.OpenAiEnv, credentials.Source);
    }

    [Fact]
    public void UserSecretsSpeechKey_BeatsSharedAi()
    {
        Clear(OpenAiSpeechCredentialResolver.SpeechApiKeyEnv);
        Clear(OpenAiSpeechCredentialResolver.SharedAiEnv);
        Clear(OpenAiSpeechCredentialResolver.OpenAiEnv);
        var resolver = Create(new Dictionary<string, string?>
        {
            ["Learning:Speech:OpenAI:ApiKey"] = "sk-speech-secret",
            ["AI:ApiKey"] = "sk-shared-secret"
        });
        Assert.True(resolver.TryResolve(out var credentials));
        Assert.Equal("sk-speech-secret", credentials.ApiKey);
    }

    [Fact]
    public void DisabledSpeech_NeverResolves()
    {
        Set(OpenAiSpeechCredentialResolver.SharedAiEnv, "sk-present");
        var resolver = Create(new Dictionary<string, string?>(), enabled: false);
        Assert.False(resolver.TryResolve(out _));
    }

    [Fact]
    public void ExplicitInvalidSharedAiEnv_DoesNotFallThroughToConfigOrOpenAi()
    {
        Clear(OpenAiSpeechCredentialResolver.SpeechApiKeyEnv);
        Set(OpenAiSpeechCredentialResolver.SharedAiEnv, "invalid shared credential");
        Clear(OpenAiSpeechCredentialResolver.OpenAiEnv);
        var resolver = Create(new Dictionary<string, string?>
        {
            ["AI:ApiKey"] = "sk-config-should-not-use"
        });
        Assert.False(resolver.TryResolve(out _));
    }

    [Fact]
    public void ExplicitEmptySharedAiConfig_DoesNotFallThroughToOpenAiEnv()
    {
        Clear(OpenAiSpeechCredentialResolver.SpeechApiKeyEnv);
        Clear(OpenAiSpeechCredentialResolver.SharedAiEnv);
        Set(OpenAiSpeechCredentialResolver.OpenAiEnv, "sk-openai-should-not-use");
        var resolver = Create(new Dictionary<string, string?>
        {
            ["AI:ApiKey"] = ""
        });
        Assert.False(resolver.TryResolve(out _));
    }

    [Fact]
    public void CredentialsToString_RedactsApiKey()
    {
        var credentials = new OpenAiSpeechCredentials(
            "sk-secret-value",
            "test",
            "gpt-transcribe",
            new Uri("https://api.openai.com/v1/"));
        var text = credentials.ToString();
        Assert.Contains("[REDACTED]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-secret-value", text, StringComparison.Ordinal);
    }

    private OpenAiSpeechCredentialResolver Create(Dictionary<string, string?> values, bool enabled = true)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new OpenAiSpeechCredentialResolver(
            configuration,
            Options.Create(new SpeechProcessingOptions
            {
                Enabled = enabled,
                OpenAI = new OpenAiSpeechOptions
                {
                    Model = "gpt-transcribe",
                    BaseUrl = "https://api.openai.com/v1/"
                }
            }));
    }

    private void Set(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _cleared.Add(name);
    }

    private void Clear(string name)
    {
        Environment.SetEnvironmentVariable(name, null);
        _cleared.Add(name);
    }
}
