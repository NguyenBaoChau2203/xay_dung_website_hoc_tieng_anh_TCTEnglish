---
name: tct-admin-panel
description: Use when adding or changing TCT English admin pages, user management or admin-only workflows.
---

Follow `AGENTS.md` and the current source before applying examples. Paths below
are relative to `TCTEnglish/` unless explicitly prefixed. Use the same agent for
implementation and review. Examples do not authorize changing protected config.


# Admin Panel Feature Builder

## When to Use
Use when building admin panel features: user management, speaking video management, dashboard statistics, or any admin-only functionality.

## Security Contract (MANDATORY)
```csharp
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class <Feature>ManagementController : BaseController { }
```

## Admin Structure
```
TCTEnglish/Areas/Admin/
├── Controllers/
│   ├── DashboardController.cs
│   ├── SpeakingVideoManagementController.cs
│   └── UserManagementController.cs
└── Views/<Controller>/<Action>.cshtml
```

## Controller Template
```csharp
public async Task<IActionResult> Index(string? searchTerm, int page = 1, int pageSize = 20)
{
    var query = _context.<Entities>.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(searchTerm))
        query = query.Where(e => e.Name.Contains(searchTerm));
    // Pagination: .Skip((page - 1) * pageSize).Take(pageSize)
}
```

## Logging Pattern
```csharp
_logger.LogInformation("Admin {AdminId} blocked User {UserId} at {Time}",
    GetCurrentUserId(), targetUserId, DateTime.UtcNow);
```

## UI Conventions
- Tables: Bootstrap `.table .table-hover .table-striped`
- Destructive actions: confirm with Bootstrap modal, NOT `confirm()`
- Messages: `TempData["Success"]` and `TempData["Error"]`
- Filters: inline form with `GET` method (bookmarkable URLs)
- Pagination: Bootstrap `.pagination .justify-content-center`
