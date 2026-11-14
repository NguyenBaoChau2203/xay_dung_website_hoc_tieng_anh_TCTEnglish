using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.Services.Vocabulary;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services
{
    public partial class StudyService
    {
        private const string SystemVocabularyEmail = "system@tct.local";

        public async Task<bool> TryIncrementVocabularySetViewCountAsync(int setId)
        {
            var trackedSet = await _context.Sets
                .FirstOrDefaultAsync(set => set.SetId == setId && set.Owner != null && set.Owner.Email == SystemVocabularyEmail);

            if (trackedSet == null)
            {
                return false;
            }

            trackedSet.ViewCount++;
            await _context.SaveChangesAsync();
            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Rebuilt Vocabulary module (home, course, lesson)
        //  System scope rule: a set is in scope only when OwnerId == the system
        //  user; a folder only when UserId == the system user.

        // ════════════════════════════════════════════════════════════════════

        private const int VocabularyReviewLimit = 20;
        private const int VocabularyPoolLimit = 60;
        private const int VocabularyPoolMinimum = 8;
        private const string VocabularyHomeUrl = "/Vocabulary/Index";

        private static readonly string[] VocabularyLevelOrder = { "A1", "A2", "B1", "B2", "C1", "C2" };
        private static readonly string[] VocabularyHues = { "primary", "success", "warning", "info", "danger", "accent" };

        private sealed record VocabularySetRow(int SetId, string SetName, string? Description);
        private sealed record VocabularyCardKeyRow(int CardId, int SetId, string? Topic);
        private sealed record VocabularyCardRow(
            int CardId, string Term, string? Phonetic, string Definition,
            string? Example, string? ExampleTranslation, string? ImageUrl, string? Topic, string? Emoji);
        private sealed record VocabularyProgressRow(
            int CardId, string? Status, DateTime? NextReviewDate, DateTime? LastReviewedDate,
            int RepetitionCount, bool IsSuspended);

        /// <summary>Estimated seconds per due word, for the "Khoảng n phút" note.</summary>
        private const int VocabularySecondsPerDueWord = 15;
        private static readonly string[] VocabularyWeekdayLabels = { "CN", "T2", "T3", "T4", "T5", "T6", "T7" };

        private static int VocabularyLevelOf(VocabularyProgressRow row) =>
            SpacedRepetitionScheduler.LevelFrom(row.RepetitionCount, row.Status);

        /// <summary>"Hôm nay" (today or overdue), "Ngày mai" or "Sau n ngày", by business date.</summary>
        internal static string VocabularyDueLabel(DateTime nextReviewUtcStorage, DateTime businessToday)
        {
            var days = (BusinessDateHelper.ToBusinessDateFromUtcStorage(nextReviewUtcStorage) - businessToday).Days;
            return days <= 0 ? "Hôm nay" : days == 1 ? "Ngày mai" : $"Sau {days} ngày";
        }

        private static VocabularyMemoryViewModel BuildVocabularyMemory(IEnumerable<VocabularyProgressRow> rows)
        {
            var memory = new VocabularyMemoryViewModel();
            foreach (var row in rows)
            {
                switch (SpacedRepetitionScheduler.StageFor(VocabularyLevelOf(row)))
                {
                    case "new": memory.New++; break;
                    case "growing": memory.Growing++; break;
                    case "strong": memory.Strong++; break;
                    default: memory.Long++; break;
                }
            }

            return memory;
        }

        private static List<VocabularyForecastDayViewModel> BuildVocabularyForecast(
            IEnumerable<VocabularyProgressRow> rows, DateTime businessToday)
        {
            var counts = new int[7];
            foreach (var row in rows)
            {
                if (row.IsSuspended || !row.NextReviewDate.HasValue)
                {
                    continue;
                }

                var day = (BusinessDateHelper.ToBusinessDateFromUtcStorage(row.NextReviewDate.Value) - businessToday).Days;
                if (day <= 0)
                {
                    counts[0]++;
                }
                else if (day < 7)
                {
                    counts[day]++;
                }
            }

            var max = counts.Max();
            var forecast = new List<VocabularyForecastDayViewModel>();
            for (var index = 0; index < 7; index++)
            {
                var date = businessToday.AddDays(index);
                forecast.Add(new VocabularyForecastDayViewModel
                {
                    Label = index == 0 ? "Hôm nay" : VocabularyWeekdayLabels[(int)date.DayOfWeek],
                    Count = counts[index],
                    IsToday = index == 0,
                    HeightPercent = max == 0 || counts[index] == 0 ? 0 : Math.Max(8, counts[index] * 100 / max)
                });
            }

            return forecast;
        }

        /// <summary>The label of the nearest future review among rows that are not due now; null when none.</summary>
        private static string? NextVocabularyDueLabel(
            IEnumerable<VocabularyProgressRow> rows, DateTime utcNow, DateTime businessToday)
        {
            var next = rows
                .Where(row => !row.IsSuspended && row.NextReviewDate.HasValue && row.NextReviewDate.Value > utcNow)
                .Select(row => row.NextReviewDate!.Value)
                .DefaultIfEmpty()
                .Min();

            return next == default ? null : VocabularyDueLabel(next, businessToday);
        }

        private static VocabularyLessonWordViewModel ToVocabularyLessonWord(
            VocabularyCardRow card, IReadOnlyDictionary<int, VocabularyProgressRow>? progress = null)
        {
            VocabularyProgressRow? row = null;
            progress?.TryGetValue(card.CardId, out row);
            int? level = row == null ? null : VocabularyLevelOf(row);

            return new VocabularyLessonWordViewModel
            {
                Id = card.CardId,
                Term = card.Term,
                Phonetic = card.Phonetic,
                Definition = card.Definition,
                Example = card.Example,
                ExampleVi = card.ExampleTranslation,
                ImageUrl = SafeVocabularyImageUrl(card.ImageUrl),
                Level = level,
                Stage = level.HasValue ? SpacedRepetitionScheduler.StageFor(level.Value) : "none",
                StageLabel = level.HasValue ? SpacedRepetitionScheduler.StageLabelFor(level.Value) : null,
                Emoji = VocabularyEmojiMap.For(card.Emoji, card.Term)
            };
        }

        public async Task<VocabularyHomeViewModel?> GetVocabularyHomeAsync(int userId, int? folderId = null)
        {
            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                return folderId.HasValue ? null : new VocabularyHomeViewModel();
            }

            var sys = systemUserId.Value;
            var utcNow = DateTime.UtcNow;

            var folders = await _context.Folders
                .AsNoTracking()
                .Where(folder => folder.UserId == sys)
                .OrderBy(folder => folder.FolderId)
                .Select(folder => new
                {
                    folder.FolderId,
                    folder.FolderName,
                    Sets = folder.Sets
                        .Where(set => set.OwnerId == sys)
                        .OrderBy(set => set.SetId)
                        .Select(set => new VocabularySetRow(set.SetId, set.SetName, set.Description))
                        .ToList()
                })
                .ToListAsync();

            if (folderId.HasValue && folders.All(folder => folder.FolderId != folderId.Value))
            {
                return null;
            }

            var cardKeys = await _context.Cards
                .AsNoTracking()
                .Where(card => card.Set.OwnerId == sys)
                .OrderBy(card => card.CardId)
                .Select(card => new VocabularyCardKeyRow(card.CardId, card.SetId, card.Topic))
                .ToListAsync();

            var progress = await LoadSystemProgressAsync(userId, sys);
            var cardsBySet = cardKeys
                .GroupBy(card => card.SetId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var setByCard = cardKeys.ToDictionary(card => card.CardId, card => card.SetId);

            var streak = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId)
                .Select(user => user.Streak ?? 0)
                .FirstOrDefaultAsync();

            // Course cards and recommended lessons, in collection order.
            var courses = new List<(int FolderId, VocabularyCourseCardViewModel Card, IReadOnlyList<LessonPlan> Plans)>();
            foreach (var folder in folders)
            {
                foreach (var set in folder.Sets)
                {
                    var keys = cardsBySet.GetValueOrDefault(set.SetId) ?? new List<VocabularyCardKeyRow>();
                    var plans = VocabularyLessonBuilder.Build(
                        keys.Select(key => new LessonCard(key.CardId, key.Topic)).ToList());
                    var card = BuildVocabularyCourseCard(
                        set.SetId, set.SetName, set.Description,
                        keys.Select(key => key.CardId).ToList(), plans.Count, progress, utcNow);
                    courses.Add((folder.FolderId, card, plans));
                }
            }

            var today = BusinessDateHelper.Today;
            var stats = new VocabularyStatsViewModel
            {
                TotalCards = cardKeys.Count,
                LearnedCards = progress.Count,
                KnownCards = progress.Values.Count(row => row.Status == "Mastered"),
                DueCards = progress.Values.Count(row => IsVocabularyDue(row, utcNow)),
                LearnedToday = progress.Values.Count(row =>
                    row.LastReviewedDate.HasValue &&
                    BusinessDateHelper.ToBusinessDateFromUtcStorage(row.LastReviewedDate.Value) == today),
                Streak = streak
            };

            var model = new VocabularyHomeViewModel
            {
                Stats = stats,
                DueUrl = stats.DueCards > 0 ? "/Vocabulary/Review" : null,
                Next = BuildVocabularyNextLesson(courses.Select(c => (c.Card, c.Plans)).ToList(), progress, setByCard),
                DueCount = stats.DueCards,
                DueMinutes = Math.Max(1, (stats.DueCards * VocabularySecondsPerDueWord + 59) / 60),
                TodayCount = stats.LearnedToday,
                Streak = streak,
                LearnedCount = stats.LearnedCards,
                Memory = BuildVocabularyMemory(progress.Values),
                Forecast = BuildVocabularyForecast(progress.Values, today),
                NextDueLabel = stats.DueCards == 0 ? NextVocabularyDueLabel(progress.Values, utcNow, today) : null
            };

            var visibleFolders = folderId.HasValue
                ? folders.Where(folder => folder.FolderId == folderId.Value).ToList()
                : folders;

            foreach (var folder in visibleFolders)
            {
                var collection = new VocabularyCollectionViewModel
                {
                    FolderId = folder.FolderId,
                    FolderName = folder.FolderName,
                    Courses = courses.Where(c => c.FolderId == folder.FolderId).Select(c => c.Card).ToList()
                };

                if (collection.Courses.Count > 0 || folderId.HasValue)
                {
                    model.Collections.Add(collection);
                }
            }

            model.Levels = model.Collections
                .SelectMany(collection => collection.Courses)
                .Select(course => course.Level)
                .Where(level => level != null)
                .Select(level => level!)
                .Distinct()
                .OrderBy(level => Array.IndexOf(VocabularyLevelOrder, level))
                .ToList();

            if (folderId.HasValue)
            {
                model.FilterFolderId = folderId.Value;
                model.FilterFolderName = folders.First(folder => folder.FolderId == folderId.Value).FolderName;
            }

            return model;
        }

        public async Task<VocabularyCourseViewModel?> GetVocabularyCourseAsync(int setId, int userId)
        {
            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                return null;
            }

            var sys = systemUserId.Value;
            var set = await LoadSystemSetAsync(setId, sys);
            if (set == null)
            {
                return null;
            }

            var utcNow = DateTime.UtcNow;
            var cards = await LoadSystemCardsAsync(sys, setId);
            var progress = await LoadSystemProgressAsync(userId, sys, setId);
            var plans = VocabularyLessonBuilder.Build(
                cards.Select(card => new LessonCard(card.CardId, card.Topic)).ToList());
            var summary = BuildVocabularyCourseCard(
                set.SetId, set.SetName, set.Description,
                cards.Select(card => card.CardId).ToList(), plans.Count, progress, utcNow);

            var nextPlan = plans.FirstOrDefault(plan => !IsLessonDone(plan, progress));
            var termById = cards.ToDictionary(card => card.CardId, card => card.Term);
            var cardById = cards.ToDictionary(card => card.CardId);
            var lessonByCard = plans
                .SelectMany(plan => plan.CardIds.Select(id => (Id: id, plan.Number)))
                .ToDictionary(item => item.Id, item => item.Number);

            var model = new VocabularyCourseViewModel
            {
                Course = summary,
                FolderId = set.FolderId,
                FolderName = set.FolderName,
                CourseComplete = cards.Count > 0 && nextPlan == null,
                NextLesson = nextPlan == null
                    ? null
                    : new VocabularyLessonLinkViewModel { Number = nextPlan.Number, Url = VocabularyLessonUrl(setId, nextPlan.Number) },
                DueUrl = summary.DueCount > 0 ? $"/Vocabulary/Study?setId={setId}&mode=review" : null,
                PracticeUrl = $"/Practice?set={setId}",
                Memory = BuildVocabularyMemory(progress.Values)
            };

            var businessToday = BusinessDateHelper.Today;
            model.Upcoming = progress.Values
                .Where(row => !row.IsSuspended && row.NextReviewDate.HasValue && termById.ContainsKey(row.CardId))
                .OrderBy(row => row.NextReviewDate)
                .ThenBy(row => row.CardId)
                .Take(6)
                .Select(row => new VocabularyUpcomingWordViewModel
                {
                    Term = termById[row.CardId],
                    Definition = cardById[row.CardId].Definition,
                    Emoji = VocabularyEmojiMap.For(cardById[row.CardId].Emoji, cardById[row.CardId].Term),
                    Level = VocabularyLevelOf(row),
                    Stage = SpacedRepetitionScheduler.StageFor(VocabularyLevelOf(row)),
                    Label = VocabularyDueLabel(row.NextReviewDate!.Value, businessToday)
                })
                .ToList();

            foreach (var plan in plans)
            {
                var learned = plan.CardIds.Count(progress.ContainsKey);
                var row = new VocabularyLessonRowViewModel
                {
                    Number = plan.Number,
                    Title = plan.Title,
                    WordCount = plan.CardIds.Count,
                    LearnedCount = learned,
                    State = learned == plan.CardIds.Count ? "done" : learned > 0 ? "progress" : "new",
                    Url = VocabularyLessonUrl(setId, plan.Number),
                    IsNext = nextPlan != null && plan.Number == nextPlan.Number,
                    DueCount = plan.CardIds.Count(id => progress.TryGetValue(id, out var due) && IsVocabularyDue(due, utcNow)),
                    WordStages = plan.CardIds
                        .Select(id => progress.TryGetValue(id, out var stageRow)
                            ? SpacedRepetitionScheduler.StageFor(VocabularyLevelOf(stageRow))
                            : "none")
                        .ToList()
                };

                if (row.IsNext)
                {
                    row.PreviewTerms = plan.CardIds.Take(5).Select(id => termById[id]).ToList();
                    row.MoreCount = Math.Max(0, plan.CardIds.Count - row.PreviewTerms.Count);
                }

                model.Lessons.Add(row);
            }

            foreach (var card in cards)
            {
                progress.TryGetValue(card.CardId, out var row);
                int? level = row == null ? null : VocabularyLevelOf(row);
                model.Words.Add(new VocabularyWordRowViewModel
                {
                    CardId = card.CardId,
                    Term = card.Term,
                    Phonetic = card.Phonetic,
                    Definition = card.Definition,
                    Example = card.Example,
                    ExampleTranslation = card.ExampleTranslation,
                    ImageUrl = SafeVocabularyImageUrl(card.ImageUrl),
                    LessonNumber = lessonByCard.GetValueOrDefault(card.CardId),
                    Topic = VocabularyLessonBuilder.NormalizeTopic(card.Topic),
                    State = row == null ? "new" : row.Status == "Mastered" ? "known" : "learning",
                    IsDue = row != null && IsVocabularyDue(row, utcNow),
                    Level = level,
                    Stage = level.HasValue ? SpacedRepetitionScheduler.StageFor(level.Value) : "none",
                    StageLabel = level.HasValue ? SpacedRepetitionScheduler.StageLabelFor(level.Value) : "Chưa học",
                    NextReviewLabel = row == null
                        ? "Chưa học"
                        : row.NextReviewDate == null || row.IsSuspended
                            ? "Chưa có lịch"
                            : VocabularyDueLabel(row.NextReviewDate.Value, businessToday),
                    Emoji = VocabularyEmojiMap.For(card.Emoji, card.Term)
                });
            }

            return model;
        }

        public async Task<VocabularyLessonViewModel?> GetVocabularyLessonAsync(
            int setId, int userId, int? lesson, string? topic, bool review)
        {
            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                return null;
            }

            var sys = systemUserId.Value;
            var set = await LoadSystemSetAsync(setId, sys);
            if (set == null)
            {
                return null;
            }

            var utcNow = DateTime.UtcNow;
            var cards = await LoadSystemCardsAsync(sys, setId);
            var progress = await LoadSystemProgressAsync(userId, sys, setId);
            var plans = VocabularyLessonBuilder.Build(
                cards.Select(card => new LessonCard(card.CardId, card.Topic)).ToList());

            var model = new VocabularyLessonViewModel
            {
                SetId = setId,
                SetName = set.SetName,
                CourseUrl = $"/Vocabulary/Detail?setId={setId}",
                HomeUrl = VocabularyHomeUrl,
                RestartUrl = plans.Count > 0 ? VocabularyLessonUrl(setId, 1) : null,
                ReviewUrl = cards.Any(card => progress.TryGetValue(card.CardId, out var row) && IsVocabularyDue(row, utcNow))
                    ? $"/Vocabulary/Study?setId={setId}&mode=review"
                    : null
            };

            if (cards.Count == 0)
            {
                model.Kind = review ? "review" : "learn";
                model.EmptyReason = "no-cards";
                return model;
            }

            List<VocabularyCardRow> sessionCards;

            if (review)
            {
                model.Kind = "review";
                model.LessonTitle = "Ôn tập";
                var dueCards = cards
                    .Where(card => progress.TryGetValue(card.CardId, out var row) && IsVocabularyDue(row, utcNow))
                    .OrderBy(card => progress[card.CardId].NextReviewDate)
                    .ThenBy(card => card.CardId)
                    .ToList();
                sessionCards = dueCards.Take(VocabularyReviewLimit).ToList();
                model.RemainingDue = dueCards.Count - sessionCards.Count;

                if (sessionCards.Count == 0)
                {
                    model.EmptyReason = "nothing-due";
                    model.NextDueLabel = NextVocabularyDueLabel(progress.Values, utcNow, BusinessDateHelper.Today);
                    return model;
                }
            }
            else
            {
                LessonPlan? plan = null;
                if (lesson.HasValue && lesson.Value >= 1 && lesson.Value <= plans.Count)
                {
                    plan = plans[lesson.Value - 1];
                }
                else if (!string.IsNullOrWhiteSpace(topic))
                {
                    var wanted = VocabularyLessonBuilder.NormalizeTopic(topic);
                    plan = plans.FirstOrDefault(p => string.Equals(p.Topic, wanted, StringComparison.OrdinalIgnoreCase));
                }

                plan ??= plans.FirstOrDefault(p => !IsLessonDone(p, progress));

                if (plan == null)
                {
                    model.Kind = "learn";
                    model.EmptyReason = "complete";
                    return model;
                }

                var done = IsLessonDone(plan, progress);
                var cardById = cards.ToDictionary(card => card.CardId);
                model.Kind = done ? "relearn" : "learn";
                model.LessonNumber = plan.Number;
                model.LessonTitle = plan.Title;
                model.RelearnUrl = VocabularyLessonUrl(setId, plan.Number);
                if (plan.Number < plans.Count)
                {
                    model.NextLessonNumber = plan.Number + 1;
                    model.NextLessonUrl = VocabularyLessonUrl(setId, plan.Number + 1);
                }

                sessionCards = plan.CardIds
                    .Where(id => done || !progress.ContainsKey(id))
                    .Select(id => cardById[id])
                    .ToList();
            }

            model.Words = sessionCards.Select(card => ToVocabularyLessonWord(card, progress)).ToList();

            var sessionIds = sessionCards.Select(card => card.CardId).ToHashSet();
            model.Pool = cards
                .Where(card => !sessionIds.Contains(card.CardId))
                .Take(VocabularyPoolLimit)
                .Select(card => new VocabularyPoolWordViewModel { Id = card.CardId, Term = card.Term, Definition = card.Definition })
                .ToList();
            await TopUpVocabularyPoolAsync(model.Pool, sessionIds, sys);

            return model;
        }

        public async Task<VocabularyLessonViewModel> GetVocabularyReviewAsync(int userId)
        {
            var model = new VocabularyLessonViewModel
            {
                Kind = "review",
                SetName = "Ôn tập",
                LessonTitle = "Ôn tập",
                CourseUrl = VocabularyHomeUrl,
                HomeUrl = VocabularyHomeUrl
            };

            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                model.EmptyReason = "nothing-due";
                return model;
            }

            var sys = systemUserId.Value;
            var utcNow = DateTime.UtcNow;
            var progress = await LoadSystemProgressAsync(userId, sys);
            var allDue = progress.Values
                .Where(row => IsVocabularyDue(row, utcNow))
                .OrderBy(row => row.NextReviewDate)
                .ThenBy(row => row.CardId)
                .ToList();
            var dueIds = allDue.Take(VocabularyReviewLimit).Select(row => row.CardId).ToList();
            model.RemainingDue = allDue.Count - dueIds.Count;

            if (dueIds.Count == 0)
            {
                model.EmptyReason = "nothing-due";
                model.NextDueLabel = NextVocabularyDueLabel(progress.Values, utcNow, BusinessDateHelper.Today);
                return model;
            }

            var rows = await _context.Cards
                .AsNoTracking()
                .Where(card => dueIds.Contains(card.CardId) && card.Set.OwnerId == sys)
                .Select(card => new VocabularyCardRow(
                    card.CardId, card.Term, card.Phonetic, card.Definition,
                    card.Example, card.ExampleTranslation, card.ImageUrl, card.Topic, card.Emoji))
                .ToListAsync();

            var byId = rows.ToDictionary(row => row.CardId);
            var ordered = dueIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            if (ordered.Count == 0)
            {
                model.EmptyReason = "nothing-due";
                return model;
            }

            model.Words = ordered.Select(row => ToVocabularyLessonWord(row, progress)).ToList();
            var sessionIds = ordered.Select(row => row.CardId).ToHashSet();
            model.Pool = await _context.Cards
                .AsNoTracking()
                .Where(card => card.Set.OwnerId == sys && !sessionIds.Contains(card.CardId))
                .OrderBy(card => card.CardId)
                .Take(VocabularyPoolLimit)
                .Select(card => new VocabularyPoolWordViewModel { Id = card.CardId, Term = card.Term, Definition = card.Definition })
                .ToListAsync();

            return model;
        }

        public async Task<VocabularyReviewRecordResult?> RecordVocabularyReviewAsync(
            int userId, int cardId, ReviewOutcome outcome)
        {
            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                return null;
            }

            var inScope = await _context.Cards
                .AsNoTracking()
                .AnyAsync(card => card.CardId == cardId && card.Set.OwnerId == systemUserId.Value);
            if (!inScope)
            {
                return null;
            }

            // A concurrent request can create the row first (unique UserId + CardId): reload once and apply on top of it.
            for (var attempt = 0; ; attempt++)
            {
                var progress = await _context.LearningProgresses
                    .FirstOrDefaultAsync(row => row.UserId == userId && row.CardId == cardId);
                var isNew = progress == null;
                var previousStatus = progress?.Status;
                var utcNow = DateTime.UtcNow;

                if (progress != null && progress.IsSuspended)
                {
                    var suspendedLevel = SpacedRepetitionScheduler.LevelFrom(progress.RepetitionCount, progress.Status);
                    return await BuildRecordResultAsync(
                        userId, cardId, suspendedLevel, progress.NextReviewDate,
                        SpacedRepetitionScheduler.ScheduleUnchangedLabel, false, false, 0, null);
                }

                var current = progress == null
                    ? new SrsState(0, null, false)
                    : new SrsState(
                        SpacedRepetitionScheduler.LevelFrom(progress.RepetitionCount, progress.Status),
                        progress.NextReviewDate,
                        true);
                var result = SpacedRepetitionScheduler.Apply(current, outcome, utcNow);

                if (progress == null)
                {
                    progress = new LearningProgress { UserId = userId, CardId = cardId, WrongCount = 0 };
                    _context.LearningProgresses.Add(progress);
                }

                progress.LastReviewedDate = utcNow;
                if (!result.EarlyReview)
                {
                    progress.RepetitionCount = result.Level;
                    progress.Status = result.Status;
                    progress.NextReviewDate = result.NextReviewUtc;
                    if (result.CountsWrong)
                    {
                        progress.WrongCount = (progress.WrongCount ?? 0) + 1;
                    }
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException) when (isNew && attempt == 0)
                {
                    _context.ChangeTracker.Clear();
                    continue;
                }

                var xp = 0;
                int? streak = null;
                if (!result.EarlyReview)
                {
                    var update = _goalsService.BuildVocabularyActivityUpdate(isNew, previousStatus, result.Status);
                    var activity = await _goalsService.RecordLearningActivityAsync(userId, update);
                    if (activity.Status == OperationStatus.Success)
                    {
                        xp = update.XpEarned;
                        streak = activity.Streak;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Vocabulary review for user {UserId}, card {CardId} was saved but the activity was not credited ({Status})",
                            userId, cardId, activity.Status);
                    }
                }

                var label = result.EarlyReview
                    ? SpacedRepetitionScheduler.ScheduleUnchangedLabel
                    : SpacedRepetitionScheduler.NextReviewLabelFor(result.Level);
                return await BuildRecordResultAsync(
                    userId, cardId, result.Level, progress.NextReviewDate, label,
                    result.Promoted, result.Demoted, xp, streak);
            }
        }

        private async Task<VocabularyReviewRecordResult> BuildRecordResultAsync(
            int userId, int cardId, int level, DateTime? next, string label,
            bool promoted, bool demoted, int xp, int? streak)
        {
            streak ??= await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId)
                .Select(user => user.Streak ?? 0)
                .FirstOrDefaultAsync();

            return new VocabularyReviewRecordResult
            {
                CardId = cardId,
                Level = level,
                Stage = SpacedRepetitionScheduler.StageFor(level),
                StageLabel = SpacedRepetitionScheduler.StageLabelFor(level),
                NextReviewDate = next,
                NextReviewLabel = label,
                Promoted = promoted,
                Demoted = demoted,
                XpEarned = xp,
                Streak = streak.Value
            };
        }

        private async Task<VocabularySetSource?> LoadSystemSetAsync(int setId, int systemUserId)
        {
            return await _context.Sets
                .AsNoTracking()
                .Where(set => set.SetId == setId && set.OwnerId == systemUserId)
                .Select(set => new VocabularySetSource(
                    set.SetId, set.SetName, set.Description, set.FolderId,
                    set.Folder != null ? set.Folder.FolderName : null))
                .FirstOrDefaultAsync();
        }

        private sealed record VocabularySetSource(int SetId, string SetName, string? Description, int? FolderId, string? FolderName);

        private async Task<List<VocabularyCardRow>> LoadSystemCardsAsync(int systemUserId, int setId)
        {
            return await _context.Cards
                .AsNoTracking()
                .Where(card => card.SetId == setId && card.Set.OwnerId == systemUserId)
                .OrderBy(card => card.CardId)
                .Select(card => new VocabularyCardRow(
                    card.CardId, card.Term, card.Phonetic, card.Definition,
                    card.Example, card.ExampleTranslation, card.ImageUrl, card.Topic, card.Emoji))
                .ToListAsync();
        }

        private async Task<Dictionary<int, VocabularyProgressRow>> LoadSystemProgressAsync(
            int userId, int systemUserId, int? setId = null)
        {
            var query = _context.LearningProgresses
                .AsNoTracking()
                .Where(progress => progress.UserId == userId && progress.Card.Set.OwnerId == systemUserId);

            if (setId.HasValue)
            {
                query = query.Where(progress => progress.Card.SetId == setId.Value);
            }

            var rows = await query
                .Select(progress => new VocabularyProgressRow(
                    progress.CardId, progress.Status, progress.NextReviewDate, progress.LastReviewedDate,
                    progress.RepetitionCount, progress.IsSuspended))
                .ToListAsync();

            return rows
                .GroupBy(row => row.CardId)
                .ToDictionary(group => group.Key, group => group.First());
        }

        /// <summary>Adds cards of other system courses when the session has too few distractors.</summary>
        private async Task TopUpVocabularyPoolAsync(
            List<VocabularyPoolWordViewModel> pool, HashSet<int> sessionIds, int systemUserId)
        {
            if (pool.Count >= 3)
            {
                return;
            }

            var exclude = sessionIds.Concat(pool.Select(item => item.Id)).ToList();
            var extra = await _context.Cards
                .AsNoTracking()
                .Where(card => card.Set.OwnerId == systemUserId && !exclude.Contains(card.CardId))
                .OrderBy(card => card.CardId)
                .Take(VocabularyPoolMinimum - pool.Count)
                .Select(card => new VocabularyPoolWordViewModel { Id = card.CardId, Term = card.Term, Definition = card.Definition })
                .ToListAsync();

            pool.AddRange(extra);
        }

        private static VocabularyCourseCardViewModel BuildVocabularyCourseCard(
            int setId, string setName, string? description, IReadOnlyList<int> cardIds, int lessonCount,
            IReadOnlyDictionary<int, VocabularyProgressRow> progress, DateTime utcNow)
        {
            var learned = 0;
            var known = 0;
            var due = 0;
            foreach (var id in cardIds)
            {
                if (!progress.TryGetValue(id, out var row))
                {
                    continue;
                }

                learned++;
                if (row.Status == "Mastered")
                {
                    known++;
                }

                if (IsVocabularyDue(row, utcNow))
                {
                    due++;
                }
            }

            var words = cardIds.Count;
            return new VocabularyCourseCardViewModel
            {
                SetId = setId,
                SetName = setName,
                Level = ExtractVocabularyLevel(description),
                WordCount = words,
                LessonCount = lessonCount,
                LearnedCount = learned,
                KnownCount = known,
                DueCount = due,
                ProgressPercent = words > 0 ? learned * 100 / words : 0,
                Status = words > 0 && learned == words ? "done" : learned > 0 ? "progress" : "new",
                IconKey = GetVocabularyIconKey(description),
                Hue = VocabularyHues[setId % VocabularyHues.Length],
                ArtKey = VocabularyLessonBuilder.ArtKeyFor(setName, setId)
            };
        }

        /// <summary>
        /// The recommended lesson: the first unfinished lesson of the most recently studied course,
        /// otherwise of the first course (collection order) that still has one.
        /// </summary>
        private static VocabularyNextLessonViewModel? BuildVocabularyNextLesson(
            IReadOnlyList<(VocabularyCourseCardViewModel Card, IReadOnlyList<LessonPlan> Plans)> courses,
            IReadOnlyDictionary<int, VocabularyProgressRow> progress,
            IReadOnlyDictionary<int, int> setByCard)
        {
            var lastCardId = progress.Values
                .Where(row => row.LastReviewedDate.HasValue)
                .OrderByDescending(row => row.LastReviewedDate)
                .Select(row => (int?)row.CardId)
                .FirstOrDefault();

            int? lastSetId = lastCardId.HasValue && setByCard.TryGetValue(lastCardId.Value, out var setId) ? setId : null;

            var candidates = new List<(VocabularyCourseCardViewModel Card, IReadOnlyList<LessonPlan> Plans)>();
            if (lastSetId.HasValue)
            {
                candidates.AddRange(courses.Where(course => course.Card.SetId == lastSetId.Value));
            }

            candidates.AddRange(courses.Where(course => !lastSetId.HasValue || course.Card.SetId != lastSetId.Value));

            foreach (var (card, plans) in candidates)
            {
                var plan = plans.FirstOrDefault(p => !IsLessonDone(p, progress));
                if (plan == null)
                {
                    continue;
                }

                return new VocabularyNextLessonViewModel
                {
                    SetId = card.SetId,
                    SetName = card.SetName,
                    LessonNumber = plan.Number,
                    LessonTitle = plan.Title,
                    NewWordCount = plan.CardIds.Count(id => !progress.ContainsKey(id)),
                    Url = VocabularyLessonUrl(card.SetId, plan.Number)
                };
            }

            return null;
        }

        private static bool IsLessonDone(LessonPlan plan, IReadOnlyDictionary<int, VocabularyProgressRow> progress)
        {
            return plan.CardIds.All(progress.ContainsKey);
        }

        private static bool IsVocabularyDue(VocabularyProgressRow row, DateTime utcNow)
        {
            return !row.IsSuspended && row.NextReviewDate.HasValue && row.NextReviewDate.Value <= utcNow;
        }

        private static string VocabularyLessonUrl(int setId, int lessonNumber)
        {
            return $"/Vocabulary/Study?setId={setId}&lesson={lessonNumber}";
        }

        /// <summary>Only an absolute https URL or a site-local path may reach an img src.</summary>
        internal static string? SafeVocabularyImageUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var value = url.Trim();
            if (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\'))
            {
                return value;
            }

            return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                ? value
                : null;
        }

        /// <summary>First #a1…#c2 tag in the set description, upper-cased; null when there is none.</summary>
        internal static string? ExtractVocabularyLevel(string? description)
        {
            foreach (var tag in ExtractTags(description))
            {
                var code = tag.TrimStart('#').ToUpperInvariant();
                if (Array.IndexOf(VocabularyLevelOrder, code) >= 0)
                {
                    return code;
                }
            }

            return null;
        }

        /// <summary>Icon for a course cover: the first tag that has a mapping wins.</summary>
        internal static string GetVocabularyIconKey(string? description)
        {
            foreach (var tag in ExtractTags(description))
            {
                switch (tag.TrimStart('#').ToLowerInvariant())
                {
                    case "food":
                    case "drink":
                        return "food";
                    case "travel":
                    case "places":
                        return "travel";
                    case "jobs":
                    case "profession":
                        return "jobs";
                    case "colors":
                        return "colors";
                    case "communication":
                    case "conversation":
                        return "talk";
                    case "oxford":
                    case "essential":
                        return "book";
                    case "common":
                    case "basic":
                        return "star";
                }
            }

            return "book";
        }

        private async Task<int?> GetSystemUserIdAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Where(user => user.Email == SystemVocabularyEmail)
                .Select(user => (int?)user.UserId)
                .FirstOrDefaultAsync();
        }

        private static List<string> ExtractTags(string? description)
        {
            return string.IsNullOrWhiteSpace(description)
                ? new List<string>()
                : description
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(tag => tag.StartsWith("#"))
                    .ToList();
        }
    }
}
