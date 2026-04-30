using System;
using System.IO;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class IpnServiceStructureTests
{
    [Fact]
    public void IpnService_SuccessfulPaymentPath_UsesExecutionStrategyWrappedTransaction()
    {
        var source = ReadIpnServiceSource();
        var helperSlice = SliceFrom(
            source,
            "private async Task ProcessSuccessfulPaymentAsync(");

        Assert.Contains("_context.Database.CreateExecutionStrategy()", helperSlice, StringComparison.Ordinal);
        Assert.Contains("strategy.ExecuteAsync", helperSlice, StringComparison.Ordinal);
        Assert.Contains("_context.ChangeTracker.Clear()", helperSlice, StringComparison.Ordinal);
        Assert.Contains("_context.Database.BeginTransactionAsync(ct)", helperSlice, StringComparison.Ordinal);
    }

    private static string ReadIpnServiceSource()
    {
        return File.ReadAllText(Path.Combine(
            GetWorkspaceRoot(),
            "TCTEnglish",
            "Services",
            "Billing",
            "IpnService.cs"));
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
