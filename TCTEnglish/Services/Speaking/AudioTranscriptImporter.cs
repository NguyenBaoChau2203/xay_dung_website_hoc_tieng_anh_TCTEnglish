using System.Text;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Speaking;

/// <summary>Long-media orchestration; never passes a full video into the learner clip adapter.</summary>
public sealed class AudioTranscriptImporter(
    IYoutubeAudioSource source, IAudioPcmDecoder decoder, ISpeechTranscriber transcriber,
    IOptions<SpeechProcessingOptions> speechOptions, IOptions<AudioImportOptions> options,
    ILogger<AudioTranscriptImporter> logger) : IAudioTranscriptImporter
{
    public async Task<AudioImportResult> TranscribeAsync(string youtubeId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!speechOptions.Value.IsConfigured) return AudioImportResult.Failed("engine_not_ready");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.TotalTimeoutSeconds, 1, 300)));
        string? ownedDirectory = null;
        try
        {
            await decoder.EnsureAvailableAsync(budget.Token);
            ownedDirectory = Path.Combine(Path.GetTempPath(), "TCTEnglish", "audio-import", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ownedDirectory);
            var mediaPath = Path.Combine(ownedDirectory, "source.media");
            var pcmPath = Path.Combine(ownedDirectory, "audio.pcm");
            await using (var input = await source.OpenAudioAsync(youtubeId, budget.Token))
            await using (var output = new FileStream(mediaPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true))
                await CopyBoundedAsync(input, output, AudioImportOptions.MaxSourceBytes, budget.Token);
            await decoder.DecodeAsync(mediaPath, pcmPath, budget.Token);
            await using var pcm = new FileStream(pcmPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
            if (pcm.Length <= 0 || pcm.Length % 2 != 0) return AudioImportResult.Failed("invalid_audio");
            if (pcm.Length > AudioImportOptions.MaxPcmBytes) return AudioImportResult.Failed("audio_too_large");
            var segments = new List<SpeechSegment>();
            var chunk = new byte[AudioImportOptions.ChunkSeconds * AudioImportOptions.PcmBytesPerSecond];
            long offsetBytes = 0;
            while (offsetBytes < pcm.Length)
            {
                var count = (int)Math.Min(chunk.Length, pcm.Length - offsetBytes);
                await pcm.ReadExactlyAsync(chunk.AsMemory(0, count), budget.Token);
                using var wav = CreateWav(chunk.AsSpan(0, count));
                // WhisperHttpTranscriber acquires/releases the same singleton limiter as learner assessment.
                // Each chunk yields its lease, allowing queued learner requests between chunks.
                var result = await transcriber.TranscribeAsync(wav, budget.Token);
                if (result.Status == SpeechTranscriptionStatus.NoSpeech)
                { offsetBytes += count; continue; }
                if (result.Status != SpeechTranscriptionStatus.Ok)
                    return AudioImportResult.Failed(result.ErrorCode ?? "engine_unavailable");
                if (result.Segments.Count == 0) return AudioImportResult.Failed("missing_timestamps");
                var duration = count / (double)AudioImportOptions.PcmBytesPerSecond;
                var offset = offsetBytes / (double)AudioImportOptions.PcmBytesPerSecond;
                var previousEnd = 0d;
                foreach (var segment in result.Segments)
                {
                    if (!double.IsFinite(segment.StartSeconds) || !double.IsFinite(segment.EndSeconds)
                        || segment.StartSeconds < previousEnd || segment.EndSeconds <= segment.StartSeconds
                        || segment.EndSeconds > duration || string.IsNullOrWhiteSpace(segment.Text))
                        return AudioImportResult.Failed("invalid_timestamps");
                    segments.Add(new() { StartSeconds = offset + segment.StartSeconds, EndSeconds = offset + segment.EndSeconds, Text = segment.Text });
                    previousEnd = segment.EndSeconds;
                }
                offsetBytes += count;
                await Task.Yield();
            }
            budget.Token.ThrowIfCancellationRequested();
            return segments.Count == 0 ? AudioImportResult.Failed("no_speech") : new AudioImportResult(segments);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return AudioImportResult.Failed("audio_timeout"); }
        catch (AudioImportException ex) { return AudioImportResult.Failed(ex.Code); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or YoutubeExplode.Exceptions.YoutubeExplodeException)
        {
            logger.LogWarning("Audio import unavailable ({FailureType}).", ex.GetType().Name);
            return AudioImportResult.Failed("audio_unavailable");
        }
        finally
        {
            if (ownedDirectory != null)
            {
                try { Directory.Delete(ownedDirectory, recursive: true); }
                catch (IOException) { logger.LogWarning("Owned audio import temp cleanup failed."); }
                catch (UnauthorizedAccessException) { logger.LogWarning("Owned audio import temp cleanup denied."); }
            }
        }
    }

    public static async Task CopyBoundedAsync(Stream input, Stream output, long maxBytes, CancellationToken ct)
    {
        var buffer = new byte[8192]; long total = 0; int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > maxBytes) throw new AudioImportException("audio_too_large");
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    public static MemoryStream CreateWav(ReadOnlySpan<byte> pcm)
    {
        var stream = new MemoryStream(pcm.Length + 44);
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000);
            writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcm.Length); writer.Write(pcm);
        }
        stream.Position = 0;
        return stream;
    }
}
