# Account UI — implementation checklist for Gemini

Owner visual update 2026-11-08: all redesigned Account pages use Claymorphism
from the current DESIGN.md/shared CSS, with laptop/desktop demo priority. Preserve
the approved option B and all behavior contracts below. Recheck the rendered
visuals after adopting current tokens; old screenshots are not Clay acceptance.
The shared foundation remains read-only for this Account implementation task.

Status: **Design approved by owner; Stage I awaits owner launch.**
Date: 2026-11-08 (Asia/Bangkok). Design baseline:
`eed4e2c95c79ef708dc9af77c8b9326186835122`, `feature/chau-ui-design-system`.

## Outcome and activation

Implement Account screens AU01–AU07 using the recommended arrangement B in
[account-ui-design.md](../../design-system/account-ui-design.md). Châu approved
arrangement B and the AU01–AU07 design deliverables with the chat message
“Duyệt” on 2026-11-08 (Asia/Bangkok). The owner will activate the existing
[Gemini implementation prompt](../../../.ai/prompts/gemini38-flash-high-account-ui-implementation.md)
in chat before application edits. Design approval is recorded; implementation
activation remains pending. The approval does not expand backend/shared or
Git/CI/release authority.
The authoring session was `ANALYSIS_AND_DESIGN_ONLY`; it stopped before app edits.

After activation, use `IMPLEMENT_LOCAL` only for the approved UI boundary below.
Work as one agent for investigation, implementation, review and verification.
Do not start another model, worker, reviewer, chat or CLI agent. Follow AGENTS.md
and the current prompt standard/template. Owner-selected IDE models are separate
from application AI settings; preserve both without claiming model substitution.

## Context and evidence

- Read [design specification](../../design-system/account-ui-design.md), its
  contract appendix, [research](../../ui-audits/2026-11-08-account-ui-research.md)
  and [static preview](../../design-system/account-ui-preview.html). Read the
  active batch only after establishing scope; use current source if it moved.
- Reuse AGENTS.md, DESIGN.md, team-ui-workflow.md and required TCT skills.
  CodeGraph first for indexed source; its Razor header-only gap is documented.
  Read omitted markup directly instead of repeatedly querying the same gap.
- Stage D ran local launcher/readiness and 20 existing Account/badge/partial
  tests successfully. That is baseline evidence, not acceptance of future UI.
  Research distinguishes observed, fixture, source, preview and blocked states.
- Preparation documents include stale local-only publication wording. Actual
  baseline already contains merged PR #130 and #131. Inspect Git instead of
  rewriting unrelated historical documents.

## Authority, scope and deliverables after activation

Application files allowed by this recommendation:

| Type | Exact files |
| --- | --- |
| Views | `TCTEnglish/Views/Account/Auth.cshtml`, `ForgotPassword.cshtml`, `ForgotPasswordConfirmation.cshtml`, `ResetPassword.cshtml`, `Profile.cshtml`, `Settings.cshtml`, `AccessDenied.cshtml` |
| New scoped CSS | `TCTEnglish/wwwroot/css/account-auth-ui.css`, `TCTEnglish/wwwroot/css/account-ui.css` |
| New JS | `TCTEnglish/wwwroot/js/account-auth-ui.js`, `TCTEnglish/wwwroot/js/account-ui.js` |
| New behavior/render tests | `TCTEnglish.Tests/AccountUiRenderTests.cs`, `scripts/tests/test_account_auth_ui.cjs`, `scripts/tests/test_account_ui.cjs` |
| Handoff | `docs/feature-audits/2026-11-08-account-ui-execution-ledger.md`; only applicable Account adoption rows in `docs/design-system/team-ui-workflow.md`; symptom-matched bug memory if fixing an observed bug |

No Account partials are needed in this design. Reuse existing `_PageHeader` and
`_StateMessage` without changing their implementations. Keep typed legacy
ViewModels exact; `Auth` remains dynamic until a separately authorized backend task.

Read-only: both shared layouts, `tct-learning-ui.css`, `layout.css`, `site.css`,
`account.css`, `auth-stitch.css`, controllers/services/ViewModels/models, all
configuration/project/dependency files and database schema/data. Do not remove
legacy files used by Admin or other consumers. The two Account views may remove
their own legacy CSS/font links. Do not change the Account font link still
conditionally loaded by `_Layout`; override rendered page typography through
scoped element rules, excluding icon font elements.

