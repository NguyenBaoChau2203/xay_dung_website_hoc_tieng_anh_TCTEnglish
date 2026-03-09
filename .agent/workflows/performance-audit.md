---
description: Profile and optimize application performance across database, server, and frontend layers
---

# Workflow: Performance Audit

## Phase 1 — Identify the Bottleneck
Never optimize blindly. Measure first.
- Browser DevTools → Network tab: total load time, largest files, slowest requests
- EF Core SQL logging: `Microsoft.EntityFrameworkCore.Database.Command: Information`
- `query.ToQueryString()` to inspect generated SQL

## Phase 2 — Database Optimization (Highest Impact)

### N+1 Queries
```csharp
// BAD: var sets = await _context.Sets.ToListAsync(); // then loop accessing Cards
// GOOD: var sets = await _context.Sets.Include(s => s.Cards).AsNoTracking().ToListAsync();
```

### Missing `.AsNoTracking()` — all read-only queries need it

### SELECT Projection — use `.Select(s => new ViewModel { ... })` instead of loading full entities

### Database Indexes — check high-traffic columns: `Sets.OwnerId`, `LearningProgress.UserId`, `SpeakingVideo.Level`

### Pagination — any list without `.Skip().Take()` is a performance risk

## Phase 3 — Server-Side Optimization
- Response caching for public/rarely-changing pages
- Remove sync blocking: no `.Result` or `.Wait()`
- Background work offloading for non-critical tasks (email)

## Phase 4 — Frontend Optimization
- Move page-specific JS to `@section Scripts { }` (not global `_Layout.cshtml`)
- Debounce search inputs (300-500ms)
- AJAX instead of full page reload for single-item updates
- Lazy load media content (YouTube players)

## Phase 5 — Audit Report
```markdown
## Performance Audit: [Target]
| # | Layer | Issue | Severity | Expected Impact |
### Optimization Plan (ordered by impact)
### After Metrics (fill after applying fixes)
```
