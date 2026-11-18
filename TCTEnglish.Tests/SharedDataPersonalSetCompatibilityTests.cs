using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Infrastructure.SharedData;
using TCTVocabulary.Models;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

/// <summary>
/// Personal-set columns must not change what shared packages carry: imported sets stay Public, secrets and
/// personal data are never exported, private sets are rejected, and packages written before the new Card
/// fields existed still validate and import.
/// </summary>
public class SharedDataPersonalSetCompatibilityTests
{
    private static readonly string[] LegacyCardFields =
        ["Definition", "Example", "ExampleTranslation", "ImageUrl", "Phonetic", "Term", "Topic"];

    private static async Task WithDb(Func<DbflashcardContext, Task> action)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await action(db);
    }

    private static SharedRecord SetRecord(string key = "team.sets.one") => new(key, "Set",
        new() { ["SetName"] = JsonSerializer.SerializeToElement("Shared set"), ["Description"] = JsonSerializer.SerializeToElement<string?>(null) },
        new() { ["FolderId"] = null, ["OwnerId"] = "owner:system" });

    private static SharedRecord CardRecord(string key, Dictionary<string, JsonElement> values) => new(key, "Card", values, new() { ["SetId"] = "team.sets.one" });

    private static Dictionary<string, JsonElement> LegacyCardValues(string term) => new()
    {
        ["Term"] = JsonSerializer.SerializeToElement(term),
        ["Definition"] = JsonSerializer.SerializeToElement("nghĩa"),
        ["ImageUrl"] = JsonSerializer.SerializeToElement<string?>(null),
        ["Phonetic"] = JsonSerializer.SerializeToElement<string?>(null),
        ["Example"] = JsonSerializer.SerializeToElement<string?>(null),
        ["ExampleTranslation"] = JsonSerializer.SerializeToElement<string?>(null),
        ["Topic"] = JsonSerializer.SerializeToElement<string?>(null),
    };

    [Fact]
    public Task EveryOptionalRegistryFieldExistsOnTheModel() => WithDb(db =>
    {
        foreach (var spec in SharedDataRegistry.Entities.Values)
        foreach (var field in spec.OptionalFields)
        {
            Assert.NotNull(spec.Type.GetProperty(field));
            Assert.NotNull(db.Model.FindEntityType(spec.Type)!.FindProperty(field));
        }
        Assert.Equal(new[] { "PartsOfSpeech", "MeaningEn", "Cefr", "PhoneticUk", "Emoji", "DetailsJson", "ImageSourceJson" }, SharedDataRegistry.Get("Card").OptionalFields);
        foreach (var forbidden in new[] { "PersonalNote", "Visibility", "ShareSlug", "ClonedFromSetId", "IsSuspended" })
            Assert.DoesNotContain(SharedDataRegistry.Entities.Values.SelectMany(s => s.AllFields), f => f == forbidden);
        return Task.CompletedTask;
    });

    [Fact]
    public Task LegacyPackageWithoutNewCardFieldsStillImportsAsPublicSetWithDefaults() => WithDb(async db =>
    {
        using var dir = new TempFolder();
        var package = Package(1, SetRecord(), CardRecord("team.sets.one.c1", LegacyCardValues("apple")));
        var loaded = await WriteAsync(dir.Path, package);
        var engine = new SharedDataEngine(db, Path.Combine(dir.Path, "wwwroot"), new());
        await engine.ApplyAsync([loaded]);
        await engine.ApplyAsync([loaded]); // rerun is a no-op

        var set = await db.Sets.SingleAsync();
        Assert.Equal("Public", set.Visibility);
        Assert.Null(set.ShareSlug);
        Assert.Null(set.ClonedFromSetId);
        var card = await db.Cards.SingleAsync();
        Assert.Equal("apple", card.Term);
        Assert.Null(card.PartsOfSpeech);
        Assert.Null(card.DetailsJson);
        Assert.False(card.AiGenerated);
        Assert.Null(card.PersonalNote);
        Assert.Empty(await engine.InspectAsync([loaded]) is { } report ? report.Where(r => r.StartsWith("local-edit")) : []);
    });

    [Fact]
    public Task NewCardFieldsInAPackageAreImportedAndOldKeysStayOptional() => WithDb(async db =>
    {
        using var dir = new TempFolder();
        var values = LegacyCardValues("run");
        values["PartsOfSpeech"] = JsonSerializer.SerializeToElement("verb,noun");
        values["Cefr"] = JsonSerializer.SerializeToElement("A1");
        values["DetailsJson"] = JsonSerializer.SerializeToElement("{\"synonyms\":[\"jog\"]}");
        var loaded = await WriteAsync(dir.Path, Package(1, SetRecord(), CardRecord("team.sets.one.c1", values)));
        await new SharedDataEngine(db, Path.Combine(dir.Path, "wwwroot"), new()).ApplyAsync([loaded]);

        var card = await db.Cards.SingleAsync();
        Assert.Equal("verb,noun", card.PartsOfSpeech);
        Assert.Equal("A1", card.Cefr);
        Assert.Contains("jog", card.DetailsJson);
        Assert.Null(card.MeaningEn);
    });

    [Fact]
    public async Task UnknownCardFieldIsStillRejected()
    {
        using var dir = new TempFolder();
        var values = LegacyCardValues("x");
        values["PersonalNote"] = JsonSerializer.SerializeToElement("secret");
        var package = Package(1, SetRecord(), CardRecord("team.sets.one.c1", values));
        var relative = $"packages/{package.Id}/{package.Version}/package.json";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir.Path, relative))!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(package, SharedDataFormat.Json);
        await File.WriteAllBytesAsync(Path.Combine(dir.Path, relative), bytes);
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "catalog.json"),
            JsonSerializer.Serialize(new SharedCatalog(1, [new SharedRelease(package.Id, package.Version, relative, SharedDataFormat.Hash(bytes))]), SharedDataFormat.Json));

        await Assert.ThrowsAsync<SharedDataException>(() => SharedDataFormat.LoadAsync(dir.Path));
    }

    [Fact]
    public Task ExportNeverCarriesPersonalNoteVisibilitySlugOrCloneSource_AndDefaultCardsKeepLegacyKeys() => WithDb(async db =>
    {
        using var dir = new TempFolder();
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "catalog.json"), "{\"schemaVersion\":1,\"releases\":[]}");
        db.Users.Add(new User { UserId = 111, Email = "author@example.test", PasswordHash = "x", Role = "Admin", CreatedAt = DateTime.UtcNow });
        var set = new Set { SetName = "Public deck", OwnerId = 111, Visibility = "Public", ShareSlug = "abcdefghij", ClonedFromSetId = 5, CreatedAt = DateTime.UtcNow };
        set.Cards.Add(new Card { Term = "plain", Definition = "đơn giản" });
        set.Cards.Add(new Card { Term = "rich", Definition = "phong phú", PersonalNote = "TOP-SECRET-NOTE", PartsOfSpeech = "adj", Cefr = "B2", AiGenerated = true });
        db.Sets.Add(set);
        await db.SaveChangesAsync();

        await new SharedDataExporter(db, dir.Path, new() { { "team-owner", 111 } }).ExportAsync(dir.Path, "Set", set.SetId, "team.export.set", "team.export");
        var packages = await SharedDataFormat.LoadAsync(dir.Path);
        var raw = await File.ReadAllTextAsync(Directory.GetFiles(dir.Path, "package.json", SearchOption.AllDirectories).Single());

        Assert.DoesNotContain("TOP-SECRET-NOTE", raw);
        Assert.DoesNotContain("PersonalNote", raw);
        Assert.DoesNotContain("abcdefghij", raw);
        Assert.DoesNotContain("ShareSlug", raw);
        Assert.DoesNotContain("Visibility", raw);
        Assert.DoesNotContain("ClonedFromSetId", raw);
        Assert.DoesNotContain("AiGenerated", raw);

        var cards = packages.Single().Package.Records.Where(r => r.Kind == "Card").ToList();
        var plain = cards.Single(c => c.Values["Term"].GetString() == "plain");
        Assert.Equal(LegacyCardFields, plain.Values.Keys.Order(StringComparer.Ordinal).ToArray());
        var rich = cards.Single(c => c.Values["Term"].GetString() == "rich");
        Assert.Equal("adj", rich.Values["PartsOfSpeech"].GetString());
        Assert.Equal("B2", rich.Values["Cefr"].GetString());
    });

    [Fact]
    public Task ExportingAPrivateSetIsRejected() => WithDb(async db =>
    {
        using var dir = new TempFolder();
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "catalog.json"), "{\"schemaVersion\":1,\"releases\":[]}");
        db.Users.Add(new User { UserId = 111, Email = "author@example.test", PasswordHash = "x", Role = "Admin", CreatedAt = DateTime.UtcNow });
        var set = new Set { SetName = "Mine", OwnerId = 111, Visibility = "Private", CreatedAt = DateTime.UtcNow };
        set.Cards.Add(new Card { Term = "a", Definition = "b" });
        db.Sets.Add(set);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SharedDataException>(() =>
            new SharedDataExporter(db, dir.Path, new() { { "team-owner", 111 } }).ExportAsync(dir.Path, "Set", set.SetId, "team.private.set", "team.private"));
        Assert.Contains("Private learner sets", ex.Message);
        Assert.Throws<SharedDataException>(() => SharedDataRegistry.AssertShareable(new Set { Visibility = "Private" }));
        SharedDataRegistry.AssertShareable(new Set());
    });
}
