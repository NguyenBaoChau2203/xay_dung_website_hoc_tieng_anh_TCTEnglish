using Microsoft.ML;
using Microsoft.ML.Data;

namespace TCTEnglish.Services.AI.Internal;

public sealed class MlNetAiQueryClassifier : IAiQueryClassifier
{
    private readonly MlNetIntentClassifierAssetResolver _assetResolver;
    private readonly DeterministicIntentClassifier _fallbackClassifier;
    private readonly ILogger<MlNetAiQueryClassifier> _logger;
    private readonly object _runtimeLock = new();
    private volatile ModelRuntime? _runtime;
    private volatile bool _runtimeLoadAttempted;

    public MlNetAiQueryClassifier(
        MlNetIntentClassifierAssetResolver assetResolver,
        DeterministicIntentClassifier fallbackClassifier,
        ILogger<MlNetAiQueryClassifier> logger)
    {
        _assetResolver = assetResolver;
        _fallbackClassifier = fallbackClassifier;
        _logger = logger;
    }

    public void InvalidateModel()
    {
        lock (_runtimeLock)
        {
            _runtime = null;
            _runtimeLoadAttempted = false;
        }
    }

    public IntentClassification Classify(string userMessage)
    {
        var normalizedMessage = NormalizeForModel(userMessage);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return _fallbackClassifier.Classify(userMessage);
        }

        var deterministicClassification = _fallbackClassifier.Classify(userMessage);

        var runtime = GetOrLoadRuntime();
        if (runtime is null)
        {
            return deterministicClassification;
        }

        try
        {
            var prediction = runtime.Predict(normalizedMessage);
            if (!Enum.TryParse<UserIntent>(prediction.Label, ignoreCase: true, out var intent))
            {
                _logger.LogWarning(
                    "ML.NET predicted an unknown intent label '{Label}'. Falling back to deterministic classifier.",
                    prediction.Label);
                return deterministicClassification;
            }

            var mlNetClassification = new IntentClassification(intent, prediction.Confidence, "mlnet");
            return ShouldPreferDeterministic(deterministicClassification, mlNetClassification)
                ? deterministicClassification
                : mlNetClassification;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ML.NET prediction failed. Falling back to deterministic classifier.");
            return deterministicClassification;
        }
    }

    private static bool ShouldPreferDeterministic(
        IntentClassification deterministic,
        IntentClassification mlNetClassification)
    {
        if (deterministic.Confidence < 0.3f)
        {
            return false;
        }

        if (deterministic.Intent is not (UserIntent.WebsiteGuide
            or UserIntent.StudyRecommendation
            or UserIntent.OutOfScope))
        {
            return false;
        }

        return mlNetClassification.Intent != deterministic.Intent
            || mlNetClassification.Confidence < 0.75f;
    }

    private ModelRuntime? GetOrLoadRuntime()
    {
        var runtime = _runtime;
        if (runtime is not null)
        {
            return runtime;
        }

        if (_runtimeLoadAttempted)
        {
            return null;
        }

        lock (_runtimeLock)
        {
            runtime = _runtime;
            if (runtime is not null)
            {
                return runtime;
            }

            if (_runtimeLoadAttempted)
            {
                return null;
            }

            runtime = LoadRuntimeOrNull();
            _runtime = runtime;
            _runtimeLoadAttempted = true;
            return runtime;
        }
    }

    private ModelRuntime? LoadRuntimeOrNull()
    {
        var snapshot = _assetResolver.ResolveSnapshot();
        if (!snapshot.ModelArtifactExists)
        {
            _logger.LogWarning(
                "ML.NET model artifact not found at '{ModelPath}'. Deterministic classifier fallback is active.",
                snapshot.ModelArtifactAbsolutePath);
            return null;
        }

        try
        {
            var mlContext = new MLContext(seed: 42);
            using var stream = File.OpenRead(snapshot.ModelArtifactAbsolutePath);
            var model = mlContext.Model.Load(stream, out _);
            return ModelRuntime.Create(mlContext, model);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not load ML.NET model artifact at '{ModelPath}'. Deterministic classifier fallback is active.",
                snapshot.ModelArtifactAbsolutePath);
            return null;
        }
    }

    private static string NormalizeForModel(string userMessage)
    {
        var normalized = AiTextNormalizer.Normalize(userMessage);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        normalized = ReplacePhrase(normalized, "bo the", "bo tu vung");
        normalized = ReplacePhrase(normalized, "set tu", "bo tu vung");
        normalized = ReplacePhrase(normalized, "set vocab", "bo tu vung");
        normalized = ReplacePhrase(normalized, "vocab", "tu vung");
        normalized = ReplacePhrase(normalized, "lop", "lop hoc");
        normalized = ReplacePhrase(normalized, "video noi", "speaking");
        normalized = ReplacePhrase(normalized, "noi tieng anh", "speaking");
        normalized = ReplacePhrase(normalized, "de xuat", "goi y");
        normalized = ReplacePhrase(normalized, "huong dan", "cach");
        normalized = ReplacePhrase(normalized, "tra cuu", "tra tu");

        return normalized;
    }

    private static string ReplacePhrase(string value, string oldPhrase, string newPhrase)
    {
        var oldToken = $" {AiTextNormalizer.Normalize(oldPhrase)} ";
        var newToken = $" {AiTextNormalizer.Normalize(newPhrase)} ";
        return $" {value} ".Replace(oldToken, newToken, StringComparison.Ordinal).Trim();
    }

    private sealed class ModelRuntime
    {
        private readonly PredictionEngine<MlNetTrainingInput, MlNetOutput> _engine;
        private readonly object _predictionLock = new();

        private ModelRuntime(PredictionEngine<MlNetTrainingInput, MlNetOutput> engine)
        {
            _engine = engine;
        }

        public static ModelRuntime Create(MLContext mlContext, ITransformer model)
        {
            var engine = mlContext.Model.CreatePredictionEngine<MlNetTrainingInput, MlNetOutput>(model);
            return new ModelRuntime(engine);
        }

        public (string Label, float Confidence) Predict(string normalizedMessage)
        {
            lock (_predictionLock)
            {
                var prediction = _engine.Predict(new MlNetTrainingInput { Text = normalizedMessage });
                var confidence = ResolveConfidence(prediction.Score);
                var label = prediction.PredictedLabel ?? string.Empty;
                return (label, confidence);
            }
        }

        private static float ResolveConfidence(IReadOnlyList<float>? scores)
        {
            if (scores is null || scores.Count == 0)
            {
                return 0f;
            }

            return Math.Clamp(scores.Max(), 0f, 1f);
        }
    }

    private sealed class MlNetOutput
    {
        [ColumnName("PredictedLabel")]
        public string? PredictedLabel { get; set; }

        [ColumnName("Score")]
        public float[] Score { get; set; } = [];
    }
}
