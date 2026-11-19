using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Sets;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.Services.Vocabulary;

namespace TCTEnglish.Services.Onboarding;

/// <summary>
/// First-run onboarding: who is asked, the preview of the starter decks, and creating them as ordinary private personal sets.
/// Every query is scoped by the user id; the system vocabulary is only ever read.
/// </summary>
public sealed class OnboardingService : IOnboardingService
{
    /// <summary>Accounts created before this instant are never prompted automatically (they can still open the flow by hand).</summary>
    public static readonly DateTime DefaultEligibleFromUtc = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);

    public const string FolderName = "Lộ trình của tôi";
    public const int MaxPrompts = 2;
    public static readonly TimeSpan PromptGap = TimeSpan.FromHours(20);
    private const int MaxKnownTerms = 2000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly DbflashcardContext _db;
    private readonly TimeProvider _time;
    private readonly DateTime _eligibleFromUtc;

    public OnboardingService(DbflashcardContext db, TimeProvider time, DateTime? eligibleFromUtc = null)
    {
        _db = db;
        _time = time;
        _eligibleFromUtc = eligibleFromUtc ?? DefaultEligibleFromUtc;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    // =====================================================================================
    // State, prompts, banner
    // =====================================================================================

    public async Task<OnboardingState> GetStateAsync(int userId, CancellationToken ct = default)
    {
        var row = await EnsureRowAsync(userId, ct);
        if (row == null)
        {
            return new OnboardingState(false, OnboardingStatus.NotEligible, false, false);
        }

        var hasDecks = !string.IsNullOrEmpty(row.DecksJson) && row.DecksJson != "[]";
        var banner = row.Status == OnboardingStatus.Completed && hasDecks && row.BannerDismissedAt == null;
        return new OnboardingState(ShouldPrompt(row), row.Status, hasDecks, banner);
    }

    private bool ShouldPrompt(UserOnboarding row) =>
        row.Status == OnboardingStatus.Pending
        && row.PromptCount < MaxPrompts
        && (row.LastPromptedAt == null || Now - row.LastPromptedAt.Value >= PromptGap);

    public async Task MarkPromptedAsync(int userId, CancellationToken ct = default)
    {
        var row = await EnsureRowAsync(userId, ct);
        if (row == null || !ShouldPrompt(row))
        {
            return;
        }

        row.PromptCount++;
        row.LastPromptedAt = Now;
        row.UpdatedAt = Now;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> SkipAsync(int userId, CancellationToken ct = default)
    {
        var row = await EnsureRowAsync(userId, ct);
        if (row == null)
        {
            return OnboardingStatus.NotEligible;
        }

        if (row.Status != OnboardingStatus.Pending)
        {
            return row.Status;
        }

        if (row.PromptCount == 0)
        {
            row.PromptCount = 1; // a skip without a recorded prompt still counts as one
            row.LastPromptedAt = Now;
        }

        if (row.PromptCount >= MaxPrompts)
        {
            row.Status = OnboardingStatus.Skipped;
        }

        row.UpdatedAt = Now;
        await _db.SaveChangesAsync(ct);
        return row.Status;
    }

    public async Task DismissBannerAsync(int userId, CancellationToken ct = default)
    {
        var row = await _db.UserOnboardings.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (row == null || row.BannerDismissedAt != null)
        {
            return;
        }

        row.BannerDismissedAt = Now;
        row.UpdatedAt = Now;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The user's row, created on the first call: Pending for a new account that has no sets and no progress,
    /// NotEligible otherwise (so an existing user is never nagged). Null when the user does not exist.
    /// </summary>
    private async Task<UserOnboarding?> EnsureRowAsync(int userId, CancellationToken ct)
    {
        var row = await _db.UserOnboardings.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (row != null)
        {
            return row;
        }

        var user = await _db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => new { u.CreatedAt }).FirstOrDefaultAsync(ct);
        if (user == null)
        {
            return null;
        }

        var eligible = user.CreatedAt != null
            && user.CreatedAt.Value >= _eligibleFromUtc
            && !await _db.Sets.AnyAsync(s => s.OwnerId == userId, ct)
            && !await _db.LearningProgresses.AnyAsync(p => p.UserId == userId, ct);

        var now = Now;
        row = new UserOnboarding
        {
            UserId = userId,
            Status = eligible ? OnboardingStatus.Pending : OnboardingStatus.NotEligible,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.UserOnboardings.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request created the row first (one row per user): use that one.
            _db.ChangeTracker.Clear();
            return await _db.UserOnboardings.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        }

        return row;
    }

    // =====================================================================================
    // Catalogue (step 0 and step 2)
    // =====================================================================================

    /// <summary>The topics shown on the welcome screen: five sets that show the range of the library.</summary>
    private static readonly string[] WelcomeTopicKeys = { "A03", "C01", "B04", "A21", "A06" };

    public async Task<OnboardingCatalogView> GetCatalogAsync(CancellationToken ct = default)
    {
        var covers = await LoadCoversAsync(ct);
        var topics = OnboardingCatalog.Topics
            .Select(topic => new CatalogTopicView(
                topic.Key, topic.SetName, topic.Band, OnboardingCatalog.GoalsOfTopic(topic.Key), covers.GetValueOrDefault(topic.Key)))
            .ToList();
        var welcome = WelcomeTopicKeys.Select(key => covers.GetValueOrDefault(key)).Where(url => url != null).Select(url => url!).ToList();
        return new OnboardingCatalogView(OnboardingCatalog.Goals, topics, welcome);
    }

    /// <summary>The cover of each topic: the same rule as the Vocabulary courses (<see cref="VocabularyCoverCatalog"/>), two queries in all.</summary>
    private async Task<IReadOnlyDictionary<string, string>> LoadCoversAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var systemId = await _db.Users.AsNoTracking()
            .Where(u => u.Email == PersonalSetService.SystemOwnerEmail)
            .Select(u => (int?)u.UserId)
            .FirstOrDefaultAsync(ct);
        if (systemId == null)
        {
            return result;
        }

        var keyByName = OnboardingCatalog.Topics.ToDictionary(topic => topic.SetName, topic => topic.Key, StringComparer.Ordinal);
        var names = keyByName.Keys.ToList();
        var sets = await _db.Sets.AsNoTracking()
            .Where(s => s.OwnerId == systemId && names.Contains(s.SetName))
            .Select(s => new { s.SetId, s.SetName, Cards = s.Cards.Count })
            .ToListAsync(ct);
        var chosen = sets
            .GroupBy(s => s.SetName)
            .Select(group => group.OrderByDescending(s => s.Cards).ThenBy(s => s.SetId).First())
            .ToDictionary(s => s.SetId, s => s.SetName);
        if (chosen.Count == 0)
        {
            return result;
        }

        var setIds = chosen.Keys.ToList();
        var rows = await _db.Cards.AsNoTracking()
            .Where(card => setIds.Contains(card.SetId) && card.ImageUrl != null && card.ImageUrl != string.Empty)
            .OrderBy(card => card.CardId)
            .Select(card => new { card.SetId, card.CardId, card.Term, card.ImageUrl })
            .ToListAsync(ct);

        foreach (var group in rows.GroupBy(row => row.SetId))
        {
            var candidates = group
                .Select(row => (Row: row, Url: StudyService.SafeVocabularyImageUrl(row.ImageUrl)))
                .Where(item => item.Url != null)
                .Select(item => new CoverCandidate(item.Row.CardId, item.Row.Term ?? string.Empty, item.Url!))
                .ToList();
            var name = chosen[group.Key];
            var choice = VocabularyCoverCatalog.Pick(name, candidates);
            if (choice != null)
            {
                result[keyByName[name]] = choice.ImageUrl;
            }
        }

        return result;
    }

    // =====================================================================================
    // Preview
    // =====================================================================================

    private static string? ValidateChoices(OnboardingChoices? choices)
    {
        if (choices == null)
        {
            return "Dữ liệu không hợp lệ.";
        }

        var goals = choices.Goals ?? Array.Empty<string>();
        if (goals.Count < 1 || goals.Count > OnboardingCatalog.MaxGoals
            || goals.Distinct(StringComparer.Ordinal).Count() != goals.Count || !goals.All(OnboardingCatalog.IsGoal))
        {
            return "Hãy chọn từ 1 đến 3 mục tiêu.";
        }

        var topics = choices.Topics ?? Array.Empty<string>();
        if (topics.Count < OnboardingCatalog.MinTopics || topics.Count > OnboardingCatalog.MaxTopics
            || topics.Distinct(StringComparer.Ordinal).Count() != topics.Count || !topics.All(key => OnboardingCatalog.TryGetTopic(key, out _)))
        {
            return "Hãy chọn từ 3 đến 8 chủ đề.";
        }

        if (!OnboardingCatalog.Levels.Contains(choices.Level))
        {
            return "Trình độ không hợp lệ.";
        }

        return OnboardingCatalog.Paces.Contains(choices.Pace) ? null : "Số từ mỗi ngày không hợp lệ.";
    }

    public async Task<OnboardingResult<OnboardingPreview>> BuildPreviewAsync(int userId, OnboardingChoices choices, CancellationToken ct = default)
    {
        var error = ValidateChoices(choices);
        if (error != null)
        {
            return OnboardingResult<OnboardingPreview>.Invalid(error);
        }

        var row = await EnsureRowAsync(userId, ct);
        if (row == null)
        {
            return OnboardingResult<OnboardingPreview>.NotFound();
        }

        var built = await BuildDecksAsync(userId, choices, ReadKnownTerms(row), ct);
        if (built.Decks.Count == 0)
        {
            return OnboardingResult<OnboardingPreview>.Invalid("Chưa có từ phù hợp với lựa chọn này. Hãy thử chọn thêm chủ đề.");
        }

        return OnboardingResult<OnboardingPreview>.Ok(ToPreview(built.Decks, choices.Pace));
    }

    private sealed record BuiltDecks(IReadOnlyList<StarterDeck> Decks, IReadOnlyDictionary<int, OnboardingCard> CardsById);

    private async Task<BuiltDecks> BuildDecksAsync(int userId, OnboardingChoices choices, IReadOnlySet<string> knownTerms, CancellationToken ct)
    {
        var pool = await LoadPoolAsync(choices.Topics, ct);
        var ownedTerms = await LoadOwnedTermsAsync(userId, ct);
        var existingNames = await _db.Sets.AsNoTracking().Where(s => s.OwnerId == userId).Select(s => s.SetName).ToListAsync(ct);

        var request = new DeckBuildRequest(choices.Goals, choices.Topics, choices.Level, choices.Pace);
        var decks = OnboardingDeckBuilder.Build(request, pool, ownedTerms, knownTerms, existingNames);
        var byId = decks.SelectMany(deck => deck.Cards).GroupBy(card => card.CardId).ToDictionary(group => group.Key, group => group.First());
        return new BuiltDecks(decks, byId);
    }

    private static OnboardingPreview ToPreview(IReadOnlyList<StarterDeck> decks, int pace)
    {
        var shown = decks
            .Select(deck => new PreviewDeck(
                deck.Key, deck.Name, deck.Goal, deck.TopicKeys,
                deck.Cards.Select(card => new PreviewCard(card.CardId, card.Term, card.Meaning, card.ImageUrl, card.Cefr)).ToList()))
            .ToList();
        var total = shown.Sum(deck => deck.Cards.Count);
        return new OnboardingPreview(shown, total, OnboardingCatalog.EstimateDays(total, pace));
    }

    /// <summary>The cards of the chosen topics. A topic is the system set with that exact name; when two system sets share a name (an older set), the one with more cards is used.</summary>
    private async Task<IReadOnlyList<OnboardingCard>> LoadPoolAsync(IReadOnlyList<string> topicKeys, CancellationToken ct)
    {
        var systemId = await _db.Users.AsNoTracking()
            .Where(u => u.Email == PersonalSetService.SystemOwnerEmail)
            .Select(u => (int?)u.UserId)
            .FirstOrDefaultAsync(ct);
        if (systemId == null)
        {
            return Array.Empty<OnboardingCard>();
        }

        var keyByName = topicKeys
            .Select(OnboardingCatalog.Topic)
            .GroupBy(topic => topic.SetName)
            .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);
        var names = keyByName.Keys.ToList();

        var sets = await _db.Sets.AsNoTracking()
            .Where(s => s.OwnerId == systemId && names.Contains(s.SetName))
            .Select(s => new { s.SetId, s.SetName, Cards = s.Cards.Count })
            .ToListAsync(ct);
        var chosen = sets
            .GroupBy(s => s.SetName)
            .Select(group => group.OrderByDescending(s => s.Cards).ThenBy(s => s.SetId).First())
            .ToDictionary(s => s.SetId, s => keyByName[s.SetName]);
        if (chosen.Count == 0)
        {
            return Array.Empty<OnboardingCard>();
        }

        var setIds = chosen.Keys.ToList();
        var cards = await _db.Cards.AsNoTracking()
            .Where(card => setIds.Contains(card.SetId))
            .OrderBy(card => card.CardId)
            .Select(card => new { card.CardId, card.SetId, card.Term, card.Definition, card.ImageUrl, card.Cefr })
            .ToListAsync(ct);

        return cards
            .Select(card => new OnboardingCard(
                card.CardId, chosen[card.SetId], card.Term, card.Definition, StudyService.SafeVocabularyImageUrl(card.ImageUrl), card.Cefr))
            .ToList();
    }

    private async Task<IReadOnlySet<string>> LoadOwnedTermsAsync(int userId, CancellationToken ct)
    {
        var terms = await _db.Cards.AsNoTracking()
            .Where(card => card.Set.OwnerId == userId)
            .Select(card => card.Term)
            .Distinct()
            .Take(20000)
            .ToListAsync(ct);
        return terms.Select(OnboardingDeckBuilder.Lemma).ToHashSet(StringComparer.Ordinal);
    }

    // =====================================================================================
    // Complete
    // =====================================================================================

    public async Task<OnboardingResult<CompleteResult>> CompleteAsync(int userId, CompleteRequest request, CancellationToken ct = default)
    {
        if (request == null || request.RequestId == Guid.Empty)
        {
            return OnboardingResult<CompleteResult>.Invalid("Yêu cầu không hợp lệ.");
        }

        var error = ValidateChoices(request.Choices);
        if (error != null)
        {
            return OnboardingResult<CompleteResult>.Invalid(error);
        }

        var row = await EnsureRowAsync(userId, ct);
        if (row == null)
        {
            return OnboardingResult<CompleteResult>.NotFound();
        }

        var decksOff = (request.DecksOff ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToList();
        var knownIds = (request.KnownCardIds ?? Array.Empty<int>()).Distinct().ToList();
        var hash = Hash("complete", string.Join(",", request.Choices.Goals), string.Join(",", request.Choices.Topics),
            request.Choices.Level, request.Choices.Pace.ToString(), string.Join(",", decksOff.OrderBy(x => x, StringComparer.Ordinal)),
            string.Join(",", knownIds.OrderBy(x => x)));

        var replay = Replay<CompleteResult>(row, request.RequestId, hash);
        if (replay != null)
        {
            return replay;
        }

        var built = await BuildDecksAsync(userId, request.Choices, ReadKnownTerms(row), ct);
        if (built.Decks.Count == 0)
        {
            return OnboardingResult<CompleteResult>.Invalid("Chưa có từ phù hợp với lựa chọn này. Hãy thử chọn thêm chủ đề.");
        }

        var deckKeys = built.Decks.Select(deck => deck.Key).ToHashSet(StringComparer.Ordinal);
        if (decksOff.Any(key => !deckKeys.Contains(key)) || knownIds.Any(id => !built.CardsById.ContainsKey(id)))
        {
            return OnboardingResult<CompleteResult>.Invalid("Lựa chọn không khớp với bản xem trước. Hãy xem trước lại.");
        }

        var knownSet = knownIds.ToHashSet();
        var kept = built.Decks
            .Where(deck => !decksOff.Contains(deck.Key))
            .Select(deck => (Deck: deck, Cards: deck.Cards.Where(card => !knownSet.Contains(card.CardId)).ToList()))
            .Where(item => item.Cards.Count > 0)
            .ToList();
        if (kept.Count == 0)
        {
            return OnboardingResult<CompleteResult>.Invalid("Hãy giữ lại ít nhất một bộ thẻ.");
        }

        if (await _db.Sets.CountAsync(s => s.OwnerId == userId, ct) + kept.Count > PersonalSetService.MaxOwnedSets)
        {
            return OnboardingResult<CompleteResult>.Invalid(
                $"Bạn đã có quá nhiều bộ thẻ (tối đa {PersonalSetService.MaxOwnedSets}). Hãy xóa bớt bộ cũ rồi thử lại.");
        }

        var sourceIds = kept.SelectMany(item => item.Cards.Select(card => card.CardId)).Distinct().ToList();
        var sources = (await _db.Cards.AsNoTracking().Where(card => sourceIds.Contains(card.CardId)).ToListAsync(ct))
            .ToDictionary(card => card.CardId);

        var now = Now;
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (!await ClaimRequestAsync(userId, request.RequestId, hash, ct))
            {
                await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return await ReplayAfterLostRaceAsync<CompleteResult>(userId, request.RequestId, hash, ct);
            }

            var folderId = await FindOrCreateFolderAsync(userId, ct);
            var created = new List<(Set Set, StarterDeck Deck)>();
            foreach (var item in kept)
            {
                var set = new Set
                {
                    SetName = item.Deck.Name,
                    Description = item.Deck.Description,
                    FolderId = folderId,
                    OwnerId = userId,
                    Visibility = SetVisibility.Private,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                foreach (var card in item.Cards)
                {
                    set.Cards.Add(PersonalSetService.CopyCard(sources[card.CardId], now, keepPersonalNote: false));
                }

                _db.Sets.Add(set);
                created.Add((set, item.Deck));
            }

            await _db.SaveChangesAsync(ct);

            var result = new CompleteResult(
                folderId,
                created.Select(item => new CreatedDeck(item.Set.SetId, item.Set.SetName, item.Set.Cards.Count)).ToList(),
                request.Choices.Pace);

            var refs = ReadDecks(row.DecksJson).ToList();
            refs.AddRange(created.Select(item => new OnboardingDeckRef(item.Set.SetId, item.Deck.Goal, item.Deck.TopicKeys)));

            var known = ReadKnownTerms(row).ToHashSet(StringComparer.Ordinal);
            foreach (var id in knownIds)
            {
                known.Add(OnboardingDeckBuilder.Lemma(built.CardsById[id].Term));
            }

            await _db.Entry(row).ReloadAsync(ct);
            row.Status = OnboardingStatus.Completed;
            row.CompletedAt = now;
            row.GoalsJson = JsonSerializer.Serialize(request.Choices.Goals, Json);
            row.TopicKeysJson = JsonSerializer.Serialize(request.Choices.Topics, Json);
            row.Level = request.Choices.Level;
            row.DailyNewWords = request.Choices.Pace;
            row.KnownTermsJson = JsonSerializer.Serialize(known.Take(MaxKnownTerms).OrderBy(x => x, StringComparer.Ordinal), Json);
            row.DecksJson = JsonSerializer.Serialize(refs, Json);
            row.FolderId = folderId;
            row.LastResultJson = JsonSerializer.Serialize(result, Json);
            row.BannerDismissedAt = null;
            row.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return OnboardingResult<CompleteResult>.Ok(result);
        }
        catch
        {
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>An existing folder with exactly this name (ignoring case and edge spaces) or a new one.</summary>
    private async Task<int> FindOrCreateFolderAsync(int userId, CancellationToken ct)
    {
        var folders = await _db.Folders.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => new { f.FolderId, f.FolderName })
            .ToListAsync(ct);
        var existing = folders.FirstOrDefault(f => string.Equals(f.FolderName.Trim(), FolderName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing.FolderId;
        }

        var folder = new Folder { FolderName = FolderName, UserId = userId, ParentFolderId = null };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync(ct);
        return folder.FolderId;
    }

    // =====================================================================================
    // Refill
    // =====================================================================================

    public async Task<OnboardingResult<RefillResult>> RefillAsync(int userId, int setId, int count, Guid requestId, CancellationToken ct = default)
    {
        if (requestId == Guid.Empty || count < 1 || count > 20)
        {
            return OnboardingResult<RefillResult>.Invalid("Yêu cầu không hợp lệ.");
        }

        var row = await EnsureRowAsync(userId, ct);
        var deck = row == null ? null : ReadDecks(row.DecksJson).FirstOrDefault(d => d.SetId == setId);
        if (row == null || deck == null)
        {
            return OnboardingResult<RefillResult>.NotFound();
        }

        var set = await _db.Sets.FirstOrDefaultAsync(s => s.SetId == setId && s.OwnerId == userId, ct);
        if (set == null)
        {
            return OnboardingResult<RefillResult>.NotFound();
        }

        var hash = Hash("refill", setId.ToString(), count.ToString());
        var replay = Replay<RefillResult>(row, requestId, hash);
        if (replay != null)
        {
            return replay;
        }

        var pool = await LoadPoolAsync(deck.TopicKeys, ct);
        var used = (await LoadOwnedTermsAsync(userId, ct)).ToHashSet(StringComparer.Ordinal);
        used.UnionWith(ReadKnownTerms(row));
        var picked = OnboardingDeckBuilder.PickNext(deck.TopicKeys, row.Level ?? "A2", pool, used, count);

        var sourceIds = picked.Select(card => card.CardId).ToList();
        var sources = sourceIds.Count == 0
            ? new Dictionary<int, Card>()
            : (await _db.Cards.AsNoTracking().Where(card => sourceIds.Contains(card.CardId)).ToListAsync(ct)).ToDictionary(card => card.CardId);

        var now = Now;
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (!await ClaimRequestAsync(userId, requestId, hash, ct))
            {
                await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return await ReplayAfterLostRaceAsync<RefillResult>(userId, requestId, hash, ct);
            }

            foreach (var card in picked)
            {
                var copy = PersonalSetService.CopyCard(sources[card.CardId], now, keepPersonalNote: false);
                copy.SetId = set.SetId;
                _db.Cards.Add(copy);
            }

            set.UpdatedAt = now;
            var result = new RefillResult(picked.Count, picked.Count < count);
            await _db.SaveChangesAsync(ct);

            await _db.Entry(row).ReloadAsync(ct);
            row.LastResultJson = JsonSerializer.Serialize(result, Json);
            row.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return OnboardingResult<RefillResult>.Ok(result);
        }
        catch
        {
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    // =====================================================================================
    // Idempotency
    // =====================================================================================

    private static string Hash(params string[] parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\u001f", parts)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>The stored result when this request id was already handled, a conflict when the payload differs, null when it is new.</summary>
    private static OnboardingResult<T>? Replay<T>(UserOnboarding row, Guid requestId, string hash)
    {
        if (!string.Equals(row.LastRequestId, requestId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.Equals(row.LastRequestHash, hash, StringComparison.Ordinal))
        {
            return OnboardingResult<T>.Conflict("Yêu cầu này đã được dùng với nội dung khác.");
        }

        var stored = string.IsNullOrEmpty(row.LastResultJson) ? default : JsonSerializer.Deserialize<T>(row.LastResultJson, Json);
        return stored == null
            ? OnboardingResult<T>.Invalid("Yêu cầu đang được xử lý. Hãy thử lại sau giây lát.")
            : OnboardingResult<T>.Ok(stored);
    }

    /// <summary>
    /// Takes the request id on the user's row inside the transaction. The row lock makes a second request with the same id wait,
    /// then see that it was already claimed (zero rows updated), so a double submit never creates anything twice.
    /// </summary>
    private async Task<bool> ClaimRequestAsync(int userId, Guid requestId, string hash, CancellationToken ct)
    {
        var id = requestId.ToString();
        var affected = await _db.UserOnboardings
            .Where(x => x.UserId == userId && (x.LastRequestId == null || x.LastRequestId != id))
            .ExecuteUpdateAsync(set => set
                .SetProperty(x => x.LastRequestId, id)
                .SetProperty(x => x.LastRequestHash, hash)
                .SetProperty(x => x.LastResultJson, (string?)null), ct);
        return affected == 1;
    }

    private async Task<OnboardingResult<T>> ReplayAfterLostRaceAsync<T>(int userId, Guid requestId, string hash, CancellationToken ct)
    {
        var row = await _db.UserOnboardings.AsNoTracking().FirstAsync(x => x.UserId == userId, ct);
        return Replay<T>(row, requestId, hash) ?? OnboardingResult<T>.Invalid("Yêu cầu đang được xử lý. Hãy thử lại sau giây lát.");
    }

    // =====================================================================================
    // Stored lists
    // =====================================================================================

    private static IReadOnlySet<string> ReadKnownTerms(UserOnboarding row)
    {
        if (string.IsNullOrEmpty(row.KnownTermsJson))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            return (JsonSerializer.Deserialize<List<string>>(row.KnownTermsJson, Json) ?? new List<string>())
                .Select(OnboardingDeckBuilder.Lemma)
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static IReadOnlyList<OnboardingDeckRef> ReadDecks(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return Array.Empty<OnboardingDeckRef>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<OnboardingDeckRef>>(json, Json) ?? new List<OnboardingDeckRef>();
        }
        catch (JsonException)
        {
            return Array.Empty<OnboardingDeckRef>();
        }
    }
}
