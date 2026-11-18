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
        new(fixture.Importer, NullLogger<YoutubeTranscriptService>.Instance, youtube, fixture.CreateClient());

    [Fact]
    public async Task BlockedPlayer_UsesPublicTranscriptWithSourceTimingAndNoAudio()
    {
        using var fixture = new LocalYoutubeFixture(LocalYoutubeFixture.Mode.PublicPanel);
        using var youtube = new YoutubeClient(fixture.CreateClient());
        var captions = await Service(youtube, fixture).GetEnglishCaptionsOnlyAsync(VideoId);

        Assert.True(captions.IsEnglishUsable);
        Assert.Equal("youtube-public-transcript-panel", captions.TranscriptSource);
        Assert.Equal(3, captions.Sentences.Count);
        Assert.Equal("The cat and the dog went to the park with you.", captions.Sentences[0].Text);
        Assert.Equal(2.5, captions.Sentences[1].StartTime);
        Assert.Equal(4.75, captions.Sentences[1].EndTime);
        Assert.Equal(0, fixture.Importer.Calls);
        Assert.Equal(1, fixture.PanelRequests);
    }

    [Fact]
    public async Task PublicTranscript_RejectsMismatchedTimingWithoutFabricatingCues()
    {
        using var fixture = new LocalYoutubeFixture(LocalYoutubeFixture.Mode.BadPanelTiming);
        using var youtube = new YoutubeClient(fixture.CreateClient());
        var captions = await Service(youtube, fixture).GetEnglishCaptionsOnlyAsync(VideoId);
        Assert.False(captions.IsEnglishUsable);
        Assert.Empty(captions.Sentences);
        Assert.Equal(1, fixture.PanelRequests);
        Assert.Equal(0, fixture.Importer.Calls);
    }

    [Fact]
    public async Task PublicTranscriptCancellation_PropagatesWithoutCallingAudio()
    {
        using var fixture = new LocalYoutubeFixture(LocalYoutubeFixture.Mode.WaitPanel);
        using var youtube = new YoutubeClient(fixture.CreateClient());
        using var cts = new CancellationTokenSource();
        var pending = Service(youtube, fixture).GetEnglishCaptionsOnlyAsync(VideoId, cts.Token);
        await fixture.PanelStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        Assert.Equal(0, fixture.Importer.Calls);
    }

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
        public enum Mode { Success, RejectPlayer, WaitForCancellation, PublicPanel, BadPanelTiming, WaitPanel }

        private readonly Mode _mode;
        public RecordingImporter Importer { get; } = new();
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource PanelStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Hosts { get; } = [];
        public int WatchRequests { get; private set; }
        public int CaptionRequests { get; private set; }
        public int PanelRequests { get; private set; }

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
                if (_mode is Mode.RejectPlayer or Mode.PublicPanel or Mode.BadPanelTiming or Mode.WaitPanel)
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

            if (path.Contains("/youtubei/v1/get_panel", StringComparison.Ordinal))
            {
                PanelRequests++;
                PanelStarted.TrySetResult();
                if (_mode == Mode.RejectPlayer) return Text("{}", "application/json");
                if (_mode == Mode.WaitPanel) await Task.Delay(Timeout.Infinite, cancellationToken);
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                Assert.Contains("PAmodern_transcript_view", body);
                Assert.Contains(Convert.ToBase64String([0xaa, 0x09, 0x0f, 0x0a, 0x0b, .. Encoding.ASCII.GetBytes(VideoId), 0x18, 0x01]), body);
                var texts = new[] { "The cat and the dog went to the park with you.", "You have this book for the class and it is good.", "It is on the table or in the bag." };
                return Text(System.Text.Json.JsonSerializer.Serialize(new {
                    content = new { engagementPanelSectionListRenderer = new { content = new { sectionListRenderer = new { contents = new[] {
                        new { itemSectionRenderer = new { sectionIdentifier = "section-en", contents = texts.Select((text, i) => new {
                            macroMarkersPanelItemViewModel = new {
                                item = new { timelineItemViewModel = new { contentItems = new[] { new { transcriptSegmentViewModel = new { simpleText = text } } } } },
                                onTap = new { innertubeCommand = new { watchEndpoint = new { startTimeSeconds = i * 2 } } }
                            }
                        }) } }
                    } } } } },
                    frameworkUpdates = new { entityBatchUpdate = new { mutations = new[] { new { payload = new {
                        timedMarkersListSyncEntity = new { timedListData = new { sections = new[] { new {
                            sectionId = "section-en", timedSyncDataList = Enumerable.Range(0, _mode == Mode.BadPanelTiming ? 2 : 3).Select(i => new {
                                videoTimeMs = (i * 2000 + (i == 1 ? 500 : 0)).ToString(), endTimeMs = (i * 2000 + 2750).ToString()
                            })
                        } } } }
                    } } } } }
                }), "application/json");
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
            ";</script><script>ytcfg.set({\"INNERTUBE_CONTEXT\":{\"client\":{\"clientName\":\"WEB\",\"clientVersion\":\"fixture\",\"hl\":\"en\"}}});</script></body></html>";

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
