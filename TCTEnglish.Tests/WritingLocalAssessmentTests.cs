using TCTEnglish.Services.Writing;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingLocalAssessmentTests
{
    [Theory]
    [InlineData("I like tea.", "i like tea")]
    [InlineData("I like tea.", "I like tea")]
    [InlineData("I like tea.", "I like tea!")]
    public void ExactNormalizedAnswers_AreAccepted(string reference, string answer)
    {
        var decision = WritingAssessmentPolicy.Assess(reference, answer);

        Assert.Equal(WritingAssessmentStatus.Accepted, decision.Status);
        Assert.True(decision.Passed);
        Assert.True(decision.CanAutoAdvance);
        Assert.False(decision.ReviewRequired);
        Assert.Equal("local-exact", decision.Source);
    }

    [Theory]
    [InlineData("I like tea.", "I do not like tea.")]
    [InlineData("I like tea.", "Tea likes me.")]
    public void MeaningChangingAnswers_AreNotAccepted(string reference, string answer)
    {
        var decision = WritingAssessmentPolicy.Assess(reference, answer);

        Assert.NotEqual(WritingAssessmentStatus.Accepted, decision.Status);
        Assert.False(decision.Passed);
        Assert.False(decision.CanAutoAdvance);
    }

    [Fact]
    public void ParaphraseWithoutApprovedVariant_NeedsReview()
    {
        var decision = WritingAssessmentPolicy.Assess("I like tea.", "I enjoy tea.");

        Assert.Equal(WritingAssessmentStatus.NeedsReview, decision.Status);
        Assert.True(decision.ReviewRequired);
        Assert.False(decision.Passed);
        Assert.Equal("local-review", decision.Source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void EmptyOrSymbolOnly_NeedsRevision(string answer)
    {
        var decision = WritingAssessmentPolicy.Assess("I like tea.", answer);

        Assert.Equal(WritingAssessmentStatus.NeedsRevision, decision.Status);
        Assert.False(decision.Passed);
        Assert.False(decision.ReviewRequired);
    }

    [Fact]
    public void KeywordOverlap_DoesNotAutoAccept()
    {
        // Old heuristic could pass on ~40% content coverage — must not Accepted now.
        var decision = WritingAssessmentPolicy.Assess(
            "I really enjoy drinking hot tea every morning.",
            "I tea morning");

        Assert.Equal(WritingAssessmentStatus.NeedsReview, decision.Status);
        Assert.False(decision.Passed);
    }
}
