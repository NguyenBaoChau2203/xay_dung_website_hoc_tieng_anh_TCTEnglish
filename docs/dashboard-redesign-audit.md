# Learner Dashboard Redesign — Codebase Audit

**Date:** 2026-11-24  
**Scope:** `/Home/Index` presentation layer only  
**Approach:** Evolve-in-place (Approach 1)

## 1. File map

| Role | Path |
| --- | --- |
| View | `TCTEnglish/Views/Home/Index.cshtml` |
| Challenge partial | `TCTEnglish/Views/Home/_DailyChallenge.cshtml` |
| Page CSS | `TCTEnglish/wwwroot/css/Dashboard.css` |
| Float JS | `TCTEnglish/wwwroot/js/dashboard-float-card.js` |
| ViewModel | `TCTEnglish/ViewModels/DashboardViewModel.cs` |
| Controller | `TCTEnglish/Controllers/HomeController.cs` (`Index`, `BuildDashboardViewModelAsync`, `CheckAnswer`, `RefreshDailyChallenge`) |
| Tokens | `TCTEnglish/wwwroot/css/tct-learning-ui.css` (`.tct-theme`, `--tct-*`, `--tct-clay-*`) |
| Design system | `DESIGN.md`, `docs/design-system/team-ui-workflow.md` |

**Out of scope for this redesign:** controller business logic, ViewModel shape, DB/migrations, `Program.cs`, shared foundation CSS palette, new routes/libraries.

## 2. Existing IA (already matches brief order)

1. Greeting + XP chip + Role badge  
2. Learning Command (col-lg-8) + Streak (col-lg-4)  
3. Goals band  
4. Stats strip (Thẻ / Bộ / Folder + vocab memory bar)  
5. Weekly time chart (col-lg-7) + 28-day heatmap (col-lg-5)  
6. Quick Access + Daily Challenge  
7. Continue Reading (conditional on `RecentInProcessReading`)  
8. `_OnboardingHost`

## 3. ViewModel fields vs UI

| Field | Rendered | Notes |
| --- | ---: | --- |
| `FullName`, `BusinessHour` | Yes | Time-based greeting |
| `TotalXp`, `TodayXp` | Yes | XP chip only (not duplicated in stats) |
| `Role` | Yes | `.tct-badge` |
| `Streak`, `StudyDays` | Yes | Week dots from goals snapshot |
| `Goal`, `CardsReviewedToday` | Yes | Command progress when Goal > 0 |
| `TodayFolders` | Yes | Primary + up to 2 secondary chips |
| `ActiveGoalCount`, `MetGoalCount`, `DailyGoalPercent`, `DailyGoalSummary` | Yes | Goals band |
| `CardCount`, `SetCount`, `FolderCount` | Yes | Stats |
| `MasteredCount`, `LearningCount`, `NewCount` | Yes | Memory bar + float |
| `FeatureTimeBreakdown` | Yes | Stacked segments + float `data-*` |
| `HeatmapCells` | Yes | Intensity 0–3 from `ReviewedCards` |
| `DailyChallenge` | Yes | Partial + `/Home/CheckAnswer`, `/Home/RefreshDailyChallenge` |
| `RecentInProcessReading` | Yes | Title, Level, Topic, CTA — **no** thumbnail or % in VM |

## 4. Floating data contract (pre-redesign)

Single `#dbFloatCard` host. Triggers: `[data-db-float]` with kinds `weekly` | `vocab` | `heatmap`.

- **weekly:** `data-day`, `data-date`, `data-skill`, `data-minutes`, `data-total`, `data-accent`  
- **vocab:** `data-label`, `data-count`, `data-total`, `data-pct`, `data-accent`  
- **heatmap:** `data-day`, `data-date`, `data-week`, `data-reviewed`, `data-intensity`, `data-intensity-label`, `data-accent`

All values come from the ViewModel at render time. No fabricated metrics.

## 5. Challenge endpoints (preserve)

- POST `/Home/CheckAnswer` — antiforgery, `selectedCardId`, `challengeToken` → JSON  
- GET `/Home/RefreshDailyChallenge` → HTML partial  
- Inline script on Index binds `.answer-btn` inside `#daily-challenge-content`

## 6. Tico 2D assets available

Under `TCTEnglish/wwwroot/brand/mascot/`:

| Asset | Planned use (owner decision A) |
| --- | --- |
| `poses/tico-mascot-welcome-color.png` | Greeting only |
| `poses/tico-mascot-thinking-color.png` | Challenge pending (term + options) |
| `poses/tico-mascot-success-color.png` | Challenge completed |
| Other poses/stickers | Unused on dashboard (avoid mascot soup) |

No Reading/Headphones poses exist; those sections stay without mascot. 3D welcome was removed; do not restore.

## 7. Gaps addressed by this redesign

- No static Tico on Greeting / Challenge  
- Float is single-tier (hover = card); brief requires Quick Tooltip + Detail Card  
- Quick Access duplicates Vocabulary; Goals missing  
- Heatmap hover scale slightly above brief max (1.06 → cap 1.04)  
- Mascot must remain immune to parent hover transforms  

## 8. Constraints (non-negotiable)

- No mock XP / streak / chart / goals / progress  
- No ViewModel or controller calculation changes  
- No new chart/tooltip/motion libraries  
- No mascot animation (CSS/JS/GIF/video)  
- Continue Reading: no invented progress or thumbnail  
- Inter + `--tct-*` only; no page-level `:root` palette; no Baloo/Comic Neue  
