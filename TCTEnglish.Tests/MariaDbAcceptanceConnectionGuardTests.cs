using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class MariaDbAcceptanceConnectionGuardTests
{
    [Theory]
    [InlineData("Server=db.example.com;Port=3308;Database=tct_acceptance_r2;User=tct_acc;Password=x")]
    [InlineData("Server=127.0.0.1;Port=3307;Database=tct_acceptance_r2;User=tct_acc;Password=x")]
    [InlineData("Server=127.0.0.1;Port=3308;Database=tctenglish_dev;User=tct_acc;Password=x")]
    [InlineData("Server=localhost;Port=3308;Database=nhtctsnn_tctenglish;User=tct_acc;Password=x")]
    [InlineData("Server=127.0.0.1;Port=3308;Database=tctenglish_local;User=tct_acc;Password=x")]
    [InlineData("Server=127.0.0.1;Port=3308;Database=mysql;User=root;Password=x")]
    [InlineData("")]
    public void ParseAndValidateOffline_RejectsUnsafeTargets(string connectionString)
    {
        Assert.ThrowsAny<InvalidOperationException>(
            () => MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connectionString));
    }

    [Fact]
    public void ParseAndValidateOffline_AcceptsEphemeralLoopbackContract()
    {
        var parsed = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(
            "Server=127.0.0.1;Port=3308;Database=tct_acceptance_2930;User=tct_acc;Password=secret");

        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.Equal(3308, parsed.Port);
        Assert.Equal("tct_acceptance_2930", parsed.Database);
        Assert.Equal("tct_acc", parsed.User);
    }

    [Fact]
    public void Constructor_RejectsUnsafeConnection_BeforeHostBuild()
    {
        Assert.ThrowsAny<InvalidOperationException>(() =>
            new MariaDbAcceptanceFactory(
                "Server=127.0.0.1;Port=3308;Database=tctenglish_dev;User=tct_acc;Password=x"));
    }
}
