# AGENTS.md - TCT English

> Universal instructions for AI coding agents working in this repository.

## Project

**TCT English** - Internal EdTech platform for vocabulary learning and speaking
practice for TCT Company employees.

## Technology Stack

- Backend: C# 11+ / ASP.NET Core MVC (.NET 10) / Entity Framework Core 10
- Database: Microsoft SQL Server
- Frontend: Razor Views (`.cshtml`) / Bootstrap 5.3 / jQuery / Vanilla JS
- Real-time: SignalR (`ClassChatHub`)
- Auth: Cookie-based + Google OAuth + Facebook OAuth + BCrypt password hashing
- Media: YoutubeExplode (transcript extraction)
- Email: SMTP (Gmail)

## Core Entities

```text
User (Admin | Teacher | Student)
|- Set -> Card -> LearningProgress (new | learning | mastered)
|- Folder (nested, max 3 levels)
|- Class -> ClassMember (Owner | Member) -> ClassMessage
`- UserSpeakingProgress

SpeakingPlaylist -> SpeakingVideo (YoutubeId, Level A1-C2, Topic) -> SpeakingSentence
```

## Current Architecture (Post-March 2026 Refactor)

The codebase now uses feature-oriented boundaries. Do not assume the old
monolithic `HomeController` structure still exists.

Important note:
- The canonical project namespace is `TCTEnglish.*`.
- If older `TCTVocabulary.*` references still appear in docs or source, treat
  them as legacy leftovers and verify before reusing them in new work.

### Controller Boundaries

| Controller | Responsibility |
|---|---|
| `HomeController` | Dashboard, landing, contact, search, privacy; public/dashboard pages only |
| `ClassController` | Class browsing, creation, detail, join/leave flows |
| `FolderController` | Folder list and folder detail flows |
| `SetController` | Set create, edit, update, and delete flows |
| `StudyController` | Study modes: flashcard, quiz, write, matching, reading, listening, grammar |
| `ChatController` | Chat image upload and class chat HTTP endpoints |
| `VocabularyController` | Vocabulary landing, detail, topic pages; delegates to `IStudyService` |
| `SpeakingController` | Speaking playlist index and practice flows |
| `AccountController` | Auth, OAuth, profile, password reset, security settings |
| `LearningApiController` | AJAX endpoints for card progress tracking |
| `GoalsController` | Goals page; currently a static placeholder |
| `Areas/Admin/*` | Admin-only: dashboard, user management, speaking video management |

### View Boundaries

- `Views/Home/` - dashboard and public pages only
- `Views/Class/`, `Views/Folder/`, `Views/Set/`, `Views/Study/` - feature views created by the refactor
- `Views/Vocabulary/`, `Views/Speaking/` - feature domains
- `Views/Account/` - auth and profile screens
- `Areas/Admin/Views/` - admin area only

Never add new domain logic to `HomeController` or new feature screens to
`Views/Home/`.

Legacy `/Home/*` URLs are preserved for route compatibility, but the logic now
lives in feature controllers.

### Service Layer

| Interface | Implementation | Purpose |
|---|---|---|
| `IClassService` | `ClassService` | Class creation, membership, access checks |
| `IStudyService` | `StudyService` + `StudyService.Vocabulary.cs` | Study mode data and vocabulary pages |
| `IStreakService` | `StreakService` | Daily streak tracking |
| `IFileStorageService` | `LocalFileStorageService` | File save/delete with `ImageUploadPolicies` |
| `IAvatarUploadService` | `AvatarUploadService` | Avatar upload orchestration |
| `IAppEmailSender` | `SmtpAppEmailSender` | Email sending |
| `IYoutubeTranscriptService` | `YoutubeTranscriptService` | Transcript extraction |
| `OperationResult` | shared helper | Standard success/failure wrapper for service returns |

## Mandatory Coding Rules

1. Async-first: always use `async/await` for I/O. Never use `.Result` or `.Wait()`.
2. Anti-IDOR: every UPDATE/DELETE/parameterized READ must include an ownership check like
   `.Where(x => x.UserId == currentUserId && x.Id == requestedId)`.
3. Anti-CSRF: add `[ValidateAntiForgeryToken]` to all POST/PUT/DELETE actions.
4. User identity: use `GetCurrentUserId()` from `BaseController`; never parse claims inline.
5. ViewModels only: never pass raw EF entities to Views; always project to ViewModels.
6. Read-only queries: use `.AsNoTracking()` for display-only queries.
7. Thin controllers: keep actions small; extract business logic into `Services/`.
8. DI: inject through constructors and register services in `Program.cs`.
9. Eager loading: use `.Include()` / `.ThenInclude()` where needed to avoid N+1 queries.
10. Bootstrap 5: no inline styles for standard layouts; prefer Bootstrap utilities.

## Security Boundaries (Never Violate)

- Never modify `Program.cs`, `appsettings.json`, or `.csproj` unless explicitly requested.
- Never generate migrations with `DROP TABLE` / `DROP COLUMN` without explicit user approval.
- Never hardcode connection strings, OAuth secrets, or SMTP credentials.
- RBAC: use `[Authorize(Roles = "Admin")]` for admin-only, `[Authorize]` for authenticated users.
- Return `NotFound()` for IDOR violations; never `Forbid()`.

## Bug Fix Memory

- If the task is a bug fix, read `.ai/context/bug-fix-log.md` and `.ai/context/known-issues.md` before editing code.
- Check newest bug log entries first and search for matching symptoms, stack traces, controllers, entities, or regression patterns.
- Reuse a previous fix pattern only after verifying the current bug has the same root cause.
- After resolving a bug, append a new entry to `.ai/context/bug-fix-log.md` using the file's required format and update `.ai/context/known-issues.md` if that issue was listed there.

## Communication

- After completing any task, include a concise summary in Vietnamese.
- The Vietnamese summary should cover outcome, files changed, checks run, and any remaining risks or blockers.

## Key File Locations

| Type | Location |
|------|----------|
| Main controllers | `TCTEnglish/Controllers/` |
| Admin controllers | `TCTEnglish/Areas/Admin/Controllers/` |
| Models / EF entities | `TCTEnglish/Models/` |
| DbContext | `TCTEnglish/Models/DbflashcardContext.cs` |
| ViewModels (main app) | `TCTEnglish/ViewModels/` |
| ViewModels (admin) | `TCTEnglish/Areas/Admin/ViewModels/` |
| Services | `TCTEnglish/Services/` |
| Security helpers | `TCTEnglish/Security/CurrentUserIdExtensions.cs` |
| SignalR hubs | `TCTEnglish/Hubs/ClassChatHub.cs` |
| Real-time shared types | `TCTEnglish/Realtime/` |
| Background workers | `TCTEnglish/Workers/AutoUnlockWorker.cs` |
| Razor views | `TCTEnglish/Views/` |
| Static files | `TCTEnglish/wwwroot/` |
| Base controller | `TCTEnglish/Controllers/BaseController.cs` |
| Architecture and backlog docs | `docs/` |

## Extended Context - Doc Hierarchy

Read these documents in priority order when starting a task:

| Doc | Purpose | Trust level |
|---|---|---|
| `docs/project-structure.md` | Authoritative folder/boundary map | Current |
| `docs/architecture-prioritized-backlog.md` | Active backlog and next priorities | Current |
| `.ai/context/known-issues.md` | Known bugs and tech debt warnings | Current |
| `.ai/context/coding-conventions.md` | Naming and architecture patterns | Current |
| `docs/post-refactor-followup-plan.md` | Post-refactor sequencing reference | Supporting |
| `docs/branch-handoff-architecture-hardening.md` | March 2026 refactor snapshot | Historical |

Supporting resources:

- `.ai/context/` - project overview, conventions, glossary, bug history
- `.ai/templates/` - reusable code boilerplate (Controller, Service, ViewModel, View, Entity)
- `.agent/skills/` - Antigravity skills
- `.agent/workflows/` - Antigravity workflows
- `.github/copilot-instructions.md` - GitHub Copilot / Visual Studio guidance
