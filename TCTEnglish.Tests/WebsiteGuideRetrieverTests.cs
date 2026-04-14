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
        Assert.Equal("/Class/Create", guide.Route);
    }

    [Fact]
    public async Task RetrieveAsync_FindsGuideForSpeakingTopic()
    {
        var retriever = new WebsiteGuideRetriever(GetGuideFilePath());

        var snippets = await retriever.RetrieveAsync(1, "hướng dẫn luyện shadowing", CancellationToken.None);

        var guide = Assert.Single(snippets);
        Assert.Contains("Shadowing", guide.Body);
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
