using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiQuotaReservationIntegrationTests
{
    [Fact]
    public async Task ConcurrentReservations_AcrossTwoConversations_OnlyOneGetsLastStandardSlot()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerCalls = 0;

        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.PostConfigure<AiOptions>(options =>
            {
                options.ApiKey = "test-api-key";
                options.BaseUrl = "https://test-ai-provider.local/v1";
                options.RequestTimeoutSeconds = 5;
                options.MaxInputChars = 1000;
                options.RequestTokenBudget = 4600;
                options.DailyTokenBudgetPerUser = 60000;
            });
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient(async (_, _, ct) =>
            {
                Interlocked.Increment(ref providerCalls);
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
                return new AiProviderReply("ok", 10, 10, 20, "test-model", Guid.NewGuid().ToString("N"));
            }));
            services.KeepChatLifecycleOnProviderStub();
        });
        await factory.InitializeAsync();

        var day = DateTime.UtcNow.Date;
        Guid conversationA;
        Guid conversationB;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            conversationA = Guid.NewGuid();
            conversationB = Guid.NewGuid();
            db.AiConversations.AddRange(
                new AiConversation
                {
                    Id = conversationA,
                    UserId = TestDataIds.UserId,
                    Title = "A",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new AiConversation
                {
                    Id = conversationB,
                    UserId = TestDataIds.UserId,
                    Title = "B",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });

            // Seed 14 settled successful slots for today.
            db.AiUserDailyQuotas.Add(new AiUserDailyQuota
            {
                UserId = TestDataIds.UserId,
                DayUtc = day,
                PendingQuestions = 0,
                SettledQuestions = 14,
                PendingTokens = 0,
                SettledTokens = 140,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var chatA = scopeA.ServiceProvider.GetRequiredService<IAiChatService>();
        var chatB = scopeB.ServiceProvider.GetRequiredService<IAiChatService>();

        var taskA = chatA.SendAsync(TestDataIds.UserId, conversationA, "slot race A", default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Exception? secondError = null;
        try
        {
            await chatB.SendAsync(TestDataIds.UserId, conversationB, "slot race B", default);
        }
        catch (Exception ex)
        {
            secondError = ex;
        }

        release.TrySetResult();
        var first = await taskA.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(string.IsNullOrWhiteSpace(first.Text));
        Assert.NotNull(secondError);
        Assert.IsType<AiRateLimitException>(secondError);
        Assert.Equal(1, providerCalls);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var quota = await assertDb.AiUserDailyQuotas.AsNoTracking()
            .SingleAsync(x => x.UserId == TestDataIds.UserId && x.DayUtc == day);
        Assert.Equal(15, quota.SettledQuestions);
        Assert.Equal(0, quota.PendingQuestions);
    }
}
