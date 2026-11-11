using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Services.Speaking;
using TCTVocabulary.Services;
using Xunit;
using YoutubeExplode;

namespace TCTEnglish.Tests;

public sealed class YoutubeClientFixtureCompatibilityTests
{
    private const string VideoId = "localfixtu1";

    [Fact]
    public async Task WatchPage_ParsesMetadataAndDurationThroughAngleSharp()
    {
        using var fixture = new LocalYoutubeFixture(LocalYoutubeFixture.Mode.Success);
        using var youtube = new YoutubeClient(fixture.CreateClient());
        var video = await youtube.Videos.GetAsync(VideoId);
        var service = Service(youtube, fixture);

        var metadata = await service.GetVideoMetadataAsync(VideoId);
        var duration = await service.GetVideoDurationAsync(VideoId);

        Assert.Equal("Fixture lesson", video.Title);
        Assert.Equal(TimeSpan.FromSeconds(95), video.Duration);
        Assert.NotNull(metadata);
        Assert.Equal("Fixture lesson", metadata!.Title);
        Assert.Equal("1:35", duration);
        Assert.Equal(3, fixture.WatchRequests);
        Assert.DoesNotContain(fixture.Hosts, host => host != "www.youtube.com");
        Assert.Equal(0, fixture.Importer.Calls);
    }

    [Fact]
    public async Task EnglishCaptions_ImportWithoutCallingAudioFallback()
    {
        using var fixture = new LocalYoutubeFixture(LocalYoutubeFixture.Mode.Success);
        using var youtube = new YoutubeClient(fixture.CreateClient());
        var service = Service(youtube, fixture);

        var imported = await service.GetTranscriptForSpeakingImportAsync(VideoId);

        Assert.True(imported.IsEnglishUsable);
        Assert.Equal("youtube-captions", imported.TranscriptSource);
        Assert.Null(imported.ErrorCode);
        Assert.Equal(3, imported.Sentences.Count);
        Assert.Equal("The cat and the dog went to the park with you.", imported.Sentences[0].Text);
        Assert.True(imported.Sentences[0].EndTime > imported.Sentences[0].StartTime);
        Assert.Equal(0, imported.Sentences[0].StartTime);
        Assert.Equal(0, fixture.Importer.Calls);
        Assert.True(fixture.CaptionRequests > 0);
        Assert.DoesNotContain(fixture.Hosts, host => host != "www.youtube.com");
    }

    [Fact]
    public async Task CancelledCaptionRequest_PropagatesAndDoesNotCallAudio()
    {
        using var fixture = new LocalYoutubeFixture(LocalYoutubeFixture.Mode.WaitForCancellation);
        using var youtube = new YoutubeClient(fixture.CreateClient());
        var service = Service(youtube, fixture);
        using var cts = new CancellationTokenSource();

        var pending = service.GetEnglishCaptionsOnlyAsync(VideoId, cts.Token);
        await fixture.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        Assert.Equal(0, fixture.Importer.Calls);
    }

    [Fact]
    public async Task UnavailablePlayer_ReturnsEmptyCaptionsThenControlledAudioFailure()
    {
        using var fixture = new LocalYoutubeFixture(LocalYoutubeFixture.Mode.RejectPlayer);
        using var youtube = new YoutubeClient(fixture.CreateClient());
        var service = Service(youtube, fixture);

        var captions = await service.GetEnglishCaptionsOnlyAsync(VideoId);
        var imported = await service.GetTranscriptForSpeakingImportAsync(VideoId);

        Assert.False(captions.IsEnglishUsable);
        Assert.Empty(captions.Sentences);
        Assert.False(imported.IsEnglishUsable);
        Assert.Equal("fixture_blocked", imported.ErrorCode);
        Assert.Equal(1, fixture.Importer.Calls);
        Assert.DoesNotContain(fixture.Hosts, host => host != "www.youtube.com");
    }

