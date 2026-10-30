namespace TCTEnglish.Services.Speaking;

public sealed class WavValidationResult
{
    public required bool IsValid { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public int SampleRate { get; init; }
    public short Channels { get; init; }
    public short BitsPerSample { get; init; }
    public double DurationSeconds { get; init; }
}

public static class WavAudioValidator
{
    public static WavValidationResult ValidatePcm16Mono16kHz(ReadOnlySpan<byte> bytes, int maxBytes, int maxSeconds)
    {
        if (bytes.Length < 44)
        {
            return Fail("invalid_audio", "File âm thanh không hợp lệ.");
        }

        if (bytes.Length > maxBytes)
        {
            return Fail("payload_too_large", "File âm thanh vượt quá dung lượng cho phép.");
        }

        // RIFF....WAVE
        if (bytes[0] != (byte)'R' || bytes[1] != (byte)'I' || bytes[2] != (byte)'F' || bytes[3] != (byte)'F'
            || bytes[8] != (byte)'W' || bytes[9] != (byte)'A' || bytes[10] != (byte)'V' || bytes[11] != (byte)'E')
        {
            return Fail("invalid_audio", "Chỉ chấp nhận WAV PCM.");
        }

        var fmtOffset = FindChunk(bytes, "fmt ");
        if (fmtOffset < 0 || fmtOffset + 24 > bytes.Length)
        {
            return Fail("invalid_audio", "Thiếu chunk fmt trong WAV.");
        }

        var audioFormat = BitConverter.ToInt16(bytes.Slice(fmtOffset + 8, 2));
        var channels = BitConverter.ToInt16(bytes.Slice(fmtOffset + 10, 2));
        var sampleRate = BitConverter.ToInt32(bytes.Slice(fmtOffset + 12, 4));
        var bitsPerSample = BitConverter.ToInt16(bytes.Slice(fmtOffset + 22, 2));

        if (audioFormat != 1 || channels != 1 || sampleRate != 16000 || bitsPerSample != 16)
        {
            return Fail("invalid_audio", "Yêu cầu WAV mono PCM16 16kHz.");
        }

        var dataOffset = FindChunk(bytes, "data");
        if (dataOffset < 0 || dataOffset + 8 > bytes.Length)
        {
            return Fail("invalid_audio", "Thiếu chunk data trong WAV.");
        }

        var dataSize = BitConverter.ToInt32(bytes.Slice(dataOffset + 4, 4));
        if (dataSize <= 0 || dataOffset + 8 + dataSize > bytes.Length)
        {
            return Fail("invalid_audio", "Chunk data WAV không hợp lệ.");
        }

        var duration = dataSize / (double)(sampleRate * channels * (bitsPerSample / 8));
        if (duration <= 0.05)
        {
            return Fail("invalid_audio", "Bản ghi quá ngắn.");
        }

        if (duration > maxSeconds + 0.25)
        {
            return Fail("audio_too_long", $"Chỉ chấp nhận tối đa {maxSeconds} giây.");
        }

        return new WavValidationResult
        {
            IsValid = true,
            SampleRate = sampleRate,
            Channels = channels,
            BitsPerSample = bitsPerSample,
            DurationSeconds = duration
        };
    }

    private static WavValidationResult Fail(string code, string message) => new()
    {
        IsValid = false,
        ErrorCode = code,
        Message = message
    };

    private static int FindChunk(ReadOnlySpan<byte> bytes, string fourCc)
    {
        // Start after RIFF header (12 bytes)
        var offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            var id0 = (char)bytes[offset];
            var id1 = (char)bytes[offset + 1];
            var id2 = (char)bytes[offset + 2];
            var id3 = (char)bytes[offset + 3];
            var size = BitConverter.ToInt32(bytes.Slice(offset + 4, 4));
            if (size < 0)
            {
                return -1;
            }

            if (id0 == fourCc[0] && id1 == fourCc[1] && id2 == fourCc[2] && id3 == fourCc[3])
            {
                return offset;
            }

            offset += 8 + size;
            if ((size & 1) == 1)
            {
                offset++; // word align
            }
        }

        return -1;
    }
}
