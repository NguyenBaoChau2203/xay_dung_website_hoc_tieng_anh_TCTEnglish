using Microsoft.ML;

namespace TCTEnglish.Services.AI.Internal;

public sealed class MlNetTrainerService : IMlNetTrainerService
{
    private readonly MlNetIntentDatasetLoader _datasetLoader;
    private readonly MlNetIntentClassifierAssetResolver _assetResolver;
    private readonly ILogger<MlNetTrainerService> _logger;

    public MlNetTrainerService(
        MlNetIntentDatasetLoader datasetLoader,
        MlNetIntentClassifierAssetResolver assetResolver,
        ILogger<MlNetTrainerService> logger)
    {
        _datasetLoader = datasetLoader;
        _assetResolver = assetResolver;
        _logger = logger;
    }

    public async Task<MlNetTrainingResult> TrainAndSaveModelAsync(CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;

        try
        {
            var examples = await _datasetLoader.LoadSeedDatasetAsync(ct);
            ct.ThrowIfCancellationRequested();

            var rows = examples
                .Select(x => new MlNetTrainingInput
                {
                    Text = x.Text,
                    Label = x.Intent.ToString()
                })
                .ToList();

            var sampleCount = rows.Count;
            var intentCount = rows
                .Select(x => x.Label)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (sampleCount == 0)
            {
                return CreateFailure("Dataset rỗng. Không thể huấn luyện model.", startedAt);
            }

            if (intentCount < 2)
            {
                return CreateFailure("Dataset phải có ít nhất 2 intent để huấn luyện.", startedAt);
            }

            var snapshot = _assetResolver.ResolveSnapshot();
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot.ModelArtifactAbsolutePath)!);

            var trainingOutcome = await Task.Run(() => TrainAndPersist(rows, snapshot.ModelArtifactAbsolutePath, ct), ct);

            _logger.LogInformation(
                "ML.NET model trained successfully. Samples={SampleCount}, Intents={IntentCount}, MicroAccuracy={MicroAccuracy:F4}, MacroAccuracy={MacroAccuracy:F4}, ModelPath={ModelPath}",
                sampleCount,
                intentCount,
                trainingOutcome.MicroAccuracy,
                trainingOutcome.MacroAccuracy,
                snapshot.ModelArtifactAbsolutePath);

            return new MlNetTrainingResult(
                Success: true,
                SampleCount: sampleCount,
                IntentCount: intentCount,
                MicroAccuracy: trainingOutcome.MicroAccuracy,
                MacroAccuracy: trainingOutcome.MacroAccuracy,
                ErrorMessage: null,
                Duration: DateTime.UtcNow - startedAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ML.NET model training failed.");
            return CreateFailure(ex.Message, startedAt);
        }
    }

    private static (float MicroAccuracy, float MacroAccuracy) TrainAndPersist(
        IReadOnlyList<MlNetTrainingInput> rows,
        string modelPath,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var ml = new MLContext(seed: 42);
        var data = ml.Data.LoadFromEnumerable(rows);

        var pipeline = ml.Transforms.Text.FeaturizeText("Features", nameof(MlNetTrainingInput.Text))
            .Append(ml.Transforms.Conversion.MapValueToKey("Label", nameof(MlNetTrainingInput.Label)))
            .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
            .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        var folds = Math.Min(5, rows.Count);
        if (folds < 2)
        {
            throw new InvalidOperationException("Dataset phải có ít nhất 2 dòng để cross-validation.");
        }

        var crossValidationResults = ml.MulticlassClassification.CrossValidate(
            data,
            pipeline,
            numberOfFolds: folds,
            labelColumnName: "Label");

        ct.ThrowIfCancellationRequested();

        var microAccuracy = (float)crossValidationResults.Average(x => x.Metrics.MicroAccuracy);
        var macroAccuracy = (float)crossValidationResults.Average(x => x.Metrics.MacroAccuracy);

        var finalModel = pipeline.Fit(data);
        ml.Model.Save(finalModel, data.Schema, modelPath);

        return (microAccuracy, macroAccuracy);
    }

    private static MlNetTrainingResult CreateFailure(string errorMessage, DateTime startedAt)
    {
        return new MlNetTrainingResult(
            Success: false,
            SampleCount: 0,
            IntentCount: 0,
            MicroAccuracy: 0,
            MacroAccuracy: 0,
            ErrorMessage: errorMessage,
            Duration: DateTime.UtcNow - startedAt);
    }
}
