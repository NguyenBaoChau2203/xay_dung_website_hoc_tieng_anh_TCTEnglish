using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Speaking;

/// <summary>
/// Reads the public Show transcript panel when the player/captions endpoint is
/// unavailable. Uses source cue boundaries; never estimates timing or runs ASR.
/// </summary>
internal sealed class YoutubePublicTranscriptClient(HttpClient http)
{
    private static readonly Regex ConfigPattern = new(@"ytcfg\.set\((\{.*?\})\);",
        RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    public async Task<List<SpeakingSentence>> GetAsync(string youtubeId, CancellationToken ct)
    {
        if (!Regex.IsMatch(youtubeId, @"\A[a-zA-Z0-9_-]{11}\z"))
            return [];

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var watchRequest = new HttpRequestMessage(HttpMethod.Get, $"https://www.youtube.com/watch?v={youtubeId}");
        watchRequest.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        using var watch = await http.SendAsync(watchRequest, timeout.Token);
        watch.EnsureSuccessStatusCode();
        var html = await watch.Content.ReadAsStringAsync(timeout.Token);

        JsonElement context = default;
        foreach (Match match in ConfigPattern.Matches(html))
        {
            using var config = JsonDocument.Parse(match.Groups[1].Value);
            if (config.RootElement.TryGetProperty("INNERTUBE_CONTEXT", out var candidate))
                context = candidate.Clone();
        }
        if (context.ValueKind != JsonValueKind.Object)
            return [];

        // Public panel parameters: video identity plus the transcript flag, as
        // used by tools/SpeakingCatalogImport/captions.mjs. No account cookies.
        byte[] panelParams = [0xaa, 0x09, 0x0f, 0x0a, 0x0b, .. Encoding.ASCII.GetBytes(youtubeId), 0x18, 0x01];
        using var response = await http.PostAsJsonAsync(
            "https://www.youtube.com/youtubei/v1/get_panel?prettyPrint=false",
            new { context, panelId = "PAmodern_transcript_view", @params = Convert.ToBase64String(panelParams) }, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
        return ParsePanel(json.RootElement);
    }

    private static List<SpeakingSentence> ParsePanel(JsonElement panel)
    {
        var contents = At(panel, "content", "engagementPanelSectionListRenderer", "content", "sectionListRenderer", "contents");
        if (contents.ValueKind != JsonValueKind.Array)
            return [];

        var timingSections = Find(At(panel, "frameworkUpdates"), "timedMarkersListSyncEntity")
            .SelectMany(entity => Items(At(entity, "timedListData", "sections")))
            .ToList();
        var cues = new List<SpeakingSentence>();
        foreach (var block in contents.EnumerateArray())
        {
            var section = At(block, "itemSectionRenderer");
            var rows = Items(At(section, "contents"))
                .Select(row => At(row, "macroMarkersPanelItemViewModel"))
                .Where(row => Items(At(row, "item", "timelineItemViewModel", "contentItems"))
                    .Any(item => At(item, "transcriptSegmentViewModel").ValueKind == JsonValueKind.Object))
                .ToList();
            if (rows.Count == 0)
                continue;

            var sectionId = At(section, "sectionIdentifier").ToString();
            var matchingSections = timingSections.Where(stamp => At(stamp, "sectionId").ToString() == sectionId).ToList();
            if (string.IsNullOrEmpty(sectionId) || matchingSections.Count != 1)
                return [];
            var stamps = Items(At(matchingSections[0], "timedSyncDataList"))
                .Where(stamp => At(stamp, "endTimeMs").ValueKind != JsonValueKind.Undefined).ToList();
            if (stamps.Count != rows.Count)
                return [];

            for (var i = 0; i < rows.Count; i++)
            {
                var start = Number(At(stamps[i], "videoTimeMs")) / 1000;
                var end = Number(At(stamps[i], "endTimeMs")) / 1000;
                var seek = Number(At(rows[i], "onTap", "innertubeCommand", "watchEndpoint", "startTimeSeconds"));
                var text = string.Join(" ", Items(At(rows[i], "item", "timelineItemViewModel", "contentItems"))
                    .Select(item => At(item, "transcriptSegmentViewModel", "simpleText").ToString())).Trim();
                if (!double.IsFinite(start) || !double.IsFinite(end) || !double.IsFinite(seek)
                    || start < 0 || end <= start || Math.Floor(start) != seek || string.IsNullOrWhiteSpace(text)
                    || (cues.Count > 0 && start < cues[^1].StartTime))
                    return [];
                cues.Add(new SpeakingSentence { StartTime = start, EndTime = end, Text = text, VietnameseMeaning = string.Empty });
            }
        }
        return cues;
    }

    private static JsonElement At(JsonElement value, params string[] path)
    {
        foreach (var key in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out value))
                return default;
        }
        return value;
    }

    private static IEnumerable<JsonElement> Items(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    private static double Number(JsonElement value) =>
        double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : double.NaN;

    private static IEnumerable<JsonElement> Find(JsonElement value, string key)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (property.NameEquals(key)) yield return property.Value;
                foreach (var found in Find(property.Value, key)) yield return found;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                foreach (var found in Find(item, key)) yield return found;
        }
    }
}
