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
                Moq.Mock.Of<TCTEnglish.Services.Billing.IPaymentQueryService>(),
                Moq.Mock.Of<TCTEnglish.Services.Billing.ISubscriptionService>(),
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
        public void MoMoQr_OldLink_RedirectsToTheOrderPage()
        {
            var controller = new BillingController(
                new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),
                Mock.Of<IBillingService>(),
                Mock.Of<IPremiumAccessService>(),
                Mock.Of<IIpnService>(),
                Mock.Of<IPaymentProviderHealthService>(),
                Options.Create(new MoMoOptions()),
                new StubWebHostEnvironment { EnvironmentName = "Development" },
                Moq.Mock.Of<TCTEnglish.Services.Billing.IPaymentQueryService>(),
                Moq.Mock.Of<TCTEnglish.Services.Billing.ISubscriptionService>(),
                NullLogger<BillingController>.Instance);

            var result = controller.MoMoQr("ORD-1");

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal("/Billing/Order/ORD-1", redirect.Url);
            Assert.IsType<NotFoundResult>(controller.MoMoQr(" "));
        }
    }
}
