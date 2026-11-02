using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Infrastructure;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class LocalDevelopmentBootstrapTests
{
    private static IConfiguration Config(string server = "127.0.0.1", string database = "tctenglish_dev") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"Server={server};Database={database};User=tct_dev;Password=fixture-only;"
        }).Build();

    [Theory]
    [InlineData(false, "127.0.0.1", "tctenglish_dev")]
    [InlineData(true, "hosting.invalid", "tctenglish_dev")]
    [InlineData(true, "127.0.0.1", "production")]
    public void Guard_RejectsProductionRemoteOrOtherDatabase_BeforeConnecting(bool development, string server, string database)
    {
        Assert.Throws<InvalidOperationException>(() => LocalDevelopmentBootstrap.ValidateTarget(Config(server, database), development));
    }

    [Fact]
    public async Task EmptyDatabase_GetsRandomLocalAccountsAndSamples_RerunPreservesEditsAndCredentials()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await db.Database.EnsureCreatedAsync();
        var directory = Path.Combine(Path.GetTempPath(), "tct-bootstrap-test-" + Guid.NewGuid().ToString("N"));
        var accessPath = Path.Combine(directory, "access.json");
        try
        {
            var created = await LocalDevelopmentBootstrap.RunAsync(db, Config(), true, accessPath);
            Assert.True(created);
            var credentials = await File.ReadAllTextAsync(accessPath);
            using var json = System.Text.Json.JsonDocument.Parse(credentials);
            var accounts = json.RootElement.GetProperty("accounts").EnumerateArray().ToArray();
            Assert.Equal(3, accounts.Length);
            Assert.Equal(3, await db.Users.CountAsync());
            foreach (var account in accounts)
            {
                var user = await db.Users.SingleAsync(u => u.Email == account.GetProperty("email").GetString());
                var password = account.GetProperty("password").GetString()!;
                Assert.True(password.Length >= 24);
                Assert.True(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
            }
            Assert.Equal(3, accounts.Select(a => a.GetProperty("password").GetString()).Distinct().Count());
            Assert.True(await db.Cards.AnyAsync());
            Assert.True(await db.WritingExercises.AnyAsync());
            Assert.True(await db.SpeakingSentences.AnyAsync());
            var card = await db.Cards.FirstAsync();
            card.Definition = "Owner's local edit";
            await db.SaveChangesAsync();
            Assert.False(await LocalDevelopmentBootstrap.RunAsync(db, Config(), true, accessPath));
            Assert.Equal(credentials, await File.ReadAllTextAsync(accessPath));
            Assert.Equal("Owner's local edit", (await db.Cards.SingleAsync(c => c.CardId == card.CardId)).Definition);
            Assert.Equal(3, await db.Users.CountAsync());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ExistingUser_IsNeverPromotedOrReplaced_AndGetsNoBootstrapPasswordFile()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var count = await db.Users.CountAsync();
        var path = Path.Combine(Path.GetTempPath(), "tct-bootstrap-existing-" + Guid.NewGuid().ToString("N"), "access.json");
        Assert.False(await LocalDevelopmentBootstrap.RunAsync(db, Config(), true, path));
        Assert.False(File.Exists(path));
        Assert.Equal(count, await db.Users.CountAsync());
    }

    [Fact]
    public async Task ExistingCredentialFile_IsNotOverwritten_AndLeavesEmptyDatabaseUnchanged()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await db.Database.EnsureCreatedAsync();
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "preserve existing credentials");
            await Assert.ThrowsAsync<IOException>(() => LocalDevelopmentBootstrap.RunAsync(db, Config(), true, path));
            Assert.Equal("preserve existing credentials", await File.ReadAllTextAsync(path));
            Assert.False(await db.Users.AnyAsync());
        }
        finally { File.Delete(path); }
    }
}
