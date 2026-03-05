using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTVocabulary.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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
            int currentUserId = 1; // Tạm thời gán fix cứng

            var progress = await _context.LearningProgresses
                .FirstOrDefaultAsync(lp => lp.CardId == request.CardId && lp.UserId == currentUserId);

            if (progress == null)
            {
                progress = new LearningProgress
                {
                    UserId = currentUserId,
                    CardId = request.CardId,
                    WrongCount = 0
                };
                _context.LearningProgresses.Add(progress);
            }

            // Cập nhật ngày review cuối
            progress.LastReviewedDate = DateTime.Now;

            // Logic SRS cơ bản
            switch (request.MasteryLevel.ToLower())
            {
                case "hard":
                    progress.Status = "Reviewing";
                    progress.NextReviewDate = DateTime.Now.AddDays(1);
                    progress.WrongCount = (progress.WrongCount ?? 0) + 1;
                    break;
                case "good":
                    progress.Status = "Reviewing";
                    progress.NextReviewDate = DateTime.Now.AddDays(3);
                    break;
                case "easy":
                case "perfect":
                    progress.Status = "Mastered";
                    progress.NextReviewDate = DateTime.Now.AddDays(7);
                    break;
                default:
                    return BadRequest("Invalid mastery level");
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, nextReviewDate = progress.NextReviewDate });
        }
    }

    public class LearningRecordRequest
    {
        public int CardId { get; set; }
        public string MasteryLevel { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}
