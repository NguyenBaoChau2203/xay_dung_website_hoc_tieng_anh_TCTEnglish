using System.Text;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Speaking;

/// <summary>Long-media orchestration; never passes a full video into the learner clip adapter.</summary>
public sealed class AudioTranscriptImporter(
    IYoutubeAudioSource source,
    IAudioPcmDecoder decoder,
    ISpeechTranscriber transcriber,
    IPcmSpeechWindowPlanner planner,
    IOpenAiSpeechCredentialResolver credentials,
    IOptions<SpeechProcessingOptions> speechOptions,
    IOptions<AudioImportOptions> options,
    ILogger<AudioTranscriptImporter> logger) : IAudioTranscriptImporter
{
    public async Task<AudioImportResult> TranscribeAsync(string youtubeId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!speechOptions.Value.Enabled || !credentials.TryResolve(out _))
        {
            return AudioImportResult.Failed("engine_not_ready");
        }

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
            {
                await CopyBoundedAsync(input, output, AudioImportOptions.MaxSourceBytes, budget.Token);
            }

            await decoder.DecodeAsync(mediaPath, pcmPath, budget.Token);
            var pcmBytes = await File.ReadAllBytesAsync(pcmPath, budget.Token);
            if (pcmBytes.Length <= 0 || pcmBytes.Length % 2 != 0)
            {
                return AudioImportResult.Failed("invalid_audio");
            }

            if (pcmBytes.Length > AudioImportOptions.MaxPcmBytes)
            {
                return AudioImportResult.Failed("audio_too_large");
            }

            var windows = planner.Plan(pcmBytes);
            if (windows.Count == 0)
            {
                return AudioImportResult.Failed("no_speech");
            }

            var maxRequests = Math.Clamp(options.Value.MaxTranscriptionRequests, 1, 500);
            if (windows.Count > maxRequests)
            {
                return AudioImportResult.Failed("request_cap_exceeded");
            }

            var sourceDuration = pcmBytes.Length / (double)AudioImportOptions.PcmBytesPerSecond;
            // Validate the entire plan before any paid/provider call.
            if (!TryValidateWindows(windows, pcmBytes.Length, sourceDuration))
            {
                return AudioImportResult.Failed("invalid_timestamps");
            }

            var segments = new List<SpeechSegment>();
            foreach (var window in windows)
            {
                budget.Token.ThrowIfCancellationRequested();

                var byteStart = window.StartSample * 2;
                var byteCount = window.SampleCount * 2;
                using var wav = CreateWav(pcmBytes.AsSpan(byteStart, byteCount));
                // OpenAiSpeechTranscriber acquires/releases the shared limiter; yield between windows.
                var result = await transcriber.TranscribeAsync(wav, budget.Token);
                if (result.Status == SpeechTranscriptionStatus.NoSpeech)
                {
                    await Task.Yield();
                    continue;
                }

                if (result.Status != SpeechTranscriptionStatus.Ok)
                {
                    return AudioImportResult.Failed(result.ErrorCode ?? "engine_unavailable");
                }

                if (string.IsNullOrWhiteSpace(result.Transcript))
                {
                    await Task.Yield();
                    continue;
                }

                // Measured audio-window timing owned by the importer — not provider word timestamps.
                segments.Add(new SpeechSegment
                {
                    StartSeconds = window.StartSeconds,
                    EndSeconds = window.EndSeconds,
                    Text = result.Transcript.Trim()
                });
                await Task.Yield();
            }

            budget.Token.ThrowIfCancellationRequested();
            if (segments.Count == 0)
            {
                return AudioImportResult.Failed("no_speech");
            }

            var previousEnd = 0d;
            foreach (var segment in segments)
            {
                if (!double.IsFinite(segment.StartSeconds)
                    || !double.IsFinite(segment.EndSeconds)
                    || segment.StartSeconds < previousEnd
                    || segment.EndSeconds <= segment.StartSeconds
                    || segment.EndSeconds > sourceDuration + 1e-6
                    || string.IsNullOrWhiteSpace(segment.Text))
                {
                    return AudioImportResult.Failed("invalid_timestamps");
                }

                previousEnd = segment.EndSeconds;
            }

            return new AudioImportResult(segments);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AudioImportResult.Failed("audio_timeout");
        }
        catch (AudioImportException ex)
        {
            return AudioImportResult.Failed(ex.Code);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or YoutubeExplode.Exceptions.YoutubeExplodeException)
        {
            logger.LogWarning("Audio import unavailable ({FailureType}).", ex.GetType().Name);
            return AudioImportResult.Failed("audio_unavailable");
        }
        finally
        {
            if (ownedDirectory != null)
            {
                try
                {
                    Directory.Delete(ownedDirectory, recursive: true);
                }
                catch (IOException)
                {
                    logger.LogWarning("Owned audio import temp cleanup failed.");
                }
                catch (UnauthorizedAccessException)
                {
                    logger.LogWarning("Owned audio import temp cleanup denied.");
                }
            }
        }
    }

    internal static bool TryValidateWindows(
        IReadOnlyList<PcmSpeechWindow> windows,
        int pcmByteLength,
        double sourceDurationSeconds)
    {
        var previousEndSample = 0;
        foreach (var window in windows)
        {
            if (window.StartSample < previousEndSample
                || window.EndSample <= window.StartSample
                || window.StartSample < 0
                || window.EndSample * 2L > pcmByteLength
                || window.SampleCount > PcmSpeechWindowPlanner.MaxWindowSamples
                || !double.IsFinite(window.StartSeconds)
                || !double.IsFinite(window.EndSeconds)
                || window.EndSeconds > sourceDurationSeconds + 1e-9)
            {
                return false;
            }

            previousEndSample = window.EndSample;
        }

        return true;
    }

    public static async Task CopyBoundedAsync(Stream input, Stream output, long maxBytes, CancellationToken ct)
    {
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new AudioImportException("audio_too_large");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    public static MemoryStream CreateWav(ReadOnlySpan<byte> pcm)
    {
        var stream = new MemoryStream(pcm.Length + 44);
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcm.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(16000);
            writer.Write(32000);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcm.Length);
            writer.Write(pcm);
        }

        stream.Position = 0;
        return stream;
    }
}
