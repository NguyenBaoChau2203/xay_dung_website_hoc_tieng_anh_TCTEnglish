using System;
using System.IO;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class IpnServiceStructureTests
{
    [Fact]
    public void ConfirmationService_SuccessfulPaymentPath_UsesExecutionStrategyWrappedTransaction()
    {
        var source = ReadBillingSource("PaymentConfirmationService.cs");
        var helperSlice = SliceFrom(
            source,
            "public async Task<PaymentConfirmationResult> ConfirmPaidAsync(");

        Assert.Contains("_context.Database.CreateExecutionStrategy()", helperSlice, StringComparison.Ordinal);
        Assert.Contains("strategy.ExecuteAsync", helperSlice, StringComparison.Ordinal);
        Assert.Contains("_context.ChangeTracker.Clear()", helperSlice, StringComparison.Ordinal);
        Assert.Contains("_context.Database.BeginTransactionAsync(ct)", helperSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void IpnService_DelegatesSuccessToTheConfirmationService_AndNeverMarksOrdersPaidItself()
    {
        var source = ReadBillingSource("IpnService.cs");

        Assert.Contains("_confirmationService.ConfirmPaidAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PaymentOrderStatuses.Paid;", source.Replace("== PaymentOrderStatuses.Paid", string.Empty), StringComparison.Ordinal);
        Assert.DoesNotContain("ActivateFromPaidOrderAsync", source, StringComparison.Ordinal);
    }

    private static string ReadBillingSource(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            GetWorkspaceRoot(),
            "TCTEnglish",
            "Services",
            "Billing",
            fileName));
    }

    private static string SliceFrom(string source, string startMarker)
    {
        var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Expected to find start marker: {startMarker}");
        return source[startIndex..];
    }

    private static string GetWorkspaceRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
