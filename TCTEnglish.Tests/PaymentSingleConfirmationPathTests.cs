using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>
    /// The money rule (gate G16): an order becomes <c>paid</c> in exactly one place,
    /// <c>PaymentConfirmationService</c>, and only for a provider callback, a verified provider query with the right
    /// amount, or an admin action. The behaviour is proven by the service, IPN, query and admin tests; this guard stops
    /// a new code path from assigning the status somewhere else.
    /// </summary>
    public class PaymentSingleConfirmationPathTests
    {
        private static readonly string AppRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TCTEnglish"));

        private static string[] AppSources() => Directory
            .GetFiles(AppRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .ToArray();

        [Fact]
        public void TheStatusPaid_IsAssignedOnlyInsideTheConfirmationService()
        {
            var assignment = new Regex(@"\.Status\s*=\s*(PaymentOrderStatuses\.Paid|""paid"")", RegexOptions.CultureInvariant);

            var offenders = AppSources()
                .Where(f => assignment.IsMatch(File.ReadAllText(f)))
                .Select(f => Path.GetFileName(f))
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(new[] { "PaymentConfirmationService.cs" }, offenders);
        }

        [Fact]
        public void EveryWayToConfirm_GoesThroughConfirmPaidAsync()
        {
            string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { AppRoot }.Concat(parts).ToArray()));

            Assert.Contains("ConfirmPaidAsync", Read("Services", "Billing", "IpnService.cs"));
            Assert.Contains("ConfirmPaidAsync", Read("Services", "Billing", "PaymentQueryService.cs"));
            Assert.Contains("ConfirmPaidAsync", Read("Areas", "Admin", "Controllers", "BillingManagementController.cs"));
        }

        [Fact]
        public void TheReturnHandlers_NeverConfirmDirectly()
        {
            var controller = File.ReadAllText(Path.Combine(AppRoot, "Controllers", "BillingController.cs"));
            var service = File.ReadAllText(Path.Combine(AppRoot, "Services", "Billing", "BillingService.cs"));

            Assert.DoesNotContain("ConfirmPaidAsync", controller);
            Assert.DoesNotContain("ConfirmPaidAsync", service);
            Assert.DoesNotContain("ActivateFromPaidOrderAsync", controller);
            Assert.DoesNotContain("ActivateFromPaidOrderAsync", service);
        }

        [Fact]
        public void OnlyTheConfirmationService_AndTheManualGrant_ActivateSubscriptions()
        {
            var callers = AppSources()
                .Where(f => File.ReadAllText(f).Contains("ActivateFromPaidOrderAsync", StringComparison.Ordinal))
                .Select(Path.GetFileName)
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(new[] { "ISubscriptionService.cs", "PaymentConfirmationService.cs", "SubscriptionService.cs" }, callers);
        }
    }
}
