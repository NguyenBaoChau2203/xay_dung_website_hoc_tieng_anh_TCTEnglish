using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.Workers;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SpeakingImportJobLifecycleTests
{
    private const string Url = "https://www.youtube.com/watch?v=abcdefghijk";

    [Fact]
    public async Task FailedAndCancelled_Resubmit_ReusesRow()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await MakePremiumAsync(factory, TestDataIds.UserId);

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISpeakingImportJobService>();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var first = await service.EnqueueYoutubeImportAsync(TestDataIds.UserId, Url, CancellationToken.None);
        Assert.NotNull(first.JobPublicId);
        var job = await context.SpeakingImportJobs.SingleAsync();
        Assert.Equal(SpeakingImportFingerprints.ForOwnerVideo(TestDataIds.UserId, "abcdefghijk"), job.RequestFingerprint);

        job.Status = "failed";
        job.LastError = "boom";
        job.AttemptCount = 3;
        job.CompletedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var retried = await service.EnqueueYoutubeImportAsync(TestDataIds.UserId, Url, CancellationToken.None);
        Assert.False(retried.IsInvalid);
        Assert.Equal(first.JobPublicId, retried.JobPublicId);
        var again = await context.SpeakingImportJobs.AsNoTracking().SingleAsync();
        Assert.Equal("queued", again.Status);
        Assert.Equal(0, again.AttemptCount);
        Assert.Null(again.LastError);

        again = await context.SpeakingImportJobs.SingleAsync();
        again.Status = "cancelled";
        await context.SaveChangesAsync();
        var cancelled = await service.EnqueueYoutubeImportAsync(TestDataIds.UserId, Url, CancellationToken.None);
        Assert.Equal(first.JobPublicId, cancelled.JobPublicId);
        Assert.Equal(1, await context.SpeakingImportJobs.CountAsync());
    }

    [Fact]
    public async Task ParallelEnqueue_ReturnsTheWinningJob()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await MakePremiumAsync(factory, TestDataIds.UserId);

        async Task<Guid?> EnqueueOnce()
        {
            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISpeakingImportJobService>();
            var result = await service.EnqueueYoutubeImportAsync(TestDataIds.UserId, Url, CancellationToken.None);
            return result.JobPublicId;
        }

        var results = await Task.WhenAll(EnqueueOnce(), EnqueueOnce());
        Assert.NotNull(results[0]);
        Assert.Equal(results[0], results[1]);

        using var verify = factory.Services.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(1, await context.SpeakingImportJobs.CountAsync());
    }

    [Fact]
    public async Task OtherUser_CannotReadOrRequeue()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await MakePremiumAsync(factory, TestDataIds.UserId);
        await MakePremiumAsync(factory, TestDataIds.OutsiderUserId);

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISpeakingImportJobService>();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var created = await service.EnqueueYoutubeImportAsync(TestDataIds.UserId, Url, CancellationToken.None);
        var hidden = await service.GetOwnedJobAsync(TestDataIds.OutsiderUserId, created.JobPublicId!.Value, CancellationToken.None);
        Assert.Null(hidden);

        var outsider = await service.EnqueueYoutubeImportAsync(TestDataIds.OutsiderUserId, Url, CancellationToken.None);
        Assert.NotEqual(created.JobPublicId, outsider.JobPublicId);
        Assert.Equal(2, await context.SpeakingImportJobs.CountAsync());
    }

    [Fact]
    public async Task SucceededWithDeletedVideo_CanBeReimported()
    {
        await using var factory = CreateCaptionFactory();
        await factory.InitializeAsync();
        await MakePremiumAsync(factory, TestDataIds.UserId);

        var worker = CreateWorker(factory);
        await worker.ProcessOnceAsync(CancellationToken.None);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var service = scope.ServiceProvider.GetRequiredService<ISpeakingImportJobService>();
            await service.EnqueueYoutubeImportAsync(TestDataIds.UserId, Url, CancellationToken.None);
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        int videoId;
        Guid publicId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var job = await context.SpeakingImportJobs.SingleAsync();
            Assert.Equal("succeeded", job.Status);
            Assert.NotNull(job.ResultVideoId);
            videoId = job.ResultVideoId!.Value;
            publicId = job.PublicId;
            Assert.Equal(0, CaptionFake.GeminiCalls);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var video = await context.SpeakingVideos.SingleAsync(v => v.Id == videoId);
            context.SpeakingVideos.Remove(video);
            await context.SaveChangesAsync();
            var service = scope.ServiceProvider.GetRequiredService<ISpeakingImportJobService>();
            var retried = await service.EnqueueYoutubeImportAsync(TestDataIds.UserId, Url, CancellationToken.None);
            Assert.Equal(publicId, retried.JobPublicId);
        }

        await worker.ProcessOnceAsync(CancellationToken.None);
        using var verify = factory.Services.CreateScope();
        var verifyContext = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var finalJob = await verifyContext.SpeakingImportJobs.AsNoTracking().SingleAsync();
        Assert.Equal("succeeded", finalJob.Status);
        Assert.NotEqual(videoId, finalJob.ResultVideoId);
        Assert.Equal(1, await verifyContext.SpeakingVideos.CountAsync(v => v.OwnerUserId == TestDataIds.UserId && v.YoutubeId == "abcdefghijk"));
    }

    [Fact]
    public async Task PremiumRevokedBeforePublish_DoesNotCreateVideo()
    {
        await using var factory = CreateCaptionFactory();
        await factory.InitializeAsync();
        await MakePremiumAsync(factory, TestDataIds.UserId);
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ISpeakingImportJobService>();
            await service.EnqueueYoutubeImportAsync(TestDataIds.UserId, Url, CancellationToken.None);
        }

        var worker = CreateWorker(factory);
        worker.BeforePublishAsync = async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
            user.Role = Roles.Standard;
            await context.SaveChangesAsync();
        };

        await worker.ProcessOnceAsync(CancellationToken.None);
        using var verify = factory.Services.CreateScope();
        var verifyContext = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var job = await verifyContext.SpeakingImportJobs.SingleAsync();
        Assert.Equal("failed", job.Status);
        Assert.Contains("Premium", job.LastError);
        Assert.Equal(0, await verifyContext.SpeakingVideos.CountAsync(v => v.YoutubeId == "abcdefghijk" && v.OwnerUserId == TestDataIds.UserId));
    }

    private static TestWebApplicationFactory CreateCaptionFactory()
    {
        CaptionFake.GeminiCalls = 0;
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IYoutubeTranscriptService>();
            services.AddSingleton<IYoutubeTranscriptService, CaptionFake>();
        });
    }

    private static SpeakingImportWorker CreateWorker(TestWebApplicationFactory factory)
    {
        return new SpeakingImportWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SpeakingImportWorker>.Instance,
            factory.Services.GetRequiredService<TimeProvider>());
    }

    private static async Task MakePremiumAsync(TestWebApplicationFactory factory, int userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == userId);
        user.Role = Roles.Premium;
        await context.SaveChangesAsync();
    }

    private sealed class CaptionFake : IYoutubeTranscriptService
    {
        public static int GeminiCalls;

        public Task<List<SpeakingSentence>> GetTranscriptAsync(string youtubeId)
            => Task.FromResult(Captions().Sentences);

        public Task<YoutubeTranscriptResult> GetTranscriptForSpeakingImportAsync(string youtubeId, CancellationToken ct = default)
        {
            Interlocked.Increment(ref GeminiCalls);
            return Task.FromResult(Captions());
        }

        public Task<YoutubeTranscriptResult> GetEnglishCaptionsOnlyAsync(string youtubeId, CancellationToken ct = default)
            => Task.FromResult(Captions());

        public Task<string?> GetVideoDurationAsync(string youtubeId) => Task.FromResult<string?>("0:04");

        public Task<YoutubeVideoMetadata?> GetVideoMetadataAsync(string youtubeId)
            => Task.FromResult<YoutubeVideoMetadata?>(new YoutubeVideoMetadata("Imported", null));

        private static YoutubeTranscriptResult Captions() => new(
            new List<SpeakingSentence>
            {
                new() { StartTime = 0, EndTime = 2, Text = "Hello there friend", VietnameseMeaning = string.Empty }
            },
            true,
            "youtube-captions");
    }
}
