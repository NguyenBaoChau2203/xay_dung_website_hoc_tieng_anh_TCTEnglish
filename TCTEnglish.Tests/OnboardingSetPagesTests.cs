using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.Onboarding;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>S6: "Bộ thẻ của tôi" and the deck detail page after the onboarding.</summary>
public sealed class OnboardingSetPagesTests
{
    private const int NewUser = 970;
    private const int PlainUser = 971;

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync(CountingDbCommandInterceptor? counter = null)
    {
        var factory = new TestWebApplicationFactory();
        if (counter != null) { factory.SaveInterceptors.Add(counter); }
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var systemId = await db.Users.Where(u => u.Email == "system@tct.local").Select(u => u.UserId).SingleAsync();
        foreach (var id in new[] { NewUser, PlainUser })
        {
            db.Users.Add(new User
            {
                UserId = id, Email = $"pages{id}@test.local", PasswordHash = "hash", FullName = "Pages " + id,
                Role = Roles.Standard, Status = UserStatus.Online, CreatedAt = DateTime.UtcNow
            });
        }

        Seed(db, systemId, "Phương tiện giao thông", "veh", 30);
        Seed(db, systemId, "Địa điểm trong thành phố", "place", 30);
        Seed(db, systemId, "Hỏi và chỉ đường", "phrase", 20);
        await db.SaveChangesAsync();
        return factory;
    }

    private static void Seed(DbflashcardContext db, int systemId, string name, string prefix, int count)
    {
        var set = new Set { OwnerId = systemId, SetName = name, Visibility = "Public", CreatedAt = DateTime.UtcNow };
        for (var i = 1; i <= count; i++)
        {
            set.Cards.Add(new Card
            {
                Term = $"{prefix}{i:D2}", Definition = $"nghĩa {prefix}{i:D2}", Cefr = i % 2 == 0 ? "A2" : "A1",
                ImageUrl = $"/uploads/shared/{prefix}{i:D2}.webp", Topic = name
            });
        }

        db.Sets.Add(set);
    }

