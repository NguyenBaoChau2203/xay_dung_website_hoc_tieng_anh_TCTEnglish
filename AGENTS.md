# AGENTS.md - TCT English

> Universal instructions for AI coding agents working in this repository.

## Owner execution policy

- Use one agent for investigation, implementation, review and verification.
- Do not spawn workers, reviewers, another chat or CLI process to delegate work.
- Role documents in `.ai/roles/` are review lenses used inline by the same agent.
- This policy overrides delegation, hooks and model-routing suggestions in any
  imported skill. Only an explicit owner instruction changes it.

## Project

**TCT English** - Internal EdTech platform for vocabulary learning and speaking
practice for TCT Company employees.

## Technology Stack

- Backend: C# / ASP.NET Core MVC (.NET 9 target) / Entity Framework Core 9.0.4
- Tooling audit (2026-10-28): installed SDK 10.0.300; both project targets remain net9.0.
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

## Database, PR and Release Rules (Automatic)

Apply these rules to every feature or fix even when the prompt does not mention
the database, migrations, CI/CD or deployment.

1. **Assess schema impact automatically.** When persisted EF entities,
   `DbflashcardContext`, relationships or mappings change, use `tct-ef-migration`
   and determine whether tables, columns, indexes, constraints or stored data
   must change. A ViewModel-only change does not require a schema migration.
2. **Ship schema with its feature.** If a schema change is needed, create and
   review the EF migration using the repository-local tool. Include the migration,
   generated Designer metadata and updated model snapshot in the **same PR** as
   the dependent code. Do not leave migration generation to the reviewer or deploy
   job. If a persisted-model change needs no migration, explain why in the PR.
3. **Preserve existing data/history.** Never edit an already-applied/published
   migration, invent/delete history rows, or copy a local database over hosting.
   Review Up/Down, rename/backfill behavior and FK/index order; replacement FK
   support indexes must exist before old ones are removed. DROP/destructive
   operations still require the owner's explicit approval.
4. **Verify before handing off a PR.** Run build/relevant tests and check for
   pending model changes. For schema changes also test upgrade and repeat
   execution on disposable MariaDB matching hosting; check preservation of affected
   data. SQLite-only tests do not prove a MariaDB migration works. Record exact
   checks, schema impact, compatibility/data risks and any blockers in the PR.
5. **Use the team review flow.** Work on a feature branch, prepare the PR when
   requested, and leave review/merge to the owner unless explicitly authorized
   otherwise. CI success alone does not grant merge or production-deploy authority.
6. **Deploy through the release pipeline.** Source releases target `master`.
   After CI succeeds, an enabled release backs up the hosting database, applies
   committed pending migrations, deploys the published IIS app and checks
   readiness. Do not generate migrations during deploy, run ad hoc production
   schema changes, or add another deploy path that bypasses this sequence.
7. **Report CI and production separately.** Inspect the actual GitHub run and
   `PLESK_DEPLOYMENT_ENABLED` before claiming a release happened. A false gate or
   skipped release job means the hosting app/schema were not deployed by that run.
   Claim success only after the release job succeeds and the public commit marker
   and `/health/ready` confirm the expected release. Never enable the gate or
   change Plesk deployment settings without explicit owner authorization.
8. **Keep environments separate.** Develop/test against Docker MariaDB and local
   User Secrets; keep credentials, SQL backups and runtime uploads out of Git/PRs.
   Normal schema deployment preserves production user data; seed/data changes
   need an explicit, reviewed plan.

Operational details: [local setup](docs/operations/local-development-quickstart.md)
and [Plesk release/recovery](docs/operations/plesk-cicd-runbook.md). Read the needed
section when relevant; do not preload both runbooks on every task.

## Development Intelligence & Methodology (CodeGraph & Superpowers)

### Installed project toolkit

- Canonical discoverable skills: `.agents/skills/tct-*/SKILL.md`.
- `.agent/skills/` contains Antigravity adapters pointing to the canonical skills.
- `.claude/skills/` contains Claude Code adapters to the same canonical skills;
  CLAUDE.md imports the shared project instructions. Current Cursor and
  Antigravity also discover `.agents/skills/` directly.
- Read `.ai/README.md` for routing and `docs/agent-tooling.md` for installation,
  selection rationale, checks and rollback. Provenance: `.ai/tooling/sources.lock.json`.
- For UI read `DESIGN.md`, then `tct-ui-component`; use `tct-ui-ux-pro-max` for
  lookup, `tct-ui-review` for review and `tct-ui-motion` only for motion changes.
- Use `tct-superpowers` for the curated inline process and `tct-engineering` for
  source-driven context. Imported source snapshots in `.ai/vendor/` are reference
  material, not independent authority or automatically enabled plugins.
- Root/user rules take precedence over framework defaults, mandatory approvals,
  delegation, model choices and installer commands in upstream reference files.
