using System;
using System.Linq;
using TCTEnglish.Services.Billing;
using TCTEnglish.ViewModels.Billing;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>The shared learner-facing wording: Vietnam time, money, status labels, benefits, Premium status (plan 4.9, 5.1, 5.3).</summary>
    public class BillingDisplayHelpersTests
    {
        private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        private const string Nbsp = " ";

        // ─── Time and money ───────────────────────────────────────────────────────

        [Fact]
        public void Times_AreShownInVietnamTime()
        {
            Assert.Equal("17/11/2026 14:23", BillingTime.ToDisplay(new DateTime(2026, 11, 17, 7, 23, 0, DateTimeKind.Utc)));
            Assert.Equal("17/11/2026", BillingTime.ToDisplayDate(new DateTime(2026, 11, 17, 7, 23, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public void LateUtcEvenings_RollOverToTheNextVietnameseDay()
        {
            var utc = new DateTime(2026, 11, 17, 20, 30, 0, DateTimeKind.Utc);

            Assert.Equal("18/11/2026 03:30", BillingTime.ToDisplay(utc));
            Assert.Equal("18/11/2026", BillingTime.ToDisplayDate(utc));
        }

        [Fact]
        public void StoredValues_WithoutAKind_AreTreatedAsUtc()
        {
            // EF returns database datetimes with Kind = Unspecified; they are UTC.
            var stored = new DateTime(2026, 11, 17, 7, 23, 0, DateTimeKind.Unspecified);

            Assert.Equal("17/11/2026 14:23", BillingTime.ToDisplay(stored));
        }

        [Fact]
        public void LocalValues_AreConvertedFromTheirOwnZone()
        {
            var local = new DateTime(2026, 11, 17, 7, 23, 0, DateTimeKind.Utc).ToLocalTime();

            Assert.Equal("17/11/2026 14:23", BillingTime.ToDisplay(local));
        }

        [Theory]
        [InlineData(399000, "399.000")]
        [InlineData(49000, "49.000")]
        [InlineData(1234567, "1.234.567")]
        [InlineData(0, "0")]
        [InlineData(999, "999")]
        public void Money_UsesDotGrouping_AndANonBreakingSpaceBeforeTheSign(int amount, string digits)
        {
            Assert.Equal(digits + Nbsp + "₫", BillingTime.FormatMoney(amount));
        }

        [Fact]
        public void Money_DropsFractions_LikeTheProviderAmountCheck()
        {
            Assert.Equal("49.000" + Nbsp + "₫", BillingTime.FormatMoney(49000.90m));
        }

        // ─── Status labels ────────────────────────────────────────────────────────

        private static OrderStatusView Describe(string status, bool pastExpiry = false, bool returned = false)
            => PaymentOrderLabels.Describe(status, pastExpiry ? Now.AddMinutes(-5) : Now.AddMinutes(10), returned, Now);

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending, false, false, "Chờ thanh toán", "warning", PaymentOrderStages.Waiting)]
        [InlineData(PaymentOrderStatuses.Pending, false, true, "Đang xác nhận", "warning", PaymentOrderStages.Confirming)]
        [InlineData(PaymentOrderStatuses.Pending, true, false, "Hết hạn", "neutral", PaymentOrderStages.NotCompleted)]
        [InlineData(PaymentOrderStatuses.ManualReview, false, false, "Đang xác nhận", "warning", PaymentOrderStages.Confirming)]
        [InlineData(PaymentOrderStatuses.Paid, false, false, "Đã thanh toán", "success", PaymentOrderStages.Paid)]
        [InlineData(PaymentOrderStatuses.Cancelled, false, false, "Đã hủy", "neutral", PaymentOrderStages.NotCompleted)]
        [InlineData(PaymentOrderStatuses.Failed, false, false, "Thất bại", "danger", PaymentOrderStages.NotCompleted)]
        [InlineData(PaymentOrderStatuses.Expired, true, false, "Hết hạn", "neutral", PaymentOrderStages.NotCompleted)]
        [InlineData(PaymentOrderStatuses.Refunded, false, false, "Đã hoàn tiền", "info", PaymentOrderStages.NotCompleted)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded, false, false, "Hoàn tiền một phần", "info", PaymentOrderStages.NotCompleted)]
        public void Statuses_ReadTheSameEverywhere(
            string status, bool pastExpiry, bool returned, string label, string tone, string stage)
        {
            var view = Describe(status, pastExpiry, returned);

            Assert.Equal(label, view.Label);
            Assert.Equal(tone, view.Tone);
            Assert.Equal(stage, view.Stage);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending)]
        [InlineData(PaymentOrderStatuses.ManualReview)]
        [InlineData(PaymentOrderStatuses.Paid)]
        [InlineData(PaymentOrderStatuses.Cancelled)]
        [InlineData(PaymentOrderStatuses.Failed)]
        [InlineData(PaymentOrderStatuses.Expired)]
        [InlineData(PaymentOrderStatuses.Refunded)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded)]
        public void Icons_AreFontAwesome_AndLabelsNeverShowEnglishOrInternalWords(string status)
        {
            var view = Describe(status);

            Assert.StartsWith("fa-solid fa-", view.IconClass);
            Assert.DoesNotContain("bi ", view.IconClass);
            Assert.DoesNotContain("manual", view.Label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("IPN", view.Label);
        }

        [Fact]
        public void Providers_AndMethods_AreReadable()
        {
            Assert.Equal("VNPay", PaymentOrderLabels.ProviderName("vnpay"));
            Assert.Equal("MoMo", PaymentOrderLabels.ProviderName(" MOMO "));
            Assert.Equal("VNPay · NCB", PaymentOrderLabels.Method("vnpay", "NCB"));
            Assert.Equal("MoMo", PaymentOrderLabels.Method("momo", null));
            Assert.Equal("MoMo", PaymentOrderLabels.Method("momo", "  "));
        }

        // ─── Benefits ─────────────────────────────────────────────────────────────

        [Fact]
        public void Benefits_AreBackedByAGate_AndNeverClaimAnAdFreeApp()
        {
            Assert.NotEmpty(PremiumBenefits.Gated);
            Assert.All(PremiumBenefits.Gated, b =>
            {
                Assert.False(string.IsNullOrWhiteSpace(b.Gate), b.Title);
                Assert.DoesNotContain("quảng cáo", b.Title, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("quảng cáo", b.ShortTitle, StringComparison.OrdinalIgnoreCase);
            });
            Assert.Equal(PremiumBenefits.Gated.Count, PremiumBenefits.Gated.Select(b => b.Title).Distinct().Count());
        }

        [Fact]
        public void TheAiAssistantRow_StatesTheRealFreeLimit()
        {
            var row = PremiumBenefits.Gated.Single(b => b.Title == "Trợ lý AI");

            Assert.Equal("15 lượt mỗi ngày", row.FreeText);
            Assert.Equal("Không giới hạn lượt", row.PremiumText);
        }

        [Fact]
        public void TheUnlockedHighlights_AreFourOfTheGatedBenefits()
        {
            Assert.Equal(4, PremiumBenefits.UnlockedHighlights.Count);
            Assert.All(PremiumBenefits.UnlockedHighlights, h => Assert.Contains(PremiumBenefits.Gated, b => b.ShortTitle == h));
        }

        // ─── Premium status card ──────────────────────────────────────────────────

        private static PremiumAccessSnapshot Snapshot(
            bool premium = false, bool permanent = false, DateTime? ends = null, int? days = null)
            => new() { IsAuthenticated = true, IsPremium = premium, IsPermanent = permanent, PremiumEndsAtUtc = ends, DaysLeft = days };

        [Fact]
        public void FreeAccount_IsToldItIsFree()
        {
            var model = PremiumStatusViewModel.From(Snapshot(), compact: false);

            Assert.Equal(PremiumStatusMode.Free, model.Mode);
            Assert.Equal("Bạn đang dùng gói Miễn phí", model.Title);
            Assert.Equal("Nâng cấp để mở các tính năng dùng AI và nội dung riêng của bạn.", model.Text);
            Assert.True(model.ShowHistoryLink);
        }

        [Fact]
        public void PremiumAccount_SeesTheEndDateAndDaysLeft()
        {
            var ends = new DateTime(2027, 11, 17, 7, 0, 0, DateTimeKind.Utc);

            var model = PremiumStatusViewModel.From(Snapshot(premium: true, ends: ends, days: 365), compact: false);

            Assert.Equal(PremiumStatusMode.Premium, model.Mode);
            Assert.Equal("Bạn đang dùng Premium", model.Title);
            Assert.Equal("Có hiệu lực đến 17/11/2027 (còn 365 ngày). Mua thêm sẽ được cộng nối vào hạn này.", model.Text);
        }

        [Fact]
        public void OneDayLeft_IsWrittenInTheSingular()
        {
            var ends = new DateTime(2026, 11, 18, 7, 0, 0, DateTimeKind.Utc);

            var model = PremiumStatusViewModel.From(Snapshot(premium: true, ends: ends, days: 1), compact: false);

            Assert.Contains("(còn 1 ngày)", model.Text);
        }

        [Fact]
        public void CompactHistoryCard_NamesTheOrderThatActivatedIt()
        {
            var ends = new DateTime(2027, 11, 17, 7, 0, 0, DateTimeKind.Utc);

            var model = PremiumStatusViewModel.From(Snapshot(premium: true, ends: ends, days: 365), compact: true, "TCT-20261117064512-9F3A2C1B");

            Assert.Equal("Premium đến 17/11/2027", model.Title);
            Assert.Equal("Còn 365 ngày. Kích hoạt từ đơn TCT-20261117064512-9F3A2C1B.", model.Text);
            Assert.False(model.ShowHistoryLink);
            Assert.True(model.Compact);
        }

        [Fact]
        public void CompactHistoryCard_WithoutAnOrder_OmitsTheSentence()
        {
            var ends = new DateTime(2027, 11, 17, 7, 0, 0, DateTimeKind.Utc);

            var model = PremiumStatusViewModel.From(Snapshot(premium: true, ends: ends, days: 12), compact: true);

            Assert.Equal("Còn 12 ngày.", model.Text);
        }

        [Fact]
        public void PermanentAccount_HasNoEndDate()
        {
            var model = PremiumStatusViewModel.From(Snapshot(premium: true, permanent: true), compact: false);

            Assert.Equal(PremiumStatusMode.Permanent, model.Mode);
            Assert.Equal("Tài khoản của bạn có quyền Premium không thời hạn.", model.Text);
        }
    }
}
