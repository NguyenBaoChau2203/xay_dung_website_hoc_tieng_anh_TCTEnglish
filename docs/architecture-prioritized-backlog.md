# TCT English Architecture Backlog (Prioritized)

This backlog reflects the live codebase after the March 2026 refactor, not the
pre-split architecture review state.

Need the current large change-set summary and a recommended three-part commit
split? See [refactor-package-overview.md](refactor-package-overview.md).

Primary goal:
- Keep the security and regression gains from the refactor
- Finish the remaining controller-to-service cleanup
- Only then move into larger roadmap work such as payments, AI, and new modules

Current codebase snapshot:
- ASP.NET Core MVC monolith on .NET 10 with useful boundaries:
  `Areas/Admin`, `Hubs`, `Workers`, `Services`
- `HomeController` is now limited to dashboard/public pages
- Legacy `/Home/*` URLs are preserved, but class/folder/set/study/chat logic now
  lives in dedicated controllers
- `ViewModels/` is the normalized typed-model folder
- Service layer is partial but real:
  `IClassService`, `IStudyService`, `IStreakService`, `IFileStorageService`,
  `IAvatarUploadService`, `IAppEmailSender`, `IYoutubeTranscriptService`
- A dedicated test project now exists: `TCTEnglish.Tests`

## What Is Already In Place

| Item | Current state | Evidence |
|---|---|---|
| Minimal regression coverage | Done and growing | `Sprint1-4`, `CriticalFlowSqliteIntegrationTests`, `FolderSetIdorRegressionTests` |
| Split `HomeController` | Done | `ClassController`, `FolderController`, `SetController`, `StudyController`, `ChatController` now own the moved flows |
| Standardized current-user lookup | Done for MVC/API | `BaseController` + `CurrentUserIdExtensions` |
| Anti-forgery/security pass on touched flows | Largely done | Chat upload, class joins, folder/set mutations, learning API, admin mutations |
| Remove sync EF writes on touched handlers | Done | No request-handler `SaveChanges()` calls remain |
| Shared streak logic | Done | `IStreakService` + `StreakService` |
| File storage abstraction | Done | `IFileStorageService`, `LocalFileStorageService`, `ImageUploadPolicies` |
| Vocabulary page service layer | Done | `VocabularyController` delegates to `IStudyService` |
| ViewModel folder normalization | Done | `TCTEnglish/ViewModel/` removed; `ViewModels/` only |
| Structured logging baseline | Partial but meaningful | Critical controllers/services/hub use `ILogger<T>` |

## Remaining Priorities

## P0 - Stabilize The Remaining Hotspots

### 1. Finish service extraction for the last controller-heavy domains

Why:
- The biggest structural gains are already in place.
- The next regression risk is now concentrated in controllers that still mix EF,
  branching logic, and UI concerns directly.

Primary targets:
- `AccountController`
- `FolderController`
- `SetController`
- `SpeakingController`
- `Areas/Admin/UserManagementController`

Recommended direction:
- Add bounded services rather than a service per entity.
- Favor workflow slices such as account security, folder/set mutations, and
  speaking progress over generic repositories.

Success criteria:
- Controllers become orchestration-only for the touched flows.
- Validation, persistence, and authorization rules become reusable and testable.

### 2. Decide whether the Goals feature should be real or hidden

Current state:
- `GoalsController` only returns a static page.
- Dashboard already reads `User.Goal`.
- The goals UI still shows placeholder/demo content.

Why this is P0:
- Users can reach the page today, so the inconsistency is product-visible.

Options:
- Implement the real goal-edit + progress flow.
- Or remove/hide the page until the feature is ready.

Success criteria:
- There is one canonical goals workflow.
- The UI no longer shows static demo data to production users.

### 3. Operationalize account lock/unlock behavior safely

Current state:
- `UserManagementController.BlockUser` now sets real `LockExpiry` values.
- `AutoUnlockWorker` exists and is healthier than before.
- The worker is still config-gated and disabled by default in the shipped config.

