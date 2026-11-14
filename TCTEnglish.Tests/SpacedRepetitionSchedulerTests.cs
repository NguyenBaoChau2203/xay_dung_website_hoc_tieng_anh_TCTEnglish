using TCTVocabulary.Services.Vocabulary;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SpacedRepetitionSchedulerTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc);

    private static SrsState Due(int level) => new(level, Now.AddHours(-1), true);

    // ── 4.1 levels ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(2, 0, 3)]
    [InlineData(3, 0, 7)]
    [InlineData(4, 0, 14)]
    [InlineData(5, 0, 30)]
    [InlineData(6, 0, 60)]
    public void IntervalFor_ReturnsTheLadder(int level, int minutes, int days)
    {
        var expected = minutes > 0 ? TimeSpan.FromMinutes(minutes) : TimeSpan.FromDays(days);

        Assert.Equal(expected, SpacedRepetitionScheduler.IntervalFor(level));
    }

    [Theory]
    [InlineData(-5, 10)]
    [InlineData(99, 60 * 24 * 60)]
    public void IntervalFor_ClampsOutOfRangeLevels(int level, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), SpacedRepetitionScheduler.IntervalFor(level));
    }

    [Theory]
    [InlineData(0, "Learning", "new")]
    [InlineData(1, "Reviewing", "new")]
    [InlineData(2, "Reviewing", "growing")]
    [InlineData(3, "Reviewing", "growing")]
    [InlineData(4, "Mastered", "strong")]
    [InlineData(5, "Mastered", "strong")]
    [InlineData(6, "Mastered", "long")]
    public void StatusAndStage_FollowTheTable(int level, string status, string stage)
    {
        Assert.Equal(status, SpacedRepetitionScheduler.StatusFor(level));
        Assert.Equal(stage, SpacedRepetitionScheduler.StageFor(level));
    }

    [Theory]
    [InlineData(0, "Mới học")]
    [InlineData(1, "Mới học")]
    [InlineData(2, "Đang nhớ")]
    [InlineData(3, "Đang nhớ")]
    [InlineData(4, "Nhớ tốt")]
    [InlineData(5, "Nhớ tốt")]
    [InlineData(6, "Thuộc lâu")]
    public void StageLabel_UsesTheLearnerWording(int level, string label)
    {
        Assert.Equal(label, SpacedRepetitionScheduler.StageLabelFor(level));
    }

    [Theory]
    [InlineData(0, "Ôn lại sau 10 phút")]
    [InlineData(1, "Ôn lại sau 1 ngày")]
    [InlineData(2, "Ôn lại sau 3 ngày")]
    [InlineData(3, "Ôn lại sau 7 ngày")]
    [InlineData(4, "Ôn lại sau 14 ngày")]
    [InlineData(5, "Ôn lại sau 30 ngày")]
    [InlineData(6, "Ôn lại sau 60 ngày")]
    public void NextReviewLabel_NamesTheInterval(int level, string label)
    {
        Assert.Equal(label, SpacedRepetitionScheduler.NextReviewLabelFor(level));
    }

    // ── 4.2 legacy rows ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, "Mastered", 4)]
    [InlineData(0, "Mastered", 4)]
    [InlineData(3, "Mastered", 4)]
    [InlineData(5, "Mastered", 5)]
    [InlineData(3, "Learning", 0)]
    [InlineData(0, "Learning", 0)]
    [InlineData(2, "Reviewing", 2)]
    [InlineData(99, "Reviewing", 6)]
    [InlineData(99, "Mastered", 6)]
    [InlineData(-4, "Reviewing", 0)]
    [InlineData(-4, "Mastered", 4)]
    [InlineData(3, null, 3)]
    public void LevelFrom_ReadsLegacyRows(int repetitionCount, string? status, int expected)
    {
        Assert.Equal(expected, SpacedRepetitionScheduler.LevelFrom(repetitionCount, status));
    }

    // ── 4.3 outcomes, every level ───────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Again_ResetsToLevelZeroTenMinutesAheadAndCountsWrong(int level)
    {
        var result = SpacedRepetitionScheduler.Apply(Due(level), ReviewOutcome.Again, Now);

        Assert.Equal(0, result.Level);
        Assert.Equal("Learning", result.Status);
        Assert.Equal(Now.AddMinutes(10), result.NextReviewUtc);
        Assert.True(result.CountsWrong);
        Assert.False(result.Promoted);
        Assert.Equal(level > 0, result.Demoted);
        Assert.False(result.EarlyReview);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(5, 5)]
    [InlineData(6, 6)]
    public void Hard_KeepsTheLevelWithAFloorOfOne(int level, int expected)
    {
        var result = SpacedRepetitionScheduler.Apply(Due(level), ReviewOutcome.Hard, Now);

        Assert.Equal(expected, result.Level);
        Assert.Equal(Now + SpacedRepetitionScheduler.IntervalFor(expected), result.NextReviewUtc);
        Assert.Equal(SpacedRepetitionScheduler.StatusFor(expected), result.Status);
        Assert.False(result.CountsWrong);
        Assert.False(result.Demoted);
        Assert.Equal(expected > level, result.Promoted);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 5)]
    [InlineData(5, 6)]
    [InlineData(6, 6)]
    public void Good_ClimbsOneLevelAndCapsAtSix(int level, int expected)
    {
        var result = SpacedRepetitionScheduler.Apply(Due(level), ReviewOutcome.Good, Now);

        Assert.Equal(expected, result.Level);
        Assert.Equal(Now + SpacedRepetitionScheduler.IntervalFor(expected), result.NextReviewUtc);
        Assert.Equal(SpacedRepetitionScheduler.StatusFor(expected), result.Status);
        Assert.Equal(level < 6, result.Promoted);
        Assert.False(result.Demoted);
        Assert.False(result.CountsWrong);
        Assert.False(result.EarlyReview);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 4)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]
    [InlineData(5, 5)]
    [InlineData(6, 6)]
    public void Known_LiftsToAtLeastLevelFour(int level, int expected)
    {
        var result = SpacedRepetitionScheduler.Apply(Due(level), ReviewOutcome.Known, Now);

        Assert.Equal(expected, result.Level);
        Assert.Equal(Now + SpacedRepetitionScheduler.IntervalFor(expected), result.NextReviewUtc);
        Assert.Equal("Mastered", result.Status);
        Assert.Equal(expected > level, result.Promoted);
        Assert.False(result.Demoted);
        Assert.False(result.CountsWrong);
    }

    // ── a word with no row ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(ReviewOutcome.Again, 0)]
    [InlineData(ReviewOutcome.Hard, 1)]
    [InlineData(ReviewOutcome.Good, 1)]
    [InlineData(ReviewOutcome.Known, 4)]
    public void NewWord_StartsFromLevelZero(ReviewOutcome outcome, int expected)
    {
        var result = SpacedRepetitionScheduler.Apply(new SrsState(0, null, false), outcome, Now);

        Assert.Equal(expected, result.Level);
        Assert.Equal(Now + SpacedRepetitionScheduler.IntervalFor(expected), result.NextReviewUtc);
        Assert.False(result.EarlyReview);
        Assert.False(result.Demoted);
        Assert.Equal(outcome == ReviewOutcome.Again, result.CountsWrong);
    }

    [Fact]
    public void NewWord_IgnoresAStaleLevelOnTheState()
    {
        var result = SpacedRepetitionScheduler.Apply(new SrsState(5, null, false), ReviewOutcome.Good, Now);

        Assert.Equal(1, result.Level);
    }

    // ── P7 early review ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ReviewOutcome.Hard)]
    [InlineData(ReviewOutcome.Good)]
    [InlineData(ReviewOutcome.Known)]
    public void EarlyReview_KeepsLevelStatusAndDueDate(ReviewOutcome outcome)
    {
        var next = Now.AddDays(3);
        var result = SpacedRepetitionScheduler.Apply(new SrsState(2, next, true), outcome, Now);

        Assert.True(result.EarlyReview);
        Assert.Equal(2, result.Level);
        Assert.Equal("Reviewing", result.Status);
        Assert.Equal(next, result.NextReviewUtc);
        Assert.False(result.Promoted);
        Assert.False(result.Demoted);
        Assert.False(result.CountsWrong);
    }

    [Fact]
    public void EarlyReview_AgainStillApplies()
    {
        var result = SpacedRepetitionScheduler.Apply(new SrsState(3, Now.AddDays(5), true), ReviewOutcome.Again, Now);

        Assert.False(result.EarlyReview);
        Assert.Equal(0, result.Level);
        Assert.Equal(Now.AddMinutes(10), result.NextReviewUtc);
        Assert.True(result.CountsWrong);
        Assert.True(result.Demoted);
    }

    [Fact]
    public void DueExactlyNow_CountsAsDueNotEarly()
    {
        var result = SpacedRepetitionScheduler.Apply(new SrsState(2, Now, true), ReviewOutcome.Good, Now);

        Assert.False(result.EarlyReview);
        Assert.Equal(3, result.Level);
        Assert.True(result.Promoted);
    }

    [Fact]
    public void ARowWithoutADueDate_CountsAsDue()
    {
        var result = SpacedRepetitionScheduler.Apply(new SrsState(1, null, true), ReviewOutcome.Good, Now);

        Assert.False(result.EarlyReview);
        Assert.Equal(2, result.Level);
    }

    [Fact]
    public void OneSecondBeforeDue_IsEarly()
    {
        var result = SpacedRepetitionScheduler.Apply(new SrsState(2, Now.AddSeconds(1), true), ReviewOutcome.Good, Now);

        Assert.True(result.EarlyReview);
    }

    [Fact]
    public void Apply_ClampsAStateLevelOutsideTheLadder()
    {
        Assert.Equal(6, SpacedRepetitionScheduler.Apply(Due(40), ReviewOutcome.Good, Now).Level);
        Assert.Equal(1, SpacedRepetitionScheduler.Apply(Due(-3), ReviewOutcome.Good, Now).Level);
    }

    // ── TryParseOutcome ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("again", ReviewOutcome.Again)]
    [InlineData("hard", ReviewOutcome.Hard)]
    [InlineData("good", ReviewOutcome.Good)]
    [InlineData("known", ReviewOutcome.Known)]
    [InlineData("GOOD", ReviewOutcome.Good)]
    [InlineData("Known", ReviewOutcome.Known)]
    public void TryParseOutcome_AcceptsTheFourNamesIgnoringCase(string raw, ReviewOutcome expected)
    {
        Assert.True(SpacedRepetitionScheduler.TryParseOutcome(raw, out var outcome));
        Assert.Equal(expected, outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("easy")]
    [InlineData("perfect")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData(" good")]
    [InlineData("good ")]
    [InlineData("good,hard")]
    [InlineData("goodd")]
    public void TryParseOutcome_RejectsEverythingElse(string? raw)
    {
        Assert.False(SpacedRepetitionScheduler.TryParseOutcome(raw, out _));
    }
}
