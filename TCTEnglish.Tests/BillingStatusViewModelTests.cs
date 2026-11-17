using TCTEnglish.Services.Billing;
using TCTEnglish.ViewModels.Billing;
using TCTVocabulary.Areas.Admin.ViewModels.Billing;
using Xunit;

namespace TCTEnglish.Tests
{
    public class BillingStatusViewModelTests
    {
        [Fact]
        public void PaymentHistoryItem_ManualReviewAndPartiallyRefunded_HaveExpectedDisplayMappings()
        {
            var manualReview = new PaymentHistoryItemViewModel
            {
                Provider = PaymentProviders.VNPay,
                Status = PaymentOrderStatuses.ManualReview,
                ExpiresAtUtc = System.DateTime.UtcNow.AddDays(-2)
            };
            var partiallyRefunded = new PaymentHistoryItemViewModel
            {
                Provider = PaymentProviders.VNPay,
                Status = PaymentOrderStatuses.PartiallyRefunded,
                ExpiresAtUtc = System.DateTime.UtcNow.AddDays(-2)
            };

            // The learner never sees "Manual review" or an internal word: a review is "Đang xác nhận".
            Assert.Equal("Đang xác nhận", manualReview.StatusView.Label);
            Assert.Equal("fa-solid fa-hourglass-half", manualReview.StatusView.IconClass);
            Assert.Equal("Hoàn tiền một phần", partiallyRefunded.StatusView.Label);
            Assert.Equal("info", partiallyRefunded.StatusView.Tone);
        }

        [Fact]
        public void AdminBillingViewModels_ManualReviewAndPartiallyRefunded_HaveExpectedBadges()
        {
            var rowManualReview = new AdminPaymentOrderRowViewModel
            {
                Status = PaymentOrderStatuses.ManualReview
            };
            var detailsPartiallyRefunded = new AdminPaymentOrderDetailsViewModel
            {
                Status = PaymentOrderStatuses.PartiallyRefunded
            };

            Assert.Equal("badge bg-warning text-dark", rowManualReview.StatusBadgeClass);
            Assert.Equal("badge bg-info text-dark", detailsPartiallyRefunded.StatusBadgeClass);
        }
    }
}
