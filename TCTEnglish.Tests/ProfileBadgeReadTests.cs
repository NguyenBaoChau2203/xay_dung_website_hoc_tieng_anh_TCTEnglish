using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class ProfileBadgeReadTests
{
    [Fact]
    public async Task ProfileBadgeQuery_IsUserScoped_ReadOnly_AndSmallerThanGoals()
    {
        await using var factory = new TestWebApplicationFactory();
        var counter = new RowCountingCommandInterceptor(); factory.SaveInterceptors.Add(counter);
        await factory.InitializeAsync();
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.UserBadges.AddRange(new UserBadge { UserId = TestDataIds.UserId, BadgeId = 1, AwardedAt = DateTime.UtcNow },
                new UserBadge { UserId = TestDataIds.OutsiderUserId, BadgeId = 2, AwardedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        counter.Reset();
        var own = await service.GetEarnedBadgesForProfileAsync(TestDataIds.UserId);
        var profileCommands = counter.Commands;
        Assert.Equal("first-session", Assert.Single(own).Code);
        Assert.Equal("three-day-streak", Assert.Single(await service.GetEarnedBadgesForProfileAsync(TestDataIds.OutsiderUserId)).Code);
        var dbVerify = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(2, await dbVerify.UserBadges.CountAsync());
        counter.Reset();
        await service.GetGoalsAsync(TestDataIds.UserId);
        Assert.True(profileCommands < counter.Commands, $"Profile={profileCommands}, full Goals={counter.Commands}");
        Assert.Equal(1, profileCommands);
    }
}
