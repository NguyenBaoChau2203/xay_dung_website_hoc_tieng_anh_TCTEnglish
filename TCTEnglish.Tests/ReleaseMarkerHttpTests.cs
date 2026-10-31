using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class ReleaseMarkerHttpTests
{
    [Fact]
    public async Task ReleaseMarker_FromWebRoot_ReturnsJsonWithoutCaching()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var env = factory.Services.GetRequiredService<IWebHostEnvironment>();
        Directory.CreateDirectory(env.WebRootPath);
        var markerPath = Path.Combine(env.WebRootPath, "tct-release.json");
        var payload = """{"commit":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","runNumber":42}""";
        await File.WriteAllTextAsync(markerPath, payload);

        try
        {
            using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            using var response = await client.GetAsync("/tct-release.json");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("application/json", response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                response.Headers.TryGetValues("Cache-Control", out var cacheValues)
                || response.Content.Headers.TryGetValues("Cache-Control", out cacheValues),
                "Cache-Control header missing.");
            Assert.Contains(cacheValues!, value => value.Contains("no-store", StringComparison.OrdinalIgnoreCase));

            using var json = JsonDocument.Parse(body);
            Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", json.RootElement.GetProperty("commit").GetString());
            Assert.Equal(42, json.RootElement.GetProperty("runNumber").GetInt32());
            Assert.False(json.RootElement.TryGetProperty("database", out _));
            Assert.False(json.RootElement.TryGetProperty("password", out _));
        }
        finally
        {
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }
    }

    [Fact]
    public async Task ReleaseMarker_MissingFile_ReturnsNotFound()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var env = factory.Services.GetRequiredService<IWebHostEnvironment>();
        var markerPath = Path.Combine(env.WebRootPath, "tct-release.json");
        if (File.Exists(markerPath))
        {
            File.Delete(markerPath);
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/tct-release.json");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
