using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Data.Common;
using TCTVocabulary.Models;
using TCTEnglish.ViewModels;

namespace TCTVocabulary.Services
{
    public class GoalsService : IGoalsService
    {
        private const int VocabularyReviewXp = 5;
        private const int VocabularyNewLearningXp = 10;
        private const int VocabularyMasteredXp = 15;
        private const int SpeakingVideoCompletionXp = 20;
        private const int WritingExerciseCompletionXp = 20;
        private const int StreakIncreaseXp = 10;
        private static readonly GoalArea[] GoalAreasWithRealCompletionSignals =
        [
            GoalArea.Vocabulary,
            GoalArea.Speaking,
            GoalArea.Writing
        ];

        private readonly DbflashcardContext _context;
        private readonly IStreakService _streakService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<GoalsService> _logger;
        private UserDailyActivityColumnSupport? _userDailyActivityColumnSupport;

        public GoalsService(
            DbflashcardContext context,
            IStreakService streakService,
            INotificationService notificationService,
            ILogger<GoalsService> logger)
        {
            _context = context;
            _streakService = streakService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<GoalsViewModel?> GetGoalsAsync(int userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.Goal,
                    u.Streak,
                    u.LongestStreak,
                    u.LastStudyDate
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("Goals page requested for missing user {userId}", userId);
                return null;
            }

            var today = ResolveBusinessDate();
            var weekStart = today.AddDays(-6);

            var activeGoals = await _context.UserGoals
                .AsNoTracking()
                .Where(goal => goal.UserId == userId && goal.IsActive)
                .OrderBy(goal => goal.GoalArea)
                .ToListAsync();

            var dailyActivities = await _context.UserDailyActivities
                .AsNoTracking()
                .Where(activity =>
                    activity.UserId == userId
                    && activity.ActivityDate >= weekStart
                    && activity.ActivityDate <= today)
                .Select(activity => new
                {
                    Date = activity.ActivityDate,
                    activity.CardsReviewed
                })
                .ToListAsync();

            var todayActivity = await GetTodayActivitySnapshotAsync(userId, today);

            if (!activeGoals.Any() && (user.Goal ?? 0) > 0)
            {
                activeGoals.Add(new UserGoal
                {
                    UserId = userId,
                    GoalArea = GoalArea.Vocabulary,
                    TargetValue = Math.Max(UpdateGoalInputViewModel.MinTargetValue, user.Goal ?? 0),
                    IsActive = true
                });
            }

            var todayProgressByArea = BuildTodayProgressByArea(todayActivity);
            var goalCards = BuildGoalCards(activeGoals, todayProgressByArea);

            var vocabularyGoal = goalCards.FirstOrDefault(card => card.GoalArea == GoalArea.Vocabulary);

            var currentStreak = ComputeCurrentStreak(user.LastStudyDate, user.Streak, today);
            var longestStreak = Math.Max(user.LongestStreak ?? 0, currentStreak);
            var dailyGoal = vocabularyGoal?.TargetValue
                ?? Math.Max(UpdateGoalInputViewModel.MinTargetValue, user.Goal ?? 0);
            var activityByDate = dailyActivities.ToDictionary(
                activity => activity.Date.Date,
                activity => activity.CardsReviewed);
            var weeklyActivity = BuildWeeklyActivity(weekStart, today, dailyGoal, activityByDate);
            var hasWeeklyActivity = weeklyActivity.Any(day => day.ActivityCount > 0);
            var todayProgressValue = vocabularyGoal?.TodayProgressValue
                ?? (activityByDate.TryGetValue(today, out var reviewedToday) ? reviewedToday : 0);

            var badgeMetrics = await BuildBadgeMetricsAsync(
                userId,
                user.LastStudyDate,
                user.Streak,
                user.LongestStreak);

            var badges = badgeMetrics == null
                ? new List<GoalsBadgeViewModel>()
                : await BuildBadgesAsync(userId, badgeMetrics);

            return new GoalsViewModel
            {
                CurrentStreak = currentStreak,
                LongestStreak = longestStreak,
                StreakMessage = BuildStreakMessage(currentStreak, user.LastStudyDate, today),
                GoalCards = goalCards,
                DailyGoal = dailyGoal,
                TodayProgressValue = todayProgressValue,
                TodayProgressMax = dailyGoal,
                ProgressPercent = CalculateProgressPercent(todayProgressValue, dailyGoal),
                WeeklyActivity = weeklyActivity,
                WeeklyActivityMessage = BuildWeeklyActivityMessage(hasWeeklyActivity),
                Badges = badges,
                GoalAreaOptions = BuildGoalAreaOptions(activeGoals),
                DeferredGoalAreaLabels = BuildDeferredGoalAreaLabels(),
                GoalEditor = new UpdateGoalInputViewModel
                {
                    GoalArea = goalCards.FirstOrDefault()?.GoalArea ?? GoalArea.Vocabulary,
                    TargetValue = dailyGoal
                }
            };
        }

        public GoalsActivityUpdate BuildSpeakingCompletionActivityUpdate()
        {
            return new GoalsActivityUpdate
            {
                SpeakingCompletedCount = 1,
                XpEarned = SpeakingVideoCompletionXp
            };
        }

        public GoalsActivityUpdate BuildWritingCompletionActivityUpdate()
        {
            return new GoalsActivityUpdate
            {
                WritingCompletedCount = 1,
                XpEarned = WritingExerciseCompletionXp
            };
        }