Preserve current routes, native/AJAX distinctions, hidden reset fields, names,
antiforgery, Enctype, identity/access guards, role behavior, JSON/redirects,
callbacks and selector compatibility. No new persistence, endpoints, password
policy, social-link proof, subscription facts, auth feature or runtime key change.
No migration/data package is needed for this UI-only boundary. If a persisted
change becomes necessary, stop that dependency and request scope rather than
silently creating it. Continue independent approved UI batches.

Git/CI/release stages remain ungranted: no commit/push/PR/remote CI/merge/deploy.
Do not enable staging/production gates or alter hosting. Preserve existing dirty
files/workloads and saved credentials. No real mail, external OAuth, paid AI/audio,
account deletion/reset, user mutation or database reset for manual QA.

## Ordered work

### B0 — Reconcile the approved revision and baseline

Dependencies: explicit owner approval of the design and launch of Stage I.

- [ ] Record the approval/design revision, current branch/HEAD/status and hashes
      of pre-existing dirty files in the execution ledger. Do not edit the five
      preparation files or unrelated report/scope documents merely to mark progress.
- [ ] Reconcile any affected source changes against specification section 6.
      Recheck AccountController, all seven views and bound ViewModels/consumers.
      Never assume a tool's historical index is fresh.
- [ ] Read symptom-relevant 2026-11-02 Account bug entries and current known
      issues; preserve avatar fallback, sidebar inert, earned-badge truth and
      safe BCrypt/social handling. These prior fixes are not permission for refactors.
- [ ] Run the existing focused regression command below once; record baseline
      failures separately. The source at Stage D passed 20/20 with existing
      nullability/analyzer warnings. Do not describe it as warning-free.
- [ ] Capture safe before screenshots at 1440/768/390 using existing synthetic
      local accounts. Redact identity before capture; keep credentials in memory.

Evidence: ledger identifies actual permission, baseline and preservation list;
TRX or exact test output; screenshots under ignored `.tmp/account-ui/implementation/`.

### B1 — Auth (AU01) and standalone asset ownership

Files: `Auth.cshtml`, new `account-auth-ui.css`, new `account-auth-ui.js`.
Tests: new `test_account_auth_ui.cjs`, AU01 cases in `AccountUiRenderTests.cs`.
Dependency: B0; later recovery pages reuse these assets.

- [ ] Write meaningful failing regressions before implementation: execute the
      production JS with controlled DOM/fetch/FormData using existing
      `scripts/tests/dom_fixture.cjs` and Node vm, as the existing submit-guard
      harness does. Do not mirror a second implementation of the guard in tests.
      Wrap/extend fake elements in the new test file; do not add a DOM package.
- [ ] Add `.tct-theme.account-auth-ui`; replace private auth-stitch composition
      with the specified desktop split and compact mobile brand/form. Remove
      only Auth's link to auth-stitch.css; retain that file. Use shared tokens,
      Inter weights <=700, existing Font Awesome family and Bootstrap 5.3.3.
- [ ] Reuse PageHeader semantics/partial for one visible h1. Keep tab-login,
      tab-register, form-login, form-register, auth-heading, inputs, buttons,
      blocked IDs, loader IDs, terms and callback contracts in the appendix.
- [ ] Move inline behavior to `account-auth-ui.js` once. Use encoded data-* for
      initial tab/home/external destinations; encode server text in Razor. One
      owner binds each event; compatibility window aliases must not bind twice.
- [ ] Preserve Login fetch FormData and success/blocked/message responses;
      Register stays native POST, keeps email/password/fullName and terms required.
      Preserve success TempData and both blocked-message variants. Authenticate
      success only from response, then navigate Home/Index; remove fake percentage
      delay without changing destination. Keep external entry routes uncalled in QA.
- [ ] Implement scoped show/hide names, 44px targets, autocomplete/paste, roving
      tabs including Left/Right/Home/End, hidden inactive panel and focusable error.
      Do not add Register ConfirmPassword or a stronger password restriction.
