using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>
    /// Matrix T11 (plan 12, finding A1): two tabs press "Thanh toán" at the same time.
    /// Exactly one pending order may exist for the same user, plan and provider.
    /// </summary>
    public class BillingConcurrentCheckoutTests
    {
        private static DbflashcardContext NewContext(string dbName)
            => new(new DbContextOptionsBuilder<DbflashcardContext>().UseInMemoryDatabase(dbName).Options);

        private static BillingService NewService(DbflashcardContext ctx, IPaymentGateway gateway)
            => new(
                ctx,
                new[] { gateway },
                TCTEnglish.Tests.TestHelpers.BillingTestFactories.SilentQueryService(ctx),
                NullLogger<BillingService>.Instance);

        private static async Task<(int userId, string planCode)> SeedAsync(string dbName)
        {
            await using var ctx = NewContext(dbName);
            var user = new User { Email = $"u-{Guid.NewGuid():N}@test.com", PasswordHash = "hash", Role = Roles.Standard };
            var plan = new PremiumPlan
            {
                Code = "premium_monthly",
                Name = "Premium 1 tháng",
                Description = "Test plan",
                PriceVnd = 49000,
                DurationDays = 30,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();
            return (user.UserId, plan.Code);
        }

        [Fact]
        public async Task TwoSimultaneousCheckouts_CreateOneOrder_AndBothBuyersLandOnIt()
        {
            var dbName = Guid.NewGuid().ToString();
            var (userId, planCode) = await SeedAsync(dbName);
            var gateway = new SlowResumableGateway();

            await using var ctxA = NewContext(dbName);
            await using var ctxB = NewContext(dbName);
            var a = NewService(ctxA, gateway);
            var b = NewService(ctxB, gateway);

            var results = await Task.WhenAll(
                a.CreateCheckoutAsync(userId, planCode, gateway.ProviderName, "127.0.0.1"),
                b.CreateCheckoutAsync(userId, planCode, gateway.ProviderName, "127.0.0.1"));

            Assert.All(results, r => Assert.True(r.Success, r.ErrorMessage));

            await using var verify = NewContext(dbName);
            var orders = await verify.PaymentOrders.AsNoTracking().ToListAsync();
            Assert.Single(orders);
            Assert.Equal(1, gateway.CreateCalls);
            Assert.Equal(results[0].RedirectUrl, results[1].RedirectUrl);
        }

        [Fact]
        public async Task SimultaneousCheckouts_OfDifferentUsers_AreNotMerged()
        {
            var dbName = Guid.NewGuid().ToString();
            var (userA, planCode) = await SeedAsync(dbName);
            int userB;
            await using (var seed = NewContext(dbName))
            {
                var other = new User { Email = $"u-{Guid.NewGuid():N}@test.com", PasswordHash = "hash", Role = Roles.Standard };
                seed.Users.Add(other);
                await seed.SaveChangesAsync();
                userB = other.UserId;
            }

            var gateway = new SlowResumableGateway();
            await using var ctxA = NewContext(dbName);
            await using var ctxB = NewContext(dbName);

            var results = await Task.WhenAll(
                NewService(ctxA, gateway).CreateCheckoutAsync(userA, planCode, gateway.ProviderName, "127.0.0.1"),
                NewService(ctxB, gateway).CreateCheckoutAsync(userB, planCode, gateway.ProviderName, "127.0.0.1"));

            Assert.All(results, r => Assert.True(r.Success, r.ErrorMessage));
            await using var verify = NewContext(dbName);
            Assert.Equal(2, await verify.PaymentOrders.AsNoTracking().CountAsync());
        }

        /// <summary>A gateway whose checkout takes a moment and whose order can be resumed from the stored URL.</summary>
        private sealed class SlowResumableGateway : IPaymentGateway
        {
            private int _createCalls;

            public int CreateCalls => Volatile.Read(ref _createCalls);
            public string ProviderName => "resumable";
            public bool IsEnabled => true;

            public async Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string orderDescription, string clientIp, CancellationToken ct = default)
            {
                Interlocked.Increment(ref _createCalls);
                await Task.Delay(75, ct);
                var url = $"https://pay.fake/checkout/{orderCode}";
                return ProviderCheckoutResult.Ok(url, $"req-{orderCode}", url, null, null);
            }

            public Task<ProviderCallbackResult> ProcessReturnAsync(IDictionary<string, string> queryParams, CancellationToken ct = default)
                => Task.FromResult(new ProviderCallbackResult());

            public Task<ProviderIpnResult> ProcessIpnAsync(IDictionary<string, string> parameters, CancellationToken ct = default)
                => Task.FromResult(new ProviderIpnResult());
        }
    }
}
