using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI.Internal;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class MlNetIntentDatasetLoaderTests
{
    [Fact]
    public void ParseLines_SkipsHeaderAndComments()
    {
        var rows = MlNetIntentDatasetLoader.ParseLines(
        [
            "Text,Label",
            "# comment",
            "Xin chào,Greeting",
            "Tôi có những bộ từ nào?,MyVocabulary"
        ],
        "test.csv");

        Assert.Equal(2, rows.Count);
        Assert.Equal(UserIntent.Greeting, rows[0].Intent);
        Assert.Equal(UserIntent.MyVocabulary, rows[1].Intent);
    }

    [Fact]
    public void ParseLines_WhenInvalidFormat_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => MlNetIntentDatasetLoader.ParseLines(["missing separator"], "test.csv"));
    }

    [Fact]
    public void ParseLines_WhenUnknownLabel_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => MlNetIntentDatasetLoader.ParseLines(["hello,UnknownIntent"], "test.csv"));
    }

    [Fact]
    public void ParseLines_WhenTextIsEmpty_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => MlNetIntentDatasetLoader.ParseLines([",Greeting"], "test.csv"));
    }

    [Fact]
    public async Task LoadSeedDatasetAsync_WhenFileMissing_ThrowsFileNotFoundException()
    {
        var root = CreateTempDirectory();
        var loader = CreateLoader(root, new MlNetIntentClassifierOptions
        {
            SeedDatasetPath = "data/missing.csv"
        });

        await Assert.ThrowsAsync<FileNotFoundException>(() => loader.LoadSeedDatasetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task LoadSeedDatasetAsync_LoadsRealSeedDataset_WithAllFrozenIntents()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TCTEnglish"));
        var loader = CreateLoader(root, new MlNetIntentClassifierOptions());

        var rows = await loader.LoadSeedDatasetAsync(CancellationToken.None);

        var intents = rows.Select(x => x.Intent).Distinct().OrderBy(x => x.ToString()).ToArray();

        Assert.True(rows.Count >= 450);
        Assert.Equal(
        [
            UserIntent.CardLookup,
            UserIntent.ClassInfo,
            UserIntent.Greeting,
            UserIntent.MyProgress,
            UserIntent.MyVocabulary,
            UserIntent.OutOfScope,
            UserIntent.SpeakingSuggestion,
            UserIntent.StudyRecommendation,
            UserIntent.WebsiteGuide
        ],
        intents);
    }

    [Fact]
    public async Task LoadSeedDatasetAsync_LoadsRowsForEveryIntentAtLeastFifty()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TCTEnglish"));
        var loader = CreateLoader(root, new MlNetIntentClassifierOptions());

        var rows = await loader.LoadSeedDatasetAsync(CancellationToken.None);

        var countsByIntent = rows
            .GroupBy(x => x.Intent)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var intent in Enum.GetValues<UserIntent>())
        {
            Assert.True(countsByIntent.TryGetValue(intent, out var count), $"Missing samples for intent {intent}");
            Assert.True(count >= 50, $"Intent {intent} must have >= 50 samples, found {count}");
        }
    }

    private static MlNetIntentDatasetLoader CreateLoader(string contentRoot, MlNetIntentClassifierOptions options)
    {
        var resolver = new MlNetIntentClassifierAssetResolver(
            new StubHostEnvironment(contentRoot),
            Options.Create(options));

        return new MlNetIntentDatasetLoader(resolver);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "tct-mlnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHostEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "TCTEnglish.Tests";

        public string WebRootPath { get; set; } = contentRoot;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = contentRoot;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
