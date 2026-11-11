# Account UI — repair plan after Gemini implementation review

Date: 2026-11-08 (Asia/Bangkok).
Status: **Review deliverable; implementation awaits explicit owner fix authority.**
Branch/HEAD inspected: `feature/chau-ui-design-system` / `eed4e2c`.

## Outcome, evidence and scope

Repair findings R1–R8 in the
[review report](../../ui-audits/2026-11-08-account-ui-implementation-review.md)
while preserving the [approved option B](../../design-system/account-ui-design.md).
Do not repeat design research or replace the Account feature. One agent performs
all diagnosis, changes, review and verification inline.

Current authority is REVIEW_ONLY. This plan and its
[execution prompt](../../../.ai/prompts/account-ui-review-repair.md) are concrete
handoff artifacts, not permission to edit runtime code. Activate IMPLEMENT_LOCAL
only after the owner explicitly requests fixes/launches the repair prompt.

Expected repair boundary after activation:

- `TCTEnglish/Views/Account/ForgotPassword.cshtml`, `ResetPassword.cshtml`,
  `Profile.cshtml`, `Settings.cshtml`; Auth markup only for a directly related
  focus/target/handler regression that cannot be resolved in its owned assets.
- `TCTEnglish/wwwroot/js/account-auth-ui.js`, `account-ui.js`.
- `TCTEnglish/wwwroot/css/account-auth-ui.css`, `account-ui.css`.
- `TCTEnglish.Tests/AccountUiRenderTests.cs`,
  `scripts/tests/test_account_auth_ui.cjs`, `scripts/tests/test_account_ui.cjs`.
- Correct the Account execution ledger, only the Account adoption rows, and
  relevant bug-memory entries after actual fixes. Update these review artifacts
  with evidence; do not change approved design into an implementation success claim.

Read-only: shared layouts/partials/CSS, controllers/services/ViewModels/models,
Program/appsettings/project/dependency files, migrations and real account data.
No new token/palette/library, no password/auth/OAuth/provider/subscription policy
or persistence change. No migration/data package for this UI-only work. Preserve
unrelated DESIGN.md and report changes. No Git/CI/release stage is granted.

## Ordered checklist

### C0 — Preserve current work and establish failing regressions

Dependencies: owner fix authority; read AGENTS.md/current skills and approved spec.

- [ ] Record actual branch/HEAD/status and hashes of existing modified/untracked
      files, including Gemini outputs. Reuse the review's evidence, not its
      ephemeral process/port assumptions. Do not reset or overwrite dirty files.
- [ ] Recheck the exact R1–R8 locations through CodeGraph then bounded reads for
      omitted/unindexed source. Read current symptom-matched bug memory before fixes.
- [ ] Establish meaningful failing regressions for each behavior before modifying
      production code. Browser tests must use real form event dispatch including
      inline markup, not call each handler once in a fake DOM and infer correctness.
- [ ] Review baseline: app build passed; focused26/26 and Node10/10; full normal
      suite1175 pass/22 skip/0 fail. Counts are historical evidence until rerun.

Acceptance: preserved-work manifest, red regression evidence and a batch ledger.

### C1 — R1/P1: restore recovery submission

Files: ForgotPassword.cshtml:50; ResetPassword.cshtml:50;
account-auth-ui.js:227–287,354–361; auth Node tests and render tests.

- [ ] Select one submit owner for each recovery form, preferably the external JS
      listener already used by Auth. Remove duplicate inline binding, keeping
      Html.BeginForm, POST action/method, antiforgery and hidden fields exact.
- [ ] Keep compatibility function exports if required by a remaining caller;
      aliases must not register a second handler. Confirm init is idempotent.
- [ ] For a valid first submit, allow the native POST and set pending once.
      Repeated click/Enter during that same flight must be rejected without
      canceling the first event. Invalid/mismatched fields must remain editable.
- [ ] Preserve server response/PRG semantics, generic recovery message and reset
      token policy. No AJAX conversion, fake success timer or real SMTP/reset call.

