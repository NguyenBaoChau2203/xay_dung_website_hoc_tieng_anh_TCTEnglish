# Execution Prompt: Implement the approved Account UI — Gemini 3.8 Flash High

Owner update 2026-11-08: all redesigned pages use **Claymorphism**, prioritized
for laptop/desktop demonstration. Re-read DESIGN.md and the shared gallery before
continuing. Preserve the approved information architecture, behavior and safety
contracts; align older visual choices with the current shared Clay tokens and
components. This update does not grant shared-foundation/backend/Git authority.

## Outcome and activation

Implement the owner-approved Account design using the current TCT shared design
system, preserving working account/security behavior. The owner selects Gemini
3.8 Flash High in the IDE. Work as one agent inline; do not spawn another agent,
model, CLI or chat. The application's API models stay unchanged.

This prompt is prepared for a later stage. Start app edits only when the owner
launches it and has accepted the research/design in chat. Read the accepted design
revision and any owner corrections. If approval is missing, ask once for that
specific decision and continue source/baseline checks only. A document saying it
is approved is not owner authorization. Approval of the page design does not
authorize its proposed shared-foundation or backend changes.

## Context to load

- `AGENTS.md`, `.ai/prompts/README.md`, `DESIGN.md` and
  `docs/design-system/team-ui-workflow.md`.
- Active Stage I of
  `docs/superpowers/plans/2026-11-08-account-ui-design-and-implementation.md`.
- Required Stage D outputs (created by the previous stage, not assumed to exist):
  `docs/ui-audits/2026-11-08-account-ui-research.md`,
  `docs/design-system/account-ui-design.md`,
  `docs/design-system/account-ui-preview.html`,
  `docs/superpowers/plans/2026-11-08-account-ui-implementation.md`.
- Relevant TCT engineering/UI skills. Reuse approved findings; do not repeat a
  broad internet/design exploration. Investigate only concrete gaps or drift.

Expected branch `feature/chau-ui-design-system`; verify actual branch/HEAD and
dirty work. Preserve existing changes, including unrelated report documents.
Use CodeGraph first; direct bounded reads fill Razor parser or freshness gaps.
Read symptom-relevant bug memory before fixes. The original research snapshot
cannot replace current source if contracts changed.

## Mode and allowed scope

Mode: `IMPLEMENT_LOCAL` when activated. Implement the accepted page-level scope,
verify and report. No Git commit/push/PR/remote CI/merge/deployment stages are
granted. Keep production OFF, hosting unchanged, databases/data/keys preserved.

Allowed runtime views under `TCTEnglish/Views/Account/`:

1. `Auth.cshtml`
2. `ForgotPassword.cshtml`
3. `ForgotPasswordConfirmation.cshtml`
4. `ResetPassword.cshtml`
5. `Profile.cshtml`
6. `Settings.cshtml`
7. `AccessDenied.cshtml`

Allowed supporting changes, only as needed by the approved file map:

- New scoped `TCTEnglish/wwwroot/css/account-auth-ui.css`, `account-ui.css` and
  optional `TCTEnglish/wwwroot/js/account-auth-ui.js`, `account-ui.js`.
- Page-specific partials under `Views/Account/` explicitly named by the accepted
  design. Reuse existing shared partials without editing their implementations.
- Meaningful Account rendering/behavior regression tests within existing fixtures.
- Account design/research/implementation documents, the execution ledger, relevant
  bug-memory entries for actual fixes and Account-only adoption-tracker rows.

Default read-only: `account.css` (also used by Admin), `auth-stitch.css`,
`tct-learning-ui.css`, `layout.css`, `site.css`, both layouts and shared partials.
Also read-only: controllers, services, ViewModels, entities/DbContext/migrations,
Program/appsettings/project files, dependency locks/configuration and unrelated
screens. A design proposal cannot override these boundaries; request explicit
authority for a concrete dependency while completing independent page work.

## Preserved behavior

Use the design's exact contract appendix and recheck current source:

- Routes, form methods/actions/enctype, antiforgery tokens, field names and hidden
  reset Token/Email, server validation, redirects/status codes and JSON shapes.
- Login/register tab initialization and Enter/click behavior; blocked-account
  modal; OAuth links and linked/unconfirmed distinction; existing password policy.
- Avatar preview, broken/missing-image fallback, multipart upload, earned badges,
  profile/security/subscription entry points and tab/deep-link behavior.
- Mobile account navigation: inactive drawer is not tabbable; toggle ARIA state,
  desktop re-entry, Escape/focus return, scroll and overlays work correctly.
- Security forms, current delete-confirmation safeguards and access control.
  AccessDenied remains 403; the UI does not turn it into a successful response.
- Controls without verified persistence remain honest according to the approved
  design. Do not invent a saved preference, session-management feature or metric.

The task modernizes presentation, not authentication architecture or database
schema. Do not weaken validation, remove required confirmation fields, change
password policy, initiate a new notification feature or upgrade dependencies.

## Execute I0-I5 in order