        public async Task<OperationResult> UpdateGoalAsync(int userId, GoalArea goalArea, int targetValue)
        {
            if (!Enum.IsDefined(goalArea))
            {
                return OperationResult.Invalid("Kỹ năng mục tiêu không hợp lệ.");
            }

            if (!IsGoalAreaEnabled(goalArea))
            {
                return OperationResult.Invalid($"{GetGoalAreaLabel(goalArea)} chưa có luồng hoàn thành chính thức nên đang tạm hoãn trong Goals.");
            }

            if (targetValue < UpdateGoalInputViewModel.MinTargetValue || targetValue > UpdateGoalInputViewModel.MaxTargetValue)
            {
                return OperationResult.Invalid(
                    $"Mục tiêu mỗi ngày phải từ {UpdateGoalInputViewModel.MinTargetValue} đến {UpdateGoalInputViewModel.MaxTargetValue}.");
            }

            var user = await _context.Users
                .Where(u => u.UserId == userId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("Goal update failed because user {userId} was not found", userId);
                return OperationResult.NotFound();
            }

            var userGoal = await _context.UserGoals
                .Where(goal => goal.UserId == userId && goal.GoalArea == goalArea)
                .FirstOrDefaultAsync();

            if (userGoal == null)
            {
                userGoal = new UserGoal
                {
                    UserId = userId,
                    GoalArea = goalArea,
                    TargetValue = targetValue,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.UserGoals.Add(userGoal);
            }
            else
            {
                userGoal.TargetValue = targetValue;
                userGoal.IsActive = true;
                userGoal.UpdatedAt = DateTime.UtcNow;
            }

            if (goalArea == GoalArea.Vocabulary)
            {
                user.Goal = targetValue;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Updated goal for user {userId}, goalArea {goalArea}, targetValue {targetValue}",
                userId,
                goalArea,
                targetValue);
            return OperationResult.Success();
        }

        public async Task<OperationResult> RecordActivityAsync(int userId, GoalsActivityUpdate update)
        {
            return await RecordActivityCoreAsync(userId, update, refreshBadgesAfterPersist: true);
        }

        public async Task<GoalsActivityRecordResult> RecordLearningActivityAsync(int userId, GoalsActivityUpdate update)
        {
            var activityResult = await RecordActivityCoreAsync(userId, update, refreshBadgesAfterPersist: false);
            if (activityResult.Status == OperationStatus.NotFound)
            {
                return GoalsActivityRecordResult.NotFound(activityResult.ErrorMessage);
            }

            if (activityResult.Status == OperationStatus.Invalid)
            {
                return GoalsActivityRecordResult.Invalid(activityResult.ErrorMessage);
            }

            var streakResult = await UpdateStreakAndRewardsAsync(userId);
            var unlockedBadgeCount = await RefreshUserBadgesAsync(userId);

            _logger.LogDebug(
                "Recorded learning activity for user {userId}, streak {streak}, streakXpAwarded {streakXpAwarded}, unlockedBadges {unlockedBadgeCount}",
                userId,
                streakResult.CurrentStreak,
                streakResult.StreakXpAwarded,
                unlockedBadgeCount);

            // ── Notification: goal progress / completion ─────────────────
            var activityRecordResult = GoalsActivityRecordResult.Success(streakResult.CurrentStreak);
            try
            {
                await _notificationService.GenerateGoalNotificationsAsync(userId, activityRecordResult);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to generate goal notification for user {UserId}", userId);
            }

            return activityRecordResult;
        }

        public async Task<StreakUpdateResult> UpdateStreakAndRewardsAsync(int userId)
        {
            var streakResult = await _streakService.UpdateStreakWithMetadataAsync(userId);
            if (!streakResult.DidIncrease)
            {
                return streakResult;
            }

            var streakXpAwarded = await AwardStreakXpIfNeededAsync(userId, ResolveBusinessDate());

            return new StreakUpdateResult
            {
                CurrentStreak = streakResult.CurrentStreak,
                DidIncrease = streakResult.DidIncrease,
                StreakXpAwarded = streakXpAwarded
            };
        }

        public GoalsActivityUpdate BuildVocabularyActivityUpdate(bool isNewProgress, string? previousStatus, string currentStatus)
        {
            var activityKind = DetermineVocabularyActivityKind(isNewProgress, previousStatus, currentStatus);

            return new GoalsActivityUpdate
            {
                CardsReviewed = 1,
                NewCardsLearned = isNewProgress ? 1 : 0,
                VocabularyCompletedCount = activityKind == VocabularyActivityKind.Mastered ? 1 : 0,
                XpEarned = activityKind switch
                {
                    VocabularyActivityKind.NewLearning => VocabularyNewLearningXp,
                    VocabularyActivityKind.Mastered => VocabularyMasteredXp,
                    _ => VocabularyReviewXp
                }
            };
        }

        private async Task<OperationResult> RecordActivityCoreAsync(
            int userId,
            GoalsActivityUpdate update,
            bool refreshBadgesAfterPersist)
        {
            ArgumentNullException.ThrowIfNull(update);

            if (update.HasNegativeValues)
            {
                return OperationResult.Invalid("Goals activity increments must be non-negative.");
            }

            if (!update.HasChanges)
            {
                return OperationResult.Success();
            }

            var activityDate = ResolveBusinessDate();
            var updatedRows = await IncrementExistingActivityAsync(userId, activityDate, update);
            if (updatedRows > 0)
            {
                var unlockedBadges = await RefreshBadgesIfNeededAsync(userId, refreshBadgesAfterPersist);

                _logger.LogDebug(
                    "Updated existing daily activity for user {userId} on {activityDate} with xp {xpEarned}, cardsReviewed {cardsReviewed}, refreshBadgesAfterPersist {refreshBadgesAfterPersist}, unlockedBadges {unlockedBadges}",
                    userId,
                    activityDate,
                    update.XpEarned,
                    update.CardsReviewed,
                    refreshBadgesAfterPersist,
                    unlockedBadges);
                return OperationResult.Success();
            }

            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserId == userId);

            if (!userExists)
            {
                _logger.LogWarning("Goals activity update failed because user {userId} was not found", userId);
                return OperationResult.NotFound();
            }

            try
            {
                await InsertUserDailyActivityAsync(userId, activityDate, update, streakXpAwarded: 0);
            }
            catch (Exception ex) when (IsUserDailyActivityUniqueConflict(ex))
            {
                var recoveredRows = await IncrementExistingActivityAsync(userId, activityDate, update);
                if (recoveredRows == 0)
                {
                    throw;
                }

                var recoveredBadges = await RefreshBadgesIfNeededAsync(userId, refreshBadgesAfterPersist);

                _logger.LogDebug(
                    "Recovered daily activity race for user {userId} on {activityDate}, refreshBadgesAfterPersist {refreshBadgesAfterPersist}, unlockedBadges {unlockedBadges}",
                    userId,
                    activityDate,
                    refreshBadgesAfterPersist,
                    recoveredBadges);
                return OperationResult.Success();
            }

            var unlockedBadgeCount = await RefreshBadgesIfNeededAsync(userId, refreshBadgesAfterPersist);

            _logger.LogInformation(
                "Created daily activity for user {userId} on {activityDate} with xp {xpEarned}, cardsReviewed {cardsReviewed}, refreshBadgesAfterPersist {refreshBadgesAfterPersist}, unlockedBadges {unlockedBadgeCount}",
                userId,
                activityDate,
                update.XpEarned,
                update.CardsReviewed,
                refreshBadgesAfterPersist,
                unlockedBadgeCount);
            return OperationResult.Success();
        }

        private async Task<List<GoalsBadgeViewModel>> BuildBadgesAsync(int userId, GoalsBadgeMetrics metrics)
        {
            var today = ResolveBusinessDate();
            var badges = await _context.Badges
                .AsNoTracking()
                .OrderBy(badge => badge.SortOrder)
                .ThenBy(badge => badge.Id)
                .ToListAsync();

            if (badges.Count == 0)
            {
                return new List<GoalsBadgeViewModel>();
            }

            var awardedLookup = await _context.UserBadges
                .AsNoTracking()
                .Where(userBadge => userBadge.UserId == userId)
                .ToDictionaryAsync(userBadge => userBadge.BadgeId, userBadge => userBadge.AwardedAt);

            return badges
                .Select(badge => BuildBadgeViewModel(badge, metrics, awardedLookup, today))
                .ToList();
        }

