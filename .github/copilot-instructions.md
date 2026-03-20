# GitHub Copilot Instructions - TCT English

> For VS Code GitHub Copilot Chat and Visual Studio Copilot users.
> Read this together with `AGENTS.md` and `docs/project-structure.md`.

## Project Summary

**TCT English** - Internal EdTech web app for vocabulary learning and business
English speaking practice.

Stack:
- ASP.NET Core MVC (.NET 10)
- EF Core 10
- SQL Server
- Bootstrap 5.3
- SignalR
- jQuery

## Post-Refactor Architecture (March 2026)

The codebase was refactored from a large monolithic `HomeController` into
feature-oriented boundaries. Do not assume the old pre-split structure still
exists.

### Current Controller Split

| Controller | Owns |
|---|---|
| `HomeController` | Dashboard, landing, contact, search, privacy; public/dashboard only |
| `ClassController` | Class create, browse, join/leave |
| `FolderController` | Folder list and detail |
| `SetController` | Set create/edit/update/delete |
| `StudyController` | Flashcard, quiz, write, matching, reading study modes |
| `ChatController` | Chat image uploads |
| `VocabularyController` | Vocabulary landing/detail; delegates to `IStudyService` |
| `SpeakingController` | Speaking playlist and practice |
| `AccountController` | Auth, OAuth, profile, password reset |
| `LearningApiController` | AJAX card progress endpoints |
| `GoalsController` | Goals page; static placeholder |
| `Areas/Admin/*` | Admin dashboard, user management, speaking video management |

### Route Compatibility

Legacy `/Home/*` routes are preserved intentionally for URL stability. The
logic, however, lives in the dedicated feature controllers above, not in
`HomeController`.

### Key Boundaries

- `ViewModels/` is the only canonical typed view-model location.
- Feature views live in `Views/{Controller}/`.
- New services belong in `Services/` and must be registered in `Program.cs`.
- `Security/CurrentUserIdExtensions.cs` plus `BaseController.GetCurrentUserId()`
  are the approved ways to read the current user ID.
- Use `OperationResult` from `Services/OperationResult.cs` for service mutations.
- Important namespace note: use `TCTEnglish.*` in guidance and examples.
- Treat older `TCTVocabulary.*` references in legacy docs or source files as
  transition-era leftovers that should be verified before reuse.

## Critical Coding Rules

- Async only: always use `async/await`, never `.Result` or `.Wait()`.
- Anti-IDOR: every parameterized READ/UPDATE/DELETE should scope by owner/user where applicable.
- Anti-CSRF: add `[ValidateAntiForgeryToken]` to all POST/PUT/DELETE actions.
- Thin controllers: keep actions small and move business logic into `Services/`.
- ViewModels only: never pass raw EF entities to Razor views.
- AsNoTracking: use it on display-only EF queries.
- Ownership violations should return `NotFound()`, not `Forbid()`.

## Security Boundaries

- Do not modify `Program.cs`, `appsettings.json`, or `.csproj` without an explicit request.
- Do not generate migrations with `DROP TABLE` / `DROP COLUMN` without user approval.
- Do not hardcode connection strings, OAuth secrets, or SMTP credentials.
- Admin controllers must require `[Area("Admin")]` and `[Authorize(Roles = "Admin")]`.

## Code Style Quick Reference

### C# Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Classes/Methods | PascalCase | `ClassController`, `GetByIdAsync()` |
| Local variables | camelCase | `userId`, `cardCount` |
| Private fields | `_camelCase` | `_context`, `_classService` |
| Interfaces | `I` prefix | `IClassService`, `IStudyService` |
| Async methods | `Async` suffix | `SaveChangesAsync()` |

### Frontend

- Bootstrap 5 utility classes
- ASP.NET Tag Helpers (`asp-controller`, `asp-action`, `asp-for`)
- `@Html.AntiForgeryToken()` in forms
- Page-specific JS in `@section Scripts { }`

## Where To Read More

| Doc | Purpose |
|---|---|
| `AGENTS.md` | Universal agent rules |
| `docs/project-structure.md` | Authoritative folder/boundary map |
| `docs/architecture-prioritized-backlog.md` | Active backlog |
| `.ai/context/known-issues.md` | Known bugs and tech debt |
| `.ai/context/coding-conventions.md` | Naming and pattern reference |
| `docs/branch-handoff-architecture-hardening.md` | Historical refactor snapshot; not current instructions |
