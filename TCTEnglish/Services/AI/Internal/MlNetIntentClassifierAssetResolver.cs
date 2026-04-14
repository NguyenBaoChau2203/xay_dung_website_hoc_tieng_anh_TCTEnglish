using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.AI.Internal;

public sealed class MlNetIntentClassifierAssetResolver
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly MlNetIntentClassifierOptions _options;

    public MlNetIntentClassifierAssetResolver(
        IHostEnvironment hostEnvironment,
        IOptions<MlNetIntentClassifierOptions> options)
    {
        _hostEnvironment = hostEnvironment;
        _options = options.Value;
    }

    public MlNetIntentClassifierAssetSnapshot ResolveSnapshot()
    {
        var datasetPath = ResolveAbsolutePath(_options.SeedDatasetPath);
        var modelPath = ResolveAbsolutePath(_options.ModelArtifactPath);

        return new MlNetIntentClassifierAssetSnapshot(
            datasetPath,
            File.Exists(datasetPath),
            modelPath,
            File.Exists(modelPath));
    }

    private string ResolveAbsolutePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return _hostEnvironment.ContentRootPath;
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configuredPath));
    }
}
