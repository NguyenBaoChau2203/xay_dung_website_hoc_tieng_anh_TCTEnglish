using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SpeakingComponentScorerTests
{
    [Theory]
    [InlineData("we like green apples", "we like green apples", 100)]
    [InlineData("we like green apples", "we green apples", 75)]
    [InlineData("we like green apples", "we love green apples", 100)]
    [InlineData("we like green apples", "we really like green apples today", 100)]
    [InlineData("we like green apples", "", 0)]
    [InlineData("hello hello world", "hello world", 66.67)]
    public void CompletenessDistinguishesOmittedPositionsFromWrongAndExtraWords(string reference, string spoken, double expected)
        => Assert.Equal(expected, SpeakingComponentScorer.Completeness(SpeakingTextAlignment.Compare(reference, spoken)));

    [Fact]
    public void CompletenessDoesNotFabricateNumbersWithoutAUsableReference()
    {
        Assert.Null(SpeakingComponentScorer.Completeness(SpeakingTextAlignment.Compare("", "hello")));
        Assert.Null(SpeakingComponentScorer.Completeness(SpeakingTextAlignment.Compare(string.Join(' ', Enumerable.Repeat("word", 257)), "hello")));
    }

    [Fact]
    public void InternalPausesReduceFluencyWhileLeadingAndTrailingSilenceDoNot()
    {
        const string text = "we like green apples";
        var smooth = SpeakingComponentScorer.EstimateFluency(Wav(2, (0, 2)), text);
        var padded = SpeakingComponentScorer.EstimateFluency(Wav(6, (2, 4)), text);
        var hesitant = SpeakingComponentScorer.EstimateFluency(Wav(6, (0, 1), (5, 6)), text);
        Assert.NotNull(smooth); Assert.NotNull(padded); Assert.NotNull(hesitant);
        Assert.InRange(smooth.Value, 95, 100);
        Assert.InRange(Math.Abs(smooth.Value - padded.Value), 0, 1);
        Assert.True(hesitant < smooth - 30);
    }

    [Fact]
    public void ExtremeRatesLosePointsAndInsufficientSignalRemainsUnavailable()
    {
        const string text = "we like green apples";
        var normal = SpeakingComponentScorer.EstimateFluency(Wav(2, (0, 2)), text);
        Assert.True(SpeakingComponentScorer.EstimateFluency(Wav(10, (0, 10)), text) < normal);
        Assert.True(SpeakingComponentScorer.EstimateFluency(Wav(0.5, (0, 0.5)), text) < normal);
        Assert.Null(SpeakingComponentScorer.EstimateFluency(Wav(3), text));
        Assert.Null(SpeakingComponentScorer.EstimateFluency(Wav(0.1, (0, 0.1)), text));
        Assert.Null(SpeakingComponentScorer.EstimateFluency(Wav(2, (0, 2)), "hello"));
        Assert.Null(SpeakingComponentScorer.EstimateFluency([1, 2, 3], text));
    }

    [Fact]
    public void FluencyReadsActualRiffDataChunkWithAncillaryMetadata()
    {
        var original = Wav(2, (0, 2));
        var extended = new byte[original.Length + 12];
        original.AsSpan(0, 36).CopyTo(extended);
        "JUNK"u8.CopyTo(extended.AsSpan(36));
        BitConverter.GetBytes(4).CopyTo(extended, 40);
        original.AsSpan(36).CopyTo(extended.AsSpan(48));
        BitConverter.GetBytes(extended.Length - 8).CopyTo(extended, 4);
        Assert.Equal(SpeakingComponentScorer.EstimateFluency(original, "we like green apples"),
            SpeakingComponentScorer.EstimateFluency(extended, "we like green apples"));
    }

    private static byte[] Wav(double seconds, params (double Start, double End)[] signal)
    {
        var wav = SpeakingAssessmentTestHelper.CreateSilentPcm16Mono16kWav(seconds);
        foreach (var (start, end) in signal)
        for (var sample = (int)(start * 16000); sample < (int)(end * 16000); sample++)
            BitConverter.GetBytes((short)(5000 * Math.Sin(sample * 2 * Math.PI * 180 / 16000)))
                .CopyTo(wav, 44 + sample * 2);
        return wav;
    }
}
