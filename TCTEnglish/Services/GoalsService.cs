using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTEnglish.ViewModels;

namespace TCTVocabulary.Services
{
    public class GoalsService : IGoalsService
    {
        private readonly DbflashcardContext _context;
        private readonly IStreakService _streakService;
        private readonly ILogger<GoalsService> _logger;

        public GoalsService(
            DbflashcardContext context,
            IStreakService streakService,
            ILogger<GoalsService> logger)
        {
            _context = context;
            _streakService = streakService;
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

            var currentStreak = ComputeCurrentStreak(user.LastStudyDate, user.Streak, today);
            var longestStreak = Math.Max(user.LongestStreak ?? 0, currentStreak);
            var dailyGoal = Math.Max(UpdateGoalInputViewModel.MinDailyGoal, user.Goal ?? 0);
            var activityByDate = dailyActivities.ToDictionary(
                activity => activity.Date.Date,
                activity => activity.CardsReviewed);
            var weeklyActivity = BuildWeeklyActivity(weekStart, today, dailyGoal, activityByDate);
            var hasWeeklyActivity = weeklyActivity.Any(day => day.ActivityCount > 0);
            var todayProgressValue = activityByDate.TryGetValue(today, out var reviewedToday)
                ? reviewedToday
                : 0;

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
                DailyGoal = dailyGoal,
                TodayProgressValue = todayProgressValue,
                TodayProgressMax = dailyGoal,
                ProgressPercent = CalculateProgressPercent(todayProgressValue, dailyGoal),
                WeeklyActivity = weeklyActivity,
                WeeklyActivityMessage = BuildWeeklyActivityMessage(hasWeeklyActivity),
                Badges = badges,
                GoalEditor = new UpdateGoalInputViewModel
                {
                    DailyGoal = dailyGoal
                }
            };
        }

        public async Task<OperationResult> UpdateGoalAsync(int userId, int dailyGoal)
        {
            if (dailyGoal < UpdateGoalInputViewModel.MinDailyGoal || dailyGoal > UpdateGoalInputViewModel.MaxDailyGoal)
            {
                return OperationResult.Invalid(
                    $"Mục tiêu ngày phải từ {UpdateGoalInputViewModel.MinDailyGoal} đến {UpdateGoalInputViewModel.MaxDailyGoal} thẻ.");
            }

            var user = await _context.Users
                .Where(u => u.UserId == userId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("Goal update failed because user {userId} was not found", userId);
                return OperationResult.NotFound();
            }

            user.Goal = dailyGoal;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated daily goal for user {userId} to {dailyGoal}", userId, dailyGoal);
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

            var streak = await _streakService.UpdateStreakAsync(userId);
            var unlockedBadgeCount = await RefreshUserBadgesAsync(userId);

            _logger.LogDebug(
                "Recorded learning activity for user {userId}, streak {streak}, unlockedBadges {unlockedBadgeCount}",
                userId,
                streak,
                unlockedBadgeCount);

            return GoalsActivityRecordResult.Success(streak);
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

            var dailyActivity = new UserDailyActivity
            {
                UserId = userId,
                ActivityDate = activityDate,
                XpEarned = update.XpEarned,
                CardsReviewed = update.CardsReviewed,
                NewCardsLearned = update.NewCardsLearned,
                QuizzesCompleted = update.QuizzesCompleted,
                SpeakingCompletedCount = update.SpeakingCompletedCount
            };

            _context.UserDailyActivities.Add(dailyActivity);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUserDailyActivityUniqueConflict(ex))
            {
                _context.Entry(dailyActivity).State = EntityState.Detached;

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

            var totals = await _context.UserDailyActivities
                .AsNoTracking()
                .Where(activity => activity.UserId == userId)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    TotalDaysActive = group.Count(),
                    TotalXp = group.Sum(activity => activity.XpEarned)
                })
                .FirstOrDefaultAsync();

            return new GoalsBadgeMetrics
            {
                CurrentStreak = currentStreak,
                LongestStreak = longestStreak,
                TotalDaysActive = totals?.TotalDaysActive ?? 0,
                TotalXp = totals?.TotalXp ?? 0
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

        private Task<int> IncrementExistingActivityAsync(int userId, DateTime activityDate, GoalsActivityUpdate update)
        {
            return _context.UserDailyActivities
                .Where(activity => activity.UserId == userId && activity.ActivityDate == activityDate)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(activity => activity.XpEarned, activity => activity.XpEarned + update.XpEarned)
                    .SetProperty(activity => activity.CardsReviewed, activity => activity.CardsReviewed + update.CardsReviewed)
                    .SetProperty(activity => activity.NewCardsLearned, activity => activity.NewCardsLearned + update.NewCardsLearned)
                    .SetProperty(activity => activity.QuizzesCompleted, activity => activity.QuizzesCompleted + update.QuizzesCompleted)
                    .SetProperty(activity => activity.SpeakingCompletedCount, activity => activity.SpeakingCompletedCount + update.SpeakingCompletedCount));
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

        private static bool IsUserDailyActivityUniqueConflict(DbUpdateException exception)
        {
            return IsUniqueConstraintViolation(exception);
        }

        private static bool IsUserBadgeUniqueConflict(DbUpdateException exception)
        {
            return IsUniqueConstraintViolation(exception);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
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
            return metricType == BadgeMetricType.TotalXp ? "XP" : "ngày";
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
        }
    }
}