        private async Task<GoalsBadgeMetrics?> BuildBadgeMetricsAsync(
            int userId,
            DateTime? lastStudyDate = null,
            int? storedStreak = null,
            int? storedLongestStreak = null)
        {
            if (!lastStudyDate.HasValue && !storedStreak.HasValue && !storedLongestStreak.HasValue)
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .Where(candidate => candidate.UserId == userId)
                    .Select(candidate => new
                    {
                        candidate.LastStudyDate,
                        candidate.Streak,
                        candidate.LongestStreak
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return null;
                }

                lastStudyDate = user.LastStudyDate;
                storedStreak = user.Streak;
                storedLongestStreak = user.LongestStreak;
            }

            var today = ResolveBusinessDate();
            var currentStreak = ComputeCurrentStreak(lastStudyDate, storedStreak, today);
            var longestStreak = Math.Max(storedLongestStreak ?? 0, currentStreak);

            var columnSupport = await GetUserDailyActivityColumnSupportAsync();
            var totals = columnSupport.HasAllPhase2AreaCounters
                ? await _context.UserDailyActivities
                    .AsNoTracking()
                    .Where(activity => activity.UserId == userId)
                    .GroupBy(_ => 1)
                    .Select(group => new DailyActivityTotals
                    {
                        TotalDaysActive = group.Count(),
                        TotalXp = group.Sum(activity => activity.XpEarned),
                        VocabularyCompletions = group.Sum(activity => activity.VocabularyCompletedCount),
                        WritingExercisesCompleted = group.Sum(activity => activity.WritingCompletedCount)
                    })
                    .FirstOrDefaultAsync()
                : await QueryDailyActivityTotalsCompatibilityAsync(userId, columnSupport);

            var speakingVideosCompleted = await _context.UserSpeakingVideoCompletions
                .AsNoTracking()
                .CountAsync(completion => completion.UserId == userId && completion.IsCompleted);

            return new GoalsBadgeMetrics
            {
                CurrentStreak = currentStreak,
                LongestStreak = longestStreak,
                TotalDaysActive = totals?.TotalDaysActive ?? 0,
                TotalXp = totals?.TotalXp ?? 0,
                SpeakingVideosCompleted = speakingVideosCompleted,
                VocabularyCompletions = totals?.VocabularyCompletions ?? 0,
                WritingExercisesCompleted = totals?.WritingExercisesCompleted ?? 0
            };
        }

        private async Task<int> RefreshUserBadgesAsync(int userId, GoalsBadgeMetrics? existingMetrics = null)
        {
            var metrics = existingMetrics ?? await BuildBadgeMetricsAsync(userId);
            if (metrics == null)
            {
                return 0;
            }

            var badgeCatalog = await _context.Badges
                .AsNoTracking()
                .OrderBy(badge => badge.SortOrder)
                .ThenBy(badge => badge.Id)
                .ToListAsync();

            if (badgeCatalog.Count == 0)
            {
                return 0;
            }

            var existingBadgeIds = await _context.UserBadges
                .Where(userBadge => userBadge.UserId == userId)
                .Select(userBadge => userBadge.BadgeId)
                .ToListAsync();

            var existingBadgeIdSet = existingBadgeIds.ToHashSet();
            var badgesToAward = badgeCatalog
                .Where(badge =>
                    !existingBadgeIdSet.Contains(badge.Id)
                    && GetMetricValue(metrics, badge.MetricType) >= badge.ThresholdValue)
                .ToList();

            if (badgesToAward.Count == 0)
            {
                return 0;
            }

            var awardedAt = DateTime.UtcNow;
            var newUserBadges = badgesToAward
                .Select(badge => new UserBadge
                {
                    UserId = userId,
                    BadgeId = badge.Id,
                    AwardedAt = awardedAt
                })
                .ToList();

            _context.UserBadges.AddRange(newUserBadges);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUserBadgeUniqueConflict(ex))
            {
                foreach (var userBadge in newUserBadges)
                {
                    _context.Entry(userBadge).State = EntityState.Detached;
                }

                var persistedBadgeIds = await _context.UserBadges
                    .AsNoTracking()
                    .Where(userBadge => userBadge.UserId == userId)
                    .Select(userBadge => userBadge.BadgeId)
                    .ToListAsync();

                if (!badgesToAward.All(badge => persistedBadgeIds.Contains(badge.Id)))
                {
                    throw;
                }
            }

            _logger.LogInformation(
                "Awarded {badgeCount} badges to user {userId}: {badgeCodes}",
                badgesToAward.Count,
                userId,
                string.Join(", ", badgesToAward.Select(badge => badge.Code)));

            // ── Notification: badge earned ───────────────────────────────
            foreach (var awardedBadge in badgesToAward)
            {
                try
                {
                    await _notificationService.GenerateBadgeNotificationAsync(userId, awardedBadge.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to generate badge notification for user {UserId}, badge {BadgeId}",
                        userId, awardedBadge.Id);
                }
            }

            return badgesToAward.Count;
        }

        private static GoalsBadgeViewModel BuildBadgeViewModel(
            Badge badge,
            GoalsBadgeMetrics metrics,
            IReadOnlyDictionary<int, DateTime> awardedLookup,
            DateTime today)
        {
            var isUnlocked = awardedLookup.TryGetValue(badge.Id, out var awardedAt);
            var currentValue = Math.Min(GetMetricValue(metrics, badge.MetricType), badge.ThresholdValue);

            return new GoalsBadgeViewModel
            {
                Code = badge.Code,
                Name = badge.Name,
                Description = badge.Description,
                IconClass = badge.IconClass,
                IsUnlocked = isUnlocked,
                IsRecentlyUnlocked = isUnlocked && BusinessDateHelper.ToBusinessDateFromUtcStorage(awardedAt) == today,
                ProgressValue = currentValue,
                TargetValue = badge.ThresholdValue,
                ProgressPercent = CalculateProgressPercent(currentValue, badge.ThresholdValue),
                ProgressLabel = BuildBadgeProgressLabel(badge.MetricType, currentValue, badge.ThresholdValue),
                MetricLabel = GetBadgeMetricLabel(badge.MetricType),
                AwardedAt = isUnlocked ? awardedAt : null
            };
        }

        private static int ComputeCurrentStreak(DateTime? lastStudyDate, int? storedStreak, DateTime today)
        {
            var lastStudy = lastStudyDate?.Date;
            if (!lastStudy.HasValue)
            {
                return 0;
            }

            return lastStudy.Value >= today.AddDays(-1)
                ? Math.Max(storedStreak ?? 0, 0)
                : 0;
        }

