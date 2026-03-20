# March 2026 Refactor Package Overview

This document summarizes the large change package currently present in the
worktree and suggests how to split it into three reviewable commits.

> [!NOTE]
> This is a packaging and continuation guide, not the active backlog.
> Use it together with `project-structure.md` and
> `architecture-prioritized-backlog.md`.

## What This Package Already Delivers

The current change set already implements a meaningful portion of the active
architecture backlog:

| Backlog item already reflected in the worktree | Evidence in the change set |
| --- | --- |
| Split `HomeController` by responsibility | New `ClassController`, `FolderController`, `SetController`, `StudyController`, `ChatController`; matching feature views under `Views/Class`, `Views/Folder`, `Views/Set`, and `Views/Study` |
| Standardized current-user lookup | `BaseController`, `Security/CurrentUserIdExtensions.cs`, and related controller updates |
| Anti-forgery and security pass on touched flows | `LearningApiController`, `ChatController`, `ClassChatHub`, and related controller/view updates |
| Shared service extraction | `IClassService`, `IStudyService`, `IStreakService`, `IFileStorageService`, `OperationResult`, and related implementations |
| ViewModel normalization | New `TCTEnglish/ViewModels/` files plus removals from `TCTEnglish/ViewModel/` and legacy `Models/*ViewModel.cs` locations |
| Regression coverage expansion | `Sprint1-4` smoke tests, `CriticalFlowSqliteIntegrationTests`, `FolderSetIdorRegressionTests`, and SQLite test infrastructure |

## Recommended Commit Split

### Part 1 - Architecture hardening already implemented from the backlog

Story:
- Split feature flows out of `HomeController`
- Extract shared services and security helpers
- Normalize ViewModels and feature views
- Keep route compatibility while improving tests and security

Include these path groups:
- `TCTEnglish/Controllers/`
- `TCTEnglish/Services/`
- `TCTEnglish/Security/`
- `TCTEnglish/Hubs/ClassChatHub.cs`
- `TCTEnglish/Areas/Admin/Controllers/`
- `TCTEnglish/ViewModels/`
- deletions from `TCTEnglish/ViewModel/`
- deletions from legacy `TCTEnglish/Models/*ViewModel.cs`
- `TCTEnglish/Views/Class/`
- `TCTEnglish/Views/Folder/`
- `TCTEnglish/Views/Set/`
- `TCTEnglish/Views/Study/`
- related updates in `TCTEnglish/Views/Home/`, `TCTEnglish/Views/Vocabulary/`,
  `TCTEnglish/Views/Speaking/`, `TCTEnglish/Views/Shared/`,
  `TCTEnglish/Views/_ViewImports.cshtml`
- `TCTEnglish.Tests/`
- `TCTEnglish/Program.cs`

Keep out of this part:
- `docs/`
- `AGENTS.md`
- `.ai/`
- `.agent/`
- `.github/copilot-instructions.md`
- `.gitignore` unless a specific ignore rule is required for the runtime/test
  story

Suggested commit message direction:
- `refactor(core): split feature flows and normalize app boundaries`

### Part 2 - Repository and documentation organization

Story:
- Make the repository easier to navigate
- Keep the docs layer small and role-based
- Capture the large refactor package in a durable review guide

Include these path groups:
- `docs/README.md`
- `docs/project-structure.md`
- `docs/architecture-prioritized-backlog.md`
- `docs/architecture-prioritized-backlog.vi.md`
- `docs/post-refactor-followup-plan.md`
- `docs/branch-handoff-architecture-hardening.md`
- `docs/refactor-package-overview.md`
- `.gitignore` when its changes are only about local artifacts and repo hygiene

Suggested commit message direction:
- `docs(repo): organize architecture docs and cleanup repo hygiene`

### Part 3 - AI agent rules, skills, workflows, and templates

Story:
- Realign AI guidance with the post-refactor architecture
- Keep prompts, examples, and templates consistent with the repo boundaries
- Make future agent work safer and faster

Include these path groups:
- `AGENTS.md`
- `.github/copilot-instructions.md`
- `.ai/README.md`
- `.ai/context/`
- `.ai/templates/`
- `.agent/skills/`
- `.agent/workflows/`

Suggested commit message direction:
- `docs(ai): realign agent guidance with post-refactor structure`

## Practical Staging Order

1. Stage Part 1 first so the app/test/runtime story stands on its own.
2. Stage Part 2 next so the repository and docs taxonomy are reviewed
   separately from runtime code.
3. Stage Part 3 last so AI guidance changes stay isolated from product code and
   docs packaging.
4. Use `git add -p` only when a file mixes two stories and cannot be grouped by
   path alone.

## Review Notes

- Many delete/add pairs are logical moves, not unrelated removals and new code.
- Legacy `/Home/*` routes are preserved intentionally even though the logic has
  moved into dedicated feature controllers.
- `branch-handoff-architecture-hardening.md` is historical and should not be
  reviewed as the current active plan.
- The active backlog is still `architecture-prioritized-backlog.md`; this file
  only helps package the current large change set cleanly.
