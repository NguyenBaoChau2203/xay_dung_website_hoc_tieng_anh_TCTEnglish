using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Append-only audit log for admin billing actions.
    /// Never updates or deletes existing rows.
    /// </summary>
    public class PaymentAuditService : IPaymentAuditService
    {
        private readonly DbflashcardContext _context;

        public PaymentAuditService(DbflashcardContext context)
        {
            _context = context;
        }

        public async Task<PaymentAdminAction> RecordAsync(
            int adminUserId,
            string actionType,
            string reason,
            long? paymentOrderId = null,
            long? subscriptionId = null,
            string? oldStatus = null,
            string? newStatus = null,
            string? payloadJson = null,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Audit reason is required for all sensitive billing actions.", nameof(reason));

            if (string.IsNullOrWhiteSpace(actionType))
                throw new ArgumentException("Audit actionType is required.", nameof(actionType));

            var entry = new PaymentAdminAction
            {
                AdminUserId    = adminUserId,
                ActionType     = actionType.Trim(),
                Reason         = reason.Trim(),
                PaymentOrderId = paymentOrderId,
                SubscriptionId = subscriptionId,
                OldStatus      = oldStatus,
                NewStatus      = newStatus,
                PayloadJson    = payloadJson,
                IpAddress      = ipAddress?[..Math.Min(ipAddress.Length, 50)],
                UserAgent      = userAgent?[..Math.Min(userAgent.Length, 500)],
                CreatedAtUtc   = DateTime.UtcNow
            };

            _context.PaymentAdminActions.Add(entry);
            await _context.SaveChangesAsync(ct);
            return entry;
        }

        public async Task<IReadOnlyList<PaymentAdminAction>> GetForOrderAsync(
            long paymentOrderId, CancellationToken ct = default)
        {
            return await _context.PaymentAdminActions
                .AsNoTracking()
                .Where(a => a.PaymentOrderId == paymentOrderId)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Include(a => a.AdminUser)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PaymentAdminAction>> GetForSubscriptionAsync(
            long subscriptionId, CancellationToken ct = default)
        {
            return await _context.PaymentAdminActions
                .AsNoTracking()
                .Where(a => a.SubscriptionId == subscriptionId)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Include(a => a.AdminUser)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PaymentAdminAction>> GetRecentAsync(
            int count = 50, CancellationToken ct = default)
        {
            return await _context.PaymentAdminActions
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(count)
                .Include(a => a.AdminUser)
                .ToListAsync(ct);
        }
    }
}