        private static string BuildStreakMessage(int currentStreak, DateTime? lastStudyDate, DateTime today)
        {
            if (currentStreak > 0)
            {
                return lastStudyDate?.Date == today
                    ? "Hôm nay bạn đã có hoạt động học tập. Hãy tiếp tục giữ nhịp này."
                    : "Bạn đang giữ được streak từ hôm qua. Học thêm hôm nay để không bị ngắt quãng.";
            }

            return lastStudyDate.HasValue
                ? "Streak hiện đang tạm nghỉ. Hoàn thành một buổi học hôm nay để bắt đầu lại."
                : "Bắt đầu buổi học đầu tiên để tạo streak của bạn.";
        }

        private static int CalculateProgressPercent(int currentValue, int targetValue)
        {
            if (targetValue <= 0)
            {
                return 0;
            }

            var rawPercent = (int)Math.Round(currentValue * 100d / targetValue);
            return Math.Clamp(rawPercent, 0, 100);
        }

        private static string BuildWeeklyActivityMessage(bool hasWeeklyActivity)
        {
            return hasWeeklyActivity
                ? "Biểu đồ hiển thị số thẻ đã ôn trong 7 ngày gần nhất."
                : "Chưa ghi nhận hoạt động học tập trong 7 ngày gần nhất. Hoàn thành một buổi học hôm nay để bắt đầu lấp đầy biểu đồ.";
        }

        private static List<GoalsWeekDayViewModel> BuildWeeklyActivity(
            DateTime weekStart,
            DateTime today,
            int dailyGoal,
            IReadOnlyDictionary<DateTime, int> activityByDate)
        {
            var highestCount = activityByDate.Count == 0
                ? 0
                : activityByDate.Values.Max();
            var chartMax = Math.Max(1, Math.Max(highestCount, dailyGoal));
            var weeklyActivity = new List<GoalsWeekDayViewModel>(capacity: 7);

            for (var offset = 0; offset < 7; offset++)
            {
                var date = weekStart.AddDays(offset);
                var activityCount = activityByDate.TryGetValue(date, out var count)
                    ? count
                    : 0;

                weeklyActivity.Add(new GoalsWeekDayViewModel
                {
                    DayLabel = GetDayLabel(date.DayOfWeek),
                    FullLabel = date.ToString("dd/MM"),
                    ActivityCount = activityCount,
                    HeightPercent = activityCount == 0
                        ? 0
                        : Math.Clamp((int)Math.Round(activityCount * 100d / chartMax), 0, 100),
                    IsToday = date == today
                });
            }

            return weeklyActivity;
        }

        private static Dictionary<GoalArea, int> BuildTodayProgressByArea(TodayActivitySnapshot? todayActivity)
        {
            var progressByArea = new Dictionary<GoalArea, int>
            {
                [GoalArea.Vocabulary] = todayActivity?.CardsReviewed ?? 0,
                [GoalArea.Speaking] = todayActivity?.SpeakingCompletedCount ?? 0,
                [GoalArea.Writing] = todayActivity?.WritingCompletedCount ?? 0,
                [GoalArea.Reading] = todayActivity?.ReadingCompletedCount ?? 0,
                [GoalArea.Listening] = todayActivity?.ListeningCompletedCount ?? 0
            };

            return progressByArea;
        }

        private static List<GoalCardViewModel> BuildGoalCards(
            IReadOnlyCollection<UserGoal> activeGoals,
            IReadOnlyDictionary<GoalArea, int> todayProgressByArea)
        {
            return activeGoals
                .Where(goal => IsGoalAreaEnabled(goal.GoalArea))
                .Where(goal => goal.TargetValue >= UpdateGoalInputViewModel.MinTargetValue)
                .Select(goal =>
                {
                    var progressValue = todayProgressByArea.TryGetValue(goal.GoalArea, out var value) ? value : 0;
                    return new GoalCardViewModel
                    {
                        GoalArea = goal.GoalArea,
                        AreaLabel = GetGoalAreaLabel(goal.GoalArea),
                        UnitLabel = GetGoalUnitLabel(goal.GoalArea),
                        TargetValue = goal.TargetValue,
                        TodayProgressValue = progressValue,
                        ProgressPercent = CalculateProgressPercent(progressValue, goal.TargetValue)
                    };
                })
                .OrderBy(card => card.GoalArea)
                .ToList();
        }

