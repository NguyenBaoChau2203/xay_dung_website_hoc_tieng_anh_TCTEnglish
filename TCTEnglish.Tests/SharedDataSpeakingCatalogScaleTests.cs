using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Infrastructure.SharedData;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;
using Xunit.Abstractions;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class SharedDataSpeakingCatalogScaleTests(ITestOutputHelper output)
{
    [MariaDbAcceptanceFact]
    public async Task FreshFullCatalogAndNoOpRepeatPreserveOriginalAndPrivateLearnerRows()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        await using var factory = new MariaDbAcceptanceFactory(connection);
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var original = await db.SpeakingSentences.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var packages = await SharedDataFormat.LoadAsync(Path.Combine(repo, "data/shared"));
        var firstPublic = packages.SelectMany(p => p.Package.Records).First(r => r.Kind == "SpeakingVideo");
        var personal = new SpeakingVideo { Title = firstPublic.Values["Title"].GetString()!,
            YoutubeId = firstPublic.Values["YoutubeId"].GetString()!, OwnerUserId = TestDataIds.UserId,
            SpeakingSentences = [new() { Text = "Private learner cue", StartTime = 0, EndTime = 1 }] };
        db.SpeakingVideos.Add(personal);
        db.UserSpeakingProgresses.Add(new() { UserId = TestDataIds.UserId, SentenceId = original[0].Id, TotalScore = 93 });
        await db.SaveChangesAsync();
        var engine = new SharedDataEngine(db, Path.Combine(repo, ".appdata/local/shared-scale-webroot"),
            new() { ["team-owner"] = TestDataIds.AdminUserId });
        var watch = Stopwatch.StartNew();
        await engine.ApplyAsync(packages);
        output.WriteLine($"Fresh 271-package import: {watch.Elapsed.TotalSeconds:F3}s; {engine.Report.Count} record operations.");
        Assert.Equal(90, await db.SharedDataBindings.CountAsync(b => b.Kind == "SpeakingVideo" && b.PackageId.StartsWith("speaking-youtube-")));
        Assert.Equal(16262, await db.SharedDataBindings.CountAsync(b => b.Kind == "SpeakingSentence" && b.PackageId.StartsWith("speaking-youtube-")));
        Assert.Equal(TestDataIds.UserId, (await db.SpeakingVideos.AsNoTracking().SingleAsync(v => v.Id == personal.Id)).OwnerUserId);
        Assert.Equal("Private learner cue", (await db.SpeakingSentences.AsNoTracking().SingleAsync(s => s.VideoId == personal.Id)).Text);
        Assert.Equal(93, (await db.UserSpeakingProgresses.AsNoTracking().SingleAsync()).TotalScore);
        foreach (var sentence in original)
        {
            var stored = await db.SpeakingSentences.AsNoTracking().SingleAsync(s => s.Id == sentence.Id);
            Assert.Equal(sentence.Text, stored.Text); Assert.Equal(sentence.VideoId, stored.VideoId);
        }
        var bindings = await db.SharedDataBindings.AsNoTracking().ToDictionaryAsync(b => b.Key, b => b.EntityId);
        watch.Restart(); await engine.ApplyAsync(packages);
        output.WriteLine($"No-op repeat: {watch.Elapsed.TotalSeconds:F3}s.");
        Assert.Empty(engine.Report);
        Assert.Equal(bindings.OrderBy(p => p.Key), (await db.SharedDataBindings.AsNoTracking().ToDictionaryAsync(b => b.Key, b => b.EntityId)).OrderBy(p => p.Key));
    }
}
