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
                Status = PaymentOrderStatuses.ManualReview
            };
            var partiallyRefunded = new PaymentHistoryItemViewModel
            {
                Status = PaymentOrderStatuses.PartiallyRefunded
            };

            Assert.Equal("Chờ kiểm tra thủ công", manualReview.StatusDisplayName);
            Assert.Equal("badge bg-warning text-dark rounded-pill px-3", manualReview.StatusBadgeClass);
            Assert.Equal("bi bi-search me-1", manualReview.StatusIconClass);

            Assert.Equal("Hoàn tiền một phần", partiallyRefunded.StatusDisplayName);
            Assert.Equal("badge bg-info text-dark rounded-pill px-3", partiallyRefunded.StatusBadgeClass);
            Assert.Equal("bi bi-arrow-left-right me-1", partiallyRefunded.StatusIconClass);
        }

        [Fact]
        public void PaymentResult_ManualReviewAndPartiallyRefunded_HaveExpectedDisplayMappings()
        {
            var manualReview = new PaymentResultViewModel
            {
                HasValidReturnSignature = true,
                OrderStatus = PaymentOrderStatuses.ManualReview
            };
            var partiallyRefunded = new PaymentResultViewModel
            {
                HasValidReturnSignature = true,
                OrderStatus = PaymentOrderStatuses.PartiallyRefunded
            };

            Assert.Equal("Manual review", manualReview.StatusDisplayName);
            Assert.Equal("bi bi-search", manualReview.IconClass);
            Assert.Equal("badge bg-warning-subtle text-warning-emphasis border border-warning-subtle", manualReview.StatusBadgeClass);

            Assert.Equal("Hoàn tiền một phần", partiallyRefunded.StatusDisplayName);
            Assert.Equal("bi bi-arrow-left-right", partiallyRefunded.IconClass);
            Assert.Equal("badge bg-info-subtle text-info-emphasis border border-info-subtle", partiallyRefunded.StatusBadgeClass);
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
