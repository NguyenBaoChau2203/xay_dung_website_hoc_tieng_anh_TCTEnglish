# AGENTS.md — TCT English

> Universal instructions for all AI coding agents (Cursor, Antigravity, Codex, Copilot, etc.)

## Project

**TCT English** — EdTech Platform for vocabulary learning & speaking practice.
Internal web app for TCT Company employees to learn business English.

## Technology Stack

- **Backend**: C# 11+ / ASP.NET Core MVC (.NET 10) / Entity Framework Core 10
- **Database**: Microsoft SQL Server
- **Frontend**: Razor Views (.cshtml) / Bootstrap 5.3 / jQuery / Vanilla JS
- **Real-time**: SignalR (`ClassChatHub`)
- **Auth**: Cookie-based + Google OAuth + Facebook OAuth + BCrypt password hashing
- **Media**: YoutubeExplode (transcript extraction)
- **Email**: SMTP (Gmail)

## Core Entities

```
User (Admin | Teacher | Student)
├── Set → Card → LearningProgress (new | learning | mastered)
├── Folder (nested, max 3 levels)
├── Class → ClassMember (Owner | Member) → ClassMessage
└── UserSpeakingProgress

SpeakingPlaylist → SpeakingVideo (YoutubeId, Level A1-C2, Topic) → SpeakingSentence
```

## Mandatory Coding Rules

1. **Async-first**: Always `async/await` for I/O. NEVER `.Result` or `.Wait()` (deadlock risk)
2. **Anti-IDOR**: Every UPDATE/DELETE/parameterized READ must include ownership check:
   `.Where(x => x.UserId == currentUserId && x.Id == requestedId)`
3. **Anti-CSRF**: `[ValidateAntiForgeryToken]` on ALL POST/PUT/DELETE actions
4. **User identity**: Use `GetCurrentUserId()` from `BaseController` — never parse claims inline
5. **ViewModels only**: Never pass raw EF entities to Views — always project to ViewModels
6. **Read-only queries**: Always `.AsNoTracking()` for display-only queries
7. **Thin controllers**: ≤25 lines logic per action. Extract business logic to Services in `Services/`
8. **DI**: Inject via constructor. Register with `builder.Services.AddScoped<>()` in `Program.cs`
9. **Eager loading**: Use `.Include()` / `.ThenInclude()` to prevent N+1 queries
10. **Bootstrap 5**: No inline styles for standard layouts. Use Bootstrap utility classes

## Security Boundaries (NEVER violate)

- NEVER modify `Program.cs`, `appsettings.json`, or `.csproj` unless explicitly requested
- NEVER generate migrations with `DROP TABLE` / `DROP COLUMN` without explicit user approval
- NEVER hardcode connection strings, OAuth secrets, or SMTP credentials
- RBAC: `[Authorize(Roles = "Admin")]` for admin-only, `[Authorize]` for authenticated users
- Return `NotFound()` for IDOR violations (never `Forbid()` — avoids resource enumeration)

## Bug Fix Memory

- If the task is a bug fix, read `.ai/context/bug-fix-log.md` and `.ai/context/known-issues.md` before editing any code
- Check newest bug log entries first and search for matching symptoms, stack traces, controllers, entities, or regression patterns
- Reuse a previous fix pattern only after verifying the current bug has the same root cause
- After resolving a bug, append a new entry to `.ai/context/bug-fix-log.md` using the file's required format and update `.ai/context/known-issues.md` if that issue was listed there

## Communication

- After completing any task, include a concise summary in Vietnamese
- The Vietnamese summary should cover outcome, files changed, checks run, and any remaining risks or blockers

## Key File Locations

| Type | Location |
|------|----------|
| Controllers | `TCTEnglish/Controllers/` |
| Admin Controllers | `TCTEnglish/Areas/Admin/Controllers/` |
| Models/Entities | `TCTEnglish/Models/` |
| DbContext | `TCTEnglish/Models/DbflashcardContext.cs` |
| ViewModels | `TCTEnglish/ViewModels/` |
| Services | `TCTEnglish/Services/` |
| Views | `TCTEnglish/Views/` |
| Static files | `TCTEnglish/wwwroot/` |
| Base Controller | `TCTEnglish/Controllers/BaseController.cs` |

## Extended Context

- See `.ai/context/` for detailed project documentation, coding conventions, known issues, domain glossary, and bug fix history (`bug-fix-log.md`)
- See `.ai/templates/` for reusable code boilerplate (Controller, Service, ViewModel, View, Entity)
- See `.agent/skills/` for Antigravity IDE skills (SKILL.md format)
- See `.agent/workflows/` for multi-step process guides
- See `.cursor/rules/` for Cursor IDE auto-triggered rules (.mdc format)
