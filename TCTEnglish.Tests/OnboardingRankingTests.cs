using System.Text.Json;
using TCTEnglish.Services.Onboarding;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>S2: the topic catalogue and the deterministic deck ranking.</summary>
public sealed class OnboardingCatalogTests
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

    [Fact]
    public void Catalogue_HasThe48Topics_MatchingTheSystemSetsByCodeAndExactName()
    {
        var root = Path.Combine(RepoRoot(), "data", "content", "vocabulary");
        var bySet = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
        {
            var set = JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("set");
            bySet[set.GetProperty("code").GetString()!] = set.GetProperty("name").GetString()!.Trim();
        }

        Assert.Equal(48, OnboardingCatalog.Topics.Count);
        Assert.Equal(48, OnboardingCatalog.Topics.Select(t => t.Key).Distinct().Count());
        foreach (var topic in OnboardingCatalog.Topics)
        {
            Assert.True(bySet.TryGetValue(topic.Key, out var name), $"No system set with code {topic.Key}.");
            Assert.Equal(name, topic.SetName);
        }
    }

    [Fact]
    public void EveryGoal_HasAtLeastFourTopics_AndEveryMappedTopicExists()
    {
        Assert.Equal(6, OnboardingCatalog.Goals.Count);
        foreach (var goal in OnboardingCatalog.Goals)
        {
            var topics = OnboardingCatalog.TopicsOfGoal(goal.Key);
            Assert.True(topics.Count >= 4, $"Goal {goal.Key} has {topics.Count} topics.");
            foreach (var key in topics)
            {
                Assert.True(OnboardingCatalog.TryGetTopic(key, out _), $"Goal {goal.Key} maps to unknown topic {key}.");
            }
        }
    }

    [Theory]
    [InlineData("A16", "travel")]
    [InlineData("C03", "communication")]
    [InlineData("B01", "work")]
    [InlineData("A19", "hobbies")]
    public void TopicsKnowTheirGoals(string topic, string goal) =>
        Assert.Contains(goal, OnboardingCatalog.GoalsOfTopic(topic));

    [Fact]
    public void Bands_FollowTheFolder()
    {
        Assert.Equal("A1–A2", OnboardingCatalog.Topic("A03").Band);
        Assert.Equal("B1–B2", OnboardingCatalog.Topic("B04").Band);
        Assert.Equal("Giao tiếp", OnboardingCatalog.Topic("C01").Band);
        Assert.True(OnboardingCatalog.Topic("C01").IsPhrase);
        Assert.False(OnboardingCatalog.Topic("A03").IsPhrase);
    }

    [Theory]
    [InlineData("A1", "A1", true)]
    [InlineData("A1", "A2", true)]
    [InlineData("A1", "B1", false)]
    [InlineData("A2", "A1", true)]
    [InlineData("A2", "B1", true)]
    [InlineData("A2", "B2", false)]
    [InlineData("B1", "A1", false)]
    [InlineData("B1", "A2", true)]
    [InlineData("B1", "B2", true)]
    [InlineData("B2", "A2", false)]
    [InlineData("B2", "B1", true)]
    [InlineData("B2", "B2", true)]
    [InlineData("A2", null, true)]
    public void CefrWindow_PerLevel(string level, string? cefr, bool expected) =>
        Assert.Equal(expected, OnboardingDeckBuilder.InWindow(level, cefr));

    [Theory]
    [InlineData(25, 5, 5)]
    [InlineData(26, 5, 6)]
    [InlineData(100, 10, 10)]
    [InlineData(0, 10, 0)]
    [InlineData(73, 15, 5)]
    public void EstimateDays_IsCeilingOfWordsOverPace(int words, int pace, int days) =>
        Assert.Equal(days, OnboardingCatalog.EstimateDays(words, pace));

    [Fact]
    public void DefaultLevel_IsA1ForBeginners_OtherwiseA2()
    {
        Assert.Equal("A1", OnboardingCatalog.DefaultLevel(new[] { "beginner", "travel" }));
        Assert.Equal("A2", OnboardingCatalog.DefaultLevel(new[] { "travel" }));
    }
}