- [ ] Guard each form synchronously before async work. Restore pending on HTTP,
      malformed JSON, network failure and pageshow; native invalid validation must
      not latch disabled state. Preserve entered nonsecret values within the DOM;
      do not claim server round-trip values that Register does not supply.

Acceptance/evidence: render tests check action/method/binding/token plus correct
initial tabs on GET and error return; JS tests prove one request for click+Enter
or requestSubmit during flight, no redirect on failure, retry after failure and
textContent encoding. Browser shows default/register/error/pending/success/blocked
fixture, focus and 44px targets at required sizes. Screenshots plus exact browser
engine/version, viewport and fixture source go in the ledger. No OAuth invocation.

### B2 — Recovery (AU02–AU04)

Files: three recovery views and auth assets. Tests: recovery cases in new render
test file and submit guards in `test_account_auth_ui.cjs`. Dependency: B1.

- [ ] Keep Html.BeginForm methods/actions and AntiForgeryToken; preserve
      form-forgot-password/btn-forgot-submit/Email and form-reset-password/
      btn-reset-submit/Token/Email/NewPassword/ConfirmPassword. Keep reset
      ValidationSummary excludePropertyErrors:false so Token errors remain visible.
- [ ] Apply the common <=30rem recovery card and copy from the spec. Remove
      per-view inline palettes/global CSS, load shared/auth assets directly because
      Layout=null. Keep brand compact and the main-content landmark exact.
- [ ] Follow the exact asset-order table in specification section 2. Standalone
      recovery does not gain jQuery: use native constraints plus narrowly scoped
      min6/ConfirmPassword client checks matching the existing server model;
      clear custom validity on edits. Profile/Settings retain their existing
      validation partial before account-ui.js. Preserve existing asset versions.
- [ ] Make confirmation generic even on ViewBag.Message fallback; no echo of
      account email, no claim of email delivery, no standalone confirmation GET.
      Reuse StateMessage Success and PageHeader; add only the existing recovery/
      login navigation, no resend endpoint/timer.
- [ ] Add distinct reveal controls for both reset fields; keep hidden model
      values untouched. Token ModelState error remains actionable with AU02 link.
      Do not infer expired token from query or client clock. Show mismatch/short
      inline feedback; backend min6/Compare and atomic consumption remain intact.
- [ ] Preserve native navigation. Pending guards run after validation; pageshow
      releases stale busy state. Do not promise custom fetch network-error UI for
      native POST navigation. Never store/reset passwords in browser storage.

Acceptance/evidence: use existing TestWebApplicationFactory with SQLite in-memory
and replace IAppEmailSender with a test double for confirmation. Assert equal
generic messaging for both existence branches without external mail. Existing
null-expiry/single-consume/concurrency tests must stay green. Render tests assert
Token errors are visible and hidden inputs remain bound without recording their
values. Browser fixture evidence or an explicit blocked label for each token/error
state; no synthetic token inserted into the owner's MariaDB. No SMTP calls.

### B3 — Profile and Account navigation (AU05)

Files: `Profile.cshtml`, new `account-ui.css`, new `account-ui.js`; new Account UI
render tests and `test_account_ui.cjs`. Dependency: B0; can reuse B1 review patterns.

- [ ] Keep `_Layout`, HideSidebar=true and routes; replace only Profile's legacy
      asset links with new scoped assets. Do not alter shell offsets or header.
      Keep account navigation IDs, data-tab/panes, collapse preference key and
      deep-link behavior. Use `.tct-theme.tct-page.account-ui`, no old negative margin.
- [ ] Render one PageHeader outside all panes. Tab values profile/security/
      subscription work; unknown query falls back to profile; replaceState retains
      unrelated query values. Hidden pane is absent from Tab/AT order. Security
      quick forms remain in place and continue redirecting to Settings after POST.
- [ ] Implement the specified 15rem desktop navigation / <=992 drawer adapter
      around Bootstrap Offcanvas on the same aside. Preserve `.open`, inert and
      aria-expanded; exactly one overlay (`sidebarOverlay`), one focus trap and
      one event owner. Mobile mode ignores collapsed width but preserves preference.
      On desktop transition, close then dispose; restore previous inert states
      of shell header/footer/launcher. Do not change shared layout.js.
