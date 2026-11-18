using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>
    /// F7: a duplicate checkout or return event (a unique error from MariaDB or SQLite) is ignored and the failed row is
    /// forgotten, so the next save in the same request does not throw again; any other database error still surfaces.
    /// </summary>
    public class BillingEventConflictTests
    {
        private sealed class MySqlException : Exception
        {
            public MySqlException() : base("Duplicate entry 'co:TCT-1' for key 'IX_PaymentEvents_Provider_EventType_EventKey'") { }
            public int Number => 1062;
        }

        private sealed class FailEventInsert : SaveChangesInterceptor
        {
            private readonly string _eventType;
            private readonly Func<Exception> _error;

            public FailEventInsert(string eventType, Func<Exception> error)
            {
                _eventType = eventType;
                _error = error;
            }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
            {
                var adding = eventData.Context!.ChangeTracker.Entries<PaymentEvent>()
                    .Any(e => e.State == EntityState.Added && e.Entity.EventType == _eventType);
                // Like a real unique index: the same row is refused every time it is tried, so a row that is left
                // tracked after the first failure would break the next save of the request.
                if (adding)
                {
                    throw new DbUpdateException("event insert failed", _error());
                }

                return base.SavingChangesAsync(eventData, result, ct);
            }
        }

        private static DbflashcardContext CreateContext(IInterceptor interceptor)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;
            return new DbflashcardContext(options);
        }

        private static async Task<(User user, PremiumPlan plan)> SeedAsync(DbflashcardContext ctx)
        {
            var user = new User { Email = "u@test.com", PasswordHash = "h", Role = Roles.Standard };
            var plan = new PremiumPlan
            {
                Code = "premium_monthly", Name = "Premium", Description = "d", PriceVnd = 49000m, DurationDays = 30,
                IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();
            return (user, plan);
        }

        private sealed class Gateway : IPaymentGateway
        {
            public string ProviderName => PaymentProviders.VNPay;
            public bool IsEnabled => true;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string orderDescription, string clientIp, CancellationToken ct = default)
                => Task.FromResult(ProviderCheckoutResult.Ok("https://pay.example/vnpay"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(IDictionary<string, string> queryParams, CancellationToken ct = default)
                => Task.FromResult(new ProviderCallbackResult { IsVerified = true, IsPaid = false });

            public Task<ProviderIpnResult> ProcessIpnAsync(IDictionary<string, string> parameters, CancellationToken ct = default)
                => Task.FromResult(new ProviderIpnResult { SignatureValid = true });
        }

        private static BillingService Service(DbflashcardContext ctx)
            => new(ctx, new IPaymentGateway[] { new Gateway() }, BillingTestFactories.SilentQueryService(ctx), NullLogger<BillingService>.Instance);

        [Theory]
        [InlineData("mariadb")]
        [InlineData("sqlite")]
        public async Task Checkout_WhenTheCheckoutEventHitsAUniqueError_StillSucceeds_AndSavesTheRestOfTheRequest(string kind)
        {
            Func<Exception> error = kind == "mariadb"
                ? () => new MySqlException()
                : () => new SqliteException("SQLite Error 19: 'UNIQUE constraint failed: PaymentEvents.EventKey'.", 19);
            await using var ctx = CreateContext(new FailEventInsert(PaymentEventTypes.CheckoutCreated, error));
            var (user, plan) = await SeedAsync(ctx);

            var result = await Service(ctx).CreateCheckoutAsync(user.UserId, plan.Code, PaymentProviders.VNPay, "127.0.0.1");

            Assert.True(result.Success);
            var order = Assert.Single(ctx.PaymentOrders);
            // The save after the failed event (the provider fields) went through: the failed row was forgotten.
            Assert.Equal("https://pay.example/vnpay", order.ProviderPaymentUrl);
            Assert.DoesNotContain(ctx.ChangeTracker.Entries<PaymentEvent>(), e => e.State == EntityState.Added);
        }

        [Fact]
        public async Task Checkout_WhenTheCheckoutEventFailsForAnotherReason_TheErrorSurfaces()
        {
            await using var ctx = CreateContext(
                new FailEventInsert(PaymentEventTypes.CheckoutCreated, () => new InvalidOperationException("disk full")));
            var (user, plan) = await SeedAsync(ctx);

            await Assert.ThrowsAsync<DbUpdateException>(
                () => Service(ctx).CreateCheckoutAsync(user.UserId, plan.Code, PaymentProviders.VNPay, "127.0.0.1"));
        }

        [Fact]
        public async Task Return_WhenTheReturnEventHitsAUniqueError_StillShowsTheOrder_AndKeepsTheReturnPayload()
        {
            await using var ctx = CreateContext(new FailEventInsert(PaymentEventTypes.Return, () => new MySqlException()));
            var (user, plan) = await SeedAsync(ctx);
            ctx.PaymentOrders.Add(new PaymentOrder
            {
                OrderCode = "TCT-RET-F7", UserId = user.UserId, PlanId = plan.Id, Provider = PaymentProviders.VNPay,
                AmountVnd = 49000m, Currency = "VND", Status = PaymentOrderStatuses.Pending,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });
            await ctx.SaveChangesAsync();

            var model = await Service(ctx).GetVnPayReturnResultAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["vnp_TxnRef"] = "TCT-RET-F7", ["vnp_ResponseCode"] = "24", ["vnp_SecureHash"] = "abc"
            });

            Assert.NotNull(model);
            Assert.True(model!.HasValidReturnSignature);
            var order = await ctx.PaymentOrders.AsNoTracking().SingleAsync();
            Assert.NotNull(order.ReturnPayloadJson);
        }
    }
}
