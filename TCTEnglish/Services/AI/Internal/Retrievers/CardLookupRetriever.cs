using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class CardLookupRetriever : IKnowledgeRetriever
{
    private const int ExactBound = 25;
    private const int PhraseBound = 20;
    private const int BroadBound = 100;

    private static readonly HashSet<string> LookupFraming = new(StringComparer.Ordinal)
    {
        "against",
        "bo",
        "card",
        "cards",
        "check",
        "class",
        "cua",
        "explain",
        "from",
        "giai",
        "giua",
        "help",
        "looking",
        "my",
        "nay",
        "notes",
        "please",
        "reading",
        "sanh",
        "set",
        "show",
        "so",
        "study",
        "the",
        "thich",
        "toi",
        "trong",
        "va",
        "while",
        "your"
    };

    private static readonly string[] GenericSetPhrases =
    [
        "the",
        "the cua toi",
        "the cua minh",
        "the cua ban",
        "bo the",
        "set cua toi"
    ];

    private static readonly Regex QuotedSetLabelRegex = new(
        "trong\\s+(?:bộ|bo|set)\\s+[\"']([^\"']+)[\"']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly DbflashcardContext _context;

    public CardLookupRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent) => intent == UserIntent.CardLookup;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        var searchTerms = AiCardLookupQueryParser.ExtractSearchTerms(userMessage);
        var request = BuildRequest(userMessage, searchTerms);
        var exactNeedles = BuildExactNeedles(searchTerms, request.TargetTokens);
        var phrase = request.TargetTokens.Count >= 2 ? string.Join(' ', request.TargetTokens) : null;
        if (phrase is { Length: < 3 })
        {
            phrase = null;
        }

        var broadTokens = request.TargetTokens
            .Where(term => term.Length >= 3)
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToArray();
        if (exactNeedles.Count == 0 && phrase is null && broadTokens.Length == 0)
        {
            return [];
        }

        var setIds = await ResolveSourceIdsAsync(userId, request, ct);
        if (setIds is null)
        {
            return [];
        }

        var authorized = AuthorizedCards(userId);
        var exact = exactNeedles.Count == 0
            ? []
            : await Project(BoundedExact(authorized, exactNeedles.ToArray(), setIds)).ToListAsync(ct);
        var phraseHits = phrase is null
            ? []
            : await Project(BoundedPhrase(authorized, phrase, setIds)).ToListAsync(ct);
        var broad = broadTokens.Length == 0
            ? []
            : await Project(BoundedBroad(authorized, broadTokens, setIds)).ToListAsync(ct);
        var cards = exact
            .Concat(phraseHits)
            .Concat(broad)
            .GroupBy(card => card.CardId)
            .Select(group => group.First())
            .ToList();

        var matches = cards
            .Select(card => new
            {
                Card = card,
                Score = Score(card.Term, card.Definition, searchTerms)
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Card.Term, StringComparer.Ordinal)
            .ThenBy(result => result.Card.SetName, StringComparer.Ordinal)
            .ThenBy(result => result.Card.CardId)
            .Take(3)
            .ToList();

        return matches.Select(result => new KnowledgeSnippet(
            result.Card.Term,
            string.Join(
                '|',
                $"setName={SanitizeMetadataValue(result.Card.SetName)}",
                $"definition={SanitizeMetadataValue(result.Card.Definition)}",
                $"phonetic={SanitizeMetadataValue(result.Card.Phonetic)}",
                $"example={SanitizeMetadataValue(result.Card.Example)}"),
            KnowledgeSnippetSources.CardLookupResult,
            Priority: result.Score,
            EntityId: result.Card.CardId,
            ContainerId: result.Card.SetId))
            .ToList();
    }

    private IQueryable<Card> AuthorizedCards(int userId)
        => LearnerCardAccess.Authorized(_context, userId);

    private static IQueryable<CardCandidate> Project(IQueryable<Card> cards)
    {
        return cards.Select(card => new CardCandidate
        {
            CardId = card.CardId,
            SetId = card.SetId,
            Term = card.Term,
            Definition = card.Definition,
            Phonetic = card.Phonetic,
            Example = card.Example,
            SetName = card.Set.SetName
        });
    }

    private static List<string> BuildExactNeedles(IReadOnlyList<string> searchTerms, IReadOnlyList<string> contentTokens)
    {
        if (searchTerms.Count == 0 && contentTokens.Count == 0)
        {
            return [];
        }

        var selected = new List<string>();
        if (searchTerms.Count > 0)
        {
            selected.Add(searchTerms[0]);
        }

        if (contentTokens.Count >= 2)
        {
            selected.Add(string.Join(' ', contentTokens));
        }

        selected.AddRange(contentTokens);
        selected.AddRange(searchTerms.Where(term => term.Contains(' ', StringComparison.Ordinal)));
        selected.AddRange(searchTerms.Where(term =>
            !term.Contains(' ', StringComparison.Ordinal)
            && term.Length == 2
            && !LookupFraming.Contains(term)));

        var needles = new List<string>();
        foreach (var term in selected.Distinct(StringComparer.Ordinal))
        {
            needles.Add(term);
            if (term.Contains(' ', StringComparison.Ordinal))
            {
                needles.Add(term.Replace(' ', '-'));
                needles.Add(term.Replace(' ', '_'));
            }
        }

        return needles
            .Distinct(StringComparer.Ordinal)
            .Take(24)
            .ToList();
    }

    private static List<string> SelectContentTokens(IReadOnlyList<string> searchTerms)
    {
        var singles = searchTerms
            .Where(term => !term.Contains(' ', StringComparison.Ordinal) && term.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var content = singles.Where(term => !LookupFraming.Contains(term)).ToList();
        return content.Count == 0 ? singles : content;
    }

    public static IQueryable<Card> BoundedExact(IQueryable<Card> authorized, IReadOnlyCollection<string> needles, IReadOnlyCollection<int> setIds)
    {
        var exactNeedles = needles.Distinct(StringComparer.Ordinal).ToArray();
        return ApplySourceFilter(authorized, setIds)
            .Where(card => exactNeedles.Contains(card.Term.ToLower()))
            .OrderBy(card => card.CardId)
            .Take(ExactBound);
    }

    public static IQueryable<Card> BoundedPhrase(IQueryable<Card> authorized, string phrase, IReadOnlyCollection<int> setIds)
    {
        return ApplySourceFilter(authorized, setIds)
            .Where(card => card.Term.ToLower().Replace("-", " ").Contains(phrase))
            .OrderBy(card => card.CardId)
            .Take(PhraseBound);
    }

    public static IQueryable<Card> BoundedBroad(IQueryable<Card> authorized, IReadOnlyCollection<string> tokens, IReadOnlyCollection<int> setIds)
    {
        var broadTokens = tokens.Where(token => token.Length >= 3).Distinct(StringComparer.Ordinal).Take(8).ToArray();
        if (broadTokens.Length == 0)
        {
            return authorized.Where(card => false);
        }

        return ApplySourceFilter(authorized, setIds)
            .Where(BuildTokenFilter(broadTokens))
            .OrderBy(card => card.Term)
            .ThenBy(card => card.CardId)
            .Take(BroadBound);
    }

    private static CardLookupRequest BuildRequest(string? userMessage, IReadOnlyList<string> searchTerms)
    {
        var contentTokens = SelectContentTokens(searchTerms);
        var source = ParseSource(userMessage);
        var labelTokens = source.Labels
            .SelectMany(label => label.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.Ordinal);
        var targetTokens = contentTokens.Where(token => !labelTokens.Contains(token)).ToList();
        if (targetTokens.Count == 0)
        {
            targetTokens = contentTokens;
        }

        return new CardLookupRequest(targetTokens, source.Kind, source.Labels);
    }

    private static (CardLookupSourceKind Kind, IReadOnlyList<string> Labels) ParseSource(string? userMessage)
    {
        var normalized = AiTextNormalizer.Normalize(userMessage);
        if (AiTextNormalizer.ContainsAny(normalized, "so sanh", "compare", "khac nhau"))
        {
            var labels = ReadComparisonLabels(normalized);
            if (labels.Count > 0)
            {
                return (CardLookupSourceKind.Comparison, labels);
            }
        }

        var label = ReadExplicitLabel(userMessage, normalized);
        return label is null
            ? (CardLookupSourceKind.None, [])
            : (CardLookupSourceKind.Specified, [label]);
    }

    private static List<string> ReadComparisonLabels(string normalized)
    {
        foreach (var marker in new[] { "giua bo ", "giua set " })
        {
            var index = normalized.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            var labels = new List<string>();
            foreach (var part in normalized[(index + marker.Length)..].Split([" va ", " and ", " voi "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Length >= 3)
                {
                    labels.Add(part);
                }
            }

            if (labels.Count > 0)
            {
                return labels;
            }
        }

        return [];
    }

    private static string? ReadExplicitLabel(string? raw, string normalized)
    {
        var quoted = QuotedSetLabelRegex.Match(raw ?? string.Empty);
        if (quoted.Success)
        {
            var quotedLabel = AiTextNormalizer.Normalize(quoted.Groups[1].Value);
            return quotedLabel.Length >= 3 && !IsGenericSetPhrase(quotedLabel) ? quotedLabel : null;
        }

        foreach (var marker in new[] { "trong bo ", "trong set " })
        {
            var index = normalized.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            var label = normalized[(index + marker.Length)..].Trim();
            if (label.Length < 3 || IsGenericSetPhrase(label))
            {
                return null;
            }

            return label;
        }

        return null;
    }

    private static bool IsGenericSetPhrase(string label)
    {
        return GenericSetPhrases.Contains(label, StringComparer.Ordinal);
    }

    private async Task<int[]?> ResolveSourceIdsAsync(int userId, CardLookupRequest request, CancellationToken ct)
    {
        if (request.SourceKind == CardLookupSourceKind.None || request.SourceLabels.Count == 0)
        {
            return [];
        }

        var sets = await AccessibleSets(userId)
            .Select(set => new { set.SetId, set.SetName })
            .ToListAsync(ct);
        var resolved = new List<int>();
        foreach (var label in request.SourceLabels)
        {
            var matches = sets
                .Where(set => string.Equals(AiTextNormalizer.Normalize(set.SetName), label, StringComparison.Ordinal))
                .Select(set => set.SetId)
                .Distinct()
                .ToList();
            if (matches.Count == 0 || (matches.Count > 1 && request.SourceKind == CardLookupSourceKind.Comparison))
            {
                return null;
            }

            resolved.AddRange(matches);
        }

        return resolved.Distinct().ToArray();
    }

    private static IQueryable<Card> ApplySourceFilter(IQueryable<Card> cards, IReadOnlyCollection<int> setIds)
    {
        if (setIds.Count == 0)
        {
            return cards;
        }

        var ids = setIds.Distinct().ToArray();
        return cards.Where(card => ids.Contains(card.SetId));
    }

    private IQueryable<Set> AccessibleSets(int userId)
    {
        return _context.Sets
            .AsNoTracking()
            .Where(set =>
                set.OwnerId == userId
                || (set.FolderId != null
                    && (_context.Folders.Any(folder => folder.FolderId == set.FolderId && folder.UserId == userId)
                        || _context.SavedFolders.Any(saved => saved.UserId == userId && saved.FolderId == set.FolderId)
                        || _context.ClassFolders.Any(link =>
                            link.FolderId == set.FolderId
                            && (_context.Classes.Any(currentClass =>
                                    currentClass.ClassId == link.ClassId && currentClass.OwnerId == userId)
                                || _context.ClassMembers.Any(member =>
                                    member.ClassId == link.ClassId && member.UserId == userId))))));
    }

    private static Expression<Func<Card, bool>> BuildTokenFilter(IReadOnlyList<string> tokens)
    {
        var card = Expression.Parameter(typeof(Card), "card");
        var term = Expression.Property(card, nameof(Card.Term));
        var definition = Expression.Property(card, nameof(Card.Definition));
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        Expression? body = null;
        foreach (var token in tokens)
        {
            var needle = Expression.Constant(token);
            var either = Expression.OrElse(
                Expression.Call(term, contains, needle),
                Expression.Call(definition, contains, needle));
            body = body is null ? either : Expression.OrElse(body, either);
        }

        return Expression.Lambda<Func<Card, bool>>(body!, card);
    }

    private static int Score(string term, string definition, IReadOnlyList<string> searchTerms)
    {
        var normalizedTerm = AiTextNormalizer.Normalize(term);
        var normalizedDefinition = AiTextNormalizer.Normalize(definition);
        var bestScore = 0;

        foreach (var searchTerm in searchTerms)
        {
            if (string.Equals(normalizedTerm, searchTerm, StringComparison.Ordinal))
            {
                bestScore = Math.Max(bestScore, 100);
                continue;
            }

            if (normalizedTerm.StartsWith($"{searchTerm} ", StringComparison.Ordinal)
                || normalizedTerm.EndsWith($" {searchTerm}", StringComparison.Ordinal)
                || normalizedTerm.Contains($" {searchTerm} ", StringComparison.Ordinal))
            {
                bestScore = Math.Max(bestScore, 85);
                continue;
            }

            if (normalizedTerm.Contains(searchTerm, StringComparison.Ordinal))
            {
                bestScore = Math.Max(bestScore, 70);
            }

            if (normalizedDefinition.Contains(searchTerm, StringComparison.Ordinal))
            {
                bestScore = Math.Max(bestScore, 40);
            }
        }

        return bestScore;
    }

    private static string SanitizeMetadataValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace('|', '/')
            .Replace('=', ':')
            .Trim();
    }

    private sealed class CardCandidate
    {
        public int CardId { get; set; }

        public int SetId { get; set; }

        public string Term { get; set; } = string.Empty;

        public string Definition { get; set; } = string.Empty;

        public string? Phonetic { get; set; }

        public string? Example { get; set; }

        public string SetName { get; set; } = string.Empty;
    }
}

internal static class LearnerCardAccess
{
    public static IQueryable<Card> Authorized(DbflashcardContext context, int userId)
    {
        return context.Cards
            .AsNoTracking()
            .Where(card => context.Sets.Any(set =>
                set.SetId == card.SetId
                && (set.OwnerId == userId
                    || (set.FolderId != null
                        && (context.Folders.Any(folder => folder.FolderId == set.FolderId && folder.UserId == userId)
                            || context.SavedFolders.Any(saved => saved.UserId == userId && saved.FolderId == set.FolderId)
                            || context.ClassFolders.Any(link =>
                                link.FolderId == set.FolderId
                                && (context.Classes.Any(currentClass =>
                                        currentClass.ClassId == link.ClassId && currentClass.OwnerId == userId)
                                    || context.ClassMembers.Any(member =>
                                        member.ClassId == link.ClassId && member.UserId == userId))))))));
    }
}