- [ ] Keep multipart UpdateProfile binding and validation. Email display remains
      outside update binding; FullName/Avatar fields exact. Avatar chooser only
      previews; valid image unhides preview, error/fallback restores initials;
      JPG/PNG,2MB copy matches ImageUploadPolicies.Avatar. Cancel reloads.
- [ ] Render streak/count/list only from existing model EarnedBadges/StreakDays;
      encode badge text, render unlocked names/descriptions, meaningful empty
      StateMessage and Goals link. No percentages, locked badges or query writes.
- [ ] Present role Standard/Premium/Admin exactly; no invented expiry or active
      subscription state. Keep subscription pane and `/Premium` entry. Preserve
      Goals and security routes. Implement profile security labels/IDs, reveal
      accessible names and CONFIRM dialog behavior consistently with Settings.
- [ ] Move inline scripts once to account-ui.js, scope selectors inside wrapper,
      and retain previewImage alias only if needed. Native save pending and
      TempData feedback must be honest; retain server PRG limitations noted below.

Acceptance/evidence: Node behavior tests execute production JS for deep links,
drawer open/close/inert/focus and breakpoint cleanup, avatar image/fallback,
duplicate save guard and storage-denied fallback. Use browser for actual Bootstrap
focus trap, desktop collapse, Escape/focus return and viewport fit. Use a synthetic
image without upload to prove preview from missing/broken-avatar state. Render
tests cover role variants and empty/earned data without writing local real users.
Existing ProfileBadgeReadTests remains read-only/user-scoped. Photograph no
credentials. Do not submit UpdateProfile on existing accounts for manual evidence.

### B4 — Settings and AccessDenied (AU06–AU07)

Files: `Settings.cshtml`, `AccessDenied.cshtml`, account assets, both Account UI
test files. Dependency: B3 navigation/handler conventions.

- [ ] Keep Settings layout/model, form actions, field names, validation and
      TempData PRG. PageHeader, section nav then security/linked/notifications/
      danger in one content column. Preserve all nav/section IDs/data-section.
      Use non-fixed wrapping section nav; no new backend/hash routing feature.
- [ ] Keep settingsEmailInput and asp-for CurrentPassword/NewPassword/
      ConfirmPassword. Replace misleading8 hint with current6 copy; leave server
      requirements unchanged. Show/Hide labels distinguish all fields. Do not
      claim a social-only password can be set without CurrentPassword.
- [ ] Keep Google boolean/unconfirmed/false branches and Facebook boolean/false
      branches, their routes and permanent explanatory copy. A success TempData
      message does not turn an unconfirmed state into linked. Do not add unlink.
- [ ] Keep notification section and selector, disable/clear both checkbox UI
      controls with explicit unavailable copy. No save button or toast, no new
      names/API/data fields. Do not describe disabled UI as all notifications off.
- [ ] Keep Bootstrap delete modal IDs and exact trim CONFIRM gate; reset gate on
      each open/close, focus after shown, Escape/Cancel return focus after hidden.
      Confirm submission remains native antiforgery POST; pending guard prevents
      repeat, never test real deletion. Modal stays in the tct-theme subtree.
- [ ] AU07 keeps `_Layout`, its learner sidebar and controller's 403. Reuse
      PageHeader, short explanation and current Home/Profile links; don't create
      request-access routes or expose the denied private URL.
- [ ] Use the scoped account script on Settings only; AU07 needs CSS, no new
      behavior. Remove old inline handlers/auto-dismiss timers from edited views.

Acceptance/evidence: prove unsupported checkbox state survives re-render without
claiming persistence; provider branches with fixture models, no OAuth. Fake-mail/
test-auth fixture handles security POST success/error/no-op; browser verifies
modal reopen disabled, lowercase/mismatch disabled, trim CONFIRM enabled, cancel
zero requests, focus/Tab trap. Existing AccountCorrectnessIntegrationTests keeps
403/RBAC, IDOR404, social/malformed-hash behavior. Check server PRG messages rather
than a timer or client-authored success. No deletion or password change on UAT users.

### B5 — Complete acceptance, inline review and Vietnamese handoff

Dependencies: B1–B4 focused tests passed; new failures resolved within scope or
clearly reported as blocked. Do not call partial coverage a pass.

- [ ] Build Release; rerun affected focused tests after changes. Run the complete
      normal application suite once at closeout, after focused checks pass.
