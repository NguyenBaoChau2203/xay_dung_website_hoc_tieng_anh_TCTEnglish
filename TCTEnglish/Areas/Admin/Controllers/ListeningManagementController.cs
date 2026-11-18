using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.Admin;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Security;

namespace TCTVocabulary.Areas.Admin.Controllers;

/// <summary>
/// Listening lessons in the admin. Rules live in <see cref="IAdminListeningService"/>: system lessons are
/// edited through one four-tab editor, a learner's own lesson can only be viewed, unpublished and deleted.
/// </summary>
[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class ListeningManagementController : Controller
{
    private readonly IAdminListeningService _service;

    public ListeningManagementController(IAdminListeningService service)
    {
        _service = service;
    }

    // GET: Admin/ListeningManagement
    public async Task<IActionResult> Index(string? q, string? level, string? source, string? status, int page = 1, CancellationToken ct = default)
    {
        var sourceKey = source is "learner" or "all" ? source : "system";
        var statusKey = status is "published" or "draft" or "incomplete" ? status : "";
        var levelKey = AdminInputRules.IsCefrLevel(level) ? level! : "";

        var list = await _service.ListLessonsAsync(new AdminLessonQuery(sourceKey, levelKey, statusKey, q, page), ct);
        var figures = await _service.GetFiguresAsync(ct);

        return View(new ListeningIndexViewModel
        {
            List = list,
            Figures = figures,
            Q = q,
            Level = levelKey,
            Source = sourceKey,
            Status = statusKey,
        });
    }

    // GET: Admin/ListeningManagement/Create  (the editor's Info tab for a new lesson)
    public IActionResult Create()
    {
        return View("Editor", LessonEditorViewModel.From(AdminLessonEditor.NewLesson(), LessonTabs.Info));
    }

    // GET: Admin/ListeningManagement/Edit/5  (old route: the editor's Info tab)
    public IActionResult Edit(int id)
    {
        return RedirectToAction(nameof(Editor), new { id, tab = LessonTabs.Info });
    }

    // GET: Admin/ListeningManagement/Editor/5?tab=info|transcript|quiz|vocab
    public async Task<IActionResult> Editor(int id, string? tab, CancellationToken ct)
    {
        var editor = await _service.GetEditorAsync(id, ct);
        if (editor == null)
        {
            return NotFound();
        }

        return View(LessonEditorViewModel.From(editor, LessonTabs.Normalize(tab)));
    }

    // Old routes: the three management pages are tabs of the editor now.
    [HttpGet]
    public IActionResult ManageTranscript(int id) => RedirectToAction(nameof(Editor), new { id, tab = LessonTabs.Transcript });

    [HttpGet]
    public IActionResult ManageQuiz(int id) => RedirectToAction(nameof(Editor), new { id, tab = LessonTabs.Quiz });

    [HttpGet]
    public IActionResult ManageVocab(int id) => RedirectToAction(nameof(Editor), new { id, tab = LessonTabs.Vocab });

    // POST: Admin/ListeningManagement/CreateEdit  (Info tab; id 0 creates a draft)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEdit(int id, AdminLessonInfoInput input, CancellationToken ct)
    {
        var result = await _service.SaveInfoAsync(id > 0 ? id : null, input, ct);
        if (result.IsSuccess)
        {
            SetSuccess(id > 0 ? "Đã lưu thông tin." : "Đã tạo bài nghe ở dạng bản nháp. Hãy thêm lời thoại, quiz và từ vựng.", result.Warnings);
            return RedirectToAction(nameof(Editor), new { id = result.Value, tab = id > 0 ? LessonTabs.Info : LessonTabs.Transcript });
        }

        if (result.Status == AdminResultStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == AdminResultStatus.Forbidden)
        {
            return ForbiddenRedirect(id, result.Message);
        }

        var model = await BuildEditorAsync(id, LessonTabs.Info, ct);
        if (model == null)
        {
            return NotFound();
        }

        model.Info = input;
        model.Errors = result.FieldErrors;
        model.Summary = result.Message;
        return View("Editor", model);
    }

    // POST: Admin/ListeningManagement/ManageTranscript/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageTranscript(int id, List<TranscriptRowPost>? lines, CancellationToken ct)
    {
        var rows = lines ?? new List<TranscriptRowPost>();
        var result = await _service.SaveTranscriptAsync(id, ToLineInputs(rows), ct);
        return await FinishAsync(result, id, LessonTabs.Transcript, "Đã lưu lời thoại.", ct, model => model.Lines = rows);
    }

    // POST: Admin/ListeningManagement/ManageQuiz/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageQuiz(int id, List<AdminQuizInput>? questions, CancellationToken ct)
    {
        var rows = questions ?? new List<AdminQuizInput>();
        var result = await _service.SaveQuizAsync(id, rows, ct);
        return await FinishAsync(result, id, LessonTabs.Quiz, "Đã lưu quiz.", ct, model => model.Questions = rows);
    }

    // POST: Admin/ListeningManagement/ManageVocab/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageVocab(int id, List<AdminVocabInput>? items, CancellationToken ct)
    {
        var rows = items ?? new List<AdminVocabInput>();
        var result = await _service.SaveVocabAsync(id, rows, ct);
        return await FinishAsync(result, id, LessonTabs.Vocab, "Đã lưu từ vựng.", ct, model => model.Items = rows);
    }

    // POST: Admin/ListeningManagement/ImportTranscript/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportTranscript(int id, bool replaceExisting, CancellationToken ct)
    {
        var result = await _service.ImportTranscriptAsync(id, replaceExisting, ct);
        if (result.Status == AdminResultStatus.NotFound)
        {
            return NotFound();
        }

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã nhập {result.Value} dòng lời thoại từ YouTube.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? AdminListeningMessages.ImportFailed;
        }

        return RedirectToAction(nameof(Editor), new { id, tab = LessonTabs.Transcript });
    }

    // POST: Admin/ListeningManagement/Publish/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Publish(int id, CancellationToken ct) => SetPublishedAsync(id, true, ct);

    // POST: Admin/ListeningManagement/Unpublish/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Unpublish(int id, CancellationToken ct) => SetPublishedAsync(id, false, ct);

    // POST: Admin/ListeningManagement/Delete/5 (the admin has to type the exact title)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? confirmName, CancellationToken ct)
    {
        if (!User.TryGetUserId(out var adminId))
        {
            return Challenge();
        }

        var result = await _service.DeleteAsync(id, confirmName, adminId, ct);
        if (result.Status == AdminResultStatus.NotFound)
        {
            return NotFound();
        }

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xóa bài nghe và tiến độ học liên quan.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không xóa được bài nghe.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<IActionResult> SetPublishedAsync(int id, bool published, CancellationToken ct)
    {
        var result = await _service.SetPublishedAsync(id, published, ct);
        switch (result.Status)
        {
            case AdminResultStatus.NotFound:
                return NotFound();
            case AdminResultStatus.Success:
                SetSuccess(published ? "Đã xuất bản bài nghe." : "Đã chuyển bài nghe về bản nháp.", result.Warnings);
                break;
            default:
                TempData["ErrorMessage"] = result.Message ?? "Không đổi được trạng thái xuất bản.";
                break;
        }

        return RedirectToAction(nameof(Editor), new { id, tab = LessonTabs.Info });
    }

    /// <summary>Success redirects back to the same tab; errors show the same tab again with the typed rows and per-row messages.</summary>
    private async Task<IActionResult> FinishAsync(AdminResult result, int id, string tab, string successMessage, CancellationToken ct, Action<LessonEditorViewModel> showPosted)
    {
        switch (result.Status)
        {
            case AdminResultStatus.Success:
                TempData["SuccessMessage"] = successMessage;
                return RedirectToAction(nameof(Editor), new { id, tab });
            case AdminResultStatus.NotFound:
                return NotFound();
            case AdminResultStatus.Forbidden:
                return ForbiddenRedirect(id, result.Message, tab);
            default:
                var model = await BuildEditorAsync(id, tab, ct);
                if (model == null)
                {
                    return NotFound();
                }

                showPosted(model);
                model.Errors = result.FieldErrors;
                model.Summary = result.Message;
                return View("Editor", model);
        }
    }

    private IActionResult ForbiddenRedirect(int id, string? message, string tab = LessonTabs.Info)
    {
        TempData["ErrorMessage"] = message ?? AdminListeningMessages.LearnerLessonReadOnly;
        return id > 0
            ? RedirectToAction(nameof(Editor), new { id, tab })
            : RedirectToAction(nameof(Index));
    }

    private async Task<LessonEditorViewModel?> BuildEditorAsync(int id, string tab, CancellationToken ct)
    {
        if (id <= 0)
        {
            return LessonEditorViewModel.From(AdminLessonEditor.NewLesson(), tab);
        }

        var editor = await _service.GetEditorAsync(id, ct);
        return editor == null ? null : LessonEditorViewModel.From(editor, tab);
    }

    private void SetSuccess(string message, IReadOnlyList<string> warnings)
    {
        TempData["SuccessMessage"] = message;
        if (warnings.Count > 0)
        {
            TempData["WarningMessage"] = string.Join(" ", warnings);
        }
    }

    private static List<AdminTranscriptLineInput> ToLineInputs(IEnumerable<TranscriptRowPost> rows)
        => rows.Select(r => new AdminTranscriptLineInput
        {
            Speaker = r.Speaker,
            Text = r.Text,
            VietnameseMeaning = r.VietnameseMeaning,
            StartTime = ParseClock(r.Start),
            EndTime = ParseClock(r.End),
        }).ToList();

    /// <summary>Empty stays empty; a value that is not a time becomes NaN so the service reports it on the right field.</summary>
    private static double? ParseClock(string? text) => AdminInputRules.TryParseClock(text, out var seconds) ? seconds : double.NaN;
}
