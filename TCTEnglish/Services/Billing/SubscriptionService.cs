using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Billing
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly DbflashcardContext _context;

        public SubscriptionService(DbflashcardContext context)
        {
            _context = context;
        }

        // ───────────────────────────────────────────────────────────────────────
        // Activate
        // ───────────────────────────────────────────────────────────────────────

        public async Task<UserSubscription> ActivateFromPaidOrderAsync(
            long paymentOrderId, CancellationToken ct = default)
        {
            var order = await _context.PaymentOrders
                .Include(o => o.Plan)
                .FirstOrDefaultAsync(o => o.Id == paymentOrderId, ct);

            if (order == null)
                throw new InvalidOperationException(
                    $"PaymentOrder {paymentOrderId} not found.");

            if (!order.Status.Equals(PaymentOrderStatuses.Paid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"PaymentOrder {paymentOrderId} status is '{order.Status}', expected '{PaymentOrderStatuses.Paid}'.");

            // ── Idempotency guard: if this order already activated a subscription, return it ──
            var existingSub = await _context.UserSubscriptions
                .FirstOrDefaultAsync(s => s.ActivatedByPaymentOrderId == paymentOrderId, ct);

            if (existingSub != null)
                return existingSub;

            // ── Compute subscription window ──
            var nowUtc = DateTime.UtcNow;
            var currentActive = await GetActiveSubscriptionInternalAsync(order.UserId, nowUtc, ct);

            DateTime startsAt;
            DateTime endsAt;

            if (currentActive != null)
            {
                // Extend: stack on top of the current end date (or now if somehow past)
                var baseEnd = currentActive.EndsAtUtc > nowUtc ? currentActive.EndsAtUtc : nowUtc;
                startsAt = baseEnd;
                endsAt = baseEnd.AddDays(order.Plan.DurationDays);
            }
            else
            {
                startsAt = nowUtc;
                endsAt = nowUtc.AddDays(order.Plan.DurationDays);
            }

            var subscription = new UserSubscription
            {
                UserId = order.UserId,
                PlanId = order.PlanId,
                Status = SubscriptionStatuses.Active,
                StartsAtUtc = startsAt,
                EndsAtUtc = endsAt,
                ActivatedByPaymentOrderId = paymentOrderId,
                CreatedAtUtc = nowUtc
            };

            _context.UserSubscriptions.Add(subscription);

            // ── Sync User.Role for legacy compatibility ──
            // Never downgrade Admin. Only promote Standard → Premium.
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == order.UserId, ct);
            if (user != null)
            {
                var normalizedRole = Roles.Normalize(user.Role);
                if (!normalizedRole.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    user.Role = Roles.Premium;
                }
            }

            await _context.SaveChangesAsync(ct);
            return subscription;
        }

        // ───────────────────────────────────────────────────────────────────────
        // Expire
        // ───────────────────────────────────────────────────────────────────────

        public async Task<int> ExpireSubscriptionsAsync(CancellationToken ct = default)
        {
            var nowUtc = DateTime.UtcNow;

            var overdue = await _context.UserSubscriptions
                .Where(s => s.Status == SubscriptionStatuses.Active && s.EndsAtUtc <= nowUtc)
                .ToListAsync(ct);

            if (overdue.Count == 0)
                return 0;

            foreach (var sub in overdue)
            {
                sub.Status = SubscriptionStatuses.Expired;
            }

            // Determine which users need a role downgrade
            var affectedUserIds = overdue.Select(s => s.UserId).Distinct().ToList();

            foreach (var userId in affectedUserIds)
            {
                // Check if the user still has any remaining active subscription
                var stillHasActive = await _context.UserSubscriptions
                    .AnyAsync(s => s.UserId == userId
                                   && s.Status == SubscriptionStatuses.Active
                                   && s.EndsAtUtc > nowUtc, ct);

                if (stillHasActive)
                    continue;

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
                if (user == null)
                    continue;

                var normalizedRole = Roles.Normalize(user.Role);

                // Never touch Admin
                if (normalizedRole.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase))
                    continue;

                // NOTE: Legacy risk — A user whose role was manually set to "Premium"
                // (without any subscription) will NOT be downgraded by this method because
                // they would not have any UserSubscription records entering the overdue list.
                // Only users whose Premium status came from a subscription are affected.
                user.Role = Roles.Standard;
            }

            await _context.SaveChangesAsync(ct);
            return overdue.Count;
        }

        // ───────────────────────────────────────────────────────────────────────
        // Revoke
        // ───────────────────────────────────────────────────────────────────────

        public async Task<OperationResult> RevokeAsync(
            int userId, string reason, CancellationToken ct = default)
        {
            var nowUtc = DateTime.UtcNow;
            var activeSubs = await _context.UserSubscriptions
                .Where(s => s.UserId == userId
                            && s.Status == SubscriptionStatuses.Active
                            && s.EndsAtUtc > nowUtc)
                .ToListAsync(ct);

            if (activeSubs.Count == 0)
                return OperationResult.NotFound("User does not have an active subscription.");

            foreach (var activeSub in activeSubs)
            {
                activeSub.Status = SubscriptionStatuses.Revoked;
                activeSub.CancelledAtUtc = nowUtc;
                activeSub.CancelReason = reason;
            }

            // Downgrade role unless Admin
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (user != null)
            {
                var normalizedRole = Roles.Normalize(user.Role);
                if (!normalizedRole.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    user.Role = Roles.Standard;
                }
            }

            await _context.SaveChangesAsync(ct);
            return OperationResult.Success();
        }

        public async Task<OperationResult> GrantManualAsync(
            int userId,
            int planId,
            int? durationDays,
            string reason,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return OperationResult.Invalid("Grant reason is required.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (user == null)
                return OperationResult.NotFound("User not found.");

            var plan = await _context.PremiumPlans
                .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct);
            if (plan == null)
                return OperationResult.NotFound("Active premium plan not found.");

            var grantDurationDays = durationDays.GetValueOrDefault(plan.DurationDays);
            if (grantDurationDays <= 0)
                return OperationResult.Invalid("Grant duration must be greater than zero.");

            var nowUtc = DateTime.UtcNow;
            var currentActive = await GetActiveSubscriptionInternalAsync(userId, nowUtc, ct);
            var baseEnd = currentActive?.EndsAtUtc > nowUtc
                ? currentActive.EndsAtUtc
                : nowUtc;

            var subscription = new UserSubscription
            {
                UserId = userId,
                PlanId = plan.Id,
                Status = SubscriptionStatuses.Active,
                StartsAtUtc = baseEnd,
                EndsAtUtc = baseEnd.AddDays(grantDurationDays),
                CreatedAtUtc = nowUtc
            };

            _context.UserSubscriptions.Add(subscription);

            var normalizedRole = Roles.Normalize(user.Role);
            if (!normalizedRole.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                user.Role = Roles.Premium;
            }

            await _context.SaveChangesAsync(ct);
            return OperationResult.Success();
        }

        // ───────────────────────────────────────────────────────────────────────
        // Query
        // ───────────────────────────────────────────────────────────────────────

        public async Task<UserSubscription?> GetActiveSubscriptionAsync(
            int userId, CancellationToken ct = default)
        {
            var nowUtc = DateTime.UtcNow;
            return await GetActiveSubscriptionInternalAsync(userId, nowUtc, ct);
        }

        // ───────────────────────────────────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────────────────────────────────

        private async Task<UserSubscription?> GetActiveSubscriptionInternalAsync(
            int userId, DateTime nowUtc, CancellationToken ct)
        {
            return await _context.UserSubscriptions
                .Where(s => s.UserId == userId
                            && s.Status == SubscriptionStatuses.Active
                            && s.EndsAtUtc > nowUtc)
                .OrderByDescending(s => s.EndsAtUtc)
                .FirstOrDefaultAsync(ct);
        }
    }
}
