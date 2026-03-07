using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // [FIX-AI-AUTH]
using System.Security.Claims; // [FIX-AI-AUTH]
using TCTVocabulary.Models;

namespace TCTVocabulary.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // [FIX-AI-AUTH]
    public class LearningApiController : ControllerBase
    {
        private readonly DbflashcardContext _context;

        public LearningApiController(DbflashcardContext context)
        {
            _context = context;
        }

        [HttpPost("record")]
        public async Task<IActionResult> Record([FromBody] LearningRecordRequest request)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!); // [FIX-AI-AUTH]

            var progress = await _context.LearningProgresses
                .FirstOrDefaultAsync(lp => lp.CardId == request.CardId && lp.UserId == currentUserId);

            if (progress == null)
            {
                progress = new LearningProgress
                {
                    UserId = currentUserId,
                    CardId = request.CardId,
                    Status = "Learning",
                    WrongCount = 0,
                    RepetitionCount = 0
                };
                _context.LearningProgresses.Add(progress);
            }

            // Cập nhật ngày review cuối
            progress.LastReviewedDate = DateTime.Now;

            // Logic SRS – dùng RepetitionCount để tính khoảng cách ôn tập
            int reps = progress.RepetitionCount;
            switch (request.MasteryLevel.ToLower())
            {
                case "hard":
                    // Khó → ôn lại sớm, reset repetition
                    progress.Status = "Learning";
                    progress.NextReviewDate = DateTime.Now.AddDays(1);
                    progress.WrongCount = (progress.WrongCount ?? 0) + 1;
                    progress.RepetitionCount = 0;
                    break;
                case "good":
                    // Tốt → khoảng cách tăng dần: 1 → 3 → 7 ngày
                    progress.Status = "Reviewing";
                    int goodDays = reps == 0 ? 1 : (reps == 1 ? 3 : 7);
                    progress.NextReviewDate = DateTime.Now.AddDays(goodDays);
                    progress.RepetitionCount = reps + 1;
                    break;
                case "easy":
                case "perfect":
                    // Dễ → khoảng cách lớn hơn: 3 → 7 → 14 → 30 ngày
                    progress.Status = "Mastered";
                    int easyDays = reps == 0 ? 3 : (reps == 1 ? 7 : (reps <= 3 ? 14 : 30));
                    progress.NextReviewDate = DateTime.Now.AddDays(easyDays);
                    progress.RepetitionCount = reps + 1;
                    break;
                default:
                    return BadRequest("Invalid mastery level");
            }

            // ============ STREAK UPDATE ============
            var user = await _context.Users.FindAsync(currentUserId);
            if (user != null)
            {
                var today = DateTime.Now.Date;
                var lastStudy = user.LastStudyDate?.Date;

                if (lastStudy == null || lastStudy < today)
                {
                    if (lastStudy == today.AddDays(-1))
                    {
                        // Hôm qua đã học → tăng streak
                        user.Streak = (user.Streak ?? 0) + 1;
                    }
                    else if (lastStudy < today.AddDays(-1))
                    {
                        // Bỏ lỡ → reset streak về 1
                        user.Streak = 1;
                    }

                    user.LastStudyDate = today;

                    // Cập nhật kỷ lục
                    if ((user.Streak ?? 0) > (user.LongestStreak ?? 0))
                    {
                        user.LongestStreak = user.Streak;
                    }
                }
                // Nếu lastStudy == today → không thay đổi streak (đã tính rồi)
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, nextReviewDate = progress.NextReviewDate, streak = user?.Streak ?? 0 });
        }
    }

    public class LearningRecordRequest
    {
        public int CardId { get; set; }
        public string MasteryLevel { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}
