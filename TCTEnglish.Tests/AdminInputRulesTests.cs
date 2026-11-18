using TCTEnglish.Services.Admin;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AdminInputRulesTests
{
    private const string Id = "dQw4w9WgXcQ";

    [Theory]
    [InlineData(Id)]
    [InlineData("  dQw4w9WgXcQ  ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ&t=42s")]
    [InlineData("https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc123")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ?start=10")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("youtu.be/dQw4w9WgXcQ")]
    [InlineData("www.youtube.com/watch?v=dQw4w9WgXcQ")]
    public void TryParseYoutubeId_AcceptsEveryKnownShape(string input)
    {
        Assert.True(AdminInputRules.TryParseYoutubeId(input, out var id));
        Assert.Equal(Id, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("dQw4w9WgXc")]
    [InlineData("dQw4w9WgXcQQ")]
    [InlineData("dQw4w9WgX!Q")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/watch")]
    [InlineData("https://vimeo.com/dQw4w9WgXcQ")]
    [InlineData("https://evil.example/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com.evil.example/watch?v=dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://www.youtube.com/channel/UC1234567890123456789012")]
    public void TryParseYoutubeId_RejectsEverythingElse(string? input)
    {
        Assert.False(AdminInputRules.TryParseYoutubeId(input, out var id));
        Assert.Null(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://cdn.example.com/a/b.png")]
    [InlineData("https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg")]
    [InlineData("/images/animals/cat.jpg")]
    [InlineData("/uploads/premium/abc-1.png")]
    public void IsSafeImageUrl_AcceptsEmptyHttpsAndLocalPaths(string? url)
    {
        Assert.True(AdminInputRules.IsSafeImageUrl(url));
    }

    [Theory]
    [InlineData("http://cdn.example.com/a.png")]
    [InlineData("//cdn.example.com/a.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("ftp://cdn.example.com/a.png")]
    [InlineData("images/a.png")]
    [InlineData("/\\evil.example/a.png")]
    [InlineData("/../etc/passwd")]
    [InlineData("/uploads/a b.png")]
    [InlineData("https://")]
    [InlineData("https:///a.png")]
    [InlineData("https://exa mple.com/a.png")]
    [InlineData("https://cdn.example.com/a.png\n")]
    public void IsSafeImageUrl_RejectsEverythingElse(string url)
    {
        Assert.False(AdminInputRules.IsSafeImageUrl(url));
        Assert.False(AdminInputRules.IsSafeMediaUrl(url));
    }

    [Fact]
    public void IsSafeMediaUrl_UsesTheSameRuleAsImages()
    {
        Assert.True(AdminInputRules.IsSafeMediaUrl(null));
        Assert.True(AdminInputRules.IsSafeMediaUrl("https://cdn.example.com/a.mp3"));
        Assert.True(AdminInputRules.IsSafeMediaUrl("/media/a.mp3"));
        Assert.False(AdminInputRules.IsSafeMediaUrl("http://cdn.example.com/a.mp3"));
    }

    [Theory]
    [InlineData("4:12", "04:12")]
    [InlineData("04:12", "04:12")]
    [InlineData(" 4:05 ", "04:05")]
    [InlineData("0:45", "00:45")]
    [InlineData("75:30", "75:30")]
    [InlineData("1:02:03", "1:02:03")]
    [InlineData("01:02:03", "1:02:03")]
    [InlineData("12:00:00", "12:00:00")]
    public void TryNormalizeDuration_NormalizesKnownShapes(string input, string expected)
    {
        Assert.True(AdminInputRules.TryNormalizeDuration(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalizeDuration_TreatsEmptyAsNotSet(string? input)
    {
        Assert.True(AdminInputRules.TryNormalizeDuration(input, out var normalized));
        Assert.Null(normalized);
    }

    [Theory]
    [InlineData("4 phút")]
    [InlineData("4:7")]
    [InlineData("4:60")]
    [InlineData("1:60:00")]
    [InlineData("1:2:03")]
    [InlineData("12")]
    [InlineData("-1:30")]
    [InlineData("1:30:00:00")]
    [InlineData("00:00")]
    [InlineData("a:bc")]
    [InlineData("1000:00")]
    public void TryNormalizeDuration_RejectsOtherText(string input)
    {
        Assert.False(AdminInputRules.TryNormalizeDuration(input, out var normalized));
        Assert.Null(normalized);
    }

    [Fact]
    public void CefrLevels_AreTheSixLevelsInOrder()
    {
        Assert.Equal(new[] { "A1", "A2", "B1", "B2", "C1", "C2" }, AdminInputRules.CefrLevels);
        Assert.True(AdminInputRules.IsCefrLevel("B2"));
        Assert.False(AdminInputRules.IsCefrLevel("b2"));
        Assert.False(AdminInputRules.IsCefrLevel("D1"));
        Assert.False(AdminInputRules.IsCefrLevel(null));
        Assert.False(AdminInputRules.IsCefrLevel(""));
    }

    [Fact]
    public void NormalizeText_TrimsAndCollapsesSingleLineText()
    {
        var check = AdminInputRules.NormalizeText("  Hello \t  big\n world  ", 100);
        Assert.Equal("Hello big world", check.Value);
        Assert.False(check.TooLong);
        Assert.False(check.IsEmpty);
    }

    [Fact]
    public void NormalizeText_KeepsLineBreaksInMultilineText()
    {
        var check = AdminInputRules.NormalizeText("  line one  \r\n\r\n\r\n\r\n  line   two  ", 100, singleLine: false);
        Assert.Equal("line one\n\nline   two", check.Value);
        Assert.False(check.TooLong);
    }

    [Fact]
    public void NormalizeText_ReportsTextOverTheLimitWithoutCutting()
    {
        var check = AdminInputRules.NormalizeText(new string('a', 11), 10);
        Assert.True(check.TooLong);
        Assert.Equal(11, check.Value.Length);

        Assert.False(AdminInputRules.NormalizeText(new string('a', 10), 10).TooLong);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    public void NormalizeText_TreatsBlankAsEmpty(string? input)
    {
        var check = AdminInputRules.NormalizeText(input, 10);
        Assert.True(check.IsEmpty);
        Assert.Equal(string.Empty, check.Value);
        Assert.False(check.TooLong);
    }

    [Fact]
    public void YoutubeThumbnailUrl_BuildsTheDefaultImage()
    {
        Assert.Equal($"https://img.youtube.com/vi/{Id}/hqdefault.jpg", AdminInputRules.YoutubeThumbnailUrl(Id));
        Assert.Null(AdminInputRules.YoutubeThumbnailUrl(null));
        Assert.Null(AdminInputRules.YoutubeThumbnailUrl(""));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(99, 5)]
    public void ClampPage_KeepsThePageInsideTheRange(int requested, int expected)
    {
        Assert.Equal(expected, AdminInputRules.ClampPage(requested, 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ClampPage_AlwaysReturnsAtLeastPageOne(int totalPages)
    {
        Assert.Equal(1, AdminInputRules.ClampPage(7, totalPages));
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(41, 20, 3)]
    public void TotalPages_RoundsUp(int total, int pageSize, int expected)
    {
        Assert.Equal(expected, AdminInputRules.TotalPages(total, pageSize));
    }
}
