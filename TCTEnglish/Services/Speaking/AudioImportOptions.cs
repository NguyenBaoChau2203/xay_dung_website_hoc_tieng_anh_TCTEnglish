namespace TCTEnglish.Services.Speaking;

public sealed class AudioImportOptions
{
    public const string SectionName = "Learning:AudioImport";
    public const int MaxSourceSeconds = 1800;
    public const long MaxSourceBytes = 100L * 1024 * 1024;
    public const int PcmBytesPerSecond = 32000;
    public const long MaxPcmBytes = MaxSourceSeconds * (long)PcmBytesPerSecond;
    public const int ChunkSeconds = 10;
    public int TotalTimeoutSeconds { get; set; } = 300;
    public string FfmpegPath { get; set; } = Path.Combine("..", ".appdata", "local", "media", "ffmpeg.exe");
    public string FfmpegSha256 { get; set; } = "228d7a8556258de907fdb55f36850078ebc7680b84ec30d84ea02e99bec1d1eb";
}

public sealed record AudioImportResult(IReadOnlyList<SpeechSegment> Segments, string? ErrorCode = null)
{
    public bool Success => ErrorCode is null && Segments.Count > 0;
    public static AudioImportResult Failed(string code) => new(Array.Empty<SpeechSegment>(), code);
}

public interface IAudioTranscriptImporter
{
    Task<AudioImportResult> TranscribeAsync(string youtubeId, CancellationToken ct);
}

public interface IYoutubeAudioSource
{
    Task<Stream> OpenAudioAsync(string youtubeId, CancellationToken ct);
}

public interface IAudioPcmDecoder
{
    Task EnsureAvailableAsync(CancellationToken ct);
    Task DecodeAsync(string sourcePath, string pcmPath, CancellationToken ct);
}

public sealed class AudioImportException(string code) : Exception("Audio import failed: " + code)
{
    public string Code { get; } = code;
}
