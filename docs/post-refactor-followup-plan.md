# TCT English Post-Refactor Implementation Plan

> [!NOTE]
> **SUPPORTING DOCUMENT** - This is the post-refactor sequencing plan created after the March 2026 refactor.
> It is useful context but not a replacement for the canonical active backlog.
> For the current prioritized backlog, see [`docs/architecture-prioritized-backlog.md`](architecture-prioritized-backlog.md).
> For the current large change package and commit split guidance, see [`docs/refactor-package-overview.md`](refactor-package-overview.md).

This plan turns the remaining work into a practical sequence after the March
2026 controller/service/view-model refactor.

## Baseline Already Completed

- `HomeController` has been reduced to dashboard/public responsibilities.
- Class, folder, set, study, and chat flows were split into dedicated
  controllers while preserving legacy `/Home/*` URLs.
- `BaseController` and `CurrentUserIdExtensions` now centralize current-user
  resolution for MVC and API flows.
- `IClassService`, `IStudyService`, `IStreakService`, and
  `IFileStorageService` are in place.
- `ViewModels/` is the single typed view-model folder.
- `TCTEnglish.Tests` now provides smoke, SQLite integration, and anti-IDOR
  regression coverage.
- Critical logging, upload validation, and chat/class authorization paths were
  strengthened during the refactor.

## Phase 1 - Stabilize Remaining Controller Hotspots

Primary target:
- Move the remaining high-risk logic out of the heaviest controllers.

Work items:
- Introduce bounded services for folder/set mutations.
- Split account responsibilities into smaller slices or services
  (auth/profile/security/account lifecycle).
- Revisit `SpeakingController` and admin user-management for the same pattern.
- Add tests before or alongside each extraction so the refactor stays safe.

Definition of done:
- The touched controllers mostly orchestrate HTTP concerns.
- Business rules live in reusable services.
- New tests cover the extracted workflows.

## Phase 2 - Resolve Product Gaps Left Behind By The Refactor

Primary target:
- Remove visible inconsistencies in the user experience.

Work items:
- Decide whether the Goals feature should be implemented now or hidden.
- If implemented, connect the page to `User.Goal` with typed models and real
  persistence.
- Replace remaining `ViewBag`-driven account UI flows with typed models where
  practical.

Definition of done:
- Users no longer see placeholder Goals content.
- Account screens rely less on loose view-state and are easier to regression-test.

## Phase 3 - Operational Hardening

Primary target:
- Make startup and account-lock behavior predictable across environments.

Work items:
- Define an environment-specific rollout plan for `AutoUnlockWorker`.
- Validate existing `LockExpiry` data before enabling background auto unlock.
- Gate or optimize JSON seeding so startup does not always perform a full sync.
- Continue expanding structured logging in the remaining hotspots.

Definition of done:
- Lock/unlock behavior is consistent.
- Startup behavior is environment-driven and documented.
- Logs are good enough to diagnose failures in the critical flows.

## Phase 4 - Performance And Config Cleanup

Primary target:
- Address scale and operational debt that is now easier to tackle.

Work items:
- Add pagination to search/discovery flows that still load full result sets.
- Review and add missing database indexes for learning progress, class
  membership, and speaking filters.
- Remove secrets from source-controlled configuration.

Definition of done:
- User-facing list/search pages scale better.
- High-traffic query paths have explicit index coverage.
- Runtime secrets are no longer committed in `appsettings.json`.

## Decisions Needed Before Large Roadmap Work

- Should the Goals feature be completed now, or temporarily hidden?
- In which environments should `AutoUnlockWorker` be enabled first?
- When is the seeder allowed to mutate data on startup, if ever?

## Exit Criteria Before Starting Payment, AI, Or New Learning Modules

- Remaining controller-heavy domains have clear service boundaries.
- Goals/account/lock workflows are product-consistent.
- Critical flows retain smoke/integration coverage.
- Startup and background-job behavior are environment-driven.
- Structured logging is reliable in the main user/admin paths.
