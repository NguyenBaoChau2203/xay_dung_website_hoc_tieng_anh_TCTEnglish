using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.Sets;
using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Controllers;

namespace TCTEnglish.Controllers;

/// <summary>Practice hub, session pages and the hard-words list. Words are selected here; grading runs in the browser.</summary>
[Authorize]
[AutoValidateAntiforgeryToken]
[Route("Practice")]
public sealed class PracticeController : BaseController
{
    private readonly IPersonalSetService _personalSets;

    public PracticeController(IPersonalSetService personalSets)
    {
        _personalSets = personalSets;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int? set, CancellationToken ct)
    {
        var model = await _personalSets.GetPracticeHubAsync(set, GetCurrentUserId(), IsAdminUser(), ct);
        return View(model);
    }

    // Literal segment: wins over "{mode}" in route precedence, kept above it for readability.
    [HttpGet("HardWords")]
    public async Task<IActionResult> HardWords(int? set, CancellationToken ct)
    {
        var model = await _personalSets.GetHardWordsAsync(set, GetCurrentUserId(), IsAdminUser(), ct);
        return View(model);
    }

    [HttpGet("{mode}")]
    public async Task<IActionResult> Session(string mode, [FromQuery] PracticeQuery q, CancellationToken ct)
    {
        if (PracticeModes.Find(mode) == null)
        {
            return NotFound();
        }

        // A set the user cannot view is indistinguishable from a missing one (IDOR rule).
        var model = await _personalSets.GetPracticeAsync(mode, q, GetCurrentUserId(), IsAdminUser(), ct);
        return model == null ? NotFound() : View(model);
    }
}
