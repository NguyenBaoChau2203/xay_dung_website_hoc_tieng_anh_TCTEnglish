using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class MlNetRuntimeIntegrationTests
{
    [Fact]
    public async Task GenerateReplyAsync_WithMlNetClassifier_UsesMlNetModelName()
    {
        var root = CreateTempDirectory();
        var modelPath = Path.Combine(root, "data", "model.zip");
        TrainAndSaveModel(
            modelPath,
            [
                ("xin chao", nameof(UserIntent.Greeting)),
                ("hello", nameof(UserIntent.Greeting)),
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
                ("toi co bo tu nao", nameof(UserIntent.MyVocabulary)),
                ("bo tu vung cua toi", nameof(UserIntent.MyVocabulary)),
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
                ("show bo tu vung cua toi", nameof(UserIntent.MyVocabulary))
            ]);

        var classifier = CreateClassifier(root, new MlNetIntentClassifierOptions
        {
            ModelArtifactPath = "data/model.zip"
        });

        var provider = new InternalKnowledgeProvider(classifier, [], new EchoComposer());

        var reply = await provider.GenerateReplyAsync(
            10,
            [new AiContextMessage("user", "xin chao")],
            CancellationToken.None);

        Assert.Equal("internal-mlnet", reply.Model);
        Assert.Equal("Greeting", reply.Text);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenMlModelMissing_FallsBackToDeterministicPath()
    {
        var root = CreateTempDirectory();
        var classifier = CreateClassifier(root, new MlNetIntentClassifierOptions
        {
            ModelArtifactPath = "data/missing-model.zip"
        });

        var provider = new InternalKnowledgeProvider(classifier, [], new EchoComposer());

        var reply = await provider.GenerateReplyAsync(
            10,
            [new AiContextMessage("user", "xin chao")],
            CancellationToken.None);

        Assert.Equal("internal-keyword", reply.Model);
        Assert.Equal("Greeting", reply.Text);
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
        var data = ml.Data.LoadFromEnumerable(rows.Select(x => new ModelRow(x.Text, x.Label)));

        var pipeline = ml.Transforms.Text.FeaturizeText("Features", nameof(ModelRow.Text))
            .Append(ml.Transforms.Conversion.MapValueToKey("Label", nameof(ModelRow.Label)))
            .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
            .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        var model = pipeline.Fit(data);
        ml.Model.Save(model, data.Schema, modelPath);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "tct-mlnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class EchoComposer : IAnswerComposer
    {
        public Task<string> ComposeAsync(UserIntent intent, string userMessage, IReadOnlyList<KnowledgeSnippet> snippets, CancellationToken ct)
            => Task.FromResult(intent.ToString());
    }

    private sealed record ModelRow(string Text, string Label);

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
