using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class NotificationRetriever : IKnowledgeRetriever
{
    private readonly DbflashcardContext _context;

    public NotificationRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent) => intent == UserIntent.WebsiteGuide;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        var normalizedMessage = AiTextNormalizer.Normalize(userMessage);
        if (!IsNotificationStatusQuery(normalizedMessage))
        {
            return [];
        }

        var totalCount = await _context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .CountAsync(ct);

        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .CountAsync(ct);

        var latestNotifications = await _context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Select(notification => new
            {
                notification.Title,
                notification.Type,
                notification.IsRead,
                notification.CreatedAt
            })
            .Take(3)
            .ToListAsync(ct);

        var snippets = new List<KnowledgeSnippet>
        {
            new(
                "notification-summary",
                $"totalCount={totalCount}|unreadCount={unreadCount}",
                KnowledgeSnippetSources.NotificationSummary,
                "/Notification/Index")
        };

        snippets.AddRange(latestNotifications.Select(notification => new KnowledgeSnippet(
            notification.Title,
            string.Join(
                '|',
                $"type={notification.Type}",
                $"isRead={notification.IsRead.ToString().ToLowerInvariant()}",
                $"createdAt={notification.CreatedAt:yyyy-MM-dd}"),
            KnowledgeSnippetSources.NotificationItem,
            "/Notification/Index")));

        return snippets;
    }

    private static bool IsNotificationStatusQuery(string normalizedMessage)
    {
        var hasNotificationKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "thong bao",
            "notification",
            "notifications",
            "bell",
            "chuong");

        if (!hasNotificationKeyword)
        {
            return false;
        }

        return AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "cua toi",
            "chua doc",
            "da doc",
            "moi nhat",
            "gan day",
            "bao nhieu",
            "co thong bao");
    }
}
