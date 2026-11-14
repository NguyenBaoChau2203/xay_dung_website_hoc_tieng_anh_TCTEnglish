using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Infrastructure.SharedData;
using TCTVocabulary.Models;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

public sealed class SharedDataExportLineEndingTests
{
    [Fact]
    public async Task ExportNewVersionUsesLfAndLeavesLegacyCrLfPackageBytesReadableAndUnchanged()
    {
        using var dir = new TempFolder();
        var legacy = Package(1, Reading()) with { RootKey = "team.reading.one" };
        var legacyJson = new JsonSerializerOptions(SharedDataFormat.Json) { NewLine = "\r\n" };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(legacy, legacyJson);
        var relative = "packages/team.demo/1/package.json";
        var legacyPath = Path.Combine(dir.Path, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        await File.WriteAllBytesAsync(legacyPath, bytes);
        var release = new SharedRelease("team.demo", 1, relative, SharedDataFormat.Hash(bytes));
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "catalog.json"), JsonSerializer.Serialize(new SharedCatalog(1, [release]), legacyJson));
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var engine = new SharedDataEngine(db, dir.Path, new());
        await engine.ApplyAsync(await SharedDataFormat.LoadAsync(dir.Path));
        var root = await db.ReadingPassages.SingleAsync();
        root.IsPublished = false;
        await db.SaveChangesAsync();
        var exported = await new SharedDataExporter(db, dir.Path, new()).ExportAsync(dir.Path, "ReadingPassage", root.Id, "team.reading.one", "team.demo");
        Assert.Equal(2, exported.Version);
        var newBytes = await File.ReadAllBytesAsync(Path.Combine(dir.Path, exported.Path));
        Assert.DoesNotContain("\r", Encoding.UTF8.GetString(newBytes));
        Assert.Equal(exported.Sha256, SharedDataFormat.Hash(newBytes));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(legacyPath));
        var loaded = await SharedDataFormat.LoadAsync(dir.Path);
        Assert.Equal(2, loaded.Count);
        await engine.ApplyAsync(loaded);
        Assert.False((await db.ReadingPassages.SingleAsync()).IsPublished);
    }
}
