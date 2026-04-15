using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTVocabulary.Services;

internal sealed class SpeakingVideoCatalogRow
{
    public int Id { get; set; }
    public int? PlaylistId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string YoutubeId { get; set; } = string.Empty;
    public string Level { get; set; } = SpeakingVideoPresentationDefaults.UnassignedLevel;
    public string Topic { get; set; } = SpeakingVideoPresentationDefaults.DefaultTopic;
    public string? ThumbnailUrl { get; set; }
    public string? Duration { get; set; }
    public double? MaxEndTime { get; set; }
    public int SentenceCount { get; set; }
}

internal sealed class SpeakingPlaylistCatalogRow
{
    public int Id { get; set; }
    public string Name { get; set; } = SpeakingVideoPresentationDefaults.UntitledPlaylist;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
}

internal sealed class SpeakingVideoEditRow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Topic { get; set; } = SpeakingVideoPresentationDefaults.DefaultTopic;
    public int PlaylistId { get; set; }
    public string YoutubeId { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int SentenceCount { get; set; }
}

internal static class SpeakingVideoCompatibilityQueries
{
    public static Task<List<SpeakingVideoCatalogRow>> GetCatalogRowsAsync(DbflashcardContext context)
    {
        return context.Database.SqlQuery<SpeakingVideoCatalogRow>(
            $"""
            SELECT
                v.Id,
                v.PlaylistId,
                COALESCE(v.Title, {SpeakingVideoPresentationDefaults.UntitledVideo}) AS Title,
                COALESCE(v.YoutubeId, {string.Empty}) AS YoutubeId,
                COALESCE(v.Level, {SpeakingVideoPresentationDefaults.UnassignedLevel}) AS Level,
                COALESCE(v.Topic, {SpeakingVideoPresentationDefaults.DefaultTopic}) AS Topic,
                v.ThumbnailUrl,
                v.Duration,
                (
                    SELECT MAX(s.EndTime)
                    FROM SpeakingSentences AS s
                    WHERE s.VideoId = v.Id
                ) AS MaxEndTime,
                (
                    SELECT COUNT(*)
                    FROM SpeakingSentences AS s
                    WHERE s.VideoId = v.Id
                ) AS SentenceCount
            FROM SpeakingVideos AS v
            """)
            .ToListAsync();
    }

    public static Task<List<SpeakingPlaylistCatalogRow>> GetPlaylistRowsAsync(DbflashcardContext context)
    {
        return context.Database.SqlQuery<SpeakingPlaylistCatalogRow>(
            $"""
            SELECT
                p.Id,
                COALESCE(p.Name, {SpeakingVideoPresentationDefaults.UntitledPlaylist}) AS Name,
                p.Description,
                p.ThumbnailUrl
            FROM SpeakingPlaylists AS p
            ORDER BY p.Name
            """)
            .ToListAsync();
    }

    public static Task<SpeakingVideoEditRow?> GetEditRowAsync(DbflashcardContext context, int id)
    {
        return context.Database.SqlQuery<SpeakingVideoEditRow>(
            $"""
            SELECT
                v.Id,
                COALESCE(v.Title, {SpeakingVideoPresentationDefaults.UntitledVideo}) AS Title,
                COALESCE(v.Level, {string.Empty}) AS Level,
                COALESCE(v.Topic, {SpeakingVideoPresentationDefaults.DefaultTopic}) AS Topic,
                COALESCE(v.PlaylistId, {0}) AS PlaylistId,
                COALESCE(v.YoutubeId, {string.Empty}) AS YoutubeId,
                v.ThumbnailUrl,
                (
                    SELECT COUNT(*)
                    FROM SpeakingSentences AS s
                    WHERE s.VideoId = v.Id
                ) AS SentenceCount
            FROM SpeakingVideos AS v
            WHERE v.Id = {id}
            """)
            .SingleOrDefaultAsync();
    }
}
