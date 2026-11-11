using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Sets;

public sealed partial class PersonalSetService
{
    private const int MaxBatch = 30;
    private const int MaxBulkIds = 500;
    private const string SlugAlphabet = "abcdefghijkmnpqrstuvwxyz23456789";
    private const long MaxImageBytes = 5 * 1024 * 1024;

    // =====================================================================================
    // Sets
    // =====================================================================================

    public async Task<OperationResult<int>> CreateSetAsync(SetInfoFormViewModel input, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var name = input.SetName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return OperationResult<int>.Invalid("Nhập tên bộ thẻ", new Dictionary<string, string> { ["SetName"] = "Nhập tên bộ thẻ" });
        }

        if (name.Length > 255)
        {
            return OperationResult<int>.Invalid("Tối đa 255 ký tự", new Dictionary<string, string> { ["SetName"] = "Tối đa 255 ký tự" });
        }

        var description = Trim(input.Description, 500);
        if (input.FolderId.HasValue && !await ManageableFolders(userId, isAdmin).AnyAsync(f => f.FolderId == input.FolderId.Value, ct))
        {
            return OperationResult<int>.NotFound();
        }

        if (await _db.Sets.CountAsync(s => s.OwnerId == userId, ct) >= MaxOwnedSets)
        {
            return OperationResult<int>.Invalid($"Bạn đã có quá nhiều bộ thẻ (tối đa {MaxOwnedSets}).");
        }

        var now = Now;
        var set = new Set
        {
            SetName = name,
            Description = description,
            FolderId = input.FolderId,
            OwnerId = userId,
            Visibility = SetVisibility.Normalize(input.Visibility),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Sets.Add(set);
        await _db.SaveChangesAsync(ct);
        return OperationResult<int>.Success(set.SetId);
    }

    public async Task<OperationResult> UpdateSetInfoAsync(int setId, SetInfoFormViewModel input, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var set = await ManageableSets(userId, isAdmin).FirstOrDefaultAsync(s => s.SetId == setId, ct);
        if (set == null)
        {
            return OperationResult.NotFound();
        }

        var name = input.SetName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return OperationResult.Invalid("Nhập tên bộ thẻ", new Dictionary<string, string> { ["SetName"] = "Nhập tên bộ thẻ" });
        }

        if (name.Length > 255)
        {
            return OperationResult.Invalid("Tối đa 255 ký tự", new Dictionary<string, string> { ["SetName"] = "Tối đa 255 ký tự" });
        }

        if (input.FolderId.HasValue && !await ManageableFolders(userId, isAdmin).AnyAsync(f => f.FolderId == input.FolderId.Value, ct))
        {
            return OperationResult.NotFound();
        }

