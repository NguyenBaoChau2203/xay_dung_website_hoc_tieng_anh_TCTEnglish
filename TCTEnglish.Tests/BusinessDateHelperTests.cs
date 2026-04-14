using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class BusinessDateHelperTests
{
    [Fact]
    public void ToBusinessDateFromUtcStorage_UnspecifiedSqlServerDateTime2_ConvertsFromUtc()
    {
        var storedUtcDateTime = new DateTime(2026, 3, 29, 17, 30, 0, DateTimeKind.Unspecified);

        var businessDate = BusinessDateHelper.ToBusinessDateFromUtcStorage(storedUtcDateTime);

        Assert.Equal(new DateTime(2026, 3, 30), businessDate);
    }

    [Fact]
    public void ToBusinessDate_UnspecifiedDateOnly_KeepsExistingDate()
    {
        var storedBusinessDate = new DateTime(2026, 3, 30, 0, 0, 0, DateTimeKind.Unspecified);

        var businessDate = BusinessDateHelper.ToBusinessDate(storedBusinessDate);

        Assert.Equal(new DateTime(2026, 3, 30), businessDate);
    }
}
