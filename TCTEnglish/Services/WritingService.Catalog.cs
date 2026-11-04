using Microsoft.EntityFrameworkCore;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Services;

public partial class WritingService
{
    private async Task<WritingExerciseListViewModel> LoadPagedWritingCatalogAsync(
        string? level, string? contentType, string? topic, int page, int? userId, string? status, int myPage)
    {
        const int pageSize = 6; // Preserve the existing rendered page size.
        var index = BuildWritingIndexViewModel(level);
        var type = ResolveWritingContentTypeKey(contentType);
        var selectedTopic = NormalizeWritingTopic(topic);
        var selectedStatus = NormalizeWritingStatus(status, userId.HasValue);
        var publicQuery = _context.WritingExercises.AsNoTracking().Where(e => e.UserId == null && e.IsPublished
            && e.Level == index.SelectedLevelKey && e.ContentType == type);
        var ownedQuery = _context.WritingExercises.AsNoTracking().Where(e => userId.HasValue && e.UserId == userId
            && e.Level == index.SelectedLevelKey && e.ContentType == type);
        var topics = await publicQuery.Select(e => e.Topic).Union(ownedQuery.Select(e => e.Topic)).Distinct().OrderBy(t => t).ToListAsync();
        if (!selectedTopic.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var lowerTopic = selectedTopic.ToLowerInvariant();
            publicQuery = publicQuery.Where(e => e.Topic.ToLower() == lowerTopic);
            ownedQuery = ownedQuery.Where(e => e.Topic.ToLower() == lowerTopic);
        }
        var cards = ProjectWritingCatalog(publicQuery, userId);
        if (userId.HasValue)
        {
            cards = selectedStatus switch
            {
                WritingStatusNotStarted => cards.Where(e => e.Attempts == 0),
                WritingStatusCompleted => cards.Where(e => e.Attempts > 0 && e.Sentences > 0 && e.Completed >= e.Sentences),
                WritingStatusInProgress => cards.Where(e => e.Attempts > 0 && (e.Sentences == 0 || e.Completed < e.Sentences)),
                _ => cards
            };
        }
        var total = await cards.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        var current = totalPages == 0 ? 0 : Math.Clamp(page, 1, totalPages);
        var rows = await cards.OrderBy(e => e.Id).Skip(Math.Max(0, current - 1) * pageSize).Take(pageSize).ToListAsync();
        var access = userId.HasValue ? await ResolveWritingAccessContextAsync(userId.Value) : null;
        var ownTotal = await ownedQuery.CountAsync();
        var ownPages = (int)Math.Ceiling(ownTotal / (double)pageSize);
        var ownCurrent = ownPages == 0 ? 0 : Math.Clamp(myPage, 1, ownPages);
        var ownRows = await ProjectWritingCatalog(ownedQuery, access?.CanAccessOwnedPrivateExercises == true ? userId : null)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Skip(Math.Max(0, ownCurrent - 1) * pageSize).Take(pageSize).ToListAsync();
        var topicOptions = new List<WritingFilterOptionViewModel> { new() { Value = "all", Label = "Tất cả chủ đề" } };
        topicOptions.AddRange(topics.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(t => new WritingFilterOptionViewModel { Value = t, Label = t }));
        return new WritingExerciseListViewModel
        {
            SelectedLevelKey = index.SelectedLevelKey, SelectedLevelTitle = index.SelectedLevelTitle,
            SelectedContentTypeKey = type, SelectedContentTypeTitle = ResolveWritingContentTypeTitle(type),
            SelectedTopic = selectedTopic, SelectedStatus = selectedStatus, ShowProgressMetadata = userId.HasValue,
            CurrentPage = current, TotalPages = totalPages, TotalCount = total, PageSize = pageSize,
            StartItemNumber = current == 0 ? 0 : (current - 1) * pageSize + 1, EndItemNumber = Math.Min(current * pageSize, total),
            PageNumbers = BuildWritingPageNumbers(current, totalPages), TopicOptions = topicOptions,
            StatusOptions = userId.HasValue ? BuildWritingStatusOptions() : [],
            Exercises = rows.Select(r => CatalogCard(r, false, false, userId.HasValue)).ToList(),
            MyExercises = ownRows.Select(r => CatalogCard(r, true, access?.CanAccessOwnedPrivateExercises != true, userId.HasValue)).ToList(),
            IsMyExercisesLocked = ownTotal > 0 && access?.CanAccessOwnedPrivateExercises != true,
            IsAuthenticatedUser = userId.HasValue, CanCreateFromAi = access?.CanCreateFromAi == true,
            MyCurrentPage = ownCurrent, MyTotalPages = ownPages, MyTotalCount = ownTotal
        };
    }

    private IQueryable<WritingCatalogRow> ProjectWritingCatalog(IQueryable<WritingExercise> query, int? userId)
    {
        return query.Select(e => new WritingCatalogRow
        {
            Id = e.Id, Title = e.Title, Preview = e.PreviewText, Topic = e.Topic, CreatedAt = e.CreatedAt,
            Sentences = e.WritingExerciseSentences.Count(), CanDelete = !e.UserWritingAttempts.Any(),
            Attempts = e.UserWritingAttempts.Count(a => userId.HasValue && a.UserId == userId),
            // Count sentences with any successful attempt. A correlated DISTINCT
            // derived table cannot refer to e.Id on MariaDB 10.5.
            Completed = e.WritingExerciseSentences.Count(s => s.UserWritingAttempts.Any(a =>
                userId.HasValue && a.UserId == userId && a.Passed && a.WritingExerciseId == e.Id)),
            Last = e.UserWritingAttempts.Where(a => userId.HasValue && a.UserId == userId).Max(a => (DateTime?)a.CreatedAtUtc)
        });
    }

    private static WritingExerciseCardViewModel CatalogCard(WritingCatalogRow row, bool owned, bool locked, bool progress)
    {
        var status = ResolveWritingProgressStatusKey(row.Attempts, row.Completed, row.Sentences);
        return new WritingExerciseCardViewModel
        {
            Id = row.Id, Title = row.Title, PreviewText = row.Preview, Topic = row.Topic, SentenceCount = row.Sentences,
            AttemptCount = row.Attempts, CompletedSentenceCount = row.Completed,
            StatusKey = progress ? status : WritingStatusNotStarted, StatusLabel = ResolveWritingProgressStatusLabel(status),
            StartActionLabel = locked ? "Nâng cấp để mở khóa" : ResolveWritingStartActionLabel(status),
            LastAttemptedAtDisplay = FormatWritingTimestamp(row.Last), IsPrivate = owned, IsLocked = locked,
            CanDelete = owned && row.CanDelete, CreatedAtDisplay = owned ? FormatWritingTimestamp(row.CreatedAt) : string.Empty
        };
    }

    private sealed class WritingCatalogRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Preview { get; set; } = "";
        public string Topic { get; set; } = "";
        public int Sentences { get; set; }
        public int Attempts { get; set; }
        public int Completed { get; set; }
        public DateTime? Last { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool CanDelete { get; set; }
    }
}
