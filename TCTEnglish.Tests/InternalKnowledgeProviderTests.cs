using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class InternalKnowledgeProviderTests
{
    [Fact]
    public async Task GenerateReplyAsync_WhenConfidenceBelowThreshold_ForcesOutOfScope()
    {
        var classifier = new FakeClassifier(new IntentClassification(UserIntent.MyVocabulary, 0.4f, "keyword"));
        var composer = new FakeComposer();
        var provider = new InternalKnowledgeProvider(classifier, [], composer);

        var reply = await provider.GenerateReplyAsync(
            7,
            [new AiContextMessage("user", "show my vocabulary")],
            CancellationToken.None);

        Assert.Equal(UserIntent.OutOfScope, composer.LastIntent);
        Assert.Equal("intent:OutOfScope snippets:0", reply.Text);
        Assert.Equal("internal-keyword", reply.Model);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenMlNetConfidenceBelowThreshold_ForcesOutOfScope()
    {
        var classifier = new FakeClassifier(new IntentClassification(UserIntent.MyVocabulary, 0.3f, "mlnet"));
        var composer = new FakeComposer();
        var provider = new InternalKnowledgeProvider(classifier, [], composer);

        var reply = await provider.GenerateReplyAsync(
            7,
            [new AiContextMessage("user", "toi co bo tu nao")],
            CancellationToken.None);

        Assert.Equal(UserIntent.OutOfScope, composer.LastIntent);
        Assert.Equal("intent:OutOfScope snippets:0", reply.Text);
        Assert.Equal("internal-mlnet", reply.Model);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenRetrieverMatches_UsesUserIdAndSnippets()
    {
        var classifier = new FakeClassifier(new IntentClassification(UserIntent.MyVocabulary, 0.99f, "keyword"));
        var retriever = new FakeRetriever(UserIntent.MyVocabulary, [new KnowledgeSnippet("Set A", "10 cards", "db")]);
        var composer = new FakeComposer();
        var provider = new InternalKnowledgeProvider(classifier, [retriever], composer);

        var reply = await provider.GenerateReplyAsync(
            42,
            [new AiContextMessage("user", "bo tu vung cua toi")],
            CancellationToken.None);

        Assert.Equal(42, retriever.CapturedUserId);
        Assert.Equal("bo tu vung cua toi", retriever.CapturedMessage);
        Assert.Equal(1, composer.LastSnippetCount);
        Assert.Equal("intent:MyVocabulary snippets:1", reply.Text);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenNoRetrieverAvailable_ComposesWithEmptySnippets()
    {
        var classifier = new FakeClassifier(new IntentClassification(UserIntent.ClassInfo, 1.0f, "keyword"));
        var composer = new FakeComposer();
        var provider = new InternalKnowledgeProvider(classifier, [], composer);

        var reply = await provider.GenerateReplyAsync(
            3,
            [new AiContextMessage("user", "lop hoc cua toi")],
            CancellationToken.None);

        Assert.Equal(UserIntent.ClassInfo, composer.LastIntent);
        Assert.Equal(0, composer.LastSnippetCount);
        Assert.Equal("intent:ClassInfo snippets:0", reply.Text);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenNoUserMessage_ReturnsOutOfScopeFallback()
    {
        var classifier = new FakeClassifier(new IntentClassification(UserIntent.Greeting, 1.0f, "keyword"));
        var composer = new FakeComposer();
        var provider = new InternalKnowledgeProvider(classifier, [], composer);

        var reply = await provider.GenerateReplyAsync(
            1,
            [new AiContextMessage("assistant", "hello")],
            CancellationToken.None);

        Assert.Equal(UserIntent.OutOfScope, composer.LastIntent);
        Assert.Equal("intent:OutOfScope snippets:0", reply.Text);
        Assert.Equal("internal-keyword", reply.Model);
    }

    [Fact]
    public async Task GenerateReplyAsync_AlwaysReturnsZeroTokens()
    {
        var classifier = new FakeClassifier(new IntentClassification(UserIntent.Greeting, 1.0f, "mlnet"));
        var composer = new FakeComposer();
        var provider = new InternalKnowledgeProvider(classifier, [], composer);

        var reply = await provider.GenerateReplyAsync(
            9,
            [new AiContextMessage("user", "hi")],
            CancellationToken.None);

        Assert.Equal(0, reply.PromptTokens);
        Assert.Equal(0, reply.CompletionTokens);
        Assert.Equal(0, reply.TotalTokens);
        Assert.Equal("internal-mlnet", reply.Model);
    }

    private sealed class FakeClassifier(IntentClassification result) : IAiQueryClassifier
    {
        public IntentClassification Classify(string userMessage) => result;
    }

    private sealed class FakeRetriever(UserIntent intent, IReadOnlyList<KnowledgeSnippet> snippets) : IKnowledgeRetriever
    {
        public int CapturedUserId { get; private set; }

        public string CapturedMessage { get; private set; } = string.Empty;

        public bool CanHandle(UserIntent inputIntent) => inputIntent == intent;

        public Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
        {
            CapturedUserId = userId;
            CapturedMessage = userMessage;
            return Task.FromResult(snippets);
        }
    }

    private sealed class FakeComposer : IAnswerComposer
    {
        public UserIntent LastIntent { get; private set; }

        public int LastSnippetCount { get; private set; }

        public Task<string> ComposeAsync(UserIntent intent, string userMessage, IReadOnlyList<KnowledgeSnippet> snippets, CancellationToken ct)
        {
            LastIntent = intent;
            LastSnippetCount = snippets.Count;
            return Task.FromResult($"intent:{intent} snippets:{snippets.Count}");
        }
    }
}
