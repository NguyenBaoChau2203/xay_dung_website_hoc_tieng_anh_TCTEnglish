# TCT English — Coding Conventions & Architecture Patterns

This document defines the authoritative coding standards for the TCT English codebase.
All AI-generated code must follow these conventions.

---

## Naming Conventions

### C# (Backend)

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `VocabularyController`, `SetCardViewModel` |
| Methods | PascalCase | `GetCurrentUserId()`, `SaveProgressAsync()` |
| Properties | PascalCase | `SetId`, `OwnerId`, `CreatedAt` |
| Local variables | camelCase | `userId`, `currentSet`, `cardCount` |
| Private fields | `_camelCase` | `_context`, `_emailSender` |
| Constants | UPPER_CASE or PascalCase | `PageSize`, `DefaultRole` |
| Interfaces | `I` prefix PascalCase | `IAppEmailSender`, `IAvatarUploadService` |
| Async methods | `Async` suffix | `SaveChangesAsync()`, `GetByIdAsync()` |

### Files & Folders

| Type | Convention | Location |
|------|-----------|---------|
| Controllers | `{Feature}Controller.cs` | `Controllers/` |
| Admin Controllers | `{Feature}ManagementController.cs` | `Areas/Admin/Controllers/` |
| Models (entities) | `{EntityName}.cs` | `Models/` |
| ViewModels | `{Feature}ViewModel.cs` | `ViewModels/` |
| Services (interface) | `I{Feature}Service.cs` | `Services/` |
| Services (impl) | `{Feature}Service.cs` | `Services/` |
| Views | `{Action}.cshtml` | `Views/{Controller}/` |
| Shared partials | `_{Name}.cshtml` | `Views/Shared/` |
| JS files | `{feature}.js` (kebab-case) | `wwwroot/js/` |
| CSS files | `{feature}.css` (kebab-case) | `wwwroot/css/` |

### JavaScript

```javascript
// Variables + functions: camelCase
const userId = 42;
function markCardLearned(cardId) { ... }

// Constants: UPPER_CASE
const MAX_LOOP_COUNT = 3;
const API_BASE = '/api';

// DOM element variables: prefix with `el`
const elSearchInput = document.getElementById('searchInput');

// Event handlers: `on` prefix or `handle` prefix
function onCardFlipped(cardId) { ... }
function handleSearchInput(event) { ... }
```

---

## Architecture Patterns

### MVC Layer Responsibilities

```
Controller  → Receive request → validate → call service → return result
Service     → Business logic, DB operations, domain rules
ViewModel   → Data contract between controller and view
View        → Render HTML with Bootstrap 5, display ViewModel data
```

**Controller action should be ≤ 25 lines of logic:**
```csharp
// CORRECT — thin controller:
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> CreateSet(CreateSetViewModel model)
{
    if (!ModelState.IsValid) return View(model);
    var userId = GetCurrentUserId();
    var result = await _setService.CreateAsync(model, userId);
    if (!result.Success) { ModelState.AddModelError("", result.Error); return View(model); }
    return RedirectToAction(nameof(Index));
}

// INCORRECT — business logic in controller (extract to service):
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> CreateSet(CreateSetViewModel model)
{
    // ... 50 lines of business logic directly here
}
```

### Dependency Injection Pattern

```csharp
// Constructor injection (preferred):
public class VocabularyController : BaseController
{
    private readonly DbflashcardContext _context;
    private readonly ISetService _setService;

    public VocabularyController(DbflashcardContext context, ISetService setService)
    {
        _context = context;
        _setService = setService;
    }
}

// Register in Program.cs:
builder.Services.AddScoped<ISetService, SetService>();
// AddScoped  → one per HTTP request (most common for services)
// AddSingleton → one for app lifetime (IAppEmailSender, caches)
// AddTransient → new instance every time (rare)
```

### User Identity Pattern

```csharp
// ALWAYS use BaseController.GetCurrentUserId():
var userId = GetCurrentUserId();

// NEVER use inline parsing:
var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);  // ❌
```

### EF Core Query Patterns

