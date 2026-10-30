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

    [Theory]
    [InlineData("The temperature is -5 degrees.", "The temperature is 5 degrees.")]
    [InlineData("The temperature is −5 degrees.", "The temperature is 5 degrees.")]
    [InlineData("I paid $50.", "I paid €50.")]
    [InlineData("I paid $50.", "I paid £50.")]
    [InlineData("The discount is 5%.", "The discount is 5.")]
    [InlineData("Add 2.5 grams.", "Add 25 grams.")]
    [InlineData("I like tea.", "I do not like tea.")]
    public void MeaningBearingNumberOrSignChanges_AreNotAccepted(string reference, string answer)
    {
        var decision = WritingAssessmentPolicy.Assess(reference, answer);

        Assert.NotEqual(WritingAssessmentStatus.Accepted, decision.Status);
        Assert.False(decision.Passed);
        Assert.False(decision.CanAutoAdvance);
    }

    [Theory]
    [InlineData("The temperature is -5 degrees.", "The temperature is -5 degrees")]
    [InlineData("I paid $50.", "i paid $50")]
    [InlineData("The discount is 5%.", "The discount is 5%.")]
    public void MeaningPreservingFormatting_StillAccepted(string reference, string answer)
    {
        var decision = WritingAssessmentPolicy.Assess(reference, answer);

        Assert.Equal(WritingAssessmentStatus.Accepted, decision.Status);
        Assert.True(decision.Passed);
        Assert.True(decision.CanAutoAdvance);
    }
}
