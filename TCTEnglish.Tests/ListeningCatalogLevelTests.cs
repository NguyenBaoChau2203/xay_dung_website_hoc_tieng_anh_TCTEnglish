using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class ListeningCatalogLevelTests
{
    [Fact]
    public async Task PublicC2AppearsInOrderedLevelsCardsTopicFilterAndRenderedCatalog()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.ListeningLessons.AddRange(
            new() { Title = "Public C2 fixture", Level = "C2", Topic = "Proficiency", IsPublished = true },
            new() { Title = "Private C2 fixture", Level = "C2", Topic = "Private topic", OwnerUserId = TestDataIds.UserId, IsPublished = true },
            new() { Title = "Draft C2 fixture", Level = "C2", Topic = "Draft topic", IsPublished = false });
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IListeningService>();
        var catalog = await service.GetIndexViewModelAsync(null, null);
        Assert.Equal(new[] { "A1", "A2", "B1", "B2", "C1", "C2" }, catalog.Levels.Select(x => x.Key));
        Assert.Equal("Public C2 fixture", Assert.Single(catalog.LessonsByLevel["C2"]).Title);
        Assert.Equal(1, catalog.Levels.Single(x => x.Key == "C2").LessonCount);
        Assert.DoesNotContain("Private topic", catalog.Topics);
        Assert.DoesNotContain("Draft topic", catalog.Topics);
        var filtered = await service.GetIndexViewModelAsync("C2", "Proficiency");
        Assert.Equal("Public C2 fixture", Assert.Single(filtered.LessonsByLevel["C2"]).Title);
        Assert.Empty((await service.GetIndexViewModelAsync("C2", "Other")).LessonsByLevel);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/Home/Listening");
        Assert.Contains("Public C2 fixture", html);
        Assert.Contains("data-level-section=\"C2\"", html); // the redesigned page marks each level section
        Assert.DoesNotContain("Private C2 fixture", html);
        Assert.DoesNotContain("Draft C2 fixture", html);
    }
}
