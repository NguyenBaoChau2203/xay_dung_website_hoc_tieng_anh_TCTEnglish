using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Infrastructure.SharedData;
using TCTEnglish.Infrastructure.SpeakingCatalog;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
[Trait("Category", "MariaDbAcceptance")]
public sealed class MariaDbSpeakingCatalogTransferTests
{
    [MariaDbAcceptanceFact]
    public async Task WithdrawalsOnCleanRecipientAndSpeakingRoundTripPreserveOrdinalIdentityAndHistory()
    {
        await using var factory = new MariaDbAcceptanceFactory(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!, Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID"));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var real = (await SharedDataFormat.LoadAsync(Path.Combine(repo, "data/shared"))).Where(x => x.Package.Id.StartsWith("listening-youtube-", StringComparison.Ordinal)).ToList();
        Assert.Equal(180, real.Count);
        var engine = new SharedDataEngine(db, Path.Combine(repo, "TCTEnglish/wwwroot"), new());
        await engine.ApplyAsync(real); await engine.ApplyAsync(real);
        var importedIds = await db.SharedDataBindings.Where(x => x.Kind == "ListeningLesson" && x.PackageId.StartsWith("listening-youtube-")).Select(x => x.EntityId).ToListAsync();
        Assert.Equal(90, importedIds.Count);
        Assert.False(await db.ListeningLessons.AnyAsync(x => importedIds.Contains(x.Id) && x.IsPublished));

        var id = "A" + Guid.NewGuid().ToString("N")[..10];
        var item = SpeakingCatalogTransferTests.Item(id);
        var distinct = item with { YoutubeId = "a" + id[1..], WatchUrl = "https://www.youtube.com/watch?v=a" + id[1..], ThumbnailUrl = "https://img.youtube.com/vi/a" + id[1..] + "/hqdefault.jpg" };
        var source = SpeakingCatalogTransferTests.Source(item);
        var other = SpeakingCatalogTransferTests.Source(distinct);
        var personal = SpeakingCatalogTransferTests.Source(item); personal.OwnerUserId = TestDataIds.UserId;
        db.ListeningLessons.AddRange(source, other, personal); await db.SaveChangesAsync();
        db.UserListeningProgresses.Add(new() { LessonId = source.Id, UserId = TestDataIds.UserId, QuizScore = 89 }); await db.SaveChangesAsync();
        var transfer = new SpeakingCatalogTransfer(db);
        var first = await transfer.PublishAsync(SpeakingCatalogTransferTests.Ready(item));
        var second = await transfer.PublishAsync(SpeakingCatalogTransferTests.Ready(distinct));
        Assert.NotEqual(first.SpeakingId, second.SpeakingId);
        Assert.Equal(2, await db.SpeakingVideos.CountAsync(x => x.OwnerUserId == null && x.YoutubeId == id)); // MariaDB collation conflates case
        var cues = await db.SpeakingSentences.Where(x => x.VideoId == first.SpeakingId).OrderBy(x => x.StartTime).Select(x => x.Id).ToListAsync();
        db.UserSpeakingProgresses.Add(new() { UserId = TestDataIds.UserId, SentenceId = cues[0], TotalScore = 93 }); await db.SaveChangesAsync();
        using var folder = new TempFolder();
        await File.WriteAllTextAsync(Path.Combine(folder.Path, "catalog.json"), "{\"schemaVersion\":1,\"releases\":[]}");
        await new SharedDataExporter(db, folder.Path, new()).ExportAsync(folder.Path, "SpeakingVideo", first.SpeakingId!.Value, first.SpeakingKey, "speaking-youtube-" + SpeakingCatalogTransfer.HashId(id));
        var speaking = await SharedDataFormat.LoadAsync(folder.Path);
        Assert.All(speaking.SelectMany(x => x.Package.Records), x => Assert.Contains(x.Kind, new[] { "SpeakingVideo", "SpeakingSentence" }));
        await new SharedDataEngine(db, folder.Path, new()).ApplyAsync(speaking);
        await new SharedDataEngine(db, folder.Path, new()).ApplyAsync(speaking);
        var repeat = await transfer.PublishAsync(SpeakingCatalogTransferTests.Ready(item));
        Assert.False(repeat.SpeakingCreated);
        Assert.Equal(cues, await db.SpeakingSentences.Where(x => x.VideoId == first.SpeakingId).OrderBy(x => x.StartTime).Select(x => x.Id).ToListAsync());
        Assert.True(await db.UserSpeakingProgresses.AnyAsync(x => x.SentenceId == cues[0] && x.TotalScore == 93));
        Assert.Equal(89, (await db.UserListeningProgresses.SingleAsync(x => x.LessonId == source.Id)).QuizScore);
        Assert.True((await db.ListeningLessons.SingleAsync(x => x.Id == personal.Id)).IsPublished);
        var replacement = SpeakingCatalogTransferTests.Item("Q" + Guid.NewGuid().ToString("N")[..10], 300) with { ReplacementOfYoutubeId = item.YoutubeId };
        var replacementWriter = new SpeakingCatalogTransfer(db, new Dictionary<string, SpeakingCatalogEntry>(StringComparer.Ordinal) { [item.YoutubeId] = item });
        var localDraft = SpeakingCatalogTransferTests.Ready(replacement);
        localDraft = localDraft with { Proof = localDraft.Proof with { FullSpeechReviewed = false, Reviews = [], Coverage = [], UnresolvedFlags = ["local UAT before staging"] } };
        var replaced = await replacementWriter.ImportForLocalTestAsync(localDraft);
        Assert.False((await replacementWriter.ImportForLocalTestAsync(localDraft)).SpeakingCreated);
        Assert.Equal(first.ListeningId, replaced.ListeningId);
        Assert.NotEqual(first.SpeakingId, replaced.SpeakingId);
        Assert.Equal(item.YoutubeId, replaced.ListeningYoutubeId);
        Assert.Equal(first.ListeningKey, replaced.ListeningKey);
        Assert.NotEqual(first.SpeakingKey, replaced.SpeakingKey);
        Assert.Equal(89, (await db.UserListeningProgresses.SingleAsync(x => x.LessonId == source.Id)).QuizScore);
    }

    [MariaDbAcceptanceFact]
    public async Task FailureAfterSqlSaveRollsBackRootCuesAndListeningFlag()
    {
        var failure = new FailAfterSave();
        await using var factory = new MariaDbAcceptanceFactory(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!, Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID"));
        factory.SaveInterceptors.Add(failure);
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var item = SpeakingCatalogTransferTests.Item("R" + Guid.NewGuid().ToString("N")[..10]);
        var source = SpeakingCatalogTransferTests.Source(item); db.ListeningLessons.Add(source); await db.SaveChangesAsync();
        failure.Enabled = true;
        await Assert.ThrowsAsync<IOException>(() => new SpeakingCatalogTransfer(db).PublishAsync(SpeakingCatalogTransferTests.Ready(item)));
        failure.Enabled = false; db.ChangeTracker.Clear();
        Assert.True((await db.ListeningLessons.SingleAsync(x => x.Id == source.Id)).IsPublished);
        Assert.False(await db.SpeakingVideos.AnyAsync(x => x.YoutubeId == item.YoutubeId));
    }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken token = default)
            => Enabled ? throw new IOException("Synthetic interruption after SQL, before commit") : ValueTask.FromResult(result);
    }
}
