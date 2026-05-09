using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Controllers
{
    [Authorize]
    public class ReadingController : Controller
    {
        private readonly DbflashcardContext _context;
        private readonly IGoalsService _goalsService;
        private readonly IAiProviderClient _aiClient;

        public ReadingController(
            DbflashcardContext context,
            IGoalsService goalsService,
            IAiProviderClient aiClient)
        {
            _context = context;
            _goalsService = goalsService;
            _aiClient = aiClient;
        }

        // ─── Trang học chi tiết ────────────────────────────────────────────────
        [HttpGet("Reading/Study/{id}")]
        public async Task<IActionResult> Study(int id)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login", "Account");

            var passage = await _context.ReadingPassages
                .Include(p => p.Questions).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (passage == null) return NotFound();

            // Ghi nhận lịch sử xem bài
            var history = await _context.UserReadingHistories
                .FirstOrDefaultAsync(x => x.UserId == userId && x.ReadingPassageId == id);
            if (history == null)
            {
                history = new UserReadingHistory
                {
                    UserId = userId,
                    ReadingPassageId = id,
                    ViewedAt = DateTime.Now,
                    IsCompleted = false
                };
                _context.UserReadingHistories.Add(history);
                await _context.SaveChangesAsync();
            }

            // Load bản dịch nổi bật (public, sắp xếp theo like desc)
            var featuredTranslations = await _context.ReadingUserTranslations
                .Where(t => t.ReadingPassageId == id && t.IsPublic)
                .OrderByDescending(t => t.LikeCount)
                .ThenByDescending(t => t.CreatedAtUtc)
                .Take(10)
                .Select(t => new ReadingTranslationCardViewModel
                {
                    Id = t.Id,
                    AuthorName = t.User.FullName ?? t.User.Email,
                    AuthorAvatar = t.User.AvatarUrl,
                    LikeCount = t.LikeCount,
                    DislikeCount = t.DislikeCount,
                    CreatedAtUtc = t.CreatedAtUtc,
                    CurrentUserVote = t.Votes
                        .Where(v => v.UserId == userId)
                        .Select(v => (int)v.VoteType)
                        .FirstOrDefault()
                })
                .AsNoTracking()
                .ToListAsync();

            // Bản dịch hiện tại của user
            var myTranslationId = await _context.ReadingUserTranslations
                .Where(t => t.ReadingPassageId == id && t.UserId == userId)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync();

            var model = new ReadingStudyViewModel
            {
                Id = passage.Id,
                Title = passage.Title,
                Content = passage.Content,
                ImageUrl = passage.ImageUrl,
                Questions = passage.Questions.OrderBy(q => q.OrderIndex).Select(q => new QuestionViewModel
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Options = q.Options.Select(o => new OptionViewModel { Id = o.Id, OptionText = o.OptionText }).ToList()
                }).ToList(),
                FeaturedTranslations = featuredTranslations,
                CurrentUserTranslationId = myTranslationId
            };

            return View("~/Views/Study/ReadingStudy.cshtml", model);
        }

        // ─── Nộp bài quiz ─────────────────────────────────────────────────────
        [HttpPost("Reading/SubmitReading")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReading(int passageId, Dictionary<int, int> answers)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var questions = await _context.ReadingQuestions
                .Include(q => q.Options)
                .Where(q => q.PassageId == passageId).ToListAsync();

            int correctCount = 0;
            var details = new List<object>();

            foreach (var q in questions)
            {
                answers.TryGetValue(q.Id, out int selectedId);
                var correctOption = q.Options.FirstOrDefault(o => o.IsCorrect);
                bool isCorrect = (correctOption != null && correctOption.Id == selectedId);
                if (isCorrect) correctCount++;

                details.Add(new
                {
                    questionId = q.Id,
                    isCorrect,
                    correctOptionText = correctOption?.OptionText
                });
            }

            var history = await _context.UserReadingHistories
                .FirstOrDefaultAsync(h => h.UserId == userId && h.ReadingPassageId == passageId);
            if (history != null)
            {
                var isNewCompletion = !history.IsCompleted;
                history.IsCompleted = true;
                history.Score = correctCount;
                history.ViewedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                if (isNewCompletion)
                {
                    var activityUpdate = _goalsService.BuildReadingCompletionActivityUpdate();
                    await _goalsService.RecordLearningActivityAsync(userId, activityUpdate);
                }
            }

            return Json(new { success = true, correctCount, totalCount = questions.Count, details });
        }

        // ─── Dịch toàn bài (MyMemory) ─────────────────────────────────────────
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Translate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Json(new { translation = "" });
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair=en|vi";
                var response = await client.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(response);
                var translated = doc.RootElement
                    .GetProperty("responseData")
                    .GetProperty("translatedText")
                    .GetString();
                return Json(new { translation = translated });
            }
            catch
            {
                return Json(new { translation = "" });
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // TRANSLATION ENDPOINTS
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>Lưu (tạo mới hoặc cập nhật) bản dịch của user</summary>
        [HttpPost("Reading/Translation/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTranslation([FromBody] SubmitTranslationRequest req)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.TranslatedContent))
                return BadRequest(new { error = "Nội dung bản dịch không được để trống." });

            var passage = await _context.ReadingPassages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == req.PassageId);
            if (passage == null) return NotFound();

            var existing = await _context.ReadingUserTranslations
                .FirstOrDefaultAsync(t => t.ReadingPassageId == req.PassageId && t.UserId == userId);

            if (existing == null)
            {
                existing = new ReadingUserTranslation
                {
                    ReadingPassageId = req.PassageId,
                    UserId = userId,
                    TranslatedTitle = req.TranslatedTitle?.Trim(),
                    TranslatedContent = req.TranslatedContent.Trim(),
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _context.ReadingUserTranslations.Add(existing);
            }
            else
            {
                existing.TranslatedTitle = req.TranslatedTitle?.Trim();
                existing.TranslatedContent = req.TranslatedContent.Trim();
                existing.UpdatedAtUtc = DateTime.UtcNow;
                // Reset AI khi bản dịch thay đổi
                existing.AiScore = null;
                existing.AiFeedback = null;
                existing.IsAiApproved = null;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, translationId = existing.Id });
        }

        /// <summary>Gọi AI đánh giá bản dịch</summary>
        [HttpPost("Reading/Translation/Evaluate/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EvaluateTranslation(int id, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var translation = await _context.ReadingUserTranslations
                .Include(t => t.ReadingPassage)
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (translation == null) return NotFound();

            var prompt =
                "Bạn là giáo viên tiếng Anh. Hãy đánh giá bản dịch từ tiếng Anh sang tiếng Việt dưới đây.\n\n" +
                "=== BÀI GỐC (tiếng Anh) ===\n" +
                $"Tiêu đề: {translation.ReadingPassage.Title}\n" +
                $"Nội dung:\n{translation.ReadingPassage.Content}\n\n" +
                "=== BẢN DỊCH CỦA HỌC VIÊN ===\n" +
                $"Tiêu đề dịch: {translation.TranslatedTitle ?? "(không có)"}\n" +
                $"Nội dung dịch:\n{translation.TranslatedContent}\n\n" +
                "=== YÊU CẦU ===\n" +
                "Hãy trả về JSON với định dạng chính xác sau (không thêm markdown):\n" +
                "{\"isApproved\": true/false, \"score\": <0-100>, \"feedback\": \"<nhận xét tiếng Việt, tối đa 200 từ>\"}\n\n" +
                "Tiêu chí: isApproved = true nếu score >= 60. Đánh giá: độ chính xác nghĩa, tính tự nhiên, ngữ pháp.";

            try
            {
                var messages = new List<AiContextMessage>
                {
                    new("user", prompt)
                };

                var reply = await _aiClient.GenerateReplyAsync(userId, messages, ct);
                var json = (reply.Text ?? "{}").Trim();

                int startIndex = json.IndexOf("{");
                int endIndex = json.LastIndexOf("}");
                if (startIndex >= 0 && endIndex >= startIndex)
                {
                    json = json.Substring(startIndex, endIndex - startIndex + 1);
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool isApproved = root.TryGetProperty("isApproved", out var ap) && ap.GetBoolean();
                int score = root.TryGetProperty("score", out var sc) ? sc.GetInt32() : 0;
                string feedback = root.TryGetProperty("feedback", out var fb) ? fb.GetString() ?? "" : "";

                translation.AiScore = score;
                translation.AiFeedback = feedback;
                translation.IsAiApproved = isApproved;
                translation.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, isApproved, score, feedback });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EvaluateTranslation ERROR]: {ex}");
                return StatusCode(500, new { error = "Không thể kết nối AI. Vui lòng thử lại.", detail = ex.ToString() });
            }
        }

        /// <summary>Public bản dịch cho cộng đồng</summary>
        [HttpPost("Reading/Translation/Publish/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishTranslation(int id)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var translation = await _context.ReadingUserTranslations
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (translation == null) return NotFound();
            if (translation.IsAiApproved != true)
                return BadRequest(new { error = "Bản dịch chưa được AI chấp thuận." });

            translation.IsPublic = true;
            translation.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        /// <summary>Xóa bản dịch của tôi</summary>
        [HttpPost("Reading/Translation/Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTranslation(int id)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var translation = await _context.ReadingUserTranslations
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (translation == null) return NotFound();

            // Xóa các vote liên quan trước để tránh lỗi khóa ngoại
            var votes = await _context.ReadingTranslationVotes
                .Where(v => v.TranslationId == id)
                .ToListAsync();
            _context.ReadingTranslationVotes.RemoveRange(votes);

            _context.ReadingUserTranslations.Remove(translation);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        /// <summary>Lấy chi tiết bản dịch (JSON)</summary>
        [HttpGet("Reading/Translation/{id:int}/Json")]
        public async Task<IActionResult> GetTranslationDetailJson(int id)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var translation = await _context.ReadingUserTranslations
                .Where(t => t.Id == id && (t.IsPublic || t.UserId == userId))
                .Select(t => new ReadingTranslationDetailViewModel
                {
                    Id = t.Id,
                    AuthorName = t.User.FullName ?? t.User.Email,
                    AuthorAvatar = t.User.AvatarUrl,
                    TranslatedTitle = t.TranslatedTitle,
                    TranslatedContent = t.TranslatedContent,
                    LikeCount = t.LikeCount,
                    DislikeCount = t.DislikeCount,
                    AiScore = t.AiScore,
                    AiFeedback = t.AiFeedback,
                    IsAiApproved = t.IsAiApproved,
                    IsPublic = t.IsPublic,
                    CreatedAtUtc = t.CreatedAtUtc,
                    IsOwner = t.UserId == userId,
                    CurrentUserVote = t.Votes
                        .Where(v => v.UserId == userId)
                        .Select(v => (int)v.VoteType)
                        .FirstOrDefault()
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (translation == null) return NotFound();
            return Ok(translation);
        }

        /// <summary>Xem chi tiết bản dịch trên trang riêng (Mazii style)</summary>
        [HttpGet("Reading/Translation/View/{id:int}")]
        public async Task<IActionResult> ViewTranslation(int id)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login", "Account");

            var translation = await _context.ReadingUserTranslations
                .Include(t => t.ReadingPassage)
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == id && (t.IsPublic || t.UserId == userId));

            if (translation == null) return NotFound();

            var model = new ReadingTranslationDetailViewModel
            {
                Id = translation.Id,
                AuthorName = translation.User.FullName ?? translation.User.Email,
                AuthorAvatar = translation.User.AvatarUrl,
                TranslatedTitle = translation.TranslatedTitle,
                TranslatedContent = translation.TranslatedContent,
                LikeCount = translation.LikeCount,
                DislikeCount = translation.DislikeCount,
                AiScore = translation.AiScore,
                AiFeedback = translation.AiFeedback,
                IsAiApproved = translation.IsAiApproved,
                IsPublic = translation.IsPublic,
                CreatedAtUtc = translation.CreatedAtUtc,
                IsOwner = translation.UserId == userId,
                CurrentUserVote = _context.ReadingTranslationVotes
                    .Where(v => v.TranslationId == id && v.UserId == userId)
                    .Select(v => (int)v.VoteType)
                    .FirstOrDefault()
            };

            ViewBag.OriginalPassage = translation.ReadingPassage;
            ViewBag.FeaturedTranslations = await _context.ReadingUserTranslations
                .Where(t => t.ReadingPassageId == translation.ReadingPassageId && t.IsPublic && t.Id != translation.Id)
                .OrderByDescending(t => t.LikeCount)
                .Take(5)
                .Select(t => new ReadingTranslationCardViewModel
                {
                    Id = t.Id,
                    AuthorName = t.User.FullName ?? t.User.Email,
                    AuthorAvatar = t.User.AvatarUrl,
                    LikeCount = t.LikeCount,
                    DislikeCount = t.DislikeCount,
                    CreatedAtUtc = t.CreatedAtUtc
                })
                .ToListAsync();
                
            return View("~/Views/Study/ViewTranslation.cshtml", model);
        }


        /// <summary>Like / Dislike bản dịch (toggle)</summary>
        [HttpPost("Reading/Translation/Vote")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoteTranslation([FromBody] VoteTranslationRequest req)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var voteTypeEnum = req.VoteType?.ToLower() == "like"
                ? TranslationVoteType.Like
                : TranslationVoteType.Dislike;

            var translation = await _context.ReadingUserTranslations
                .FirstOrDefaultAsync(t => t.Id == req.TranslationId && t.IsPublic);
            if (translation == null) return NotFound();

            if (translation.UserId == userId)
                return BadRequest(new { error = "Bạn không thể vote bản dịch của chính mình." });

            var existingVote = await _context.ReadingTranslationVotes
                .FirstOrDefaultAsync(v => v.TranslationId == req.TranslationId && v.UserId == userId);

            if (existingVote == null)
            {
                _context.ReadingTranslationVotes.Add(new ReadingTranslationVote
                {
                    TranslationId = req.TranslationId,
                    UserId = userId,
                    VoteType = voteTypeEnum,
                    CreatedAtUtc = DateTime.UtcNow
                });
                if (voteTypeEnum == TranslationVoteType.Like) translation.LikeCount++;
                else translation.DislikeCount++;
            }
            else if (existingVote.VoteType == voteTypeEnum)
            {
                // Toggle off (hủy vote)
                _context.ReadingTranslationVotes.Remove(existingVote);
                if (voteTypeEnum == TranslationVoteType.Like)
                    translation.LikeCount = Math.Max(0, translation.LikeCount - 1);
                else
                    translation.DislikeCount = Math.Max(0, translation.DislikeCount - 1);
                voteTypeEnum = 0;
            }
            else
            {
                // Đổi loại vote
                if (existingVote.VoteType == TranslationVoteType.Like)
                {
                    translation.LikeCount = Math.Max(0, translation.LikeCount - 1);
                    translation.DislikeCount++;
                }
                else
                {
                    translation.DislikeCount = Math.Max(0, translation.DislikeCount - 1);
                    translation.LikeCount++;
                }
                existingVote.VoteType = voteTypeEnum;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                likeCount = translation.LikeCount,
                dislikeCount = translation.DislikeCount,
                currentVote = (int)voteTypeEnum
            });
        }

        // ─── Helper ───────────────────────────────────────────────────────────
        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out userId);
        }
    }
}