using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using TCTEnglish.Services.AI.Internal;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class MlNetAiQueryClassifierTests
{
    [Fact]
    public void Classify_WhenModelMissing_UsesDeterministicFallback()
    {
        var root = CreateTempDirectory();
        var classifier = CreateClassifier(root, new MlNetIntentClassifierOptions
        {
            ModelArtifactPath = "missing/intent-classifier-model.zip"
        });

        var result = classifier.Classify("xin chao ban");

        Assert.Equal(UserIntent.Greeting, result.Intent);
        Assert.Equal("keyword", result.ClassifierName);
    }

    [Fact]
    public void Classify_WhenModelExists_ReturnsMlNetPrediction()
    {
        var root = CreateTempDirectory();
        var modelPath = Path.Combine(root, "data", "model.zip");
        TrainAndSaveModel(
            modelPath,
            [
                ("xin chao", nameof(UserIntent.Greeting)),
                ("hello ban", nameof(UserIntent.Greeting)),
                ("chao ban nhe", nameof(UserIntent.Greeting)),
                ("good morning", nameof(UserIntent.Greeting)),
                ("hi ban oi", nameof(UserIntent.Greeting)),
                ("hey chao", nameof(UserIntent.Greeting)),
                ("chao buoi sang", nameof(UserIntent.Greeting)),
                ("chao buoi chieu", nameof(UserIntent.Greeting)),
                ("hello xin chao", nameof(UserIntent.Greeting)),
                ("chao ban toi la ai", nameof(UserIntent.Greeting)),
                ("xin chao ban la tro ly", nameof(UserIntent.Greeting)),
                ("chao nhe", nameof(UserIntent.Greeting)),
                ("hi there", nameof(UserIntent.Greeting)),
                ("hey hello", nameof(UserIntent.Greeting)),
                ("chao ban toi", nameof(UserIntent.Greeting)),
                ("goi y bai speaking", nameof(UserIntent.SpeakingSuggestion)),
                ("de xuat bai speaking", nameof(UserIntent.SpeakingSuggestion)),
                ("speaking video nao phu hop", nameof(UserIntent.SpeakingSuggestion)),
                ("goi y video speaking phu hop", nameof(UserIntent.SpeakingSuggestion)),
                ("bai speaking nao nen hoc", nameof(UserIntent.SpeakingSuggestion)),
                ("cho toi goi y speaking", nameof(UserIntent.SpeakingSuggestion)),
                ("de xuat video speaking level a1", nameof(UserIntent.SpeakingSuggestion)),
                ("video speaking goi y cho toi", nameof(UserIntent.SpeakingSuggestion)),
                ("speaking nao de hoc", nameof(UserIntent.SpeakingSuggestion)),
                ("goi y playlist speaking", nameof(UserIntent.SpeakingSuggestion)),
                ("bai speaking phu hop voi toi", nameof(UserIntent.SpeakingSuggestion)),
                ("de xuat speaking moi", nameof(UserIntent.SpeakingSuggestion)),
                ("speaking video goi y", nameof(UserIntent.SpeakingSuggestion)),
                ("speaking nao tot nhat", nameof(UserIntent.SpeakingSuggestion)),
                ("cho toi xem bai speaking phu hop", nameof(UserIntent.SpeakingSuggestion))
            ]);

        var classifier = CreateClassifier(root, new MlNetIntentClassifierOptions
        {
            ModelArtifactPath = "data/model.zip"
        });

        var result = classifier.Classify("xin chao");

        Assert.Equal(UserIntent.Greeting, result.Intent);
        Assert.Equal("mlnet", result.ClassifierName);
        Assert.InRange(result.Confidence, 0f, 1f);
    }

    [Fact]
    public void Classify_AppliesSynonymNormalizationBeforeMlPrediction()
    {
        var root = CreateTempDirectory();
        var modelPath = Path.Combine(root, "data", "model.zip");
        TrainAndSaveModel(
            modelPath,
            [
                ("bo tu vung cua toi", nameof(UserIntent.MyVocabulary)),
                ("toi co bo tu vung nao", nameof(UserIntent.MyVocabulary)),
                ("xem bo tu vung", nameof(UserIntent.MyVocabulary)),
                ("danh sach bo tu vung toi co", nameof(UserIntent.MyVocabulary)),
                ("bo tu vung cua toi co bao nhieu", nameof(UserIntent.MyVocabulary)),
                ("toi co nhung bo tu vung nao", nameof(UserIntent.MyVocabulary)),
                ("bo tu vung list", nameof(UserIntent.MyVocabulary)),
                ("co bao nhieu bo tu vung", nameof(UserIntent.MyVocabulary)),
                ("bo tu vung toi da tao", nameof(UserIntent.MyVocabulary)),
                ("xem danh sach bo tu vung", nameof(UserIntent.MyVocabulary)),
                ("bo tu vung moi nhat cua toi", nameof(UserIntent.MyVocabulary)),
                ("toi co nhung set tu vung nao", nameof(UserIntent.MyVocabulary)),
                ("bo tu vung cua toi dau", nameof(UserIntent.MyVocabulary)),
                ("list bo tu vung toi co", nameof(UserIntent.MyVocabulary)),
                ("show bo tu vung cua toi", nameof(UserIntent.MyVocabulary)),
                ("cach tao lop hoc", nameof(UserIntent.WebsiteGuide)),
                ("huong dan tao class", nameof(UserIntent.WebsiteGuide)),
                ("lam sao tao lop hoc moi", nameof(UserIntent.WebsiteGuide)),
                ("cach tao bo tu vung moi", nameof(UserIntent.WebsiteGuide)),
                ("huong dan su dung flashcard", nameof(UserIntent.WebsiteGuide)),
                ("cach doi mat khau tai khoan", nameof(UserIntent.WebsiteGuide)),
                ("huong dan tao set tu vung", nameof(UserIntent.WebsiteGuide)),
                ("lam sao xoa lop hoc", nameof(UserIntent.WebsiteGuide)),
                ("cach chinh sua profile", nameof(UserIntent.WebsiteGuide)),
                ("huong dan hoc speaking", nameof(UserIntent.WebsiteGuide)),
                ("cach tham gia lop hoc", nameof(UserIntent.WebsiteGuide)),
                ("huong dan quiz mode", nameof(UserIntent.WebsiteGuide)),
                ("cach su dung matching game", nameof(UserIntent.WebsiteGuide)),
                ("huong dan dung chatbox", nameof(UserIntent.WebsiteGuide)),
                ("lam sao de tao thu muc", nameof(UserIntent.WebsiteGuide))
            ]);

        var classifier = CreateClassifier(root, new MlNetIntentClassifierOptions
        {
            ModelArtifactPath = "data/model.zip"
        });

        var result = classifier.Classify("bo the cua toi");

        Assert.Equal(UserIntent.MyVocabulary, result.Intent);
        Assert.Equal("mlnet", result.ClassifierName);
    }

    [Fact]
    public void InvalidateModel_ClearsCachedRuntimeAndLoadsLatestModel()
    {
        var root = CreateTempDirectory();
        var modelPath = Path.Combine(root, "data", "model.zip");

        TrainAndSaveModel(
            modelPath,
            [
                ("xin chao", nameof(UserIntent.Greeting)),
                ("hello", nameof(UserIntent.Greeting)),
                ("chao ban", nameof(UserIntent.Greeting)),
                ("hi", nameof(UserIntent.Greeting)),
                ("goi y speaking", nameof(UserIntent.SpeakingSuggestion)),
                ("video speaking", nameof(UserIntent.SpeakingSuggestion)),
                ("hoc speaking", nameof(UserIntent.SpeakingSuggestion)),
                ("de xuat speaking", nameof(UserIntent.SpeakingSuggestion))
            ]);

        var classifier = CreateClassifier(root, new MlNetIntentClassifierOptions
        {
            ModelArtifactPath = "data/model.zip"
        });

        var firstPrediction = classifier.Classify("xin chao");
        Assert.Equal(UserIntent.Greeting, firstPrediction.Intent);

        TrainAndSaveModel(
            modelPath,
            [
                ("xin chao", nameof(UserIntent.SpeakingSuggestion)),
                ("hello", nameof(UserIntent.SpeakingSuggestion)),
                ("chao ban", nameof(UserIntent.SpeakingSuggestion)),
                ("hi", nameof(UserIntent.SpeakingSuggestion)),
                ("bo tu vung", nameof(UserIntent.MyVocabulary)),
                ("set tu vung", nameof(UserIntent.MyVocabulary)),
                ("xem bo tu", nameof(UserIntent.MyVocabulary)),
                ("vocabulary cua toi", nameof(UserIntent.MyVocabulary))
            ]);

        var cachedPrediction = classifier.Classify("xin chao");
        Assert.Equal(UserIntent.Greeting, cachedPrediction.Intent);

        classifier.InvalidateModel();
        var reloadedPrediction = classifier.Classify("xin chao");

        Assert.Equal(UserIntent.SpeakingSuggestion, reloadedPrediction.Intent);
        Assert.Equal("mlnet", reloadedPrediction.ClassifierName);
    }

    private static MlNetAiQueryClassifier CreateClassifier(string contentRoot, MlNetIntentClassifierOptions options)
    {
        var resolver = new MlNetIntentClassifierAssetResolver(
            new StubHostEnvironment(contentRoot),
            Options.Create(options));

        return new MlNetAiQueryClassifier(
            resolver,
            new DeterministicIntentClassifier(),
            NullLogger<MlNetAiQueryClassifier>.Instance);
    }

    private static void TrainAndSaveModel(string modelPath, IReadOnlyList<(string Text, string Label)> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);

        var ml = new MLContext(seed: 42);
        var data = ml.Data.LoadFromEnumerable(rows.Select(x => new TrainRow { Text = x.Text, Label = x.Label }));

        var pipeline = ml.Transforms.Text.FeaturizeText("Features", nameof(TrainRow.Text))
            .Append(ml.Transforms.Conversion.MapValueToKey("Label", nameof(TrainRow.Label)))
            .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
            .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));

        var model = pipeline.Fit(data);
        ml.Model.Save(model, data.Schema, modelPath);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "tct-mlnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TrainRow
    {
        [LoadColumn(0)]
        public string Text { get; set; } = string.Empty;

        [LoadColumn(1)]
        public string Label { get; set; } = string.Empty;
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
