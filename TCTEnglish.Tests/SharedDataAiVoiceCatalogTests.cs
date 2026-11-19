using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TCTEnglish.Infrastructure.SharedData;
using TCTEnglish.Models;
using TCTEnglish.Services.AI.Voice;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace TCTEnglish.Tests;

public class SharedDataAiVoiceCatalogTests
{
    [Theory]
    [InlineData("AiVoiceCustomTopic")]
    [InlineData("AiVoiceSession")]
    [InlineData("AiVoiceTurn")]
    [InlineData("AiVoiceUsageDay")]
    public void PrivateKindsCannotEnterPackage(string kind) => Assert.Throws<SharedDataException>(() => SharedDataRegistry.Get(kind));

    [Fact]
    public void RejectsPrivateObjectsAndUnpublishedCatalog()
    {
        Assert.Throws<SharedDataException>(() => SharedDataRegistry.AssertShareable(new AiVoiceCustomTopic()));
        Assert.Throws<SharedDataException>(() => SharedDataRegistry.AssertShareable(new AiVoiceSession()));
        Assert.Throws<SharedDataException>(() => SharedDataRegistry.AssertShareable(new AiVoiceCatalogTopic()));
        var spec = SharedDataRegistry.Get("AiVoiceCatalogTopic");
        Assert.Equal(typeof(AiVoiceCatalogTopic), spec.Type);
        Assert.Empty(spec.Owners);
        Assert.Empty(spec.References);
    }

    [Fact]
    public async Task SeedRerunPreservesLocalEdit()
    {
        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await AiVoiceCatalogSeed.SeedAsync(db);
        var row = await db.AiVoiceCatalogTopics.FirstAsync();
        row.Title = "Local reviewed title";
        await db.SaveChangesAsync();
        await AiVoiceCatalogSeed.SeedAsync(db);
        Assert.Equal(6, await db.AiVoiceCatalogTopics.CountAsync());
        Assert.Equal("Local reviewed title", (await db.AiVoiceCatalogTopics.FindAsync(row.Id))!.Title);
    }

    [Fact]
    public async Task ExportImportRerunIsIdempotentAndConflictingUpdateRollsBack()
    {
        var temp = Path.Combine(Path.GetTempPath(), "tct-voice-public-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            await System.IO.File.WriteAllTextAsync(Path.Combine(temp, "catalog.json"), "{\"schemaVersion\":1,\"releases\":[]}");
            await using var sourceConnection = new SqliteConnection("Data Source=:memory:"); await sourceConnection.OpenAsync();
            await using var targetConnection = new SqliteConnection("Data Source=:memory:"); await targetConnection.OpenAsync();
            DbflashcardContext Db(SqliteConnection c) => new(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(c)
                .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>().Options);
            await using var source = Db(sourceConnection); await source.Database.EnsureCreatedAsync();
            await AiVoiceCatalogSeed.SeedAsync(source); var topic = await source.AiVoiceCatalogTopics.FirstAsync();
            await new SharedDataExporter(source, temp, []).ExportAsync(temp, "AiVoiceCatalogTopic", topic.Id, "voice.test", "voice.test");
            var packages = await SharedDataFormat.LoadAsync(temp);
            Assert.Single(packages); Assert.Single(packages[0].Package.Records);
            Assert.Empty(packages[0].Package.Records[0].References);
            await using var target = Db(targetConnection); await target.Database.EnsureCreatedAsync();
            await new SharedDataEngine(target, temp, []).ApplyAsync(packages);
            await new SharedDataEngine(target, temp, []).ApplyAsync(packages);
            Assert.Equal(1, await target.AiVoiceCatalogTopics.CountAsync());
            var imported = await target.AiVoiceCatalogTopics.SingleAsync(); imported.Title = "Private local edit"; await target.SaveChangesAsync();
            topic.Title = "Next reviewed version"; await source.SaveChangesAsync();
            await new SharedDataExporter(source, temp, []).ExportAsync(temp, "AiVoiceCatalogTopic", topic.Id, "voice.test", "voice.test");
            var next = await SharedDataFormat.LoadAsync(temp);
            await Assert.ThrowsAsync<SharedDataException>(() => new SharedDataEngine(target, temp, []).ApplyAsync(next));
            Assert.Equal("Private local edit", (await target.AiVoiceCatalogTopics.AsNoTracking().SingleAsync()).Title);
        }
        finally { Directory.Delete(temp, true); }
    }
}
