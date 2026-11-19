using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.Services.Vocabulary;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Course covers on the Vocabulary pages: the photo comes from the course's own reviewed card images,
/// never from another topic, and the illustration fallback is neutral.
/// </summary>
public sealed class VocabularyCoverCatalogTests
{
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

    private static List<(string Name, HashSet<string> TermsWithImage)> Manifests()
    {
        var dir = Path.Combine(RepoRoot(), "data", "content", "vocabulary");
        var list = new List<(string, HashSet<string>)>();
        foreach (var file in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (Path.GetFileName(file) == "covers.json") continue;
            var root = JsonDocument.Parse(File.ReadAllText(file)).RootElement;
            var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in root.GetProperty("cards").EnumerateArray())
            {
                if (card.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.Object)
                {
                    terms.Add(card.GetProperty("term").GetString()!.Trim());
                }
            }

            list.Add((root.GetProperty("set").GetProperty("name").GetString()!.Trim(), terms));
        }

        return list;
    }

    [Fact]
    public void EveryNewSystemSetHasACoverWord_ThatIsOneOfItsCardsWithAnImage()
    {
        var manifests = Manifests();
        Assert.Equal(48, manifests.Count);

        foreach (var (name, termsWithImage) in manifests)
        {
            Assert.True(VocabularyCoverCatalog.Terms.TryGetValue(name, out var term), $"No cover word for '{name}'.");
            Assert.False(string.IsNullOrWhiteSpace(term), $"'{name}' must have a real cover word.");
            Assert.True(termsWithImage.Contains(term!.Trim()), $"The cover word '{term}' of '{name}' is not a card of that set with an image.");
        }
    }

    [Fact]
    public void CoversJson_AgreesWithTheCatalogInCode()
    {
        var path = Path.Combine(RepoRoot(), "data", "content", "vocabulary", "covers.json");
        var rows = JsonDocument.Parse(File.ReadAllText(path)).RootElement.EnumerateArray().ToList();

        var fromJson = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var name = row.GetProperty("setName").GetString()!;
            var term = row.GetProperty("coverTerm");
            Assert.True(fromJson.TryAdd(name, term.ValueKind == JsonValueKind.Null ? null : term.GetString()), $"Duplicate cover row '{name}'.");
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("reason").GetString()), $"'{name}' has no reason.");
        }

        Assert.Equal(
            VocabularyCoverCatalog.Terms.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList(),
            fromJson.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList());
    }

    private static CoverCandidate Card(int id, string term, string url = "/images/x.webp") => new(id, term, url);

    [Fact]
    public void Pick_UsesTheCuratedCoverWord_IgnoringCaseAndEdgeSpaces()
    {
        var cards = new[] { Card(1, "lion", "/a.webp"), Card(2, " Tiger ", "/b.webp"), Card(3, "zebra", "/c.webp") };
        var choice = VocabularyCoverCatalog.Pick(" Động vật hoang dã ", cards);

        Assert.NotNull(choice);
        Assert.Equal("/b.webp", choice!.ImageUrl);
        Assert.Equal("Tiger", choice.Term);
    }

    [Fact]
    public void Pick_FallsBackToTheFirstImageByCardId_WhenTheCoverWordHasNoImage()
    {
        var cards = new[] { Card(9, "zebra", "/z.webp"), Card(4, "lion", "/l.webp") };
        Assert.Equal("/l.webp", VocabularyCoverCatalog.Pick("Động vật hoang dã", cards)!.ImageUrl);
    }

    [Fact]
    public void Pick_UnlistedCourse_UsesItsFirstCardWithAnImage()
    {
        var cards = new[] { Card(30, "c", "/c.webp"), Card(10, "a", "/a.webp"), Card(20, "b", "/b.webp") };
        Assert.Equal("/a.webp", VocabularyCoverCatalog.Pick("A personal course", cards)!.ImageUrl);
    }

    [Fact]
    public void Pick_ListedWithoutAWord_ShowsTheIllustration()
    {
        // "3000 từ vựng Oxford A1" mixes many topics: a single photo would be a different topic for most words.
        Assert.True(VocabularyCoverCatalog.Terms.ContainsKey("3000 từ vựng Oxford A1"));
        Assert.Null(VocabularyCoverCatalog.Pick("3000 từ vựng Oxford A1", new[] { Card(1, "Family") }));
    }

    [Fact]
    public void Pick_NoImages_ReturnsNull()
    {
        Assert.Null(VocabularyCoverCatalog.Pick("Động vật hoang dã", Array.Empty<CoverCandidate>()));
        Assert.Null(VocabularyCoverCatalog.Pick(null, Array.Empty<CoverCandidate>()));
    }
}

