# Claymorphism shared foundation and agent alignment

Owner decision: 2026-11-08. Every redesigned TCT English page uses Claymorphism,
with laptop/desktop presentation prioritized for the classroom demonstration.
Branch: `feature/chau-ui-design-system`, baseline `eed4e2c` plus existing Account
work. One agent implements/reviews this foundation; existing Account changes and
unrelated reports are preserved. No Git/release/production authority is implied.

## Scope and source evidence

- Canonical visual specification: DESIGN.md. Shared code: tct-learning-ui.css;
  examples: docs/design-system/preview.html. All four IDEs read AGENTS.md.
- The shared stylesheet is loaded by both application layouts and directly by
  standalone Account pages. Existing `.tct-theme`, `.tct-card`, toolbar, state,
  table wrapper and Bootstrap bridges are the integration points.
- The root legacy tokens still serve Auth/Listening and other unconverted pages.
  Preserve their values. Introduce Clay tokens and scoped aliases in `.tct-theme`
  so adopting pages share one implementation without repainting every legacy view.
- Existing Account view/CSS/JS/test files are dirty and belong to another active
  implementation. Do not rewrite them for this foundation task. A visual-only
  supersession notice in design/checklist documents preserves their contracts.
- Scope includes the shared CSS, gallery, canonical design/rules, current page
  prompt and current Account prompt/design entry points. No database, API, app
  configuration, layout geometry, fonts, packages or CDN changes.

## Design decisions and consumers

- Retain TCT blue/yellow, Inter and readable semantic colors. Add airy pastel
  canvas and raised surfaces with one outer and two inset shadows.
- Rounded containers and controls share new Clay shape/depth tokens. Keep fields,
  prose, video/transcripts and table rows clear, using subtle inset fields and
  raised containing panels. Borders/focus convey interaction independently of
  shadows; no shadow-only control boundary or low-contrast pastel text.
- Clay is mandatory for redesigns across Auth/Account, all learning modules,
  Goals, AI Chat, dashboard, marketing, utility pages and Admin. Page-specific
  composition remains flexible. More restrained depth on dense pages is still
  the same design language, not a separate flat theme.
- Keep geometry and default legacy token values stable. No automatic body-level
  theme switch or route/business changes. Existing unconverted pages require their
  owners to migrate markup/CSS; a foundation change is not full-site completion.
- Consumers to check: shared gallery, current seven Account pages, Bootstrap
  bridge/Admin patterns, learner shell, root legacy tokens, focus/validation,
  reduced motion and forced-colors behavior.

## Work and evidence

- [x] Inspect existing foundation, current Account changes and source consumers.
- [x] Look up installed UX Clay guidance and primary clay.css author reference.
      Use concepts only; do not install/copy the external library or its claims
      about automatically solving accessibility.
- [x] Capture browser before metrics at 1440x900: card18px/one outer shadow;
      primary button12px/no shadow; zero token contrast failures/viewport overflow.
- [x] Implement shared Clay tokens/components and update the live gallery.
- [x] Update canonical rules and current execution entry points; clearly supersede
      only older visual decisions, preserving prior behavior/approval boundaries.
- [x] Verify gallery across laptop/tablet/mobile, focus/interaction, contrast,
      reduced motion and scoped legacy isolation. Check actual Account integration
      where a current local app is available, without claiming unrelated fixes.
- [x] Run build/relevant UI rendering tests, mandatory tooling/script checks and
      diff/preservation review. Record results and unobserved application screens.

References consulted 2026-11-08:
[clay.css author repository](https://github.com/codeAdrian/clay.css/), and installed
`tct-ui-ux-pro-max` style lookup for Claymorphism. No external dependency imported.

Screenshot export through the Chrome MCP filePath parameter was denied because
that server's configured output roots do not include this checkout. Use available
inline screenshots and browser measurements; do not alter its trust/root config.

## Verification results and limits

- Actual shared gallery, Chrome: 1440x900, 1366x768, 768x1024, 390x844 and375x812
  had no horizontal viewport overflow. A720x450 reflow check also passed; it is
  not a native200% browser-zoom acceptance. The zoom shortcut did not alter the
  emulated viewport/device ratio, so actual browser zoom remains unverified.
- Raised card24px, control16px and modal28px radii; browser-computed composites
  include the expected outer/two-inset shadows. Quiet panels and shallow fields
  use the separate shared variants. All19 effective-theme token contrast pairs
  in the gallery passed. This is not a claim about every legacy rendered color.
- Keyboard Tab reached the field with a visible2px outline. Pending state sets
  disabled/aria-busy and recovers. After Bootstrap's actual shown/hidden events,
  Escape closed the modal and returned focus. Reduced-motion focus-within had
  no transform and near-zero transition; forced colors removed shadows and
  retained system-color field borders.
- A browser fixture outside the theme retained card18px/old single shadow.
  A root-token comparison against the before snapshot found no changed existing
  values. Only new Clay tokens and scoped aliases supply the new foundation.
- `dotnet test` Release built the current app/test projects and passed27 focused
  Account/UI/badge/shared-partial tests. The complete Node script suite passed
  90/90. Tooling validator passed. Python suite:139 tests,138 passed/1 skipped/
  0 failed,132.512s. No full application suite or authenticated user-flow claim.
- Account implementation was concurrently active: a14-file hash snapshot later
  detected an external Auth.cshtml change; auth-stitch.css/images also appeared
  as new external work. This task did not edit those runtime Account files or
  reset/revert them. The focused tests describe their run, not acceptance of
  later changes by that agent.
- Existing localhost:5100 process was preserved. Its anonymous login served an
  older unthemed Auth document using auth-stitch.css, not the current new Account
  assets. This inspection confirms why that process cannot certify the latest
  Account redesign. No process restart, account mutation, secret access, API/mail
  call, database change, commit, push or deployment was performed by this task.
- Fresh sessions read current AGENTS.md/DESIGN.md. Already-running agents should
  execute `.ai/prompts/adopt-claymorphism-for-current-ui-task.md` within their
  existing mode/screen scope. All legacy pages remain independently pending
  until each owner migrates and verifies them; do not mark the whole site done.
