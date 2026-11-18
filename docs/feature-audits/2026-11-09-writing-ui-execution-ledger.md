# Writing UI/UX — Execution Ledger (Full Module Redesign)

## Overview & Context

- **Task**: Complete UI/UX research, visual design, local implementation, and verification for the entire Writing module (all 3 learner surfaces + all 3 Admin surfaces + partials + scripts + assets).
- **Date**: 2026-11-09 (Asia/Bangkok)
- **Mode**: `IMPLEMENT_LOCAL` (one agent investigating, designing, implementing, reviewing, and verifying inline; no workers/delegation).
- **Branch**: `feature/chau-writing-clay-ui`
- **Baseline Git Check**: On branch `feature/chau-writing-clay-ui`, based on `1617ee9` (prompt commit on top of Goals `a9acf69`). Working tree has preserved unrelated `docs/superpowers/plans/2026-11-08-bth3-class-report.md`.
- **Target Screens**:
  - WR01: `/Home/Writing` (`TCTEnglish/Views/Study/Writing.cshtml`)
  - WR02: `/Home/Writing/Exercises` (`TCTEnglish/Views/Study/WritingExercises.cshtml`)
  - WR03: `/Home/Writing/Practice?exerciseId=...` (`TCTEnglish/Views/Study/WritingPractice.cshtml`)
  - WA01: `/Admin/WritingExerciseManagement` (`TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Index.cshtml`)
  - WA02: `/Admin/WritingExerciseManagement/Create` (`TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Create.cshtml`)
  - WA03: `/Admin/WritingExerciseManagement/Edit/{id}` (`TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Edit.cshtml`)
  - Admin Partials: `_ExerciseForm.cshtml`, `_ExerciseFormScripts.cshtml`, `_AcceptedAnswersPanels.cshtml`
  - Scoped CSS: `writing-index.css`, `writing-exercises.css`, `writing.css`, `admin-writing-exercise.css`
  - Scoped JS: `writing-index.js`, `writing-exercises.js`, `writing.js`
  - Visual Assets: `TCTEnglish/wwwroot/images/writing/`

---

## 1. Baseline Verification & Fact Ledger

### Server & ViewModel Contracts Verified
- **WR01 (Landing)**:
  - Route: GET `/Home/Writing?level=...` -> `StudyController.Writing(string? level)`.
  - ViewModel: `TCTEnglish.ViewModels.WritingIndexViewModel`.
  - Level selection triggers dynamic client URL update, description update, and push/replace state.
  - Bento layout: Continue learning card + stats column (streak, words written) + level selection cards + content topic cards.
- **WR02 (Catalog)**:
  - Route: GET `/Home/Writing/Exercises` -> `StudyController.WritingExercises(...)`.
  - ViewModel: `TCTEnglish.ViewModels.WritingExerciseListViewModel`.
  - Filter state: `level`, `contentType`, `topic`, `status`, `page`, and `myPage` (independent pagination for personal exercises).
  - Authenticated section: "Bài viết của tôi" with AI generation form POST `/Home/Writing/Exercises/CreateFromAi` (`SourceText`, `IdempotencyKey`, antiforgery).
  - Deletion: POST `/Home/Writing/Exercises/Delete` (`exerciseId`, filters, antiforgery).
  - Locked state (`IsMyExercisesLocked`): Clear banner and disabled practice/delete controls.
  - Empty state when no system exercises match current filters (`writing-empty-search-clay.svg`).
- **WR03 (Practice Workspace)**:
  - Route: GET `/Home/Writing/Practice?exerciseId=...` -> `StudyController.WritingPractice(...)`.
  - ViewModel: `TCTEnglish.ViewModels.WritingPracticeViewModel`.
  - Data attributes: `data-owner-user-id`, `data-exercise-id`, `data-resume-sentence-id`, `data-hint-url`, `data-evaluate-url`.
  - Namespaced local drafts: `wp_draft_u{owner}_ex{exercise}_{sentence}`. Legacy unowned drafts ignored.
  - Endpoints: GET `/Home/Writing/Practice/Hint` and POST `/Home/Writing/Practice/Evaluate`.
  - Evaluation statuses: `Accepted`, `NeedsRevision`, `NeedsReview` (must display "Cần xem lại", never "Làm lại").
  - Auto-advance on acceptance, input retention on retry, keyboard shortcut `Ctrl+Enter`.
  - All 24 DOM elements in `PRACTICE_IDS` preserved strictly.
- **WA01 (Admin Catalog)**:
  - Route: GET `/Admin/WritingExerciseManagement` -> `Areas/Admin/Controllers/WritingExerciseManagementController.Index(...)`.
  - ViewModel: `TCTVocabulary.Areas.Admin.ViewModels.WritingExerciseIndexViewModel`.
  - Filters: `search`, `level`, `contentType`, `topic`, `visibility`.
  - Pager: `<partial name="_CatalogPager" model="Model" />`.
  - Actions: Edit and Delete (modal `#deleteExerciseModal` with form `#deleteExerciseForm`).
