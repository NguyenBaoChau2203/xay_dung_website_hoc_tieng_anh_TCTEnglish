using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SpeakingAssessmentAdmissionTests
{
    [Fact]
    public void TenthRequest_IsAdmitted_EleventhIsBusy_ThenRecovers()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-03T10:00:00Z"));
        var admission = new SpeakingAssessmentAdmission(
            Options.Create(new SpeechProcessingOptions { MaxAssessmentsPerUserPerMinute = 10 }),
            clock);

        for (var i = 0; i < 10; i++)
        {
            Assert.True(admission.TryAdmit(7, out _));
        }

        Assert.False(admission.TryAdmit(7, out var retry));
        Assert.NotNull(retry);
        Assert.InRange(retry!.Value, 1, 60);

        Assert.True(admission.TryAdmit(8, out _), "Cross-user must not share the same bucket.");

        clock.Advance(TimeSpan.FromMinutes(1.1));
        Assert.True(admission.TryAdmit(7, out _));
    }

    [Fact]
    public void ExpiredUsers_AreEvicted_AndBucketCountIsBounded()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-03T10:00:00Z"));
        var admission = new SpeakingAssessmentAdmission(
            Options.Create(new SpeechProcessingOptions { MaxAssessmentsPerUserPerMinute = 10 }),
            clock);

        for (var userId = 1; userId <= 10_000; userId++)
        {
            Assert.True(admission.TryAdmit(userId, out _));
        }

        Assert.Equal(10_000, admission.TrackedUserCount);
        clock.Advance(TimeSpan.FromHours(2));
        Assert.True(admission.TryAdmit(10_001, out _));
        Assert.Equal(1, admission.TrackedUserCount);
    }

    [Fact]
    public void Eviction_DoesNotBypassActiveUserLimit()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-03T10:00:00Z"));
        var admission = new SpeakingAssessmentAdmission(
            Options.Create(new SpeechProcessingOptions { MaxAssessmentsPerUserPerMinute = 10 }),
            clock);

        for (var i = 0; i < 10; i++)
        {
            Assert.True(admission.TryAdmit(42, out _));
        }

        Assert.False(admission.TryAdmit(42, out _));
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(admission.TryAdmit(42, out _), "Active bucket must still enforce 10/min.");
    }

    [Fact]
    public void CapacityExhaustion_RefusesNewUser_WithoutEvictingActiveBuckets()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-03T10:00:00Z"));
        var admission = new SpeakingAssessmentAdmission(
            Options.Create(new SpeechProcessingOptions { MaxAssessmentsPerUserPerMinute = 10 }),
            clock);

        for (var userId = 1; userId <= SpeakingAssessmentAdmission.MaxTrackedUsers; userId++)
        {
            Assert.True(admission.TryAdmit(userId, out _), $"user {userId}");
        }

        Assert.Equal(SpeakingAssessmentAdmission.MaxTrackedUsers, admission.TrackedUserCount);
        Assert.False(admission.TryAdmit(SpeakingAssessmentAdmission.MaxTrackedUsers + 1, out var retry));
        Assert.Equal(5, retry);
        Assert.True(admission.TryAdmit(1, out _), "Existing active buckets must remain usable.");
        Assert.Equal(SpeakingAssessmentAdmission.MaxTrackedUsers, admission.TrackedUserCount);
    }

    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _utc = start;

        public override DateTimeOffset GetUtcNow() => _utc;

        public void Advance(TimeSpan delta) => _utc += delta;
    }
}
