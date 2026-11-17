using System;
using TCTEnglish.Services.Billing;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>The learner-facing reading of an order (plan 4.9): label stages and the return hint.</summary>
    public class PaymentOrderPresentationTests
    {
        private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Future = Now.AddMinutes(10);
        private static readonly DateTime Past = Now.AddMinutes(-10);

        [Theory]
        // pending, not expired
        [InlineData(PaymentOrderStatuses.Pending, false, false, PaymentOrderStages.Waiting)]
        [InlineData(PaymentOrderStatuses.Pending, false, true, PaymentOrderStages.Confirming)]
        // pending but past its expiry and nobody paid: effectively over
        [InlineData(PaymentOrderStatuses.Pending, true, false, PaymentOrderStages.NotCompleted)]
        [InlineData(PaymentOrderStatuses.Pending, true, true, PaymentOrderStages.Confirming)]
        [InlineData(PaymentOrderStatuses.ManualReview, false, false, PaymentOrderStages.Confirming)]
        [InlineData(PaymentOrderStatuses.ManualReview, true, false, PaymentOrderStages.Confirming)]
        [InlineData(PaymentOrderStatuses.Paid, false, false, PaymentOrderStages.Paid)]
        [InlineData(PaymentOrderStatuses.Paid, true, true, PaymentOrderStages.Paid)]
        [InlineData(PaymentOrderStatuses.Cancelled, false, false, PaymentOrderStages.NotCompleted)]
        [InlineData(PaymentOrderStatuses.Failed, false, true, PaymentOrderStages.NotCompleted)]
        [InlineData(PaymentOrderStatuses.Refunded, true, false, PaymentOrderStages.NotCompleted)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded, true, false, PaymentOrderStages.NotCompleted)]
        [InlineData(PaymentOrderStatuses.Expired, true, false, PaymentOrderStages.NotCompleted)]
        // expired shortly ago, but the provider's return said paid: a query can still confirm it
        [InlineData(PaymentOrderStatuses.Expired, true, true, PaymentOrderStages.Confirming)]
        public void ResolveStage_FollowsThePlanTable(string status, bool pastExpiry, bool returnSuccess, string expected)
        {
            var expiry = pastExpiry ? Past : Future;

            Assert.Equal(expected, PaymentOrderPresentation.ResolveStage(status, expiry, returnSuccess, Now));
        }

        [Fact]
        public void ExpiredMoreThanADayAgo_EvenWithASuccessfulReturn_IsNotCompleted()
        {
            var stage = PaymentOrderPresentation.ResolveStage(
                PaymentOrderStatuses.Expired, Now.AddHours(-25), true, Now);

            Assert.Equal(PaymentOrderStages.NotCompleted, stage);
        }

        [Fact]
        public void ResolveStage_IgnoresCaseAndWhitespaceInTheStatus()
        {
            Assert.Equal(PaymentOrderStages.Paid, PaymentOrderPresentation.ResolveStage(" PAID ", Past, false, Now));
        }

        [Theory]
        [InlineData(PaymentProviders.VNPay, "{\"vnp_ResponseCode\":\"00\",\"vnp_TransactionStatus\":\"00\"}", true)]
        [InlineData(PaymentProviders.VNPay, "{\"vnp_ResponseCode\":\"00\"}", true)]
        [InlineData(PaymentProviders.VNPay, "{\"vnp_ResponseCode\":\"00\",\"vnp_TransactionStatus\":\"02\"}", false)]
        [InlineData(PaymentProviders.VNPay, "{\"vnp_ResponseCode\":\"24\",\"vnp_TransactionStatus\":\"02\"}", false)]
        [InlineData(PaymentProviders.VNPay, "{\"VNP_RESPONSECODE\":\"00\"}", true)]
        [InlineData(PaymentProviders.MoMo, "{\"resultCode\":\"0\"}", true)]
        [InlineData(PaymentProviders.MoMo, "{\"resultCode\":0}", true)]
        [InlineData(PaymentProviders.MoMo, "{\"resultCode\":\"1006\"}", false)]
        [InlineData(PaymentProviders.MoMo, "{\"vnp_ResponseCode\":\"00\"}", false)]
        [InlineData(PaymentProviders.VNPay, "{\"resultCode\":\"0\"}", false)]
        [InlineData(PaymentProviders.VNPay, null, false)]
        [InlineData(PaymentProviders.VNPay, "", false)]
        [InlineData(PaymentProviders.VNPay, "not json", false)]
        [InlineData(PaymentProviders.VNPay, "[1,2]", false)]
        public void ReturnReportedSuccess_ReadsTheStoredReturnParameters(string provider, string? json, bool expected)
        {
            Assert.Equal(expected, PaymentOrderPresentation.ReturnReportedSuccess(provider, json));
        }
    }
}