public sealed class VocabularyCoverServiceTests
{
    private const int FolderId = 7100;
    private const int WildSetId = 7101;
    private const int UnlistedSetId = 7102;
    private const int NoImageSetId = 7103;
    private const int GenericSetId = 7104;
    private const int ManyFirstId = 7200;

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync(CountingDbCommandInterceptor? counter = null)
    {
        var factory = new TestWebApplicationFactory();
        if (counter != null)
        {
            factory.SaveInterceptors.Add(counter);
        }

        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var sys = await context.Users.Where(user => user.Email == "system@tct.local").Select(user => user.UserId).SingleAsync();

        context.Folders.Add(new Folder { FolderId = FolderId, UserId = sys, FolderName = "Cover folder" });
        await context.SaveChangesAsync();

        context.Sets.AddRange(
            NewSet(WildSetId, sys, "Động vật hoang dã"),
            NewSet(UnlistedSetId, sys, "Unlisted course"),
            NewSet(NoImageSetId, sys, "Rau củ"),
            NewSet(GenericSetId, sys, "3000 từ vựng Oxford A1"));
        await context.SaveChangesAsync();

        context.Cards.AddRange(
            NewCard(7301, WildSetId, "lion", "/images/cards/lion.webp"),
            NewCard(7302, WildSetId, "Tiger", "/images/cards/tiger.webp"),
            NewCard(7311, UnlistedSetId, "first", null),
            NewCard(7312, UnlistedSetId, "insecure", "http://cdn.example.test/a.png"),
            NewCard(7313, UnlistedSetId, "protocol", "//evil.example.test/a.png"),
            NewCard(7314, UnlistedSetId, "script", "javascript:alert(1)"),
            NewCard(7315, UnlistedSetId, "good", "/images/cards/good.webp"),
            NewCard(7316, UnlistedSetId, "later", "https://cdn.example.test/later.png"),
            NewCard(7321, NoImageSetId, "carrot", null),
            NewCard(7331, GenericSetId, "Family", "/images/cards/family.webp"));
        await context.SaveChangesAsync();
        return factory;
    }

    private static Set NewSet(int id, int owner, string name) =>
        new() { SetId = id, OwnerId = owner, FolderId = FolderId, SetName = name, Description = "#a1", CreatedAt = DateTime.UtcNow };

    private static Card NewCard(int id, int setId, string term, string? image) => new()
    {
        CardId = id,
        SetId = setId,
        Term = term,
        Definition = "nghia " + id,
        ImageUrl = image,
        Topic = "Topic"
    };

    private static async Task<TCTVocabulary.ViewModels.VocabularyHomeViewModel> HomeAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStudyService>();
        var model = await service.GetVocabularyHomeAsync(TestDataIds.UserId);
        Assert.NotNull(model);
        return model!;
    }

    private static TCTVocabulary.ViewModels.VocabularyCourseCardViewModel Course(
        TCTVocabulary.ViewModels.VocabularyHomeViewModel home, int setId) =>
        home.Collections.SelectMany(collection => collection.Courses).Single(course => course.SetId == setId);

    [Fact]
    public async Task Home_CoverIsTheCuratedCardImage_OrTheFirstSafeImage_OrNothing()
    {
        await using var factory = await CreateFactoryAsync();
        var home = await HomeAsync(factory);

        var wild = Course(home, WildSetId);
        Assert.Equal("/images/cards/tiger.webp", wild.CoverImageUrl);
        Assert.Equal("Tiger", wild.CoverAlt);

        // not listed: the first card by id with a SAFE image (insecure, protocol-relative and javascript: urls are skipped)
        var unlisted = Course(home, UnlistedSetId);
        Assert.Equal("/images/cards/good.webp", unlisted.CoverImageUrl);

        Assert.Null(Course(home, NoImageSetId).CoverImageUrl);
        Assert.Null(Course(home, GenericSetId).CoverImageUrl);
    }

    [Fact]
    public async Task Home_IllustrationFallbackIsNeutral_NotAThemedKeyFromTheSetId()
    {
        await using var factory = await CreateFactoryAsync();
        var home = await HomeAsync(factory);

        // "Rau củ" has no images: its art is decided by name ("rau" = food), not by id; the unlisted course is neutral.
        Assert.Equal("food", Course(home, NoImageSetId).ArtKey);
        Assert.Equal("star", Course(home, UnlistedSetId).ArtKey);
        Assert.Equal("star", Course(home, WildSetId).ArtKey);
    }

    [Fact]
    public async Task Detail_UsesTheSameCoverRule()
    {
        await using var factory = await CreateFactoryAsync();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStudyService>();

        var wild = await service.GetVocabularyCourseAsync(WildSetId, TestDataIds.UserId);
        Assert.Equal("/images/cards/tiger.webp", wild!.Course.CoverImageUrl);

        var unlisted = await service.GetVocabularyCourseAsync(UnlistedSetId, TestDataIds.UserId);
        Assert.Equal("/images/cards/good.webp", unlisted!.Course.CoverImageUrl);

        var none = await service.GetVocabularyCourseAsync(NoImageSetId, TestDataIds.UserId);
        Assert.Null(none!.Course.CoverImageUrl);
    }

    [Fact]
    public async Task Home_CoverQueriesDoNotGrowWithTheNumberOfCourses()
    {
        var counter = new CountingDbCommandInterceptor();
        await using var factory = await CreateFactoryAsync(counter);

        await HomeAsync(factory); // warm-up (caches such as the system user id)
        counter.Reset();
        await HomeAsync(factory);
        var few = counter.Counts.GetValueOrDefault("SELECT");

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var sys = await context.Users.Where(user => user.Email == "system@tct.local").Select(user => user.UserId).SingleAsync();
            for (var i = 0; i < 50; i++)
            {
                context.Sets.Add(NewSet(ManyFirstId + i, sys, "Many " + i));
                context.Cards.Add(NewCard(ManyFirstId * 10 + i, ManyFirstId + i, "word" + i, "/images/cards/w" + i + ".webp"));
            }

            await context.SaveChangesAsync();
        }

        counter.Reset();
        var home = await HomeAsync(factory);
        var many = counter.Counts.GetValueOrDefault("SELECT");

        Assert.True(few > 0);
        Assert.Equal(few, many);
        Assert.Equal("/images/cards/w7.webp", Course(home, ManyFirstId + 7).CoverImageUrl);
    }
}
