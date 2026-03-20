# TCT English - Known Issues & Technical Debt

This document tracks the confirmed remaining issues after the March 2026
controller/service/view-model refactor. Agents should read this before
proposing follow-up work so they do not optimize already-completed items.

---

## Current Status Snapshot

- `HomeController` is now limited to dashboard/public pages; class, folder, set,
  study, and chat flows live in dedicated controllers while keeping legacy
  `/Home/*` routes.
- `ViewModels/` is the single feature view-model folder; the legacy
  `TCTEnglish/ViewModel/` folder is gone.
- `CurrentUserIdExtensions` plus `BaseController` now own claim parsing for MVC
  and API flows; direct inline `ClaimTypes.NameIdentifier` parsing in
  controllers has been removed.
- `TCTEnglish.Tests` exists and covers smoke routes, SQLite integration, SignalR
  chat upload authorization, vocabulary system pages, and folder/set anti-IDOR
  regressions.
- Earlier refactor blockers around `SpeakingController` includes, SignalR method
  name mismatches, class detail read-scope leakage, and split folder/set IDOR
  gaps are resolved.

---

## Known Bugs (Unresolved)

### BUG-006: Goals page is still a static placeholder
- **Symptom**: `/Goals/Index` renders demo cards and a "Chỉnh sửa mục tiêu"
  button, but the page does not load or persist `User.Goal` data.
- **Root Cause**: `GoalsController` only returns the view and there is no real
  read/write flow, service, or POST endpoint for daily-goal management.
- **When it triggers**: Any user tries to edit or trust the numbers shown on the
  goals page.
- **Recommended fix**: Either implement real goal CRUD/progress binding or hide
  the navigation entry until the feature is production-ready.
- **Status**: Confirmed unresolved.

### BUG-007: AutoUnlockWorker is disabled by default in the current config
- **Symptom**: Expired locks are not auto-cleared in the default app
  configuration; users rely on lazy unlock during login or manual admin unlock.
- **Root Cause**: `AutoUnlockWorker` exits early unless
  `BackgroundJobs:AutoUnlockWorkerEnabled` is set to `true`, and the shipped
  config does not currently enable it.
- **When it triggers**: Any environment that uses the current appsettings files
  without an override.
- **Recommended fix**: Add environment-specific configuration and validate
  `LockExpiry` data before enabling the worker broadly.
- **Status**: Confirmed unresolved / operational gap.

### BUG-008: Startup seeding always performs a full JSON sync
- **Symptom**: Application startup still reads and synchronizes
  `wwwroot/data/system-vocabulary.json` on every boot, increasing startup cost
  and mutating the database during normal app launch.
- **Root Cause**: `Program.cs` always calls `JsonVocabularySeeder.SeedFromJsonAsync(...)`;
  the seeder is now upsert-based, but it still scans and saves each startup.
- **When it triggers**: Every application startup.
- **Recommended fix**: Gate seeding by environment/flag, or add a cheap
  change-detection guard before the expensive sync path.
- **Status**: Confirmed unresolved / operational gap.

---

## Technical Debt

### TD-001: HomeController split is complete, but the boundary must stay enforced [LOW]
- **Files**: `Controllers/HomeController.cs`,
  `Controllers/ClassController.cs`, `Controllers/FolderController.cs`,
  `Controllers/SetController.cs`, `Controllers/StudyController.cs`,
  `Controllers/ChatController.cs`
- **Issue**: The main split is done and `HomeController` is down to dashboard
  and public pages, but legacy `/Home/*` routes still make it easy to drift new
  feature work back into the wrong controller.
- **Impact**: Future maintenance will regress quickly if new domain logic is
  added back into `HomeController`.
- **Recommended fix**: Keep all new class/folder/set/study/chat behavior in the
  dedicated feature controllers and services only.
- **Effort**: Small.

### TD-002: Service extraction is still partial [MEDIUM]
- **Files**: `Controllers/AccountController.cs`,
  `Controllers/FolderController.cs`, `Controllers/SetController.cs`,
  `Controllers/SpeakingController.cs`,
  `Areas/Admin/Controllers/UserManagementController.cs`
- **Issue**: `IClassService`, `IStudyService`, `IStreakService`, and
  `IFileStorageService` are now in place, but several controllers still own
  validation, EF orchestration, and branching business rules directly.
- **Impact**: These areas are harder to unit-test and remain the next likely
  regression hotspots.
- **Recommended fix**: Extract the next bounded services around
  account/profile/security, folder/set mutations, and admin user-management
  workflows.
- **Effort**: Medium.

### TD-003: AccountController is still a large mixed-responsibility controller [MEDIUM]
- **Files**: `Controllers/AccountController.cs`,
  `Views/Account/Auth.cshtml`, `Views/Account/ForgotPasswordConfirmation.cshtml`
