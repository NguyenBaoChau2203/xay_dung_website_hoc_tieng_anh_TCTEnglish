using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTVocabulary.Services
{
    public class StreakService : IStreakService
    {
        private readonly DbflashcardContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<StreakService> _logger;

        public StreakService(
            DbflashcardContext context,
            INotificationService notificationService,
            ILogger<StreakService> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<int> UpdateStreakAsync(int userId)
        {
            var result = await UpdateStreakWithMetadataAsync(userId);
            return result.CurrentStreak;
        }

        public async Task<StreakUpdateResult> UpdateStreakWithMetadataAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                _logger.LogWarning("Cannot update streak because user {userId} was not found", userId);
                return new StreakUpdateResult
                {
                    CurrentStreak = 0,
                    DidIncrease = false
                };
            }

            var today = BusinessDateHelper.Today;
            var lastStudy = user.LastStudyDate?.Date;
            var previousStreak = user.Streak ?? 0;
            var previousLongestStreak = user.LongestStreak ?? 0;

            var didIncrease = false;
            if (lastStudy != today)
            {
                user.Streak = lastStudy == today.AddDays(-1)
                    ? (user.Streak ?? 0) + 1
                    : 1;
                didIncrease = true;

                user.LastStudyDate = today;

                if ((user.Streak ?? 0) > (user.LongestStreak ?? 0))
                {
                    user.LongestStreak = user.Streak;
                }

                _logger.LogInformation(
                    "Streak updated for user {userId}: previousStreak {previousStreak}, currentStreak {currentStreak}, longestStreak {longestStreak}, lastStudyDate {lastStudyDate}",
                    userId,
                    previousStreak,
                    user.Streak,
                    user.LongestStreak,
                    user.LastStudyDate);
            }
            else
            {
                _logger.LogDebug(
                    "Streak already updated today for user {userId}, currentStreak {currentStreak}",
                    userId,
                    user.Streak);
            }

            await _context.SaveChangesAsync();

            var result = new StreakUpdateResult
            {
                CurrentStreak = user.Streak ?? 0,
                DidIncrease = didIncrease
            };

            // ── Notification: streak record ──────────────────────────────
            // Fire-and-forget — never let notification errors break streak logic
            if (didIncrease && (user.Streak ?? 0) > previousLongestStreak)
            {
                try
                {
                    await _notificationService.GenerateStreakNotificationsAsync(userId, result);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to generate streak notification for user {UserId}", userId);
                }
            }

            return result;
        }
    }
}

