using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Sets;

// Practice area: read-only selection of words for the /Practice modes. Grading and state live in JS.
public sealed partial class PersonalSetService
{
    private sealed class PracticeRow
    {
        public int CardId { get; set; }
        public int SetId { get; set; }
        public string Term { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public string? Phonetic { get; set; }
        public string? PhoneticUk { get; set; }
        public string? MeaningEn { get; set; }
        public string? PartsOfSpeech { get; set; }
        public string? Topic { get; set; }
        public string? Cefr { get; set; }
        public string? Emoji { get; set; }
        public string? ImageUrl { get; set; }
        public string? Example { get; set; }
        public string? ExampleTranslation { get; set; }
        public string? DetailsJson { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool HasProgress { get; set; }
        public string? ProgressStatus { get; set; }
        public int? WrongCount { get; set; }
        public DateTime? NextReviewDate { get; set; }
        public DateTime? LastReviewedDate { get; set; }
    }

    /// <summary>Cards of <paramref name="cards"/> that may be practised: not suspended for this user, term and meaning present.</summary>
    private IQueryable<PracticeRow> PracticeRows(IQueryable<Card> cards, int userId)
    {
        var progress = _db.LearningProgresses.AsNoTracking().Where(p => p.UserId == userId);
        return from c in cards.AsNoTracking()
               join p in progress on c.CardId equals p.CardId into joined
               from p in joined.DefaultIfEmpty()
               where (p == null || !p.IsSuspended) && c.Term.Trim() != string.Empty && c.Definition.Trim() != string.Empty
               select new PracticeRow
               {
                   CardId = c.CardId,
                   SetId = c.SetId,
                   Term = c.Term,
                   Definition = c.Definition,
                   Phonetic = c.Phonetic,
                   PhoneticUk = c.PhoneticUk,
                   MeaningEn = c.MeaningEn,
                   PartsOfSpeech = c.PartsOfSpeech,
                   Topic = c.Topic,
                   Cefr = c.Cefr,
                   Emoji = c.Emoji,
                   ImageUrl = c.ImageUrl,
                   Example = c.Example,
                   ExampleTranslation = c.ExampleTranslation,
                   DetailsJson = c.DetailsJson,
                   CreatedAt = c.CreatedAt,
                   HasProgress = p != null,
                   ProgressStatus = p == null ? null : p.Status,
                   WrongCount = p == null ? null : p.WrongCount,
                   NextReviewDate = p == null ? null : p.NextReviewDate,
                   LastReviewedDate = p == null ? null : p.LastReviewedDate,
               };
    }

    private IQueryable<Set> MineSets(int userId)
        => _db.Sets.Where(s => s.OwnerId == userId || _db.SetSubscriptions.Any(x => x.SetId == s.SetId && x.UserId == userId));

    private PracticeWordDto ToPracticeWord(PracticeRow r)
    {
        var details = CardDetailsCodec.Parse(r.DetailsJson, out _);
        var examples = details.Examples
            .Where(e => !string.IsNullOrWhiteSpace(e.Text))
            .Take(6)
            .Select(e => new PracticeExampleDto { Text = e.Text!.Trim(), Vi = string.IsNullOrWhiteSpace(e.Vi) ? null : e.Vi.Trim() })
            .ToList();
        if (examples.Count == 0 && !string.IsNullOrWhiteSpace(r.Example))
        {
            examples.Add(new PracticeExampleDto
            {
                Text = r.Example.Trim(),
                Vi = string.IsNullOrWhiteSpace(r.ExampleTranslation) ? null : r.ExampleTranslation.Trim(),
            });
        }

        var verbForms = details.VerbForms is { IsEmpty: false } vf
            ? new PracticeVerbFormsDto { Past = vf.Past, PastParticiple = vf.PastParticiple, Ing = vf.Ing, ThirdPerson = vf.ThirdPerson }
            : null;

        return new PracticeWordDto
        {
            Id = r.CardId,
            SetId = r.SetId,
            Term = r.Term.Trim(),
            Phonetic = r.Phonetic,
            PhoneticUk = r.PhoneticUk,
            Definition = r.Definition.Trim(),
            MeaningEn = r.MeaningEn,
            PartsOfSpeech = CardPartsOfSpeech.Parse(r.PartsOfSpeech).ToList(),
            Topic = r.Topic,
            Cefr = r.Cefr,
            Emoji = r.Emoji,
            ImageUrl = r.ImageUrl,
            Examples = examples,
            VerbForms = verbForms,
            Plural = string.IsNullOrWhiteSpace(details.Plural) ? null : details.Plural,
            Status = CardStatus.Classify(false, r.WrongCount, r.ProgressStatus, r.HasProgress),
            WrongCount = r.WrongCount ?? 0,
        };
    }

    // =====================================================================================
    // Session
    // =====================================================================================

    public async Task<PracticeSessionViewModel?> GetPracticeAsync(string mode, PracticeQuery query, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var info = PracticeModes.Find(mode);
        if (info == null)
        {
            return null;
        }

        var q = PracticeModes.Normalize(info.Key, query);
        var model = new PracticeSessionViewModel
        {
            Mode = info.Key,
            ModeLabel = info.Label,
            Hue = info.Hue,
            Query = q,
            CountsToSrs = q.Srs == "1",
            TimeFeature = info.TimeFeature,
            UserId = userId,
            ScopeKey = "mine",
            ScopeName = "Tất cả bộ của tôi",
        };

        IQueryable<Card> scopeCards;
        string? setName = null;
        if (q.Set.HasValue)
        {
            var setInfo = await ViewableSets(userId, isAdmin).AsNoTracking()
                .Where(s => s.SetId == q.Set.Value)
                .Select(s => new { s.SetId, s.SetName })
                .FirstOrDefaultAsync(ct);
            if (setInfo == null)
            {
                return null;
            }

            setName = setInfo.SetName;
            model.SetId = setInfo.SetId;
            model.ScopeKey = "set:" + setInfo.SetId;
            model.ScopeName = setInfo.SetName;
            scopeCards = _db.Cards.Where(c => c.SetId == setInfo.SetId);
        }
        else
        {
            scopeCards = _db.Cards.Where(c => MineSets(userId).Select(s => s.SetId).Contains(c.SetId));
        }

        var viewableIds = ViewableSets(userId, isAdmin).Select(s => s.SetId);
        var now = Now;

        List<PracticeRow> targets;
        if (q.WordIds.Count > 0)
        {
            var wordIds = q.WordIds;
            var found = await PracticeRows(_db.Cards.Where(c => wordIds.Contains(c.CardId) && viewableIds.Contains(c.SetId)), userId)
                .ToListAsync(ct);
            targets = found.OrderBy(r => wordIds.IndexOf(r.CardId)).ToList();
            model.ScopeKey = "words:" + (q.Set.HasValue ? q.Set.Value.ToString(CultureInfo.InvariantCulture) : "mine") + ":" + WordsHash(wordIds);
            if (!q.Set.HasValue)
            {
                model.ScopeName = "Các từ đã chọn";
            }
        }
        else
        {
            var rows = PracticeRows(scopeCards, userId);
            var n = q.N ?? PracticeModes.CountsFor(info.Key)[0];
            var take = n == 0 ? PracticeModes.FlashcardWholeSetCap : n;
            if (info.Key == PracticeModes.Test && (q.Types ?? string.Empty).Contains("cloze", StringComparison.Ordinal))
            {
                take = n * 2;
            }

            var ordered = q.Source switch
            {
                PracticeModes.SourceDue => rows.Where(r => r.HasProgress && r.NextReviewDate != null && r.NextReviewDate <= now)
                    .OrderBy(r => r.NextReviewDate).ThenBy(r => r.CardId),
                PracticeModes.SourceHard => rows.Where(r => r.HasProgress && (r.WrongCount ?? 0) >= 1)
                    .OrderByDescending(r => r.WrongCount).ThenByDescending(r => r.LastReviewedDate).ThenBy(r => r.CardId),
                PracticeModes.SourceRecent => rows.OrderBy(r => r.CreatedAt == null)
                    .ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.CardId),
                _ => q.Set.HasValue
                    ? rows.OrderBy(r => r.CreatedAt).ThenBy(r => r.CardId)
                    : rows.OrderByDescending(r => r.SetId).ThenBy(r => r.CreatedAt).ThenBy(r => r.CardId),
            };
            targets = await ordered.Take(take).ToListAsync(ct);
        }

        model.Targets = targets.Select(ToPracticeWord).ToList();
        model.Empty = model.Targets.Count == 0;
        model.EmptyReason = !model.Empty ? null
            : q.WordIds.Count > 0 ? "none"
            : q.Source is PracticeModes.SourceDue or PracticeModes.SourceHard ? q.Source : "none";

        BuildUrls(model, info.Key, q, setName);

        if (!model.Empty)
        {
            model.Pool = await LoadPoolAsync(targets, q, userId, isAdmin, viewableIds, ct);
        }

        return model;
    }