Acceptance: real rendered Forgot/Reset forms each generate exactly one allowed
native POST on the first valid submit in a fake-mail/isolated fixture; second
submit during flight is suppressed; mismatch/short input does not latch pending;
back/pageshow restores retry. The regression must fail on the reviewed code.

### C2 — R2/P1: complete the Offcanvas lifecycle at breakpoint changes

Files: account-ui.js:45–89,143–187; Account Node tests; rendered browser.
Dependency: C0; independent of recovery.

- [ ] Close an open/showing drawer through the Bootstrap lifecycle before
      disposal and desktop cleanup. Handle rapid resize without overlapping
      instances or losing a queued hidden callback.
- [ ] Restore body scrollbar/padding through the owning Bootstrap lifecycle;
      remove stale show/dialog/aria-modal and page overlay state; restore previous
      inert values and desktop collapse preference. Keep closed mobile links inert.
- [ ] Keep one focus trap/overlay/event owner; preserve all Account IDs and
      key accountSidebarCollapsed. Do not replace shared layout.js or manually
      clear an unrelated modal's body scroll lock.

Acceptance: on both Profile and Settings, open at390×844, resize1440×900 and
scroll with wheel/keyboard to lower content. Body scroll/padding, dialog attrs,
overlay and inert are correct; re-enter mobile and reopen/Escape/focus return
work. Test open/closed/transition and storage-denied states. Browser proof is
required; a fake Offcanvas object alone cannot certify scrollbar cleanup.

### C3 — R3/P2: preserve destructive safeguards during state restoration

Files: account-ui.js:441–503; Settings.cshtml:297–329;
Profile.cshtml delete modal; Account Node/render tests.
Dependencies: C0; complete this before moving the Settings modal into form scope.

- [ ] Replace unconditional pageshow enablement with form-aware restoration.
      Delete submit must still obey trim CONFIRM and reset on each open/close;
      no pageshow event may enable an empty/lowercase confirmation.
- [ ] Bring Settings modal into its Account tct-theme/behavior scope and retain
      IDs, POST/antiforgery and Bootstrap focus behavior. Add duplicate-submit
      protection for its actual form without bypassing the confirmation gate.
- [ ] Respect canceled/invalid submit events before setting in-flight state;
      add a behavioral regression. Do not install jQuery or edit shared layout
      to hide the pre-existing dependency limitation during this repair.

Acceptance: both entry points have empty/lowercase disabled, trim CONFIRM enabled,
reopen/reset disabled, synthetic persisted pageshow safe, Cancel/Escape zero POST,
correct focus return and no duplicate mutation. Never exercise deletion on an
existing local account. Label synthetic pageshow separately from actual BFCache.

### C4 — R4/P2: restore original avatar state on Cancel

Files: account-ui.js:362–439; Profile.cshtml:95–99,151–155; Account Node tests.

- [ ] Restore the approved reload-on-Cancel behavior or, if retaining local reset,
      restore the complete original saved image/name state, not generic initials.
      The approved default is reload; avoid introducing a second snapshot/cache.
- [ ] Keep FileReader success/error handling, missing/broken-avatar fallback,
      JPG/PNG2MB checks and multipart submission. Selecting a file is not upload.

Acceptance: with an existing saved avatar, Cancel with no edits and after preview
shows that saved avatar; missing/broken originals still fall back correctly;
invalid files can be corrected; no server upload is made by the browser proof.

### C5 — R5–R6/P2: semantic contrast, focus, targets and theme scope

Files: Settings.cshtml:215–240; both new Account CSS files; directly affected
owned markup only. Dependencies: C3 for Settings modal scope/lifecycle.

- [ ] Replace warning-colored text/badges with existing warning-text/soft tokens
      or tct-badge--warning. Preserve linked/unconfirmed state certainty and routes.
- [ ] Remove invalid color-as-box-shadow declarations that suppress the shared
      focus outline. Reuse the real shared focus rules or width/style/color tokens.
      Do not assume every existing focus selector is broken: measure each case.
- [ ] Use existing --tct-shadow-soft instead of undefined --tct-shadow-sm. Resolve
      target sizing specificity so the approved touch buttons reach44px; keep
      icon targets and accessible labels. No global CSS/root/font/token changes.
