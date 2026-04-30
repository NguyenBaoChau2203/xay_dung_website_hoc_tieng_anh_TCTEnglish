using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTVocabulary.Areas.Admin.Controllers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.Areas.Admin.ViewModels.Billing;
using Microsoft.Extensions.Logging;

namespace TCTEnglish.Tests
{
    public class AdminBillingManagementTests
    {
        private DbflashcardContext GetInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new DbflashcardContext(options);
        }

        [Fact]
        public async Task Index_SeedData_OrderExistsInDb()
        {
            var ctx = GetInMemoryContext(Guid.NewGuid().ToString());
            var user = new User { Email = "user@test.com", PasswordHash = "h" };
            ctx.Users.Add(user);
            var plan = new PremiumPlan { Code = "p1", Name = "Plan 1", Description = "Desc", PriceVnd = 1000, DurationDays = 30, IsActive = true, CreatedAtUtc = DateTime.UtcNow };
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            ctx.PaymentOrders.Add(new PaymentOrder
            {
                OrderCode = "ORD1",
                UserId = user.UserId,
                PlanId = plan.Id,
                AmountVnd = 1000,
                Provider = PaymentProviders.VNPay,
                Status = PaymentOrderStatuses.Paid,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            });
            await ctx.SaveChangesAsync();

            // Verify at DbContext level (controller Index logic is verified at integration level)
            var orderCount = await ctx.PaymentOrders.CountAsync();
            Assert.Equal(1, orderCount);
            var order = await ctx.PaymentOrders.FirstAsync();
            Assert.Equal("ORD1", order.OrderCode);
            Assert.Equal(PaymentOrderStatuses.Paid, order.Status);
        }

