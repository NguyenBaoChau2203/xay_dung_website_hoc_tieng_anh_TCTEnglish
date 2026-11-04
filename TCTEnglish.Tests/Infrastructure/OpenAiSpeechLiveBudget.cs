using System.Buffers.Binary;
using TCTEnglish.Services.Speaking;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Process-shared live audio budget (8 POST / 120s). Enforced at HTTP send boundary.
/// Offline tests construct a fresh instance; live paths use <see cref="Shared"/>.
/// </summary>
public sealed class OpenAiSpeechLiveBudget
{
    public const int DefaultMaxPosts = 8;
    public const double DefaultMaxAudioSeconds = 120d;

    public static OpenAiSpeechLiveBudget Shared { get; } = new();

    private readonly object _gate = new();
    private readonly int _maxPosts;
    private readonly double _maxAudioSeconds;
    private int _posts;
    private double _audioSeconds;

    public OpenAiSpeechLiveBudget(int maxPosts = DefaultMaxPosts, double maxAudioSeconds = DefaultMaxAudioSeconds)
    {
        _maxPosts = maxPosts;
        _maxAudioSeconds = maxAudioSeconds;
    }

    public int Posts
    {
        get { lock (_gate) { return _posts; } }
    }

    public double AudioSeconds
    {
        get { lock (_gate) { return _audioSeconds; } }
    }

    public bool TryReserve(double audioSeconds, out string reason)
    {
        if (audioSeconds < 0 || double.IsNaN(audioSeconds) || double.IsInfinity(audioSeconds))
        {
            reason = "invalid_audio_seconds";
            return false;
        }

        lock (_gate)
        {
            if (_posts >= _maxPosts)
            {
                reason = "post_cap";
                return false;
            }

            if (_audioSeconds + audioSeconds > _maxAudioSeconds + 1e-9)
            {
                reason = "audio_cap";
                return false;
            }

            _posts += 1;
            _audioSeconds += audioSeconds;
            reason = "ok";
            return true;
        }
    }

    public void ResetForTests()
    {
        lock (_gate)
        {
            _posts = 0;
            _audioSeconds = 0;
        }
    }
}

/// <summary>
/// Reserves live budget from verified WAV payload length before the inner send.
/// Failed/canceled attempts after reservation still consume budget (already reserved).
/// </summary>
public sealed class OpenAiSpeechLiveBudgetHandler : DelegatingHandler
{
    private readonly OpenAiSpeechLiveBudget _budget;

    public OpenAiSpeechLiveBudgetHandler(HttpMessageHandler inner, OpenAiSpeechLiveBudget budget)
        : base(inner)
    {
        _budget = budget;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var audioSeconds = await MeasureRequestAudioSecondsAsync(request, cancellationToken);
        if (!_budget.TryReserve(audioSeconds, out var reason))
        {
            throw new InvalidOperationException(
                $"OpenAI speech live budget exhausted ({reason}). Max {OpenAiSpeechLiveBudget.DefaultMaxPosts} POSTs / {OpenAiSpeechLiveBudget.DefaultMaxAudioSeconds}s.");
        }

        return await base.SendAsync(request, cancellationToken);
    }

    internal static async Task<double> MeasureRequestAudioSecondsAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not MultipartFormDataContent multipart)
        {
            throw new InvalidOperationException("Live speech budget requires multipart WAV content.");
        }

        var files = multipart.Where(part =>
            string.Equals(part.Headers.ContentDisposition?.Name?.Trim('"'), "file", StringComparison.Ordinal)).ToArray();
        if (files.Length != 1)
        {
            throw new InvalidOperationException("Live speech budget requires exactly one multipart file part.");
        }

        // HttpContent buffers this read, including StreamContent: sending the original content
        // still sends the same complete bytes and keeps headers/disposal ownership intact.
        var fileBytes = await files[0].ReadAsByteArrayAsync(cancellationToken);
        return MeasureWavPcmSeconds(fileBytes);
    }

    internal static double MeasureWavPcmSeconds(byte[] wav)
    {
        var bytes = wav.AsSpan();
        if (wav.Length < 44
            || !bytes[..4].SequenceEqual("RIFF"u8)
            || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4)) != (long)wav.Length - 8)
        {
            throw new InvalidOperationException("Live speech budget requires a complete RIFF/WAVE payload.");
        }

        var formatFound = false;
        var dataBytes = -1;
        var offset = 12;
        while (offset < wav.Length)
        {
            if (wav.Length - offset < 8)
            {
                throw new InvalidOperationException("Live speech budget found a truncated WAV chunk header.");
            }

            var chunkId = bytes.Slice(offset, 4);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            var next = offset + 8L + chunkSize + (chunkSize & 1);
            if (next > wav.Length)
            {
                throw new InvalidOperationException("Live speech budget found a truncated WAV chunk.");
            }

            if (chunkId.SequenceEqual("fmt "u8))
            {
                if (formatFound || chunkSize < 16)
                {
                    throw new InvalidOperationException("Live speech budget requires one valid WAV format chunk.");
                }

                var format = bytes.Slice(offset + 8, (int)chunkSize);
                if (BinaryPrimitives.ReadUInt16LittleEndian(format[..2]) != 1
                    || BinaryPrimitives.ReadUInt16LittleEndian(format.Slice(2, 2)) != 1
                    || BinaryPrimitives.ReadUInt32LittleEndian(format.Slice(4, 4)) != 16_000
                    || BinaryPrimitives.ReadUInt32LittleEndian(format.Slice(8, 4)) != AudioImportOptions.PcmBytesPerSecond
                    || BinaryPrimitives.ReadUInt16LittleEndian(format.Slice(12, 2)) != 2
                    || BinaryPrimitives.ReadUInt16LittleEndian(format.Slice(14, 2)) != 16)
                {
                    throw new InvalidOperationException("Live speech budget requires PCM16 mono 16kHz WAV.");
                }

                formatFound = true;
            }
            else if (chunkId.SequenceEqual("data"u8))
            {
                if (dataBytes >= 0 || chunkSize == 0 || chunkSize % 2 != 0)
                {
                    throw new InvalidOperationException("Live speech budget requires one aligned WAV data chunk.");
                }

                dataBytes = (int)chunkSize;
            }

            offset = (int)next;
        }

        if (!formatFound || dataBytes <= 0)
        {
            throw new InvalidOperationException("Live speech budget could not measure WAV PCM bytes.");
        }

        return dataBytes / (double)AudioImportOptions.PcmBytesPerSecond;
    }
}
