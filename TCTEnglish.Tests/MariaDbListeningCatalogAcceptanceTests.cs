using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Infrastructure.ListeningCatalog;
using TCTEnglish.Infrastructure.SharedData;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
[Trait("Category", "MariaDbAcceptance")]
public sealed class MariaDbListeningCatalogAcceptanceTests
{
    [MariaDbAcceptanceFact]
    public async Task CaseSensitiveImportRerunConflictAndPublicPackageRoundTripPreserveHistory()
    {
        await using var factory = new MariaDbAcceptanceFactory(
            Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!,
            Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID"));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        // The shared acceptance factory intentionally retains Listening content.
        // Use a new fixture identity so this test itself can run repeatedly.
        var fixtureId = "A" + Guid.NewGuid().ToString("N")[..10];
        var item = ListeningCatalogImportTests.Item(fixtureId);
        var privateRow = new ListeningLesson { Title = "Private fixture", Level = "B1", Topic = "Private", YoutubeId = item.YoutubeId, OwnerUserId = TestDataIds.UserId, IsPublished = true };
        db.ListeningLessons.Add(privateRow); await db.SaveChangesAsync();
        var importer = new ListeningCatalogImporter(db);
        var staged = new StagedListeningLesson(item, [new("This is a real fixture caption.", 1, 3)]);
        var first = await importer.ImportOneAsync(staged);
        var caseDistinct = await importer.ImportOneAsync(new(ListeningCatalogImportTests.Item("a" + fixtureId[1..]), []));
        Assert.NotEqual(first.LessonId, caseDistinct.LessonId);
        // Prove the provider's ordinary equality would conflate the two IDs.
        Assert.Equal(2, await db.ListeningLessons.CountAsync(x => x.OwnerUserId == null && x.YoutubeId == item.YoutubeId));
        db.UserListeningProgresses.Add(new() { UserId = TestDataIds.UserId, LessonId = first.LessonId, QuizScore = 87 });
        await db.SaveChangesAsync();
        var child = await db.ListeningTranscriptLines.SingleAsync(x => x.LessonId == first.LessonId);
        var repeat = await importer.ImportOneAsync(staged);
        Assert.False(repeat.Created); Assert.Equal(first.LessonId, repeat.LessonId);
        Assert.Equal(child.Id, (await db.ListeningTranscriptLines.SingleAsync(x => x.LessonId == first.LessonId)).Id);
        await Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportOneAsync(new(item with { Level = "C1" }, staged.Captions)));

        using var folder = new TempFolder();
        await File.WriteAllTextAsync(Path.Combine(folder.Path, "catalog.json"), "{\"schemaVersion\":1,\"releases\":[]}");
        var hash = ListeningCatalogImporter.StableHash(item.YoutubeId);
        await new SharedDataExporter(db, folder.Path, new()).ExportAsync(folder.Path, "ListeningLesson", first.LessonId,
            "tct.listening.youtube." + hash, "listening-youtube-" + hash);
        var packages = await SharedDataFormat.LoadAsync(folder.Path);
        var exported = Assert.Single(packages);
        Assert.Equal(2, exported.Package.Records.Count); // one public root and one real caption only
        Assert.DoesNotContain(exported.Package.Records, r => r.Kind.Contains("User", StringComparison.Ordinal));
        var engine = new SharedDataEngine(db, folder.Path, new());
        await engine.ApplyAsync(packages); await engine.ApplyAsync(packages);
        Assert.Equal(87, (await db.UserListeningProgresses.SingleAsync(x => x.LessonId == first.LessonId)).QuizScore);
        Assert.Equal(TestDataIds.UserId, (await db.ListeningLessons.AsNoTracking().SingleAsync(x => x.Id == privateRow.Id)).OwnerUserId);
        Assert.Equal(first.LessonId, (await db.ListeningLessons.AsNoTracking().SingleAsync(x => x.Id == first.LessonId)).Id);
    }
}
