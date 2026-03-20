# TCT English Project Structure

This document is the authoritative map for the post-refactor repository layout on
the `architecture-hardening` branch. It describes the intended folder
responsibilities after the controller split, ViewModel normalization, and
repository cleanup work.

If you need the docs taxonomy first, start with [README.md](README.md).

Use this together with [post-refactor-followup-plan.md](post-refactor-followup-plan.md)
for ongoing architecture work. The branch handoff note remains historical
context; this file describes the structure we intend to keep.

## Repository Root

| Path | Responsibility |
| --- | --- |
| `TCTEnglish/` | Main ASP.NET Core MVC application |
| `TCTEnglish.Tests/` | Smoke, regression, and integration tests |
| `docs/` | Architecture notes, handoff docs, and cleanup guidance |
| `scripts/` | Local helper scripts such as encoding or validation utilities |
| `.ai/context/` | Project-specific architecture, conventions, glossary, and bug history |
| `.agent/workflows/` | Repo workflows used to guide implementation and review passes |
| `.agent/skills/` | Repo-specific skills for feature and audit tasks |

## Main Application Layout

| Path | Responsibility |
| --- | --- |
| `TCTEnglish/Controllers/` | MVC controllers for public and authenticated user flows |
| `TCTEnglish/Areas/Admin/Controllers/` | Admin-only controllers |
| `TCTEnglish/Services/` | Business workflows, reusable domain services, and small infrastructure abstractions |
| `TCTEnglish/ViewModels/` | Typed UI models for the main application |
| `TCTEnglish/Areas/Admin/ViewModels/` | Typed UI models used only by the admin area |
| `TCTEnglish/Views/` | Razor views organized by feature/controller responsibility |
| `TCTEnglish/Models/` | EF Core entities, `DbflashcardContext`, and seeding helpers only |
| `TCTEnglish/Security/` | Authentication and current-user helper extensions |
| `TCTEnglish/Hubs/` | SignalR hub endpoints |
| `TCTEnglish/Realtime/` | Shared presence/state helpers and SignalR message contracts used across hubs, admin flows, and workers |
| `TCTEnglish/Workers/` | Background workers such as auto unlock processing |
| `TCTEnglish/wwwroot/` | Static assets, library files, and runtime upload roots |

## Controller And View Boundaries

The current app structure is feature-oriented, but route compatibility is still
preserved where useful. That means some flows now live in dedicated controllers
even when the public URL still uses `/Home/...`.

### Controllers

- `HomeController` is intentionally limited to dashboard and public pages such as landing, contact, search, privacy, and dashboard widgets.
- `ClassController` owns class browsing, creation, and class detail flows.
- `FolderController` owns folder list and folder detail flows.
- `SetController` owns set create/edit/update/delete flows.
- `StudyController` owns study-mode views such as quiz, matching, write, reading, listening, and related practice pages.
- `ChatController` owns chat upload and class chat HTTP endpoints.
- `VocabularyController` remains the vocabulary landing/detail/topic area and now leans on `IStudyService`.
- `SpeakingController` remains the speaking playlist and practice area.

### Views

- `Views/Home/` now contains only dashboard/public pages and shared home-specific partials.
- `Views/Class/`, `Views/Folder/`, `Views/Set/`, and `Views/Study/` are the canonical feature view folders created by the refactor.
- `Views/Vocabulary/` and `Views/Speaking/` remain feature folders for those domains.
- `Areas/Admin/Views/` stays isolated inside the admin area.

## ViewModel Placement Rules

- Main-site typed models belong in `TCTEnglish/ViewModels/`.
- Admin-only typed models belong in `TCTEnglish/Areas/Admin/ViewModels/`.
- EF entities and persistence types stay in `TCTEnglish/Models/`.
- Do not reintroduce UI-only types under `Models/` or the removed legacy `ViewModel/` folder.

## Important Normalizations Kept By This Branch

| Old path | New path |
| --- | --- |
| `TCTEnglish/ViewModel/*` | `TCTEnglish/ViewModels/*` |
| `TCTEnglish/Models/ErrorViewModel.cs` | `TCTEnglish/ViewModels/ErrorViewModel.cs` |
| `TCTEnglish/Models/ForgotPasswordViewModel.cs` | `TCTEnglish/ViewModels/ForgotPasswordViewModel.cs` |
| `TCTEnglish/Models/ResetPasswordViewModel.cs` | `TCTEnglish/ViewModels/ResetPasswordViewModel.cs` |
| `TCTEnglish/Models/UpdateProfileViewModel.cs` | `TCTEnglish/ViewModels/UpdateProfileViewModel.cs` |
| `TCTEnglish/Views/Home/Class*.cshtml` | `TCTEnglish/Views/Class/` |
| `TCTEnglish/Views/Home/Folder*.cshtml` | `TCTEnglish/Views/Folder/` |
| `TCTEnglish/Views/Home/CreateSet.cshtml`, `EditSet.cshtml` | `TCTEnglish/Views/Set/` |
| `TCTEnglish/Views/Home/Study.cshtml`, `QuizMode.cshtml`, `WriteMode.cshtml`, `MatchingMode.cshtml`, `Speaking.cshtml`, `Reading.cshtml`, `Listening.cshtml`, `Writing.cshtml`, `Grammar.cshtml` | `TCTEnglish/Views/Study/` |

## Repository Hygiene Rules

- Local-only directories such as `.tmp/`, `.dotnet-home/`, `.dotnet/`, `.dotnet_cli/`, `.appdata/`, `bin/`, and `obj/` are not source and should stay ignored.
- Temporary artifacts should not be committed under the repository root or inside `docs/`.
- Runtime uploads stay under the existing ignored subfolders in `wwwroot/`.
- Keep documentation in `docs/` and keep ad hoc scratch notes inside ignored local folders instead of source directories.

## Intentional Non-Changes

These points are intentional and should not be treated as cleanup misses:

- The canonical project namespace is `TCTEnglish.*`. Older
  `TCTVocabulary.*` references may still appear in legacy files and should be
  treated as transition leftovers.
- `TCTEnglish/Realtime/` remains separate from `TCTEnglish/Hubs/` because it holds shared real-time support types, not hub endpoints.
- `Views/Home/` still exists because the app still has real home/public pages. The goal is not to remove the folder, only to keep feature screens out of it.
- Existing routes and behavior should stay stable even when implementation files moved to cleaner locations.

## Quick Scan Checklist For Future Changes

- New controller-heavy workflow: prefer a dedicated controller plus service, not more `HomeController` growth.
- New UI model: place it in `ViewModels/` or `Areas/Admin/ViewModels/`.
- New real-time endpoint: hub in `Hubs/`, shared payload/state types in `Realtime/` if reused elsewhere.
- New background job: place it in `Workers/`.
- New documentation about architecture or structure: place it in `docs/`.
