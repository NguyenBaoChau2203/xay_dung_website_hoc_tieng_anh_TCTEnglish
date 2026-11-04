using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI;

public sealed class AiConversationService : IAiConversationService
{
    private const int MaxConversationTitleLength = 80;

    private readonly DbflashcardContext _context;

    public AiConversationService(DbflashcardContext context)
    {
        _context = context;
    }

    public async Task<AiConversation> CreateConversationAsync(int userId, string? title, CancellationToken ct)
    {
        var normalizedTitle = NormalizeConversationTitle(title);

        var now = DateTime.UtcNow;
        var conversation = new AiConversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = normalizedTitle,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _context.AiConversations.Add(conversation);
        await _context.SaveChangesAsync(ct);

        return conversation;
    }

    private static string NormalizeConversationTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "New chat";
        }

        var collapsedWhitespace = string.Join(
            ' ',
            title
                .Trim()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (collapsedWhitespace.Length <= MaxConversationTitleLength)
        {
            return collapsedWhitespace;
        }

        return collapsedWhitespace[..MaxConversationTitleLength];
    }

    public const int DefaultConversationPageSize = 30;
    public const int DefaultMessagePageSize = 50;
    public const int MaxPageSize = 100;

    public async Task<IReadOnlyList<AiConversationSummaryDto>> GetConversationsByUserAsync(int userId, CancellationToken ct)
    {
        var page = await GetConversationsPageAsync(userId, DefaultConversationPageSize, null, null, ct);
        return page.Items;
    }

    public async Task<AiConversationPageDto> GetConversationsPageAsync(
        int userId,
        int pageSize,
        DateTime? beforeUpdatedAtUtc,
        Guid? beforeId,
        CancellationToken ct)
    {
        var take = ClampPageSize(pageSize, DefaultConversationPageSize);
        var query = _context.AiConversations
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (beforeUpdatedAtUtc.HasValue && beforeId.HasValue)
        {
            var cursorTime = beforeUpdatedAtUtc.Value;
            var cursorId = beforeId.Value;
            query = query.Where(x =>
                x.UpdatedAtUtc < cursorTime
                || (x.UpdatedAtUtc == cursorTime && x.Id.CompareTo(cursorId) < 0));
        }

        var items = await query
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(take + 1)
            .Select(x => new AiConversationSummaryDto(
                x.Id,
                x.Title,
                x.UpdatedAtUtc,
                x.CreatedAtUtc))
            .ToListAsync(ct);

        var hasMore = items.Count > take;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new AiConversationPageDto(items, hasMore);
    }

    public async Task<bool> DeleteConversationAsync(int userId, Guid conversationId, CancellationToken ct)
    {
        var conversation = await _context.AiConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId, ct);

        if (conversation == null)
        {
            return false;
        }

        _context.AiConversations.Remove(conversation);
        await _context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<IReadOnlyList<AiMessage>> GetMessagesByConversationAsync(int userId, Guid conversationId, CancellationToken ct)
    {
        var page = await GetMessagesPageAsync(userId, conversationId, DefaultMessagePageSize, null, null, ct);
        return page.Items;
    }

    public async Task<AiMessagePageDto> GetMessagesPageAsync(
        int userId,
        Guid conversationId,
        int pageSize,
        DateTime? beforeCreatedAtUtc,
        Guid? beforeId,
        CancellationToken ct)
    {
        var hasOwnership = await _context.AiConversations
            .AsNoTracking()
            .AnyAsync(x => x.Id == conversationId && x.UserId == userId, ct);

        if (!hasOwnership)
        {
            throw new KeyNotFoundException($"Conversation '{conversationId}' was not found.");
        }

        var take = ClampPageSize(pageSize, DefaultMessagePageSize);
        var query = _context.AiMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId);

        if (beforeCreatedAtUtc.HasValue && beforeId.HasValue)
        {
            var cursorTime = beforeCreatedAtUtc.Value;
            var cursorId = beforeId.Value;
            query = query.Where(x =>
                x.CreatedAtUtc < cursorTime
                || (x.CreatedAtUtc == cursorTime && x.Id.CompareTo(cursorId) < 0));
        }

        // Newest-first window for paging, then reverse to chronological for UI.
        var newestFirst = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(take + 1)
            .ToListAsync(ct);

        var hasMore = newestFirst.Count > take;
        if (hasMore)
        {
            newestFirst.RemoveAt(newestFirst.Count - 1);
        }

        newestFirst.Reverse();
        return new AiMessagePageDto(newestFirst, hasMore);
    }

    private static int ClampPageSize(int pageSize, int defaultSize)
    {
        if (pageSize < 1)
        {
            return defaultSize;
        }

        return Math.Min(pageSize, MaxPageSize);
    }
}
