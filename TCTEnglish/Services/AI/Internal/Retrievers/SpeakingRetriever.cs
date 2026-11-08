using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class SpeakingRetriever : IKnowledgeRetriever
{
    private static readonly HashSet<string> IgnoredQueryTokens = new(StringComparer.Ordinal)
    {
        "bai",
        "cho",
        "goi",
        "hoc",
        "nao",
        "nen",
        "phu",
        "playlist",
        "speaking",
        "toi",
        "video",
        "y"
    };

    private readonly DbflashcardContext _context;

    public SpeakingRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent) => intent == UserIntent.SpeakingSuggestion;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        var privateUnlocked = await AiLearnerAccess.CanUseOwnPrivateMediaAsync(_context, userId, ct);
        var publicCount = await _context.SpeakingVideos
            .AsNoTracking()
            .CountAsync(video => video.OwnerUserId == null, ct);
        var ownCount = await _context.SpeakingVideos
            .AsNoTracking()
            .CountAsync(video => video.OwnerUserId == userId, ct);
        if (publicCount == 0 && ownCount == 0)
        {
            return [];
        }

        var ownUnlockedCount = privateUnlocked ? ownCount : 0;
        var ownLockedCount = privateUnlocked ? 0 : ownCount;
        var videos = await _context.SpeakingVideos
            .AsNoTracking()
            .Where(video => video.OwnerUserId == null || (privateUnlocked && video.OwnerUserId == userId))
            .Join(
                _context.SpeakingPlaylists.AsNoTracking(),
                video => video.PlaylistId,
                playlist => playlist.Id,
                (video, playlist) => new
                {
                    video.Id,
                    video.Title,
                    video.Level,
                    video.Topic,
                    video.OwnerUserId,
                    PlaylistName = playlist.Name
                })
            .ToListAsync(ct);

        var accessibleIds = videos.Select(video => video.Id).ToList();
        var userProgressByVideo = accessibleIds.Count == 0
            ? new Dictionary<int, ProgressSnapshot>()
            : await _context.UserSpeakingProgresses
                .AsNoTracking()
                .Where(progress => progress.UserId == userId)
                .Join(
                    _context.SpeakingSentences.AsNoTracking().Where(sentence => accessibleIds.Contains(sentence.VideoId)),
                    progress => progress.SentenceId,
                    sentence => sentence.Id,
                    (progress, sentence) => new
                    {
                        sentence.VideoId,
                        progress.TotalScore
                    })
                .GroupBy(item => item.VideoId)
                .Select(group => new ProgressSnapshot
                {
                    VideoId = group.Key,
                    AttemptCount = group.Count(),
                    BestScore = group.Max(item => item.TotalScore)
                })
                .ToDictionaryAsync(group => group.VideoId, ct);

        var normalizedQueryTokens = AiTextNormalizer.Tokenize(userMessage)
            .Where(token => !IgnoredQueryTokens.Contains(token))
            .ToHashSet(StringComparer.Ordinal);
        var requestedLevel = ExtractLevel(normalizedQueryTokens);
        var rankedVideos = videos
            .Select(video =>
            {
                userProgressByVideo.TryGetValue(video.Id, out var progress);
                var topicScore = CalculateTopicScore(normalizedQueryTokens, video.Title, video.Topic, video.PlaylistName);
                var score = 1;
                if (requestedLevel is not null)
                {
                    score += string.Equals(video.Level, requestedLevel, StringComparison.OrdinalIgnoreCase) ? 5 : -1;
                }

                score += topicScore * 2;
                if (progress is null)
                {
                    score += 3;
                }
                else if (progress.BestScore < 70d)
                {
                    score += 2;
                }
                else if (progress.BestScore < 85d)
                {
                    score += 1;
                }

                return new
                {
                    video.Id,
                    video.Title,
                    video.Level,
                    video.Topic,
                    video.OwnerUserId,
                    AttemptCount = progress?.AttemptCount ?? 0,
                    BestScore = progress?.BestScore ?? 0d,
                    Score = score
                };
            })
            .OrderByDescending(video => video.Score)
            .ThenBy(video => video.AttemptCount)
            .ThenBy(video => video.BestScore)
            .ThenBy(video => video.Title, StringComparer.Ordinal)
            .Take(3)
            .ToList();

        var snippets = new List<KnowledgeSnippet>
        {
            new(
                "summary",
                $"publicCount={publicCount}|ownUnlockedCount={ownUnlockedCount}|ownLockedCount={ownLockedCount}|fallback={(ownLockedCount > 0 ? "upgrade" : "none")}",
                KnowledgeSnippetSources.SpeakingSuggestionSummary)
        };
        snippets.AddRange(rankedVideos.Select(video => new KnowledgeSnippet(
            video.Title,
            $"level={video.Level}|topic={video.Topic}|visibility={(video.OwnerUserId is null ? "public" : "owner-premium")}",
            KnowledgeSnippetSources.SpeakingSuggestion,
            Priority: video.Score,
            EntityKey: $"speaking:{video.Id}")));
        return snippets;
    }

    private static string? ExtractLevel(HashSet<string> queryTokens)
    {
        foreach (var level in new[] { "a1", "a2", "b1", "b2", "c1", "c2" })
        {
            if (queryTokens.Contains(level))
            {
                return level.ToUpperInvariant();
            }
        }

        return null;
    }

    private static int CalculateTopicScore(HashSet<string> queryTokens, string title, string? topic, string playlistName)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        var contentTokens = AiTextNormalizer.Tokenize(title);
        contentTokens.UnionWith(AiTextNormalizer.Tokenize(topic));
        contentTokens.UnionWith(AiTextNormalizer.Tokenize(playlistName));
        return queryTokens.Count(contentTokens.Contains);
    }

    private sealed class ProgressSnapshot
    {
        public int VideoId { get; set; }

        public int AttemptCount { get; set; }

        public double BestScore { get; set; }
    }
}
