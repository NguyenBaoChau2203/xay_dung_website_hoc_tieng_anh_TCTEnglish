---
description: Systematic bug diagnosis and fix process for TCT English
---

# Workflow: Bug Investigation

## Phase 1 — Triage
Classify the bug immediately:
- HTTP 500 (server error), 404 (route), 401/403 (auth), 400 (validation)
- JS/AJAX error, SignalR issue, EF Migration mismatch, Seeder issue, Silent logic bug

## Phase 2 — Information Gathering
Collect: Exception type, stack trace, request info (URL, method, role), when it started, reproducibility, environment.

## Phase 3 — Layer-by-Layer Trace
1. **Route**: Is the controller/action mapped? Area route registered?
2. **Controller**: Is action async? Parameters bound correctly? `[Authorize]` causing redirect?
3. **EF Query**: Ownership check? `.Include()` for navigation? Null-checked `FirstOrDefaultAsync`?
4. **ViewModel**: All properties populated? Type matches `@model` in View?
5. **View**: `@model` declared? Null-safe access? No unsafe `@Html.Raw()`?
6. **JavaScript**: Fetch URL correct? Anti-forgery token sent? Response parsed correctly?

## Phase 4 — Known TCT English Bug Patterns
Check these FIRST:
| Symptom | Root Cause | Fix |
|---------|-----------|-----|
| Speaking page 500 | Missing `.Include(v => v.SpeakingVideo)` | Add Include |
| Goal not updating | UserId parsed as string | Use `int.TryParse()` |
| Admin block not persisting | AutoUnlockWorker + null LockExpiry | Set LockExpiry correctly |
| SignalR not receiving | Hub method case mismatch | Match exact case |
| Seeder duplicating | Missing guard condition | Add `AnyAsync()` check |

## Phase 5 — Minimal Fix
Fix ONLY the root cause. Add comment: `// FIX: [reason]`. No refactoring.

## Phase 6 — Verify
Reproduce original bug → apply fix → confirm resolved → check regression → test edge cases.

## Phase 7 — Document
Add to known issues, commit with `fix(<scope>): <what>` message.
