using TCTEnglish.Services.Speaking;
using Xunit;

namespace TCTEnglish.Tests;

public class SpeakingImportReliabilityTests
{
    [Fact]
    public void SubtitleParser_RejectsInvalidTimestamps()
    {
        var content = """
            1
            00:00:05,000 --> 00:00:01,000
            Bad cue
            """;

        var (ok, error, sentences) = SpeakingSubtitleParser.Parse(content);
        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Empty(sentences);
    }

    [Fact]
    public void SubtitleParser_AcceptsOrderedWebVtt()
    {
        var content = """
            WEBVTT

            00:00:00.000 --> 00:00:02.000
            Hello world

            00:00:02.500 --> 00:00:04.000
            Second line
            """;

        var (ok, error, sentences) = SpeakingSubtitleParser.Parse(content);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(2, sentences.Count);
        Assert.Equal("Hello world", sentences[0].Text);
        Assert.True(sentences[0].StartTime < sentences[0].EndTime);
    }

    [Fact]
    public void SubtitleParser_AcceptsMinuteSecondCues_AndRejectsOverlapAndBadClock()
    {
        var bom = "\uFEFFWEBVTT\n\nintro\n00:01.000 --> 00:03.500 align:start\nHello\nthere\n\n00:03.500 --> 00:04.000\nNext";
        var (ok, error, sentences) = SpeakingSubtitleParser.Parse(bom);
        Assert.True(ok, error);
        Assert.Equal(2, sentences.Count);
        Assert.Equal("Hello there", sentences[0].Text);
        Assert.Equal(1, sentences[0].StartTime);

        var overlap = "00:00:00,000 --> 00:00:02,000\nOne\n\n00:00:01,000 --> 00:00:03,000\nTwo";
        var (overlapOk, overlapError, _) = SpeakingSubtitleParser.Parse(overlap);
        Assert.False(overlapOk);
        Assert.Contains("thời gian", overlapError, StringComparison.OrdinalIgnoreCase);

        var badClock = "00:60:00,000 --> 00:61:00,000\nBad";
        Assert.False(SpeakingSubtitleParser.Parse(badClock).Ok);
    }

    [Fact]
    public void SubtitleParser_DoesNotFabricateEmptyTranscript()
    {
        var (ok, _, sentences) = SpeakingSubtitleParser.Parse("WEBVTT\n\n");
        Assert.False(ok);
        Assert.Empty(sentences);
    }
}
