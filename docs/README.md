# TCT English Docs Guide

This folder is intentionally small. Keep only current guidance, supporting
planning notes that still help ongoing work, and clearly labeled historical
handoff material.

## Docs Map

| File | Status | Use When |
| --- | --- | --- |
| `project-structure.md` | Current | You need the authoritative folder map and controller/view boundaries. |
| `architecture-prioritized-backlog.md` | Current | You need the live backlog and the next priorities. |
| `architecture-prioritized-backlog.vi.md` | Current | You want the Vietnamese mirror of the active backlog. |
| `refactor-package-overview.md` | Supporting | You are continuing the current large refactor package or splitting it into reviewable commits. |
| `post-refactor-followup-plan.md` | Supporting | You need a practical post-refactor execution sequence. |
| `branch-handoff-architecture-hardening.md` | Historical | You need the original branch handoff snapshot for March 2026 context. |

## Recommended Read Order

1. Start with `project-structure.md`.
2. Read `architecture-prioritized-backlog.md`.
3. Read `refactor-package-overview.md` if you are continuing the current large
   change set or splitting commits.
4. Read `post-refactor-followup-plan.md` only when sequencing follow-up work.
5. Read `branch-handoff-architecture-hardening.md` only for historical context.

## Maintenance Rules

- Prefer updating an existing current doc instead of adding a new one.
- Label supporting and historical docs clearly near the top.
- Remove one-off planning docs after their actions are fully reflected in the
  current guidance set.
- Keep long-lived architecture truth in `project-structure.md` or
  `architecture-prioritized-backlog.md`, not in temporary planning files.
