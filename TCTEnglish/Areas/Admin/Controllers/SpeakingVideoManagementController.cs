using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class SpeakingVideoManagementController : Controller
{
    private static readonly Regex YoutubeIdRegex = new("^[a-zA-Z0-9_-]{11}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly DbflashcardContext _context;
    private readonly IYoutubeTranscriptService _transcriptService;
    private readonly ILogger<SpeakingVideoManagementController> _logger;

    public SpeakingVideoManagementController(
        DbflashcardContext context,
        IYoutubeTranscriptService transcriptService,
        ILogger<SpeakingVideoManagementController> logger)
    {
        _context = context;
        _transcriptService = transcriptService;
        _logger = logger;
    }

    // GET: Admin/SpeakingVideoManagement
    public async Task<IActionResult> Index()
    {
        var videos = await _context.SpeakingVideos
            .AsNoTracking()
            .OrderByDescending(v => v.Id)
            .Select(v => new
            {
                Id = v.Id,
                Title = v.Title,
                YoutubeId = v.YoutubeId,
                Level = v.Level,
                Topic = v.Topic,
                ThumbnailUrl = v.ThumbnailUrl,
                Duration = v.Duration,
                SentenceCount = v.SpeakingSentences.Count,
                MaxEndTime = v.SpeakingSentences
                    .Select(s => (double?)s.EndTime)
                    .Max()
            })
            .ToListAsync();

        var viewModels = videos.Select(v => new SpeakingVideoListItemViewModel
        {
            Id = v.Id,
            Title = v.Title,
            YoutubeId = v.YoutubeId,
            Level = v.Level,
            Topic = v.Topic,
            ThumbnailUrl = v.ThumbnailUrl,
            Duration = v.Duration ?? FormatDurationFromSeconds(v.MaxEndTime),
            SentenceCount = v.SentenceCount
        }).ToList();

        return View(viewModels);
    }


    // GET: Admin/SpeakingVideoManagement/Create
    public async Task<IActionResult> Create()
    {
        var model = new SpeakingVideoCreateViewModel
        {
            Topic = "General",
            Playlists = await GetPlaylistOptionsAsync()
        };

        return View(model);
    }

    // POST: Admin/SpeakingVideoManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] SpeakingVideoCreateViewModel model)
    {
        model.Playlists = await GetPlaylistOptionsAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedYoutubeId = NormalizeYoutubeId(model.YoutubeId);
        if (normalizedYoutubeId is null)
        {
            ModelState.AddModelError(nameof(model.YoutubeId), "YouTube ID is invalid. Please enter a valid video ID or URL.");
            return View(model);
        }

        var isDuplicateVideo = await _context.SpeakingVideos
            .AsNoTracking()
            .AnyAsync(v => v.YoutubeId == normalizedYoutubeId);

        if (isDuplicateVideo)
        {
            ModelState.AddModelError(nameof(model.YoutubeId), "This YouTube video is already in the system.");
            return View(model);
        }

        var normalizedTitle = model.Title.Trim();
        var normalizedLevel = model.Level.Trim().ToUpperInvariant();
        var normalizedTopic = string.IsNullOrWhiteSpace(model.Topic) ? "General" : model.Topic.Trim();

        try
        {
            var sentences = await _transcriptService.GetTranscriptAsync(normalizedYoutubeId);
            if (sentences.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Unable to extract captions from this video. Please try another video.");
                return View(model);
            }

            var targetPlaylistId = await ResolveTargetPlaylistIdAsync(model.PlaylistId);
            if (targetPlaylistId == 0)
            {
                ModelState.AddModelError(nameof(model.PlaylistId), "Selected playlist does not exist.");
                return View(model);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var video = new SpeakingVideo
                {
                    Title = normalizedTitle,
                    YoutubeId = normalizedYoutubeId,
                    Level = normalizedLevel,
                    Topic = normalizedTopic,
                    PlaylistId = targetPlaylistId,
                    ThumbnailUrl = $"https://img.youtube.com/vi/{normalizedYoutubeId}/hqdefault.jpg",
                    Duration = await _transcriptService.GetVideoDurationAsync(normalizedYoutubeId)
                };

                _context.SpeakingVideos.Add(video);
                await _context.SaveChangesAsync();

                foreach (var sentence in sentences)
                {
                    sentence.VideoId = video.Id;
                }

                _context.SpeakingSentences.AddRange(sentences);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            });

            TempData["SuccessMessage"] = $"Video created successfully with {sentences.Count} transcript sentences.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create speaking video from YouTube source {YoutubeId}", normalizedYoutubeId);
            ModelState.AddModelError(string.Empty, "An unexpected error happened while creating this video. Please try again.");
            return View(model);
        }
    }

    // POST: Admin/SpeakingVideoManagement/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromForm] int id)
    {
        var video = await _context.SpeakingVideos.FirstOrDefaultAsync(v => v.Id == id);
        if (video == null)
        {
            TempData["ErrorMessage"] = "Video not found.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.SpeakingVideos.Remove(video);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Video deleted successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete speaking video {VideoId}", id);
            TempData["ErrorMessage"] = "Unable to delete video at this time. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<int> ResolveTargetPlaylistIdAsync(int playlistId)
    {
        if (playlistId > 0)
        {
            var playlistExists = await _context.SpeakingPlaylists
                .AsNoTracking()
                .AnyAsync(p => p.Id == playlistId);

            return playlistExists ? playlistId : 0;
        }

        var defaultPlaylist = await _context.SpeakingPlaylists
            .FirstOrDefaultAsync(p => p.Name == "General");

        if (defaultPlaylist != null)
        {
            return defaultPlaylist.Id;
        }

        defaultPlaylist = new SpeakingPlaylist
        {
            Name = "General",
            Description = "Default playlist for videos without selected playlist."
        };

        _context.SpeakingPlaylists.Add(defaultPlaylist);
        await _context.SaveChangesAsync();

        return defaultPlaylist.Id;
    }

    private async Task<List<SpeakingPlaylistOptionViewModel>> GetPlaylistOptionsAsync()
    {
        return await _context.SpeakingPlaylists
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new SpeakingPlaylistOptionViewModel
            {
                Id = p.Id,
                Name = p.Name
            })
            .ToListAsync();
    }

    private static string? FormatDurationFromSeconds(double? totalSeconds)
    {
        if (!totalSeconds.HasValue || totalSeconds.Value <= 0)
        {
            return null;
        }

        var ts = TimeSpan.FromSeconds(totalSeconds.Value);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }

    private static string? NormalizeYoutubeId(string input)
    {
        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (YoutubeIdRegex.IsMatch(value))
        {
            return value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("youtu.be", StringComparison.Ordinal))
        {
            var shortId = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return shortId is not null && YoutubeIdRegex.IsMatch(shortId) ? shortId : null;
        }

        if (!host.Contains("youtube.com", StringComparison.Ordinal))
        {
            return null;
        }

        var query = uri.Query.TrimStart('?');
        if (!string.IsNullOrWhiteSpace(query))
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2, StringSplitOptions.None);
                if (parts.Length != 2 || !parts[0].Equals("v", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var candidate = Uri.UnescapeDataString(parts[1]);
                if (YoutubeIdRegex.IsMatch(candidate))
                {
                    return candidate;
                }
            }
        }

        var pathSegments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (pathSegments.Length >= 2 &&
            (pathSegments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase) ||
             pathSegments[0].Equals("embed", StringComparison.OrdinalIgnoreCase)) &&
            YoutubeIdRegex.IsMatch(pathSegments[1]))
        {
            return pathSegments[1];
        }

        return null;
    }
}
