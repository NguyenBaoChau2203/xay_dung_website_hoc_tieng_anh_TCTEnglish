# Code Style Quick Reference — TCT English

## C# Naming
| Element | Convention | Example |
|---------|-----------|---------|
| Classes/Methods | PascalCase | `VocabularyController`, `GetByIdAsync()` |
| Local variables | camelCase | `userId`, `cardCount` |
| Private fields | `_camelCase` | `_context`, `_emailSender` |
| Interfaces | `I` + PascalCase | `IAppEmailSender` |
| Async methods | `Async` suffix | `SaveChangesAsync()` |
| Constants | PascalCase | `PageSize`, `DefaultRole` |

## JavaScript Naming
| Element | Convention | Example |
|---------|-----------|---------|
| Variables/Functions | camelCase | `userId`, `markCardLearned()` |
| Constants | UPPER_CASE | `MAX_LOOP_COUNT`, `API_BASE` |
| DOM elements | `el` prefix | `elSearchInput` |
| Event handlers | `on`/`handle` prefix | `onCardFlipped()` |

## File Naming
| Type | Convention | Example |
|------|-----------|---------|
| Controllers | `{Feature}Controller.cs` | `SpeakingController.cs` |
| Admin Controllers | `{Feature}ManagementController.cs` | `UserManagementController.cs` |
| Services | `I{Feature}Service.cs` + `{Feature}Service.cs` | `IAppEmailSender.cs` |
| ViewModels | `{Feature}ViewModel.cs` | `SetCardViewModel.cs` |
| Views | `{Action}.cshtml` | `Index.cshtml` |

## EF Core Patterns
```csharp
// READ: AsNoTracking + ownership
var item = await _context.Sets.AsNoTracking()
    .Where(s => s.SetId == id && s.OwnerId == userId)
    .Select(s => new ViewModel { ... })
    .FirstOrDefaultAsync();

// WRITE: ownership check first
var item = await _context.Sets.FirstOrDefaultAsync(s => s.SetId == id && s.OwnerId == userId);
if (item == null) return NotFound();
```

## Anti-Patterns (NEVER do)
- `someTask.Result` / `.Wait()` — deadlock risk
- `return View(entity)` — use ViewModel
- `var svc = new Service()` — use DI
- `Console.WriteLine()` — use `ILogger<T>`
