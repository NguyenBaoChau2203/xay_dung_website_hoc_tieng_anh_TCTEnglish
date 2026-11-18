# Goals UI/UX — Execution Ledger (Stage I)

## Overview & Context

- **Task**: Complete Goals UI/UX research, design and local implementation in one execution.
- **Date**: 2026-11-08 / 2026-11-09 (Asia/Bangkok)
- **Mode**: `IMPLEMENT_LOCAL` (one agent investigating, designing, implementing, reviewing and verifying inline).
- **Branch**: `feature/chau-goals-clay-ui`
- **Baseline Git Check**: Clean working tree on `feature/chau-goals-clay-ui` based on `fc01d91`.
- **Target Screens**:
  - `/Goals` and `/Goals/Index` (`TCTEnglish/Views/Goals/Index.cshtml`)
  - Scoped styling: `TCTEnglish/wwwroot/css/goals.css`
  - Client interaction: `TCTEnglish/wwwroot/js/goals.js`
  - Purposeful visual assets: `TCTEnglish/wwwroot/images/goals/`

---

## 1. Baseline Verification & Fact Ledger

### Server & ViewModel Contracts
- **Route**: GET `/Goals` & `/Goals/Index` -> `GoalsController.Index()` returns `GoalsViewModel`.
- **Form Submission**: POST `/Goals/UpdateGoal` bound with prefix `[Bind(Prefix = nameof(GoalsViewModel.GoalEditor))] UpdateGoalInputViewModel input`.
  - Required form field: `GoalEditor.TargetValue` (range `0`..`500` inclusive).
  - Target value `0` semantics: Skill goal is paused/inactive in computing daily progress average; does not wipe history.
  - Antiforgery: `[AutoValidateAntiforgeryToken]` enforced on controller; hidden token rendered on form.
  - Success: `TempData["SuccessMessage"] = "Đã lưu mục tiêu ngày."` -> Redirect to `/Goals`.
  - Validation error: `ReturnEditorViewAsync()` re-renders view with `ShowGoalEditor = true`, causing modal to auto-open via `data-open-on-load="true"`.
- **Supported Goal Areas**:
  - All 5 skill areas are active with real completion signals in `GoalsService`:
    1. `Vocabulary`: unit = `thẻ` (Cards reviewed today)
    2. `Speaking`: unit = `video` (Speaking video completion)
    3. `Writing`: unit = `bài` (Writing exercise completion)
    4. `Reading`: unit = `bài` (Reading completion)
    5. `Listening`: unit = `bài` (Listening completion)
- **Activity & Metrics**:
  - `WeeklyActivity`: 7-day counts of **Vocabulary cards reviewed** (`UserDailyActivities.CardsReviewed`). Label: "Ôn từ vựng hàng tuần" (honest semantic labeling, not generic study time).
  - `Streak`: Current streak and longest streak computed server-side with `ResolveBusinessDate()`.
  - `Badges`: Authoritative server evaluation based on real activity totals and milestones.

---

## 2. UI/UX Research & Layout Decisions

### Reference Analysis & Best Practices
- **Design System**: TCT English Claymorphism (`DESIGN.md`, `wwwroot/css/tct-learning-ui.css`).
  - Palette: TCT Blue `#4255ff`, Accent Gold `#ffcd1f`, Clay canvas `#f0f3ff`, Surface `#ffffff`, semantic emerald/amber/rose.
  - Elevation & Depth: `--tct-clay-shadow`, `--tct-clay-shadow-quiet`, `--tct-clay-shadow-lift`, `--tct-clay-shadow-button`, `--tct-clay-radius-panel` (24px), `--tct-clay-radius-control` (16px), `--tct-clay-radius-dialog` (28px).
  - Typography: Inter with weights 400, 500, 600, 700. Single `<h1>` per page.
  - Accessibility: WCAG AA contrast (text >= 4.5:1, controls >= 3:1), visible focus rings with offset, responsive down to 375px without horizontal scrolling, reduced motion support.
- **Laptop / Classroom Demo Priority (1440x900 & 1366x768)**:
  - Keep primary metrics and action triggers within the top viewport fold without excessive vertical scrolling.
  - Avoid heavy 3D runtimes or oversized decorative blocks that push learning data off-screen.

### Arrangement Comparison
- **Arrangement A (Vertical Stack)**: Sequential full-width cards.
  - *Drawback*: Excessive vertical height; badges and weekly chart pushed far below the laptop fold.
