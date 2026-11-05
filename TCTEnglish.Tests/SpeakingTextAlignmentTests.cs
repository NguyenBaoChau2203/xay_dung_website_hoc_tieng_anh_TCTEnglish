using System.Linq;
using TCTEnglish.Services.Speaking;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SpeakingTextAlignmentTests
{
    [Fact]
    public void ExactMatch_AllMatched()
    {
        var result = SpeakingTextAlignment.Compare("Hello world", "Hello world");

        Assert.True(result.ComparisonAvailable);
        Assert.Equal(2, result.Operations.Count);
        Assert.All(result.Operations, op => Assert.Equal(SpeakingAlignmentKind.Matched, op.Kind));
    }

    [Fact]
    public void CaseAndPunctuation_NormalizeLikeTextMatch()
    {
        var result = SpeakingTextAlignment.Compare("Hello, World!", "hello world");

        Assert.True(result.ComparisonAvailable);
        Assert.Equal(2, result.Operations.Count(op => op.Kind == SpeakingAlignmentKind.Matched));
    }

    [Fact]
    public void MissingExtraAndSubstitution_AreClassified()
    {
        var result = SpeakingTextAlignment.Compare(
            "one two three",
            "one three four");

        Assert.True(result.ComparisonAvailable);
        Assert.Contains(result.Operations, op =>
            op.Kind == SpeakingAlignmentKind.Matched
            && op.ReferenceToken == "one"
            && op.HypothesisToken == "one");
        Assert.Contains(result.Operations, op =>
            op.Kind == SpeakingAlignmentKind.Missing
            && op.ReferenceToken == "two");
        Assert.Contains(result.Operations, op =>
            op.Kind == SpeakingAlignmentKind.Matched
            && op.ReferenceToken == "three"
            && op.HypothesisToken == "three");
        Assert.Contains(result.Operations, op =>
            op.Kind == SpeakingAlignmentKind.Extra
            && op.HypothesisToken == "four");

        var substituted = SpeakingTextAlignment.Compare("I like tea", "I love tea");
        Assert.Contains(substituted.Operations, op =>
            op.Kind == SpeakingAlignmentKind.Substituted
            && op.ReferenceToken == "like"
            && op.HypothesisToken == "love");
    }

    [Fact]
    public void RepeatedTokens_HaveDeterministicAlignment()
    {
        var first = SpeakingTextAlignment.Compare("a a b", "a b a");
        var second = SpeakingTextAlignment.Compare("a a b", "a b a");

        Assert.Equal(
            first.Operations.Select(Describe),
            second.Operations.Select(Describe));
    }

    [Fact]
    public void EmptyHypothesis_AllMissing()
    {
        var result = SpeakingTextAlignment.Compare("one two", "");

        Assert.True(result.ComparisonAvailable);
        Assert.Equal(2, result.Operations.Count);
        Assert.All(result.Operations, op => Assert.Equal(SpeakingAlignmentKind.Missing, op.Kind));
    }

    [Fact]
    public void EmptyReferenceAndHypothesis_AvailableEmpty()
    {
        var result = SpeakingTextAlignment.Compare("   ", "");

        Assert.True(result.ComparisonAvailable);
        Assert.Empty(result.Operations);
    }

    [Fact]
    public void OverTokenLimit_IsUnavailableWithoutPartialPretend()
    {
        var tokens = string.Join(' ', Enumerable.Range(1, SpeakingTextAlignment.MaxTokensPerSide + 1).Select(i => $"w{i}"));
        var result = SpeakingTextAlignment.Compare(tokens, "hello");

        Assert.False(result.ComparisonAvailable);
        Assert.Equal("token_limit", result.UnavailableReason);
        Assert.Empty(result.Operations);
    }

    [Fact]
    public void UnicodeAndNumbers_StayStable()
    {
        var result = SpeakingTextAlignment.Compare("café 100", "cafe 100");

        Assert.True(result.ComparisonAvailable);
        Assert.Contains(result.Operations, op => op.Kind == SpeakingAlignmentKind.Matched && op.ReferenceToken == "100");
    }

    private static string Describe(SpeakingAlignmentOperation op)
        => $"{op.Kind}:{op.ReferenceToken}:{op.HypothesisToken}";
}
