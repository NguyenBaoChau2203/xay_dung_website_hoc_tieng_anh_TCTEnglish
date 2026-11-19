using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Security;
using TCTEnglish.Services.Billing;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Voice;

public sealed class AiVoiceEntitlementService(DbflashcardContext db, IPremiumAccessService premium,
    ISubscriptionService subscriptions, TimeProvider clock)
{
    public async Task<bool> IsPremiumAsync(int userId, CancellationToken ct)
    {
        await subscriptions.ExpireForUserAsync(userId, ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (user == null || user.AnonymizedAt != null
            || user.Status == UserStatus.Blocked && (user.LockExpiry == null || user.LockExpiry > clock.GetUtcNow().UtcDateTime))
            throw new VoiceException("not_found", 404);
        if (!await premium.HasFeatureAsync(userId, PremiumFeatures.AiVoiceCustomTopics, ct)) return false;
        if (Roles.Normalize(user.Role) == Roles.Admin) return true;
        var hadSubscription = await db.UserSubscriptions.AsNoTracking().AnyAsync(x => x.UserId == userId, ct);
        if (!hadSubscription && Roles.Normalize(user.Role) == Roles.Premium) return true;
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.UserSubscriptions.AsNoTracking().AnyAsync(x => x.UserId == userId
            && x.Status == SubscriptionStatuses.Active && x.StartsAtUtc <= now && x.EndsAtUtc > now, ct);
    }
}