- **Arrangement B (Balanced Bento Overview & Unified Skill Hub - Selected)**:
  - Header: Single `<h1>` with `.tct-page-header`, motivating tagline, and primary CTA (`data-testid="goal-header-cta"`).
  - Row 1 (Hero Bento):
    - Col 1 (4 cols): **Streak & Momentum Card**: Current streak, best record, streak message, mini 7-day streak bar, Clay streak SVG illustration.
    - Col 2 (5 cols): **Today's Target & Progress Card**: Overall completion percentage with circular radial ring, skill progress bars, 7-day completion check dots, honest empty state if no active goals (`data-testid="goal-empty-state-cta"`).
    - Col 3 (3 cols): **Achievement Spotlight Card**: Badges unlocked counter, celebration banner for recently unlocked badges, next milestone goal with progress bar, Clay trophy SVG illustration.
  - Row 2 (Skill Goals Hub):
    - Dynamic responsive grid for all 5 skills (Vocabulary, Speaking, Writing, Reading, Listening) driven by `Model.GoalCards` and `Model.GoalAreaOptions`.
    - Each card: Clay surface, distinctive pastel skill badge, current/target numbers with real units, progress bar, completion badge, and direct link to start learning.
  - Row 3 (Weekly Activity & Badges Showcase):
    - Left (8 cols): **Ôn từ vựng hàng tuần** (Weekly Vocabulary Review Chart): Accessible 7-day bar chart with numbers, tooltips, keyboard focus, today highlight, and honest empty state (`chart-empty-state`) with CTA to start reviewing flashcards.
    - Right (4 cols): **Bộ sưu tập huy hiệu**: Badge count badge, Clay medal tiles for unlocked and locked badges, next goal preview.

---

## 3. Asset Provenance Ledger

| Asset | Path | Format | Size | Purpose & Provenance |
|---|---|---|---|---|
| Goal Target Clay | `TCTEnglish/wwwroot/images/goals/goal-target-clay.svg` | SVG | 3.8 KB | Bullseye target with layered concentric clay rings, arrow, and sparkles. Pure vector, crafted locally, no scripts/external refs. |
| Streak Flame Clay | `TCTEnglish/wwwroot/images/goals/streak-flame-clay.svg` | SVG | 3.5 KB | 3D clay flame with warm core and orange-red body. Pure vector, crafted locally, no scripts/external refs. |
| Badge Trophy Clay | `TCTEnglish/wwwroot/images/goals/badge-trophy-clay.svg` | SVG | 3.7 KB | Clay golden cup trophy with star emblem and pedestal. Pure vector, crafted locally, no scripts/external refs. |

---

## 4. Execution Progress Checklist

- [x] Baseline facts, contracts, and existing test suite verified.
- [x] UI/UX research conducted and Arrangement B selected.
- [x] Pure vector Clay SVG assets created under `TCTEnglish/wwwroot/images/goals/`.
- [x] Implement scoped Claymorphism styling in `TCTEnglish/wwwroot/css/goals.css`.
- [x] Implement enhanced view structure in `TCTEnglish/Views/Goals/Index.cshtml`.
- [x] Harden interaction, accessibility, and BFCache/double-submit prevention in `TCTEnglish/wwwroot/js/goals.js`.
- [x] Add meaningful automated UI rendering and interaction tests (`test_goals_ui.cjs` & `GoalsUiRenderTests.cs`).
- [x] Run full test suites and verify all contracts pass:
  - `GoalsUiRenderTests`: 6/6 passed (16 s)
  - Full Goals regression suite (`GoalsPhase1`–`7`, `ProfileBadgeReadTests`, `GoalsCompletionRecoveryTests`, `GoalsUiRenderTests`): 52/52 passed (32 s)
  - Node UI interaction suite (`scripts/tests/test_goals_ui.cjs`): 7/7 passed (4.12 s)
- [x] Redesign Goal Editor Pop-up Modal (#goalEditorModal) to full Claymorphism standard:
  - Added `.tct-theme` root cascade to modal dialog.
  - Clay dialog squircle container (28px radius, multi-layer outer & inset clay depth).
  - Modal Header: 3D Clay icon badge with target bullseye, subtitle, circular tactile clay close button.
  - Skill Select: Clay well with custom arrow and smooth focus ring.
  - Target Stepper: Tactile `[-]` and `[+]` clay stepper buttons (step by 5, clamped 0..500) and bold centered numeric display.
  - Dynamic Unit Badge: switches between `từ / ngày` (Vocabulary) and `bài / ngày` (other skills).
  - Quick Presets: 3D clay pills with `.is-active` primary highlight when value matches.
  - Tip Box: Pastel clay gradient card with lightbulb icon badge.
  - Footer Buttons: Tactile Clay cancel button and vibrant 3D Clay submit button with depress animation.
- [x] Update adoption row in `docs/design-system/team-ui-workflow.md` (Row 154 `/Goals` -> Đã chuyển code (sửa theo review), Owner: Chau).
- [x] Final report in Vietnamese (working tree kept uncommitted per owner instructions).

## Owner closeout and local commit checkpoint — 2026-11-09

The owner confirmed Goals was satisfactory, requested the next Writing UI task,
and explicitly asked to commit current completed work first. This checkpoint
covers the existing Goals view, scoped CSS/JS, three decorative SVGs, UI tests
and the Goals adoption row; no controller/service/schema/config change.

Fresh Release verification: 65 related Goals/badge/shared-UI tests passed,
0 skipped/failed; all 97 Node script tests passed. The ten candidate files were
checked for secret-like/private paths and the SVGs for active/external content;
no finding. These checks do not claim a new browser walkthrough or full app
suite run. The unrelated BTH3 plan remains preserved outside this commit.

This is a local commit only. No push, remote CI, PR, merge or deployment is
requested by this checkpoint. The owner-approved Writing work is prepared next.
