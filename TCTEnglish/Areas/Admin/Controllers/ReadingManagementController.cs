using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Areas.Admin.ViewModels;
using TCTEnglish.Models.Reading;
using TCTEnglish.Services.Reading;
using TCTVocabulary.Controllers;
using TCTVocabulary.Models;

namespace TCTEnglish.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class ReadingManagementController : BaseController
    {
        private readonly IReadingAdminService _admin;
        private readonly IReadingExcelService _excel;

        public ReadingManagementController(IReadingAdminService admin, IReadingExcelService excel)
        {
            _admin = admin;
            _excel = excel;
        }

        public async Task<IActionResult> Index(string? searchString, string? status)
        {
            ViewData["Title"] = "Quản lý bài đọc";
            var items = await _admin.ListAsync(searchString, status);
            ViewBag.SearchString = searchString;
            ViewBag.Status = status;
            ViewBag.Statuses = ReadingStatuses.All;
            return View(items);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewData["Title"] = "Tạo bài đọc";
            return View(new ReadingEditorViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> Create(ReadingEditorViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                var actorId = GetCurrentUserId();
                var created = await _admin.CreateDraftAsync(model, actorId);
                TempData["Success"] = "Đã lưu bản nháp.";
                return RedirectToAction(nameof(Edit), new { id = created.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var passage = await _admin.GetForEditAsync(id);
            if (passage == null) return NotFound();
            ViewData["Title"] = "Chỉnh sửa bài đọc";
            return View(MapEditor(passage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> Edit(int id, ReadingEditorViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                await _admin.UpdateDraftAsync(id, model, GetCurrentUserId());
                TempData["Success"] = "Đã cập nhật bản nháp.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForReview(int id)
        {
            try
            {
                await _admin.SubmitForReviewAsync(id, GetCurrentUserId());
                TempData["Success"] = "Đã gửi kiểm duyệt.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                await _admin.ApproveAsync(id, GetCurrentUserId());
                TempData["Success"] = "Đã duyệt bài.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRevision(ReadingReviewActionViewModel model)
        {
            try
            {
                await _admin.RequestRevisionAsync(model.PassageId, GetCurrentUserId(), model.Reason ?? "");
                TempData["Success"] = "Đã yêu cầu chỉnh sửa.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Edit), new { id = model.PassageId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(ReadingReviewActionViewModel model)
        {
            try
            {
                await _admin.RejectAsync(model.PassageId, GetCurrentUserId(), model.Reason ?? "");
                TempData["Success"] = "Đã từ chối bài.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Edit), new { id = model.PassageId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(int id)
        {
            try
            {
                await _admin.PublishAsync(id, GetCurrentUserId());
                TempData["Success"] = "Đã xuất bản.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            try { await _admin.ArchiveAsync(id, GetCurrentUserId(), HttpContext.RequestAborted); }
            catch (InvalidOperationException) { return NotFound(); }
            TempData["Success"] = "Đã lưu trữ bài đọc và giữ nguyên lịch sử học.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _admin.DeleteAsync(id);
                TempData["Success"] = "Đã xóa bài đọc.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var bytes = _excel.BuildTemplate();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Reading_Template_v2.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> Export(int id, CancellationToken ct)
        {
            try
            {
                var bytes = await _excel.ExportReadingAsync(id, ct);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Reading_{id}.xlsx");
            }
            catch (ReadingNotFoundException) { return NotFound(); }
        }

        [HttpGet]
        public IActionResult Import()
        {
            ViewData["Title"] = "Import bài đọc Excel";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(40 * 1024 * 1024)]
        public async Task<IActionResult> ImportPreview(IFormFile file, IFormFile? mediaZip)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Chọn file Excel.";
                return RedirectToAction(nameof(Import));
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Chỉ chấp nhận file .xlsx.";
                return RedirectToAction(nameof(Import));
            }

            const long maxBytes = 10 * 1024 * 1024;
            if (file.Length > maxBytes)
            {
                TempData["Error"] = "File Excel vượt quá 10MB.";
                return RedirectToAction(nameof(Import));
            }

            await using var stream = file.OpenReadStream();
            var result = await _excel.ParseAndValidateAsync(stream, HttpContext.RequestAborted);

            if (result.HasImportableReadings)
            {
                await ReadingImportMediaZip.ApplyAsync(
                    mediaZip,
                    result.ValidReadings,
                    _admin,
                    result.Warnings);
                ViewBag.ImportPreviewJson = JsonSerializer.Serialize(result.ValidReadings);
            }
            else if (mediaZip != null && mediaZip.Length > 0)
            {
                result.Warnings.Add(new ReadingExcelWarning
                {
                    Sheet = "MediaZip",
                    Message = "Có ZIP ảnh nhưng không có bài Excel hợp lệ — đã bỏ qua ZIP."
                });
            }

            if (result.Errors.Count > 0)
            {
                TempData["ImportErrorReport"] = Convert.ToBase64String(_excel.BuildErrorReport(result.Errors));
                ViewBag.HasErrorReport = true;
            }

            return View("ImportPreview", result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportConfirm(string previewJson)
        {
            if (string.IsNullOrWhiteSpace(previewJson))
            {
                TempData["Error"] = "Không có dữ liệu preview hợp lệ.";
                return RedirectToAction(nameof(Import));
            }

            var models = JsonSerializer.Deserialize<List<ReadingEditorViewModel>>(previewJson);
            if (models == null || models.Count == 0)
            {
                TempData["Error"] = "Preview không hợp lệ.";
                return RedirectToAction(nameof(Import));
            }

            var confirm = await _excel.ConfirmImportAsync(models, GetCurrentUserId());
            TempData["ImportResultJson"] = JsonSerializer.Serialize(confirm);
            TempData["Success"] =
                $"Import xong: {confirm.SuccessCount} thành công (Draft), {confirm.FailedCount} lỗi, {confirm.SkippedCount} bỏ qua. Chưa xuất bản — cần kiểm duyệt.";
            return RedirectToAction(nameof(ImportResult));
        }

        [HttpGet]
        public IActionResult ImportResult()
        {
            ViewData["Title"] = "Kết quả Import";
            var json = TempData["ImportResultJson"] as string;
            if (string.IsNullOrWhiteSpace(json))
            {
                TempData["Error"] = "Không có báo cáo Import.";
                return RedirectToAction(nameof(Import));
            }

            var result = JsonSerializer.Deserialize<ReadingExcelImportConfirmResult>(json)
                         ?? new ReadingExcelImportConfirmResult();
            return View(result);
        }

        [HttpGet]
        public IActionResult DownloadImportErrorReport()
        {
            var b64 = TempData["ImportErrorReport"] as string;
            if (string.IsNullOrWhiteSpace(b64))
            {
                TempData["Error"] = "Không có báo cáo lỗi.";
                return RedirectToAction(nameof(Import));
            }

            var bytes = Convert.FromBase64String(b64);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Reading_Import_Errors.xlsx");
        }

        // Legacy ManageQuestions kept as redirect
        [HttpGet]
        public IActionResult ManageQuestions(int passageId) => RedirectToAction(nameof(Edit), new { id = passageId });

        private static ReadingEditorViewModel MapEditor(ReadingPassage passage) => new()
        {
            Id = passage.Id,
            Title = passage.Title,
            Description = passage.Description,
            Topic = passage.Topic,
            Level = passage.Level,
            EstimatedTimeMinutes = passage.EstimatedTimeMinutes,
            Content = passage.Content,
            ImageUrl = passage.ImageUrl,
            ExternalReadingKey = passage.ExternalReadingKey,
            Status = passage.Status,
            Paragraphs = passage.Paragraphs.OrderBy(p => p.ParagraphOrder).Select(p => new ReadingParagraphEditorViewModel
            {
                Id = p.Id,
                ExternalParagraphKey = p.ExternalParagraphKey,
                ParagraphOrder = p.ParagraphOrder,
                ParagraphContent = p.ParagraphContent
            }).ToList(),
            Questions = passage.Questions.OrderBy(q => q.OrderIndex).Select(q => new ReadingQuestionEditorViewModel
            {
                Id = q.Id,
                ExternalQuestionKey = q.ExternalQuestionKey,
                OrderIndex = q.OrderIndex,
                QuestionType = q.QuestionType,
                QuestionText = q.QuestionText,
                ParagraphReference = q.ParagraphReference,
                Points = q.Points,
                CorrectAnswerText = q.CorrectAnswerText,
                Explanation = q.Explanation,
                EvidenceText = q.EvidenceText,
                EvidenceParagraphId = q.EvidenceParagraphId,
                Paraphrase = q.Paraphrase,
                Options = q.Options.OrderBy(o => o.OptionOrder).Select(o => new ReadingOptionEditorViewModel
                {
                    Id = o.Id,
                    ExternalOptionKey = o.ExternalOptionKey,
                    OptionText = o.OptionText,
                    IsCorrect = o.IsCorrect,
                    MatchKey = o.MatchKey,
                    MatchSide = o.MatchSide,
                    OptionOrder = o.OptionOrder
                }).ToList()
            }).ToList(),
            Vocabulary = passage.VocabularyItems.Select(v => new ReadingVocabularyEditorViewModel
            {
                Id = v.Id,
                ExternalVocabularyKey = v.ExternalVocabularyKey,
                Word = v.Word,
                Ipa = v.Ipa,
                PartOfSpeech = v.PartOfSpeech,
                EnglishDefinition = v.EnglishDefinition,
                VietnameseMeaning = v.VietnameseMeaning,
                Example = v.Example,
                ParagraphId = v.ParagraphId,
                ParagraphOrder = v.Paragraph?.ParagraphOrder
            }).ToList()
        };
    }
}