- **WA02 & WA03 (Admin Create / Edit)**:
  - Routes: GET/POST `/Admin/WritingExerciseManagement/Create` and `/Admin/WritingExerciseManagement/Edit/{id}`.
  - ViewModels: `WritingExerciseCreateViewModel`, `WritingExerciseEditViewModel`, `WritingExerciseFormViewModel`.
  - Full Text auto-split: Client preview in `_ExerciseFormScripts.cshtml` detects sentence boundaries (`.`, `!`, `?`, `;`, `:`) and paragraph breaks, pairs Vietnamese with English sentences, and reports mismatch warnings.
  - WA03 Approved Answer Variants: `_AcceptedAnswersPanels.cshtml` with independent per-sentence forms submitting to `SaveAcceptedAnswers` (`ExerciseId`, `SentenceId`, `ExpectedStateToken`, `Answers[0..4]`). Never nested inside bulk edit form.

---

## 2. UI/UX Research & Layout Decisions

### Reference Analysis & Best Practices
- **Design System**: Shared Claymorphism (`DESIGN.md`, `tct-learning-ui.css`).
  - Colors: TCT Blue `#4255ff`, Accent Gold `#ffcd1f`, Clay canvas `#f0f3ff`, Surface `#ffffff`, semantic emerald/amber/rose.
  - Clay Depth: `--tct-clay-shadow`, `--tct-clay-shadow-quiet`, `--tct-clay-shadow-lift`, `--tct-clay-shadow-button`, `--tct-clay-radius-panel` (24px), `--tct-clay-radius-control` (16px), `--tct-clay-radius-dialog` (28px).
  - Typography: Single `<h1>` per page, Inter 400/500/600/700, no 'Be Vietnam Pro', no competing `:root` palettes (`--wi-*`, `--we-*`, `--w-*` eliminated).
  - Laptop Priority (1440x900 & 1366x768):
    - In WR01: Bento layout with balanced aspect ratio, vector banner SVG replacing broken JPG (`writing-banner-clay.svg`).
    - In WR02: Compact toolbar filter, organized personal vs system cards, accessible search and status badges (`writing-status-badge`, `writing-attempt-badge`).
    - In WR03: Two-column grid (4.8 : 7.2) keeping prompt, active sentence, input textarea (220-260px), and submit action above the fold.
    - In WA01-WA03: Dense tabular views and structured two-column editor with sticky live preview sidebar.

---

## 3. Asset Provenance Ledger

| Asset | Path | Format | Size | Purpose & Provenance |
|---|---|---|---|---|
| Writing Banner Clay | `TCTEnglish/wwwroot/images/writing/writing-banner-clay.svg` | SVG | 5.6 KB | 3D clay desk scene (open notebook, pen, coffee mug, paper airplane). Replaces broken 404 image. Pure vector, crafted locally, no scripts/external refs. |
| Writing Quill Clay | `TCTEnglish/wwwroot/images/writing/writing-quill-clay.svg` | SVG | 3.2 KB | 3D clay fountain pen with gold nib and glowing ink trail. Pure vector, crafted locally, no scripts/external refs. |
| Writing AI Sparkle Clay | `TCTEnglish/wwwroot/images/writing/writing-ai-sparkle-clay.svg` | SVG | 3.5 KB | 3D clay document with magical AI wand and golden star sparkles. Pure vector, crafted locally, no scripts/external refs. |
| Writing Empty Search Clay | `TCTEnglish/wwwroot/images/writing/writing-empty-search-clay.svg` | SVG | 3.4 KB | 3D clay magnifying glass focusing on blank note. Pure vector, crafted locally, no scripts/external refs. |

---

## 4. Execution & Verification Checklist

- [x] Baseline facts, contracts, and tests verified.
- [x] UI/UX research conducted and Clay vector SVG assets crafted under `TCTEnglish/wwwroot/images/writing/`.
- [x] Implement WR01: `Writing.cshtml` and `writing-index.css` (clean Claymorphism, remove `--wi-*`, wrap in `.tct-theme`, single `<h1>`).
- [x] Implement WR02: `WritingExercises.cshtml` and `writing-exercises.css` (toolbar filter, personal AI panel, system catalog, remove `--we-*`, wrap in `.tct-theme`).
- [x] Implement WR03: `WritingPractice.cshtml` and `writing.css` (two-column workspace, feedback banner states, remove `--w-*`, wrap in `.tct-theme`).
- [x] Implement WA01: `Index.cshtml` (KPI stat cards, fast filter, table wrap, delete modal, wrap in `.tct-theme`).
- [x] Implement WA02 & WA03: `Create.cshtml`, `Edit.cshtml`, `_ExerciseForm.cshtml`, `_AcceptedAnswersPanels.cshtml`, `_ExerciseFormScripts.cshtml`, `admin-writing-exercise.css`.
- [x] Automated tests: Verify existing Node regressions + add meaningful writing UI tests + dotnet tests.
  - `scripts/tests/test_writing_draft_isolation.cjs`: 4/4 passed.
  - `scripts/tests/test_writing_assessment_status.cjs`: 2/2 passed.
  - `scripts/tests/test_writing_ui.cjs`: 8/8 passed (including Motion & Micro-interactions test).
  - Total Node tests: 14/14 passed.
  - `TCTEnglish.Tests/WritingUiRenderTests.cs`: 5/5 passed.
  - Full suite `dotnet test --filter "FullyQualifiedName~Writing"`: 169 passed, 7 skipped, 0 failed.
