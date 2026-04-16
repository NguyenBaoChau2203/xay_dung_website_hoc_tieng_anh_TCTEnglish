using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SpeakingLegacyNullMetadataIntegrationTests
{
    [Fact]
    public async Task SpeakingIndex_LoadsWhenLegacyMetadataContainsNulls()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await ConvertSpeakingVideosToLegacyNullableSchemaAsync(factory);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Speaking/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("Goals Speaking Video", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpeakingPractice_LoadsWhenLegacyMetadataContainsNulls()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await ConvertSpeakingVideosToLegacyNullableSchemaAsync(factory);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync($"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("Goals Speaking Video", body, StringComparison.Ordinal);
        Assert.Contains("youtube-player", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminSpeakingEdit_LoadsWhenLegacyMetadataContainsNulls()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await ConvertSpeakingVideosToLegacyNullableSchemaAsync(factory);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        using var response = await client.GetAsync($"/Admin/SpeakingVideoManagement/Edit/{TestDataIds.SpeakingVideoId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("Goals Speaking Video", body, StringComparison.Ordinal);
        Assert.Contains("name=\"PlaylistId\"", body, StringComparison.Ordinal);
    }

    private static async Task ConvertSpeakingVideosToLegacyNullableSchemaAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE SpeakingVideos_Legacy (
                Id INTEGER NOT NULL CONSTRAINT PK_SpeakingVideos PRIMARY KEY,
                PlaylistId INTEGER NULL,
                Title TEXT NOT NULL,
                YoutubeId TEXT NOT NULL,
                Level TEXT NULL,
                Topic TEXT NULL,
                ThumbnailUrl TEXT NULL,
                Duration TEXT NULL
            );
            """);
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO SpeakingVideos_Legacy (Id, PlaylistId, Title, YoutubeId, Level, Topic, ThumbnailUrl, Duration)
            SELECT Id, PlaylistId, Title, YoutubeId, Level, Topic, ThumbnailUrl, Duration
            FROM SpeakingVideos;
            """);
        await context.Database.ExecuteSqlRawAsync("DROP TABLE SpeakingVideos;");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE SpeakingVideos_Legacy RENAME TO SpeakingVideos;");
        await context.Database.ExecuteSqlRawAsync("CREATE INDEX IX_SpeakingVideos_PlaylistId ON SpeakingVideos (PlaylistId);");
        await context.Database.ExecuteSqlRawAsync(
            $"""
            UPDATE SpeakingVideos
            SET Level = NULL,
                Topic = NULL,
                PlaylistId = NULL
            WHERE Id = {TestDataIds.SpeakingVideoId};
            """);
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
    }
}
