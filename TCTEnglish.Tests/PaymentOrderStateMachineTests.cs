using TCTEnglish.Services.Billing;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>One assertion per cell of the transition table in the billing plan (4.1).</summary>
    public class PaymentOrderStateMachineTests
    {
        private const PaymentTransitionSource Callback = PaymentTransitionSource.ProviderCallback;
        private const PaymentTransitionSource Query = PaymentTransitionSource.ProviderQuery;
        private const PaymentTransitionSource Cleanup = PaymentTransitionSource.Cleanup;
        private const PaymentTransitionSource Admin = PaymentTransitionSource.Admin;

        [Theory]
        // pending: provider and query only
        [InlineData(PaymentOrderStatuses.Pending, Callback, true)]
        [InlineData(PaymentOrderStatuses.Pending, Query, true)]
        [InlineData(PaymentOrderStatuses.Pending, Cleanup, false)]
        [InlineData(PaymentOrderStatuses.Pending, Admin, false)]
        // expired: provider and query (the 24h late rule lives in the confirmation service)
        [InlineData(PaymentOrderStatuses.Expired, Callback, true)]
        [InlineData(PaymentOrderStatuses.Expired, Query, true)]
        [InlineData(PaymentOrderStatuses.Expired, Cleanup, false)]
        [InlineData(PaymentOrderStatuses.Expired, Admin, false)]
        // failed / cancelled: a verified success wins over an earlier failure
        [InlineData(PaymentOrderStatuses.Failed, Callback, true)]
        [InlineData(PaymentOrderStatuses.Failed, Query, true)]
        [InlineData(PaymentOrderStatuses.Failed, Admin, false)]
        [InlineData(PaymentOrderStatuses.Cancelled, Callback, true)]
        [InlineData(PaymentOrderStatuses.Cancelled, Query, true)]
        [InlineData(PaymentOrderStatuses.Cancelled, Admin, false)]
        // manual_review: admin only
        [InlineData(PaymentOrderStatuses.ManualReview, Callback, false)]
        [InlineData(PaymentOrderStatuses.ManualReview, Query, false)]
        [InlineData(PaymentOrderStatuses.ManualReview, Cleanup, false)]
        [InlineData(PaymentOrderStatuses.ManualReview, Admin, true)]
        // paid is idempotent, never re-marked; refunded orders never come back
        [InlineData(PaymentOrderStatuses.Paid, Callback, false)]
        [InlineData(PaymentOrderStatuses.Paid, Admin, false)]
        [InlineData(PaymentOrderStatuses.Refunded, Callback, false)]
        [InlineData(PaymentOrderStatuses.Refunded, Query, false)]
        [InlineData(PaymentOrderStatuses.Refunded, Admin, false)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded, Callback, false)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded, Admin, false)]
        public void CanMarkPaid_FollowsTheTable(string current, PaymentTransitionSource source, bool expected)
        {
            Assert.Equal(expected, PaymentOrderStateMachine.CanMarkPaid(current, source));
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending, Callback, true)]
        [InlineData(PaymentOrderStatuses.Pending, Query, true)]
        [InlineData(PaymentOrderStatuses.Pending, Cleanup, false)]
        [InlineData(PaymentOrderStatuses.Pending, Admin, false)]
        // expired stays expired; the failure callback is recorded as ignored
        [InlineData(PaymentOrderStatuses.Expired, Callback, false)]
        [InlineData(PaymentOrderStatuses.Expired, Query, false)]
        [InlineData(PaymentOrderStatuses.Expired, Admin, false)]
        // failed / cancelled: no change
        [InlineData(PaymentOrderStatuses.Failed, Callback, false)]
        [InlineData(PaymentOrderStatuses.Failed, Query, false)]
        [InlineData(PaymentOrderStatuses.Cancelled, Callback, false)]
        [InlineData(PaymentOrderStatuses.Cancelled, Query, false)]
        // manual_review: admin only (reject)
        [InlineData(PaymentOrderStatuses.ManualReview, Callback, false)]
        [InlineData(PaymentOrderStatuses.ManualReview, Query, false)]
        [InlineData(PaymentOrderStatuses.ManualReview, Admin, true)]
        // never from paid or refunded
        [InlineData(PaymentOrderStatuses.Paid, Callback, false)]
        [InlineData(PaymentOrderStatuses.Paid, Query, false)]
        [InlineData(PaymentOrderStatuses.Paid, Admin, false)]
        [InlineData(PaymentOrderStatuses.Refunded, Callback, false)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded, Callback, false)]
        public void CanMarkFailedOrCancelled_FollowsTheTable(string current, PaymentTransitionSource source, bool expected)
        {
            Assert.Equal(expected, PaymentOrderStateMachine.CanMarkFailedOrCancelled(current, source));
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending, true)]
        [InlineData(PaymentOrderStatuses.Expired, false)]
        [InlineData(PaymentOrderStatuses.Failed, false)]
        [InlineData(PaymentOrderStatuses.Cancelled, false)]
        [InlineData(PaymentOrderStatuses.ManualReview, false)]
        [InlineData(PaymentOrderStatuses.Paid, false)]
        [InlineData(PaymentOrderStatuses.Refunded, false)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded, false)]
        public void CanExpire_OnlyPending(string current, bool expected)
        {
            Assert.Equal(expected, PaymentOrderStateMachine.CanExpire(current));
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending, Callback, true)]
        [InlineData(PaymentOrderStatuses.Pending, Query, true)]
        [InlineData(PaymentOrderStatuses.Pending, Admin, true)]
        [InlineData(PaymentOrderStatuses.Pending, Cleanup, false)]
        [InlineData(PaymentOrderStatuses.Expired, Callback, true)]
        [InlineData(PaymentOrderStatuses.Expired, Query, true)]
        [InlineData(PaymentOrderStatuses.Expired, Admin, true)]
        [InlineData(PaymentOrderStatuses.Failed, Callback, false)]
        [InlineData(PaymentOrderStatuses.Failed, Admin, true)]
        [InlineData(PaymentOrderStatuses.Cancelled, Query, false)]
        [InlineData(PaymentOrderStatuses.Cancelled, Admin, true)]
        [InlineData(PaymentOrderStatuses.ManualReview, Admin, false)]
        [InlineData(PaymentOrderStatuses.ManualReview, Callback, false)]
        [InlineData(PaymentOrderStatuses.Paid, Admin, false)]
        [InlineData(PaymentOrderStatuses.Paid, Callback, false)]
        [InlineData(PaymentOrderStatuses.Refunded, Admin, false)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded, Admin, false)]
        public void CanSendToManualReview_FollowsTheTable(string current, PaymentTransitionSource source, bool expected)
        {
            Assert.Equal(expected, PaymentOrderStateMachine.CanSendToManualReview(current, source));
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending, true)]
        [InlineData(PaymentOrderStatuses.Expired, true)]
        [InlineData(PaymentOrderStatuses.Failed, true)]
        [InlineData(PaymentOrderStatuses.Cancelled, true)]
        [InlineData(PaymentOrderStatuses.ManualReview, false)]
        [InlineData(PaymentOrderStatuses.Paid, false)]
        [InlineData(PaymentOrderStatuses.Refunded, false)]
        public void CanSendToManualReview_AnySource(string current, bool expected)
        {
            Assert.Equal(expected, PaymentOrderStateMachine.CanSendToManualReview(current));
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending, false)]
        [InlineData(PaymentOrderStatuses.ManualReview, false)]
        [InlineData(PaymentOrderStatuses.Paid, true)]
        [InlineData(PaymentOrderStatuses.Failed, true)]
        [InlineData(PaymentOrderStatuses.Cancelled, true)]
        [InlineData(PaymentOrderStatuses.Expired, true)]
        [InlineData(PaymentOrderStatuses.Refunded, true)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded, true)]
        public void IsTerminal_OnlyOpenStatesAreNotTerminal(string current, bool expected)
        {
            Assert.Equal(expected, PaymentOrderStateMachine.IsTerminal(current));
        }

        [Fact]
        public void StatusMatching_IgnoresCaseAndWhitespace()
        {
            Assert.True(PaymentOrderStateMachine.CanMarkPaid(" Pending ", Callback));
            Assert.False(PaymentOrderStateMachine.CanMarkPaid("REFUNDED", Callback));
        }

        [Fact]
        public void UnknownStatus_IsRejectedEverywhere()
        {
            Assert.False(PaymentOrderStateMachine.CanMarkPaid("mystery", Admin));
            Assert.False(PaymentOrderStateMachine.CanMarkFailedOrCancelled("mystery", Callback));
            Assert.False(PaymentOrderStateMachine.CanExpire("mystery"));
        }
    }
}
