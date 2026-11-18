namespace TCTEnglish.Services.Speaking;

/// <summary>
/// Transparent coaching estimates, not phoneme/prosody or proficiency assessment.
/// Consumes the already validated recording and the single ASR transcript.
/// </summary>
public static class SpeakingComponentScorer
{
    public const int Version = 1;

    public static double? Completeness(SpeakingTextAlignmentResult comparison)
    {
        if (!comparison.ComparisonAvailable) return null;
        var reference = comparison.Operations.Where(op => !string.IsNullOrEmpty(op.ReferenceToken)).ToList();
        if (reference.Count == 0) return null;
        // A substitution attempts a reference position; an insertion cannot fill an omission.
        var attempted = reference.Count(op => op.Kind != SpeakingAlignmentKind.Missing);
        return Math.Round(100d * attempted / reference.Count, 2);
    }

    public static double? EstimateFluency(ReadOnlySpan<byte> wav, string transcript)
    {
        var wordCount = SpeakingTextMatch.Tokenize(transcript).Count;
        if (wordCount < 2 || !WavAudioValidator.ValidatePcm16Mono16kHz(wav, int.MaxValue, 60).IsValid)
            return null;

        // Walk RIFF chunks rather than assuming every WAV has a 44-byte header.
        ReadOnlySpan<byte> pcm = default;
        for (var offset = 12; offset <= wav.Length - 8;)
        {
            var size = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(offset + 4, 4));
            if (size < 0 || size > wav.Length - offset - 8) return null;
            if (wav.Slice(offset, 4).SequenceEqual("data"u8))
            {
                pcm = wav.Slice(offset + 8, size);
                break;
            }
            var next = (long)offset + 8 + size + (size & 1);
            if (next > wav.Length) return null;
            offset = (int)next;
        }

        var windows = new PcmSpeechWindowPlanner().Plan(pcm);
        if (windows.Count == 0) return null;
        var signalSeconds = windows.Sum(w => w.SampleCount) / 16000d;
        var spanSeconds = windows[^1].EndSeconds - windows[0].StartSeconds;
        if (signalSeconds < 0.5 || spanSeconds < 0.5) return null;

        // Leading/trailing silence is excluded. The existing planner bridges short
        // natural pauses; remaining internal gaps reduce temporal continuity.
        var continuity = Math.Clamp(signalSeconds / spanSeconds, 0, 1);
        var wordsPerMinute = 60d * wordCount / spanSeconds;
        // Broad application coaching band, not a scientifically calibrated norm.
        var pace = Math.Min(1d, Math.Min(wordsPerMinute / 60d, 240d / wordsPerMinute));
        return Math.Round(Math.Clamp(100d * (0.65 * continuity + 0.35 * pace), 0, 100), 2);
    }
}