```csharp
// READ — Always AsNoTracking + ownership check:
var set = await _context.Sets
    .AsNoTracking()
    .Where(s => s.SetId == id && s.OwnerId == userId)
    .Select(s => new SetViewModel { ... })
    .FirstOrDefaultAsync();

// READ with relationships:
var user = await _context.Users
    .AsNoTracking()
    .Include(u => u.Sets).ThenInclude(s => s.Cards)
    .FirstOrDefaultAsync(u => u.UserId == userId);

// WRITE — track entity + ownership check:
var set = await _context.Sets
    .FirstOrDefaultAsync(s => s.SetId == id && s.OwnerId == userId);
if (set == null) return NotFound();
set.SetName = model.SetName;
await _context.SaveChangesAsync();

// CREATE:
var newSet = new Set { OwnerId = userId, SetName = model.SetName, CreatedAt = DateTime.UtcNow };
_context.Sets.Add(newSet);
await _context.SaveChangesAsync();

// DELETE — verify ownership first:
var set = await _context.Sets.FirstOrDefaultAsync(s => s.SetId == id && s.OwnerId == userId);
if (set == null) return NotFound();
_context.Sets.Remove(set);
await _context.SaveChangesAsync();
```

### ViewModel Pattern

```csharp
// NEVER pass EF entities to Views:
return View(set);  // ❌ Exposes internals, risky

// ALWAYS project to ViewModel:
var viewModel = new SetDetailViewModel
{
    SetId = set.SetId,
    SetName = set.SetName,
    Cards = set.Cards.Select(c => new CardViewModel { ... }).ToList()
};
return View(viewModel);  // ✅
```

---

## Security Conventions

### Authorization Hierarchy

```csharp
[AllowAnonymous]           → Public (Landing, Auth pages)
[Authorize]                → Any authenticated user
[Authorize(Roles = "Teacher,Admin")]  → Teacher or Admin
[Authorize(Roles = "Admin")]          → Admin only
```

### IDOR Prevention (Required on all parameterized queries)

```csharp
// Pattern: ID from URL + UserId from claims = ownership check
.Where(x => x.Id == requestedId && x.UserId == currentUserId)

// For admin actions (bypass ownership):
[Authorize(Roles = "Admin")]
// Then query without userId filter
```

### CSRF Pattern

```csharp
// Every form POST:
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> Update(ViewModel model) { ... }

// Every AJAX mutation (in JS):
headers: { 'RequestVerificationToken': document.querySelector('[name=__RequestVerificationToken]').value }
```

---

## Razor View Conventions

```cshtml
@* Always declare model type *@
@model TCTEnglish.ViewModels.SetDetailViewModel

@* Use Tag Helpers (not Url.Action or UrlHelper) *@
<a asp-controller="Home" asp-action="Study" asp-route-setId="@Model.SetId">Study</a>
<form asp-controller="Vocabulary" asp-action="Delete" method="post">

@* Use asp-for for form inputs *@
<input asp-for="SetName" class="form-control" />
<span asp-validation-for="SetName" class="text-danger"></span>

@* Page-specific scripts in section *@
@section Scripts {
    <script src="~/js/vocabulary.js" asp-append-version="true"></script>
}

@* Conditional rendering — always null-safe *@
@if (Model.Cards?.Any() == true) { ... }
else { <p>No cards yet.</p> }
```

---

## Anti-Patterns (Never Do These)

```csharp
// ❌ Sync DB operations:
var cards = _context.Cards.ToList();  // Use ToListAsync()

// ❌ Expose entities to Views:
return View(await _context.Sets.FindAsync(id));

// ❌ Business logic in Views:
@if (someComplexCalculation) { ... }  // Calculate in ViewModel

// ❌ Magic strings for roles:
[Authorize(Roles = "admin")]  // Wrong case! Use "Admin"

// ❌ Instantiate services with new:
var service = new SetService(_context);  // Use DI

// ❌ Hardcode user ID:
.Where(s => s.OwnerId == 42)  // Get from GetCurrentUserId()

// ❌ Blocking async:
someTask.Result;  // Deadlock risk!
someTask.GetAwaiter().GetResult();  // Deadlock risk!
```