        set.SetName = name;
        set.Description = Trim(input.Description, 500);
        set.Visibility = SetVisibility.Normalize(input.Visibility);
        set.FolderId = input.FolderId;
        set.UpdatedAt = Now;
        await _db.SaveChangesAsync(ct);
        return OperationResult.Success();
    }

    public async Task<OperationResult> DeleteSetAsync(int setId, int userId, bool isAdmin, string? confirmName = null, CancellationToken ct = default)
    {
        var set = await ManageableSets(userId, isAdmin).AsNoTracking()
            .Where(s => s.SetId == setId)
            .Select(s => new { s.SetId, s.SetName })
            .FirstOrDefaultAsync(ct);
        if (set == null)
        {
            return OperationResult.NotFound();
        }

        if (confirmName != null && !string.Equals(confirmName.Trim(), set.SetName.Trim(), StringComparison.Ordinal))
        {
            return OperationResult.Invalid("Tên xác nhận không khớp.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await DeleteProgressForSetAsync(setId, ct);
        await _db.Cards.Where(c => c.SetId == setId).ExecuteDeleteAsync(ct);
        await _db.SetSubscriptions.Where(x => x.SetId == setId).ExecuteDeleteAsync(ct);
        await _db.Sets.Where(s => s.SetId == setId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        _db.ChangeTracker.Clear();
        return OperationResult.Success();
    }

    private Task<int> DeleteProgressForSetAsync(int setId, CancellationToken ct)
        => _db.LearningProgresses
            .Where(p => _db.Cards.Any(c => c.CardId == p.CardId && c.SetId == setId))
            .ExecuteDeleteAsync(ct);

    // =====================================================================================
    // Cards
    // =====================================================================================

    public async Task<OperationResult<CardBatchResultDto>> CreateCardsBatchAsync(
        int setId, IReadOnlyList<CardInputDto> cards, int userId, bool isAdmin, CancellationToken ct = default)
    {
        if (cards == null || cards.Count == 0)
        {
            return OperationResult<CardBatchResultDto>.Invalid("Chưa có thẻ nào để lưu.");
        }

        if (cards.Count > MaxBatch)
        {
            return OperationResult<CardBatchResultDto>.Invalid($"Mỗi lần tối đa {MaxBatch} thẻ.");
        }

        var set = await ManageableSets(userId, isAdmin).FirstOrDefaultAsync(s => s.SetId == setId, ct);
        if (set == null)
        {
            return OperationResult<CardBatchResultDto>.NotFound();
        }

        var result = new CardBatchResultDto();
        var valid = new List<CardData>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in cards)
        {
            var normalized = CardInputNormalizer.Normalize(input);
            if (!normalized.IsValid)
            {
                result.Invalid.Add(string.IsNullOrWhiteSpace(input?.Term) ? "(trống)" : input!.Term!.Trim());
                continue;
            }

            if (!seen.Add(normalized.Data.Term.ToLowerInvariant()))
            {
                result.Skipped.Add(normalized.Data.Term);
                continue;
            }

            valid.Add(normalized.Data);
        }

        if (valid.Count > 0)
        {
            var lowers = valid.Select(v => v.Term.ToLowerInvariant()).ToList();
            var existing = (await _db.Cards.AsNoTracking()
                    .Where(c => c.SetId == setId && lowers.Contains(c.Term.ToLower()))
                    .Select(c => c.Term)
                    .ToListAsync(ct))
                .Select(t => t.Trim().ToLowerInvariant())
                .ToHashSet(StringComparer.Ordinal);

            var now = Now;
            var added = new List<Card>();
            foreach (var data in valid)
            {
                if (existing.Contains(data.Term.ToLowerInvariant()))
                {
                    result.Skipped.Add(data.Term);
                    continue;
                }

                await ResolveExternalImageAsync(data, ct);
                var card = new Card { SetId = setId, CreatedAt = now, UpdatedAt = now };
                ApplyData(card, data);
                _db.Cards.Add(card);
                added.Add(card);
            }

            if (added.Count > 0)
            {
                set.UpdatedAt = now;
                await _db.SaveChangesAsync(ct);
                result.Inserted.AddRange(added.Select(c => new CardInsertedDto { Id = c.CardId, Term = c.Term }));
            }
        }

        return OperationResult<CardBatchResultDto>.Success(result);
    }

    private static void ApplyData(Card card, CardData data)
    {
        card.Term = data.Term;
        card.Definition = data.Definition;
        card.Phonetic = data.Phonetic;
        card.PhoneticUk = data.PhoneticUk;
        card.MeaningEn = data.MeaningEn;
        card.PartsOfSpeech = data.PartsOfSpeech;
        card.Topic = data.Topic;
        card.Emoji = data.Emoji;
        card.Cefr = data.Cefr;
        card.PersonalNote = data.PersonalNote;
        card.ImageUrl = data.ImageUrl;
        card.ImageSourceJson = data.ImageSourceJson;
        card.Example = data.Example;
        card.ExampleTranslation = data.ExampleTranslation;
        card.DetailsJson = data.DetailsJson;
        card.AiGenerated = data.AiGenerated;
    }

    /// <summary>A Pixabay https URL is downloaded to our own storage; any failure stores no external URL.</summary>
    private async Task ResolveExternalImageAsync(CardData data, CancellationToken ct)
    {
        if (data.ImageUrl == null || !data.ImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var local = await ImportExternalImageAsync(data.ImageUrl, ct);
        data.ImageUrl = local;
        if (local == null)
        {
            data.ImageSourceJson = null;
        }
    }

    public async Task<OperationResult> UpdateCardAsync(int cardId, CardInputDto input, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.CardId == cardId, ct);
        if (card == null || !await CanEditSetAsync(card.SetId, userId, isAdmin, ct))
        {
            return OperationResult.NotFound();
        }

        var normalized = CardInputNormalizer.Normalize(input);
        if (!normalized.IsValid)
        {
            return OperationResult.Invalid("Dữ liệu chưa hợp lệ", normalized.FieldErrors);
        }

        var lower = normalized.Data.Term.ToLowerInvariant();
        var duplicate = await _db.Cards.AsNoTracking()
            .AnyAsync(c => c.SetId == card.SetId && c.CardId != cardId && c.Term.ToLower() == lower, ct);
        if (duplicate)
        {
            return OperationResult.Invalid("Đã có trong bộ này", new Dictionary<string, string> { ["Term"] = "Đã có trong bộ này" });
        }

        if (normalized.Data.ImageUrl != card.ImageUrl)
        {
            await ResolveExternalImageAsync(normalized.Data, ct);
        }

        var now = Now;
        ApplyData(card, normalized.Data);
        card.UpdatedAt = now;
        var set = await _db.Sets.FirstAsync(s => s.SetId == card.SetId, ct);
        set.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return OperationResult.Success();
    }

    public async Task<OperationResult<int>> DeleteCardAsync(int cardId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var card = await _db.Cards.AsNoTracking().Where(c => c.CardId == cardId).Select(c => new { c.CardId, c.SetId }).FirstOrDefaultAsync(ct);
        if (card == null || !await CanEditSetAsync(card.SetId, userId, isAdmin, ct))
        {
            return OperationResult<int>.NotFound();
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.LearningProgresses.Where(p => p.CardId == cardId).ExecuteDeleteAsync(ct);
        await _db.Cards.Where(c => c.CardId == cardId).ExecuteDeleteAsync(ct);
        await _db.Sets.Where(s => s.SetId == card.SetId).ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, Now), ct);
        await transaction.CommitAsync(ct);
        return OperationResult<int>.Success(card.SetId);
    }

    public async Task<OperationResult<bool>> SetSuspendedAsync(int cardId, bool suspended, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var setId = await _db.Cards.AsNoTracking().Where(c => c.CardId == cardId).Select(c => (int?)c.SetId).FirstOrDefaultAsync(ct);
        if (setId == null || !await CanViewSetAsync(setId.Value, userId, isAdmin, ct))
        {
            return OperationResult<bool>.NotFound();
        }

        await UpsertSuspendedAsync(userId, new[] { cardId }, suspended, ct);
        await _db.SaveChangesAsync(ct);
        return OperationResult<bool>.Success(suspended);
    }

    /// <summary>Creates missing progress rows with no review history (Status null) and flips the flag per user.</summary>
    private async Task UpsertSuspendedAsync(int userId, IReadOnlyCollection<int> cardIds, bool suspended, CancellationToken ct)
    {
        var existing = await _db.LearningProgresses
            .Where(p => p.UserId == userId && cardIds.Contains(p.CardId))
            .ToListAsync(ct);
        foreach (var progress in existing)
        {
            progress.IsSuspended = suspended;
        }

        var known = existing.Select(p => p.CardId).ToHashSet();
        foreach (var id in cardIds.Where(id => !known.Contains(id)))
        {
            _db.LearningProgresses.Add(new LearningProgress
            {
                UserId = userId,
                CardId = id,
                Status = null,
                WrongCount = 0,
                RepetitionCount = 0,
                IsSuspended = suspended,
            });
        }
    }

    public async Task<OperationResult<CardBulkResultDto>> BulkCardsAsync(int setId, CardBulkRequest request, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var ids = (request.CardIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0 || ids.Count > MaxBulkIds)
        {
            return OperationResult<CardBulkResultDto>.Invalid($"Chọn từ 1 đến {MaxBulkIds} từ.");
        }

        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("move" or "topic" or "suspend" or "delete"))
        {
            return OperationResult<CardBulkResultDto>.Invalid("Thao tác không hợp lệ.");
        }

        var canView = await CanViewSetAsync(setId, userId, isAdmin, ct);
        if (!canView)
        {
            return OperationResult<CardBulkResultDto>.NotFound();
        }

        var canEdit = await CanEditSetAsync(setId, userId, isAdmin, ct);
        if (action != "suspend" && !canEdit)
        {
            return OperationResult<CardBulkResultDto>.NotFound();
        }

        // Ids that do not belong to this set are ignored silently.
        var inSet = await _db.Cards.AsNoTracking()
            .Where(c => c.SetId == setId && ids.Contains(c.CardId))
            .OrderBy(c => c.CardId)
            .Select(c => new { c.CardId, c.Term })
            .ToListAsync(ct);
        var cardIds = inSet.Select(c => c.CardId).ToList();
        const string nothing = "Không có từ nào được cập nhật.";
        if (cardIds.Count == 0)
        {
            return OperationResult<CardBulkResultDto>.NotFound(nothing);
        }

        var now = Now;
        switch (action)
        {
            case "suspend":
            {
                if (request.Suspended == null)
                {
                    return OperationResult<CardBulkResultDto>.Invalid("Thiếu trạng thái tạm ẩn.");
                }

                await UpsertSuspendedAsync(userId, cardIds, request.Suspended.Value, ct);
                await _db.SaveChangesAsync(ct);
                return OperationResult<CardBulkResultDto>.Success(new CardBulkResultDto { Count = cardIds.Count });
            }
            case "topic":
            {
                var topic = Trim(request.Topic, 60);
                if (request.Topic != null && request.Topic.Trim().Length > 60)
                {
                    return OperationResult<CardBulkResultDto>.Invalid("Tối đa 60 ký tự");
                }

                var updated = await _db.Cards.Where(c => c.SetId == setId && cardIds.Contains(c.CardId))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Topic, topic).SetProperty(c => c.UpdatedAt, now), ct);
                await _db.Sets.Where(s => s.SetId == setId).ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, now), ct);
                return updated == 0
                    ? OperationResult<CardBulkResultDto>.NotFound(nothing)
                    : OperationResult<CardBulkResultDto>.Success(new CardBulkResultDto { Count = updated });
            }
            case "delete":
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(ct);
                await _db.LearningProgresses.Where(p => cardIds.Contains(p.CardId)).ExecuteDeleteAsync(ct);
                var deleted = await _db.Cards.Where(c => c.SetId == setId && cardIds.Contains(c.CardId)).ExecuteDeleteAsync(ct);
                await _db.Sets.Where(s => s.SetId == setId).ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, now), ct);
                await transaction.CommitAsync(ct);
                return deleted == 0
                    ? OperationResult<CardBulkResultDto>.NotFound(nothing)
                    : OperationResult<CardBulkResultDto>.Success(new CardBulkResultDto { Count = deleted });
            }
            default: // move
            {
                if (request.TargetSetId is not int target || target == setId)
                {
                    return OperationResult<CardBulkResultDto>.Invalid("Chọn một bộ thẻ khác để chuyển.");
                }

                var targetInfo = await ManageableSets(userId, isAdmin).AsNoTracking()
                    .Where(s => s.SetId == target)
                    .Select(s => new { s.SetId, s.SetName })
                    .FirstOrDefaultAsync(ct);
                if (targetInfo == null)
                {
                    return OperationResult<CardBulkResultDto>.NotFound();
                }

                var targetTerms = (await _db.Cards.AsNoTracking().Where(c => c.SetId == target).Select(c => c.Term).ToListAsync(ct))
                    .Select(t => t.Trim().ToLowerInvariant())
                    .ToHashSet(StringComparer.Ordinal);

                var movable = new List<int>();
                var skipped = 0;
                foreach (var card in inSet)
                {
                    if (targetTerms.Add(card.Term.Trim().ToLowerInvariant()))
                    {
                        movable.Add(card.CardId);
                    }
                    else
                    {
                        skipped++;
                    }
                }

                if (movable.Count == 0)
                {
                    return OperationResult<CardBulkResultDto>.Invalid($"Bộ {targetInfo.SetName} đã có {skipped} từ trong số này");
                }

                // Progress is keyed by CardId, so it moves with the card.
                await using var transaction = await _db.Database.BeginTransactionAsync(ct);
                var moved = await _db.Cards.Where(c => c.SetId == setId && movable.Contains(c.CardId))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.SetId, target).SetProperty(c => c.UpdatedAt, now), ct);
                await _db.Sets.Where(s => s.SetId == setId || s.SetId == target)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, now), ct);
                await transaction.CommitAsync(ct);
                return OperationResult<CardBulkResultDto>.Success(new CardBulkResultDto { Count = moved, Skipped = skipped > 0 ? skipped : null });
            }
        }
    }

    // =====================================================================================
    // Sharing: link, follow, clone, samples
    // =====================================================================================

    public async Task<OperationResult<string?>> SetShareLinkAsync(int setId, bool enabled, bool regenerate, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var set = await ManageableSets(userId, isAdmin).FirstOrDefaultAsync(s => s.SetId == setId, ct);
        if (set == null)
        {
            return OperationResult<string?>.NotFound();
        }

        if (!enabled)
        {
            set.ShareSlug = null;
            await _db.SaveChangesAsync(ct);
            return OperationResult<string?>.Success(null);
        }

        if (set.ShareSlug != null && !regenerate)
        {
            return OperationResult<string?>.Success(set.ShareSlug);
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            set.ShareSlug = NewSlug();
            try
            {
                await _db.SaveChangesAsync(ct);
                return OperationResult<string?>.Success(set.ShareSlug);
            }
            catch (DbUpdateException ex) when (RelationalUniqueConflict.IsUnique(ex))
            {
                // Extremely unlikely collision: pick another slug and retry.
            }
        }

        return OperationResult<string?>.Invalid("Không tạo được link chia sẻ. Thử lại sau.");
    }

    private static string NewSlug()
    {
        var chars = new char[10];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = SlugAlphabet[RandomNumberGenerator.GetInt32(SlugAlphabet.Length)];
        }

        return new string(chars);
    }

    private async Task<(int SetId, int OwnerId)?> ResolveShareableAsync(int? setId, string? slug, int userId, bool isAdmin, CancellationToken ct)
    {
        if (slug != null)
        {
            if (!IsValidSlug(slug))
            {
                return null;
            }

            var bySlug = await _db.Sets.AsNoTracking().Where(s => s.ShareSlug == slug)
                .Select(s => new { s.SetId, s.OwnerId }).FirstOrDefaultAsync(ct);
            return bySlug == null ? null : (bySlug.SetId, bySlug.OwnerId);
        }

        if (setId == null)
        {
            return null;
        }

        var byId = await ViewableSets(userId, isAdmin).AsNoTracking().Where(s => s.SetId == setId.Value)
            .Select(s => new { s.SetId, s.OwnerId }).FirstOrDefaultAsync(ct);
        return byId == null ? null : (byId.SetId, byId.OwnerId);
    }

    public async Task<OperationResult<int>> SubscribeAsync(int? setId, string? slug, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var target = await ResolveShareableAsync(setId, slug, userId, isAdmin, ct);
        if (target == null)
        {
            return OperationResult<int>.NotFound();
        }

        var (id, ownerId) = target.Value;
        if (ownerId == userId || await _db.SetSubscriptions.AnyAsync(x => x.SetId == id && x.UserId == userId, ct))
        {
            return OperationResult<int>.Success(id);
        }

        _db.SetSubscriptions.Add(new SetSubscription { SetId = id, UserId = userId, SubscribedAt = Now });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (RelationalUniqueConflict.IsUnique(ex))
        {
            // A concurrent request already subscribed this user: idempotent success.
            _db.ChangeTracker.Clear();
        }

        return OperationResult<int>.Success(id);
    }

    public async Task<OperationResult> UnsubscribeAsync(int setId, int userId, CancellationToken ct = default)
    {
        await _db.SetSubscriptions.Where(x => x.SetId == setId && x.UserId == userId).ExecuteDeleteAsync(ct);
        return OperationResult.Success();
    }

    public async Task<OperationResult<int>> CloneSetAsync(int? setId, string? slug, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var target = await ResolveShareableAsync(setId, slug, userId, isAdmin, ct);
        if (target == null)
        {
            return OperationResult<int>.NotFound();
        }

        return await CloneInternalAsync(target.Value.SetId, userId, ct);
    }

    private async Task<OperationResult<int>> CloneInternalAsync(int sourceSetId, int userId, CancellationToken ct)
    {
        if (await _db.Sets.CountAsync(s => s.OwnerId == userId, ct) >= MaxOwnedSets)
        {
            return OperationResult<int>.Invalid($"Bạn đã có quá nhiều bộ thẻ (tối đa {MaxOwnedSets}).");
        }

        var source = await _db.Sets.AsNoTracking().FirstOrDefaultAsync(s => s.SetId == sourceSetId, ct);
        if (source == null)
        {
            return OperationResult<int>.NotFound();
        }

        var cloneOwnNotes = source.OwnerId == userId;
        var name = source.SetName;
        if (await _db.Sets.AnyAsync(s => s.OwnerId == userId && s.SetName == name, ct))
        {
            name = (name.Length > 240 ? name[..240] : name) + " (bản sao)";
        }

        var now = Now;
        var clone = new Set
        {
            SetName = name,
            Description = source.Description,
            OwnerId = userId,
            FolderId = null,
            Visibility = SetVisibility.Private,
            ClonedFromSetId = source.SetId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var cards = await _db.Cards.AsNoTracking().Where(c => c.SetId == sourceSetId).OrderBy(c => c.CardId).ToListAsync(ct);
        foreach (var card in cards)
        {
            clone.Cards.Add(new Card
            {
                Term = card.Term,
                Definition = card.Definition,
                ImageUrl = card.ImageUrl,
                Phonetic = card.Phonetic,
                Example = card.Example,
                ExampleTranslation = card.ExampleTranslation,
                Topic = card.Topic,
                PartsOfSpeech = card.PartsOfSpeech,
                MeaningEn = card.MeaningEn,
                Cefr = card.Cefr,
                PhoneticUk = card.PhoneticUk,
                Emoji = card.Emoji,
                DetailsJson = card.DetailsJson,
                ImageSourceJson = card.ImageSourceJson,
                AiGenerated = card.AiGenerated,
                PersonalNote = cloneOwnNotes ? card.PersonalNote : null,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        _db.Sets.Add(clone);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<int>.Success(clone.SetId);
    }

    public async Task<OperationResult<SampleSetsResult>> AddSampleSetsAsync(int userId, CancellationToken ct = default)
    {
        var systemId = await _db.Users.AsNoTracking().Where(u => u.Email == SystemOwnerEmail).Select(u => (int?)u.UserId).FirstOrDefaultAsync(ct);
        if (systemId == null)
        {
            return OperationResult<SampleSetsResult>.Invalid("Chưa có bộ thẻ mẫu.");
        }

        var candidates = await _db.Sets.AsNoTracking()
            .Where(s => s.OwnerId == systemId && s.Visibility == SetVisibility.Public)
            .Select(s => new { s.SetId, Cards = s.Cards.Count })
            .Where(s => s.Cards >= 5)
            .OrderByDescending(s => s.Cards)
            .ThenBy(s => s.SetId)
            .Take(2)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return OperationResult<SampleSetsResult>.Invalid("Chưa có bộ thẻ mẫu.");
        }

        var alreadyCloned = (await _db.Sets.AsNoTracking()
                .Where(s => s.OwnerId == userId && s.ClonedFromSetId != null)
                .Select(s => s.ClonedFromSetId!.Value)
                .ToListAsync(ct))
            .ToHashSet();

        var sets = 0;
        var cards = 0;
        foreach (var candidate in candidates.Where(c => !alreadyCloned.Contains(c.SetId)))
        {
            var cloned = await CloneInternalAsync(candidate.SetId, userId, ct);
            if (cloned.Status == OperationStatus.Success)
            {
                sets++;
                cards += candidate.Cards;
            }
            else if (cloned.Status == OperationStatus.Invalid)
            {
                return OperationResult<SampleSetsResult>.Invalid(cloned.ErrorMessage);
            }
        }

        return OperationResult<SampleSetsResult>.Success(new SampleSetsResult(sets, cards));
    }

    // =====================================================================================
    // Legacy editor (non-destructive upsert) and external image import
    // =====================================================================================

    public async Task<OperationResult> ApplyLegacyEditAsync(
        int setId, string name, string? description, IReadOnlyList<LegacyCardRow> rows, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var set = await ManageableSets(userId, isAdmin).FirstOrDefaultAsync(s => s.SetId == setId, ct);
        if (set == null)
        {
            return OperationResult.NotFound();
        }

        var now = Now;
        set.SetName = name;
        set.Description = description;
        set.UpdatedAt = now;

        // 1) Pair by identical term (case-insensitive) so reordering keeps progress.
        // 2) Pair what is left by position so a fixed typo keeps its progress.
        var existing = await _db.Cards.Where(c => c.SetId == setId).OrderBy(c => c.CardId).ToListAsync(ct);
        var pairs = new List<(Card Card, LegacyCardRow Row)>();
        var unmatchedRows = new List<LegacyCardRow>();
        var free = existing.ToList();
        foreach (var row in rows)
        {
            var match = free.FirstOrDefault(c => string.Equals(c.Term.Trim(), row.Term.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                free.Remove(match);
                pairs.Add((match, row));
            }
            else
            {
                unmatchedRows.Add(row);
            }
        }

        var surplusRows = new List<LegacyCardRow>();
        foreach (var row in unmatchedRows)
        {
            if (free.Count > 0)
            {
                var card = free[0];
                free.RemoveAt(0);
                pairs.Add((card, row));
            }
            else
            {
                surplusRows.Add(row);
            }
        }

        foreach (var (card, row) in pairs)
        {
            card.Term = row.Term;
            card.Definition = row.Definition;
            await ApplyLegacyImageAsync(card, row, ct);
            card.UpdatedAt = now;
        }

        foreach (var row in surplusRows)
        {
            var card = new Card { SetId = setId, Term = row.Term, Definition = row.Definition, CreatedAt = now, UpdatedAt = now };
            await ApplyLegacyImageAsync(card, row, ct);
            _db.Cards.Add(card);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        if (free.Count > 0)
        {
            // Surplus cards: progress first, then the card (FK is not cascading).
            var removeIds = free.Select(c => c.CardId).ToList();
            await _db.LearningProgresses.Where(p => removeIds.Contains(p.CardId)).ExecuteDeleteAsync(ct);
            await _db.Cards.Where(c => removeIds.Contains(c.CardId)).ExecuteDeleteAsync(ct);
            foreach (var card in free)
            {
                _db.Entry(card).State = EntityState.Detached;
            }
        }

        await transaction.CommitAsync(ct);
        return OperationResult.Success();
    }

    private async Task ApplyLegacyImageAsync(Card card, LegacyCardRow row, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(row.UploadedImageUrl))
        {
            card.ImageUrl = row.UploadedImageUrl;
            return;
        }

        var submitted = string.IsNullOrWhiteSpace(row.ImageUrl) ? null : row.ImageUrl.Trim();
        if (submitted == card.ImageUrl)
        {
            return;
        }

        if (submitted == null)
        {
            card.ImageUrl = null;
            return;
        }

        if (submitted.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var local = await ImportExternalImageAsync(submitted, ct);
            if (local != null)
            {
                card.ImageUrl = local;
            }

            return;
        }

        if (CardInputNormalizer.IsAllowedImageUrl(submitted))
        {
            card.ImageUrl = submitted;
        }
    }

    public async Task<string?> ImportExternalImageAsync(string externalUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalUrl) || !CardInputNormalizer.IsPixabayUrl(externalUrl))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await client.GetAsync(externalUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var finalUrl = response.RequestMessage?.RequestUri?.ToString();
            if (finalUrl == null || !CardInputNormalizer.IsPixabayUrl(finalUrl))
            {
                return null;
            }

            var extension = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => null,
            };
            if (extension == null || response.Content.Headers.ContentLength > MaxImageBytes)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                if (buffer.Length + read > MaxImageBytes)
                {
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            var bytes = buffer.ToArray();
            if (!MatchesImageSignature(bytes, extension))
            {
                return null;
            }

            var fileName = Guid.NewGuid().ToString("N") + extension;
            var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var directory = Path.Combine(webRoot, "images", "cards");
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, fileName), bytes, ct);
            return "/images/cards/" + fileName;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not import external card image.");
            return null;
        }
    }

    private static bool MatchesImageSignature(byte[] bytes, string extension) => extension switch
    {
        ".jpg" => bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".png" => bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
        ".gif" => bytes.Length > 6 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8',
        ".webp" => bytes.Length > 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
                   && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P',
        _ => false,
    };

    // =====================================================================================
    // CSV export
    // =====================================================================================

    public async Task<SetExportFile?> ExportCsvAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var set = await ViewableSets(userId, isAdmin).AsNoTracking()
            .Where(s => s.SetId == setId)
            .Select(s => new { s.SetName, s.OwnerId })
            .FirstOrDefaultAsync(ct);
        if (set == null)
        {
            return null;
        }

        var includeNotes = set.OwnerId == userId;
        var cards = await _db.Cards.AsNoTracking().Where(c => c.SetId == setId).OrderBy(c => c.CardId).ToListAsync(ct);

        var builder = new StringBuilder();
        builder.Append("term,phonetic,meaning_vi,meaning_en,parts_of_speech,topic,example,example_vi,synonyms,antonyms,cefr,note\r\n");
        foreach (var card in cards)
        {
            var details = CardDetailsCodec.Parse(card.DetailsJson, out _);
            var example = card.Example ?? details.Examples.FirstOrDefault()?.Text;
            var exampleVi = card.ExampleTranslation ?? details.Examples.FirstOrDefault()?.Vi;
            var cells = new[]
            {
                card.Term,
                card.Phonetic,
                card.Definition,
                card.MeaningEn,
                card.PartsOfSpeech,
                card.Topic,
                example,
                exampleVi,
                string.Join("; ", details.Synonyms),
                string.Join("; ", details.Antonyms),
                card.Cefr,
                includeNotes ? card.PersonalNote : null,
            };
            builder.Append(string.Join(',', cells.Select(CsvCell))).Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        var content = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, content, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, content, preamble.Length, body.Length);

        return new SetExportFile { FileName = SetNaming.ToFileName(set.SetName) + ".csv", Content = content };
    }

    /// <summary>RFC 4180 escaping plus a leading apostrophe for cells a spreadsheet could execute as a formula.</summary>
    internal static string CsvCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
