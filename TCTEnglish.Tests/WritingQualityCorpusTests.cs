using TCTEnglish.Services.Writing;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// G02 Writing quality corpus (&gt;=40 labeled cases). Optional engines stay default-off.
/// </summary>
public sealed class WritingQualityCorpusTests
{
    public static TheoryData<string, string, string> Corpus()
    {
        var data = new TheoryData<string, string, string>();

        // 10 exact / formatting
        data.Add("I like tea.", "I like tea.", "Accepted");
        data.Add("I like tea.", "i like tea", "Accepted");
        data.Add("I like tea.", "I like tea!", "Accepted");
        data.Add("I like tea.", "  I   like  tea.  ", "Accepted");
        data.Add("Good morning.", "Good morning", "Accepted");
        data.Add("She works here.", "she works here.", "Accepted");
        data.Add("We can meet tomorrow.", "We can meet tomorrow", "Accepted");
        data.Add("Please send the report.", "please send the report!", "Accepted");
        data.Add("The meeting starts at noon.", "The meeting starts at noon", "Accepted");
        data.Add("Thank you for your help.", "Thank you for your help", "Accepted");

        // 10 meaning / number / sign / unit / negation changes
        data.Add("The temperature is -5 degrees.", "The temperature is 5 degrees.", "NeedsReview");
        data.Add("I paid $50.", "I paid €50.", "NeedsReview");
        data.Add("The discount is 5%.", "The discount is 5.", "NeedsReview");
        data.Add("Add 2.5 grams.", "Add 25 grams.", "NeedsReview");
        data.Add("I like tea.", "I do not like tea.", "NeedsReview");
        data.Add("There are 10 seats.", "There are 100 seats.", "NeedsReview");
        data.Add("He arrived at 3 pm.", "He arrived at 8 pm.", "NeedsReview");
        data.Add("The price is £20.", "The price is $20.", "NeedsReview");
        data.Add("Turn left.", "Turn right.", "NeedsReview");
        data.Add("I am available.", "I am not available.", "NeedsReview");

        // 10 paraphrase NeedsReview
        data.Add("I like tea.", "I enjoy tea.", "NeedsReview");
        data.Add("Please call me back.", "Could you return my call?", "NeedsReview");
        data.Add("The store opens at nine.", "The shop opens at 9.", "NeedsReview");
        data.Add("She finished the task.", "She completed the assignment.", "NeedsReview");
        data.Add("We need more time.", "We require additional time.", "NeedsReview");
        data.Add("I will arrive soon.", "I'll be there shortly.", "NeedsReview");
        data.Add("This idea is useful.", "This idea is helpful.", "NeedsReview");
        data.Add("Keep the door closed.", "Leave the door shut.", "NeedsReview");
        data.Add("He bought a new phone.", "He purchased a new mobile.", "NeedsReview");
        data.Add("The food tastes good.", "The meal is delicious.", "NeedsReview");

        // 5 empty / off-topic / multi-sentence
        data.Add("I like tea.", "", "NeedsRevision");
        data.Add("I like tea.", "   ", "NeedsRevision");
        data.Add("I like tea.", "!!!", "NeedsRevision");
        data.Add("I like tea.", "Completely unrelated answer about football scores.", "NeedsReview");
        data.Add("I like tea.", "I like tea. I also like coffee every morning.", "NeedsReview");

        // 5 Unicode / decimal / boundary
        data.Add("The temperature is −5 degrees.", "The temperature is −5 degrees.", "Accepted");
        data.Add("The temperature is −5 degrees.", "The temperature is -5 degrees.", "Accepted");
        data.Add("Pay ¥100.", "Pay ¥100", "Accepted");
        data.Add("Mix 1,5 liters.", "Mix 1.5 liters.", "Accepted");
        data.Add("Score +5 points.", "Score +5 points", "Accepted");

        return data;
    }

    [Fact]
    public void Corpus_HasAtLeastFortyLabeledCases()
    {
        Assert.True(Corpus().Count() >= 40, $"Corpus size was {Corpus().Count()}.");
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void CorpusCase_MatchesExpectedLabel(string reference, string answer, string expected)
    {
        var decision = WritingAssessmentPolicy.Assess(reference, answer);
        Assert.Equal(expected, decision.Status.ToString());
    }
}
