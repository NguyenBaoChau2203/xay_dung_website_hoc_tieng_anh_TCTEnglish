# Controller template guidance

Read AGENTS.md and tct-api-endpoint or tct-feature-scaffold. Use the actual feature
controller, BaseController and service namespaces verified through CodeGraph.
New code uses TCTEnglish; resolve existing legacy types explicitly.

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationStatus = TCTVocabulary.Services.OperationStatus;

namespace TCTEnglish.Controllers;

[Authorize]
public class {Feature}Controller : BaseController
{
    private readonly I{Feature}Service _service;

    public {Feature}Controller(I{Feature}Service service) => _service = service;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteAsync(id, GetCurrentUserId());
        if (result.Status == OperationStatus.NotFound) return NotFound();
        if (result.Status != OperationStatus.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }
        TempData["Success"] = "Deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    // Implement the feature's actual Index action using a typed ViewModel.
}
```

The service enforces ownership/access; a controller attribute alone does not.
For create/edit validate ModelState and reload the required view data on failure.
For admin add Area("Admin") and Authorize(Roles = "Admin"). Preserve established
JSON contracts for AJAX endpoints. Do not inject DbContext into a thin controller
that only delegates to services. Do not edit Program.cs without explicit authority.
