using TCTEnglish.Infrastructure.SharedData;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SharedDataAdoptionIndexTests
{
    [Fact]
    public void ManyNewChildrenProjectOriginalCandidatesOncePerReferenceContext()
    {
        var rows = Enumerable.Range(0, 100).Select(i => (object)new Candidate(i.ToString())).ToArray();
        var index = new SharedDataAdoptionIndex(rows);
        var calls = 0;
        string Project(object row) { calls++; return ((Candidate)row).Text; }
        for (var i = 0; i < 500; i++) Assert.Empty(index.Find("parent-1/legacy", "new-cue-" + i, Project));
        Assert.Equal(100, calls);
        Assert.Single(index.Find("parent-1/legacy", "12", Project));
        Assert.Equal(100, calls);
        Assert.Empty(index.Find("parent-2/legacy", "new", Project));
        Assert.Equal(200, calls);
    }

    [Fact]
    public void AmbiguousMatchesStayAmbiguous_AndClaimedRowsDisappearAcrossContexts()
    {
        var one = new Candidate("same"); var two = new Candidate("same");
        var index = new SharedDataAdoptionIndex([one, two]);
        Assert.Equal(2, index.Find("one", "same", row => ((Candidate)row).Text).Count);
        index.Claim(one);
        Assert.Same(two, Assert.Single(index.Find("one", "same", row => ((Candidate)row).Text)));
        index.Claim(two);
        Assert.Empty(index.Find("two", "same", row => ((Candidate)row).Text));
    }

    private sealed record Candidate(string Text);
}