        private static List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> BuildGoalAreaOptions(
            IReadOnlyCollection<UserGoal> activeGoals)
        {
            var existingAreas = activeGoals
                .Where(goal => goal.IsActive)
                .Select(goal => goal.GoalArea)
                .ToHashSet();

            return GoalAreasWithRealCompletionSignals
                .Select(area => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = area.ToString(),
                    Text = existingAreas.Contains(area)
                        ? $"{GetGoalAreaLabel(area)} (đã có)"
                        : GetGoalAreaLabel(area)
                })
                .ToList();
        }

        private static List<string> BuildDeferredGoalAreaLabels()
        {
            return Enum.GetValues<GoalArea>()
                .Where(area => !IsGoalAreaEnabled(area))
                .Select(GetGoalAreaLabel)
                .ToList();
        }

        private static bool IsGoalAreaEnabled(GoalArea goalArea)
        {
            return GoalAreasWithRealCompletionSignals.Contains(goalArea);
        }

        private async Task<int> IncrementExistingActivityAsync(int userId, DateTime activityDate, GoalsActivityUpdate update)
        {
            var columnSupport = await GetUserDailyActivityColumnSupportAsync();
            if (columnSupport.HasAllPhase2AreaCounters)
            {
                return await _context.UserDailyActivities
                    .Where(activity => activity.UserId == userId && activity.ActivityDate == activityDate)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(activity => activity.XpEarned, activity => activity.XpEarned + update.XpEarned)
                        .SetProperty(activity => activity.CardsReviewed, activity => activity.CardsReviewed + update.CardsReviewed)
                        .SetProperty(activity => activity.NewCardsLearned, activity => activity.NewCardsLearned + update.NewCardsLearned)
                        .SetProperty(activity => activity.VocabularyCompletedCount, activity => activity.VocabularyCompletedCount + update.VocabularyCompletedCount)
                        .SetProperty(activity => activity.QuizzesCompleted, activity => activity.QuizzesCompleted + update.QuizzesCompleted)
                        .SetProperty(activity => activity.SpeakingCompletedCount, activity => activity.SpeakingCompletedCount + update.SpeakingCompletedCount)
                        .SetProperty(activity => activity.WritingCompletedCount, activity => activity.WritingCompletedCount + update.WritingCompletedCount)
                        .SetProperty(activity => activity.ReadingCompletedCount, activity => activity.ReadingCompletedCount + update.ReadingCompletedCount)
                        .SetProperty(activity => activity.ListeningCompletedCount, activity => activity.ListeningCompletedCount + update.ListeningCompletedCount));
            }

            LogSkippedLegacyAreaCounters(update, columnSupport);

            return await WithDbConnectionAsync(async (connection, transaction) =>
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                var assignments = new List<string>
                {
                    "XpEarned = XpEarned + @xpEarned",
                    "CardsReviewed = CardsReviewed + @cardsReviewed",
                    "NewCardsLearned = NewCardsLearned + @newCardsLearned",
                    "QuizzesCompleted = QuizzesCompleted + @quizzesCompleted",
                    "SpeakingCompletedCount = SpeakingCompletedCount + @speakingCompletedCount"
                };

                if (columnSupport.HasVocabularyCompletedCount)
                {
                    assignments.Add("VocabularyCompletedCount = VocabularyCompletedCount + @vocabularyCompletedCount");
                }

                if (columnSupport.HasWritingCompletedCount)
                {
                    assignments.Add("WritingCompletedCount = WritingCompletedCount + @writingCompletedCount");
                }

                if (columnSupport.HasReadingCompletedCount)
                {
                    assignments.Add("ReadingCompletedCount = ReadingCompletedCount + @readingCompletedCount");
                }

                if (columnSupport.HasListeningCompletedCount)
                {
                    assignments.Add("ListeningCompletedCount = ListeningCompletedCount + @listeningCompletedCount");
                }

                command.CommandText = $@"
UPDATE UserDailyActivities
SET {string.Join(", ", assignments)}
WHERE UserID = @userId
  AND ActivityDate = @activityDate";

                AddIntParameter(command, "@xpEarned", update.XpEarned);
                AddIntParameter(command, "@cardsReviewed", update.CardsReviewed);
                AddIntParameter(command, "@newCardsLearned", update.NewCardsLearned);
                AddIntParameter(command, "@quizzesCompleted", update.QuizzesCompleted);
                AddIntParameter(command, "@speakingCompletedCount", update.SpeakingCompletedCount);
                AddIntParameter(command, "@userId", userId);
                AddDateParameter(command, "@activityDate", activityDate);

                if (columnSupport.HasVocabularyCompletedCount)
                {
                    AddIntParameter(command, "@vocabularyCompletedCount", update.VocabularyCompletedCount);
                }

                if (columnSupport.HasWritingCompletedCount)
                {
                    AddIntParameter(command, "@writingCompletedCount", update.WritingCompletedCount);
                }

                if (columnSupport.HasReadingCompletedCount)
                {
                    AddIntParameter(command, "@readingCompletedCount", update.ReadingCompletedCount);
                }

                if (columnSupport.HasListeningCompletedCount)
                {
                    AddIntParameter(command, "@listeningCompletedCount", update.ListeningCompletedCount);
                }

                return await command.ExecuteNonQueryAsync();
            });
        }

        private async Task<TodayActivitySnapshot?> GetTodayActivitySnapshotAsync(int userId, DateTime activityDate)
        {
            var columnSupport = await GetUserDailyActivityColumnSupportAsync();
            if (columnSupport.HasAllPhase2AreaCounters)
            {
                return await _context.UserDailyActivities
                    .AsNoTracking()
                    .Where(activity => activity.UserId == userId && activity.ActivityDate == activityDate)
                    .Select(activity => new TodayActivitySnapshot
                    {
                        CardsReviewed = activity.CardsReviewed,
                        VocabularyCompletedCount = activity.VocabularyCompletedCount,
                        SpeakingCompletedCount = activity.SpeakingCompletedCount,
                        QuizzesCompleted = activity.QuizzesCompleted,
                        WritingCompletedCount = activity.WritingCompletedCount,
                        ReadingCompletedCount = activity.ReadingCompletedCount,
                        ListeningCompletedCount = activity.ListeningCompletedCount
                    })
                    .FirstOrDefaultAsync();
            }

            return await WithDbConnectionAsync(async (connection, transaction) =>
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                var selectedColumns = new List<string>
                {
                    "CardsReviewed",
                    "SpeakingCompletedCount",
                    "QuizzesCompleted"
                };

                if (columnSupport.HasVocabularyCompletedCount)
                {
                    selectedColumns.Add("VocabularyCompletedCount");
                }

                if (columnSupport.HasWritingCompletedCount)
                {
                    selectedColumns.Add("WritingCompletedCount");
                }

                if (columnSupport.HasReadingCompletedCount)
                {
                    selectedColumns.Add("ReadingCompletedCount");
                }

                if (columnSupport.HasListeningCompletedCount)
                {
                    selectedColumns.Add("ListeningCompletedCount");
                }

                command.CommandText = $@"
SELECT {string.Join(", ", selectedColumns)}
FROM UserDailyActivities
WHERE UserID = @userId
  AND ActivityDate = @activityDate";

                AddIntParameter(command, "@userId", userId);
                AddDateParameter(command, "@activityDate", activityDate);

                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return new TodayActivitySnapshot
                {
                    CardsReviewed = GetRequiredInt32(reader, "CardsReviewed"),
                    VocabularyCompletedCount = columnSupport.HasVocabularyCompletedCount
                        ? GetRequiredInt32(reader, "VocabularyCompletedCount")
                        : 0,
                    SpeakingCompletedCount = GetRequiredInt32(reader, "SpeakingCompletedCount"),
                    QuizzesCompleted = GetRequiredInt32(reader, "QuizzesCompleted"),
                    WritingCompletedCount = columnSupport.HasWritingCompletedCount
                        ? GetRequiredInt32(reader, "WritingCompletedCount")
                        : 0,
                    ReadingCompletedCount = columnSupport.HasReadingCompletedCount
                        ? GetRequiredInt32(reader, "ReadingCompletedCount")
                        : 0,
                    ListeningCompletedCount = columnSupport.HasListeningCompletedCount
                        ? GetRequiredInt32(reader, "ListeningCompletedCount")
                        : 0
                };
            });
        }

        private async Task<DailyActivityTotals?> QueryDailyActivityTotalsCompatibilityAsync(
            int userId,
            UserDailyActivityColumnSupport columnSupport)
        {
            return await WithDbConnectionAsync(async (connection, transaction) =>
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $@"
SELECT
    COUNT(*) AS TotalDaysActive,
    COALESCE(SUM(XpEarned), 0) AS TotalXp,
    {(columnSupport.HasVocabularyCompletedCount ? "COALESCE(SUM(VocabularyCompletedCount), 0)" : "0")} AS VocabularyCompletions,
    {(columnSupport.HasWritingCompletedCount ? "COALESCE(SUM(WritingCompletedCount), 0)" : "0")} AS WritingExercisesCompleted
FROM UserDailyActivities
WHERE UserID = @userId";

                AddIntParameter(command, "@userId", userId);

                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return new DailyActivityTotals
                {
                    TotalDaysActive = GetRequiredInt32(reader, "TotalDaysActive"),
                    TotalXp = GetRequiredInt32(reader, "TotalXp"),
                    VocabularyCompletions = GetRequiredInt32(reader, "VocabularyCompletions"),
                    WritingExercisesCompleted = GetRequiredInt32(reader, "WritingExercisesCompleted")
                };
            });
        }

        private async Task InsertUserDailyActivityAsync(
            int userId,
            DateTime activityDate,
            GoalsActivityUpdate update,
            int streakXpAwarded)
        {
            var columnSupport = await GetUserDailyActivityColumnSupportAsync();
            if (columnSupport.HasAllPhase2AreaCounters)
            {
                var dailyActivity = new UserDailyActivity
                {
                    UserId = userId,
                    ActivityDate = activityDate,
                    XpEarned = update.XpEarned,
                    StreakXpAwarded = streakXpAwarded,
                    CardsReviewed = update.CardsReviewed,
                    NewCardsLearned = update.NewCardsLearned,
                    VocabularyCompletedCount = update.VocabularyCompletedCount,
                    QuizzesCompleted = update.QuizzesCompleted,
                    SpeakingCompletedCount = update.SpeakingCompletedCount,
                    WritingCompletedCount = update.WritingCompletedCount,
                    ReadingCompletedCount = update.ReadingCompletedCount,
                    ListeningCompletedCount = update.ListeningCompletedCount
                };

                _context.UserDailyActivities.Add(dailyActivity);
                await _context.SaveChangesAsync();
                return;
            }

            LogSkippedLegacyAreaCounters(update, columnSupport);

            await WithDbConnectionAsync(async (connection, transaction) =>
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                var columns = new List<string>
                {
                    "UserID",
                    "ActivityDate",
                    "XpEarned",
                    "StreakXpAwarded",
                    "CardsReviewed",
                    "NewCardsLearned",
                    "QuizzesCompleted",
                    "SpeakingCompletedCount"
                };

                var values = new List<string>
                {
                    "@userId",
                    "@activityDate",
                    "@xpEarned",
                    "@streakXpAwarded",
                    "@cardsReviewed",
                    "@newCardsLearned",
                    "@quizzesCompleted",
                    "@speakingCompletedCount"
                };

                if (columnSupport.HasVocabularyCompletedCount)
                {
                    columns.Add("VocabularyCompletedCount");
                    values.Add("@vocabularyCompletedCount");
                }

                if (columnSupport.HasWritingCompletedCount)
                {
                    columns.Add("WritingCompletedCount");
                    values.Add("@writingCompletedCount");
                }

                if (columnSupport.HasReadingCompletedCount)
                {
                    columns.Add("ReadingCompletedCount");
                    values.Add("@readingCompletedCount");
                }

                if (columnSupport.HasListeningCompletedCount)
                {
                    columns.Add("ListeningCompletedCount");
                    values.Add("@listeningCompletedCount");
                }

                command.CommandText = $@"
INSERT INTO UserDailyActivities ({string.Join(", ", columns)})
VALUES ({string.Join(", ", values)})";

                AddIntParameter(command, "@userId", userId);
                AddDateParameter(command, "@activityDate", activityDate);
                AddIntParameter(command, "@xpEarned", update.XpEarned);
                AddIntParameter(command, "@streakXpAwarded", streakXpAwarded);
                AddIntParameter(command, "@cardsReviewed", update.CardsReviewed);
                AddIntParameter(command, "@newCardsLearned", update.NewCardsLearned);
                AddIntParameter(command, "@quizzesCompleted", update.QuizzesCompleted);
                AddIntParameter(command, "@speakingCompletedCount", update.SpeakingCompletedCount);

                if (columnSupport.HasVocabularyCompletedCount)
                {
                    AddIntParameter(command, "@vocabularyCompletedCount", update.VocabularyCompletedCount);
                }

                if (columnSupport.HasWritingCompletedCount)
                {
                    AddIntParameter(command, "@writingCompletedCount", update.WritingCompletedCount);
                }

                if (columnSupport.HasReadingCompletedCount)
                {
                    AddIntParameter(command, "@readingCompletedCount", update.ReadingCompletedCount);
                }

                if (columnSupport.HasListeningCompletedCount)
                {
                    AddIntParameter(command, "@listeningCompletedCount", update.ListeningCompletedCount);
                }

                await command.ExecuteNonQueryAsync();
                return 0;
            });
        }

        private async Task<UserDailyActivityColumnSupport> GetUserDailyActivityColumnSupportAsync()
        {
            if (_userDailyActivityColumnSupport != null)
            {
                return _userDailyActivityColumnSupport;
            }

            var columnNames = await LoadUserDailyActivityColumnNamesAsync();
            _userDailyActivityColumnSupport = new UserDailyActivityColumnSupport(
                columnNames.Contains(nameof(UserDailyActivity.VocabularyCompletedCount), StringComparer.OrdinalIgnoreCase),
                columnNames.Contains(nameof(UserDailyActivity.WritingCompletedCount), StringComparer.OrdinalIgnoreCase),
                columnNames.Contains(nameof(UserDailyActivity.ReadingCompletedCount), StringComparer.OrdinalIgnoreCase),
                columnNames.Contains(nameof(UserDailyActivity.ListeningCompletedCount), StringComparer.OrdinalIgnoreCase));

            if (_userDailyActivityColumnSupport.HasMissingPhase2AreaCounters)
            {
                _logger.LogWarning(
                    "UserDailyActivities is missing goals phase-2 counters ({missingColumns}). Goals will use compatibility mode until migration 20260404143000_AddUserDailyActivityAreaCountersPhase2 is applied.",
                    string.Join(", ", _userDailyActivityColumnSupport.GetMissingColumnNames()));
            }

            return _userDailyActivityColumnSupport;
        }

        private async Task<HashSet<string>> LoadUserDailyActivityColumnNamesAsync()
        {
            if (_context.Database.IsSqlServer())
            {
                return await WithDbConnectionAsync(async (connection, transaction) =>
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'UserDailyActivities'";

                    return await ReadSingleStringColumnAsync(command, 0);
                });
            }

            if (string.Equals(
                _context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
            {
                return await WithDbConnectionAsync(async (connection, transaction) =>
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "PRAGMA table_info('UserDailyActivities');";

                    return await ReadSingleStringColumnAsync(command, 1);
                });
            }

            _logger.LogWarning(
                "GoalsService schema compatibility check does not support provider {providerName}; assuming phase-2 UserDailyActivities columns exist.",
                _context.Database.ProviderName);

            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                nameof(UserDailyActivity.VocabularyCompletedCount),
                nameof(UserDailyActivity.WritingCompletedCount),
                nameof(UserDailyActivity.ReadingCompletedCount),
                nameof(UserDailyActivity.ListeningCompletedCount)
            };
        }

        private void LogSkippedLegacyAreaCounters(GoalsActivityUpdate update, UserDailyActivityColumnSupport columnSupport)
        {
            var skippedColumns = new List<string>();

            if (!columnSupport.HasVocabularyCompletedCount && update.VocabularyCompletedCount > 0)
            {
                skippedColumns.Add(nameof(UserDailyActivity.VocabularyCompletedCount));
            }

            if (!columnSupport.HasWritingCompletedCount && update.WritingCompletedCount > 0)
            {
                skippedColumns.Add(nameof(UserDailyActivity.WritingCompletedCount));
            }

            if (!columnSupport.HasReadingCompletedCount && update.ReadingCompletedCount > 0)
            {
                skippedColumns.Add(nameof(UserDailyActivity.ReadingCompletedCount));
            }

            if (!columnSupport.HasListeningCompletedCount && update.ListeningCompletedCount > 0)
            {
                skippedColumns.Add(nameof(UserDailyActivity.ListeningCompletedCount));
            }

            if (skippedColumns.Count == 0)
            {
                return;
            }

            _logger.LogWarning(
                "Skipped goal area counter increments for legacy UserDailyActivities schema because columns are missing: {missingColumns}",
                string.Join(", ", skippedColumns));
        }

        private async Task<TResult> WithDbConnectionAsync<TResult>(Func<DbConnection, DbTransaction?, Task<TResult>> action)
        {
            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;
            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                return await action(connection, transaction);
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    connection.Close();
                }
            }
        }

        private static async Task<HashSet<string>> ReadSingleStringColumnAsync(DbCommand command, int ordinal)
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(ordinal))
                {
                    values.Add(reader.GetString(ordinal));
                }
            }

            return values;
        }

        private static void AddIntParameter(DbCommand command, string name, int value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = DbType.Int32;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private static void AddDateParameter(DbCommand command, string name, DateTime value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = DbType.Date;
            parameter.Value = value.Date;
            command.Parameters.Add(parameter);
        }

        private static int GetRequiredInt32(DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal)
                ? 0
                : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static VocabularyActivityKind DetermineVocabularyActivityKind(bool isNewProgress, string? previousStatus, string currentStatus)
        {
            if (!string.Equals(previousStatus, "Mastered", StringComparison.Ordinal)
                && string.Equals(currentStatus, "Mastered", StringComparison.Ordinal))
            {
                return VocabularyActivityKind.Mastered;
            }

            return isNewProgress
                ? VocabularyActivityKind.NewLearning
                : VocabularyActivityKind.Review;
        }

        private static DateTime ResolveBusinessDate()
        {
            return BusinessDateHelper.Today;
        }

        private async Task<int> RefreshBadgesIfNeededAsync(int userId, bool refreshBadgesAfterPersist)
        {
            if (!refreshBadgesAfterPersist)
            {
                return 0;
            }

            return await RefreshUserBadgesAsync(userId);
        }

        private static bool IsUserDailyActivityUniqueConflict(Exception exception)
        {
            return IsUniqueConstraintViolation(exception);
        }

        private static bool IsUserBadgeUniqueConflict(Exception exception)
        {
            return IsUniqueConstraintViolation(exception);
        }

        private static bool IsUniqueConstraintViolation(Exception exception)
        {
            var sqlException = FindException<SqlException>(exception);
            if (sqlException is { Number: 2601 or 2627 })
            {
                return true;
            }

            var sqliteException = FindException(
                exception,
                candidate => string.Equals(
                    candidate.GetType().FullName,
                    "Microsoft.Data.Sqlite.SqliteException",
                    StringComparison.Ordinal));

            return sqliteException != null && GetExceptionIntProperty(sqliteException, "SqliteErrorCode") == 19;
        }

        private static TException? FindException<TException>(Exception exception)
            where TException : Exception
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is TException typedException)
                {
                    return typedException;
                }
            }

            return null;
        }

        private static Exception? FindException(Exception exception, Func<Exception, bool> predicate)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (predicate(current))
                {
                    return current;
                }
            }

            return null;
        }

        private static int? GetExceptionIntProperty(Exception exception, string propertyName)
        {
            var property = exception.GetType().GetProperty(propertyName);
            if (property?.PropertyType != typeof(int))
            {
                return null;
            }

            return (int?)property.GetValue(exception);
        }

        private static int GetMetricValue(GoalsBadgeMetrics metrics, BadgeMetricType metricType)
        {
            return metricType switch
            {
                BadgeMetricType.CurrentStreak => metrics.CurrentStreak,
                BadgeMetricType.LongestStreak => metrics.LongestStreak,
                BadgeMetricType.TotalDaysActive => metrics.TotalDaysActive,
                BadgeMetricType.TotalXp => metrics.TotalXp,
                BadgeMetricType.SpeakingVideosCompleted => metrics.SpeakingVideosCompleted,
                BadgeMetricType.VocabularyCompletions => metrics.VocabularyCompletions,
                BadgeMetricType.WritingExercisesCompleted => metrics.WritingExercisesCompleted,
                _ => 0
            };
        }

        private static string BuildBadgeProgressLabel(BadgeMetricType metricType, int currentValue, int targetValue)
        {
            var unit = GetBadgeMetricLabel(metricType);
            return $"{currentValue}/{targetValue} {unit}";
        }

        private static string GetBadgeMetricLabel(BadgeMetricType metricType)
        {
            return metricType switch
            {
                BadgeMetricType.TotalXp => "XP",
                BadgeMetricType.SpeakingVideosCompleted => "video",
                BadgeMetricType.VocabularyCompletions => "thẻ",
                BadgeMetricType.WritingExercisesCompleted => "bài",
                _ => "ngày"
            };
        }

        private async Task<int> AwardStreakXpIfNeededAsync(int userId, DateTime activityDate)
        {
            var columnSupport = await GetUserDailyActivityColumnSupportAsync();
            if (!columnSupport.HasAllPhase2AreaCounters)
            {
                return await AwardStreakXpCompatibilityAsync(userId, activityDate);
            }

            var awardedRows = await _context.UserDailyActivities
                .Where(activity =>
                    activity.UserId == userId
                    && activity.ActivityDate == activityDate
                    && activity.StreakXpAwarded == 0)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(activity => activity.StreakXpAwarded, _ => StreakIncreaseXp)
                    .SetProperty(activity => activity.XpEarned, activity => activity.XpEarned + StreakIncreaseXp));

            if (awardedRows > 0)
            {
                return StreakIncreaseXp;
            }

            var existingActivity = await _context.UserDailyActivities
                .AsNoTracking()
                .Where(activity => activity.UserId == userId && activity.ActivityDate == activityDate)
                .Select(activity => new { activity.Id, activity.StreakXpAwarded })
                .FirstOrDefaultAsync();

            if (existingActivity != null)
            {
                return 0;
            }

            try
            {
                await InsertUserDailyActivityAsync(
                    userId,
                    activityDate,
                    new GoalsActivityUpdate { XpEarned = StreakIncreaseXp },
                    streakXpAwarded: StreakIncreaseXp);
                return StreakIncreaseXp;
            }
            catch (Exception ex) when (IsUserDailyActivityUniqueConflict(ex))
            {
                var recoveredRows = await _context.UserDailyActivities
                    .Where(activity =>
                        activity.UserId == userId
                        && activity.ActivityDate == activityDate
                        && activity.StreakXpAwarded == 0)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(activity => activity.StreakXpAwarded, _ => StreakIncreaseXp)
                        .SetProperty(activity => activity.XpEarned, activity => activity.XpEarned + StreakIncreaseXp));

                return recoveredRows > 0 ? StreakIncreaseXp : 0;
            }
        }

        private async Task<int> AwardStreakXpCompatibilityAsync(int userId, DateTime activityDate)
        {
            var awardedRows = await WithDbConnectionAsync(async (connection, transaction) =>
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE UserDailyActivities
SET StreakXpAwarded = @streakXpAwarded,
    XpEarned = XpEarned + @streakXpAwarded
WHERE UserID = @userId
  AND ActivityDate = @activityDate
  AND StreakXpAwarded = 0";

                AddIntParameter(command, "@streakXpAwarded", StreakIncreaseXp);
                AddIntParameter(command, "@userId", userId);
                AddDateParameter(command, "@activityDate", activityDate);
                return await command.ExecuteNonQueryAsync();
            });

            if (awardedRows > 0)
            {
                return StreakIncreaseXp;
            }

            var existingActivity = await _context.UserDailyActivities
                .AsNoTracking()
                .Where(activity => activity.UserId == userId && activity.ActivityDate == activityDate)
                .Select(activity => new { activity.Id, activity.StreakXpAwarded })
                .FirstOrDefaultAsync();

            if (existingActivity != null)
            {
                return 0;
            }

            try
            {
                await InsertUserDailyActivityAsync(
                    userId,
                    activityDate,
                    new GoalsActivityUpdate { XpEarned = StreakIncreaseXp },
                    streakXpAwarded: StreakIncreaseXp);
                return StreakIncreaseXp;
            }
            catch (Exception ex) when (IsUserDailyActivityUniqueConflict(ex))
            {
                var recoveredRows = await WithDbConnectionAsync(async (connection, transaction) =>
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = @"
UPDATE UserDailyActivities
SET StreakXpAwarded = @streakXpAwarded,
    XpEarned = XpEarned + @streakXpAwarded
WHERE UserID = @userId
  AND ActivityDate = @activityDate
  AND StreakXpAwarded = 0";

                    AddIntParameter(command, "@streakXpAwarded", StreakIncreaseXp);
                    AddIntParameter(command, "@userId", userId);
                    AddDateParameter(command, "@activityDate", activityDate);
                    return await command.ExecuteNonQueryAsync();
                });

                return recoveredRows > 0 ? StreakIncreaseXp : 0;
            }
        }

        private static string GetGoalAreaLabel(GoalArea goalArea)
        {
            return goalArea switch
            {
                GoalArea.Vocabulary => "Vocabulary",
                GoalArea.Speaking => "Speaking",
                GoalArea.Writing => "Writing",
                GoalArea.Reading => "Reading",
                GoalArea.Listening => "Listening",
                _ => "Khác"
            };
        }

        private static string GetGoalUnitLabel(GoalArea goalArea)
        {
            return goalArea switch
            {
                GoalArea.Vocabulary => "thẻ",
                GoalArea.Speaking => "video",
                GoalArea.Writing => "bài",
                GoalArea.Reading => "bài",
                GoalArea.Listening => "bài",
                _ => "đơn vị"
            };
        }

        private static string GetDayLabel(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => "T2",
                DayOfWeek.Tuesday => "T3",
                DayOfWeek.Wednesday => "T4",
                DayOfWeek.Thursday => "T5",
                DayOfWeek.Friday => "T6",
                DayOfWeek.Saturday => "T7",
                _ => "CN"
            };
        }

        private sealed class GoalsBadgeMetrics
        {
            public int CurrentStreak { get; init; }

            public int LongestStreak { get; init; }

            public int TotalDaysActive { get; init; }

            public int TotalXp { get; init; }

            public int SpeakingVideosCompleted { get; init; }

            public int VocabularyCompletions { get; init; }

            public int WritingExercisesCompleted { get; init; }
        }

        private sealed class DailyActivityTotals
        {
            public int TotalDaysActive { get; init; }

            public int TotalXp { get; init; }

            public int VocabularyCompletions { get; init; }

            public int WritingExercisesCompleted { get; init; }
        }

        private sealed class TodayActivitySnapshot
        {
            public int CardsReviewed { get; init; }

            public int VocabularyCompletedCount { get; init; }

            public int SpeakingCompletedCount { get; init; }

            public int QuizzesCompleted { get; init; }

            public int WritingCompletedCount { get; init; }

            public int ReadingCompletedCount { get; init; }

            public int ListeningCompletedCount { get; init; }
        }

        private sealed class UserDailyActivityColumnSupport(
            bool hasVocabularyCompletedCount,
            bool hasWritingCompletedCount,
            bool hasReadingCompletedCount,
            bool hasListeningCompletedCount)
        {
            public bool HasVocabularyCompletedCount { get; } = hasVocabularyCompletedCount;

            public bool HasWritingCompletedCount { get; } = hasWritingCompletedCount;

            public bool HasReadingCompletedCount { get; } = hasReadingCompletedCount;

            public bool HasListeningCompletedCount { get; } = hasListeningCompletedCount;

            public bool HasAllPhase2AreaCounters =>
                HasVocabularyCompletedCount
                && HasWritingCompletedCount
                && HasReadingCompletedCount
                && HasListeningCompletedCount;

            public bool HasMissingPhase2AreaCounters => !HasAllPhase2AreaCounters;

            public IReadOnlyList<string> GetMissingColumnNames()
            {
                var missingColumns = new List<string>();

                if (!HasVocabularyCompletedCount)
                {
                    missingColumns.Add(nameof(UserDailyActivity.VocabularyCompletedCount));
                }

                if (!HasWritingCompletedCount)
                {
                    missingColumns.Add(nameof(UserDailyActivity.WritingCompletedCount));
                }

                if (!HasReadingCompletedCount)
                {
                    missingColumns.Add(nameof(UserDailyActivity.ReadingCompletedCount));
                }

                if (!HasListeningCompletedCount)
                {
                    missingColumns.Add(nameof(UserDailyActivity.ListeningCompletedCount));
                }

                return missingColumns;
            }
        }

        private enum VocabularyActivityKind
        {
            Review,
            NewLearning,
            Mastered
        }
    }
}
