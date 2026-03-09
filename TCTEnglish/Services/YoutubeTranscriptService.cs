using System.Net.Http.Headers;
using YoutubeExplode;
using YoutubeExplode.Videos.ClosedCaptions;
using TCTVocabulary.Models;

namespace TCTVocabulary.Services;

public interface IYoutubeTranscriptService
{
    Task<List<SpeakingSentence>> GetTranscriptAsync(string youtubeId);
}

public class YoutubeTranscriptService : IYoutubeTranscriptService
{
    private readonly YoutubeClient _youtubeClient;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YoutubeTranscriptService> _logger;

    public YoutubeTranscriptService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<YoutubeTranscriptService> logger)
    {
        _youtubeClient = new YoutubeClient();
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<SpeakingSentence>> GetTranscriptAsync(string youtubeId)
    {
        try
        {
            var trackManifest = await _youtubeClient.Videos.ClosedCaptions.GetManifestAsync(youtubeId);

            var trackInfo = trackManifest.TryGetByLanguage("en")
                ?? trackManifest.Tracks.FirstOrDefault(t =>
                    t.Language.Name.Contains("English", StringComparison.OrdinalIgnoreCase));

            if (trackInfo != null)
            {
                _logger.LogInformation("Found closed captions for video {YoutubeId}.", youtubeId);
                var track = await _youtubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);
                return ParseClosedCaptions(track);
            }

            _logger.LogWarning("No English captions found for video {YoutubeId}. Falling back to Whisper.", youtubeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get closed captions for video {YoutubeId}. Falling back to Whisper.", youtubeId);
        }

        return await ProcessAudioWithWhisperAsync(youtubeId);
    }

    private static List<SpeakingSentence> ParseClosedCaptions(ClosedCaptionTrack track)
    {
        var sentences = new List<SpeakingSentence>();

        foreach (var caption in track.Captions)
        {
            if (string.IsNullOrWhiteSpace(caption.Text))
            {
                continue;
            }

            var cleanText = caption.Text.Replace("\n", " ").Replace("\r", "").Trim();
            if (string.IsNullOrWhiteSpace(cleanText))
            {
                continue;
            }

            sentences.Add(new SpeakingSentence
            {
                StartTime = caption.Offset.TotalSeconds,
                EndTime = caption.Offset.TotalSeconds + caption.Duration.TotalSeconds,
                Text = cleanText,
                VietnameseMeaning = string.Empty
            });
        }

        return sentences;
    }

    private async Task<List<SpeakingSentence>> ProcessAudioWithWhisperAsync(string youtubeId)
    {
        var apiKey = _configuration["OpenAiApiKey"] ?? _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("OpenAI API key is missing. Whisper fallback skipped for video {YoutubeId}.", youtubeId);
            return new List<SpeakingSentence>();
        }

        var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(youtubeId);
        var audioStreamInfo = streamManifest.GetAudioOnlyStreams().OrderBy(s => s.Bitrate).FirstOrDefault();

        if (audioStreamInfo == null)
        {
            _logger.LogWarning("No audio streams available for video {YoutubeId}", youtubeId);
            return new List<SpeakingSentence>();
        }

        await using var memoryStream = new MemoryStream();
        await _youtubeClient.Videos.Streams.CopyToAsync(audioStreamInfo, memoryStream);
        memoryStream.Position = 0;

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var content = new MultipartFormDataContent();

        var fileContent = new StreamContent(memoryStream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
            audioStreamInfo.Container.Name == "mp4" ? "audio/mp4" : "audio/webm");

        var fileName = $"audio.{audioStreamInfo.Container.Name}";
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent("whisper-1"), "model");
        content.Add(new StringContent("srt"), "response_format");
        content.Add(new StringContent("en"), "language");

        _logger.LogInformation("Sending audio to OpenAI Whisper API for video {YoutubeId}", youtubeId);

        var response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenAI API request failed with status {response.StatusCode}: {errorContent}");
        }

        var srtContent = await response.Content.ReadAsStringAsync();
        return ParseSrt(srtContent);
    }

    private static List<SpeakingSentence> ParseSrt(string srtContent)
    {
        var sentences = new List<SpeakingSentence>();
        var blocks = srtContent.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3)
            {
                continue;
            }

            var timeLine = lines[1];
            var timeParts = timeLine.Split(new[] { " --> " }, StringSplitOptions.None);
            if (timeParts.Length != 2 ||
                !TryParseSrtTime(timeParts[0], out var startTime) ||
                !TryParseSrtTime(timeParts[1], out var endTime))
            {
                continue;
            }

            var text = string.Join(" ", lines.Skip(2).Select(l => l.Trim()));
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            sentences.Add(new SpeakingSentence
            {
                StartTime = startTime,
                EndTime = endTime,
                Text = text,
                VietnameseMeaning = string.Empty
            });
        }

        return sentences;
    }

    private static bool TryParseSrtTime(string timeStr, out double totalSeconds)
    {
        totalSeconds = 0;

        var parts = timeStr.Trim().Split(new[] { ':', ',' });
        if (parts.Length != 4)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var hours) ||
            !int.TryParse(parts[1], out var minutes) ||
            !int.TryParse(parts[2], out var seconds) ||
            !int.TryParse(parts[3], out var milliseconds))
        {
            return false;
        }

        totalSeconds = (hours * 3600) + (minutes * 60) + seconds + (milliseconds / 1000.0);
        return true;
    }
}