    private async Task<List<PracticeWordDto>> LoadPoolAsync(
        List<PracticeRow> targets, PracticeQuery q, int userId, bool isAdmin, IQueryable<int> viewableIds, CancellationToken ct)
    {
        var targetIds = targets.Select(t => t.CardId).ToList();
        IQueryable<Card> poolScope;
        if (q.Set.HasValue)
        {
            var setId = q.Set.Value;
            poolScope = _db.Cards.Where(c => c.SetId == setId);
        }
        else if (q.WordIds.Count > 0)
        {
            var setIds = targets.Select(t => t.SetId).Distinct().ToList();
            poolScope = _db.Cards.Where(c => setIds.Contains(c.SetId));
        }
        else
        {
            poolScope = _db.Cards.Where(c => MineSets(userId).Select(s => s.SetId).Contains(c.SetId));
        }

        var picked = await SamplePoolIdsAsync(poolScope, targetIds, userId, ct);

        // A tiny scope cannot supply distractors: top up from the other sets the user may view.
        if (picked.Count < 3 && (q.Set.HasValue || q.WordIds.Count > 0))
        {
            var exclude = targetIds.Concat(picked).ToList();
            var extra = await SamplePoolIdsAsync(_db.Cards.Where(c => viewableIds.Contains(c.SetId)), exclude, userId, ct);
            picked.AddRange(extra.Take(PracticeModes.PoolSize - picked.Count));
        }

        if (picked.Count == 0)
        {
            return new List<PracticeWordDto>();
        }

        var rows = await PracticeRows(_db.Cards.Where(c => picked.Contains(c.CardId)), userId).ToListAsync(ct);
        return rows.Select(ToPracticeWord).ToList();
    }

