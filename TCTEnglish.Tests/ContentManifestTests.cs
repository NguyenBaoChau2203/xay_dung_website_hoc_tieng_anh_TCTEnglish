using System.Text.Json;
using System.Text.RegularExpressions;
using TCTEnglish.Services.Sets;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// CI guard for the committed system content manifests (data/content/vocabulary). It checks the card contract
/// that the authoring validator (tools/content/validate_vocab.py) enforces, so the data cannot silently drift.
/// </summary>
public sealed class ContentManifestTests
{
    private static readonly Regex Ipa = new(@"^/[^/\s][^/]*/$", RegexOptions.Compiled);
    private static readonly Regex Hex64 = new("^[0-9a-f]{64}$", RegexOptions.Compiled);
    private static readonly Regex License = new(@"^(CC0|Public Domain Mark|Public domain|PD[- ]|Original work|Pixabay Content License$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] Cefr = ["A1", "A2", "B1", "B2", "C1", "C2"];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static List<(string File, JsonElement Root)> Manifests()
    {
        var dir = Path.Combine(RepoRoot(), "data", "content", "vocabulary");
        return Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetRelativePath(dir, f), JsonDocument.Parse(File.ReadAllText(f)).RootElement.Clone())).ToList();
    }

    [Fact]
    public void ManifestsExistWithUniqueSetCodes_AndExpectedCardCounts()
    {
        var all = Manifests();
        Assert.NotEmpty(all);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (file, root) in all)
        {
            var code = root.GetProperty("set").GetProperty("code").GetString()!;
            Assert.True(codes.Add(code), $"{file}: duplicate set code {code}");
            Assert.Matches("^[ABC][0-9]{2}$", code);
            var expected = code[0] == 'C' ? 20 : 25;
            Assert.True(root.GetProperty("cards").GetArrayLength() == expected, $"{file}: expected {expected} cards");
        }
    }

    [Fact]
    public void EveryCardMeetsTheTextContract()
    {
        foreach (var (file, root) in Manifests())
        {
            var code = root.GetProperty("set").GetProperty("code").GetString()![0];
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in root.GetProperty("cards").EnumerateArray())
            {
                var term = card.GetProperty("term").GetString()!.Trim();
                Assert.True(term.Length is > 0 and <= CardInputNormalizer.MaxTerm, $"{file}: term length");
                Assert.True(seen.Add(term), $"{file}: duplicate term '{term}'");
                var pos = card.GetProperty("partsOfSpeech").EnumerateArray().Select(p => p.GetString()).ToList();
                Assert.NotEmpty(pos);
                Assert.All(pos, p => Assert.True(CardPartsOfSpeech.IsValid(p), $"{file}/{term}: pos {p}"));
                if (code == 'C') Assert.Equal(["phrase"], pos);
                Assert.Contains(card.GetProperty("cefr").GetString(), Cefr);
                Assert.False(string.IsNullOrWhiteSpace(card.GetProperty("definition").GetString()), $"{file}/{term}: definition");
                Assert.False(string.IsNullOrWhiteSpace(card.GetProperty("meaningEn").GetString()), $"{file}/{term}: meaningEn");
                var examples = card.GetProperty("examples").EnumerateArray().ToList();
                Assert.InRange(examples.Count, 2, 3);
                Assert.All(examples, e =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("text").GetString()), $"{file}/{term}: example text");
                    Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("vi").GetString()), $"{file}/{term}: example translation");
                });
                if (code == 'B') Assert.True(card.GetProperty("collocations").GetArrayLength() >= 2, $"{file}/{term}: collocations");
                foreach (var key in new[] { "ipaUs", "ipaUk" })
                    if (card.TryGetProperty(key, out var ipa))
                        Assert.Matches(Ipa, ipa.GetString()!);
                var cardInput = new TCTEnglish.ViewModels.Sets.CardInputDto
                {
                    Term = term, Definition = card.GetProperty("definition").GetString(),
                    PartsOfSpeech = pos!, Cefr = card.GetProperty("cefr").GetString(),
                };
                Assert.True(CardInputNormalizer.Normalize(cardInput).IsValid, $"{file}/{term}: normaliser rejects the card");
            }
        }
    }

    [Fact]
    public void EveryImageHasLicensedProvenance_AndIsNotHotlinked()
    {
        foreach (var (file, root) in Manifests())
            foreach (var card in root.GetProperty("cards").EnumerateArray())
            {
                if (!card.TryGetProperty("image", out var image)) continue; // sets still waiting for images are reported by the authoring validator
                var term = card.GetProperty("term").GetString();
                foreach (var key in new[] { "file", "provider", "pageUrl", "license", "query", "sha256", "reviewedAt" })
                    Assert.False(string.IsNullOrWhiteSpace(image.GetProperty(key).GetString()), $"{file}/{term}: image.{key}");
                Assert.Matches(License, image.GetProperty("license").GetString()!);
                Assert.StartsWith("https://", image.GetProperty("pageUrl").GetString());
                Assert.DoesNotMatch(@"^(https?:|/|\.\.)", image.GetProperty("file").GetString()!);
                Assert.Matches(Hex64, image.GetProperty("sha256").GetString()!);
                Assert.Equal(480, image.GetProperty("width").GetInt32());
                Assert.Equal(360, image.GetProperty("height").GetInt32());
            }
    }

    [Fact]
    public void FewerThanThreePercentOfCardsCarryUnverifiedIpa()
    {
        int cards = 0, unverified = 0;
        foreach (var (_, root) in Manifests())
        {
            if (root.GetProperty("set").GetProperty("code").GetString()![0] == 'C') continue; // phrases carry no IPA
            foreach (var card in root.GetProperty("cards").EnumerateArray())
            {
                cards++;
                if (!(card.TryGetProperty("ipaVerified", out var v) && v.ValueKind == JsonValueKind.True)) unverified++;
            }
        }
        Assert.True(unverified * 100.0 / Math.Max(1, cards) <= 3.0, $"{unverified} of {cards} cards have unverified IPA");
    }
}
