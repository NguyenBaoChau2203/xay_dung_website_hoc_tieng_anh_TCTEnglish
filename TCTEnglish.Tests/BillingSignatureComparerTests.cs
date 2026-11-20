using System;
using System.IO;
using TCTEnglish.Services.Billing;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Finding A2 (plan 12): provider signatures must be compared in constant time, for every provider.
/// </summary>
public sealed class BillingSignatureComparerTests
{
    private const string Sample = "8d8640d802576397a1ce45ebda7f835055768ac7ad2e0bfb77f9b8f12cca4c7f";

    [Fact]
    public void HexEquals_SameDigest_IgnoresCaseAndSurroundingWhitespace()
    {
        Assert.True(BillingSignatureComparer.HexEquals(Sample, Sample));
        Assert.True(BillingSignatureComparer.HexEquals(Sample, Sample.ToUpperInvariant()));
        Assert.True(BillingSignatureComparer.HexEquals(Sample, "  " + Sample + "\t"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HexEquals_MissingValue_IsNeverEqual(string? received)
    {
        Assert.False(BillingSignatureComparer.HexEquals(Sample, received));
        Assert.False(BillingSignatureComparer.HexEquals(received, Sample));
        Assert.False(BillingSignatureComparer.HexEquals(received, received));
    }

    [Fact]
    public void HexEquals_OneCharacterChanged_OrDifferentLength_IsNotEqual()
    {
        var flipped = (Sample[0] == '0' ? '1' : '0') + Sample[1..];
        var lastFlipped = Sample[..^1] + (Sample[^1] == '0' ? '1' : '0');

        Assert.False(BillingSignatureComparer.HexEquals(Sample, flipped));
        Assert.False(BillingSignatureComparer.HexEquals(Sample, lastFlipped));
        Assert.False(BillingSignatureComparer.HexEquals(Sample, Sample[..^1]));
        Assert.False(BillingSignatureComparer.HexEquals(Sample, Sample + "0"));
    }

    [Theory]
    [InlineData("VnPay", "VnPaySignatureHelper.cs")]
    [InlineData("MoMo", "MoMoSignatureHelper.cs")]
    public void ProviderSignatureHelpers_CompareThroughTheConstantTimeHelper(string folder, string file)
    {
        var source = File.ReadAllText(Path.Combine(
            GetWorkspaceRoot(), "TCTEnglish", "Services", "Billing", folder, file));

        Assert.Contains("BillingSignatureComparer.HexEquals(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(computedHash", source, StringComparison.Ordinal);
        Assert.DoesNotContain("receivedSignature.Trim(),", source, StringComparison.Ordinal);
    }

    [Fact]
    public void VnPayQueryClient_VerifiesTheResponseHashThroughTheConstantTimeHelper()
    {
        var source = File.ReadAllText(Path.Combine(
            GetWorkspaceRoot(), "TCTEnglish", "Services", "Billing", "VnPay", "VnPayQueryClient.cs"));

        Assert.Contains("BillingSignatureComparer.HexEquals(computedHash, receivedHash)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("computedHash, receivedHash.Trim()", source, StringComparison.Ordinal);
    }

    private static string GetWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TCTEnglish.sln"))
                || Directory.Exists(Path.Combine(directory.FullName, "TCTEnglish", "Services")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Workspace root was not found.");
    }
}
