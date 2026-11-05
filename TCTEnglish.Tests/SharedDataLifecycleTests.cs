using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Infrastructure.SharedData;
using TCTVocabulary.Models;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

public class SharedDataLifecycleTests
{
    private static async Task WithDb(Func<DbflashcardContext, Task> action)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(); await action(db);
    }

    [Fact]
    public Task EveryRegistryFieldAndReferenceMatchesCurrentModel() => WithDb(db =>
    {
        foreach (var spec in SharedDataRegistry.Entities.Values)
        foreach (var field in spec.Fields.Concat(spec.References.Select(r => r.Property)).Concat(spec.Owners).Append(spec.Id))
        { Assert.NotNull(spec.Type.GetProperty(field)); Assert.NotNull(db.Model.FindEntityType(spec.Type)!.FindProperty(field)); }
        return Task.CompletedTask;
    });

    [Fact]
    public void CanonicalHashNormalizesUnicodeEscapesNumbersAndObjectOrder()
    {
        using var a = JsonDocument.Parse("{\"n\":1.00,\"text\":\"\\u0061\"}");
        using var b = JsonDocument.Parse("{\"text\":\"a\",\"n\":1}");
        Assert.Equal(SharedDataFormat.Canonical(a.RootElement), SharedDataFormat.Canonical(b.RootElement));
    }

    [Theory]
    [InlineData("Server=127.0.0.1;Host=103.28.36.90;Port=3307;Database=tctenglish_dev;User=tct_dev", "Development", "local", false)]
    [InlineData("Server=103.28.36.90;Database=nhtctsnn_tctenglish;User=x", "Development", "local", false)]
    [InlineData("Server=127.0.0.1;Port=3307;Database=another_dev;User=tct_dev", "Development", "local", false)]
    [InlineData("Server=127.0.0.1;Database=nhtctsnn_tctenglish;User=x", "Production", "production", false)]
    public void TargetGuardRejectsRemoteWrongDatabaseAndUnapprovedProduction(string connection, string env, string target, bool approved)
        => Assert.Throws<SharedDataException>(() => SharedDataCommand.AssertTarget(connection, env, target, approved));

    [Fact]
    public Task ChildReferencesMapToDestinationIdsAndHistorySurvivesUpdate() => WithDb(async db =>
    {
        using var dir = new TempFolder();
        db.ReadingPassages.Add(new() { Id = 700, Title = "Unrelated local lesson", Content = "local", Level = "A1", IsPublished = true });
        db.Users.Add(new() { UserId = 910, Email = "shared-test@example.test", PasswordHash = "not-for-login", Role = "Standard" }); await db.SaveChangesAsync();
        var question = new SharedRecord("team.question", "ReadingQuestion", new() { ["QuestionText"] = JsonSerializer.SerializeToElement("A question"), ["OrderIndex"] = JsonSerializer.SerializeToElement(1) }, new() { ["PassageId"] = "team.reading.one" });
        var one = await WriteAsync(dir.Path, Package(1, question, Reading()));
        var engine = new SharedDataEngine(db, dir.Path, new()); await engine.ApplyAsync([one]);
        var bound = await db.SharedDataBindings.SingleAsync(b => b.Key == "team.reading.one"); Assert.NotEqual(1, bound.EntityId);
        Assert.Equal(bound.EntityId, (await db.ReadingQuestions.SingleAsync()).PassageId);
        db.UserReadingHistories.Add(new() { UserId = 910, ReadingPassageId = bound.EntityId, IsCompleted = true, Score = 99, ViewedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
        var two = await WriteAsync(dir.Path, Package(2, question, Reading(title: "Revised content"))); await engine.ApplyAsync([one, two]);
        Assert.Equal(bound.EntityId, (await db.ReadingQuestions.SingleAsync()).PassageId);
        Assert.Equal(99, (await db.UserReadingHistories.SingleAsync()).Score);
        Assert.Equal("Unrelated local lesson", (await db.ReadingPassages.SingleAsync(r => r.Id == 700)).Title);
        var deletion = Package(3) with { DeleteKeys = ["team.reading.one"] };
        var three = await WriteAsync(dir.Path, deletion);
        await Assert.ThrowsAsync<SharedDataException>(() => engine.ApplyAsync([one, two, three]));
        Assert.Single(await db.UserReadingHistories.ToListAsync());
    });

    [Fact]
    public Task ExportRegistersAuthorIdsAndNewVersionWithoutDuplicatingOriginal() => WithDb(async db =>
    {
        using var dir = new TempFolder();
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "catalog.json"), "{\"schemaVersion\":1,\"releases\":[]}");
        var source = new ReadingPassage { Title = "Bài đọc của nhóm", Content = "Public learning content", Level = "A1", IsPublished = true, CreatedAt = DateTime.UtcNow };
        db.ReadingPassages.Add(source); await db.SaveChangesAsync();
        var originalId = source.Id;
        var first = await new SharedDataExporter(db, dir.Path, new()).ExportAsync(dir.Path, "ReadingPassage", source.Id, "team.author.reading", "team.author");
        var packages = await SharedDataFormat.LoadAsync(dir.Path);
        await new SharedDataEngine(db, dir.Path, new()).ApplyAsync(packages);
        Assert.Equal(originalId, (await db.ReadingPassages.SingleAsync()).Id);
        source.Title = "Nội dung đã sửa"; await db.SaveChangesAsync();
        var second = await new SharedDataExporter(db, dir.Path, new()).ExportAsync(dir.Path, "ReadingPassage", source.Id, "team.author.reading", "team.author");
        Assert.Equal(1, first.Version); Assert.Equal(2, second.Version);
        await new SharedDataEngine(db, dir.Path, new()).ApplyAsync(await SharedDataFormat.LoadAsync(dir.Path));
        Assert.Single(await db.ReadingPassages.ToListAsync());
        Assert.Equal("Nội dung đã sửa", (await db.ReadingPassages.SingleAsync()).Title);
    });

    [Fact]
    public Task RewrittenAppliedVersionIsRejectedWithoutChangingContent() => WithDb(async db =>
    {
        using var dir = new TempFolder(); var first = await WriteAsync(dir.Path, Package(1, Reading()));
        var engine = new SharedDataEngine(db, dir.Path, new()); await engine.ApplyAsync([first]);
        var changed = new LoadedSharedPackage(first.Release with { Sha256 = new string('a', 64) }, Package(1, Reading(title: "Tampered")), first.Directory);
        await Assert.ThrowsAsync<SharedDataException>(() => engine.ApplyAsync([changed]));
        Assert.Equal("Shared reading", (await db.ReadingPassages.SingleAsync()).Title);
    });

    [Fact]
    public Task MissingDependencyRollsBackEntirePendingBatch() => WithDb(async db =>
    {
        using var dir = new TempFolder(); var first = await WriteAsync(dir.Path, Package(1, Reading()));
        var bad = new LoadedSharedPackage(new("team.other", 1, "other.json", new string('b',64)), new(1,"team.other",1,["not.installed:1"],[Reading("team.other.reading")],[],[]),dir.Path);
        await Assert.ThrowsAsync<SharedDataException>(() => new SharedDataEngine(db, dir.Path, new()).ApplyAsync([first,bad]));
        Assert.Empty(await db.ReadingPassages.ToListAsync()); Assert.Empty(await db.SharedDataApplications.ToListAsync());
    });

    [Fact]
    public Task FileConflictRollsBackRowsAndPreservesExistingFile() => WithDb(async db =>
    {
        using var dir = new TempFolder(); var content = new byte[] {137,80,78,71,13,10,26,10,0,0,0,0};
        var hash = SharedDataFormat.Hash(content); var file = hash + ".png";
        var record = Reading(); record.Values["ImageUrl"] = JsonSerializer.SerializeToElement("asset:"+file);
        var package = Package(1,record) with { Assets = [new(file,hash)] };
        var loaded = await WriteAsync(dir.Path,package);
        Directory.CreateDirectory(Path.Combine(loaded.Directory,"assets")); await File.WriteAllBytesAsync(Path.Combine(loaded.Directory,"assets",file),content);
        var destination=Path.Combine(dir.Path,"wwwroot","uploads","shared"); Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination,file),"Keep my local file");
        await Assert.ThrowsAsync<SharedDataException>(()=>new SharedDataEngine(db,Path.Combine(dir.Path,"wwwroot"),new()).ApplyAsync([loaded]));
        Assert.Empty(await db.ReadingPassages.ToListAsync()); Assert.Equal("Keep my local file",await File.ReadAllTextAsync(Path.Combine(destination,file)));
    });

    [Fact]
    public void RegistryDoesNotExportOtherClassesWhenSharingVocabularyFolder()
    {
        Assert.False(SharedDataRegistry.IsOwnedChild("Folder","ClassFolder","FolderId"));
        Assert.True(SharedDataRegistry.IsOwnedChild("Class","ClassFolder","ClassId"));
        Assert.Throws<SharedDataException>(()=>SharedDataRegistry.AssertShareable(new ListeningLesson {OwnerUserId=42}));
        Assert.Throws<SharedDataException>(()=>SharedDataRegistry.AssertShareable(new Class {HasPassword=true}));
    }

    [Fact]
    public Task AllNineteenContentKindsExportAndImportWithDifferentOwnerAndEntityIds() => WithDb(async source =>
    {
        using var folder=new TempFolder();
        await File.WriteAllTextAsync(Path.Combine(folder.Path,"catalog.json"),"{\"schemaVersion\":1,\"releases\":[]}");
        source.Users.Add(new(){UserId=111,Email="source@example.test",PasswordHash="fixture",FullName="Source",Role="Admin",CreatedAt=DateTime.UtcNow});
        await source.SaveChangesAsync();
        var originals=new Dictionary<string,object>(StringComparer.Ordinal);
        var pending=SharedDataRegistry.Entities.ToList();
        while(pending.Count>0)
        {
            var layer=pending.Where(p=>p.Value.References.Where(r=>!r.Optional).All(r=>originals.ContainsKey(r.Kind))).ToList();
            Assert.NotEmpty(layer);
            foreach(var entry in layer)
            {
                var spec=entry.Value;var row=Activator.CreateInstance(spec.Type)!;
                foreach(var field in spec.Fields)
                {
                    var property=spec.Property(field);var type=Nullable.GetUnderlyingType(property.PropertyType)??property.PropertyType;
                    object? value=type==typeof(string)?field switch {"Level"=>"A1","CorrectAnswer"=>"A","YoutubeId"=>"9F8o8eH_Hms","Duration"=>"00:10","NormalizedHash"=>new string('a',64),"SourceType"=>"system","TranscriptSource"=>"manual",_=>"Sample"}:type==typeof(bool)?true:type==typeof(decimal)?9.5m:Activator.CreateInstance(type);
                    if(field.EndsWith("Url",StringComparison.Ordinal))value=null;
                    property.SetValue(row,value);
                }
                foreach(var reference in spec.References)spec.Property(reference.Property).SetValue(row,reference.Optional && (!originals.ContainsKey(reference.Kind) || reference.Kind==entry.Key)?null:SharedDataRegistry.Get(reference.Kind).GetId(originals[reference.Kind]));
                foreach(var owner in spec.Owners)spec.Property(owner).SetValue(row,111);
                foreach(var property in spec.Type.GetProperties().Where(p=>p.PropertyType==typeof(DateTime) && p.CanWrite))property.SetValue(row,DateTime.UtcNow);
                source.Add(row);await source.SaveChangesAsync();originals[entry.Key]=row;pending.Remove(entry);
            }
        }
        foreach(var kind in new[]{"Class","ReadingPassage","ListeningLesson","SpeakingPlaylist","WritingExercise","PremiumPlan"})
        {
            var key="team.full."+kind.ToLowerInvariant();var id=SharedDataRegistry.Get(kind).GetId(originals[kind]);
            await new SharedDataExporter(source,folder.Path,new(){{"team-owner",111}}).ExportAsync(folder.Path,kind,id,key,key);
        }
        var packages=await SharedDataFormat.LoadAsync(folder.Path);
        Assert.Equal(19,packages.Sum(p=>p.Package.Records.Count));
        await WithDb(async destination=>
        {
            destination.Users.Add(new(){UserId=444,Email="destination@example.test",PasswordHash="different",FullName="Destination",Role="Admin",CreatedAt=DateTime.UtcNow});await destination.SaveChangesAsync();
            destination.ReadingPassages.Add(new(){Id=700,Title="Unrelated",Content="Keep",Level="A1",CreatedAt=DateTime.UtcNow});await destination.SaveChangesAsync();
            var engine=new SharedDataEngine(destination,folder.Path,new(){{"team-owner",444}});
            await engine.ApplyAsync(packages);await engine.ApplyAsync(packages);
            Assert.Equal(19,await destination.SharedDataBindings.CountAsync());
            Assert.Equal(444,(await destination.Classes.SingleAsync()).OwnerId);
            Assert.Equal(444,(await destination.Sets.SingleAsync()).OwnerId);
            Assert.Equal(444,(await destination.WritingAcceptedAnswers.SingleAsync()).ApprovedByUserId);
            Assert.Equal("Unrelated",(await destination.ReadingPassages.SingleAsync(r=>r.Id==700)).Title);
            Assert.Single(await destination.Users.ToListAsync());
        });
    });

    [Fact]
    public Task CuratedMediaRoundTripRepairsMissingFileWithoutDuplicatingContent() => WithDb(async source=>
    {
        using var folder=new TempFolder();using var destination=new TempFolder();
        await File.WriteAllTextAsync(Path.Combine(folder.Path,"catalog.json"),"{\"schemaVersion\":1,\"releases\":[]}");
        Directory.CreateDirectory(Path.Combine(folder.Path,"images"));
        byte[] image=[137,80,78,71,13,10,26,10];await File.WriteAllBytesAsync(Path.Combine(folder.Path,"images","lesson.png"),image);
        var row=new ReadingPassage{Title="Selected image",Content="Public",Level="A1",ImageUrl="/images/lesson.png",CreatedAt=DateTime.UtcNow};source.Add(row);await source.SaveChangesAsync();
        await new SharedDataExporter(source,folder.Path,new()).ExportAsync(folder.Path,"ReadingPassage",row.Id,"team.media.reading","team.media");
        var packages=await SharedDataFormat.LoadAsync(folder.Path);Assert.Single(packages.Single().Package.Assets);
        await WithDb(async db=>
        {
            var engine=new SharedDataEngine(db,destination.Path,new());await engine.ApplyAsync(packages);
            var imported=await db.ReadingPassages.SingleAsync();var target=SharedDataFormat.SafePath(destination.Path,imported.ImageUrl!.TrimStart('/'));
            Assert.Equal(image,await File.ReadAllBytesAsync(target));File.Delete(target);
            await engine.ApplyAsync(packages);Assert.Equal(image,await File.ReadAllBytesAsync(target));Assert.Single(await db.ReadingPassages.ToListAsync());
        });
    });
}
