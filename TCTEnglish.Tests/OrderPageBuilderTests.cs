using System;
using System.Linq;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>The order page model (plan 5.2): stage, mock box rule, safe addresses, receipt rows.</summary>
    public class OrderPageBuilderTests
    {
        private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

        private static PaymentOrder Order(
            string status = PaymentOrderStatuses.Pending, string provider = PaymentProviders.MoMo, int expiresInMinutes = 10,
            string? payUrl = "https://pay.example/x", string? deepLink = "momo://pay", string? returnPayload = null)
            => new()
            {
                Id = 1, OrderCode = "TCT-1", UserId = 5, PlanId = 1, Provider = provider, AmountVnd = 399000m, Currency = "VND",
                Status = status, CreatedAtUtc = Now.AddMinutes(-5), UpdatedAtUtc = Now.AddMinutes(-1),
                ExpiresAtUtc = Now.AddMinutes(expiresInMinutes), ProviderPaymentUrl = payUrl, ProviderDeepLink = deepLink,
                ProviderQrCodePayload = "QR", ReturnPayloadJson = returnPayload,
                Plan = new PremiumPlan { Name = "Premium 1 năm", DurationDays = 365 }
            };

        [Fact]
        public void MockBox_OnlyForAWaitingMoMoOrder_AndOnlyWhenTheCallerSaysMockMode()
        {
            Assert.True(OrderPageBuilder.Build(Order(), null, isMockMode: true, Now).IsMockMode);
            Assert.False(OrderPageBuilder.Build(Order(), null, isMockMode: false, Now).IsMockMode);
            Assert.False(OrderPageBuilder.Build(Order(PaymentOrderStatuses.Paid), null, true, Now).IsMockMode);
            Assert.False(OrderPageBuilder.Build(Order(PaymentOrderStatuses.Cancelled), null, true, Now).IsMockMode);
            Assert.False(OrderPageBuilder.Build(Order(expiresInMinutes: -1), null, true, Now).IsMockMode);
            Assert.False(OrderPageBuilder.Build(Order(provider: PaymentProviders.VNPay), null, true, Now).IsMockMode);
        }

        [Fact]
        public void Stage_AndFlags_FollowTheOrder()
        {
            var waiting = OrderPageBuilder.Build(Order(), null, false, Now);
            var confirming = OrderPageBuilder.Build(
                Order(returnPayload: "{\"resultCode\":\"0\"}"), null, false, Now);
            var review = OrderPageBuilder.Build(Order(PaymentOrderStatuses.ManualReview), null, false, Now);

            Assert.Equal(PaymentOrderStages.Waiting, waiting.Stage);
            Assert.Equal(PaymentOrderStages.Confirming, confirming.Stage);
            Assert.True(confirming.ReturnReportedSuccess);
            Assert.Equal(PaymentOrderStages.Confirming, review.Stage);
            Assert.True(review.IsManualReview);
            Assert.False(waiting.IsExpired);
            Assert.Equal(waiting.ExpiresAtMs, new DateTimeOffset(Now.AddMinutes(10)).ToUnixTimeMilliseconds());
        }

        [Theory]
        [InlineData("https://pay.example/x", true)]
        [InlineData("http://pay.example/x", false)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("momo://pay", false)]
        [InlineData("not a url", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void PayUrl_IsOnlyKeptWhenItIsHttps(string? stored, bool kept)
        {
            var model = OrderPageBuilder.Build(Order(payUrl: stored), null, false, Now);

            Assert.Equal(kept, model.PayUrl != null);
        }

        [Theory]
        [InlineData("momo://pay", true)]
        [InlineData("momoapp://pay", true)]
        [InlineData("https://pay.example/x", true)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("intent://x#Intent;end", false)]
        public void DeepLink_AllowsOnlyHttpsAndTheMoMoSchemes(string stored, bool kept)
        {
            var model = OrderPageBuilder.Build(Order(deepLink: stored), null, false, Now);

            Assert.Equal(kept, model.DeepLink != null);
        }

        [Fact]
        public void VnPay_ResumeAddress_OnlyWhileWaiting()
        {
            Assert.NotNull(OrderPageBuilder.Build(Order(provider: PaymentProviders.VNPay), null, false, Now).ResumeUrl);
            Assert.Null(OrderPageBuilder.Build(Order(provider: PaymentProviders.VNPay, expiresInMinutes: -1), null, false, Now).ResumeUrl);
            Assert.Null(OrderPageBuilder.Build(Order(PaymentOrderStatuses.Failed, PaymentProviders.VNPay), null, false, Now).ResumeUrl);
            Assert.Null(OrderPageBuilder.Build(Order(), null, false, Now).ResumeUrl);
        }

        [Fact]
        public void Receipt_UsesVietnamTime_TheBankAndADashForAMissingTransaction()
        {
            var order = Order(PaymentOrderStatuses.Paid, PaymentProviders.VNPay);
            order.PaidAtUtc = new DateTime(2026, 11, 17, 6, 45, 0, DateTimeKind.Utc);
            order.BankCode = "NCB";

            var model = OrderPageBuilder.Build(order, new DateTime(2027, 11, 17, 6, 45, 0, DateTimeKind.Utc), false, Now);

            var rows = model.Receipt.Rows;
            Assert.Equal("17/11/2026 13:45", rows.Single(r => r.Label == "Thời gian").Value);
            Assert.Equal("VNPay · NCB", rows.Single(r => r.Label == "Phương thức").Value);
            Assert.Equal("—", rows.Single(r => r.Label == "Mã giao dịch").Value);
            Assert.Equal($"399.000 ₫", rows.Single(r => r.Label == "Số tiền").Value);
            Assert.Equal(new DateTime(2027, 11, 17, 6, 45, 0, DateTimeKind.Utc), model.PremiumEndsAtUtc);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Cancelled, "Bạn đã hủy giao dịch trên MoMo.")]
        [InlineData(PaymentOrderStatuses.Failed, "Giao dịch không thành công.")]
        [InlineData(PaymentOrderStatuses.Expired, "Đơn đã hết hạn trước khi được thanh toán.")]
        [InlineData(PaymentOrderStatuses.Refunded, "Khoản thanh toán này đã được hoàn lại.")]
        public void NotCompletedReason_NamesTheOutcome(string status, string expected)
        {
            Assert.Equal(expected, OrderPageBuilder.Build(Order(status), null, false, Now).NotCompletedReason);
        }

        [Fact]
        public void PayAgain_IsNotOfferedForRefundedOrders()
        {
            Assert.False(OrderPageBuilder.Build(Order(PaymentOrderStatuses.Refunded), null, false, Now).CanPayAgain);
            Assert.False(OrderPageBuilder.Build(Order(PaymentOrderStatuses.PartiallyRefunded), null, false, Now).CanPayAgain);
            Assert.True(OrderPageBuilder.Build(Order(PaymentOrderStatuses.Cancelled), null, false, Now).CanPayAgain);
        }
    }
}
