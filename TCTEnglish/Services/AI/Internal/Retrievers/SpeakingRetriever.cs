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
        var videos = await _context.SpeakingVideos
            .AsNoTracking()
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
                    PlaylistName = playlist.Name
                })
            .ToListAsync(ct);

        if (videos.Count == 0)
        {
            return [];
        }

        var userProgressByVideo = await _context.UserSpeakingProgresses
            .AsNoTracking()
            .Where(progress => progress.UserId == userId)
            .Join(
                _context.SpeakingSentences.AsNoTracking(),
                progress => progress.SentenceId,
                sentence => sentence.Id,
                (progress, sentence) => new
                {
                    sentence.VideoId,
                    progress.TotalScore
                })
            .GroupBy(item => item.VideoId)
            .Select(group => new
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
                var progress = userProgressByVideo.GetValueOrDefault(video.Id);
                var topicScore = CalculateTopicScore(normalizedQueryTokens, video.Title, video.Topic, video.PlaylistName);

                var score = 1;
                if (requestedLevel is not null)
                {
                    if (string.Equals(video.Level, requestedLevel, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 5;
                    }
                    else
                    {
                        score -= 1;
                    }
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
                    video.Title,
                    video.Level,
                    video.Topic,
                    AttemptCount = progress?.AttemptCount ?? 0,
                    BestScore = progress?.BestScore ?? 0d,
                    Score = score
                };
            })
            .OrderByDescending(video => video.Score)
            .ThenBy(video => video.AttemptCount)
            .ThenBy(video => video.BestScore)
            .ThenBy(video => video.Title)
            .Take(3)
            .ToList();

        return rankedVideos.Select(video => new KnowledgeSnippet(
            video.Title,
            $"level={video.Level}|topic={video.Topic}",
            KnowledgeSnippetSources.SpeakingSuggestion,
            Priority: video.Score))
            .ToList();
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

    private static int CalculateTopicScore(HashSet<string> queryTokens, string title, string topic, string playlistName)
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
}
