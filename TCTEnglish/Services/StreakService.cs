using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTVocabulary.Services
{
    public class StreakService : IStreakService
    {
        private readonly DbflashcardContext _context;
        private readonly ILogger<StreakService> _logger;

        public StreakService(DbflashcardContext context, ILogger<StreakService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<int> UpdateStreakAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                _logger.LogWarning("Cannot update streak because user {userId} was not found", userId);
                return 0;
            }

            var today = DateTime.UtcNow.Date;
            var lastStudy = user.LastStudyDate?.Date;
            var previousStreak = user.Streak ?? 0;

            if (lastStudy != today)
            {
                user.Streak = lastStudy == today.AddDays(-1)
                    ? (user.Streak ?? 0) + 1
                    : 1;

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
            return user.Streak ?? 0;
        }
    }
}