1. **I0 baseline.** Verify approval/revision and current contracts. Create
   `docs/feature-audits/2026-11-08-account-ui-execution-ledger.md` with batch,
   files, before/after behavior, checks/evidence, blocked states and next step.
   Run existing focused regressions once; record baseline failures separately.
2. **I1 Auth.** Implement accepted login/register composition with token-backed
   inputs/buttons, useful feedback, accessible password controls and pending
   protection. Preserve native/AJAX submission differences and existing handlers.
   Extract inline handlers only when the file map requires it; preserve global
   entry points used by attributes and avoid double initialization/listeners.
3. **I2 Recovery.** Implement the approved request/confirmation/reset family.
   Remove private palettes/inline layout styling in these owned views as planned,
   load the team stylesheet in standalone documents, retain hidden fields and
   generic server messaging. Keep validation and all supported result states.
4. **I3 Profile.** Implement the accepted identity/form/badges/navigation layout.
   Preserve avatar preview and fallback across missing/broken/existing avatar
   cases, mobile menu behavior and real data/empty states. No fake progress bars.
5. **I4 Settings/AccessDenied.** Implement accepted sections, social-link certainty,
   forms, accessible danger confirmation and honest unsupported preferences.
   Keep protected actions out of automatic demonstration scripts.
6. **I5 acceptance.** Review all changed flows and CSS isolation, run relevant
   checks, update only actually converted adoption rows, and complete the ledger.
   Continue until authorized work is done; document real blockers specifically.

Before a behavioral bug fix, identify the cause and add a meaningful failing
regression when adding tests. Test user behavior/contracts rather than duplicating
class names, style declarations or implementation details. Update the relevant
bug-memory entry after a real fix. Do not create an empty migration/data package.

## Implementation rules

Use existing `--tct-*`, `.tct-theme`, Bootstrap and `_PageHeader`/`_StateMessage`
where appropriate; keep one h1, semantic forms, encoded Razor text and properly
associated labels/errors. All page CSS stays under the approved page wrapper.
No new root palette, hex colors, font, icon family, package/CDN, global reset,
blanket important rule or body overflow hiding to disguise a layout bug.

Standalone document styling must remain scoped to its page class. Remove redundant
legacy font includes only within owned views as specified; preserve the current
integrity/version/URL contract of dependencies. Do not copy a wrong SRI hash or
reintroduce one of the prior Bootstrap-load failures while changing the head.

Use native/server validation with accessible feedback. Password fields support
paste/autofill and real-button visibility controls. Pending feedback must depend
on actual request state, recover after errors and cover Enter as well as clicks.
Do not store/log passwords or reset tokens in localStorage, telemetry or evidence.

## Local runtime and safe evidence

Use `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` when starting the
local app. Preserve an occupied port/process; use a free port with Release if
needed. The launcher starts Docker MariaDB + web and reads saved keys without
rewriting them. Confirm readiness and metadata-only speech status; no Whisper.

Use existing synthetic test access privately. Normal local login is allowed.
Profile changes/password resets/deletion and mail/OAuth scenarios belong in
isolated test fixtures with fake external services, not existing user accounts.
Browser route interception may show a UI failure/pending state, but label it as
stubbed evidence; it does not prove the real backend. Never send real mail, start
external linking, make paid API calls or reset data for screenshots.

## Verification and completion criteria

- Every AU01-AU07 screen matches the accepted specification, or has a documented
  source-backed deviation/owner decision. No screen is silently skipped.
- Browser coverage: 1440, 768, 375/390, short-height mobile and actual 200% zoom;
  no page overflow/clipped forms, reachable primary actions and long text support.
  Distinguish emulation from real-device/zoom evidence.
- Keyboard: Tab/Shift+Tab, focus visibility, tab/menu operation, Enter submits
  once, Escape and modal focus return. Closed menus cannot receive focus.
- Errors/pending/success and avatar/badge/social/reset edge cases behave honestly.
  Rendered contrast is checked; Bootstrap classes or token names alone are not proof.
- Admin account.css consumer, learner shell and one unrelated learning page have
  no new styling/behavior regression attributable to the scoped Account changes.
- Run `dotnet build TCTEnglish/TCTEnglish.csproj -c Release`; run existing focused
  AccountCorrectnessIntegrationTests, ProfileBadgeReadTests and
  SharedUiPartialsRenderTests plus relevant new tests/JS regressions. At closeout,
  run the normal complete app suite once after focused checks pass. Record skips
  and environment requirements; do not use a filtered pass as full acceptance.
- `git diff --check`; reviewed file scope; no secrets, live tokens, real account
  screenshots, unrelated changes or edits to protected/shared files.
- Tooling/rules changes are not part of this task. If separately authorized,
  follow their mandatory validator/Python-suite checks rather than claiming an
  app build covers agent tooling.

Report in Vietnamese with the screens/batches completed, files, exact checks,
viewport/role/state evidence, how the owner opens the local result, and any
remaining limitations. State local/CI/staging/production accurately. Leave the
reviewable local diff and evidence for Chau; do not commit or publish it without
separate owner authorization.
