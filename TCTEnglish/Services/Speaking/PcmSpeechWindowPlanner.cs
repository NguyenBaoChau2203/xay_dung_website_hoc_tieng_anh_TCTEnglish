namespace TCTEnglish.Services.Speaking;

/// <summary>Sample-indexed speech window (end exclusive). Times = sample / 16000.</summary>
public readonly record struct PcmSpeechWindow(int StartSample, int EndSample)
{
    public double StartSeconds => StartSample / 16000d;
    public double EndSeconds => EndSample / 16000d;
    public int SampleCount => EndSample - StartSample;
}

public interface IPcmSpeechWindowPlanner
{
    IReadOnlyList<PcmSpeechWindow> Plan(ReadOnlySpan<byte> pcm16Mono16kHz);
}

/// <summary>
/// Pure C# energy/silence planner. Segment-window timing only — not word alignment or ASR.
/// </summary>
public sealed class PcmSpeechWindowPlanner : IPcmSpeechWindowPlanner
{
    public const int SampleRate = 16_000;
    public const int BytesPerSample = 2;
    public const int FrameSamples = 320; // 20 ms
    public const int MaxWindowSeconds = 10;
    public const int MaxWindowSamples = MaxWindowSeconds * SampleRate;
    public const int MinWindowSamples = 2 * SampleRate; // 2s target floor when merging
    public const int PadSamples = SampleRate / 10; // 100 ms
    public const int MergeGapSamples = SampleRate / 2; // 500 ms

    public IReadOnlyList<PcmSpeechWindow> Plan(ReadOnlySpan<byte> pcm16Mono16kHz)
    {
        if (pcm16Mono16kHz.Length < BytesPerSample || pcm16Mono16kHz.Length % BytesPerSample != 0)
        {
            return Array.Empty<PcmSpeechWindow>();
        }

        var totalSamples = pcm16Mono16kHz.Length / BytesPerSample;
        if (totalSamples <= 0)
        {
            return Array.Empty<PcmSpeechWindow>();
        }

        var frameCount = Math.Max(1, (totalSamples + FrameSamples - 1) / FrameSamples);
        var energies = new double[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var start = frame * FrameSamples;
            var end = Math.Min(totalSamples, start + FrameSamples);
            energies[frame] = FrameRms(pcm16Mono16kHz, start, end);
        }

        var threshold = ResolveThreshold(energies);
        var speechFrames = new bool[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            speechFrames[i] = energies[i] >= threshold;
        }

        var raw = BuildRawWindows(speechFrames, totalSamples);
        if (raw.Count == 0)
        {
            return Array.Empty<PcmSpeechWindow>();
        }

        var padded = ApplyPadding(raw, totalSamples);
        // Union overlaps (from padding) without the 10s cap; positive-gap merge still capped.
        var merged = MergeWindows(padded);
        return SplitHardBound(merged);
    }

    private static double FrameRms(ReadOnlySpan<byte> pcm, int startSample, int endSample)
    {
        long sumSquares = 0;
        var count = endSample - startSample;
        if (count <= 0)
        {
            return 0;
        }

        for (var sample = startSample; sample < endSample; sample++)
        {
            var offset = sample * BytesPerSample;
            short value = (short)(pcm[offset] | (pcm[offset + 1] << 8));
            sumSquares += (long)value * value;
        }

        return Math.Sqrt(sumSquares / (double)count);
    }

    internal static double ResolveThreshold(IReadOnlyList<double> energies)
    {
        if (energies.Count == 0)
        {
            return double.MaxValue;
        }

        var sorted = energies.ToArray();
        Array.Sort(sorted);
        var noise = sorted[Math.Clamp(sorted.Length / 10, 0, sorted.Length - 1)];
        var peak = sorted[^1];
        if (peak < 50)
        {
            // Near-silence PCM — no usable speech windows.
            return double.MaxValue;
        }

        // Flat continuous speech: noise≈peak would otherwise push threshold above every frame.
        if (noise > peak * 0.45)
        {
            return Math.Max(peak * 0.25, 120);
        }

        var relative = Math.Max(noise * 3.5, peak * 0.08);
        // Mid dynamic range: noise*3.5 can exceed peak (e.g. noise/peak ≈ 0.29–0.45).
        // Keep threshold below peak so high-energy frames remain candidates.
        if (relative >= peak)
        {
            relative = Math.Max(noise * 1.15, peak * 0.30);
        }

        relative = Math.Min(relative, peak * 0.90);
        return Math.Max(relative, 120);
    }

    private static List<PcmSpeechWindow> BuildRawWindows(IReadOnlyList<bool> speechFrames, int totalSamples)
    {
        var windows = new List<PcmSpeechWindow>();
        var index = 0;
        while (index < speechFrames.Count)
        {
            while (index < speechFrames.Count && !speechFrames[index])
            {
                index++;
            }

            if (index >= speechFrames.Count)
            {
                break;
            }

            var startFrame = index;
            while (index < speechFrames.Count && speechFrames[index])
            {
                index++;
            }

            var startSample = startFrame * FrameSamples;
            var endSample = Math.Min(totalSamples, index * FrameSamples);
            if (endSample > startSample)
            {
                windows.Add(new PcmSpeechWindow(startSample, endSample));
            }
        }

        return windows;
    }

    private static List<PcmSpeechWindow> ApplyPadding(IReadOnlyList<PcmSpeechWindow> windows, int totalSamples)
    {
        var padded = new List<PcmSpeechWindow>(windows.Count);
        foreach (var window in windows)
        {
            var start = Math.Max(0, window.StartSample - PadSamples);
            var end = Math.Min(totalSamples, window.EndSample + PadSamples);
            if (end > start)
            {
                padded.Add(new PcmSpeechWindow(start, end));
            }
        }

        return padded;
    }

    private static List<PcmSpeechWindow> MergeWindows(IReadOnlyList<PcmSpeechWindow> windows)
    {
        if (windows.Count == 0)
        {
            return new List<PcmSpeechWindow>();
        }

        var ordered = windows.OrderBy(w => w.StartSample).ThenBy(w => w.EndSample).ToList();
        var merged = new List<PcmSpeechWindow>();
        var current = ordered[0];
        for (var i = 1; i < ordered.Count; i++)
        {
            var next = ordered[i];
            var gap = next.StartSample - current.EndSample;
            var combined = next.EndSample - current.StartSample;

            // Overlap/touching from padding: always union (no 10s cap); hard-split afterwards.
            if (gap <= 0)
            {
                current = new PcmSpeechWindow(current.StartSample, Math.Max(current.EndSample, next.EndSample));
                continue;
            }

            // Positive silence gap: merge only when the combined span still fits one window.
            if (gap <= MergeGapSamples && combined <= MaxWindowSamples)
            {
                current = new PcmSpeechWindow(current.StartSample, next.EndSample);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }

    private static IReadOnlyList<PcmSpeechWindow> SplitHardBound(IReadOnlyList<PcmSpeechWindow> windows)
    {
        var result = new List<PcmSpeechWindow>();
        foreach (var window in windows)
        {
            var start = window.StartSample;
            while (window.EndSample - start > MaxWindowSamples)
            {
                var end = start + MaxWindowSamples;
                result.Add(new PcmSpeechWindow(start, end));
                start = end;
            }

            if (window.EndSample > start)
            {
                result.Add(new PcmSpeechWindow(start, window.EndSample));
            }
        }

        return result;
    }
}
