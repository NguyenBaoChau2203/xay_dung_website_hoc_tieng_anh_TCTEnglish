using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Security;
using TCTEnglish.ViewModels.Billing;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Billing
{
    /// <summary>Rules about Premium that more than one service needs to agree on.</summary>
    public static class PremiumAccessRules
    {
        /// <summary>
        /// Permanent access: an administrator, or a legacy Premium role (set by hand) that has no subscription row
        /// at all. Such an account has nothing to buy and no end date.
        /// </summary>
        public static bool IsPermanent(string? role, bool hasAnySubscriptionRow)
        {
            var normalized = Roles.Normalize(role);
            return normalized.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase)
                || (normalized.Equals(Roles.Premium, StringComparison.OrdinalIgnoreCase) && !hasAnySubscriptionRow);
        }
    }

    public class PremiumAccessService : IPremiumAccessService
    {
        private readonly DbflashcardContext _context;

        public PremiumAccessService(DbflashcardContext context)
        {
            _context = context;
        }

        public async Task<PremiumAccessSnapshot> GetAccessSnapshotAsync(int userId)
        {
            if (userId <= 0)
            {
                return CreateAnonymousSnapshot();
            }

            var nowUtc = DateTime.UtcNow;

            var userRecord = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.Role })
                .FirstOrDefaultAsync();

            if (userRecord == null)
            {
                return CreateAnonymousSnapshot();
            }

            var role = Roles.Normalize(userRecord.Role);
            var isAdmin = role.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase);
            var isLegacyPremium = role.Equals(Roles.Premium, StringComparison.OrdinalIgnoreCase);

            // The end date is loaded for every non-admin, including accounts whose role is already Premium,
            // so a paying user sees "Thời hạn đến" (F10).
            DateTime? premiumEndsAtUtc = null;
            if (!isAdmin)
            {
                premiumEndsAtUtc = await _context.UserSubscriptions
                    .AsNoTracking()
                    .Where(s => s.UserId == userId
                             && s.Status == SubscriptionStatuses.Active
                             && s.EndsAtUtc > nowUtc)
                    .OrderByDescending(s => s.EndsAtUtc)
                    .Select(s => (DateTime?)s.EndsAtUtc)
                    .FirstOrDefaultAsync();
            }

            var hasActiveSubscription = premiumEndsAtUtc.HasValue;
            var isPremium = isAdmin || isLegacyPremium || hasActiveSubscription;

            var hasAnySubscriptionRow = isLegacyPremium
                && await _context.UserSubscriptions.AsNoTracking().AnyAsync(s => s.UserId == userId);
            var isPermanent = PremiumAccessRules.IsPermanent(role, hasAnySubscriptionRow);
            int? daysLeft = premiumEndsAtUtc.HasValue
                ? Math.Max(0, (int)Math.Ceiling((premiumEndsAtUtc.Value - nowUtc).TotalDays))
                : null;

            return new PremiumAccessSnapshot
            {
                IsAuthenticated = true,
                IsPremium = isPremium,
                IsAdmin = isAdmin,
                Role = role,
                PremiumEndsAtUtc = premiumEndsAtUtc,
                IsPermanent = isPermanent,
                DaysLeft = daysLeft,
                Features = isPremium ? PremiumFeatures.AllFeatures : new HashSet<string>()
            };
        }

        /// <inheritdoc/>
        public async Task<bool> HasFeatureAsync(int userId, string featureKey, CancellationToken ct = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => u.Role)
                .FirstOrDefaultAsync(ct);

            if (user == null)
            {
                return false;
            }

            var role = Roles.Normalize(user);
            if (role.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (role.Equals(Roles.Premium, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var nowUtc = DateTime.UtcNow;
            return await _context.UserSubscriptions
                .AsNoTracking()
                .AnyAsync(s => s.UserId == userId
                               && s.Status == SubscriptionStatuses.Active
                               && s.EndsAtUtc > nowUtc, ct);
        }

        private static PremiumAccessSnapshot CreateAnonymousSnapshot()
        {
            return new PremiumAccessSnapshot
            {
                IsAuthenticated = false,
                IsPremium = false,
                IsAdmin = false,
                Role = Roles.Standard,
                Features = new HashSet<string>()
            };
        }
    }
}
