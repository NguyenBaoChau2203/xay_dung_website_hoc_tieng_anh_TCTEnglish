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

            DateTime? premiumEndsAtUtc = null;
            if (!isAdmin && !isLegacyPremium)
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

            return new PremiumAccessSnapshot
            {
                IsAuthenticated = true,
                IsPremium = isPremium,
                IsAdmin = isAdmin,
                Role = role,
                PremiumEndsAtUtc = premiumEndsAtUtc,
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