- [x] Motion & Micro-interactions (Shared Claymorphism Standard):
  - WR01: Staggered page entrance (`wp-fade-up`), floating vector illustration (`wp-banner-float`), level card active selection feedback (`wp-level-select`, `wp-copy-fade`), topic card icon tilt and CTA slide.
  - WR02: Staggered exercise card entrance (`wp-card-in`), hover elevation and active button slide, AI message slide-in (`wp-message-in`), submit tactile feedback.
  - WR03: Feedback banner entrance (`wp-banner-enter`), celebratory pop on Accepted (`wp-celebrate-pop`), alert shake on revision/error (`wp-subtle-shake`), hint pulse (`wp-hint-pulse`), rotating draft indicator icon, smooth progress bar fill.
  - WA01-WA03: Table row hover tint, auto-split sentence pair entrance (`wp-pair-in`), tactile clay button press across forms.
  - Accessibility: Full compliance with `@media (prefers-reduced-motion: reduce)` across all 4 scoped CSS files.
- [x] Update adoption tracker in `docs/design-system/team-ui-workflow.md` (Row 152 marked as 'Đã chuyển code (sửa theo review)').
- [x] Final verification: `git diff --check` clean, 0 compiler warnings introduced, implementation left uncommitted on `feature/chau-writing-clay-ui`.
- [x] Full-bleed Clay Canvas coverage fix: Applied `.main:has(...)` and `margin: calc(-1 * var(--tct-main-pad, 40px))` negative margin expansion across `writing-index.css`, `writing-exercises.css`, and `writing.css` (matching `goals.css` and `account-ui.css`), eliminating `#f8fafc` padding gutters and seamlessly filling the entire viewport with `var(--tct-clay-canvas, #f0f3ff)`. Verified on live local server `http://localhost:5100/Home/Writing`.
- [x] Distinct 3-Level Clay Palettes: Differentiated the 3 level cards on `/Home/Writing` with cohesive Claymorphism palettes:
  - **Beginner (Cơ bản)**: Emerald Green (`#10b981`), `fas fa-seedling` icon, soft green background `#ecfdf5`, green badge tag, emerald gradient on selection.
  - **Intermediate (Trung cấp)**: Warm Amber / Gold (`#f59e0b`), solid `fas fa-star` icon, warm amber background `#fffbeb`, amber badge tag, amber-gold gradient on selection.
  - **Advanced (Nâng cao)**: Royal Violet (`#8b5cf6`), `fas fa-crown` icon, royal violet background `#f5f3ff`, purple badge tag, royal violet gradient on selection.
- [x] Symmetrical 3-Column Topic Grid: Adjusted `.writing-content-grid` in `writing-index.css` to `grid-template-columns: repeat(3, minmax(0, 1fr))`, ensuring exactly 3 cards on the top row and 3 cards on the bottom row (3 x 2 grid), aligning symmetrically with the 3 level cards above.

## 5. Owner-authorized local commit checkpoint — 2026-11-09

- Chau accepted the current Writing work and explicitly requested its commit
  before the Speaking handoff. This checkpoint records the existing implementation;
  it does not certify a new end-to-end browser acceptance.
- Fresh command: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter 'FullyQualifiedName~Writing|FullyQualifiedName~SharedUiPartialsRenderTests'`
  with TRX output: **177 passed, 7 skipped, 0 failed**. The skipped live/provider
  and MariaDB opt-in checks were not run or counted as passed. This command built
  the application and test project; no paid/live flags were enabled.
- Fresh command: `node --test scripts/tests/*.cjs`: **106 passed, 0 failed**.
  The CSS/keyframe assertions are static checks, not evidence of smooth rendered
  animation, full reduced-motion behavior or browser accessibility.
- The existing `WritingService.cs` diff changes presentation colors and one
  icon only; assessment, persistence, authorization and schema are unchanged.
- Some Writing level variants still use feature-specific color literals and the
  landing illustration has a continuous floating animation. These are existing
  implementation choices, not new shared design tokens or full DESIGN.md
  conformance evidence. The Speaking handoff must use shared tokens and avoid
  perpetual decorative animation.
- Local verification logs are ignored under `.appdata/local/test-results/`.
  The unrelated untracked `docs/superpowers/plans/2026-11-08-bth3-class-report.md`
  remains outside the Writing commit. No push, remote CI, merge or deployment is
  authorized by this checkpoint; production configuration is untouched.
