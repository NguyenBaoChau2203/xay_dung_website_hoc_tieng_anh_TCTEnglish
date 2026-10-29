---
name: Admin Panel
description: Build or extend admin panel features for TCT English (user management, video management, dashboard)
---

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
└── Views/Admin/<Feature>/
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
