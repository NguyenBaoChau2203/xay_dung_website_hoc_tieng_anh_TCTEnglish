using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.Billing;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class BillingRetriever : IKnowledgeRetriever
{
    private readonly DbflashcardContext _context;

    public BillingRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent) => intent == UserIntent.WebsiteGuide;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        var normalizedMessage = AiTextNormalizer.Normalize(userMessage);
        if (!IsBillingStatusQuery(normalizedMessage))
        {
            return [];
        }

        var nowUtc = DateTime.UtcNow;
        var role = await _context.Users
            .AsNoTracking()
            .Where(user => user.UserId == userId)
            .Select(user => user.Role)
            .FirstOrDefaultAsync(ct);

        if (role is null)
        {
            return [];
        }

        var normalizedRole = Roles.Normalize(role);
        var isRolePremium = string.Equals(normalizedRole, Roles.Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedRole, Roles.Premium, StringComparison.OrdinalIgnoreCase);

        var activeSubscription = await _context.UserSubscriptions
            .AsNoTracking()
            .Where(subscription =>
                subscription.UserId == userId
                && subscription.Status == SubscriptionStatuses.Active
                && subscription.EndsAtUtc > nowUtc)
            .OrderByDescending(subscription => subscription.EndsAtUtc)
            .Select(subscription => new
            {
                subscription.Status,
                subscription.EndsAtUtc,
                PlanName = subscription.Plan.Name
            })
            .FirstOrDefaultAsync(ct);

        var activePlanCount = await _context.PremiumPlans
            .AsNoTracking()
            .Where(plan => plan.IsActive)
            .CountAsync(ct);

        var recentOrders = await _context.PaymentOrders
            .AsNoTracking()
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Id)
            .Select(order => new
            {
                order.OrderCode,
                order.Provider,
                order.Status,
                order.AmountVnd,
                order.CreatedAtUtc,
                PlanName = order.Plan.Name
            })
            .Take(3)
            .ToListAsync(ct);

        var pendingOrderCount = recentOrders.Count(order =>
            string.Equals(order.Status, PaymentOrderStatuses.Pending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(order.Status, PaymentOrderStatuses.ManualReview, StringComparison.OrdinalIgnoreCase));

        var isPremium = isRolePremium || activeSubscription is not null;
        var snippets = new List<KnowledgeSnippet>
        {
            new(
                "billing-summary",
                string.Join(
                    '|',
                    $"role={normalizedRole}",
                    $"isPremium={isPremium.ToString().ToLowerInvariant()}",
                    $"planName={activeSubscription?.PlanName ?? string.Empty}",
                    $"subscriptionStatus={activeSubscription?.Status ?? string.Empty}",
                    $"endsAtUtc={FormatDate(activeSubscription?.EndsAtUtc)}",
                    $"pendingOrderCount={pendingOrderCount}",
                    $"activePlanCount={activePlanCount}"),
                KnowledgeSnippetSources.BillingSummary,
                "/Premium")
        };

        snippets.AddRange(recentOrders.Select(order => new KnowledgeSnippet(
            order.OrderCode,
            string.Join(
                '|',
                $"provider={order.Provider}",
                $"status={order.Status}",
                $"amountVnd={order.AmountVnd:0}",
                $"createdAtUtc={FormatDate(order.CreatedAtUtc)}",
                $"planName={order.PlanName}"),
            KnowledgeSnippetSources.BillingOrderItem,
            "/Billing/History")));

        return snippets;
    }

    private static bool IsBillingStatusQuery(string normalizedMessage)
    {
        var hasBillingKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "premium",
            "goi premium",
            "goi tra phi",
            "thanh toan",
            "billing",
            "payment",
            "lich su thanh toan",
            "don hang",
            "hoa don",
            "subscription",
            "vnpay",
            "momo");

        if (!hasBillingKeyword)
        {
            return false;
        }

        return AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "cua toi",
            "dang dung",
            "con han",
            "het han",
            "trang thai",
            "lich su",
            "da thanh toan",
            "chua thanh toan",
            "thanh cong",
            "that bai",
            "cho xu ly",
            "pending",
            "order");
    }

    private static string FormatDate(DateTime? value)
        => value?.ToString("yyyy-MM-dd") ?? string.Empty;
}
