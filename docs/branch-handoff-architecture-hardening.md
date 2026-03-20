# Branch Handoff: `architecture-hardening`

> [!NOTE]
> **HISTORICAL DOCUMENT** - This is a snapshot written during the March 2026 architecture-hardening branch.
> It describes what was done during the refactor, not the current active work plan.
> For current structure guidance, see [`docs/project-structure.md`](project-structure.md).
> For the active backlog, see [`docs/architecture-prioritized-backlog.md`](architecture-prioritized-backlog.md).

## Purpose

This branch is a large architecture-hardening and project-structure cleanup pass over the TCT English codebase.
The main goal is to reduce security/regression risk, split responsibilities out of `HomeController`, normalize ViewModels,
and leave the app in a state that is easier to review and continue.

This document is meant as a handoff for another model or engineer to continue work without having to reconstruct the full diff first.

## Current Branch State

- Branch: `architecture-hardening`
- Worktree status: dirty, no staged changes were observed at handoff time
- There are many untracked files/directories because this refactor introduces new controllers, services, ViewModels, views, tests, and docs
- `docs/` itself is currently untracked
- There is an untracked `.tmp/` directory that looks like cleanup material

## High-Level Summary of What Was Done

### 1. `HomeController` was split by responsibility

The former large `HomeController` has been reduced and responsibilities were extracted into dedicated controllers:

- `ClassController`
- `FolderController`
- `SetController`
- `StudyController`
- `ChatController`

Existing URLs were kept stable by routing the new controllers under `/Home/...` where needed.

### 2. Shared business logic and security helpers were extracted

New services and helpers were introduced to centralize logic and reduce duplication:

- `IClassService` / `ClassService`
- `IStudyService` / `StudyService`
- `IStreakService` / `StreakService`
- `IFileStorageService` / `LocalFileStorageService`
- `OperationResult`
- `CurrentUserIdExtensions`
- `FileUploadOptions`
- `ImageUploadPolicies`

This work also tightened class-access checks, upload validation, and current-user resolution.

### 3. ViewModel structure was normalized

View models were moved out of legacy locations into `TCTEnglish/ViewModels/`.

Patterns that were cleaned up:

- Old `TCTEnglish/ViewModel/` files were removed
- Several view-model-like classes were removed from `TCTEnglish/Models/`
- Namespaces are now normalized to `TCTVocabulary.ViewModels`
- `Views/_ViewImports.cshtml` now imports `TCTVocabulary.ViewModels`

Additional cleanup completed in the later pass:

- `SetEditorViewModel` was introduced for create/edit set flows
- Search results were converted to typed result models
- Error, forgot-password, reset-password, and profile/security-related view models were moved into `ViewModels`

### 4. Views were reorganized by feature

Views were moved out of `Views/Home/` into feature folders:

- `Views/Class/`
- `Views/Folder/`
- `Views/Set/`
- `Views/Study/`

Notable additions:

- `_DailyChallenge` partial under `Views/Home/`
- `_SetEditorForm` partial under `Views/Set/`

The vocabulary area also moved further toward typed page models.

### 5. Vocabulary pages were refactored toward typed models and services

`VocabularyController` was simplified and now delegates to `IStudyService`.
Vocabulary pages now use typed ViewModels from `TCTEnglish/ViewModels/VocabularyPageViewModels.cs`.

Later cleanup also updated:

- `Views/Home/Search.cshtml` to use `TCTVocabulary.ViewModels.SearchViewModel`
- Total result counts and typed display properties such as `CreatorName`, `OwnerName`, and `AvatarInitial`

### 6. Security hardening and consistency cleanup were added

Important security/consistency improvements in this branch:

- `BaseController` now uses centralized current-user parsing
- `LearningApiController` now uses anti-forgery, logging, and `IStreakService`
- `ClassChatHub` checks class access consistently and logs access/messaging events
- `ChatController` validates that the uploader can access the class before saving chat images
- `AvatarUploadService` now depends on `IFileStorageService`
- `AccountController` and `SpeakingController` were updated to use the shared current-user pattern

### 7. Test coverage was significantly expanded

The test project now includes more integration/regression coverage and SQLite-backed infrastructure:

- `Sprint1SmokeTests`
- `Sprint2SmokeTests`
- `Sprint3SmokeTests`
- `Sprint4SmokeTests`
- `FolderSetIdorRegressionTests`
- `CriticalFlowSqliteIntegrationTests`

Supporting test infrastructure added:

- `IntegrationTestClientHelper`
- `SqliteTestModelCustomizer`
- updated `TestWebApplicationFactory`
- additional seeded ids in `TestDataIds`

## Important File Areas

### Core controller split

- `TCTEnglish/Controllers/HomeController.cs`
- `TCTEnglish/Controllers/ClassController.cs`
- `TCTEnglish/Controllers/FolderController.cs`
- `TCTEnglish/Controllers/SetController.cs`
- `TCTEnglish/Controllers/StudyController.cs`
- `TCTEnglish/Controllers/ChatController.cs`

