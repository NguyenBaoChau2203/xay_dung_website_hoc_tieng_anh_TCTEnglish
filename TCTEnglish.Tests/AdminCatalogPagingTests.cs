using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AdminCatalogPagingTests
{
    [Theory]
    [InlineData("SpeakingVideoManagement", "btn-delete-video", "closeout-topic")]
    [InlineData("WritingExerciseManagement", "btn-delete-exercise", "closeout-topic")]
    public async Task AdminCatalog_PagesBeforeRender_AndFiltersAcrossPages(string controller, string marker, string topic)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            for (var i = 0; i < 41; i++)
            {
                // The oldest item must remain discoverable through server filters.
                db.SpeakingVideos.Add(new() { Title = "Admin scale " + i, YoutubeId = "synthetic01", Level = "C2", Topic = i == 0 ? topic : "other", CreatedAt = DateTime.UtcNow });
                db.WritingExercises.Add(new() { Title = "Admin scale " + i, Level = "advanced", ContentType = "essays", Topic = i == 0 ? topic : "other", SourceType = "manual", PreviewText = "Synthetic", IsPublished = true, CreatedAt = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
        }
        using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        var first = await admin.GetStringAsync($"/Admin/{controller}");
        Assert.Equal(20, Regex.Matches(first, "class=\"[^\"]*" + marker + "(?:[ \"])" ).Count);
        var second = await admin.GetStringAsync($"/Admin/{controller}?page=2");
        Assert.Contains("page=3", second);
        var filtered = await admin.GetStringAsync($"/Admin/{controller}?topic={topic}");
        Assert.Single(Regex.Matches(filtered, "class=\"[^\"]*" + marker + "(?:[ \"])" ).Cast<Match>());
        Assert.Contains("Admin scale 0", filtered);
        using var standard = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        Assert.Equal(HttpStatusCode.Forbidden, (await standard.GetAsync($"/Admin/{controller}?page=2")).StatusCode);
    }
}
