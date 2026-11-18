using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

/// <summary>Explicit, source-controlled public guidance files; never arbitrary directory discovery.</summary>
internal static class WebsiteGuideCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    internal static async Task<IReadOnlyList<VersionedWebsiteGuide>> ReadAsync(string rootFile, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(rootFile)) return [];
        var root = await ReadFileAsync(rootFile, ct);
        if (root.Includes.Count > 16) throw new InvalidDataException("Too many guide packs.");
        var documents = new List<WebsiteGuideDocument> { root };
        var directory = Path.GetDirectoryName(Path.GetFullPath(rootFile))!;
        foreach (var include in root.Includes)
        {
            ct.ThrowIfCancellationRequested();
            if (include is null || !Regex.IsMatch(include, @"^guide-packs/[a-z0-9]+(?:-[a-z0-9]+)*\.json$",
                    RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(50)))
                throw new InvalidDataException("Guide includes must name a local reviewed guide pack.");
            var packPath = Path.Combine(directory, include.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(packPath)) throw new InvalidDataException("A declared guide pack is missing.");
            if ((File.GetAttributes(Path.GetDirectoryName(packPath)!) & FileAttributes.ReparsePoint) != 0
                || (File.GetAttributes(packPath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Guide packs must not cross filesystem links.");
            var pack = await ReadFileAsync(packPath, ct);
            if (pack.Includes.Count > 0) throw new InvalidDataException("Nested guide includes are not supported.");
            documents.Add(pack);
        }

        var result = new List<VersionedWebsiteGuide>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var questions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            foreach (var guide in document.Guides)
            {
                ct.ThrowIfCancellationRequested();
                guide.Id = guide.Id?.Trim() ?? string.Empty;
                guide.Keywords ??= [];
                guide.Questions ??= [];
                guide.MatchPhrases ??= [];
                guide.MatchRules ??= [];
                if (guide.MatchRules.Count > 32 || guide.MatchRules.Any(rule => rule is null || rule.Count is < 2 or > 6
                        || rule.Any(term => string.IsNullOrWhiteSpace(term) || term.Length > 80)))
                    throw new InvalidDataException("Guide match rules must contain bounded, explicit phrases.");
                if (guide.Id.Length > 0 && !ids.Add(guide.Id)) throw new InvalidDataException("Duplicate guide identity.");
                foreach (var question in guide.Questions)
                {
                    var key = AiTextNormalizer.Normalize(question);
                    if (key.Length == 0) continue;
                    if (questions.TryGetValue(key, out var owner) && owner != guide.Id)
                        throw new InvalidDataException("A reviewed question has conflicting guide identities.");
                    questions[key] = guide.Id;
                }
                result.Add(new(guide, string.IsNullOrWhiteSpace(document.Version) ? "unversioned" : document.Version));
            }
        }
        return result;
    }

    private static async Task<WebsiteGuideDocument> ReadFileAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, useAsync: true);
        if (stream.Length > 2 * 1024 * 1024) throw new InvalidDataException("Guide file exceeds the supported size.");
        try
        {
            var document = await JsonSerializer.DeserializeAsync<WebsiteGuideDocument>(stream, JsonOptions, ct);
            if (document is null || document.Guides is null || document.Includes is null)
                throw new InvalidDataException("Guide document is empty or malformed.");
            return document;
        }
        catch (JsonException ex) { throw new InvalidDataException("Guide document is malformed.", ex); }
    }

    private sealed class WebsiteGuideDocument
    {
        [JsonPropertyName("_version")] public string? Version { get; set; }
        [JsonPropertyName("_includes")] public List<string> Includes { get; set; } = [];
        public List<WebsiteGuideEntry> Guides { get; set; } = [];
    }
}

internal sealed record VersionedWebsiteGuide(WebsiteGuideEntry Guide, string Version);

internal sealed class WebsiteGuideEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = [];
    public List<string> Questions { get; set; } = [];
    public List<string> MatchPhrases { get; set; } = [];
    public List<List<string>> MatchRules { get; set; } = [];
    public string Body { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string Topic { get; set; } = string.Empty;
}
