using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using YoutubeExplode;
using YoutubeExplode.Videos.ClosedCaptions;
using TCTVocabulary.Models;
using System.Net.Http.Headers;

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
            // Primary Flow: Fetch Closed Captions
            var trackManifest = await _youtubeClient.Videos.ClosedCaptions.GetManifestAsync(youtubeId);

            // Try to find English captions (manual first, then auto-generated)
            var trackInfo = trackManifest.TryGetByLanguage("en") ?? trackManifest.Tracks.FirstOrDefault(t => t.Language.Name.Contains("English", StringComparison.OrdinalIgnoreCase));

            if (trackInfo != null)
            {
                _logger.LogInformation("Found Closed Captions for video {YoutubeId}. Using Primary Flow.", youtubeId);
                var track = await _youtubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);
                return ParseClosedCaptions(track);
            }

            _logger.LogWarning("No English Closed Captions found for video {YoutubeId}. Falling back to Audio-to-Text (Whisper).", youtubeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Closed Captions for video {YoutubeId}. Falling back to Audio-to-Text (Whisper).", youtubeId);
        }

        // Fallback Flow: Download Audio and use OpenAI Whisper
        return await ProcessAudioWithWhisperAsync(youtubeId);
    }

    private List<SpeakingSentence> ParseClosedCaptions(ClosedCaptionTrack track)
    {
        var sentences = new List<SpeakingSentence>();
        foreach (var caption in track.Captions)
        {
            // Skip empty or purely whitespace captions
            if (string.IsNullOrWhiteSpace(caption.Text)) continue;

            // Replace newlines with spaces for a cleaner text
            var cleanText = caption.Text.Replace("\n", " ").Replace("\r", "").Trim();

            sentences.Add(new SpeakingSentence
            {
                StartTime = caption.Offset.TotalSeconds,
                EndTime = caption.Offset.TotalSeconds + caption.Duration.TotalSeconds,
                Text = cleanText,
                VietnameseMeaning = "" // To be filled later or by another service
            });
        }
        return sentences;
    }

    private async Task<List<SpeakingSentence>> ProcessAudioWithWhisperAsync(string youtubeId)
    {
        var apiKey = _configuration["OpenAiApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAiApiKey is not configured in appsettings.json.");
        }

        // 1. Download lowest quality audio to memory
        var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(youtubeId);
        var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithLowestBitrate(); // Request lowest quality to optimize speed
        
        if (audioStreamInfo == null)
        {
            throw new Exception($"No audio streams available for video {youtubeId}");
        }

        using var memoryStream = new MemoryStream();
        await _youtubeClient.Videos.Streams.CopyToAsync(audioStreamInfo, memoryStream);
        memoryStream.Position = 0;

        // 2. Send to OpenAI Whisper API
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var content = new MultipartFormDataContent();
        
        // Setup file content
        var fileContent = new StreamContent(memoryStream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(audioStreamInfo.Container.Name == "mp4" ? "audio/mp4" : "audio/webm");
        
        // Determine extension based on container if possible, assume mp3 as a fallback for the API request
        var fileName = $"audio.{audioStreamInfo.Container.Name}";
        content.Add(fileContent, "file", fileName);
        
        // Setup other required parameters
        content.Add(new StringContent("whisper-1"), "model");
        content.Add(new StringContent("srt"), "response_format"); // Request SRT format for timestamps
        content.Add(new StringContent("en"), "language");

        _logger.LogInformation("Sending audio to OpenAI Whisper API for video {YoutubeId}", youtubeId);
        
        var response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenAI API request failed with status {response.StatusCode}: {errorContent}");
        }

        var srtContent = await response.Content.ReadAsStringAsync();
        
        // 3. Parse SRT content into SpeakingSentences
        return ParseSrt(srtContent);
    }

    private List<SpeakingSentence> ParseSrt(string srtContent)
    {
        var sentences = new List<SpeakingSentence>();
        
        // SRT format:
        // 1
        // 00:00:00,000 --> 00:00:02,000
        // Text line 1
        // Text line 2
        // [empty line]

        var blocks = srtContent.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length >= 3)
            {
                var timeLine = lines[1];
                var timeParts = timeLine.Split(new[] { " --> " }, StringSplitOptions.None);
                
                if (timeParts.Length == 2 && 
                    TryParseSrtTime(timeParts[0], out double startTime) && 
                    TryParseSrtTime(timeParts[1], out double endTime))
                {
                    // Combine all remaining lines as text
                    var textLines = lines.Skip(2).Select(l => l.Trim());
                    var text = string.Join(" ", textLines);

                    sentences.Add(new SpeakingSentence
                    {
                        StartTime = startTime,
                        EndTime = endTime,
                        Text = text,
                        VietnameseMeaning = ""
                    });
                }
            }
        }

        return sentences;
    }

    private bool TryParseSrtTime(string timeStr, out double totalSeconds)
    {
        totalSeconds = 0;
        // Format: HH:mm:ss,fff
        try
        {
            var parts = timeStr.Trim().Split(new[] { ':', ',' });
            if (parts.Length == 4)
            {
                if (int.TryParse(parts[0], out int hours) &&
                    int.TryParse(parts[1], out int minutes) &&
                    int.TryParse(parts[2], out int seconds) &&
                    int.TryParse(parts[3], out int milliseconds))
                {
                    totalSeconds = (hours * 3600) + (minutes * 60) + seconds + (milliseconds / 1000.0);
                    return true;
                }
            }
        }
        catch { }
        
        return false;
    }
}
