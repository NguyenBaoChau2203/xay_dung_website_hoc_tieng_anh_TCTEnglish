using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>S5: the server-rendered host of the pop-up.</summary>
public sealed class OnboardingRenderTests
{
    private const int NewUser = 960;
    private const string HostConfig = "<script type=\"application/json\" id=\"tobConfig\">";

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var systemId = await db.Users.Where(u => u.Email == "system@tct.local").Select(u => u.UserId).SingleAsync();
        db.Users.Add(new User
        {
            UserId = NewUser, Email = $"render{NewUser}@test.local", PasswordHash = "hash", FullName = "Render " + NewUser,
            Role = Roles.Standard, Status = UserStatus.Online, CreatedAt = DateTime.UtcNow
        });
        var set = new Set { OwnerId = systemId, SetName = "Trái cây", Visibility = "Public", CreatedAt = DateTime.UtcNow };
        for (var i = 1; i <= 12; i++)
        {
            set.Cards.Add(new Card { Term = "fruit" + i, Definition = "quả " + i, Cefr = "A1", ImageUrl = $"/uploads/shared/fruit{i}.webp" });
        }

        db.Sets.Add(set);
        await db.SaveChangesAsync();
        return factory;
    }

    private static string ConfigJsonOf(string html)
    {
        var start = html.IndexOf(HostConfig, StringComparison.Ordinal);
        Assert.True(start >= 0, "The host config script is missing.");
        start += HostConfig.Length;
        return html[start..html.IndexOf("</script>", start, StringComparison.Ordinal)];
    }

    [Theory]
    [InlineData("/Home/Index")]
    [InlineData("/Vocabulary/Index")]
    [InlineData("/Sets")]
    public async Task EveryHostPage_RendersTheShellOnce_WithTheScriptsInOrder_AndNoInlineCode(string route)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, NewUser, Roles.Standard);

        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(1, Regex.Matches(html, "id=\"tobRoot\"").Count);
        Assert.Contains("id=\"tobModal\"", html);
        Assert.Contains("data-bs-backdrop=\"static\"", html);
        Assert.Contains("role=\"dialog\"", html);
        Assert.Contains("aria-modal=\"true\"", html);
        Assert.Contains("aria-labelledby=\"tobTitle\"", html);
        Assert.Contains("id=\"tobLive\"", html);
        Assert.Contains("__RequestVerificationToken", html); // the token the script sends as a header

        var core = html.IndexOf("/js/onboarding-core.js", StringComparison.Ordinal);
        var ui = html.IndexOf("/js/onboarding.js", StringComparison.Ordinal);
        Assert.True(core > 0 && ui > core, "onboarding-core.js must load before onboarding.js.");
        Assert.Contains("/css/onboarding.css", html);

        // the host itself carries no inline style, script body (other than the JSON) or event handler
        var host = html[html.IndexOf("id=\"tobRoot\"", StringComparison.Ordinal)..];
        host = host[..host.IndexOf("</script>", host.IndexOf(HostConfig, StringComparison.Ordinal), StringComparison.Ordinal)];
        Assert.DoesNotContain(" style=", host);
        Assert.DoesNotMatch(" on[a-z]+=\"", host);
    }

    [Fact]
    public async Task ANewAccount_GetsAutoOpenAndTheCatalogue()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, NewUser, Roles.Standard);

        var json = JsonDocument.Parse(ConfigJsonOf(await client.GetStringAsync("/Home/Index"))).RootElement;
        Assert.True(json.GetProperty("shouldPrompt").GetBoolean());
        Assert.Equal("first", json.GetProperty("mode").GetString());
        Assert.Equal("/api/onboarding/complete", json.GetProperty("api").GetProperty("complete").GetString());

        var catalog = json.GetProperty("catalog");
        Assert.Equal(6, catalog.GetProperty("goals").GetArrayLength());
        Assert.Equal(48, catalog.GetProperty("topics").GetArrayLength());
        var fruit = catalog.GetProperty("topics").EnumerateArray().Single(t => t.GetProperty("key").GetString() == "A03");
        Assert.Equal("Trái cây", fruit.GetProperty("name").GetString());
        Assert.StartsWith("/uploads/shared/", fruit.GetProperty("cover").GetString());
        var noSet = catalog.GetProperty("topics").EnumerateArray().Single(t => t.GetProperty("key").GetString() == "B03");
        Assert.Equal(JsonValueKind.Null, noSet.GetProperty("cover").ValueKind);
    }

    [Fact]
    public async Task AnExistingAccount_GetsNoAutoOpen_AndNoCatalogueInThePage()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard); // owns a set

        var json = JsonDocument.Parse(ConfigJsonOf(await client.GetStringAsync("/Sets"))).RootElement;
        Assert.False(json.GetProperty("shouldPrompt").GetBoolean());
        Assert.Equal("again", json.GetProperty("mode").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("catalog").ValueKind);
    }

    [Fact]
    public async Task TheCatalogueEndpoint_ServesTheSameShape_ToAnySignedInUser()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/api/onboarding/catalog");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(48, json.GetProperty("topics").GetArrayLength());
        Assert.Equal(6, json.GetProperty("goals").GetArrayLength());
        Assert.True(json.GetProperty("welcome").GetArrayLength() >= 1);

        using var anonymous = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var guest = await anonymous.GetAsync("/api/onboarding/catalog");
        Assert.NotEqual(HttpStatusCode.OK, guest.StatusCode);
    }

    [Fact]
    public async Task ADisplayNameWithMarkup_CannotBreakOutOfTheConfigScript()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, NewUser, Roles.Standard);
        client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.NameHeaderName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.NameHeaderName, "</script><img src=x onerror=alert(1)>");

        var html = await client.GetStringAsync("/Home/Index");
        var config = ConfigJsonOf(html);
        Assert.DoesNotContain("<", config);
        Assert.DoesNotContain("</script><img", html);
        Assert.Equal("</script><img src=x onerror=alert(1)>", JsonDocument.Parse(config).RootElement.GetProperty("fullName").GetString());
    }
}
