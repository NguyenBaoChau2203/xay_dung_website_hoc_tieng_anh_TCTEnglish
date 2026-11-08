using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TCTEnglish.Services.AI;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiRagPhase7FixRound1Tests
{
    private const string CanonicalFixtureSha256 = "1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f";

    [Fact]
    public async Task DefaultRunClock_RejectsAdmissionAfterItsDuration()
    {
        var guard = new AiAcceptanceRunGuard(2, 100, TimeSpan.FromTicks(1));
        await Task.Yield();
        Assert.False(guard.TryAdmit(10, 100));
        Assert.Equal("duration", guard.Refusal);
        Assert.Equal(0, guard.Posts);
    }

    [Fact]
    public void ExpiredRun_CannotPromoteEvenAfterTheLastReceipt()
    {
        var elapsed = TimeSpan.Zero;
        var guard = new AiAcceptanceRunGuard(2, 100, TimeSpan.FromSeconds(1), () => elapsed);
        Assert.True(guard.TryAdmit(10, 100));
        guard.RecordKnown(10);
        elapsed = TimeSpan.FromSeconds(1);
        Assert.False(guard.TryPromote(1, 1));
        Assert.Equal("duration", guard.Refusal);
        Assert.False(guard.Promoted);
    }

    [Fact]
    public void InvalidReceipt_CannotReleaseThePendingReservationAsZero()
    {
        var guard = new AiAcceptanceRunGuard(2, 10, TimeSpan.FromMinutes(1));
        Assert.True(guard.TryAdmit(10, 10));
        guard.RecordKnown(-1);
        Assert.Equal(10, guard.UnknownTokens);
        Assert.False(guard.TryAdmit(1, 10));
        Assert.False(guard.TryPromote(1, 1));
    }

    [Fact]
    public void LostLargeBatch_KeepsUnknownAndRefusesRetryInsideTheJob()
    {
        var guard = new AiAcceptanceRunGuard(10, 113_887, TimeSpan.FromMinutes(20));
        var sends = 0;
        Assert.True(Send(guard, 36_276, 40_000, ref sends, known: false));
        Assert.False(Send(guard, 36_276, 40_000 - 36_276, ref sends, known: false));
        Assert.Equal("job-remaining", guard.Refusal);
        Assert.Equal(1, sends);
        Assert.Equal(1, guard.Posts);
        Assert.Equal(36_276, guard.UnknownTokens);
        Assert.False(guard.TryPromote(0, 487));

        Assert.True(Send(guard, 1_790, 40_000, ref sends, known: true));
        Assert.Equal(2, sends);
        Assert.Equal(36_276, guard.UnknownTokens);
        Assert.Equal(1_790, guard.KnownTokens);
        Assert.False(guard.TryPromote(2, 487));
    }

    [Fact]
    public void NominalCorpusAdmission_UsesTenPostsAndPromotesOnlyWhenComplete()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(BoundsPath()));
        var bounds = document.RootElement.GetProperty("chunkByteUppers").EnumerateArray().Select(item => item.GetInt32()).ToArray();
        Assert.Equal(487, bounds.Length);
        Assert.Equal(113_887, bounds.Sum());
        var guard = new AiAcceptanceRunGuard(10, 113_887, TimeSpan.FromMinutes(20));
        var batches = Replay(bounds, guard);
        Assert.Equal([36_276, 1_790], batches.Take(2));
        Assert.Equal(37_558, batches[2]);
        Assert.Equal(10, batches.Count);
        Assert.Equal(10, guard.Posts);
        Assert.Equal(113_887, guard.KnownTokens);
        Assert.Equal(0, guard.UnknownTokens);
        Assert.False(guard.TryPromote(486, 487));
        Assert.True(guard.TryPromote(487, 487));
        Assert.False(guard.TryAdmit(1, 40_000));
        Assert.Equal("post-cap", guard.Refusal);
    }

    [Fact]
    public void RunGuard_CountsLaterTurnsAndStopsAtDurationOrTokenAccount()
    {
        var guard = new AiAcceptanceRunGuard(4, 5_000, TimeSpan.FromMinutes(1));
        Assert.True(guard.TryAdmit(100, 40_000));
        guard.RecordKnown(100);
        Assert.True(guard.TryAdmit(200, 40_000));
        guard.RecordKnown(40);
        Assert.True(guard.TryAdmit(4_600, 4_600));
        guard.RecordKnown(100);
        Assert.Equal(3, guard.Posts);
        Assert.Equal(240, guard.KnownTokens);
        Assert.False(guard.TryAdmit(4_900, 4_900));
        Assert.Equal("run-token", guard.Refusal);

        var elapsed = TimeSpan.FromSeconds(5);
        var timed = new AiAcceptanceRunGuard(3, 100, TimeSpan.FromSeconds(5), () => elapsed);
        Assert.False(timed.TryAdmit(10, 100));
        Assert.Equal("duration", timed.Refusal);
        Assert.Equal(0, timed.Posts);
    }

    [Fact]
    public void FrozenEvaluation_KeepsRevision2DenominatorsAndStandardActor()
    {
        var bytes = File.ReadAllBytes(FixturePath());
        Assert.Equal(CanonicalFixtureSha256, HybridRagFixtureValidator.CanonicalSha256(bytes));
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        Assert.Empty(HybridRagFixtureValidator.Validate(root));
        var cases = root.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(120, cases.Count);
        Assert.Equal(80, cases.Count(item => item.GetProperty("split").GetString() == "development"));
        Assert.Equal(40, cases.Count(item => item.GetProperty("split").GetString() == "held_out"));
        Assert.Equal(44, Count(cases, "development", "answered"));
        Assert.Equal(18, Count(cases, "held_out", "answered"));
        Assert.Equal(62, cases.Count(item => item.GetProperty("expectedOutcome").GetString() == "answered"));
        Assert.Equal(20, cases.Count(item => item.GetProperty("expectedOutcome").GetString() == "access_denied"));
        Assert.Equal(15, cases.Count(item => item.GetProperty("expectedOutcome").GetString() == "out_of_scope"));
        Assert.Equal(10, cases.Count(item => item.GetProperty("expectedOutcome").GetString() == "insufficient_evidence"));
        Assert.Equal(7, cases.Count(item => item.GetProperty("expectedOutcome").GetString() == "needs_clarification"));
        Assert.All(cases, item =>
        {
            Assert.Equal("actor-lan-standard", item.GetProperty("actorId").GetString());
            Assert.Equal("2026-11-06", item.GetProperty("context").GetProperty("businessDate").GetString());
            Assert.Equal("synthetic-phase0", item.GetProperty("context").GetProperty("corpus").GetString());
        });
        var actor = root.GetProperty("actors").EnumerateArray().Single(item => item.GetProperty("id").GetString() == "actor-lan-standard");
        Assert.Equal("Standard", actor.GetProperty("role").GetString());
        var sources = root.GetProperty("sources").EnumerateArray().ToList();
        Assert.Equal(51, sources.Count);
        var hashes = sources.Select(item => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.GetProperty("canonicalText").GetString()!))).ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(51, hashes.Count);
        Assert.Equal(3_807, cases.Where(item => item.GetProperty("split").GetString() == "development").Sum(item => Encoding.UTF8.GetByteCount(item.GetProperty("question").GetString()!)));
        Assert.Equal(1_860, cases.Where(item => item.GetProperty("split").GetString() == "held_out").Sum(item => Encoding.UTF8.GetByteCount(item.GetProperty("question").GetString()!)));
        Assert.True(EmbeddingTokenBound.Fits(["fixture-source"], 40_000, 12, out _, out _));
        Assert.False(new AiAcceptanceRunGuard(1, 40_000, TimeSpan.FromMinutes(1)).TryAdmit(36_276, 3_724));
    }

    private static bool Send(AiAcceptanceRunGuard guard, int tokenUpper, int jobRemaining, ref int sends, bool known)
    {
        if (!guard.TryAdmit(tokenUpper, jobRemaining))
        {
            return false;
        }

        sends++;
        if (known)
        {
            guard.RecordKnown(tokenUpper);
        }
        else
        {
            guard.RecordUnknown();
        }

        return true;
    }

    private static List<int> Replay(IReadOnlyList<int> bounds, AiAcceptanceRunGuard guard)
    {
        var batches = new List<int>();
        var index = 0;
        while (index < bounds.Count)
        {
            var jobKnown = 0;
            var jobPosts = 0;
            var progressed = false;
            while (index < bounds.Count && jobPosts < 12)
            {
                var count = 0;
                var used = 0;
                var cursor = index;
                while (cursor < bounds.Count && count < 64 && jobKnown + used + bounds[cursor] <= 40_000)
                {
                    used += bounds[cursor];
                    count++;
                    cursor++;
                }

                if (count == 0)
                {
                    break;
                }

                Assert.True(guard.TryAdmit(used, 40_000 - jobKnown));
                guard.RecordKnown(used);
                batches.Add(used);
                jobKnown += used;
                jobPosts++;
                index = cursor;
                progressed = true;
            }

            if (!progressed)
            {
                break;
            }
        }

        return batches;
    }

    private static int Count(List<JsonElement> cases, string split, string outcome)
        => cases.Count(item => item.GetProperty("split").GetString() == split && item.GetProperty("expectedOutcome").GetString() == outcome);

    private static string FixturePath()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "AI", "hybrid-rag-evaluation.json"));

    private static string BoundsPath()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "AI", "phase7-corpus-admission-bounds.json"));
}
