using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AsrLiveAcceptanceConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingOptIn_DoesNotSelectAnEngine(string? value)
        => Assert.Null(AsrLiveAcceptanceFactAttribute.ParseEndpoint(value));

    [Theory]
    [InlineData("http://127.0.0.1:8091")]
    [InlineData("http://localhost:8091/")]
    [InlineData("http://[::1]:8091")]
    public void ExplicitLoopbackEndpoint_IsAcceptedWithoutNetwork(string value)
        => Assert.True(AsrLiveAcceptanceFactAttribute.ParseEndpoint(value)!.IsLoopback);

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://103.28.36.90:8091")]
    [InlineData("http://127.0.0.1.example.com:8091")]
    [InlineData("http://user:PRIVATE_SENTINEL@127.0.0.1:8091")]
    [InlineData("file:///tmp/engine")]
    [InlineData("http://127.0.0.1:8091/?token=PRIVATE_SENTINEL")]
    public void UnsafeEndpoint_IsRejectedWithoutEchoingInput(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => AsrLiveAcceptanceFactAttribute.ParseEndpoint(value));
        Assert.DoesNotContain("PRIVATE_SENTINEL", error.Message);
        Assert.DoesNotContain(value, error.Message);
    }
}