- **Issue**: Auth, social login, profile, settings, password reset, and account
  deletion all live in one controller, with a mix of typed models and
  `ViewBag`-driven UI messages.
- **Impact**: High churn, weak compile-time safety for some auth screens, and
  harder regression testing.
- **Recommended fix**: Split profile/security/account-lifecycle concerns into
  smaller services or slices, and finish the move to typed view models.
- **Effort**: Medium.

### TD-004: Goals domain is not wired into real user state [MEDIUM]
- **Files**: `Controllers/GoalsController.cs`, `Views/Goals/Index.cshtml`,
  `ViewModels/DashboardViewModel.cs`
- **Issue**: Dashboard displays `User.Goal`, but the dedicated goals page still
  contains static sample content instead of a live experience.
- **Impact**: Product inconsistency and misleading UX.
- **Recommended fix**: Choose one canonical goals workflow and remove the
  placeholder page once the real flow exists.
- **Effort**: Small to medium.

### TD-005: Secrets remain in source-controlled appsettings [SECURITY]
- **Files**: `appsettings.json`
- **Issue**: OAuth credentials, SMTP credentials, and connection-string secrets
  are still committed in the default config file.
- **Impact**: Serious exposure risk if the repository is copied, published, or
  shared outside the intended environment.
- **Recommended fix**: Move secrets to User Secrets for local development and
  environment variables / secret store for deployed environments.
- **Effort**: Small, but coordinated.

### TD-006: Pagination is still missing in some user-facing queries [MEDIUM]
- **Files**: `Controllers/HomeController.cs`, `Services/ClassService.cs`
- **Issue**: Admin user management is paginated, but public search results and
  class discovery/search flows still materialize full result sets.
- **Impact**: Performance risk as the dataset grows.
- **Recommended fix**: Introduce paging parameters plus `.Skip()` / `.Take()`
  on search-heavy views and endpoints.
- **Effort**: Small per screen.

### TD-007: Database index review still has not been codified [MEDIUM]
- **Columns to review**: `LearningProgress.UserId`, `LearningProgress.CardId`,
  `ClassMember.ClassId`, `ClassMember.UserId`, `SpeakingVideo.Level`,
  `SpeakingVideo.Topic`
- **Issue**: The refactor improved query shape, but high-traffic lookup columns
  still need an explicit index review.
- **Impact**: Latency risk on larger datasets and production filtering.
- **Recommended fix**: Add a focused migration after validating the current SQL
  Server execution plans.
- **Effort**: Small.

### TD-008: Structured logging is only partially normalized [LOW]
- **Files**: Critical flows already use `ILogger<T>`, but not every controller
  and service emits equally rich structured events.
- **Issue**: Logging baseline now exists in key controllers/services, yet some
  secondary flows still have thinner diagnostics.
- **Impact**: Debugging production incidents outside the covered hotspots will
  still be slower than necessary.
- **Recommended fix**: Continue standardizing structured logs as the remaining
  controllers are service-extracted.
- **Effort**: Small ongoing.

---

## Architecture Warnings

### NEVER do these in this codebase

1. Modify `Program.cs`, `appsettings.json`, or any `.csproj` without an explicit request.
2. Generate migrations with `DropTable` or `DropColumn` without approval and a rollback plan.
3. Reintroduce inline `ClaimTypes.NameIdentifier` parsing into controllers.
4. Use `.Result` or `.Wait()` on async paths.
5. Pass EF entities directly to Razor views.
6. Bypass ownership/membership checks on any parameterized read or mutation.
7. Replace typed view models with `ViewBag` on feature pages.

### Be careful with these areas

1. `AutoUnlockWorker` is config-gated; enabling it needs data/config validation.
2. `JsonVocabularySeeder` currently mutates data on startup; do not make it more expensive accidentally.
3. SignalR class access rules must stay aligned with HTTP class-detail rules.
4. File uploads should continue to go through `IFileStorageService` plus `ImageUploadPolicies`.
5. Legacy `/Home/*` routes are preserved intentionally; route changes can break bookmarks and tests.

---

## Recently Resolved / Verified

| Item | Current state |
|------|---------------|
| Speaking detail include issue | Fixed in `SpeakingController.Practice` |
| SignalR method naming mismatch | Server/client names now match (`SendMessage`, `ReceiveMessage`, `JoinClass`) |
| Class detail private graph leak | Fixed through sanitized outsider view + service/controller split |
| Folder/set split-controller anti-IDOR gaps | Fixed and covered by `FolderSetIdorRegressionTests` |
| Dashboard SQLite random ordering failure | Fixed with provider-safe fallback logic |
| Duplicate `ViewModel/` vs `ViewModels/` folders | Normalized to `ViewModels/` only |