    /// <summary>Up to 200 usable card ids from <paramref name="scope"/>, sampled in memory (no ORDER BY RAND()).</summary>
    private async Task<List<int>> SamplePoolIdsAsync(IQueryable<Card> scope, IReadOnlyCollection<int> exclude, int userId, CancellationToken ct)
    {
        var ids = await PracticeRows(scope, userId)
            .Where(r => !exclude.Contains(r.CardId))
            .OrderBy(r => r.CardId)
            .Select(r => r.CardId)
            .Take(1000)
            .ToListAsync(ct);
        if (ids.Count <= PracticeModes.PoolSize)
        {
            return ids;
        }

        Random.Shared.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(ids));
        return ids.Take(PracticeModes.PoolSize).ToList();
    }

    private static string WordsHash(IReadOnlyList<int> ids)
    {
        var hash = 17;
        foreach (var id in ids)
        {
            hash = unchecked(hash * 31 + id);
        }

        return ((uint)hash).ToString("x", CultureInfo.InvariantCulture);
    }

    private static string BuildQueryString(string? mode, PracticeQuery q, bool includeFrom = true)
    {
        var sb = new StringBuilder();
        void Add(string key, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            sb.Append(sb.Length == 0 ? '?' : '&').Append(key).Append('=').Append(Uri.EscapeDataString(value));
        }

        if (q.Set.HasValue)
        {
            Add("set", q.Set.Value.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            Add("scope", q.Scope);
        }

        if (q.N.HasValue)
        {
            Add("n", q.N.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (q.WordIds.Count > 0)
        {
            Add("words", q.Words);
        }
        else if (q.Source != PracticeModes.SourceAll)
        {
            Add("source", q.Source);
        }

        Add("srs", q.Srs);
        if (mode == PracticeModes.Test)
        {
            Add("types", q.Types);
            Add("ask", q.Ask);
        }

        if (includeFrom)
        {
            Add("from", q.From);
        }

        return sb.ToString();
    }

    private static void BuildUrls(PracticeSessionViewModel model, string mode, PracticeQuery q, string? setName)
    {
        _ = setName;
        var setPart = q.Set.HasValue ? "?set=" + q.Set.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        model.HubUrl = "/Practice" + setPart;
        model.SetUrl = q.Set.HasValue ? "/Sets/" + q.Set.Value.ToString(CultureInfo.InvariantCulture) : null;
        model.ExitUrl = q.From switch
        {
            "set" when q.Set.HasValue => model.SetUrl!,
            "hard" => "/Practice/HardWords",
            _ => model.HubUrl,
        };
        model.AgainUrl = "/Practice/" + mode + BuildQueryString(mode, q);

        // Learn/Test links keep the same scope and words but use their own counts and no test options.
        var linkQuery = new PracticeQuery { Set = q.Set, Scope = q.Scope, Words = q.Words, Source = q.Source, Srs = q.Srs, From = q.From };
        model.TestUrl = "/Practice/test" + BuildQueryString(PracticeModes.Test, PracticeModes.Normalize(PracticeModes.Test, linkQuery));
        model.LearnUrl = "/Practice/learn" + BuildQueryString(PracticeModes.Learn, PracticeModes.Normalize(PracticeModes.Learn, linkQuery));
    }

    // =====================================================================================
    // Hub
    // =====================================================================================

    public async Task<PracticeHubViewModel> GetPracticeHubAsync(int? setId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var now = Now;
        var setQuery = MineSets(userId);
        List<int> extraIds = new();
        if (setId is > 0)
        {
            var viewable = await ViewableSets(userId, isAdmin).AsNoTracking()
                .Where(s => s.SetId == setId.Value).Select(s => s.SetId).FirstOrDefaultAsync(ct);
            if (viewable != 0)
            {
                extraIds.Add(viewable);
            }
        }

        var sets = await _db.Sets.AsNoTracking()
            .Where(s => setQuery.Select(x => x.SetId).Contains(s.SetId) || extraIds.Contains(s.SetId))
            .Select(s => new { s.SetId, s.SetName, s.CreatedAt })
            .ToListAsync(ct);

        var ids = sets.Select(s => s.SetId).ToList();
        var rows = await PracticeRows(_db.Cards.Where(c => ids.Contains(c.SetId)), userId)
            .Select(r => new { r.SetId, Due = r.HasProgress && r.NextReviewDate != null && r.NextReviewDate <= now })
            .ToListAsync(ct);
        var counts = rows.GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => (Words: g.Count(), Due: g.Count(x => x.Due)));

        var model = new PracticeHubViewModel { UserId = userId };
        foreach (var s in sets.OrderBy(s => s.CreatedAt == null).ThenByDescending(s => s.CreatedAt).ThenByDescending(s => s.SetId))
        {
            if (!counts.TryGetValue(s.SetId, out var c) || c.Words == 0)
            {
                continue;
            }

            model.Sets.Add(new PracticeHubSetDto { Id = s.SetId, Name = s.SetName, WordCount = c.Words, DueCount = c.Due });
        }

        if (setId.HasValue && model.Sets.Any(s => s.Id == setId.Value))
        {
            model.PreselectedSetId = setId.Value;
        }

        model.HardWordCount = await HardWordRows(null, userId, isAdmin).CountAsync(ct);
        return model;
    }

    // =====================================================================================
    // Hard words
    // =====================================================================================

    private IQueryable<HardWordRowDto> HardWordRows(int? setId, int userId, bool isAdmin)
    {
        var sets = ViewableSets(userId, isAdmin);
        if (setId.HasValue)
        {
            sets = sets.Where(s => s.SetId == setId.Value);
        }

        return from p in _db.LearningProgresses.AsNoTracking()
               where p.UserId == userId && !p.IsSuspended && (p.WrongCount ?? 0) >= 1
               join c in _db.Cards.AsNoTracking() on p.CardId equals c.CardId
               join s in sets.AsNoTracking() on c.SetId equals s.SetId
               where c.Term.Trim() != string.Empty && c.Definition.Trim() != string.Empty
               select new HardWordRowDto
               {
                   CardId = c.CardId,
                   SetId = s.SetId,
                   SetName = s.SetName,
                   Term = c.Term,
                   Phonetic = c.Phonetic,
                   Definition = c.Definition,
                   WrongCount = p.WrongCount ?? 0,
                   LastReviewed = p.LastReviewedDate,
               };
    }

    public async Task<HardWordsViewModel> GetHardWordsAsync(int? setId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var hub = await GetPracticeHubAsync(setId, userId, isAdmin, ct);
        // An unknown or hidden set id is ignored (no leak, no error): the page shows every viewable set instead.
        var effectiveSet = hub.PreselectedSetId ?? await HardSetIdAsync(setId, userId, isAdmin, ct);
        var rows = await HardWordRows(effectiveSet, userId, isAdmin)
            .OrderByDescending(r => r.WrongCount).ThenByDescending(r => r.LastReviewed).ThenBy(r => r.CardId)
            .Take(100)
            .ToListAsync(ct);
        return new HardWordsViewModel { SetId = effectiveSet, Sets = hub.Sets, Rows = rows };
    }

    private async Task<int?> HardSetIdAsync(int? setId, int userId, bool isAdmin, CancellationToken ct)
    {
        if (setId is not > 0)
        {
            return null;
        }

        var ok = await CanViewSetAsync(setId.Value, userId, isAdmin, ct);
        return ok ? setId : null;
    }
}
