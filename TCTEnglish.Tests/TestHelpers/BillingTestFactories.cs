using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTVocabulary.Models;

namespace TCTEnglish.Tests.TestHelpers;

/// <summary>Builds billing services whose provider queries never answer (no network, nothing is confirmed).</summary>
internal static class BillingTestFactories
{
    public static PaymentQueryService SilentQueryService(DbflashcardContext context)
        => new(
            context,
            new NoOpVnPayQueryClient(),
            new SilentMoMoQueryClient(),
            new PaymentConfirmationService(context, new SubscriptionService(context), NullLogger<PaymentConfirmationService>.Instance),
            NullLogger<PaymentQueryService>.Instance);

    private sealed class SilentMoMoQueryClient : IMoMoQueryClient
    {
        public Task<PaymentQueryResult> QueryOrderAsync(string orderCode, CancellationToken ct = default)
            => Task.FromResult(PaymentQueryResult.Unknown());
    }
}
