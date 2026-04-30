using System.Text.Json;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Internal.Retrievers;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WebsiteGuideRetrieverTests
{
    [Fact]
    public async Task RetrieveAsync_FindsGuideFromAccentlessQuery()
    {
        var retriever = new WebsiteGuideRetriever(GetGuideFilePath());

        var snippets = await retriever.RetrieveAsync(1, "cach tao lop hoc", CancellationToken.None);

        var guide = Assert.Single(snippets);
        Assert.Equal(KnowledgeSnippetSources.WebsiteGuide, guide.Source);
        Assert.Equal("/Home/CreateClass", guide.Route);
    }

    [Fact]
    public async Task RetrieveAsync_FindsGuideForSpeakingTopic()
    {
        var retriever = new WebsiteGuideRetriever(GetGuideFilePath());

        var snippets = await retriever.RetrieveAsync(1, "hướng dẫn luyện shadowing", CancellationToken.None);

        var guide = Assert.Single(snippets);
        Assert.Contains("Shadowing", guide.Body);
    }

    [Theory]
    [InlineData("Làm bài Reading như thế nào?", "/Home/Reading")]
    [InlineData("Tính năng Writing hoạt động ra sao?", "/Home/Writing")]
    [InlineData("Tính năng Listening gồm những phần nào?", "/Home/Listening")]
    [InlineData("Cách liên hệ hỗ trợ?", "/Home/Contact")]
    [InlineData("Tôi có thể xem thông báo ở đâu?", "/Notification/Index")]
    [InlineData("Website có những tính năng gì?", "/Home/Index")]
    [InlineData("Premium có gì?", "/Premium")]
    [InlineData("Cách xem lịch sử thanh toán?", "/Billing/History")]
    [InlineData("Trang Grammar ở đâu?", "/Home/Grammar")]
    [InlineData("Xem chính sách bảo mật ở đâu?", "/Home/Privacypolicy")]
    public async Task RetrieveAsync_FindsQuickActionGuideRoutes(string message, string expectedRoute)
    {
        var retriever = new WebsiteGuideRetriever(GetGuideFilePath());

        var snippets = await retriever.RetrieveAsync(1, message, CancellationToken.None);

        var guide = Assert.Single(snippets);
        Assert.Equal(KnowledgeSnippetSources.WebsiteGuide, guide.Source);
        Assert.Equal(expectedRoute, guide.Route);
    }

    [Theory]
    [InlineData("Cách đặt mục tiêu học hằng ngày?", "/Goals", "Trang Goals")]
    [InlineData("Daily challenge ở đâu?", "/Home/Index", "Daily Challenge")]
    [InlineData("Cách gửi ảnh trong chat lớp học?", "/Home/Class", "tab Chat")]
    [InlineData("Làm sao đưa set vào folder?", "/Home/Folder", "Thư mục")]
    [InlineData("Tôi muốn đổi mật khẩu ở đâu?", "/Account/Settings", "Bảo mật")]
    public async Task RetrieveAsync_FindsPhase6HighValueGuides(string message, string expectedRoute, string expectedBodyText)
    {
        var retriever = new WebsiteGuideRetriever(GetGuideFilePath());

        var snippets = await retriever.RetrieveAsync(1, message, CancellationToken.None);

        var guide = Assert.Single(snippets);
        Assert.Equal(KnowledgeSnippetSources.WebsiteGuide, guide.Source);
        Assert.Equal(expectedRoute, guide.Route);
        Assert.Contains(expectedBodyText, guide.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("hướng dẫn tạo set mới", "/Home/CreateSet")]
    [InlineData("chỉnh sửa set ở đâu", "/Home/Folder")]
    [InlineData("hướng dẫn học quiz", "/Home/Folder")]
    [InlineData("hướng dẫn học matching", "/Home/Folder")]
    [InlineData("xem streak ở đâu", "/Home/Index")]
    public async Task RetrieveAsync_UsesCurrentPostRefactorRoutes(string message, string expectedRoute)
    {
        var retriever = new WebsiteGuideRetriever(GetGuideFilePath());

        var snippets = await retriever.RetrieveAsync(1, message, CancellationToken.None);

        var guide = Assert.Single(snippets);
        Assert.Equal(expectedRoute, guide.Route);
    }

    [Fact]
    public async Task RetrieveAsync_PrefersSpecificClassChatGuideOverGenericClassGuide()
    {
        var retriever = new WebsiteGuideRetriever(GetGuideFilePath());

        var snippets = await retriever.RetrieveAsync(1, "class chat gửi ảnh hoạt động thế nào", CancellationToken.None);

        var guide = Assert.Single(snippets);
        Assert.Equal("/Home/Class", guide.Route);
        Assert.Contains("tab Chat", guide.Body);
    }

    [Fact]
    public void GuideRoutes_DoNotContainTemplatePlaceholders()
    {
        using var stream = File.OpenRead(GetGuideFilePath());
        using var document = JsonDocument.Parse(stream);

        var guides = document.RootElement.GetProperty("guides").EnumerateArray();
        foreach (var guide in guides)
        {
            if (!guide.TryGetProperty("route", out var routeElement))
            {
                continue;
            }

            var route = routeElement.GetString();
            if (string.IsNullOrWhiteSpace(route))
            {
                continue;
            }

            Assert.DoesNotContain("{", route, StringComparison.Ordinal);
            Assert.DoesNotContain("}", route, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RetrieveAsync_WhenNoGuideMatches_ReturnsEmpty()
    {
        var retriever = new WebsiteGuideRetriever(GetGuideFilePath());

        var snippets = await retriever.RetrieveAsync(1, "hướng dẫn nộp bài tập toán", CancellationToken.None);

        Assert.Empty(snippets);
    }

    private static string GetGuideFilePath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "TCTEnglish",
            "wwwroot",
            "data",
            "ai",
            "website-guides.json"));
    }
}
