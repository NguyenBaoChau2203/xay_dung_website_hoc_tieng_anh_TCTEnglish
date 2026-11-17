using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TCTEnglish.Controllers;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>The MoMo IPN endpoint never answers 500 and never reads more than 64 KB (plan 4.3, F9).</summary>
    public class MoMoIpnBodyHandlingTests
    {
        private static (BillingController controller, Mock<IIpnService> ipn, ListLogger logger) Create(byte[] body)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var context = new DbflashcardContext(options);
            var ipn = new Mock<IIpnService>();
            var logger = new ListLogger();

            var controller = new BillingController(
                context,
                Mock.Of<IBillingService>(),
                Mock.Of<IPremiumAccessService>(),
                ipn.Object,
                Mock.Of<IPaymentProviderHealthService>(),
                Options.Create(new MoMoOptions()),
                new StubWebHostEnvironment { EnvironmentName = "Development" },
                Moq.Mock.Of<TCTEnglish.Services.Billing.IPaymentQueryService>(),
                Moq.Mock.Of<TCTEnglish.Services.Billing.ISubscriptionService>(),
                logger);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body = new MemoryStream(body);
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return (controller, ipn, logger);
        }

        [Fact]
        public async Task ValidObject_IsPassedToTheService_AndAnswers204()
        {
            var (controller, ipn, _) = Create(Encoding.UTF8.GetBytes("{\"orderId\":\"ORD-1\",\"resultCode\":0}"));

            var result = await controller.MoMoIpn();

            Assert.IsType<NoContentResult>(result);
            ipn.Verify(s => s.ProcessMoMoIpnAsync(
                It.Is<IDictionary<string, string>>(d => d["orderId"] == "ORD-1" && d["resultCode"] == "0"), default), Times.Once);
        }

        [Theory]
        [InlineData("not json at all")]
        [InlineData("{\"orderId\": ")]
        [InlineData("")]
        [InlineData("[1,2,3]")]
        [InlineData("\"just a string\"")]
        [InlineData("42")]
        public async Task MalformedOrNonObjectBody_Answers204_WithoutCallingTheService(string body)
        {
            var (controller, ipn, logger) = Create(Encoding.UTF8.GetBytes(body));

            var result = await controller.MoMoIpn();

            Assert.IsType<NoContentResult>(result);
            ipn.Verify(s => s.ProcessMoMoIpnAsync(It.IsAny<IDictionary<string, string>>(), default), Times.Never);
            var warning = Assert.Single(logger.Lines, l => l.Level == LogLevel.Warning);
            Assert.DoesNotContain("not json", warning.Message);
            Assert.DoesNotContain("just a string", warning.Message);
        }

        [Fact]
        public async Task BodyLargerThan64Kb_Answers204_WithoutCallingTheService()
        {
            var big = new byte[64 * 1024 + 10];
            Array.Fill(big, (byte)'a');
            var (controller, ipn, _) = Create(big);

            var result = await controller.MoMoIpn();

            Assert.IsType<NoContentResult>(result);
            ipn.Verify(s => s.ProcessMoMoIpnAsync(It.IsAny<IDictionary<string, string>>(), default), Times.Never);
        }

        [Fact]
        public async Task ABodyOfExactly64Kb_IsStillRead()
        {
            var padding = new string('x', 64 * 1024 - "{\"a\":\"\"}".Length);
            var json = $"{{\"a\":\"{padding}\"}}";
            Assert.Equal(64 * 1024, Encoding.UTF8.GetByteCount(json));
            var (controller, ipn, _) = Create(Encoding.UTF8.GetBytes(json));

            var result = await controller.MoMoIpn();

            Assert.IsType<NoContentResult>(result);
            ipn.Verify(s => s.ProcessMoMoIpnAsync(It.IsAny<IDictionary<string, string>>(), default), Times.Once);
        }

        private sealed class ListLogger : ILogger<BillingController>
        {
            public List<(LogLevel Level, string Message)> Lines { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => Lines.Add((logLevel, formatter(state, exception)));
        }
    }
}
