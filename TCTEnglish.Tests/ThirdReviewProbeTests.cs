using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;
using Xunit.Abstractions;

namespace TCTEnglish.Tests;

/// <summary>
/// Official regressions promoted from .tmp/cursor-third-review/ThirdReviewTests.cs (D1–D2).
/// </summary>
public sealed class ThirdReviewProbeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Speaking_CatalogFiltersAndPrivateList_DoNotLoseVideosAfterTwentyFour()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var user = await db.Users.SingleAsync(x => x.UserId == TestDataIds.UserId);
            user.Role = Roles.Premium;
            for (int i = 0; i < 25; i++)
            {
                db.SpeakingVideos.Add(new SpeakingVideo
                {
                    Title = $"third-review-public-{i:D2}",
                    YoutubeId = $"public-{i:D4}",
                    Level = "C2",
                    Topic = i == 24 ? "ThirdReviewTailOnlyTopic" : "ThirdReviewOther",
                    CreatedAt = DateTime.UtcNow.AddDays(-i)
                });
                db.SpeakingVideos.Add(new SpeakingVideo
                {
                    OwnerUserId = TestDataIds.UserId,
                    Title = $"third-review-private-{i:D2}",
                    YoutubeId = $"private-{i:D3}",
                    Level = "C2",
                    SourceType = "premium-user-youtube",
                    CreatedAt = DateTime.UtcNow.AddDays(-i)
                });
            }

            await db.SaveChangesAsync();
        }

        using var read = factory.Services.CreateScope();
        var context = read.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var service = new SpeakingService(context, new Mock<IYoutubeTranscriptService>().Object, NullLogger<SpeakingService>.Instance);
        var vm = await service.GetSpeakingIndexViewModelAsync(TestDataIds.UserId);
        var level = vm.VideosByLevel["C2"];
        output.WriteLine(
            $"seeded public/private=25/25; returned C2={level.Count}; private={vm.MyVideos.Count}; topic listed={vm.Topics.Contains("ThirdReviewTailOnlyTopic")}; topic result in returned cards={level.Count(x => x.Topic == "ThirdReviewTailOnlyTopic")}");

        // With true paging: first page may be capped, but topic filter / continuation must reach the tail video.
        Assert.True(vm.Topics.Contains("ThirdReviewTailOnlyTopic"), "Tail-only topic must remain listed.");
        var topicPage = await service.GetSpeakingIndexViewModelAsync(
            TestDataIds.UserId,
            new SpeakingCatalogQuery { Level = "C2", Topic = "ThirdReviewTailOnlyTopic", Page = 1, PageSize = 24 });
        Assert.Contains(topicPage.VideosByLevel.SelectMany(x => x.Value), x => x.Topic == "ThirdReviewTailOnlyTopic");

        var privateTotal = 0;
        var privatePage = 1;
        while (true)
        {
            var page = await service.GetSpeakingIndexViewModelAsync(
                TestDataIds.UserId,
                new SpeakingCatalogQuery { Mine = true, Page = privatePage, PageSize = 24 });
            privateTotal += page.MyVideos.Count;
            if (!page.MyVideosHasMore)
            {
                break;
            }

            privatePage++;
            Assert.True(privatePage <= 5, "Private list must terminate via HasMore.");
        }

        Assert.Equal(25, privateTotal);

        var publicTotal = 0;
        var publicPage = 1;
        while (true)
        {
            var page = await service.GetSpeakingIndexViewModelAsync(
                TestDataIds.UserId,
                new SpeakingCatalogQuery { Level = "C2", Page = publicPage, PageSize = 24 });
            var cards = page.VideosByLevel.GetValueOrDefault("C2") ?? [];
            publicTotal += cards.Count;
            if (!page.PublicHasMore)
            {
                break;
            }

            publicPage++;
            Assert.True(publicPage <= 5, "Public C2 list must terminate via HasMore.");
        }

        Assert.Equal(25, publicTotal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelAfterProviderReplyBeforeSave_DoesNotLeaveAssistantWithoutUser(bool draft)
    {
        using var cts = new CancellationTokenSource();
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_,_,_) =>
            {
                cts.Cancel();
                return Task.FromResult(new AiProviderReply("known reply", 10, 10, 20, "fake", "third-review"));
            }));
            services.KeepChatLifecycleOnProviderStub();
            services.PostConfigure<AiOptions>(o =>
            {
                o.RequestTokenBudget = 4600;
                o.DailyTokenBudgetPerUser = 60000;
                o.MaxInputChars = 1000;
            });
        });
        await factory.InitializeAsync();
        Guid? conversationId = null;
        if (!draft)
        {
            using var seed = factory.Services.CreateScope();
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            conversationId = Guid.NewGuid();
            db.AiConversations.Add(new AiConversation
            {
                Id = conversationId.Value,
                UserId = TestDataIds.UserId,
                Title = "existing",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var run = factory.Services.CreateScope();
        var error = await Record.ExceptionAsync(() =>
            run.ServiceProvider.GetRequiredService<IAiChatService>()
                .SendAsync(TestDataIds.UserId, conversationId, "cancel before reply save", cts.Token));
        using var verify = factory.Services.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var users = await context.AiMessages.CountAsync(x => x.Role == AiMessageRole.User);
        var assistants = await context.AiMessages.CountAsync(x => x.Role == AiMessageRole.Assistant);
        var success = await context.AiRequestLogs.CountAsync(x => x.IsSuccess);
        var ledger = await context.AiQuotaReservationLedgers.AsNoTracking().SingleAsync();
        output.WriteLine(
            $"draft={draft}; error={error?.GetType().Name}; user/assistant={users}/{assistants}; successLogs={success}; ledger={ledger.Status}/{ledger.ActualTokens}");
        Assert.True(assistants == 0 || users > 0, "Cancellation cleanup must not persist an orphan assistant with no user prompt.");
        Assert.NotEqual(AiQuotaReservationStatus.Released, ledger.Status);
    }

    [Fact]
    public async Task KnownProviderReplyPersistFailure_LeavesRecoverablePendingUsage()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_,_,_) =>
                Task.FromResult(new AiProviderReply("known", 10, 10, 20, "fake", "recover"))));
            services.KeepChatLifecycleOnProviderStub();
            services.PostConfigure<AiOptions>(o =>
            {
                o.RequestTokenBudget = 4600;
                o.DailyTokenBudgetPerUser = 60000;
                o.MaxInputChars = 1000;
            });
        });
        await factory.InitializeAsync();

        using var run = factory.Services.CreateScope();
        var quota = run.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>();
        var reservation = await quota.ReserveQuestionAsync(TestDataIds.UserId, 20, questionLimit: 0, tokenBudget: 60000, CancellationToken.None);
        await quota.MarkKnownUsagePendingAsync(reservation, actualTokens: 20, CancellationToken.None);

        using var verify = factory.Services.CreateScope();
        var ledger = await verify.ServiceProvider.GetRequiredService<DbflashcardContext>()
            .AiQuotaReservationLedgers.AsNoTracking().SingleAsync(x => x.RequestId == reservation.RequestId);
        Assert.Equal(AiQuotaReservationStatus.Pending, ledger.Status);
        Assert.Equal(20, ledger.ActualTokens);

        using var recover = factory.Services.CreateScope();
        var recovered = await recover.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>()
            .RecoverKnownPendingAsync(reservation.RequestId, CancellationToken.None);
        Assert.True(recovered);

        using var after = factory.Services.CreateScope();
        var settled = await after.ServiceProvider.GetRequiredService<DbflashcardContext>()
            .AiQuotaReservationLedgers.AsNoTracking().SingleAsync(x => x.RequestId == reservation.RequestId);
        Assert.Equal(AiQuotaReservationStatus.Settled, settled.Status);
        Assert.Equal(20, settled.ActualTokens);
    }
}