    private static YoutubeTranscriptService Service(YoutubeClient youtube, LocalYoutubeFixture fixture) =>
        new(fixture.Importer, NullLogger<YoutubeTranscriptService>.Instance, youtube);

    private sealed class RecordingImporter : IAudioTranscriptImporter
    {
        public int Calls { get; private set; }

        public Task<AudioImportResult> TranscribeAsync(string youtubeId, CancellationToken ct)
        {
            Calls++;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(AudioImportResult.Failed("fixture_blocked"));
        }
    }

    private sealed class LocalYoutubeFixture : HttpMessageHandler
    {
        public enum Mode { Success, RejectPlayer, WaitForCancellation }

        private readonly Mode _mode;
        public RecordingImporter Importer { get; } = new();
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Hosts { get; } = [];
        public int WatchRequests { get; private set; }
        public int CaptionRequests { get; private set; }

        public LocalYoutubeFixture(Mode mode) => _mode = mode;

        public HttpClient CreateClient() => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestStarted.TrySetResult();
            var uri = request.RequestUri ?? throw new InvalidOperationException("Missing fixture URI.");
            Hosts.Add(uri.Host);
            if (!string.Equals(uri.Host, "www.youtube.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Fixture refused a non-local YouTube request.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_mode == Mode.WaitForCancellation)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            var path = uri.AbsolutePath;
            if (path.Contains("/watch", StringComparison.Ordinal))
            {
                WatchRequests++;
                return Text(WatchPage);
            }

            if (path.Contains("/sw.js_data", StringComparison.Ordinal))
            {
                return Text(")]}'\n[[0,0,[[[0,0,0,0,0,0,0,0,0,0,0,0,0,\"fixture-visitor\"]]]]]");
            }

            if (path.Contains("/youtubei/v1/player", StringComparison.Ordinal))
            {
                if (_mode == Mode.RejectPlayer)
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                return Text(PlayerJson);
            }

            if (path.Contains("/api/timedtext", StringComparison.Ordinal))
            {
                CaptionRequests++;
                return Text(CaptionXml, "application/xml");
            }

            throw new InvalidOperationException("Fixture has no response for this YouTube path.");
        }

        private static HttpResponseMessage Text(string body, string mediaType = "text/html") =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            };

        private const string PlayerJson = """
            {
              "playabilityStatus": { "status": "OK" },
              "videoDetails": {
                "title": "Fixture lesson",
                "channelId": "UCfixturechannel00000001",
                "author": "Fixture Author",
                "lengthSeconds": "95",
                "shortDescription": "Local fixture",
                "viewCount": "3"
              },
              "microformat": {
                "playerMicroformatRenderer": { "uploadDate": "2020-01-02T00:00:00+00:00" }
              },
              "captions": {
                "playerCaptionsTracklistRenderer": {
                  "captionTracks": [
                    {
                      "baseUrl": "https://www.youtube.com/api/timedtext?v=localfixtu1&lang=en",
                      "languageCode": "en",
                      "vssId": ".en",
                      "name": { "simpleText": "English" }
                    }
                  ]
                }
              }
            }
            """;

        private static readonly string WatchPage =
            "<!DOCTYPE html><html><head>" +
            "<meta property=\"og:url\" content=\"https://www.youtube.com/watch?v=localfixtu1\">" +
            "<meta itemprop=\"uploadDate\" content=\"2020-01-02\">" +
            "</head><body><div id=\"player\"></div><script>var ytInitialPlayerResponse = " +
            string.Concat(PlayerJson.Where(character => character is not '\r' and not '\n')) +
            ";</script></body></html>";

        private const string CaptionXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <timedtext>
              <p t="0" d="2000">The cat and the dog went to the park with you.</p>
              <p t="2000" d="2000">You have this book for the class and it is good.</p>
              <p t="4000" d="2000">It is on the table or in the bag.</p>
            </timedtext>
            """;
    }
}
