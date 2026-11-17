using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTVocabulary.Areas.Admin.Controllers;
using TCTVocabulary.Areas.Admin.ViewModels.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>
    /// Admin billing (plan 4.8, D3 = fixes and copy only): manual confirmation is one transaction through the
    /// confirmation service, status changes obey the state machine, and every message is correct Vietnamese.
    /// </summary>
    public class AdminBillingStateRulesTests
    {
        private static DbflashcardContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new DbflashcardContext(options);
        }

        private sealed record Setup(
            BillingManagementController Controller,
            DbflashcardContext Ctx,
            Mock<IPaymentAuditService> Audit,
            ITempDataDictionary TempData);

        private static Setup Create(IPaymentConfirmationService? confirmation = null)
        {
            var ctx = CreateContext();
            var audit = new Mock<IPaymentAuditService>();
            var subscriptions = new SubscriptionService(ctx);
            var service = confirmation
                ?? new PaymentConfirmationService(ctx, subscriptions, NullLogger<PaymentConfirmationService>.Instance);
            var controller = new BillingManagementController(
                ctx, subscriptions, new Mock<IPaymentProviderHealthService>().Object, audit.Object, service,
                new Mock<ILogger<BillingManagementController>>().Object);
            var http = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "999") }, "Test"))
            };
            var tempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            controller.TempData = tempData;
            return new Setup(controller, ctx, audit, tempData);
        }

        private static async Task<(PaymentOrder order, User user)> SeedOrderAsync(
            DbflashcardContext ctx, string status, TimeSpan? expiresIn = null, string code = "ORD-ADM-1")
        {
            var user = new User { Email = $"u-{Guid.NewGuid():N}@test.com", PasswordHash = "h", Role = Roles.Standard };
            var plan = new PremiumPlan
            {
                Code = $"p-{Guid.NewGuid():N}", Name = "Plan", Description = "d", PriceVnd = 49000m, DurationDays = 30,
                IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();
            var order = new PaymentOrder
            {
                OrderCode = code, UserId = user.UserId, PlanId = plan.Id, Provider = PaymentProviders.VNPay, AmountVnd = 49000m,
                Currency = "VND", Status = status, CreatedAtUtc = DateTime.UtcNow.AddHours(-2), UpdatedAtUtc = DateTime.UtcNow.AddHours(-2),
                ExpiresAtUtc = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(-100))
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();
            return (order, user);
        }

        private static AdminResolveManualReviewRequest Resolve(long id) => new() { PaymentOrderId = id, Reason = "Đã đối soát với ngân hàng" };

        // ─── Confirm paid ─────────────────────────────────────────────────────────

        [Fact]
        public async Task ConfirmPaid_IsOneTransaction_ThatSetsEveryTimestampAndTheActivation()
        {
            var s = Create();
            var (order, user) = await SeedOrderAsync(s.Ctx, PaymentOrderStatuses.ManualReview);

            var result = await s.Controller.ResolveManualReviewConfirmPaid(Resolve(order.Id));

            Assert.IsType<RedirectToActionResult>(result);
            var saved = await s.Ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Paid, saved.Status);
            Assert.NotNull(saved.PaidAtUtc);
            Assert.NotNull(saved.ConfirmedAtUtc);
            Assert.Equal(999, saved.ConfirmedByUserId);
            Assert.NotNull(saved.ActivatedAtUtc);   // F5: it used to stay empty
            Assert.Single(s.Ctx.UserSubscriptions.Where(x => x.UserId == user.UserId));
            Assert.Contains(s.Ctx.PaymentEvents, e => e.EventType == PaymentEventTypes.ManualConfirm && e.PaymentOrderId == order.Id);
            Assert.Contains(s.Ctx.PaymentEvents, e => e.EventType == PaymentEventTypes.GrantPremium);
            Assert.Equal("Đơn ORD-ADM-1 đã được xác nhận đã thanh toán và kích hoạt gói.", s.TempData["Success"]);
            s.Audit.Verify(a => a.RecordAsync(
                999, AdminActionTypes.ResolveManualReview, "Đã đối soát với ngân hàng", order.Id, null,
                PaymentOrderStatuses.ManualReview, PaymentOrderStatuses.Paid,
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
        }

        [Fact]
        public async Task ConfirmPaid_WorksForAnOrderThatIsMoreThanADayLate()
        {
            var s = Create();
            var (order, _) = await SeedOrderAsync(s.Ctx, PaymentOrderStatuses.ManualReview, expiresIn: TimeSpan.FromHours(-30));

            await s.Controller.ResolveManualReviewConfirmPaid(Resolve(order.Id));

            Assert.Equal(PaymentOrderStatuses.Paid, (await s.Ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending)]
        [InlineData(PaymentOrderStatuses.Paid)]
        [InlineData(PaymentOrderStatuses.Refunded)]
        [InlineData(PaymentOrderStatuses.Failed)]
        public async Task ConfirmPaid_OutsideManualReview_IsRefused_WithAVietnameseMessage(string status)
        {
            var s = Create();
            var (order, user) = await SeedOrderAsync(s.Ctx, status);

            await s.Controller.ResolveManualReviewConfirmPaid(Resolve(order.Id));

            Assert.Equal("Chỉ có thể xác nhận đã thanh toán cho đơn đang ở trạng thái kiểm tra thủ công.", s.TempData["Error"]);
            Assert.Equal(status, (await s.Ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status);
            Assert.Empty(s.Ctx.UserSubscriptions.Where(x => x.UserId == user.UserId));
        }

        [Fact]
        public async Task ConfirmPaid_WhenTheServiceRefuses_ShowsTheMessage_AndWritesNoAuditRecord()
        {
            var refusing = new Mock<IPaymentConfirmationService>();
            refusing.Setup(c => c.ConfirmPaidAsync(It.IsAny<PaymentConfirmation>(), default))
                .ReturnsAsync(new PaymentConfirmationResult(PaymentConfirmationOutcome.RejectedState, null));
            var s = Create(refusing.Object);
            var (order, _) = await SeedOrderAsync(s.Ctx, PaymentOrderStatuses.ManualReview);

            await s.Controller.ResolveManualReviewConfirmPaid(Resolve(order.Id));

            Assert.Equal("Không thể xác nhận đã thanh toán: đơn đã đổi trạng thái. Hãy tải lại trang chi tiết.", s.TempData["Error"]);
            s.Audit.Verify(a => a.RecordAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        }

        [Fact]
        public async Task ConfirmPaid_SendsTheAdminSource_NoAmount_AndTheReason()
        {
            PaymentConfirmation? seen = null;
            var spy = new Mock<IPaymentConfirmationService>();
            spy.Setup(c => c.ConfirmPaidAsync(It.IsAny<PaymentConfirmation>(), default))
                .Callback<PaymentConfirmation, System.Threading.CancellationToken>((c, _) => seen = c)
                .ReturnsAsync(new PaymentConfirmationResult(PaymentConfirmationOutcome.Activated, DateTime.UtcNow.AddDays(30)));
            var s = Create(spy.Object);
            var (order, _) = await SeedOrderAsync(s.Ctx, PaymentOrderStatuses.ManualReview);

            await s.Controller.ResolveManualReviewConfirmPaid(Resolve(order.Id));

            Assert.NotNull(seen);
            Assert.Equal(PaymentTransitionSource.Admin, seen!.Source);
            Assert.Null(seen.VerifiedAmountVnd);
            Assert.Equal(PaymentEventTypes.ManualConfirm, seen.EventType);
            Assert.Equal("Đã đối soát với ngân hàng", seen.AdminReason);
            Assert.Equal(999, seen.AdminUserId);
        }

        // ─── Reject and mark ──────────────────────────────────────────────────────

        [Fact]
        public async Task Reject_MovesAReviewedOrderToFailed_WithTheMessage()
        {
            var s = Create();
            var (order, _) = await SeedOrderAsync(s.Ctx, PaymentOrderStatuses.ManualReview);

            await s.Controller.ResolveManualReviewReject(Resolve(order.Id));

            Assert.Equal(PaymentOrderStatuses.Failed, (await s.Ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status);
            Assert.Equal("Đơn ORD-ADM-1 đã bị từ chối.", s.TempData["Success"]);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending)]
        [InlineData(PaymentOrderStatuses.Paid)]
        [InlineData(PaymentOrderStatuses.Refunded)]
        public async Task Reject_OutsideManualReview_IsRefused(string status)
        {
            var s = Create();
            var (order, _) = await SeedOrderAsync(s.Ctx, status);

            await s.Controller.ResolveManualReviewReject(Resolve(order.Id));

            Assert.Equal("Chỉ có thể từ chối đơn đang ở trạng thái kiểm tra thủ công.", s.TempData["Error"]);
            Assert.Equal(status, (await s.Ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending)]
        [InlineData(PaymentOrderStatuses.Expired)]
        [InlineData(PaymentOrderStatuses.Failed)]
        [InlineData(PaymentOrderStatuses.Cancelled)]
        public async Task MarkManualReview_AllowedStates_MoveTheOrder(string status)
        {
            var s = Create();
            var (order, _) = await SeedOrderAsync(s.Ctx, status);

            await s.Controller.MarkManualReview(new AdminMarkManualReviewRequest { PaymentOrderId = order.Id, Reason = "Cần xem lại" });

            Assert.Equal(PaymentOrderStatuses.ManualReview, (await s.Ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status);
            Assert.Equal("Đơn ORD-ADM-1 đã được chuyển sang kiểm tra thủ công.", s.TempData["Success"]);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Paid, "Đã thanh toán")]
        [InlineData(PaymentOrderStatuses.Refunded, "Đã hoàn tiền")]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded, "Hoàn tiền một phần")]
        [InlineData(PaymentOrderStatuses.ManualReview, "Kiểm tra thủ công")]
        public async Task MarkManualReview_OnPaidRefundedOrAlreadyReviewedOrders_IsRefused(string status, string label)
        {
            var s = Create();
            var (order, _) = await SeedOrderAsync(s.Ctx, status);

            await s.Controller.MarkManualReview(new AdminMarkManualReviewRequest { PaymentOrderId = order.Id, Reason = "Cần xem lại" });

            Assert.Equal($"Không thể chuyển đơn đang ở trạng thái \"{label}\" sang kiểm tra thủ công.", s.TempData["Error"]);
            Assert.Equal(status, (await s.Ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status);
            s.Audit.Verify(a => a.RecordAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task Cleanup_ExpiresOnlyPendingOrders_WithVietnameseMessages()
        {
            var s = Create();
            var (stale, _) = await SeedOrderAsync(s.Ctx, PaymentOrderStatuses.Pending, TimeSpan.FromMinutes(-5), "ORD-STALE");
            var (review, _) = await SeedOrderAsync(s.Ctx, PaymentOrderStatuses.ManualReview, TimeSpan.FromMinutes(-5), "ORD-REVIEW");

            await s.Controller.CleanupExpiredPending();

            Assert.Equal(PaymentOrderStatuses.Expired, (await s.Ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == stale.Id)).Status);
            Assert.Equal(PaymentOrderStatuses.ManualReview, (await s.Ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == review.Id)).Status);
            Assert.Equal("Đã dọn 1 đơn đang chờ quá hạn (chuyển sang hết hạn).", s.TempData["Success"]);
            Assert.Equal("Đơn chờ quá hạn được dọn bởi quản trị viên.", (await s.Ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == stale.Id)).FailureMessage);

            await s.Controller.CleanupExpiredPending();
            Assert.Equal("Không có đơn đang chờ quá hạn để dọn.", s.TempData["Success"]);
        }

        // ─── Copy and display (sources) ───────────────────────────────────────────

        private static string Source(params string[] parts)
            => File.ReadAllText(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", "TCTEnglish" }.Concat(parts).ToArray()));

        [Theory]
        [InlineData("Areas", "Admin", "Controllers", "BillingManagementController.cs")]
        [InlineData("Controllers", "BillingController.cs")]
        [InlineData("Services", "Billing", "BillingService.cs")]
        [InlineData("Services", "Billing", "MoMo", "MoMoGateway.cs")]
        [InlineData("Services", "Billing", "VnPay", "VnPayGateway.cs")]
        public void BillingMessages_HaveNoMoreUndiacriticisedVietnamese(params string[] path)
        {
            var source = Source(path);

            foreach (var phrase in new[]
                     {
                         "Chi co", "da duoc", "Khong co don", "Don pending", "Thong tin dang ky", "Cong thanh toan",
                         "chua duoc cau hinh", "Khong the ket noi", "tu choi tao", "Phan hoi tu", "So tien thanh toan",
                         "Don hang da het han", "Order thieu", "kich hoat subscription", "Manual Review."
                     })
            {
                Assert.DoesNotContain(phrase, source, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AdminBillingViews_HaveNoUndiacriticisedVietnameseCopy()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TCTEnglish", "Areas", "Admin", "Views", "BillingManagement");
            var words = new System.Text.RegularExpressions.Regex(
                @"\b(hoan tien|chua duoc|khong the|vui long|nguoi dung|quy trinh|cap nhat|thu cong|thanh cong)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (var file in Directory.GetFiles(dir, "*.cshtml"))
            {
                Assert.False(words.IsMatch(File.ReadAllText(file)), Path.GetFileName(file));
            }
        }

        [Fact]
        public void AdminBillingViews_ShowVietnamTimeThroughBillingTime_NotAHardCodedOffset()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TCTEnglish", "Areas", "Admin", "Views", "BillingManagement");

            foreach (var file in Directory.GetFiles(dir, "*.cshtml"))
            {
                var text = File.ReadAllText(file);
                Assert.DoesNotContain("AddHours(7)", text);
                Assert.DoesNotContain("ToLocalTime()", text);
                Assert.DoesNotContain("ToString(\"dd/MM/yyyy HH:mm\")", text);
            }
        }

        [Fact]
        public void TheHeaderButton_IsInVietnamese()
        {
            var layout = Source("Views", "Shared", "_Layout.cshtml");

            Assert.Contains("Nâng cấp Premium", layout);
            Assert.DoesNotContain("Upgrade: Premium", layout);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Pending, "Chờ thanh toán")]
        [InlineData(PaymentOrderStatuses.ManualReview, "Kiểm tra thủ công")]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded, "Hoàn tiền một phần")]
        [InlineData("something_new", "something_new")]
        public void AdminStatusLabels_AreVietnamese_AndUnknownValuesPassThrough(string status, string expected)
        {
            Assert.Equal(expected, PaymentOrderLabels.StatusLabel(status));
        }
    }
}
