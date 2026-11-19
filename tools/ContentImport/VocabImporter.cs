using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.Sets;
using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Models;

namespace ContentImport;

public sealed record ImageProvenance(
    string File, string Provider, string PageUrl, string? Author, string License, string Sha256);

public sealed record ManifestExample(string Text, string Vi);
public sealed record ManifestWordFamily(string Text, string Pos, string? Vi);
public sealed record ManifestVerbForms(string? Past, string? PastParticiple, string? Ing, string? ThirdPerson);

public sealed record ManifestCard(
    string Term, List<string> PartsOfSpeech, string Cefr, string Definition, string MeaningEn,
    List<ManifestExample> Examples, List<ManifestExample>? Collocations,
    string? IpaUs, string? IpaUk, ImageProvenance? Image,
    List<ManifestWordFamily>? WordFamily, List<string>? Synonyms, List<string>? Antonyms,
    ManifestVerbForms? VerbForms, string? Plural);

public sealed record ManifestSet(string Code, string Name, string Description, string Topic);
public sealed record VocabManifest(int SchemaVersion, string Folder, ManifestSet Set, List<ManifestCard> Cards);

public sealed record SetReport(string Code, string Folder, string Set, int Created, int Updated, int Unchanged, int ImagesCopied, List<string> Extra);

/// <summary>
/// Idempotent importer of data/content/vocabulary manifests for the system user. Matches by
/// (system owner, folder name, set name, term) and never deletes a card: unknown cards are reported.
/// </summary>
public sealed class VocabImporter(DbflashcardContext db, string repoRoot)
{
    public const string SystemEmail = "system@tct.local";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string> ManifestFiles(string repoRoot)
        => Directory.GetFiles(Path.Combine(repoRoot, "data/content/vocabulary"), "*.json", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).ToList();

    public static VocabManifest Read(string path)
        => JsonSerializer.Deserialize<VocabManifest>(File.ReadAllText(path), Json)
           ?? throw new InvalidDataException($"Invalid manifest: {path}");

    public async Task<SetReport> ImportAsync(VocabManifest manifest, bool write, CancellationToken ct = default)
    {
        var system = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == SystemEmail, ct)
            ?? throw new InvalidOperationException("System user system@tct.local does not exist.");
        await using var tx = write ? await db.Database.BeginTransactionAsync(ct) : null;

        var folder = await db.Folders.SingleOrDefaultAsync(
            f => f.UserId == system.UserId && f.FolderName == manifest.Folder && f.ParentFolderId == null, ct);
        if (folder == null)
        {
            folder = new Folder { UserId = system.UserId, FolderName = manifest.Folder };
            if (write) { db.Folders.Add(folder); await db.SaveChangesAsync(ct); }
        }

        var set = folder.FolderId == 0 ? null : await db.Sets.Include(s => s.Cards)
            .SingleOrDefaultAsync(s => s.OwnerId == system.UserId && s.FolderId == folder.FolderId && s.SetName == manifest.Set.Name, ct);
        if (set == null)
        {
            set = new Set
            {
                SetName = manifest.Set.Name, OwnerId = system.UserId, FolderId = folder.FolderId == 0 ? null : folder.FolderId,
                Description = manifest.Set.Description, Visibility = "Public", CreatedAt = DateTime.UtcNow,
            };
            if (write) { db.Sets.Add(set); await db.SaveChangesAsync(ct); }
        }
        else if (write && set.Description != manifest.Set.Description)
        {
            set.Description = manifest.Set.Description;
            set.UpdatedAt = DateTime.UtcNow;
        }

        var existing = set.Cards.ToList();
        int created = 0, updated = 0, unchanged = 0, images = 0;
        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imageDir = Path.Combine(repoRoot, "TCTEnglish/wwwroot/images/cards");
        var finalDir = Path.Combine(repoRoot, ".appdata/local/content-build/vocab-images/final");
        if (write) Directory.CreateDirectory(imageDir);

