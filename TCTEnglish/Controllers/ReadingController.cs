using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TCTEnglish.Models;
using TCTEnglish.Services.Reading;
using TCTEnglish.ViewModels;
using TCTVocabulary.Controllers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Controllers
{
    [Authorize]
    public class ReadingController : BaseController
    {
        private readonly IReadingLearningService _learning;
        private readonly IReadingTranslationService _translationService;
        private readonly IReadingAssistService _assist;
        private readonly TCTEnglish.Services.AI.IAiRequestRateLimiter? _rateLimiter;

        public ReadingController(
            IReadingLearningService learning,
            IReadingTranslationService translationService,
            IReadingAssistService assist,
            TCTEnglish.Services.AI.IAiRequestRateLimiter? rateLimiter = null)
        {
            _learning = learning;
            _translationService = translationService;
            _assist = assist;
            _rateLimiter = rateLimiter;
        }

        [HttpGet("Reading/Progress")]
        public async Task<IActionResult> Progress()
        {
            var userId = GetCurrentUserId();
            var model = await _learning.GetProgressAsync(userId);
            return View("~/Views/Reading/Progress.cshtml", model);
        }

        [HttpGet("Reading/Notebook")]
        public async Task<IActionResult> Notebook(string? q)
        {
            var userId = GetCurrentUserId();
            var items = await _learning.GetNotebookAsync(userId, q);
            ViewBag.Search = q;
            return View("~/Views/Reading/Notebook.cshtml", items);
        }

        [HttpGet("Reading/Study/{id}")]
        public async Task<IActionResult> Study(int id, string mode = "Practice")
        {
            var userId = GetCurrentUserId();
            var model = await _learning.GetStudyAsync(userId, id);
            if (model == null) return NotFound();

            if (model.AttemptId == null)
            {
                var attempt = await _learning.StartAttemptAsync(userId, id, mode, model.EstimatedTimeMinutes.HasValue ? model.EstimatedTimeMinutes * 60 : null);
                model.AttemptId = attempt.Id;
                model.Mode = attempt.Mode;
                model.TimeLimitSeconds = attempt.TimeLimitSeconds;
            }

            return View("~/Views/Study/ReadingStudy.cshtml", model);
        }

        [HttpPost("Reading/StartAttempt")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartAttempt(int passageId, string mode = "Practice")
        {
            var userId = GetCurrentUserId();
            try
            {
                var attempt = await _learning.StartAttemptAsync(userId, passageId, mode, null);
                return Json(new { success = true, attemptId = attempt.Id, mode = attempt.Mode, timeLimitSeconds = attempt.TimeLimitSeconds });
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpPost("Reading/SaveAnswer")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SaveAnswer([FromBody] SaveReadingAnswerApiRequest request) => ReadingResultAsync(async () =>
        {
            if (request == null) return BadRequest();
            var userId = GetCurrentUserId();
            await _learning.SaveAnswerAsync(userId, request.AttemptId, new SaveReadingAnswerRequest
            {
                QuestionId = request.QuestionId,
                SelectedOptionId = request.SelectedOptionId,
                AnswerText = request.AnswerText,
                MatchingJson = request.MatchingJson,
                IsFlagged = request.IsFlagged
            });
            return Json(new { success = true });
        });

        [HttpPost("Reading/SubmitAttempt")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAttempt(int attemptId)
        {
            var userId = GetCurrentUserId();
            try
            {
                var result = await _learning.SubmitAttemptAsync(userId, attemptId);
                return Json(new { success = true, result });
            }
            catch (ReadingNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException)
            {
                return BadRequest(new { success = false, message = "Chưa thể lưu kết quả. Bạn hãy thử lại." });
            }
        }

        // Legacy submit endpoint kept for older forms — maps to attempt submit after creating/finding open attempt.
        [HttpPost("Reading/SubmitReading")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SubmitReading(int passageId, Dictionary<int, int> answers) => ReadingResultAsync(async () =>
        {
            var userId = GetCurrentUserId();
            var attempt = await _learning.StartAttemptAsync(userId, passageId, "Practice", null);
            foreach (var pair in answers)
            {
                await _learning.SaveAnswerAsync(userId, attempt.Id, new SaveReadingAnswerRequest
                {
                    QuestionId = pair.Key,
                    SelectedOptionId = pair.Value
                });
            }

            var result = await _learning.SubmitAttemptAsync(userId, attempt.Id);
            return Json(new
            {
                success = true,
                correctCount = result.CorrectCount,
                totalCount = result.TotalCount,
                details = result.Details.Select(d => new
                {
                    questionId = d.QuestionId,
                    isCorrect = d.IsCorrect,
                    correctOptionText = d.CorrectOptionText,
                    explanation = d.Explanation,
                    evidenceText = d.EvidenceText,
                    paraphrase = d.Paraphrase
                })
            });
        });

        [HttpPost("Reading/Annotations")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SaveAnnotation([FromBody] ReadingAnnotationRequest request) => ReadingResultAsync(async () =>
        {
            var userId = GetCurrentUserId();
            await _learning.SaveAnnotationAsync(userId, request);
            return Json(new { success = true });
        });

        [HttpGet("Reading/Annotations/{passageId}")]
        public Task<IActionResult> Annotations(int passageId) => ReadingResultAsync(async () =>
        {
            var userId = GetCurrentUserId();
            var items = await _learning.GetAnnotationsAsync(userId, passageId);
            return Json(new { items = items.Select(a => new { a.Id, a.AnnotationType, a.StartOffset, a.EndOffset, a.SelectedText, a.NoteText, a.Color }) });
        });

        [HttpPost("Reading/Annotations/Delete")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> DeleteAnnotation(int id) => ReadingResultAsync(async () =>
        {
            await _learning.DeleteAnnotationAsync(GetCurrentUserId(), id);
            return Json(new { success = true });
        });

        [HttpGet("Reading/Lookup")]
        public Task<IActionResult> Lookup(int passageId, string word) => ReadingResultAsync(async () =>
        {
            var item = await _learning.LookupWordAsync(GetCurrentUserId(), passageId, word);
            if (item == null) return Json(new { found = false });
            return Json(new
            {
                found = true,
                id = item.Id,
                word = item.Word,
                ipa = item.Ipa,
                partOfSpeech = item.PartOfSpeech,
                englishDefinition = item.EnglishDefinition,
                vietnameseMeaning = item.VietnameseMeaning,
                example = item.Example
            });
        });

        [HttpPost("Reading/Assist")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assist([FromBody] ReadingAssistRequest request, CancellationToken ct)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.SelectedText))
                return BadRequest(new { success = false, errorCode = "invalid_selection", errorMessage = "Chọn 1–8 từ trong đoạn văn." });

            if (_rateLimiter != null && !_rateLimiter.TryConsume(GetCurrentUserId(), HttpContext.Connection.RemoteIpAddress?.ToString(), out var retryAfter))
            {
                Response.Headers["Retry-After"] = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, errorCode = "rate_limited", errorMessage = "Bạn tra từ hơi nhanh. Hãy chờ một chút rồi thử lại." });
            }

            var result = await _assist.AssistAsync(GetCurrentUserId(), request, ct);
            if (!result.Success && string.Equals(result.ErrorCode, "not_found", StringComparison.Ordinal))
                return NotFound(new { success = false, errorCode = result.ErrorCode, errorMessage = result.ErrorMessage });

            return Json(result);
        }

        [HttpPost("Reading/Notebook/Save")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SaveNotebook([FromBody] SaveNotebookApiRequest request) => ReadingResultAsync(async () =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Word))
                return BadRequest(new { success = false, message = "Thiếu từ cần lưu." });

            var fields = new ReadingNotebookFields
            {
                Ipa = request.Ipa,
                PartOfSpeech = request.PartOfSpeech,
                EnglishDefinition = request.EnglishDefinition,
                VietnameseMeaning = request.VietnameseMeaning,
                Example = request.Example
            };

            var entry = await _learning.SaveToNotebookAsync(
                GetCurrentUserId(),
                request.PassageId,
                request.VocabItemId,
                request.Word,
                fields);
            return Json(new { success = true, id = entry.Id });
        });

        [HttpPost("Reading/Notebook/Delete")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> DeleteNotebook(int id) => ReadingResultAsync(async () =>
        {
            await _learning.DeleteNotebookEntryAsync(GetCurrentUserId(), id);
            return RedirectToAction(nameof(Notebook));
        });

        [HttpGet("Reading/Notebook/Export")]
        public async Task<IActionResult> ExportNotebook()
        {
            var items = await _learning.GetNotebookAsync(GetCurrentUserId(), null);
            var lines = new List<string> { "Word,IPA,PartOfSpeech,EnglishDefinition,VietnameseMeaning,Example" };
            foreach (var item in items)
            {
                lines.Add(string.Join(',',
                    Csv(item.Word),
                    Csv(item.Ipa),
                    Csv(item.PartOfSpeech),
                    Csv(item.EnglishDefinition),
                    Csv(item.VietnameseMeaning),
                    Csv(item.Example)));
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', lines));
            return File(bytes, "text/csv", "reading-vocabulary-notebook.csv");
        }

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
                var translated = doc.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString();
                return Json(new { translation = translated });
            }
            catch
            {
                return Json(new { translation = "" });
            }
        }

        [HttpGet("Reading/MyTranslation/{passageId}")]
        public async Task<IActionResult> MyTranslation(int passageId)
        {
            var userId = GetCurrentUserId();
            var translation = await _translationService.GetMyTranslationAsync(userId, passageId);
            if (translation == null)
                return Json(new { exists = false });

            return Json(new
            {
                exists = true,
                id = translation.Id,
                translatedTitle = translation.TranslatedTitle,
                translatedContent = translation.TranslatedContent,
                aiScore = translation.AiScore,
                aiFeedback = translation.AiFeedback,
                isAiApproved = translation.IsAiApproved,
                isPublic = translation.IsPublic
            });
        }

        [AllowAnonymous]
        [HttpGet("Reading/Translations/{passageId}")]
        public async Task<IActionResult> Translations(int passageId)
        {
            var translations = await _translationService.GetPublicTranslationsAsync(passageId);
            return Json(new { items = translations });
        }

        [HttpPost("Reading/SubmitTranslation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitTranslation([FromBody] SubmitTranslationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TranslatedContent))
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            var userId = GetCurrentUserId();
            var result = await _translationService.SubmitTranslationAsync(
                userId, request.PassageId, request.TranslatedTitle, request.TranslatedContent);
            return Json(result);
        }

        [HttpPost("Reading/PublishTranslation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishTranslation([FromBody] PublishTranslationRequest request)
        {
            var result = await _translationService.PublishTranslationAsync(GetCurrentUserId(), request.TranslationId);
            return Json(result);
        }

        [HttpPost("Reading/DeleteTranslation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTranslation([FromBody] DeleteTranslationRequest request)
        {
            var result = await _translationService.DeleteTranslationAsync(GetCurrentUserId(), request.TranslationId);
            return Json(result);
        }

        [HttpPost("Reading/VoteTranslation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoteTranslation([FromBody] VoteTranslationRequest request)
        {
            var result = await _translationService.VoteTranslationAsync(
                GetCurrentUserId(), request.TranslationId, request.VoteType);
            return Json(result);
        }

        [AllowAnonymous]
        [HttpGet("Reading/CommunityTranslation/{id}")]
        public async Task<IActionResult> CommunityTranslation(int id)
        {
            TryGetCurrentUserId(out var userId);
            var detail = await _translationService.GetTranslationDetailAsync(id, userId > 0 ? userId : null);
            if (detail == null) return NotFound();
            return View("~/Views/Reading/CommunityTranslation.cshtml", detail);
        }

        private static string Csv(string? value)
        {
            var v = value ?? "";
            if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n') v = "'" + v;
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }

        private async Task<IActionResult> ReadingResultAsync(Func<Task<IActionResult>> action)
        {
            try { return await action(); }
            catch (ReadingNotFoundException) { return NotFound(); }
            catch (ArgumentException) { return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." }); }
            catch (InvalidOperationException) { return BadRequest(new { success = false, message = "Chưa thể lưu. Bạn hãy tải lại và thử lại." }); }
        }
    }

    public class SaveReadingAnswerApiRequest
    {
        public int AttemptId { get; set; }
        public int QuestionId { get; set; }
        public int? SelectedOptionId { get; set; }
        public string? AnswerText { get; set; }
        public string? MatchingJson { get; set; }
        public bool IsFlagged { get; set; }
    }

    public class SubmitTranslationRequest
    {
        public int PassageId { get; set; }
        public string? TranslatedTitle { get; set; }
        public string TranslatedContent { get; set; } = null!;
    }

    public class PublishTranslationRequest
    {
        public int TranslationId { get; set; }
    }

    public class DeleteTranslationRequest
    {
        public int TranslationId { get; set; }
    }

    public class VoteTranslationRequest
    {
        public int TranslationId { get; set; }
        public TranslationVoteType VoteType { get; set; }
    }
}
