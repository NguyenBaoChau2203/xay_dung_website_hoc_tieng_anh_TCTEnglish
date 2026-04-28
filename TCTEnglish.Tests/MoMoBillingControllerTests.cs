using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TCTEnglish.Controllers;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Tests.TestHelpers;
using TCTEnglish.ViewModels.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    public class MoMoBillingControllerTests
    {
        [Fact]
        public async Task MoMoIpn_ReturnsNoContent()
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            await using var context = new DbflashcardContext(options);

            var billingService = new Mock<IBillingService>();
            var premiumService = new Mock<IPremiumAccessService>();
            var ipnService = new Mock<IIpnService>();
            var healthService = new Mock<IPaymentProviderHealthService>();

            var controller = new BillingController(
                context,
                billingService.Object,
                premiumService.Object,
                ipnService.Object,
                healthService.Object,
                Options.Create(new MoMoOptions()),
                new StubWebHostEnvironment { EnvironmentName = "Development" },
                NullLogger<BillingController>.Instance);

            var httpContext = new DefaultHttpContext();
            var json = "{\"orderId\":\"ORD-1\",\"resultCode\":0,\"signature\":\"abc\"}";
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = await controller.MoMoIpn();
            Assert.IsType<NoContentResult>(result);
            ipnService.Verify(s => s.ProcessMoMoIpnAsync(It.IsAny<IDictionary<string, string>>(), default), Times.Once);
        }

        [Fact]
        public async Task MoMoQr_OtherUserOrder_ReturnsNotFound()
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            await using (var seed = new DbflashcardContext(options))
            {
                var owner = new User { Email = "owner@test.com", PasswordHash = "h", Role = Roles.Standard };
                seed.Users.Add(owner);
                var other = new User { Email = "other@test.com", PasswordHash = "h", Role = Roles.Standard };
                seed.Users.Add(other);
                var plan = new PremiumPlan
                {
                    Code = "plan",
                    Name = "Plan",
                    Description = "Desc",
                    PriceVnd = 49000m,
                    DurationDays = 30,
                    IsActive = true,
                    DisplayOrder = 1,
                    CreatedAtUtc = DateTime.UtcNow
                };
                seed.PremiumPlans.Add(plan);
                await seed.SaveChangesAsync();

                seed.PaymentOrders.Add(new PaymentOrder
                {
                    OrderCode = "ORD-1",
                    UserId = owner.UserId,
                    PlanId = plan.Id,
                    Provider = PaymentProviders.MoMo,
                    AmountVnd = 49000m,
                    Currency = "VND",
                    Status = PaymentOrderStatuses.Pending,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
                });
                await seed.SaveChangesAsync();
            }

            await using var context = new DbflashcardContext(options);
            var controller = new BillingController(
                context,
                Mock.Of<IBillingService>(),
                Mock.Of<IPremiumAccessService>(),
                Mock.Of<IIpnService>(),
                Mock.Of<IPaymentProviderHealthService>(),
                Options.Create(new MoMoOptions()),
                new StubWebHostEnvironment { EnvironmentName = "Development" },
                NullLogger<BillingController>.Instance);

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "2"),
                new Claim(ClaimTypes.Email, "other@test.com")
            }, "Test");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            controller.TempData = new TempDataDictionary(
                controller.HttpContext,
                Mock.Of<ITempDataProvider>());

            var result = await controller.MoMoQr("ORD-1");
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