public sealed class OnboardingRankingTests
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    private static int _nextId = 1;

    private static OnboardingCard Card(string topic, string term, string? cefr = "A1", bool image = true, int? id = null) =>
        new(id ?? _nextId++, topic, term, "nghĩa " + term, image ? "/images/cards/" + term + ".webp" : null, cefr);

    private static List<OnboardingCard> Pool(string topic, int count, string cefr = "A1", string? prefix = null, int firstId = 0)
    {
        var list = new List<OnboardingCard>();
        for (var i = 0; i < count; i++)
        {
            list.Add(new OnboardingCard(firstId + i + 1, topic, $"{prefix ?? topic.ToLowerInvariant()}{i + 1:D2}", "nghĩa", "/images/cards/x.webp", cefr));
        }

        return list;
    }

    private static IReadOnlyList<StarterDeck> Build(
        string[] goals, string[] topics, string level, IReadOnlyList<OnboardingCard> pool,
        int size = 25, IReadOnlySet<string>? owned = null, IReadOnlySet<string>? known = null, IReadOnlyCollection<string>? names = null) =>
        OnboardingDeckBuilder.Build(
            new DeckBuildRequest(goals, topics, level, 10, size), pool, owned ?? None, known ?? None, names ?? Array.Empty<string>());

    [Fact]
    public void SameInput_GivesTheSameDecks_InTheSameOrder()
    {
        var pool = Pool("A16", 30).Concat(Pool("C04", 20, firstId: 100)).ToList();
        var first = Build(new[] { "travel" }, new[] { "A16", "C04", "A15" }, "A1", pool);
        var second = Build(new[] { "travel" }, new[] { "A16", "C04", "A15" }, "A1", pool);

        Assert.Equal(
            first.SelectMany(d => d.Cards.Select(c => c.CardId)).ToList(),
            second.SelectMany(d => d.Cards.Select(c => c.CardId)).ToList());
        Assert.Equal(first.Select(d => d.Name), second.Select(d => d.Name));
    }

    [Fact]
    public void LevelDistance_Decides_BeforeCardOrder()
    {
        // A2 learner, deck of 10: all six A2 cards (distance 0) come first, then the four lowest ids of the distance-1 cards.
        var pool = Pool("A16", 6, "A1", prefix: "a1-", firstId: 0)          // ids 1-6
            .Concat(Pool("A16", 6, "B1", prefix: "b1-", firstId: 10))       // ids 11-16
            .Concat(Pool("A16", 6, "A2", prefix: "a2-", firstId: 20))       // ids 21-26
            .ToList();

        var deck = Assert.Single(Build(new[] { "travel" }, new[] { "A16" }, "A2", pool, size: 10));
        Assert.Equal(10, deck.Cards.Count);
        Assert.Equal(6, deck.Cards.Count(c => c.Cefr == "A2"));
        Assert.Equal(new[] { 1, 2, 3, 4 }, deck.Cards.Where(c => c.Cefr == "A1").Select(c => c.CardId).OrderBy(id => id));
        Assert.DoesNotContain(deck.Cards, c => c.Cefr == "B1"); // the tie between A1 and B1 goes to the lower card ids
    }

    [Fact]
    public void Deck_IsListedFromEasyToHard()
    {
        var pool = new List<OnboardingCard>
        {
            Card("B04", "hard", "B2", id: 1),
            Card("B04", "mid", "B1", id: 2),
            Card("B04", "easy", "A2", id: 3),
        };
        var deck = Assert.Single(Build(new[] { "travel" }, new[] { "B04" }, "B1", pool, size: 3));
        Assert.Equal(new[] { "easy", "mid", "hard" }, deck.Cards.Select(c => c.Term));
    }

    [Fact]
    public void Topics_AreMixedRoundRobin()
    {
        var pool = Pool("A16", 10).Concat(Pool("A15", 10, firstId: 100)).ToList();
        var deck = Assert.Single(Build(new[] { "travel" }, new[] { "A16", "A15" }, "A1", pool, size: 10));
        Assert.Equal(
            new[] { "A16", "A15", "A16", "A15", "A16", "A15", "A16", "A15", "A16", "A15" },
            deck.Cards.Select(c => c.TopicKey));
    }

    [Fact]
    public void CardsWithoutAnImage_GoLast_WithinTheSameDistance()
    {
        // The card without an image has the lowest id but is only used when the cards with images run out.
        var pool = new List<OnboardingCard> { Card("A16", "no-image", "A1", image: false, id: 1) };
        pool.AddRange(Pool("A16", 10, firstId: 100));

        var deck = Assert.Single(Build(new[] { "travel" }, new[] { "A16" }, "A1", pool, size: 10));
        Assert.Equal(10, deck.Cards.Count);
        Assert.DoesNotContain(deck.Cards, c => c.Term == "no-image");

        var small = Assert.Single(Build(new[] { "travel" }, new[] { "A16" }, "A1", pool.Take(10).ToList(), size: 10));
        Assert.Contains(small.Cards, c => c.Term == "no-image"); // used when needed to reach the size
    }

    [Fact]
    public void OwnedAndKnownTerms_AreExcluded_IgnoringCaseAndSpaces_AndTermsAreNeverRepeated()
    {
        var pool = new List<OnboardingCard>
        {
            Card("A16", "Bus", id: 1),
            Card("A16", "train", id: 2),
            Card("A15", "BUS", id: 3),       // same lemma as card 1 in another topic
            Card("A15", " Taxi ", id: 4),
            Card("A15", "plane", id: 5),
        };
        var owned = new HashSet<string> { "train" };
        var known = new HashSet<string> { "taxi" };

        var deck = Assert.Single(Build(new[] { "travel" }, new[] { "A16", "A15" }, "A1", pool, owned: owned, known: known));
        var terms = deck.Cards.Select(c => c.Term.Trim().ToLowerInvariant()).ToList();

        Assert.DoesNotContain("train", terms);
        Assert.DoesNotContain("taxi", terms);
        Assert.Equal(terms.Distinct().Count(), terms.Count);
        Assert.Contains("bus", terms);
        Assert.Contains("plane", terms);
    }

    [Fact]
    public void AGoalGroupSmallerThanTen_MergesIntoTheNearestDeck()
    {
        // travel has plenty of cards, work only 4: the work deck is merged into the travel deck.
        var pool = Pool("A16", 30).Concat(Pool("B01", 4, "B1", firstId: 100)).ToList();
        var decks = Build(new[] { "travel", "work" }, new[] { "A16", "B01" }, "A2", pool);

        var deck = Assert.Single(decks);
        Assert.Equal("Du lịch – bộ khởi đầu", deck.Name);
        Assert.Contains("B01", deck.TopicKeys);
        Assert.Contains(deck.Cards, c => c.TopicKey == "B01");
    }

    [Fact]
    public void MoreThanFourDecks_MergeTheSmallestIntoANeighbour()
    {
        // 3 goals + phrases + "no goal" topic would be 5 groups.
        var pool = Pool("A16", 30)                       // travel
            .Concat(Pool("B01", 30, "B1", firstId: 100)) // work
            .Concat(Pool("A19", 30, firstId: 200))       // hobbies
            .Concat(Pool("C01", 20, firstId: 300))       // phrases (communication/travel goals present)
            .Concat(Pool("A07", 12, firstId: 400))       // tagged for beginner only -> "Chủ đề bạn chọn"
            .ToList();
        var decks = Build(new[] { "travel", "work", "hobbies" }, new[] { "A16", "B01", "A19", "C01", "A07" }, "A2", pool);

        Assert.True(decks.Count <= OnboardingDeckBuilder.MaxDecks);
        Assert.Equal(OnboardingDeckBuilder.MaxDecks, decks.Count);
        Assert.True(decks.Sum(d => d.Cards.Count) <= OnboardingDeckBuilder.MaxTotalCards);
    }

    [Fact]
    public void PhraseTopics_GoToTheirOwnDeck_NamedByTheGoal()
    {
        var pool = Pool("B04", 25, "B1").Concat(Pool("C04", 20, firstId: 100)).ToList();

        var travel = Build(new[] { "travel" }, new[] { "B04", "C04" }, "B1", pool);
        Assert.Equal(new[] { "Du lịch – bộ khởi đầu", "Câu giao tiếp khi đi du lịch" }, travel.Select(d => d.Name));
        Assert.Equal(new[] { "C04" }, travel[1].TopicKeys);

        var daily = Build(new[] { "communication" }, new[] { "C01", "C02", "C03" }, "A2",
            Pool("C01", 20).Concat(Pool("C02", 20, firstId: 100)).Concat(Pool("C03", 20, firstId: 200)).ToList());
        Assert.Equal("Câu giao tiếp hằng ngày", Assert.Single(daily).Name);
    }

    [Fact]
    public void TopicsWithNoChosenGoal_FormTheDeckOfTheirOwn()
    {
        var pool = Pool("A16", 25).Concat(Pool("A21", 25, firstId: 100)).ToList(); // A21 is hobbies, the goal is travel
        var decks = Build(new[] { "travel" }, new[] { "A16", "A21" }, "A1", pool);

        Assert.Equal(new[] { "Du lịch – bộ khởi đầu", "Chủ đề bạn chọn" }, decks.Select(d => d.Name));
        Assert.Equal(new[] { "A21" }, decks[1].TopicKeys);
    }

    [Fact]
    public void ATopicWithTwoChosenGoals_IsUsedOnce_ByTheFirstGoal()
    {
        // A15 (places in the city) is tagged travel only; A09 is tagged communication and beginner: with both goals chosen
        // the topic is given to the first goal in the user's order.
        var pool = Pool("A09", 25).Concat(Pool("A19", 25, firstId: 100)).ToList();
        var decks = Build(new[] { "beginner", "communication" }, new[] { "A09", "A19" }, "A1", pool);

        var withA09 = decks.Where(d => d.TopicKeys.Contains("A09")).ToList();
        Assert.Single(withA09);
        Assert.Equal("Từ vựng nền tảng – bộ khởi đầu", withA09[0].Name);
    }

    [Fact]
    public void Names_AreUniquePerUser_WithANumberedSuffix()
    {
        var pool = Pool("A16", 25);
        var decks = Build(new[] { "travel" }, new[] { "A16" }, "A1", pool,
            names: new[] { "Du lịch – bộ khởi đầu", "Du lịch – bộ khởi đầu (2)" });
        Assert.Equal("Du lịch – bộ khởi đầu (3)", Assert.Single(decks).Name);
    }

    [Fact]
    public void Sizes_AreCapped_AtThirtyPerDeck_AndOneHundredInTotal()
    {
        var pool = Pool("A16", 60).Concat(Pool("B01", 60, "B1", firstId: 100)).Concat(Pool("A19", 60, firstId: 200))
            .Concat(Pool("A13", 60, firstId: 300)).Concat(Pool("A02", 60, firstId: 400)).ToList();
        var decks = Build(new[] { "travel", "work", "hobbies", "study" }, new[] { "A16", "B01", "A19", "A13" }, "A2", pool, size: 99);

        Assert.All(decks, d => Assert.True(d.Cards.Count <= OnboardingDeckBuilder.MaxDeckSize));
        Assert.True(decks.Sum(d => d.Cards.Count) <= OnboardingDeckBuilder.MaxTotalCards);
        var ids = decks.SelectMany(d => d.Cards.Select(c => c.CardId)).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void AnA1Learner_WhoPicksOnlyIntermediateTopics_StillGetsADeck_NearestLevelFirst()
    {
        // The A1 window is {A1, A2}; B topics have no such cards, so the deck is filled with the nearest levels, B1 before B2.
        var pool = Pool("B01", 12, "B2").Concat(Pool("B01", 12, "B1", prefix: "mid", firstId: 100)).ToList();
        var deck = Assert.Single(Build(new[] { "work" }, new[] { "B01" }, "A1", pool, size: 12));

        Assert.Equal(12, deck.Cards.Count);
        Assert.All(deck.Cards, c => Assert.Equal("B1", c.Cefr));
    }

    [Fact]
    public void NoCandidates_GiveNoDecks()
    {
        Assert.Empty(Build(new[] { "travel" }, new[] { "A16" }, "A1", new List<OnboardingCard>()));
    }

    [Fact]
    public void Descriptions_NameTheSourceTopics_AndStayWithinTheSetLimit()
    {
        var decks = Build(new[] { "travel" }, new[] { "A16" }, "A1", Pool("A16", 25));
        var description = Assert.Single(decks).Description;
        Assert.StartsWith("Tạo từ sở thích · Nguồn: ", description);
        Assert.Contains("Phương tiện giao thông", description);
        Assert.True(description.Length <= 500);
    }
}
