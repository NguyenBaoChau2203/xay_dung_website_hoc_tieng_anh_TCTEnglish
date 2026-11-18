# Execution Prompt: Repair Account UI review findings R1–R8

Owner visual update 2026-11-08: apply current DESIGN.md **Claymorphism** and laptop
demo priority when touching Account UI. Preserve the approved behavior and repair
all listed functional findings; styling is not a substitute for those repairs.
Use shared Clay tokens/components and keep the original file/authority boundaries.

## Outcome

Restore working recovery submission and responsive Account navigation, preserve
destructive/avatar safeguards, and finish the approved accessibility/recovery
states with evidence. Keep the owner-approved option B for AU01–AU07.

## Context and evidence

- Read AGENTS.md, DESIGN.md and the active batch of
  `docs/superpowers/plans/2026-11-08-account-ui-review-repair.md`.
- Findings, line references, reproduction and limitations:
  `docs/ui-audits/2026-11-08-account-ui-implementation-review.md`.
- Approved design/contracts: `docs/design-system/account-ui-design.md`.
- Existing implementation record:
  `docs/feature-audits/2026-11-08-account-ui-execution-ledger.md`.
- Review observed two P1 regressions despite green tests: duplicate recovery
  submit ownership cancels the first valid POST; disposing an open Offcanvas
  during mobile-to-desktop resize leaves the page scroll-locked.
- Review also reproduced pageshow enabling an empty delete confirmation, Cancel
  hiding a saved avatar, low-contrast unconfirmed status and missing approved
  focus/recovery states. It identified inaccurate ledger/test-scope claims.
- Review baseline: branch feature/chau-ui-design-system, HEAD eed4e2c; focused26
  and Node10 passed; normal suite1175 passed/22 skipped/0 failed. Recheck current
  source and worktree rather than assuming those counts certify a later change.

Use CodeGraph first for indexed source, bounded reads for actual omitted gaps;
no automatic indexing/rebuild. Load relevant TCT process/engineering/UI skills.
Read symptom-matched bug memory before fixing. Reference material and fixture
output do not override the owner's scope or authorize production.

## Mode, authority and scope

This prompt was prepared during REVIEW_ONLY. Do not implement merely because
the file exists or says a design is approved. Start IMPLEMENT_LOCAL only when
the owner explicitly asks to fix these findings or launches this repair prompt.
The original page-design approval is already recorded; do not ask to approve
option B again when repair execution has been authorized.

After activation, edit only the owned Account views/assets/tests and reporting
files listed in the repair plan. Shared CSS/layouts/partials, backend, ViewModels,
models, migrations, Program/appsettings/project/dependency files remain read-only.
No policy/persistence/auth/provider/subscription/brand change, new token/library,
or database reset. Preserve routes, binding names, antiforgery, reset hidden fields,
multipart upload, JSON/status/redirect contracts and real identity/badge data.

Work as one agent for diagnosis, implementation, review and verification. Do not
spawn another model/worker/reviewer/chat/CLI. Preserve current dirty files and
workloads, including unrelated DESIGN/report changes. No commit/push/PR/remote
CI/merge/deployment stage is granted. Keep hosting and release gates unchanged.

## Deliverables

- Minimal local fixes and meaningful failing-then-passing regressions for R1–R7.
- Corrected Account ledger and only the applicable adoption rows for R8.
- Evidence for each repair-plan batch, screenshots/logs under ignored `.tmp`,
  accurate bug-memory updates after actual fixes, and a Vietnamese final handoff.
- Explicit remaining browser/provider limitations without counting skipped work
  as pass. Do not declare all acceptance complete solely from green unit tests.

## Execution

1. Follow C0: verify owner repair authority, actual checkout, dirty-work hashes,
   current contracts and review evidence. Keep the approved design direction.
2. Follow C1/C2: add regressions and repair duplicate form ownership and open
   drawer breakpoint cleanup. Prove first-submit and scroll behavior in a browser.
3. Follow C3/C4: preserve CONFIRM across restoration and both modal scopes; restore
   the correct saved avatar on Cancel. Never submit deletion/reset/upload against
   existing accounts just to demonstrate the UI.
4. Follow C5/C6: fix semantic contrast, focus/targets/token use and the invalid
   reset return path using existing page-scoped components and ModelState.
5. Follow C7: strengthen test coverage, correct claims/contracts in the ledger,
   run relevant checks and the normal full suite once, review the diff inline.
6. Complete authorized fixes; isolate only genuinely missing backend/shared
   authority. Do not widen scope to the pre-existing shared jQuery dependency
   or start a new design/research cycle without a concrete necessary reason.

## Verification and acceptance

- Valid Forgot/Reset first submit creates one native POST; pending duplicates
  are blocked; invalid/mismatch forms stay editable; helpers/tokens unchanged.
- Drawer open→resize→desktop restores scroll, padding, overlay, ARIA, inert and
  focus correctly, and can return to mobile. Use real Bootstrap/browser behavior.
- Empty/lowercase CONFIRM stays disabled after open/reopen/pageshow; exact trimmed
  CONFIRM enables; cancel/escape send no POST; duplicate protection is scoped.
- Cancel preserves the original saved avatar or appropriate fallback. File
  selection never pretends a server upload succeeded.
- Unconfirmed copy/badge reaches4.5:1; keyboard focus uses the shared visible
  ring; approved targets44px; no undefined token or global selector spillover.
- Invalid/expired Token errors show a direct AU02 path; backend policy and reset
  semantics stay untouched. Fake mail/OAuth and isolated data only.
- Build, focused .NET and Node tests pass; normal suite is recorded with actual
  passed/failed/skipped counts. Browser coverage and live/MariaDB blockers remain
  distinct from fixture or emulated checks. No fabricated full-suite claim.
- Original unrelated files and protected source boundaries remain unchanged;
  no credentials, cookies or live tokens enter reports/screenshots/Git.

Use the supported local launcher/saved keys privately when possible. Review's
Docker startup timed out; do not treat a fixture host as app readiness, reset a
database to work around it or make paid API calls. Report metadata-only speech
status accurately. Preserve occupied listeners and all saved secret stores.

## Handoff

Report in Vietnamese: repaired finding IDs, changed files, before/after behavior,
exact checks and remaining risks/blockers. Distinguish local, CI, staging and
production. Leave ungranted Git/release stages to the owner.