Why this matters:
- The code is better, but the feature is not fully operational without safe
  environment configuration.

Success criteria:
- Background unlock behavior is explicitly enabled per environment.
- Existing lock data is validated before rollout.
- Manual/admin unlock and lazy-login unlock stay consistent with the worker.

### 4. Add tests around the domains that were not fully service-extracted

The current test suite is a solid baseline, but the next value is here:
- account/profile/settings/password flows
- admin speaking-video management
- upload edge cases and cleanup behavior
- worker/config behavior for lock expiry

Success criteria:
- The highest-churn remaining controllers have regression coverage before the
  next major refactor pass.

## P1 - Finish The Foundation Cleanup

### 5. Review startup seeding strategy

Current state:
- `Program.cs` still seeds from `system-vocabulary.json` on every startup.
- The seeder is upsert-based, so duplicate-row risk is reduced.
- Startup still performs a full sync and writes when differences are found.

Recommended next step:
- Gate seeding by environment or feature flag.
- Consider lightweight change detection before the expensive sync path.

### 6. Remove secrets from source-controlled configuration

Current state:
- OAuth, SMTP, and connection-string secrets still live in `appsettings.json`.

Recommended next step:
- Move them to User Secrets for local work.
- Use environment variables or a real secret store for deployed environments.

### 7. Add pagination and index review where the refactor did not reach

Still worth doing:
- Paginate public search and class-discovery paths.
- Review indexes for learning progress, class membership, and speaking filters.

Why now:
- The security/stability work is far enough along that query efficiency is the
  next low-risk payoff.

### 8. Continue the structured logging rollout

Current state:
- Critical flows already use `ILogger<T>`.
- The baseline is good enough to keep, but not yet complete.

Recommended next step:
- As each remaining controller is cleaned up, standardize log shapes for
  request/user/class/set identifiers and failure cases.

## P2 - Later, When Product Scope Is Confirmed

### 9. Payment and subscription domain

Do this only when premium plans or paid orders are committed.

Keep from earlier review work:
- dedicated payment service
- idempotent callbacks
- transaction-safe status updates
- payment and order as separate concepts

Do not do yet:
- full payment architecture before business requirements settle

### 10. AI chat and streaming UX

Do this after:
- remaining service extraction is done
- request/user identity handling is stable
- logging and rate-limiting rules are defined

SignalR is still the likely fit because the app already uses it, but it is not
the next foundation task.

### 11. Unified content model for Reading/Writing/Listening/Grammar

Wait until at least one new module has concrete authoring and progress-tracking
requirements.

Safer rollout:
- launch one focused new module first
- validate authoring/query/reporting needs
- only then decide whether a shared `ContentItem` model is worth the complexity

## Not Recommended Right Now

- Full modular-monolith rewrite
- Repository pattern layered on top of EF Core
- Large polymorphic content schema before the first new module ships
- Cloud storage migration before media growth justifies the operational cost
- Major domain redesign before the remaining controllers are stabilized

## Suggested Delivery Order

### Sprint A - Stabilization
- Extract the next bounded services from account/folder/set flows
- Decide the Goals feature path
- Add regression coverage for the remaining controller-heavy areas

### Sprint B - Operational hardening
- Finalize lock/unlock rollout configuration
- Gate or optimize startup seeding
- Continue structured logging normalization

### Sprint C - Scalability cleanup
- Add pagination to search/discovery flows
- Review and add missing database indexes
- Reassess readiness for new roadmap work

## Exit Criteria Before Starting Payments, AI, Or New Learning Modules

The foundation is good enough when:
- the remaining controller-heavy flows have service boundaries
- goals/account/lock behavior are product-consistent
- critical routes keep smoke/integration coverage
- current-user lookup and authorization rules stay standardized
- startup behavior is predictable and environment-driven
- structured logging is reliable across the critical domains