- [ ] Scope modal selectors under the Account wrapper after C3; avoid styling
      unrelated .confirm-modal consumers from the Account stylesheet.

Acceptance: unconfirmed text and badge >=4.5:1 over their computed backgrounds;
focus ring2px/offset2px visible for keyboard Auth/recovery inputs and relevant
controls; approved touch targets44px. Check hover/disabled/pending and narrow
layout after the cascade. Record exact pairs/dimensions rather than only tokens.

### C6 — R7/P2: make invalid/expired recovery actionable

Files: ResetPassword.cshtml:57,88–91; AccountUiRenderTests.cs.
Dependency: C1 so the recovery destination actually submits.

- [ ] Detect existing Token ModelState errors in Razor; add the approved recovery
      instruction and local ForgotPassword anchor, with focusable error summary.
- [ ] Preserve hidden Token/Email and validation summary behavior. Do not infer
      expiration from JavaScript time, consume a token on GET or add an endpoint.

Acceptance: expired/invalid fixture renders the AU02 link and actionable message;
default/field-mismatch states retain their own feedback; valid/replay/null-expiry
existing tests pass. Use isolated models/fake services, never real account tokens.

### C7 — R8/P2: close evidence gaps and correct handoff records

Files: AccountUiRenderTests.cs; both Account Node tests;
docs/feature-audits/2026-11-08-account-ui-execution-ledger.md;
only Account adoption rows of docs/design-system/team-ui-workflow.md;
applicable bug memory and review records.
Dependencies: C1–C6 fixes and targeted regressions passing.

- [ ] Cover real form binding/actions, Register active tab/error return, recovery
      confirmation with fake mail, reset Token errors, role/earned/empty cases,
      native pending cancellation and the reproduced lifecycle regressions.
      Tests must assert behavior/output, not only CSS class or substring presence.
- [ ] Correct ledger Auth fields/IDs to the actual contract; distinguish filtered
      Account tests from the normal full suite. Remove fictitious /Account/Auth
      entry from tracker. Report blocked browser/provider checks as blocked.
- [ ] Run build/focused tests/Node first. Once those pass, run the full normal
      suite once. Do not repeatedly rebuild or run unrelated tooling suites for
      these view/asset changes.
- [ ] Browser check seven screens at1440, sample laptop1366×768,768,375/390 and
      short-height; long strings, keyboard, actual200% zoom when available,
      contrast, reduced motion and CSS isolation. Do not assume the owner's demo
      laptop resolution. Mark viewport/text-size emulation accurately.
- [ ] Smoke Admin account.css consumer, learner shell and unrelated learning page.
      Prefer supported launcher with a free port; if Docker remains unavailable,
      record that and use isolated existing fixtures without presenting them as
      MariaDB/production readiness. Keep all mutations/external services fake.
- [ ] Review the complete diff inline, append symptom/root-cause/verification
      bug memory only after actual fixes, preserve unrelated work, hand off in
      Vietnamese. Do not mark B5 complete until its required acceptance is met.

## Commands and safe verification

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountUiRenderTests|FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~ProfileBadgeReadTests|FullyQualifiedName~SharedUiPartialsRenderTests"
node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5104
pwsh -NoProfile -File scripts/speech-status.ps1
```

Preserve occupied listeners; choose another free port. Review used an ignored
GET-only fixture host at5184; its source/evidence live under `.tmp/account-ui-review`
and are not shipped app endpoints or a new supported launcher. Do not export
fixture account credentials/tokens, contact real providers or infer live account
quota from configured metadata. No production gate changes or publication.

## Short owner launch text, after fix authority is granted

> Read and execute `.ai/prompts/account-ui-review-repair.md` in IMPLEMENT_LOCAL
> mode. Fix review findings R1–R8 using the approved Account design and repair
> checklist. Work as one agent on the current feature branch, preserve unrelated
> work and protected backend/shared files, verify the real changed behavior, and
> report in Vietnamese. No commit, push, PR, remote CI, merge or deployment.
