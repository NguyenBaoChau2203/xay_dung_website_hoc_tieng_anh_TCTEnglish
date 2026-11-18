# Multi-platform UI design (mobile + desktop)

Date: 2026-11-09. Owner-selected approach: standardize rules and design docs
first; then audit and fix one feature at a time. This spec does **not** redesign
the site in bulk.

Canonical values live in [DESIGN.md](../../../DESIGN.md) and
[`tct-learning-ui.css`](../../../TCTEnglish/wwwroot/css/tct-learning-ui.css).
Process and PR checklist:
[team-ui-workflow.md](../../design-system/team-ui-workflow.md).
Phase B execution checklist:
[2026-11-09-multi-platform-feature-audit.md](../plans/2026-11-09-multi-platform-feature-audit.md).

## Outcome

Every new or changed UI surface must work on desktop and mobile in the **same**
change set. Partial viewport work (“desktop now, mobile later”) is not done.

## Whole-feature rule

When building or editing UI for a feature, the change set must cover:

1. All routes, views, scoped CSS and JS belonging to that feature that are in
   scope for the task (and directly related domain surfaces listed in the
   workflow adoption tracker).
2. Desktop with the learner sidebar open and collapsed (when the feature uses
   the learner shell).
3. Mobile drawer/overlay behavior (≤768px) when the feature uses the learner
   shell; Admin uses its own collapse breakpoint (991.98px).
4. No horizontal page overflow; no clipped essential learning content; long
   Vietnamese and English text; loading / empty / error / success states;
   keyboard focus and Escape return-of-focus for dialogs/drawers.
5. Real-browser evidence at ~1440, 768 and 375/390, plus 200% zoom. Record what
   was not observed.

## Breakpoints and shell (reference only)

Do not invent a second breakpoint system. Use DESIGN.md Layout:

| Viewport intent | Approx width | Shell behavior |
| --- | --- | --- |
| Desktop check | ~1440 | Sidebar 280px; `.main` and `.tct-footer` share left offset |
| Tablet sidebar | ≤1024 | Sidebar 240px; matching main/footer offset |
| Mobile drawer | ≤768 | Sidebar off-canvas; main/footer margin 0; `.sidebar.active` overlays |
| Compact header | ≤576 / ≤480 | Search placeholder / denser header |
| Mobile check | 375–390 | Same drawer rules; touch targets ≥44px (`--tct-target-min`) |

Admin collapses at 991.98px. Feature CSS must not hardcode 72 / 280 / 240;
consume `--tct-header-h`, `--tct-sidebar-w`, `--tct-sidebar-w-md`,
`--tct-main-pad` / `--tct-main-pad-sm`.

Footer sits outside `.layout` and must keep the same left offset as `.main`
when the desktop/tablet sidebar is open (sibling rule in `layout.css`).

## Required layout patterns

- Toolbars and action rows: stack or wrap below 768px; do not force a single
  non-wrapping horizontal strip that overflows.
- Data tables: `.tct-table-wrap` (focusable scroll) or Bootstrap
  `.table-responsive`; never a fixed-width table that forces page scroll.
- Side panels: become drawers/overlays or stacked sections on mobile; do not
  squash the main pane below a usable width.
- Modals/sheets: usable at 375px; primary actions remain reachable.
- Cards/grids: reflow with Bootstrap grid or auto-fit; avoid fixed multi-column
  widths that clip at 390px.
- Touch: icon-only and primary controls meet `--tct-target-min` (44px).

## Acceptance matrix (minimum)

| Check | Pass criteria |
| --- | --- |
| 1440 open sidebar | Content and footer clear of sidebar; no overlap |
| 1440 collapsed sidebar | Full-width main/footer; no leftover offset |
| 768 / 390 | No page-level horizontal overflow; drawer does not trap focus when closed |
| 200% zoom | Primary task still completable; essential text not clipped |
| Keyboard | Tab order logical; visible focus; Escape closes overlay and returns focus |
| States | Loading, empty, error, success (and pending submit) render |

A static gallery pass does **not** prove Razor rendering, RBAC or app JS.

## Phase A vs Phase B

| Phase | Scope |
| --- | --- |
| A | Spec, DESIGN.md, workflow, prompts, skills, gallery note, Phase B plan file. No mass feature redesign. |
| B | One feature (or shell) per authorized drop: inventory → browser audit → findings → fix whole feature → update tracker. |

Shared-impact files (`tct-learning-ui.css`, `layout.css`, `site.css`, layouts,
shared partials) need an explicit consumer list and before/after evidence.

## Non-goals

- Bulk token migration of every legacy screen in one change.
- Brand / primary color / root mobile font-size changes (proposals for Chau).
- Commit, push, PR, CI gate or deploy without owner stage authority.
