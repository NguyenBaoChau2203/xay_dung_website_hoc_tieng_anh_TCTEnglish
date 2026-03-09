---
description: Full PR review checklist covering security, correctness, performance, and architecture
---

# Workflow: Code Review — Pull Request Process

## Phase 1 — Context
Understand: purpose of change, files changed, expected user flow, roles involved.

## Phase 2 — Security Review (🔴 Priority 1)
- Anti-IDOR: ownership check on all parameterized queries
- CSRF: `[ValidateAntiForgeryToken]` on all POST/DELETE
- Auth: `[Authorize]` on sensitive actions, Admin area protected
- File upload: MIME type + extension + size validated

## Phase 3 — Correctness (🔴 Priority 2)
- All `FirstOrDefaultAsync()` results null-checked
- No `.Single()` unless expecting exactly one row
- Navigation properties `.Include()`-ed before access
- Async chain consistent (no sync calls in async context)

## Phase 4 — Performance (🟡 Priority 3)
- No LINQ queries inside loops (N+1)
- `.AsNoTracking()` on read-only queries
- List views have pagination
- Search/filter in LINQ, not in-memory

## Phase 5 — Architecture (🟡 Priority 4)
- Controller ≤25 lines logic
- No ViewBag for complex data
- New services registered in `Program.cs`

## Phase 6 — Code Quality (🟢 Priority 5)
- Naming conventions followed
- No `Console.WriteLine()` in production
- Bootstrap 5 classes used

## Report Template
```markdown
## Code Review: [Target]
### 🔴 CRITICAL | 🟡 WARNINGS | 🟢 SUGGESTIONS | ✅ What's Done Well
### Verdict: APPROVED / CHANGES REQUESTED / NEEDS DISCUSSION
```
