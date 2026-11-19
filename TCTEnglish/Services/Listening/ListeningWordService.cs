using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TCTEnglish.Services.Sets;
using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Listening
{
    /// <summary>
    /// Tap-a-word lookup and "save to my set" for the Listening practice page. Access to the lesson is decided by
    /// <see cref="IListeningService.GetWordContextAsync"/> (the same rule as the practice page); the personal set is only
    /// ever the caller's own set, found by owner and name, never by an id taken from the request.
    /// </summary>
    public sealed class ListeningWordService : IListeningWordService
    {
        private const int DefinitionMax = 300;
        private const int ExampleMax = 300;
        private const int SaveTermMax = 60;
        private const int TopicMax = 60;
        private const int AiLookupsPerWindow = 30;

        private static readonly TimeSpan LookupWindow = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);

        // One word: letters with inner apostrophes and hyphens, 1-40 characters (the straight apostrophe only; callers normalize).
        private static readonly Regex LookupTerm = new(@"^\p{L}[\p{L}'\-]{0,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // A saved term may also be a lesson phrase such as "to go": up to five words separated by single spaces.
        private static readonly Regex SaveTerm = new(@"^\p{L}[\p{L}'\-]*(?: \p{L}[\p{L}'\-]*){0,4}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // The limiter has to outlive the scoped service, and the repo's one-line DI rule leaves no room for a singleton.
        private static readonly ConcurrentDictionary<int, Queue<DateTimeOffset>> AiLookups = new();
        private static readonly MemoryCache OwnCache = new(new MemoryCacheOptions { SizeLimit = 5000 });

        private sealed record DictionaryEntry(string Word, string? Phonetic, string Definition, string? Example);

        private readonly DbflashcardContext _db;
        private readonly IListeningService _listening;
        private readonly IPersonalSetService _sets;
        private readonly IVocabSuggestService _suggest;
        private readonly IMemoryCache _cache;
        private readonly TimeProvider _time;
        private readonly ILogger<ListeningWordService> _logger;

        public ListeningWordService(
            DbflashcardContext db,
            IListeningService listening,
            IPersonalSetService sets,
            IVocabSuggestService suggest,
            ILogger<ListeningWordService> logger,
            IMemoryCache? cache = null,
            TimeProvider? time = null)
        {
            _db = db;
            _listening = listening;
            _sets = sets;
            _suggest = suggest;
            _logger = logger;
            _cache = cache ?? OwnCache;
            _time = time ?? TimeProvider.System;
        }

        // ----------------------------------------------------------------
        // Lookup
        // ----------------------------------------------------------------

        public async Task<ListeningWordLookupResult> LookupAsync(int lessonId, string? term, int? userId, CancellationToken ct = default)
        {
            var key = ListeningWordForms.Key(term?.Trim());
            if (key.Length == 0 || !LookupTerm.IsMatch(key))
                return ListeningWordLookupResult.Failure(ListeningWordLookupStatus.InvalidTerm);

            var context = await _listening.GetWordContextAsync(lessonId, userId, ct);
            if (context == null)
                return ListeningWordLookupResult.Failure(ListeningWordLookupStatus.NotFound);

            // 1. The lesson's own vocabulary, inflections included. Everyone, guests too, gets the full meaning.
            var match = context.Vocab.FirstOrDefault(v => IsSingleWord(v.Word) && ListeningWordForms.SameWord(key, v.Word));
            if (match != null)
            {
                return ListeningWordLookupResult.Found("lesson", match.Word.Trim(), Truncate(match.Definition, DefinitionMax), Truncate(match.ExampleSentence, ExampleMax), null);
            }

            // 2. Any other word: the dictionary is a Premium/Admin feature. Everyone else sees the line translation only.
            if (!context.CanLookupAnyWord || userId is not int uid)
                return ListeningWordLookupResult.Locked(key);

            var cacheKey = "listening-word:" + key;
            if (_cache.TryGetValue(cacheKey, out DictionaryEntry? cached) && cached != null)
                return ToFound(cached);

            // Only a lookup that reaches the provider costs money, so only that one counts against the limit.
            if (!TryTakeAiLookup(uid, out var retryAfter))
                return ListeningWordLookupResult.Failure(ListeningWordLookupStatus.RateLimited, retryAfter);

            VocabSuggestionResult suggestion;
            try
            {
                suggestion = await _suggest.SuggestAsync(key, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Listening word lookup failed for lesson {LessonId}", lessonId);
                return ListeningWordLookupResult.Failure(ListeningWordLookupStatus.ProviderFailed);
            }

            var definition = suggestion?.Definitions
                .Select(d => d.Definition?.Trim())
                .FirstOrDefault(d => !string.IsNullOrEmpty(d));
            if (suggestion == null || string.IsNullOrEmpty(definition))
                return ListeningWordLookupResult.Failure(ListeningWordLookupStatus.ProviderFailed);

            var entry = new DictionaryEntry(
                key,
                Truncate(suggestion.Phonetic, 100),
                Truncate(definition, DefinitionMax)!,
                Truncate(suggestion.Example, ExampleMax));
            _cache.Set(cacheKey, entry, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheLifetime, Size = 1 });
            return ToFound(entry);
        }

        private static ListeningWordLookupResult ToFound(DictionaryEntry entry)
            => ListeningWordLookupResult.Found("dictionary", entry.Word, entry.Definition, entry.Example, entry.Phonetic);

        private bool TryTakeAiLookup(int userId, out int retryAfterSeconds)
        {
            var now = _time.GetUtcNow();
            var queue = AiLookups.GetOrAdd(userId, _ => new Queue<DateTimeOffset>());
            lock (queue)
            {
                while (queue.Count > 0 && now - queue.Peek() >= LookupWindow)
                    queue.Dequeue();

                if (queue.Count >= AiLookupsPerWindow)
                {
                    var wait = queue.Peek() + LookupWindow - now;
                    retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
                    return false;
                }

                queue.Enqueue(now);
                retryAfterSeconds = 0;
                return true;
            }
        }

        // ----------------------------------------------------------------
        // Save
        // ----------------------------------------------------------------

        public async Task<ListeningWordSaveResult> SaveAsync(int lessonId, ListeningWordSaveRequest? request, int userId, CancellationToken ct = default)
        {
            if (request == null)
                return ListeningWordSaveResult.Of(ListeningWordSaveStatus.Invalid);

            var term = CleanSaveTerm(request.Term);
            var definition = (request.Definition ?? string.Empty).Trim();
            var example = (request.Example ?? string.Empty).Trim();
            if (term == null
                || definition.Length == 0 || definition.Length > DefinitionMax
                || example.Length > ExampleMax)
            {
                return ListeningWordSaveResult.Of(ListeningWordSaveStatus.Invalid);
            }

            var context = await _listening.GetWordContextAsync(lessonId, userId, ct);
            if (context == null)
                return ListeningWordSaveResult.Of(ListeningWordSaveStatus.NotFound);

            try
            {
                // The caller's own set only: found by owner and exact name. A same-named set of someone else is never touched.
                var setId = await _db.Sets
                    .AsNoTracking()
                    .Where(s => s.OwnerId == userId && s.SetName == ListeningWordSet.Name)
                    .OrderBy(s => s.SetId)
                    .Select(s => (int?)s.SetId)
                    .FirstOrDefaultAsync(ct);

                if (setId == null)
                {
                    var created = await _sets.CreateSetAsync(
                        new SetInfoFormViewModel
                        {
                            SetName = ListeningWordSet.Name,
                            Description = ListeningWordSet.Description,
                            Visibility = SetVisibility.Private,
                        },
                        userId,
                        isAdmin: false,
                        ct);
                    if (created.Status != OperationStatus.Success)
                        return ListeningWordSaveResult.Of(ListeningWordSaveStatus.Failed);

                    setId = created.Value;
                }

                var level = context.Level?.Trim().ToUpperInvariant();
                var card = new CardInputDto
                {
                    Term = term,
                    Definition = definition,
                    Topic = Truncate(context.Topic, TopicMax),
                    Cefr = CefrLevels.IsValid(level) ? level : null,
                    Examples = example.Length == 0 ? null : new List<CardExample> { new() { Text = example } },
                };

                // The batch rule already skips a term the set holds (case-insensitive): that is the duplicate answer.
                var batch = await _sets.CreateCardsBatchAsync(setId.Value, new[] { card }, userId, isAdmin: false, ct);
                if (batch.Status != OperationStatus.Success || batch.Value == null)
                    return ListeningWordSaveResult.Of(ListeningWordSaveStatus.Failed);

                if (batch.Value.Inserted.Count > 0)
                    return ListeningWordSaveResult.Of(ListeningWordSaveStatus.Saved, setId);

                return batch.Value.Skipped.Count > 0
                    ? ListeningWordSaveResult.Of(ListeningWordSaveStatus.Duplicate, setId)
                    : ListeningWordSaveResult.Of(ListeningWordSaveStatus.Invalid, setId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Saving a listening word failed for user {UserId}, lesson {LessonId}", userId, lessonId);
                return ListeningWordSaveResult.Of(ListeningWordSaveStatus.Failed);
            }
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        /// <summary>Trim, straight apostrophes, single inner spaces; null when the term is not a word or a short phrase.</summary>
        private static string? CleanSaveTerm(string? term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return null;

            var cleaned = string.Join(' ', term.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .Replace('‘', '\'').Replace('’', '\'').Replace('ʼ', '\'');
            return cleaned.Length <= SaveTermMax && SaveTerm.IsMatch(cleaned) ? cleaned : null;
        }

        private static bool IsSingleWord(string? word) => !string.IsNullOrWhiteSpace(word) && word.Trim().IndexOfAny(new[] { ' ', '\t' }) < 0;

        private static string? Truncate(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            return trimmed.Length <= max ? trimmed : trimmed[..max];
        }
    }
}