### Shared services and security

- `TCTEnglish/Services/ClassService.cs`
- `TCTEnglish/Services/StudyService.cs`
- `TCTEnglish/Services/StudyService.Vocabulary.cs`
- `TCTEnglish/Services/StreakService.cs`
- `TCTEnglish/Services/LocalFileStorageService.cs`
- `TCTEnglish/Services/AvatarUploadService.cs`
- `TCTEnglish/Security/CurrentUserIdExtensions.cs`
- `TCTEnglish/Hubs/ClassChatHub.cs`

### ViewModel normalization

- `TCTEnglish/ViewModels/`
- `TCTEnglish/Views/_ViewImports.cshtml`
- `TCTEnglish/ViewModels/SetEditorViewModel.cs`
- `TCTEnglish/ViewModels/SearchViewModel.cs`
- `TCTEnglish/ViewModels/VocabularyPageViewModels.cs`

### Feature view folders

- `TCTEnglish/Views/Class/`
- `TCTEnglish/Views/Folder/`
- `TCTEnglish/Views/Set/`
- `TCTEnglish/Views/Study/`
- `TCTEnglish/Views/Vocabulary/`

### Test coverage

- `TCTEnglish.Tests/`

## Verification Status at Handoff Time

### Test suite

Command run:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore
```

Result:

- Passed: 67
- Failed: 0
- Skipped: 0

Observed non-blocking warning:

- `NU1900` package vulnerability metadata could not be fetched from `https://api.nuget.org/v3/index.json`
- This did not fail the test run

### Diff hygiene

Command run:

```powershell
git diff --check
```

Result:

- No hard diff/check errors were reported
- There are still many LF -> CRLF warnings in the working copy

## Notable Extra Cleanup Done in the Later Pass

Compared with the earlier state of this branch, the following cleanup appears to have been completed later:

- `SetController` and set views now use `SetEditorViewModel` instead of raw `Set` entities
- `FolderPageViewModel` is now typed and no longer exposes raw `Folder` entities
- `SearchViewModel` is now typed and no longer exposes raw `Folder`, `Class`, `User` entities directly
- `ErrorViewModel`, `ForgotPasswordViewModel`, `ResetPasswordViewModel`, and related account models were moved to `ViewModels`
- `Views/Home/Search.cshtml` was updated to typed result rendering
- `Views/_ViewImports.cshtml` now imports the `ViewModels` namespace
- `YoutubeTranscriptService` got a small nullability cleanup
- `Views/Account/Auth.cshtml` got a safer `TempData["IsBlocked"] is true` check

## Things Another Model Should Know Before Continuing

### 1. `Program.cs` was intentionally touched

`Program.cs` now registers the new services introduced by this branch:

- `IFileStorageService`
- `IClassService`
- `IStreakService`
- `IStudyService`

This is necessary for the branch to run/tests to pass, but it may still matter from a repository-governance perspective because changing `Program.cs` is usually a guarded action in this repo.

### 2. `docs/` is still untracked

The branch contains planning/handoff docs under `docs/`, but the folder is still untracked at this moment.

### 3. `.tmp/` exists and should be reviewed

There is an untracked `.tmp/` directory in the worktree. Another model should decide whether it is:

- intentional working material,
- something to ignore in `.gitignore`,
- or cleanup noise to delete.

### 4. Git may show delete/add pairs even when this is logically a move/refactor

A lot of the structural change is a logical move from:

- `Models` -> `ViewModels`
- `ViewModel` -> `ViewModels`
- `Views/Home` -> feature view folders

Reviewers or follow-up automation should treat this as a refactor/move-heavy branch rather than unrelated deletes and unrelated new files.

## Suggested Next Actions

If another model needs to continue from here, the safest sequence is:

1. Re-run `git status --short` and confirm whether anything new appeared after this handoff.
2. Decide what to do with `.tmp/`.
3. Decide whether `docs/` should be committed as part of this branch or split out.
4. If the goal is final branch polish, review CRLF policy and normalize only if the team wants that.
5. If the goal is architecture review, focus on:
   - controller boundaries,
   - service boundaries,
   - route compatibility,
   - security consistency,
   - final commit slicing.

## Short Executive Summary

This branch is no longer a broken intermediate refactor. It is a broad but coherent architecture-hardening branch that:

- splits `HomeController`,
- extracts shared services,
- normalizes ViewModels,
- reorganizes views by feature,
- tightens auth/anti-forgery/upload handling,
- and expands integration/regression coverage to 67 passing tests.

The main remaining work is not rescue work. It is branch-polish/review/finalization work:

- confirm governance-sensitive changes like `Program.cs`,
- decide on `.tmp/` and `docs/`,
- and package the refactor cleanly for review/continuation.
