namespace TCTEnglish.Services.Onboarding;

/// <summary>A system card as the deck builder sees it (only what ranking and the preview need).</summary>
public sealed record OnboardingCard(int CardId, string TopicKey, string Term, string? Meaning, string? ImageUrl, string? Cefr);

/// <summary>The learner's choices. <see cref="DeckSize"/> is clamped to 10..30.</summary>
public sealed record DeckBuildRequest(IReadOnlyList<string> Goals, IReadOnlyList<string> Topics, string Level, int Pace, int DeckSize = 25);

/// <summary>One deck to create: a name, the goal it serves, the topics it draws from and its cards in learning order.</summary>
public sealed record StarterDeck(string Key, string Name, string Goal, IReadOnlyList<string> TopicKeys, IReadOnlyList<OnboardingCard> Cards, string Description);

/// <summary>
/// Builds the starter decks from the chosen goals and topics. Pure and deterministic (no clock, no randomness, no AI):
/// the same input always gives the same decks, so the preview and the final creation agree.
/// </summary>
public static class OnboardingDeckBuilder
{
    public const int DefaultDeckSize = 25;
    public const int MinDeckSize = 10;
    public const int MaxDeckSize = 30;
    public const int MaxDecks = 4;
    public const int MaxTotalCards = 100;

    private static readonly string[] CefrOrder = { "A1", "A2", "B1", "B2", "C1", "C2" };

    private sealed class Group
    {
        public required string Key { get; init; }
        public required string Name { get; init; }
        public required string Goal { get; init; }
        public List<string> Topics { get; } = new();
        public List<OnboardingCard> Cards { get; set; } = new();
    }

    /// <summary>Lower-case, trimmed form used to compare words across decks, own sets and "Đã biết".</summary>
    public static string Lemma(string? term) => (term ?? string.Empty).Trim().ToLowerInvariant();

    private static int Rank(string? cefr)
    {
        var index = cefr == null ? -1 : Array.IndexOf(CefrOrder, cefr.Trim().ToUpperInvariant());
        return index < 0 ? 2 : index; // an unknown level is treated as B1: neither the easiest nor the hardest
    }

    /// <summary>The levels allowed for each learner level: A1 → A1–A2, A2 → A1–B1, B1 → A2–B2, B2 → B1–B2.</summary>
    public static bool InWindow(string level, string? cefr)
    {
        if (string.IsNullOrWhiteSpace(cefr))
        {
            return true;
        }

        var learner = Array.IndexOf(CefrOrder, level);
        var card = Array.IndexOf(CefrOrder, cefr.Trim().ToUpperInvariant());
        if (learner < 0 || card < 0)
        {
            return true;
        }

        var low = Math.Max(0, learner - 1);
        var high = Math.Min(3, learner + 1);
        return card >= low && card <= high;
    }

    private static int Distance(string level, string? cefr) =>
        string.IsNullOrWhiteSpace(cefr) ? 1 : Math.Abs(Rank(cefr) - Math.Max(0, Array.IndexOf(CefrOrder, level)));

    public static IReadOnlyList<StarterDeck> Build(
        DeckBuildRequest request,
        IReadOnlyList<OnboardingCard> pool,
        IReadOnlySet<string> ownedTerms,
        IReadOnlySet<string> knownTerms,
        IReadOnlyCollection<string> existingSetNames)
    {
        var size = Math.Clamp(request.DeckSize, MinDeckSize, MaxDeckSize);
        var topics = request.Topics.Where(key => OnboardingCatalog.TryGetTopic(key, out _)).Distinct(StringComparer.Ordinal).ToList();
        var goals = request.Goals.Where(OnboardingCatalog.IsGoal).Distinct(StringComparer.Ordinal).ToList();

        var groups = FormGroups(goals, topics);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in ownedTerms) { used.Add(Lemma(term)); }
        foreach (var term in knownTerms) { used.Add(Lemma(term)); }

        var budget = MaxTotalCards;
        foreach (var group in groups)
        {
            var take = Math.Min(size, budget);
            group.Cards = take <= 0 ? new List<OnboardingCard>() : Fill(group, pool, request.Level, take, used);
            budget -= group.Cards.Count;
        }

        groups = groups.Where(group => group.Cards.Count > 0).ToList();
        MergeSmallGroups(groups);
        MergeDownToMaxDecks(groups);

        var taken = new HashSet<string>(existingSetNames.Select(name => name.Trim()), StringComparer.OrdinalIgnoreCase);
        var decks = new List<StarterDeck>();
        foreach (var group in groups)
        {
            var cards = group.Cards.Take(MaxDeckSize)
                .Select((card, order) => (Card: card, Order: order))
                .OrderBy(item => Rank(item.Card.Cefr))
                .ThenBy(item => item.Order)
                .Select(item => item.Card)
                .ToList();
            decks.Add(new StarterDeck(group.Key, UniqueName(group.Name, taken), group.Goal, group.Topics.ToList(), cards, Describe(group.Topics)));
        }

