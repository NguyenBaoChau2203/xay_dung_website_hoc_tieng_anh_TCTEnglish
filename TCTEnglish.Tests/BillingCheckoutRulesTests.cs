using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>Checkout rules (plan 4.7): who may buy, resuming an open order, the rate limit and plain-Vietnamese messages.</summary>
    public class BillingCheckoutRulesTests
    {
        private static DbflashcardContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new DbflashcardContext(options);
        }

        private static BillingService CreateService(DbflashcardContext ctx, params IPaymentGateway[] gateways)
            => new(
                ctx,
                gateways.Length > 0 ? gateways : new IPaymentGateway[] { new FakeGateway(PaymentProviders.VNPay), new FakeGateway(PaymentProviders.MoMo) },
                BillingTestFactories.SilentQueryService(ctx),
                NullLogger<BillingService>.Instance);

        private static async Task<(User user, PremiumPlan monthly, PremiumPlan yearly)> SeedAsync(
            DbflashcardContext ctx, string role = Roles.Standard)
        {
            var user = new User { Email = $"u-{Guid.NewGuid():N}@test.com", PasswordHash = "h", Role = role };
            var monthly = new PremiumPlan
            {
                Code = "premium_monthly", Name = "Premium 1 tháng", Description = "d", PriceVnd = 49000m,
                DurationDays = 30, IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
            };
            var yearly = new PremiumPlan
            {
                Code = "premium_yearly", Name = "Premium 1 năm", Description = "d", PriceVnd = 399000m,
                DurationDays = 365, IsActive = true, DisplayOrder = 2, CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.AddRange(monthly, yearly);
            await ctx.SaveChangesAsync();
            return (user, monthly, yearly);
        }

        private static async Task<PaymentOrder> AddOrderAsync(
            DbflashcardContext ctx, User user, PremiumPlan plan, string provider, string status = PaymentOrderStatuses.Pending,
            TimeSpan? expiresIn = null, string? paymentUrl = null, string? code = null)
        {
            var order = new PaymentOrder
            {
                OrderCode = code ?? $"TCT-{Guid.NewGuid():N}"[..20], UserId = user.UserId, PlanId = plan.Id, Provider = provider,
                AmountVnd = plan.PriceVnd, Currency = "VND", Status = status,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1), UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                ExpiresAtUtc = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(10)), ProviderPaymentUrl = paymentUrl
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();
            return order;
        }

        // ─── Who may buy ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Admin_CannotBuy_AndNoOrderIsCreated()
        {
            await using var ctx = CreateContext();
            var (admin, _, yearly) = await SeedAsync(ctx, Roles.Admin);

            var result = await CreateService(ctx).CreateCheckoutAsync(admin.UserId, yearly.Code, PaymentProviders.MoMo, "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("ALREADY_PERMANENT", result.ErrorCode);
            Assert.Equal("Tài khoản quản trị đã có sẵn quyền Premium.", result.ErrorMessage);
            Assert.Empty(ctx.PaymentOrders);
        }

        [Fact]
        public async Task LegacyPermanentPremium_CannotBuyEither()
        {
            await using var ctx = CreateContext();
            var (user, _, yearly) = await SeedAsync(ctx, Roles.Premium);

            var result = await CreateService(ctx).CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.VNPay, "127.0.0.1");

            Assert.Equal("ALREADY_PERMANENT", result.ErrorCode);
            Assert.Empty(ctx.PaymentOrders);
        }

        [Fact]
        public async Task APremiumSubscriber_CanBuyAgain_ToExtend()
        {
            await using var ctx = CreateContext();
            var (user, monthly, yearly) = await SeedAsync(ctx, Roles.Premium);
            ctx.UserSubscriptions.Add(new UserSubscription
            {
                UserId = user.UserId, PlanId = monthly.Id, Status = SubscriptionStatuses.Active,
                StartsAtUtc = DateTime.UtcNow.AddDays(-5), EndsAtUtc = DateTime.UtcNow.AddDays(25), CreatedAtUtc = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.MoMo, "127.0.0.1");

            Assert.True(result.Success);
        }

        // ─── Resuming an open order ───────────────────────────────────────────────

        [Fact]
        public async Task SecondCheckout_ForTheSamePlanAndMoMo_ReusesTheOpenOrder()
        {
            await using var ctx = CreateContext();
            var (user, _, yearly) = await SeedAsync(ctx);
            var service = CreateService(ctx);

            var first = await service.CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.MoMo, "127.0.0.1");
            var second = await service.CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.MoMo, "127.0.0.1");

            Assert.True(first.Success && second.Success);
            Assert.Equal(first.OrderCode, second.OrderCode);
            Assert.Single(ctx.PaymentOrders);
            Assert.Contains(Uri.EscapeDataString(first.OrderCode!), second.RedirectUrl);
        }

        [Fact]
        public async Task SecondCheckout_ForVnPay_RedirectsToTheStoredPaymentUrl_WithoutANewOrder()
        {
            await using var ctx = CreateContext();
            var (user, _, yearly) = await SeedAsync(ctx);
            var service = CreateService(ctx);

            var first = await service.CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.VNPay, "127.0.0.1");
            var second = await service.CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.VNPay, "127.0.0.1");

            Assert.Equal(first.RedirectUrl, second.RedirectUrl);
            Assert.Equal(first.OrderCode, second.OrderCode);
            Assert.Single(ctx.PaymentOrders);
        }

        [Fact]
        public async Task TheVnPayPaymentUrl_IsStoredOnTheOrder_OnCheckout()
        {
            await using var ctx = CreateContext();
            var (user, _, yearly) = await SeedAsync(ctx);

            var result = await CreateService(ctx).CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.VNPay, "127.0.0.1");

            var order = Assert.Single(ctx.PaymentOrders);
            Assert.Equal(result.RedirectUrl, order.ProviderPaymentUrl);
            Assert.StartsWith("https://pay.example/vnpay", order.ProviderPaymentUrl);
        }

        [Fact]
        public async Task AnOpenVnPayOrderWithoutAStoredUrl_IsNotResumed_ANewOneIsCreated()
        {
            await using var ctx = CreateContext();
            var (user, _, yearly) = await SeedAsync(ctx);
            await AddOrderAsync(ctx, user, yearly, PaymentProviders.VNPay, paymentUrl: null);

            var result = await CreateService(ctx).CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.VNPay, "127.0.0.1");

            Assert.True(result.Success);
            Assert.Equal(2, ctx.PaymentOrders.Count());
        }

        [Fact]
        public async Task OtherPlan_OtherProvider_ExpiredOrOtherUsersOrder_AreNotReused()
        {
            await using var ctx = CreateContext();
            var (user, monthly, yearly) = await SeedAsync(ctx);
            var other = new User { Email = "x@test.com", PasswordHash = "h", Role = Roles.Standard };
            ctx.Users.Add(other);
            await ctx.SaveChangesAsync();
            var existing = (await AddOrderAsync(ctx, user, monthly, PaymentProviders.MoMo)).OrderCode;           // other plan
            await AddOrderAsync(ctx, user, yearly, PaymentProviders.VNPay, paymentUrl: "https://pay.example/old"); // other provider
            await AddOrderAsync(ctx, user, yearly, PaymentProviders.MoMo, expiresIn: TimeSpan.FromMinutes(-1));    // expired by time
            await AddOrderAsync(ctx, other, yearly, PaymentProviders.MoMo);                                         // someone else's
            await AddOrderAsync(ctx, user, yearly, PaymentProviders.MoMo, PaymentOrderStatuses.Failed);            // not pending

            var result = await CreateService(ctx).CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.MoMo, "127.0.0.1");

            Assert.True(result.Success);
            Assert.NotEqual(existing, result.OrderCode);
            Assert.Equal(6, ctx.PaymentOrders.Count());
        }

        // ─── Rate limit ───────────────────────────────────────────────────────────

        [Fact]
        public async Task FiveOpenOrdersInTheWindow_StopTheSixth_WithTheDocumentedMessage()
        {
            await using var ctx = CreateContext();
            var (user, monthly, yearly) = await SeedAsync(ctx);
            for (var i = 0; i < 5; i++)
            {
                await AddOrderAsync(ctx, user, monthly, PaymentProviders.VNPay, paymentUrl: "https://pay.example/x");
            }

            var result = await CreateService(ctx).CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.MoMo, "127.0.0.1");

            Assert.Equal("RATE_LIMITED", result.ErrorCode);
            Assert.Equal("Bạn đã tạo quá nhiều đơn thanh toán. Vui lòng thử lại sau vài phút.", result.ErrorMessage);
        }

        [Fact]
        public async Task ResumingAnOpenOrder_IsNotBlockedByTheRateLimit()
        {
            await using var ctx = CreateContext();
            var (user, monthly, yearly) = await SeedAsync(ctx);
            for (var i = 0; i < 4; i++)
            {
                await AddOrderAsync(ctx, user, monthly, PaymentProviders.VNPay, paymentUrl: "https://pay.example/x");
            }

            var open = await AddOrderAsync(ctx, user, yearly, PaymentProviders.MoMo);

            var result = await CreateService(ctx).CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.MoMo, "127.0.0.1");

            Assert.True(result.Success);
            Assert.Equal(open.OrderCode, result.OrderCode);
        }

        // ─── Messages ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task LearnerMessages_AreCorrectVietnamese_WithoutConfigurationDetails()
        {
            await using var ctx = CreateContext();
            var (user, _, yearly) = await SeedAsync(ctx);
            var service = CreateService(ctx, new FakeGateway(PaymentProviders.VNPay, enabled: false));

            var notReady = await service.CreateCheckoutAsync(user.UserId, yearly.Code, PaymentProviders.VNPay, "127.0.0.1");
            var unsupported = await service.CreateCheckoutAsync(user.UserId, yearly.Code, "paypal", "127.0.0.1");

            Assert.Equal("Cổng thanh toán hiện không khả dụng.", notReady.ErrorMessage);
            Assert.Equal("Cổng thanh toán này không được hỗ trợ.", unsupported.ErrorMessage);
            Assert.DoesNotContain("cấu hình", notReady.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("paypal", unsupported.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        }

        // ─── MoMo gateway failures ────────────────────────────────────────────────

        private static MoMoGateway MoMo(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            => new(
                new HttpClient(new StubHttpMessageHandler(handler)),
                Options.Create(new MoMoOptions
                {
                    Enabled = true,
                    PartnerCode = "PC123",
                    AccessKey = "AK123",
                    SecretKey = "SK123",
                    BaseUrl = "https://test-payment.momo.vn",
                    RedirectUrl = "https://example.com/Billing/MoMoReturn",
                    IpnUrl = "https://example.com/api/billing/momo/ipn"
                }),
                new StubWebHostEnvironment { EnvironmentName = "Production" },
                NullLogger<MoMoGateway>.Instance);

        private static HttpResponseMessage Json(object body)
            => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

        [Fact]
        public async Task MoMoGateway_Failures_AreShownInCorrectVietnamese()
        {
            var network = await MoMo(_ => throw new HttpRequestException("down")).CreateCheckoutAsync("O1", 49000m, "d", "127.0.0.1");
            var refused = await MoMo(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway))).CreateCheckoutAsync("O1", 49000m, "d", "127.0.0.1");
            var garbage = await MoMo(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("nope") })).CreateCheckoutAsync("O1", 49000m, "d", "127.0.0.1");
            var emptyUrls = await MoMo(_ => Task.FromResult(Json(new { resultCode = 0, message = "ok" }))).CreateCheckoutAsync("O1", 49000m, "d", "127.0.0.1");
            var badAmount = await MoMo(_ => Task.FromResult(Json(new { }))).CreateCheckoutAsync("O1", 10m, "d", "127.0.0.1");

            Assert.Equal("Không thể kết nối cổng MoMo lúc này.", network.ErrorMessage);
            Assert.Equal("MoMo từ chối tạo phiên thanh toán.", refused.ErrorMessage);
            Assert.Equal("Phản hồi từ MoMo không hợp lệ.", garbage.ErrorMessage);
            Assert.Equal("MoMo không trả về thông tin thanh toán hợp lệ.", emptyUrls.ErrorMessage);
            Assert.Equal("Số tiền thanh toán MoMo không hợp lệ.", badAmount.ErrorMessage);
        }

        [Fact]
        public async Task MoMoGateway_ProviderWording_IsLoggedNotShown()
        {
            var gateway = MoMo(_ => Task.FromResult(Json(new { resultCode = 1006, message = "Giao dich bi tu choi boi nguoi dung (RAW)" })));

            var result = await gateway.CreateCheckoutAsync("O1", 49000m, "d", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("MoMo không thể tạo giao dịch lúc này.", result.ErrorMessage);
            Assert.DoesNotContain("RAW", result.ErrorMessage!);
        }

        [Fact]
        public async Task MoMoGateway_NotConfigured_SaysItIsNotReady_WithoutDetails()
        {
            var gateway = new MoMoGateway(
                new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("no call"))),
                Options.Create(new MoMoOptions { Enabled = false }),
                new StubWebHostEnvironment { EnvironmentName = "Production" },
                NullLogger<MoMoGateway>.Instance);

            var result = await gateway.CreateCheckoutAsync("O1", 49000m, "d", "127.0.0.1");

            Assert.Equal("Cổng thanh toán MoMo chưa sẵn sàng.", result.ErrorMessage);
        }

        // ─── Fake gateway ─────────────────────────────────────────────────────────

        private sealed class FakeGateway : IPaymentGateway
        {
            private readonly bool _enabled;

            public FakeGateway(string provider, bool enabled = true)
            {
                ProviderName = provider;
                _enabled = enabled;
            }

            public string ProviderName { get; }
            public bool IsEnabled => _enabled;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string orderDescription, string clientIp, CancellationToken ct = default)
            {
                return Task.FromResult(ProviderName == PaymentProviders.MoMo
                    ? ProviderCheckoutResult.Ok(
                        $"/Billing/MoMoQr?orderCode={orderCode}", "REQ", "https://pay.example/momo", "momo://pay", "QR")
                    : ProviderCheckoutResult.Ok($"https://pay.example/vnpay?ref={orderCode}"));
            }

            public Task<ProviderCallbackResult> ProcessReturnAsync(IDictionary<string, string> queryParams, CancellationToken ct = default)
                => Task.FromResult(new ProviderCallbackResult { IsVerified = true });

            public Task<ProviderIpnResult> ProcessIpnAsync(IDictionary<string, string> parameters, CancellationToken ct = default)
                => Task.FromResult(new ProviderIpnResult { SignatureValid = true });
        }
    }
}
