using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    public class MoMoIpnServiceTests
    {
        private static DbflashcardContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new DbflashcardContext(options);
        }

        private static IpnService CreateService(DbflashcardContext context, IPaymentGateway momoGateway)
        {
            return new IpnService(
                context,
                new[] { new FakeVnPayGateway(), momoGateway },
                new SubscriptionService(context),
                NullLogger<IpnService>.Instance);
        }

        [Fact]
        public async Task ProcessMoMoIpnAsync_InvalidSignature_DoesNotMarkPaid()
        {
            var dbName = Guid.NewGuid().ToString();
            await SeedOrderAsync(dbName, "ORD-1");

            await using var ctx = CreateContext(dbName);
            var svc = CreateService(ctx, new FakeMoMoGateway(signatureValid: false, isPaid: false));
            await svc.ProcessMoMoIpnAsync(new Dictionary<string, string>
            {
                ["orderId"] = "ORD-1",
                ["requestId"] = "REQ-1",
                ["transId"] = "TXN-1",
                ["amount"] = "49000",
                ["resultCode"] = "0",
                ["signature"] = "bad"
            });

            await using var verify = CreateContext(dbName);
            var order = await verify.PaymentOrders.FirstAsync();
            Assert.Equal(PaymentOrderStatuses.Pending, order.Status);
        }

        [Fact]
        public async Task ProcessMoMoIpnAsync_Success_MarksPaidAndCreatesSubscription()
        {
            var dbName = Guid.NewGuid().ToString();
            var userId = await SeedOrderAsync(dbName, "ORD-2");

            await using var ctx = CreateContext(dbName);
            var svc = CreateService(ctx, new FakeMoMoGateway(signatureValid: true, isPaid: true));

            await svc.ProcessMoMoIpnAsync(new Dictionary<string, string>
            {
                ["orderId"] = "ORD-2",
                ["requestId"] = "REQ-2",
                ["transId"] = "TXN-2",
                ["amount"] = "49000",
                ["resultCode"] = "0",
                ["payType"] = "qr",
                ["signature"] = "valid"
            });

            await using var verify = CreateContext(dbName);
            var order = await verify.PaymentOrders.FirstAsync(o => o.OrderCode == "ORD-2");
            Assert.Equal(PaymentOrderStatuses.Paid, order.Status);
            Assert.Equal("TXN-2", order.ProviderTransactionId);
            Assert.Equal("REQ-2", order.ProviderRequestId);
            Assert.Equal("qr", order.PayType);

            var subscription = await verify.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
            Assert.NotNull(subscription);
            Assert.Equal(SubscriptionStatuses.Active, subscription!.Status);
        }

        private static async Task<int> SeedOrderAsync(string dbName, string orderCode)
        {
            await using var ctx = CreateContext(dbName);
            var user = new User
            {
                Email = $"{Guid.NewGuid():N}@test.com",
                PasswordHash = "hash",
                Role = Roles.Standard
            };
            ctx.Users.Add(user);

            var plan = new PremiumPlan
            {
                Code = $"plan-{Guid.NewGuid():N}",
                Name = "Premium 1 tháng",
                Description = "Desc",
                PriceVnd = 49000m,
                DurationDays = 30,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            ctx.PaymentOrders.Add(new PaymentOrder
            {
                OrderCode = orderCode,
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = PaymentProviders.MoMo,
                AmountVnd = 49000m,
                Currency = "VND",
                Status = PaymentOrderStatuses.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            });
            await ctx.SaveChangesAsync();
            return user.UserId;
        }

        private sealed class FakeMoMoGateway : IPaymentGateway
        {
            private readonly bool _signatureValid;
            private readonly bool _isPaid;

            public FakeMoMoGateway(bool signatureValid, bool isPaid)
            {
                _signatureValid = signatureValid;
                _isPaid = isPaid;
            }

            public string ProviderName => PaymentProviders.MoMo;
            public bool IsEnabled => true;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode,
                decimal amountVnd,
                string orderDescription,
                string clientIp,
                CancellationToken ct = default)
                => Task.FromResult(ProviderCheckoutResult.Ok("/Billing/MoMoQr?orderCode=" + orderCode));

            public Task<ProviderCallbackResult> ProcessReturnAsync(
                IDictionary<string, string> queryParams,
                CancellationToken ct = default)
                => Task.FromResult(new ProviderCallbackResult
                {
                    IsVerified = _signatureValid,
                    IsPaid = _isPaid
                });

            public Task<ProviderIpnResult> ProcessIpnAsync(
                IDictionary<string, string> parameters,
                CancellationToken ct = default)
            {
                parameters.TryGetValue("orderId", out var orderCode);
                parameters.TryGetValue("requestId", out var requestId);
                parameters.TryGetValue("transId", out var transactionId);
                parameters.TryGetValue("resultCode", out var resultCode);
                parameters.TryGetValue("payType", out var payType);

                return Task.FromResult(new ProviderIpnResult
                {
                    SignatureValid = _signatureValid,
                    IsPaid = _signatureValid && _isPaid && string.Equals(resultCode, "0", StringComparison.Ordinal),
                    OrderCode = orderCode,
                    ProviderRequestId = requestId,
                    ProviderTransactionId = transactionId,
                    ProviderResponseCode = resultCode,
                    ProviderTransactionStatus = resultCode,
                    PayType = payType
                });
            }
        }

        private sealed class FakeVnPayGateway : IPaymentGateway
        {
            public string ProviderName => PaymentProviders.VNPay;
            public bool IsEnabled => true;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode,
                decimal amountVnd,
                string orderDescription,
                string clientIp,
                CancellationToken ct = default)
                => Task.FromResult(ProviderCheckoutResult.Ok("https://example.com"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(
                IDictionary<string, string> queryParams,
                CancellationToken ct = default)
                => Task.FromResult(new ProviderCallbackResult { IsVerified = true, IsPaid = true });

            public Task<ProviderIpnResult> ProcessIpnAsync(
                IDictionary<string, string> parameters,
                CancellationToken ct = default)
                => Task.FromResult(new ProviderIpnResult { SignatureValid = true, IsPaid = true });
        }
    }
}
