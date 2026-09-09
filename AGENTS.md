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
| `GoalsController` | Goals page with real goal update, progress, and badge workflows |
| `NotificationController` | Notification API endpoints (unread count, list, mark-read) + full notification page |
| `Areas/Admin/*` | Admin-only: dashboard, user management, speaking video management, notification broadcast |

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
| `IGoalsService` | `GoalsService` | Goal recording, badge refresh, streak rewards |
| `INotificationService` | `NotificationService` | Notification CRUD, auto-generation (streak/goal/badge), admin broadcast, cleanup |
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

## Development Intelligence & Methodology (CodeGraph & Superpowers)

### Autonomous Trigger Matrix
Whenever the user writes a request, the agent MUST automatically evaluate and invoke the corresponding skills and tools before writing or modifying code:

| User Intent / Scenario | Mandatory Tool / Skill Call | Flow & Action |
|---|---|---|
| **New Feature / UI / Workflow** ("thêm tính năng X", "làm trang Y") | Skills: `superpowers:brainstorming` → `superpowers:writing-plans` + Domain Skills (`feature-scaffold`, `ui-component`, `vocabulary-feature`, `speaking-feature`) | Brainstorm specs with user, create task checklist artifact, then implement step-by-step. |
| **Bug Fix / Test Failure** ("bị lỗi X", "fix bug Y", "không chạy được") | Tool: `codegraph_explore`<br>Skill: `superpowers:systematic-debugging`<br>Context: `.ai/context/bug-fix-log.md` | Explore affected symbols with CodeGraph, diagnose root cause with evidence before editing code. |
| **Code Exploration / Architecture** ("luồng X hoạt động thế nào", "class Y liên kết với ai") | Tool: MCP `codegraph_explore` | Query CodeGraph directly to retrieve verbatim source and call chain. Never rely on manual grep loops. |
| **API Endpoints** ("tạo API", "viết endpoint AJAX") | Skill: `api-endpoint` | Enforce anti-IDOR (`GetCurrentUserId()`), Anti-CSRF, and standard JSON response envelope. |
| **Database Changes** ("tạo migration", "thêm cột/bảng") | Skill: `ef-migration` | Impact assessment, nullable/reversible migration check, never DROP without explicit approval. |
| **Verification & Handoff** (before declaring ANY task complete) | Skill: `superpowers:verification-before-completion` | Run `dotnet build` and relevant tests. Evidence before assertions. Output Vietnamese summary. |

### CodeGraph
- The codebase is indexed with CodeGraph (`.codegraph/`).
- **Always reach for CodeGraph FIRST**: Call the `codegraph_explore` MCP tool (or run `codegraph explore "<query>"`) before using `grep_search` or manual read loops.
- CodeGraph provides verbatim source, dependency chains, and blast radius (callers and affected flows) in a single round-trip.
- Re-index or status check: `codegraph status` / `codegraph init`.

### Superpowers Methodology
- Follow the Superpowers workflow for disciplined feature and bug-fix development:
  - **Brainstorming first**: For new features or behavioral changes, clarify intent and outline specs before writing code.
  - **Writing & Executing Plans**: Create clear implementation plans with task artifacts (`write_to_file` with `IsArtifact: true` and `ArtifactMetadata.ArtifactType: "task"`).
  - **Systematic Debugging**: For bugs, find root cause with evidence before touching code (follow `.ai/context/bug-fix-log.md`).
  - **Test-Driven Development (TDD)**: Write failing tests before implementation when writing unit tests.
  - **Verification Before Completion**: Run automated tests (`dotnet test`) and verify outputs before declaring a task complete.

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
| Background workers | `TCTEnglish/Workers/` (`AutoUnlockWorker.cs`, `NotificationWorker.cs`) |
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
