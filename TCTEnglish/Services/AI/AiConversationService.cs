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

    public async Task<IReadOnlyList<AiConversationSummaryDto>> GetConversationsByUserAsync(int userId, CancellationToken ct)
    {
        return await _context.AiConversations
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => new AiConversationSummaryDto(
                x.Id,
                x.Title,
                x.UpdatedAtUtc,
                x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AiMessage>> GetMessagesByConversationAsync(int userId, Guid conversationId, CancellationToken ct)
    {
        var hasOwnership = await _context.AiConversations
            .AsNoTracking()
            .AnyAsync(x => x.Id == conversationId && x.UserId == userId, ct);

        if (!hasOwnership)
        {
            throw new KeyNotFoundException($"Conversation '{conversationId}' was not found.");
        }

        return await _context.AiMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }
}
