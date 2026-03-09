# Copilot Instructions — TCT English

## Project Context
TCT English is an ASP.NET Core MVC (.NET 10) EdTech platform for vocabulary learning and speaking practice.
Stack: C# 11+, EF Core 10, SQL Server, Bootstrap 5, jQuery, SignalR, Cookie + OAuth auth.

## Code Style

### C# Backend
- Use `async/await` for all I/O operations — never `.Result` or `.Wait()`
- Add `[ValidateAntiForgeryToken]` to all POST/PUT/DELETE actions
- Use `GetCurrentUserId()` from `BaseController` for user identity
- Always include ownership checks (Anti-IDOR): `.Where(x => x.UserId == userId && x.Id == id)`
- Add `.AsNoTracking()` to all read-only EF queries
- Project into ViewModels — never pass EF entities directly to Views
- Keep controllers thin (≤25 lines logic) — extract business logic to `Services/`

### Frontend
- Use Bootstrap 5 utility classes — no inline styles for standard layouts
- Use ASP.NET Tag Helpers (`asp-controller`, `asp-action`, `asp-for`)
- Include `@Html.AntiForgeryToken()` in all forms
- Isolate page-specific JS in `@section Scripts { }`

## Architecture
- **Controller** → validate → call service → return view/redirect
- **Service** → business logic + EF queries
- **ViewModel** → data contract between controller and view
- **View** → render HTML with Bootstrap 5

## Naming
- Classes/Methods: `PascalCase`
- Local variables: `camelCase`
- Private fields: `_camelCase`
- Interfaces: `I` prefix (`IAppEmailSender`)
- Async methods: `Async` suffix

## Security
- `[Authorize(Roles = "Admin")]` for admin-only endpoints
- Return `NotFound()` for IDOR violations (not `Forbid()`)
- Validate MIME types for file uploads, not just extensions
- Never hardcode secrets — use `appsettings.json` / environment variables

## Extended Context
See `AGENTS.md` at project root and `.ai/context/` for detailed documentation.