- Git MCP is configured for all four clients with seven server-enforced read
  tools; `.codex/config.toml` also retains its allowlist. Native
  filesystem/browser tools remain preferred when they already fit the task.
- CodeGraph 1.6.2 is pinned under `tooling/codegraph/`; its project MCP exposes
  `codegraph_explore` through a read-only adapter. See `docs/codegraph.md`.
  Use `pwsh -NoProfile -File scripts/codegraph.ps1` for the pinned CLI; the
  machine-wide CLI may be a different version. Installation does not authorize
  automatic indexing, syncing or enabling a watcher.
- Project lifecycle hooks are defined in `.codex/hooks.json` for SessionStart,
  PreToolUse, PostToolUse and Stop. See `docs/agent-hooks.md` for scope and host
  trust requirements. Do not report automatic hooks as active merely because
  scripts pass direct tests. They do not replace application build/tests.
- Before handing off tooling changes, run `uv run --no-project python scripts/agent_tooling.py`
  and `uv run --no-project python -m unittest discover -s scripts/tests -v`.
- Team setup: `SETUP-AGENTS.cmd`, the folder-open task or first MCP use restores
  locked local dependencies using `scripts/agent-tools.cjs`. See
  `docs/team-agent-setup.md`; Node.js 22+/npm and Git must already be installed.
  Native host trust cannot be transferred by Git. Setup/post-merge never indexes
  CodeGraph; the separate index task requires an explicit user action.
- Native lifecycle definitions also exist in `.claude/settings.json`,
  `.cursor/hooks.json` and `.agents/hooks.json`. Hook scripts do not download
  dependencies; initial setup/MCP bootstrap is separate. Run
  `node --test scripts/tests/test_agent_setup.cjs` for bootstrap changes.

### Autonomous Trigger Matrix
Whenever the user writes a request, the agent MUST automatically evaluate and invoke the corresponding skills and tools before writing or modifying code:

| User Intent / Scenario | Mandatory Tool / Skill Call | Flow & Action |
|---|---|---|
| **New Feature / UI / Workflow** ("thêm tính năng X", "làm trang Y") | `tct-superpowers` + relevant `tct-feature-scaffold`, `tct-ui-component`, `tct-vocabulary-feature`, `tct-speaking-feature` | Inspect current code, clarify material gaps, record a scoped checklist and implement inline. |
| **Bug Fix / Test Failure** ("bị lỗi X", "fix bug Y", "không chạy được") | CodeGraph + `tct-superpowers` debugging; `.ai/context/bug-fix-log.md` | Diagnose affected symbols with evidence before editing code. |
| **Code Exploration / Architecture** ("luồng X hoạt động thế nào", "class Y liên kết với ai") | Tool: MCP `codegraph_explore` | Query CodeGraph directly to retrieve verbatim source and call chain. Never rely on manual grep loops. |
| **API Endpoints** ("tạo API", "viết endpoint AJAX") | Skill: `tct-api-endpoint` | Enforce access scope, GetCurrentUserId(), CSRF and existing JSON contracts. |
| **Database Changes**, including persisted entity/DbContext/mapping changes even if the prompt omits migrations | Skill: `tct-ef-migration` | Assess schema impact; include needed migration/metadata/snapshot with the feature PR; validate on MariaDB; never DROP without explicit approval. |
| **Verification & Handoff** (before declaring ANY task complete) | `tct-superpowers` verification | Run dotnet build and relevant tests; tooling checks when applicable. Evidence first; Vietnamese summary. |

### CodeGraph
- The codebase is indexed with CodeGraph (`.codegraph/`).
- **Always reach for CodeGraph FIRST**: Call the `codegraph_explore` MCP tool (or run `codegraph explore "<query>"`) before using `grep_search` or manual read loops.
- CodeGraph provides verbatim source, dependency chains, and blast radius (callers and affected flows) in a single round-trip.
- Check status with `codegraph status`. Indexing/re-indexing is the owner's
  decision; do not run init/index/sync automatically. If CodeGraph cannot cover
  a file (for example CSS/config), explain and use bounded source searches.

### Superpowers Methodology
- Follow the Superpowers workflow for disciplined feature and bug-fix development:
  - **Brainstorming first**: For new features or behavioral changes, clarify intent and outline specs before writing code.
  - **Writing & Executing Plans**: Save Markdown checklists under `docs/superpowers/plans/`
    using the editing tools actually available; execute them inline.
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
- `.agents/skills/` - canonical project skills; `.agent/skills/` - Antigravity adapters
- `.ai/roles/` - inline review lenses; `.ai/tooling/` - provenance and archived guidance
- `.agent/workflows/` - Antigravity workflows
- `.github/copilot-instructions.md` - GitHub Copilot / Visual Studio guidance