        foreach (var c in manifest.Cards)
        {
            wanted.Add(c.Term);
            var input = ToInput(c, manifest.Set.Topic);
            if (c.Image != null)
            {
                var source = Path.Combine(finalDir, c.Image.File.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(source)) throw new FileNotFoundException($"Final image missing for '{c.Term}': {source}");
                var bytes = await File.ReadAllBytesAsync(source, ct);
                var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                if (hash != c.Image.Sha256) throw new InvalidDataException($"Image hash differs from the manifest for '{c.Term}'.");
                var fileName = "sys-" + manifest.Set.Code.ToLowerInvariant() + "-" + Path.GetFileName(c.Image.File);
                if (write)
                {
                    var target = Path.Combine(imageDir, fileName);
                    if (!File.Exists(target) || Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(target, ct))) != hash)
                    {
                        await File.WriteAllBytesAsync(target, bytes, ct);
                        images++;
                    }
                }
                input.ImageUrl = "/images/cards/" + fileName;
                input.ImageSource = new CardImageSource { Provider = "upload", PageUrl = c.Image.PageUrl, Author = c.Image.Author };
            }

            var normalized = CardInputNormalizer.Normalize(input);
            if (!normalized.IsValid)
                throw new InvalidDataException($"{manifest.Set.Code} '{c.Term}': " + string.Join("; ", normalized.FieldErrors.Select(e => e.Key + " " + e.Value)));
            var d = normalized.Data;

            var card = existing.FirstOrDefault(x => string.Equals(x.Term, d.Term, StringComparison.OrdinalIgnoreCase));
            if (card == null)
            {
                created++;
                if (!write) continue;
                card = new Card { SetId = set.SetId, CreatedAt = DateTime.UtcNow };
                Apply(card, d);
                db.Cards.Add(card);
                await db.SaveChangesAsync(ct); // keep CardId order equal to the manifest order
                existing.Add(card);
            }
            else if (Differs(card, d))
            {
                updated++;
                if (write) { Apply(card, d); card.UpdatedAt = DateTime.UtcNow; }
            }
            else unchanged++;
        }

        var extra = existing.Where(x => !wanted.Contains(x.Term)).Select(x => x.Term).ToList();
        if (write)
        {
            await db.SaveChangesAsync(ct);
            await tx!.CommitAsync(ct);
        }
        return new SetReport(manifest.Set.Code, manifest.Folder, manifest.Set.Name, created, updated, unchanged, images, extra);
    }

    private static CardInputDto ToInput(ManifestCard c, string topic) => new()
    {
        Term = c.Term, Definition = c.Definition, Phonetic = c.IpaUs, PhoneticUk = c.IpaUk, MeaningEn = c.MeaningEn,
        PartsOfSpeech = c.PartsOfSpeech, Topic = topic, Cefr = c.Cefr,
        Examples = c.Examples.Select(e => new CardExample { Text = e.Text, Vi = e.Vi }).ToList(),
        Collocations = c.Collocations?.Select(e => new CardCollocation { Text = e.Text, Vi = e.Vi }).ToList(),
        WordFamily = c.WordFamily?.Select(w => new CardWordFamilyItem { Text = w.Text, Pos = w.Pos, Vi = w.Vi }).ToList(),
        Synonyms = c.Synonyms, Antonyms = c.Antonyms, Plural = c.Plural,
        VerbForms = c.VerbForms == null ? null : new CardVerbForms
        { Past = c.VerbForms.Past, PastParticiple = c.VerbForms.PastParticiple, Ing = c.VerbForms.Ing, ThirdPerson = c.VerbForms.ThirdPerson },
    };

    private static void Apply(Card card, CardData d)
    {
        card.Term = d.Term; card.Definition = d.Definition; card.Phonetic = d.Phonetic; card.PhoneticUk = d.PhoneticUk;
        card.MeaningEn = d.MeaningEn; card.PartsOfSpeech = d.PartsOfSpeech; card.Topic = d.Topic; card.Cefr = d.Cefr;
        card.ImageUrl = d.ImageUrl; card.ImageSourceJson = d.ImageSourceJson; card.Example = d.Example;
        card.ExampleTranslation = d.ExampleTranslation; card.DetailsJson = d.DetailsJson; card.Emoji = d.Emoji; card.AiGenerated = false;
    }

    private static bool Differs(Card c, CardData d)
        => c.Term != d.Term || c.Definition != d.Definition || c.Phonetic != d.Phonetic || c.PhoneticUk != d.PhoneticUk
           || c.MeaningEn != d.MeaningEn || c.PartsOfSpeech != d.PartsOfSpeech || c.Topic != d.Topic || c.Cefr != d.Cefr
           || c.ImageUrl != d.ImageUrl || c.ImageSourceJson != d.ImageSourceJson || c.Example != d.Example
           || c.ExampleTranslation != d.ExampleTranslation || c.DetailsJson != d.DetailsJson;
}