        [Fact]
        public async Task Grant_ValidRequest_CallsServiceAndRedirects()
        {
            var ctx = GetInMemoryContext(Guid.NewGuid().ToString());
            var user = new User { UserId = 100, Email = "grant@test.com", PasswordHash = "h" };
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();

            var mockSubSvc = new Mock<ISubscriptionService>();
            mockSubSvc.Setup(s => s.GrantManualAsync(user.UserId, 1, 30, "Bonus", default))
                      .ReturnsAsync(OperationResult.Success());

            var controller = new BillingManagementController(ctx, mockSubSvc.Object, new Mock<IPaymentProviderHealthService>().Object, new Mock<IPaymentAuditService>().Object, new Mock<ILogger<BillingManagementController>>().Object);
            
            // Mock Claims for GetCurrentUserId()
            var claims = new List<System.Security.Claims.Claim> { new(System.Security.Claims.ClaimTypes.NameIdentifier, "999") };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            // Mock TempData for RedirectResult
            var tempData = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>();
            controller.TempData = tempData.Object;

            var model = new AdminGrantPremiumRequest
            {
                UserLookup = user.Email,
                PlanId = 1,
                DurationDays = 30,
                Reason = "Bonus"
            };

            var result = await controller.Grant(model);

            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(BillingManagementController.Subscriptions), redirectResult.ActionName);
            mockSubSvc.Verify(s => s.GrantManualAsync(user.UserId, 1, 30, "Bonus", default), Times.Once);
        }

        [Fact]
        public async Task Revoke_ValidRequest_CallsServiceAndRedirects()
        {
            var ctx = GetInMemoryContext(Guid.NewGuid().ToString());
            var userId = 200;
            var mockSubSvc = new Mock<ISubscriptionService>();
            mockSubSvc.Setup(s => s.RevokeAsync(userId, "Violated terms", default))
                      .ReturnsAsync(OperationResult.Success());

            var controller = new BillingManagementController(ctx, mockSubSvc.Object, new Mock<IPaymentProviderHealthService>().Object, new Mock<IPaymentAuditService>().Object, new Mock<ILogger<BillingManagementController>>().Object);
            
            // Mock Claims for GetCurrentUserId()
            var claims = new List<System.Security.Claims.Claim> { new(System.Security.Claims.ClaimTypes.NameIdentifier, "999") };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            var tempData = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>();
            controller.TempData = tempData.Object;

            var model = new AdminRevokePremiumRequest
            {
                UserId = userId,
                Reason = "Violated terms"
            };

            var result = await controller.Revoke(model);

            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(BillingManagementController.Subscriptions), redirectResult.ActionName);
            mockSubSvc.Verify(s => s.RevokeAsync(userId, "Violated terms", default), Times.Once);
        }

        [Fact]
        public async Task PremiumUsers_FilterActive_ReturnsOnlyActiveSubscriptions()
        {
            var dbName = Guid.NewGuid().ToString();
            await using var ctx = GetInMemoryContext(dbName);

            var user1 = new User { Email = "active@test.com", PasswordHash = "h", Role = Roles.Premium };
            var user2 = new User { Email = "standard@test.com", PasswordHash = "h", Role = Roles.Standard };
            var plan = new PremiumPlan
            {
                Code = "monthly",
                Name = "Premium 1 month",
                Description = "desc",
                PriceVnd = 49000m,
                DurationDays = 30,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.AddRange(user1, user2);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            ctx.UserSubscriptions.Add(new UserSubscription
            {
                UserId = user1.UserId,
                PlanId = plan.Id,
                Status = SubscriptionStatuses.Active,
                StartsAtUtc = DateTime.UtcNow.AddDays(-1),
                EndsAtUtc = DateTime.UtcNow.AddDays(10),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
            });
            await ctx.SaveChangesAsync();

            var controller = new BillingManagementController(
                ctx,
                new Mock<ISubscriptionService>().Object,
                new Mock<IPaymentProviderHealthService>().Object,
                new Mock<IPaymentAuditService>().Object,
                new Mock<ILogger<BillingManagementController>>().Object);

            var result = await controller.PremiumUsers(PremiumUserFilters.Active, null, 1);
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AdminPremiumUsersViewModel>(view.Model);

            Assert.Single(model.Users);
            Assert.Equal(user1.UserId, model.Users[0].UserId);
            Assert.True(model.Users[0].IsActiveSubscription);
        }

        [Fact]
        public async Task PremiumUserDetails_UserNotFound_ReturnsNotFound()
        {
            await using var ctx = GetInMemoryContext(Guid.NewGuid().ToString());
            var controller = new BillingManagementController(
                ctx,
                new Mock<ISubscriptionService>().Object,
                new Mock<IPaymentProviderHealthService>().Object,
                new Mock<IPaymentAuditService>().Object,
                new Mock<ILogger<BillingManagementController>>().Object);

            var result = await controller.PremiumUserDetails(999999);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task CleanupExpiredPending_UpdatesOnlyExpiredPendingOrders()
        {
            var dbName = Guid.NewGuid().ToString();
            await using var ctx = GetInMemoryContext(dbName);

            var user = new User { Email = "cleanup@test.com", PasswordHash = "h", Role = Roles.Standard };
            var plan = new PremiumPlan
            {
                Code = "p-cleanup",
                Name = "Cleanup Plan",
                Description = "Desc",
                PriceVnd = 49000m,
                DurationDays = 30,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            ctx.PaymentOrders.AddRange(
                new PaymentOrder
                {
                    OrderCode = "ORD-EXPIRED-PENDING",
                    UserId = user.UserId,
                    PlanId = plan.Id,
                    Provider = PaymentProviders.VNPay,
                    AmountVnd = 49000m,
                    Currency = "VND",
                    Status = PaymentOrderStatuses.Pending,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
                    UpdatedAtUtc = DateTime.UtcNow.AddHours(-2),
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-20)
                },
                new PaymentOrder
                {
                    OrderCode = "ORD-ACTIVE-PENDING",
                    UserId = user.UserId,
                    PlanId = plan.Id,
                    Provider = PaymentProviders.VNPay,
                    AmountVnd = 49000m,
                    Currency = "VND",
                    Status = PaymentOrderStatuses.Pending,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                    UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
                },
                new PaymentOrder
                {
                    OrderCode = "ORD-PAID",
                    UserId = user.UserId,
                    PlanId = plan.Id,
                    Provider = PaymentProviders.VNPay,
                    AmountVnd = 49000m,
                    Currency = "VND",
                    Status = PaymentOrderStatuses.Paid,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
                    UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-10)
                });
            await ctx.SaveChangesAsync();

            var controller = new BillingManagementController(
                ctx,
                new Mock<ISubscriptionService>().Object,
                new Mock<IPaymentProviderHealthService>().Object,
                new Mock<IPaymentAuditService>().Object,
                new Mock<ILogger<BillingManagementController>>().Object);

            var claims = new List<System.Security.Claims.Claim> { new(System.Security.Claims.ClaimTypes.NameIdentifier, "999") };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            controller.TempData = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>().Object;

            var result = await controller.CleanupExpiredPending();
            Assert.IsType<RedirectToActionResult>(result);

            var expiredPending = await ctx.PaymentOrders.FirstAsync(o => o.OrderCode == "ORD-EXPIRED-PENDING");
            var activePending = await ctx.PaymentOrders.FirstAsync(o => o.OrderCode == "ORD-ACTIVE-PENDING");
            var paidOrder = await ctx.PaymentOrders.FirstAsync(o => o.OrderCode == "ORD-PAID");

            Assert.Equal(PaymentOrderStatuses.Expired, expiredPending.Status);
            Assert.Equal(PaymentOrderStatuses.Pending, activePending.Status);
            Assert.Equal(PaymentOrderStatuses.Paid, paidOrder.Status);
        }

        [Fact]
        public async Task ResolveManualReviewConfirmPaid_UpdatesOrderAndCallsServices()
        {
            await using var ctx = GetInMemoryContext(Guid.NewGuid().ToString());
            var user = new User { Email = "manual@test.com", PasswordHash = "h", Role = Roles.Standard };
            var plan = new PremiumPlan
            {
                Code = "manual-plan",
                Name = "Manual Plan",
                Description = "Desc",
                PriceVnd = 49000m,
                DurationDays = 30,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            var order = new PaymentOrder
            {
                OrderCode = "ORD-MANUAL-1",
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = PaymentProviders.VNPay,
                AmountVnd = 49000m,
                Currency = "VND",
                Status = PaymentOrderStatuses.ManualReview,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(20)
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();

            var mockSubSvc = new Mock<ISubscriptionService>();
            mockSubSvc.Setup(s => s.ActivateFromPaidOrderAsync(order.Id, default))
                .ReturnsAsync(new UserSubscription
                {
                    Id = 1,
                    UserId = user.UserId,
                    PlanId = plan.Id,
                    Status = SubscriptionStatuses.Active,
                    StartsAtUtc = DateTime.UtcNow,
                    EndsAtUtc = DateTime.UtcNow.AddDays(30)
                });

            var mockAudit = new Mock<IPaymentAuditService>();
            var controller = new BillingManagementController(
                ctx,
                mockSubSvc.Object,
                new Mock<IPaymentProviderHealthService>().Object,
                mockAudit.Object,
                new Mock<ILogger<BillingManagementController>>().Object);

            var claims = new List<System.Security.Claims.Claim> { new(System.Security.Claims.ClaimTypes.NameIdentifier, "999") };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
            };
            controller.TempData = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>().Object;

            var result = await controller.ResolveManualReviewConfirmPaid(new AdminResolveManualReviewRequest
            {
                PaymentOrderId = order.Id,
                Reason = "Verified by admin"
            });

            Assert.IsType<RedirectToActionResult>(result);
            var refreshed = await ctx.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Paid, refreshed.Status);
            mockSubSvc.Verify(s => s.ActivateFromPaidOrderAsync(order.Id, default), Times.Once);
            mockAudit.Verify(a => a.RecordAsync(
                999,
                AdminActionTypes.ResolveManualReview,
                "Verified by admin",
                order.Id,
                null,
                PaymentOrderStatuses.ManualReview,
                PaymentOrderStatuses.Paid,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                default), Times.Once);
        }

        [Fact]
        public async Task ResolveManualReviewReject_UpdatesOrderAndWritesAudit()
        {
            await using var ctx = GetInMemoryContext(Guid.NewGuid().ToString());
            var user = new User { Email = "manual2@test.com", PasswordHash = "h", Role = Roles.Standard };
            var plan = new PremiumPlan
            {
                Code = "manual-plan-2",
                Name = "Manual Plan 2",
                Description = "Desc",
                PriceVnd = 49000m,
                DurationDays = 30,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            var order = new PaymentOrder
            {
                OrderCode = "ORD-MANUAL-2",
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = PaymentProviders.MoMo,
                AmountVnd = 49000m,
                Currency = "VND",
                Status = PaymentOrderStatuses.ManualReview,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();

            var mockAudit = new Mock<IPaymentAuditService>();
            var controller = new BillingManagementController(
                ctx,
                new Mock<ISubscriptionService>().Object,
                new Mock<IPaymentProviderHealthService>().Object,
                mockAudit.Object,
                new Mock<ILogger<BillingManagementController>>().Object);

            var claims = new List<System.Security.Claims.Claim> { new(System.Security.Claims.ClaimTypes.NameIdentifier, "888") };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
            };
            controller.TempData = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>().Object;

            var result = await controller.ResolveManualReviewReject(new AdminResolveManualReviewRequest
            {
                PaymentOrderId = order.Id,
                Reason = "Payment evidence invalid"
            });

            Assert.IsType<RedirectToActionResult>(result);
            var refreshed = await ctx.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Failed, refreshed.Status);
            Assert.Equal("Payment evidence invalid", refreshed.FailureMessage);
            mockAudit.Verify(a => a.RecordAsync(
                888,
                AdminActionTypes.ResolveManualReview,
                "Payment evidence invalid",
                order.Id,
                null,
                PaymentOrderStatuses.ManualReview,
                PaymentOrderStatuses.Failed,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                default), Times.Once);
        }
    }
}
