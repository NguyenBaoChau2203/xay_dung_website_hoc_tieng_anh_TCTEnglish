using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>Opt-in checks only; never constructs a transcriber or sends audio.</summary>
public sealed class OpenAiSpeechLiveConfigurationTests
{
    [Fact]
    public void RetiredLoopbackFlags_DoNotEnablePaidSpeechTests()
    {
        var names = new[] { OpenAiSpeechLiveFactAttribute.FlagName, "TCT_ASR_LIVE_BASEURL", "TCT_ASR_LIVE_MODEL" };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(OpenAiSpeechLiveFactAttribute.FlagName, null);
            Environment.SetEnvironmentVariable("TCT_ASR_LIVE_BASEURL", "http://127.0.0.1:8091/");
            Environment.SetEnvironmentVariable("TCT_ASR_LIVE_MODEL", "tiny.en");
            Assert.False(OpenAiSpeechLiveFactAttribute.IsEnabled());
            Assert.NotNull(new OpenAiSpeechLiveFactAttribute().Skip);
        }
        finally
        {
            foreach (var (name, value) in previous)
                Environment.SetEnvironmentVariable(name, value);
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData("1", true)]
    public void PaidSpeechTests_RequireTheirOwnExactOptIn(string? value, bool enabled)
    {
        var previous = Environment.GetEnvironmentVariable(OpenAiSpeechLiveFactAttribute.FlagName);
        try
        {
            Environment.SetEnvironmentVariable(OpenAiSpeechLiveFactAttribute.FlagName, value);
            Assert.Equal(enabled, OpenAiSpeechLiveFactAttribute.IsEnabled());
            Assert.Equal(enabled, new OpenAiSpeechLiveFactAttribute().Skip is null);
        }
        finally
        {
            Environment.SetEnvironmentVariable(OpenAiSpeechLiveFactAttribute.FlagName, previous);
        }
    }
}
