using YoutubeExplode;

namespace TCTEnglish.Services.Speaking;

/// <summary>Downloads audio-only; video metadata is checked before resolving a media stream.</summary>
public sealed class YoutubeAudioSource : IYoutubeAudioSource
{
    private readonly YoutubeClient _youtube = new();
    public async Task<Stream> OpenAudioAsync(string youtubeId, CancellationToken ct)
    {
        var video = await _youtube.Videos.GetAsync(youtubeId, ct);
        if (video.Duration is null || video.Duration.Value.TotalSeconds <= 0)
            throw new AudioImportException("duration_unavailable");
        if (video.Duration.Value.TotalSeconds > AudioImportOptions.MaxSourceSeconds)
            throw new AudioImportException("audio_too_large");
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(youtubeId, ct);
        var audio = manifest.GetAudioOnlyStreams().OrderBy(s => s.Bitrate).FirstOrDefault()
            ?? throw new AudioImportException("audio_unavailable");
        if (audio.Size.Bytes > AudioImportOptions.MaxSourceBytes) throw new AudioImportException("audio_too_large");
        return await _youtube.Videos.Streams.GetAsync(audio, ct);
    }
}
