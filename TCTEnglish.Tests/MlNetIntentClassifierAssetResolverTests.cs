using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI.Internal;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class MlNetIntentClassifierAssetResolverTests
{
    [Fact]
    public void ResolveSnapshot_UsesDefaultsUnderContentRoot()
    {
        var root = CreateTempDirectory();
        var resolver = CreateResolver(root, new MlNetIntentClassifierOptions());

        var snapshot = resolver.ResolveSnapshot();

        Assert.EndsWith(Path.Combine("Services", "AI", "Internal", "Data", "intent-samples.seed.csv"), snapshot.SeedDatasetAbsolutePath);
        Assert.EndsWith(Path.Combine("Services", "AI", "Internal", "Data", "intent-classifier-model.zip"), snapshot.ModelArtifactAbsolutePath);
    }

    [Fact]
    public void ResolveSnapshot_WhenFilesExist_MarksExistenceFlags()
    {
        var root = CreateTempDirectory();
        var options = new MlNetIntentClassifierOptions
        {
            SeedDatasetPath = "data/seed.csv",
            ModelArtifactPath = "data/model.zip"
        };

        var datasetPath = Path.Combine(root, "data", "seed.csv");
        var modelPath = Path.Combine(root, "data", "model.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(datasetPath)!);
        File.WriteAllText(datasetPath, "Text,Label\nhello,Greeting");
        File.WriteAllText(modelPath, "zip-placeholder");

        var resolver = CreateResolver(root, options);

        var snapshot = resolver.ResolveSnapshot();

        Assert.True(snapshot.SeedDatasetExists);
        Assert.True(snapshot.ModelArtifactExists);
    }

    [Fact]
    public void ResolveSnapshot_WhenConfiguredWithAbsolutePaths_UsesAbsolutePaths()
    {
        var root = CreateTempDirectory();
        var absoluteDatasetPath = Path.Combine(root, "absolute-dataset.csv");
        var absoluteModelPath = Path.Combine(root, "absolute-model.zip");

        var resolver = CreateResolver(
            root,
            new MlNetIntentClassifierOptions
            {
                SeedDatasetPath = absoluteDatasetPath,
                ModelArtifactPath = absoluteModelPath
            });

        var snapshot = resolver.ResolveSnapshot();

        Assert.Equal(Path.GetFullPath(absoluteDatasetPath), snapshot.SeedDatasetAbsolutePath);
        Assert.Equal(Path.GetFullPath(absoluteModelPath), snapshot.ModelArtifactAbsolutePath);
    }

    [Fact]
    public void ResolveSnapshot_WhenModelNotCreated_ModelFlagIsFalse()
    {
        var root = CreateTempDirectory();
        var options = new MlNetIntentClassifierOptions
        {
            SeedDatasetPath = "data/seed.csv",
            ModelArtifactPath = "data/model.zip"
        };

        var datasetPath = Path.Combine(root, "data", "seed.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(datasetPath)!);
        File.WriteAllText(datasetPath, "Text,Label\nhello,Greeting");

        var resolver = CreateResolver(root, options);

        var snapshot = resolver.ResolveSnapshot();

        Assert.True(snapshot.SeedDatasetExists);
        Assert.False(snapshot.ModelArtifactExists);
    }

    private static MlNetIntentClassifierAssetResolver CreateResolver(string contentRoot, MlNetIntentClassifierOptions options)
    {
        return new MlNetIntentClassifierAssetResolver(
            new StubHostEnvironment(contentRoot),
            Options.Create(options));
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
