using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Infrastructure.SharedData;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public class SharedDataPackageTests
{
    public static SharedRecord Reading(string key = "team.reading.one", string title = "Shared reading") => new(key, "ReadingPassage",
        new() { ["Title"] = JsonSerializer.SerializeToElement(title), ["Content"] = JsonSerializer.SerializeToElement("Hello, this is shared content."), ["Level"] = JsonSerializer.SerializeToElement("A1"), ["Topic"] = JsonSerializer.SerializeToElement("Team"), ["ImageUrl"] = JsonSerializer.SerializeToElement<string?>(null), ["IsPublished"] = JsonSerializer.SerializeToElement(true) }, new());
    public static SharedPackage Package(int version = 1, params SharedRecord[] records) => new(1, "team.demo", version, [], records.ToList(), [], []);
    public static async Task<LoadedSharedPackage> WriteAsync(string root, SharedPackage package)
    {
        var relative = $"packages/{package.Id}/{package.Version}/package.json";
        var dir = Path.GetDirectoryName(Path.Combine(root, relative))!; Directory.CreateDirectory(dir);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(package, SharedDataFormat.Json);
        await File.WriteAllBytesAsync(Path.Combine(dir, "package.json"), bytes);
        var release = new SharedRelease(package.Id, package.Version, relative, SharedDataFormat.Hash(bytes));
        var catalog = File.Exists(Path.Combine(root, "catalog.json")) ? await SharedDataFormat.ReadAsync<SharedCatalog>(Path.Combine(root, "catalog.json"), 2000000) : new SharedCatalog(1, []);
        catalog.Releases.Add(release);
        await File.WriteAllTextAsync(Path.Combine(root, "catalog.json"), JsonSerializer.Serialize(catalog, SharedDataFormat.Json));
        return new(release, package, dir);
    }

    [Fact]
    public async Task Format_RejectsPrivateEntityAndCredentialFields()
    {
        using var folder = new TempFolder();
        await WriteAsync(folder.Path, Package(1, new SharedRecord("team.secret", "User", new(), new())));
        await Assert.ThrowsAsync<SharedDataException>(() => SharedDataFormat.LoadAsync(folder.Path));
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("assets/../outside.json")]
    [InlineData("C:/private.json")]
    [InlineData("assets\\private.json")]
    public void Paths_RejectTraversalAndWindowsSeparators(string relative)
    {
        using var folder = new TempFolder();
        Assert.Throws<SharedDataException>(() => SharedDataFormat.SafePath(folder.Path, relative));
    }

    [Fact]
    public async Task Format_RejectsTamperingBeforeDatabaseAccess()
    {
        using var folder = new TempFolder();
        var loaded = await WriteAsync(folder.Path, Package(1, Reading()));
        await File.AppendAllTextAsync(Path.Combine(loaded.Directory, "package.json"), " ");
        await Assert.ThrowsAsync<SharedDataException>(() => SharedDataFormat.LoadAsync(folder.Path));
    }

    [Fact]
    public async Task FormatRejectsDuplicatePropertiesAndMissingVersionChain()
    {
        using var folder = new TempFolder();
        var file = Path.Combine(folder.Path, "catalog.json");
        await File.WriteAllTextAsync(file, "{\"schemaVersion\":1,\"schemaVersion\":1,\"releases\":[]}");
        await Assert.ThrowsAsync<SharedDataException>(() => SharedDataFormat.LoadAsync(folder.Path));
        File.Delete(file);
        await WriteAsync(folder.Path, Package(2, Reading()));
        var error = await Assert.ThrowsAsync<SharedDataException>(() => SharedDataFormat.LoadAsync(folder.Path));
        Assert.Contains("version gap", error.Message);
    }

    [Fact]
    public void MediaCannotHideExecutableOrHtmlBehindImageExtension()
    {
        Assert.Throws<SharedDataException>(() => SharedDataFormat.AssertMedia("image.png", System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>")));
        SharedDataFormat.AssertMedia("image.png", [137,80,78,71,13,10,26,10]);
        Assert.Throws<SharedDataException>(() => SharedDataFormat.AssertMedia("payload.exe", [77,90]));
    }

    [Fact]
    public async Task Apply_RerunAndUpdatePreserveLocalHistoryAndIds_ConflictRollsBack()
    {
        using var folder = new TempFolder();
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var one = await WriteAsync(folder.Path, Package(1, Reading()));
        var engine = new SharedDataEngine(db, Path.Combine(folder.Path, "wwwroot"), new());
        await engine.ApplyAsync([one]);
        var row = await db.ReadingPassages.SingleAsync(); var id = row.Id;
        await engine.ApplyAsync([one]); Assert.Single(await db.ReadingPassages.ToListAsync());
        var two = await WriteAsync(folder.Path, Package(2, Reading(title: "Updated")));
        await engine.ApplyAsync([one, two]);
        Assert.Equal(id, (await db.ReadingPassages.SingleAsync()).Id);
        row.Title = "My local edit"; await db.SaveChangesAsync();
        var three = await WriteAsync(folder.Path, Package(3, Reading(title: "Another update"), Reading("team.reading.two")));
        await Assert.ThrowsAsync<SharedDataException>(() => engine.ApplyAsync([one, two, three]));
        db.ChangeTracker.Clear();
        Assert.Equal("My local edit", (await db.ReadingPassages.SingleAsync()).Title);
        Assert.Equal(2, await db.SharedDataApplications.CountAsync());
    }

    public sealed class TempFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tct-shared-" + Guid.NewGuid().ToString("N"));
        public TempFolder() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }

    public sealed class SharedTestContext(DbContextOptions<DbflashcardContext> options) : DbflashcardContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            foreach (var property in builder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
                property.SetDefaultValueSql(null);
        }
    }
}
