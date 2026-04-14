using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI.Internal;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class MlNetTrainerServiceTests
{
    [Fact]
    public async Task TrainAndSaveModelAsync_WithValidDataset_SavesModelAndReturnsMetrics()
    {
        var root = CreateTempDirectory();
        var datasetPath = Path.Combine(root, "data", "intent-samples.seed.csv");
        var modelPath = Path.Combine(root, "data", "intent-classifier-model.zip");

        Directory.CreateDirectory(Path.GetDirectoryName(datasetPath)!);
        await File.WriteAllLinesAsync(
            datasetPath,
            [
                "Text,Label",
                "xin chao ban,Greeting",
                "hello ban,Greeting",
                "chao nhe,Greeting",
                "hi there,Greeting",
                "goi y speaking,SpeakingSuggestion",
                "de xuat speaking,SpeakingSuggestion",
                "video speaking nao,SpeakingSuggestion",
                "hoc speaking o dau,SpeakingSuggestion"
            ]);

        var service = CreateService(root, datasetPath, modelPath);

        var result = await service.TrainAndSaveModelAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(8, result.SampleCount);
        Assert.Equal(2, result.IntentCount);
        Assert.True(File.Exists(modelPath));
        Assert.InRange(result.MicroAccuracy, 0f, 1f);
        Assert.InRange(result.MacroAccuracy, 0f, 1f);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task TrainAndSaveModelAsync_WhenDatasetMissing_ReturnsFailureResult()
    {
        var root = CreateTempDirectory();
        var datasetPath = Path.Combine(root, "data", "missing.csv");
        var modelPath = Path.Combine(root, "data", "intent-classifier-model.zip");
        var service = CreateService(root, datasetPath, modelPath);

        var result = await service.TrainAndSaveModelAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.False(File.Exists(modelPath));
    }

    private static MlNetTrainerService CreateService(string contentRoot, string datasetPath, string modelPath)
    {
        var options = new MlNetIntentClassifierOptions
        {
            SeedDatasetPath = Path.GetRelativePath(contentRoot, datasetPath),
            ModelArtifactPath = Path.GetRelativePath(contentRoot, modelPath)
        };

        var resolver = new MlNetIntentClassifierAssetResolver(
            new StubHostEnvironment(contentRoot),
            Options.Create(options));

        var loader = new MlNetIntentDatasetLoader(resolver);

        return new MlNetTrainerService(loader, resolver, NullLogger<MlNetTrainerService>.Instance);
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
