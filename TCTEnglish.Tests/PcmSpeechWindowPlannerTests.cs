using TCTEnglish.Services.Speaking;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class PcmSpeechWindowPlannerTests
{
    private readonly PcmSpeechWindowPlanner _planner = new();

    [Fact]
    public void PureSilence_ReturnsNoWindows()
    {
        var pcm = new byte[16_000 * 2 * 3];
        Assert.Empty(_planner.Plan(pcm));
    }

    [Fact]
    public void OddByteLength_ReturnsNoWindows()
    {
        Assert.Empty(_planner.Plan(new byte[15]));
    }

    [Fact]
    public void NearSilenceNoise_HasNoCandidateWindows()
    {
        var pcm = new byte[3 * 16_000 * 2];
        var random = new Random(47);
        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sample = (short)random.Next(-40, 41);
            pcm[i] = (byte)(sample & 0xff);
            pcm[i + 1] = (byte)((sample >> 8) & 0xff);
        }

        Assert.Empty(_planner.Plan(pcm));
    }

    [Fact]
    public void QuietSyntheticToneAboveEnergyFloor_HasCandidateWindows()
    {
        // An energy-planner regression, not proof of recognition quality on a real mic.
        var windows = _planner.Plan(Tone(1, 2, amplitude: 400));
        var window = Assert.Single(windows);
        Assert.True(window.StartSeconds < 1);
        Assert.True(window.EndSeconds > 3);
    }

    [Fact]
    public void ThreePhrasesWithPauses_ProduceMeasuredWindowsUnderTenSeconds()
    {
        var pcm = BuildPhrases(
            (0.5, 2.0),
            (4.0, 6.0),
            (9.0, 11.5));
        var windows = _planner.Plan(pcm);
        Assert.True(windows.Count >= 3, $"Expected >=3 windows, got {windows.Count}");
        Assert.All(windows, w => Assert.InRange(w.SampleCount, 1, PcmSpeechWindowPlanner.MaxWindowSamples));
        Assert.True(windows[0].StartSeconds < 1.0);
        Assert.True(windows[^1].EndSeconds <= pcm.Length / 32000d + 1e-9);
        for (var i = 1; i < windows.Count; i++)
        {
            Assert.True(windows[i].StartSample >= windows[i - 1].EndSample);
        }
    }

    [Fact]
    public void ContinuousSpeechOverTenSeconds_IsHardBound()
    {
        var pcm = Tone(0, 25, amplitude: 8000);
        var windows = _planner.Plan(pcm);
        Assert.NotEmpty(windows);
        Assert.Contains(windows, w => Math.Abs(w.SampleCount - PcmSpeechWindowPlanner.MaxWindowSamples) < PcmSpeechWindowPlanner.FrameSamples);
        Assert.All(windows, w => Assert.True(w.SampleCount <= PcmSpeechWindowPlanner.MaxWindowSamples));
        Assert.True(windows.Sum(w => w.SampleCount) <= pcm.Length / 2);
    }

    [Fact]
    public void LeadingAndTrailingSilence_AreTrimmedWithPadding()
    {
        var pcm = Tone(2, 3, amplitude: 6000);
        var windows = _planner.Plan(pcm);
        Assert.Single(windows);
        Assert.True(windows[0].StartSample < 2 * 16_000);
        Assert.True(windows[0].EndSample > 5 * 16_000);
        Assert.True(windows[0].StartSample >= 0);
        Assert.True(windows[0].EndSample * 2 <= pcm.Length);
    }

    [Fact]
    public void TwentyOneSecondsWithHundredMsPause_WindowsAreOrderedDisjointAndKeepSpeech()
    {
        // R1: padding across a 100ms pause must not leave overlapping windows after hard split.
        var pcm = new byte[21 * 16_000 * 2];
        WriteTone(pcm, 0, 10, 7000, frequencyHz: 400);
        WriteTone(pcm, 10.1, 10.9, 7000, frequencyHz: 400);
        var windows = _planner.Plan(pcm);
        Assert.NotEmpty(windows);
        Assert.All(windows, w => Assert.InRange(w.SampleCount, 1, PcmSpeechWindowPlanner.MaxWindowSamples));
        Assert.True(windows[0].StartSample >= 0);
        Assert.True(windows[^1].EndSample * 2 <= pcm.Length);
        for (var i = 1; i < windows.Count; i++)
        {
            Assert.True(windows[i].StartSample >= windows[i - 1].EndSample);
        }

        // Speech samples near both sides of the pause must remain covered.
        Assert.Contains(windows, w => w.StartSample <= 9 * 16_000 && w.EndSample >= 9 * 16_000);
        Assert.Contains(windows, w => w.StartSample <= (int)(10.5 * 16_000) && w.EndSample >= (int)(10.5 * 16_000));
    }

    [Fact]
    public void VaryingAmplitudeContinuousTone_ProducesCandidateWindows()
    {
        // R2: 400Hz, 10s; 9 frames @1400 + 1 frame @3500 every 200ms. Not a real-mic claim.
        var pcm = new byte[10 * 16_000 * 2];
        const int frameSamples = PcmSpeechWindowPlanner.FrameSamples;
        for (var frame = 0; frame < 10 * 16_000 / frameSamples; frame++)
        {
            var amplitude = (short)(frame % 10 == 9 ? 3500 : 1400);
            WriteTone(pcm, frame * frameSamples / 16000d, frameSamples / 16000d, amplitude, frequencyHz: 400);
        }

        var windows = _planner.Plan(pcm);
        Assert.NotEmpty(windows);
        Assert.All(windows, w => Assert.True(w.SampleCount <= PcmSpeechWindowPlanner.MaxWindowSamples));
    }

    private static byte[] BuildPhrases(params (double start, double end)[] phrases)
    {
        var duration = phrases.Max(p => p.end) + 1;
        var pcm = new byte[(int)(duration * 16_000) * 2];
        foreach (var (start, end) in phrases)
        {
            WriteTone(pcm, start, end - start, 7000);
        }

        return pcm;
    }

    private static byte[] Tone(double startSeconds, double durationSeconds, short amplitude)
    {
        var pcm = new byte[(int)((startSeconds + durationSeconds + 0.5) * 16_000) * 2];
        WriteTone(pcm, startSeconds, durationSeconds, amplitude);
        return pcm;
    }

    private static void WriteTone(
        byte[] pcm,
        double startSeconds,
        double durationSeconds,
        short amplitude,
        double frequencyHz = 440)
    {
        var start = (int)(startSeconds * 16_000);
        var count = (int)(durationSeconds * 16_000);
        for (var i = 0; i < count; i++)
        {
            var sampleIndex = start + i;
            if (sampleIndex * 2 + 1 >= pcm.Length)
            {
                break;
            }

            var sample = (short)(amplitude * Math.Sin(2 * Math.PI * frequencyHz * i / 16_000.0));
            pcm[sampleIndex * 2] = (byte)(sample & 0xff);
            pcm[sampleIndex * 2 + 1] = (byte)((sample >> 8) & 0xff);
        }
    }
}