        return decks;
    }

    // One group per chosen goal (in the learner's order), then the phrase group, then the topics no goal covers.
    private static List<Group> FormGroups(IReadOnlyList<string> goals, IReadOnlyList<string> topics)
    {
        var phrasesOwnGroup = goals.Contains("travel") || goals.Contains("communication");
        var phraseName = goals.Contains("travel") ? "Câu giao tiếp khi đi du lịch" : "Câu giao tiếp hằng ngày";
        var phraseGoal = goals.Contains("travel") ? "travel" : "communication";

        var byGoal = goals.ToDictionary(goal => goal, goal => new List<string>(), StringComparer.Ordinal);
        var phrases = new List<string>();
        var other = new List<string>();
        foreach (var key in topics)
        {
            if (phrasesOwnGroup && OnboardingCatalog.Topic(key).IsPhrase)
            {
                phrases.Add(key);
                continue;
            }

            var goal = goals.FirstOrDefault(g => OnboardingCatalog.TopicsOfGoal(g).Contains(key));
            if (goal == null) { other.Add(key); } else { byGoal[goal].Add(key); }
        }

        var groups = new List<Group>();
        foreach (var goal in goals.Where(g => byGoal[g].Count > 0))
        {
            var group = new Group { Key = goal, Name = GoalDeckName(goal), Goal = goal };
            group.Topics.AddRange(byGoal[goal]);
            groups.Add(group);
        }

        if (phrases.Count > 0)
        {
            var group = new Group { Key = "phrases", Name = phraseName, Goal = phraseGoal };
            group.Topics.AddRange(phrases);
            groups.Add(group);
        }

        if (other.Count > 0)
        {
            var group = new Group { Key = "other", Name = "Chủ đề bạn chọn", Goal = "other" };
            group.Topics.AddRange(other);
            groups.Add(group);
        }

        return groups;
    }

    private static string GoalDeckName(string goal) => goal switch
    {
        "travel" => "Du lịch – bộ khởi đầu",
        "communication" => "Giao tiếp hằng ngày – bộ khởi đầu",
        "work" => "Công việc – bộ khởi đầu",
        "study" => "Học tập – bộ khởi đầu",
        "hobbies" => "Sở thích & giải trí – bộ khởi đầu",
        "beginner" => "Từ vựng nền tảng – bộ khởi đầu",
        _ => "Chủ đề bạn chọn"
    };

    // Pass 1 takes only cards inside the learner's CEFR window; pass 2 (only when the window left the deck short)
    // takes the nearest levels first. Both passes mix the topics round-robin and put cards without an image last.
    private static List<OnboardingCard> Fill(Group group, IReadOnlyList<OnboardingCard> pool, string level, int take, HashSet<string> used)
    {
        var picked = new List<OnboardingCard>();
        var inGroup = group.Topics.ToDictionary(
            key => key,
            key => pool.Where(card => card.TopicKey == key).ToList(),
            StringComparer.Ordinal);

        foreach (var pass in new[] { 1, 2 })
        {
            var queues = group.Topics.Select(key =>
            {
                var phrase = OnboardingCatalog.Topic(key).IsPhrase;
                var cards = inGroup[key]
                    .Where(card => !used.Contains(Lemma(card.Term)))
                    .Where(card => pass == 1 ? phrase || InWindow(level, card.Cefr) : !(phrase || InWindow(level, card.Cefr)))
                    .OrderBy(card => string.IsNullOrWhiteSpace(card.ImageUrl) ? 1 : 0)
                    .ThenBy(card => phrase ? 0 : Distance(level, card.Cefr))
                    .ThenBy(card => card.CardId)
                    .ToList();
                return new Queue<OnboardingCard>(cards);
            }).ToList();

            while (picked.Count < take && queues.Any(queue => queue.Count > 0))
            {
                foreach (var queue in queues)
                {
                    while (queue.Count > 0 && used.Contains(Lemma(queue.Peek().Term)))
                    {
                        queue.Dequeue();
                    }

                    if (queue.Count == 0 || picked.Count >= take)
                    {
                        continue;
                    }

                    var card = queue.Dequeue();
                    used.Add(Lemma(card.Term));
                    picked.Add(card);
                }
            }

            if (picked.Count >= take)
            {
                break;
            }
        }

        return picked;
    }

    // A deck of fewer than 10 cards is merged into the nearest deck (the one before it, or the next for the first).
    private static void MergeSmallGroups(List<Group> groups)
    {
        while (groups.Count > 1)
        {
            var index = groups.FindIndex(group => group.Cards.Count < MinDeckSize);
            if (index < 0)
            {
                return;
            }

            MergeInto(groups, index, index > 0 ? index - 1 : index + 1);
        }
    }

    private static void MergeDownToMaxDecks(List<Group> groups)
    {
        while (groups.Count > MaxDecks)
        {
            var smallest = 0;
            for (var i = 1; i < groups.Count; i++)
            {
                if (groups[i].Cards.Count < groups[smallest].Cards.Count) { smallest = i; }
            }

            MergeInto(groups, smallest, smallest > 0 ? smallest - 1 : smallest + 1);
        }
    }

    private static void MergeInto(List<Group> groups, int from, int into)
    {
        var source = groups[from];
        var target = groups[into];
        target.Cards.AddRange(source.Cards);
        foreach (var topic in source.Topics.Where(topic => !target.Topics.Contains(topic)))
        {
            target.Topics.Add(topic);
        }

        groups.RemoveAt(from);
    }

    private static string UniqueName(string name, HashSet<string> taken)
    {
        var candidate = name;
        for (var n = 2; taken.Contains(candidate); n++)
        {
            candidate = $"{name} ({n})";
        }

        taken.Add(candidate);
        return candidate;
    }

    private static string Describe(IEnumerable<string> topicKeys)
    {
        var names = string.Join(", ", topicKeys.Select(key => OnboardingCatalog.Topic(key).SetName));
        var text = "Tạo từ sở thích · Nguồn: " + names;
        return text.Length <= 500 ? text : text[..499] + "…";
    }
}
