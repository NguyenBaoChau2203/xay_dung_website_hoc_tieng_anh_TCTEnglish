using Microsoft.Extensions.Configuration;
using TCTEnglish.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public class DatabaseSettingsTests
{
    [Fact]
    public void Read_RejectsMissingConnectionWithSetupInstruction()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => DatabaseSettings.Read(Config(null), true));
        Assert.Contains("start-local.ps1", exception.Message);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    [InlineData("::1")]
    public void Read_AcceptsLoopbackDevelopmentDatabase(string server)
    {
        var settings = DatabaseSettings.Read(Config($"Server={server};Port=3307;Database=tctenglish_dev;User=dev;Password=test;"), true);
        Assert.Contains("tctenglish_dev", settings.ConnectionString);
        Assert.Contains("10.5.23", settings.ServerVersion.ToString());
    }

    [Fact]
    public void Read_RejectsRemoteDevelopmentDatabaseBeforeConnecting()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => DatabaseSettings.Read(Config("Server=database.example.test;Database=production;User=dev;Password=test;"), true));
        Assert.Contains("local", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", exception.Message);
    }

    [Fact]
    public void Read_AllowsConfiguredProductionServerWithoutNetworkAccess()
    {
        var settings = DatabaseSettings.Read(Config("Server=database.example.test;Database=production;User=app;Password=test;"), false);
        Assert.Contains("database.example.test", settings.ConnectionString);
    }

    private static IConfiguration Config(string? connection) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection,
            ["Database:ServerVersion"] = "10.5.23-mariadb"
        }).Build();
}
