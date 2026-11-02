using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Speaking;

public sealed class FfmpegPcmDecoder(IOptions<AudioImportOptions> options, IWebHostEnvironment environment) : IAudioPcmDecoder
{
    private string ExecutablePath => Path.GetFullPath(options.Value.FfmpegPath, environment.ContentRootPath);
    public async Task EnsureAvailableAsync(CancellationToken ct)
    {
        if (!File.Exists(ExecutablePath) || options.Value.FfmpegSha256.Length != 64)
            throw new AudioImportException("decoder_not_ready");
        await using var input = File.OpenRead(ExecutablePath);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, ct));
        if (!hash.Equals(options.Value.FfmpegSha256, StringComparison.OrdinalIgnoreCase))
            throw new AudioImportException("decoder_checksum_mismatch");
    }

    public async Task DecodeAsync(string sourcePath, string pcmPath, CancellationToken ct)
    {
        await EnsureAvailableAsync(ct);
        var start = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            // Avoid loading neighboring runtime libraries from upload/temp directories.
            WorkingDirectory = Path.GetDirectoryName(ExecutablePath)!
        };
        foreach (var value in new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-threads", "1",
            "-protocol_whitelist", "file,pipe", "-format_whitelist", "mov,matroska,webm,wav,mp3,ogg,aac,flac",
            "-i", Path.GetFullPath(sourcePath), "-map", "0:a:0", "-vn", "-sn", "-dn",
            "-t", "1801", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", "-f", "s16le", "pipe:1" })
            start.ArgumentList.Add(value);
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new AudioImportException("decode_failed");
            // Drain without retaining potentially sensitive paths/media metadata or an unbounded log.
            var errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, ct);
            try
            {
                await using var output = new FileStream(pcmPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true);
                await AudioTranscriptImporter.CopyBoundedAsync(process.StandardOutput.BaseStream, output, AudioImportOptions.MaxPcmBytes, ct);
                await process.WaitForExitAsync(ct);
                await errors;
                if (process.ExitCode != 0) throw new AudioImportException("decode_failed");
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                try { await errors; } catch (OperationCanceledException) { }
            }
        }
        catch (System.ComponentModel.Win32Exception) { throw new AudioImportException("decoder_not_ready"); }
    }
}