    private static async Task<CompleteResult> CompleteAsync(TestWebApplicationFactory factory, int userId)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOnboardingService>();
        var choices = new OnboardingChoices(new[] { "travel", "communication" }, new[] { "A16", "A15", "C04" }, "A2", 10);
        var result = await service.CompleteAsync(userId, new CompleteRequest(Guid.NewGuid(), choices, Array.Empty<string>(), Array.Empty<int>()));
        Assert.True(result.IsSuccess, result.Error);
        return result.Value!;
    }

    private static async Task<string> PageAsync(TestWebApplicationFactory factory, int userId, string route)
    {
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, Roles.Standard);
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Index_AfterTheOnboarding_ShowsTheBanner_TheButton_TheFolderChip_AndTheStarterTiles()
    {
        await using var factory = await CreateFactoryAsync();
        var created = await CompleteAsync(factory, NewUser);

        var html = WebUtility.HtmlDecode(await PageAsync(factory, NewUser, "/Sets"));

        Assert.Contains("2 bộ thẻ mới được tạo theo sở thích của bạn", html);
        Assert.Contains("Du lịch · Giao tiếp · A2 — sửa, xóa hay thêm từ thoải mái", html);
        Assert.Contains("href=\"/Sets#tao-them-tu-so-thich\"", html);
        Assert.Contains("Đổi sở thích", html);
        Assert.Contains("data-tob-banner-dismiss", html);
        Assert.Contains("Tạo thêm từ sở thích", html); // the header button, always there

        // the folder: route icon and the "Mới" chip
        var folder = Regex.Match(html, "<article class=\"tct-card tct-card--interactive k-card ts-folder[^>]*>.*?</article>", RegexOptions.Singleline).Value;
        Assert.Contains("fa-route", folder);
        Assert.Contains(">Mới<", folder);
        Assert.Contains("Lộ trình của tôi", folder);

        // the decks: chip and a 2x2 mosaic of their first four images
        foreach (var deck in created.Decks)
        {
            var tile = Regex.Match(html, $"<article class=\"tct-card tct-card--interactive k-card ts-tile\">\\s*<a class=\"ts-tile-link\" href=\"/Sets/{deck.SetId}\".*?</article>", RegexOptions.Singleline).Value;
            Assert.False(string.IsNullOrEmpty(tile), $"No tile for set {deck.SetId}.");
            Assert.Contains("Từ sở thích", tile);
            Assert.Contains("ts-tile-mosaic", tile);
            Assert.Equal(4, Regex.Matches(tile, "<img ").Count);
        }
    }

    [Fact]
    public async Task Banner_IsOneTime_AndNotShownToAUserWithoutAnOnboarding()
    {
        await using var factory = await CreateFactoryAsync();
        await CompleteAsync(factory, NewUser);

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOnboardingService>().DismissBannerAsync(NewUser);
        }

        var after = await PageAsync(factory, NewUser, "/Sets");
        Assert.DoesNotContain("data-tob-banner", after);
        Assert.Contains("Từ sở thích", WebUtility.HtmlDecode(after)); // the chips stay
        Assert.Contains("Tạo thêm từ sở thích", WebUtility.HtmlDecode(after));

        var plain = WebUtility.HtmlDecode(await PageAsync(factory, PlainUser, "/Sets"));
        Assert.DoesNotContain("data-tob-banner", plain);
        Assert.DoesNotContain("ts-tile-starter", plain);
        Assert.Contains("Tạo thêm từ sở thích", plain); // always visible for signed-in users
    }

    [Fact]
    public async Task EveryPersonalSetWithImages_GetsAMosaic_OrOnePhoto_OrKeepsTheArt()
    {
        await using var factory = await CreateFactoryAsync();
        int four, two, none, unsafeOnly;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Set Make(string name, params string?[] images)
            {
                var set = new Set { OwnerId = PlainUser, SetName = name, Visibility = "Private", CreatedAt = DateTime.UtcNow };
                for (var i = 0; i < images.Length; i++)
                {
                    set.Cards.Add(new Card { Term = name + i, Definition = "x", ImageUrl = images[i] });
                }

                db.Sets.Add(set);
                return set;
            }

            var a = Make("Four", "/img/a1.webp", "/img/a2.webp", "/img/a3.webp", "/img/a4.webp", "/img/a5.webp");
            var b = Make("Two", "/img/b1.webp", null, "/img/b2.webp");
            var c = Make("None", null, "");
            var d = Make("Unsafe", "javascript:alert(1)", "http://evil.example.test/x.png", "//evil.example.test/y.png");
            await db.SaveChangesAsync();
            (four, two, none, unsafeOnly) = (a.SetId, b.SetId, c.SetId, d.SetId);
        }

        var html = await PageAsync(factory, PlainUser, "/Sets");
        string Tile(int id) => Regex.Match(html, $"<article class=\"tct-card tct-card--interactive k-card ts-tile\">\\s*<a class=\"ts-tile-link\" href=\"/Sets/{id}\".*?</article>", RegexOptions.Singleline).Value;

        var fourTile = Tile(four);
        Assert.Contains("ts-tile-mosaic", fourTile);
        Assert.Equal(4, Regex.Matches(fourTile, "<img ").Count); // only the first four
        Assert.DoesNotContain("/img/a5.webp", fourTile);

        var twoTile = Tile(two);
        Assert.Contains("ts-tile-photo", twoTile);
        Assert.DoesNotContain("ts-tile-mosaic", twoTile);
        Assert.Contains("/img/b1.webp", twoTile);

        Assert.Contains("ts-tile-term", Tile(none));
        Assert.DoesNotContain("<img ", Tile(none));

        var unsafeTile = Tile(unsafeOnly);
        Assert.DoesNotContain("<img ", unsafeTile);
        Assert.DoesNotContain("evil.example.test", html);
        Assert.DoesNotContain("javascript:alert", html);
    }

    [Fact]
    public async Task TheFolderPage_ShowsTheSameMosaicAndChip()
    {
        await using var factory = await CreateFactoryAsync();
        var created = await CompleteAsync(factory, NewUser);

        var html = WebUtility.HtmlDecode(await PageAsync(factory, NewUser, $"/Sets/Folders/{created.FolderId}"));
        Assert.Contains("ts-tile-mosaic", html);
        Assert.Contains("Từ sở thích", html);
    }

    [Fact]
    public async Task Index_QueryCountDoesNotGrowWithTheNumberOfSets()
    {
        var counter = new CountingDbCommandInterceptor();
        await using var factory = await CreateFactoryAsync(counter);
        await CompleteAsync(factory, NewUser);

        async Task<int> Selects()
        {
            await PageAsync(factory, NewUser, "/Sets"); // warm-up
            counter.Reset();
            await PageAsync(factory, NewUser, "/Sets");
            return counter.Counts.GetValueOrDefault("SELECT");
        }

        var few = await Selects();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            for (var i = 0; i < 50; i++)
            {
                var set = new Set { OwnerId = NewUser, SetName = "Extra " + i, Visibility = "Private", CreatedAt = DateTime.UtcNow };
                for (var k = 0; k < 5; k++) { set.Cards.Add(new Card { Term = $"x{i}-{k}", Definition = "d", ImageUrl = $"/img/{i}-{k}.webp" }); }
                db.Sets.Add(set);
            }

            await db.SaveChangesAsync();
        }

        var many = await Selects();
        Assert.Equal(few, many);
    }

    [Fact]
    public async Task Detail_OfAStarterDeck_ShowsTheBadge_TheSource_AndTheRefillCard()
    {
        await using var factory = await CreateFactoryAsync();
        var created = await CompleteAsync(factory, NewUser);
        var setId = created.Decks[0].SetId;

        var html = WebUtility.HtmlDecode(await PageAsync(factory, NewUser, $"/Sets/{setId}"));
        Assert.Contains("ts-starter-badge", html);
        Assert.Contains("Từ sở thích", html);
        Assert.Contains("Nguồn: ", html); // from the description made at creation
        Assert.DoesNotContain("ts-hero-source", html); // so the extra source line would only repeat it
        Assert.Contains("Gợi ý thêm cùng chủ đề", html);
        Assert.Contains("Thêm 10 từ tiếp theo về ", html);
        Assert.Contains("data-tob-refill", html);
        Assert.Contains($"data-set-id=\"{setId}\"", html);
        Assert.Equal(4, Regex.Matches(html, "class=\"ts-refill__img\"").Count);
        Assert.Contains("/js/onboarding-refill.js", html);
        Assert.Contains("Học bộ này", html); // everything else on the page is unchanged: the study modes are still there
        Assert.Contains("/Practice/learn?set=" + setId, html);
    }

    [Fact]
    public async Task Detail_WhenTheDescriptionWasEdited_ShowsTheSourceLine()
    {
        await using var factory = await CreateFactoryAsync();
        var created = await CompleteAsync(factory, NewUser);
        var setId = created.Decks[0].SetId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            (await db.Sets.SingleAsync(s => s.SetId == setId)).Description = "Bộ của riêng tôi";
            await db.SaveChangesAsync();
        }

        var html = WebUtility.HtmlDecode(await PageAsync(factory, NewUser, $"/Sets/{setId}"));
        Assert.Contains("ts-hero-source", html);
        Assert.Contains("(thư viện TCT)", html);
        Assert.Contains("Nguồn: Phương tiện giao thông", html);
    }

    [Fact]
    public async Task Detail_OfAPlainOwnSet_HasNoBadgeAndNoRefillCard()
    {
        await using var factory = await CreateFactoryAsync();
        int setId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var set = new Set { OwnerId = PlainUser, SetName = "Plain deck", Visibility = "Private", CreatedAt = DateTime.UtcNow };
            set.Cards.Add(new Card { Term = "plain", Definition = "thường" });
            db.Sets.Add(set);
            await db.SaveChangesAsync();
            setId = set.SetId;
        }

        var html = WebUtility.HtmlDecode(await PageAsync(factory, PlainUser, $"/Sets/{setId}"));
        Assert.DoesNotContain("ts-starter-badge", html);
        Assert.DoesNotContain("Nguồn: ", html);
        Assert.DoesNotContain("data-tob-refill", html);
        Assert.DoesNotContain("Gợi ý thêm cùng chủ đề", html);
    }

    [Fact]
    public async Task Detail_WhenTheTopicsAreUsedUp_SaysSoInsteadOfOfferingTheButton()
    {
        await using var factory = await CreateFactoryAsync();
        var created = await CompleteAsync(factory, NewUser);
        var setId = created.Decks[0].SetId;

        // the user already owns every remaining word of the deck's topics
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var mine = await db.Cards.Where(c => c.Set.OwnerId == NewUser).Select(c => c.Term.ToLower()).ToListAsync();
            var rest = await db.Cards.AsNoTracking()
                .Where(c => c.Set.Owner.Email == "system@tct.local" && c.Topic != null)
                .Select(c => new { c.Term, c.Definition }).ToListAsync();
            var filler = new Set { OwnerId = NewUser, SetName = "Everything else", Visibility = "Private", CreatedAt = DateTime.UtcNow };
            foreach (var card in rest.Where(c => !mine.Contains(c.Term.ToLower())))
            {
                filler.Cards.Add(new Card { Term = card.Term, Definition = card.Definition });
            }

            db.Sets.Add(filler);
            await db.SaveChangesAsync();
        }

        var html = WebUtility.HtmlDecode(await PageAsync(factory, NewUser, $"/Sets/{setId}"));
        Assert.Contains("Bạn đã có tất cả từ của chủ đề này 🎉", html);
        Assert.DoesNotContain("data-tob-refill>", html);
        Assert.Contains("Gợi ý thêm cùng chủ đề", html);
    }

    [Fact]
    public async Task Detail_OfSomeoneElsesStarterDeck_IsNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        var created = await CompleteAsync(factory, NewUser);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, PlainUser, Roles.Standard);
        using var response = await client.GetAsync($"/Sets/{created.Decks[0].SetId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // private: no badge, no card, no page
    }
}
