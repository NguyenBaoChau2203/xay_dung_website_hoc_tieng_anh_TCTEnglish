using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.AI.Voice;
using TCTEnglish.Services.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public class AiVoiceEntitlementTests
{
    [Theory]
    [InlineData(Roles.Standard, false)]
    [InlineData(Roles.Premium, true)]
    [InlineData(Roles.Admin, true)]
    public async Task EntitlementComesFromDatabase(string role, bool expected)
    {
        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new User { UserId = 1, Email = "offline@test.invalid", PasswordHash = "hash", Role = role });
        await db.SaveChangesAsync();
        Assert.Equal(expected, await new AiVoiceEntitlementService(db, new PremiumAccessService(db), new SubscriptionService(db), TimeProvider.System).IsPremiumAsync(1, default));
    }

    [Theory]
    [InlineData(SubscriptionStatuses.Expired)]
    [InlineData(SubscriptionStatuses.Revoked)]
    [InlineData(SubscriptionStatuses.Active)]
    public async Task StalePremiumRoleWithExpiredOrRevokedSubscriptionIsDenied(string status)
    {
        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new User { UserId = 1, Email = "offline@test.invalid", PasswordHash = "hash", Role = Roles.Premium });
        db.UserSubscriptions.Add(new UserSubscription { UserId = 1, Status = status, StartsAtUtc = DateTime.UtcNow.AddDays(-3), EndsAtUtc = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();
        Assert.False(await new AiVoiceEntitlementService(db, new PremiumAccessService(db), new SubscriptionService(db), TimeProvider.System).IsPremiumAsync(1, default));
    }
}