- [ ] Check all seven pages at 1440×900, 768×1024, 390×844, 375×812 and 390×500;
      desktop/narrow long Vietnamese/English/unbroken strings; 200% **actual browser
      zoom**. If only text-size/viewport/CSS zoom is available, label it accurately
      and leave real zoom unverified. Check descendants for clipping even when
      scrollWidth matches viewport. No fixed CTA/header covers active controls.
- [ ] Test keyboard through tablists, password buttons, account/mobile/global
      navigation, modal cancel and feedback. Measure active focus rings and
      touch targets. Calculate rendered text/control contrast over effective
      backgrounds, including error/success/linked/disabled and reduced motion.
- [ ] Smoke one Admin page that loads unchanged account.css and one unrelated
      learning page, plus learner header/sidebar/launcher. Keep before/after
      screenshots and explain any shared-shell limitation outside page scope.
- [ ] Review the complete diff inline: feature boundary, form/JS/HTTP contracts,
      no accidental double bindings, no credentials or real tokens. Verify all
      initial unrelated files remain byte-for-byte unchanged. No hidden worktree,
      commit, remote run or data operation has been implied by local success.
- [ ] Update only the Account adoption rows after observed conversion. If an
      actual bug was fixed, append the required symptom/root-cause/evidence entry
      and update only a matching listed known issue. Do not mark backend debt fixed.
- [ ] Handoff in Vietnamese: result/files/checks, screenshots/open instructions,
      remaining blocked states and owner decisions. Distinguish local, CI, staging
      and production. Leave ungranted Git/release stages to the owner.

## Verified baseline commands and planned new-test commands

The first command ran in Stage D; the second/third are required after app edits.
The two Node test files and new render test class are to be authored in Stage I.

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~ProfileBadgeReadTests|FullyQualifiedName~SharedUiPartialsRenderTests"
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountUiRenderTests|FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~ProfileBadgeReadTests|FullyQualifiedName~SharedUiPartialsRenderTests"
node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release
```

For manual local inspection, use the supported launcher, saved credentials/keys
privately and a free port; readiness and metadata-only speech checks are required:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5102
pwsh -NoProfile -File scripts/speech-status.ps1
```

An existing listener on 5102 must be preserved or an explicitly owned process
restarted; choose a different free port when needed. Do not paste passwords or
keys into chat/argv or logs. A configured key is not live quota validation.

## Known backend/shared limits that do not block the page design

| Dependency | Default in this UI plan | Separately authorized follow-up if requested |
| --- | --- | --- |
| Settings checkboxes have no persistence | Explicit disabled/informational controls | Storage/API/notification preference design and schema assessment |
| Provider booleans always false; callback only checks email | Preserve branches and explain uncertainty | Persisted external identity proof and callback semantics |
| CurrentPassword required before social sentinel branch | Preserve form/server requirement, recovery link | Safe first-local-password workflow/policy review |
| Subscription expiry absent from Profile model | No unlimited/active claim; retain role and entry | Verified subscription ViewModel/service fields |
| Native PRG/register model round-trip limitations | Client validation and actual server messages; no false preservation claim | Controller/ViewModel error/value round-trip change |
| Profile upload-error branch omits Role | Do not fabricate role from storage/client state; report limitation | Repopulate trusted display model in controller |
| Global header height, mobile root14, legacy font download | Preserve shell/foundation, scope Account styling | Separate shared-impact proposal with all consumers |

These are isolated follow-ups, not implementation TBDs for the chosen layout.
If the owner wants a different behavior, get that specific authority before
crossing the read-only boundary. The recommended page work uses existing tokens.

## Stage I launch text

Design approval has been recorded above. The owner can use the following English
launch text to activate Stage I later; recording this text does not start execution:

> Execute
> `.ai/prompts/gemini38-flash-high-account-ui-implementation.md` using the approved
> `docs/design-system/account-ui-design.md` and this implementation checklist.
> Use one agent and IMPLEMENT_LOCAL mode on the current feature branch. Preserve
> unrelated work, source contracts and the read-only backend/shared boundaries.
> Complete the UI batches and meaningful local acceptance, then report evidence
> and blockers in Vietnamese. Do not commit, push, open a PR, run remote CI, merge
> or deploy without my separate authorization.
