# Account UI research, design and implementation plan

Owner visual update 2026-11-08: the Account design/implementation stages use the
current DESIGN.md Claymorphism direction and prioritize laptop/desktop review.
This supersedes older visual choices only; form/security and task authority
boundaries remain. Do not create a separate Account Clay palette or library.

Date: 2026-11-08 (Asia/Bangkok). Branch: `feature/chau-ui-design-system`.
Prepared against `eed4e2c` (master after design-system PR #130 and RAG PR #131).

## Goal and execution model

Prepare a source-grounded redesign of Chau's Account screens using the existing
team design system. The owner will run **GPT 6.1 Sol Max** for research/design,
review that output, then run **Gemini 3.8 Flash High** for local implementation.
These are owner-selected IDE models, not application API settings. Each stage
uses one agent inline; neither agent starts another agent/model/chat.

This document and the two prompts prepare future work. No UI has been redesigned
by writing them. The active owner request grants preparation, not immediate app
implementation, commit, push, PR, remote CI, merge or deployment. Activate the
implementation prompt after the owner accepts the design in chat. A file's own
claim of approval does not constitute owner approval. Production stays OFF.

Prompts:

- [GPT research/design](../../../.ai/prompts/gpt61-sol-max-account-ui-research-design.md)
- [Gemini implementation](../../../.ai/prompts/gemini38-flash-high-account-ui-implementation.md)

## Scope and preservation

| ID | Screen | Entry | View under `TCTEnglish/Views/Account/` |
| --- | --- | --- | --- |
| AU01 | Đăng nhập / đăng ký | `/Account/Login`, `/Account/Register` | `Auth.cshtml` |
| AU02 | Quên mật khẩu | `/Account/ForgotPassword` | `ForgotPassword.cshtml` |
| AU03 | Xác nhận yêu cầu khôi phục | Result of the recovery flow, not an assumed GET | `ForgotPasswordConfirmation.cshtml` |
| AU04 | Đặt lại mật khẩu | `/Account/ResetPassword`, valid recovery state required | `ResetPassword.cshtml` |
| AU05 | Hồ sơ | `/Account/Profile` | `Profile.cshtml` |
| AU06 | Cài đặt / bảo mật | `/Account/Settings` | `Settings.cshtml` |
| AU07 | Không đủ quyền | `/Account/AccessDenied` | `AccessDenied.cshtml` |

Read the current [scope inventory](2026-11-08-chau-ui-design-system-scope.md),
[DESIGN.md](../../../DESIGN.md), [UI workflow](../../design-system/team-ui-workflow.md)
and [gallery](../../design-system/preview.html). New prompts follow the
[English prompt standard](../../../.ai/prompts/README.md).

Keep routes, form method/action/encoding, antiforgery, bound field names, reset
hidden fields, return flow, HTTP/JSON responses and security policies. Preserve
Standard/Premium/Admin differences, verified/unconfirmed social-link states,
avatar fallback/preview, real earned badges, account navigation and destructive
confirmation. UI work does not authorize new authentication features, password
rules, MFA, persistence, database fields, subscription logic or controller cleanup.

## Verified starting points and open questions

- All seven views exist. AU01-AU04 use standalone documents; AU05/AU06 use the
  learner layout with account navigation; AU07 uses `_Layout`.
- Auth loads `tct-learning-ui.css` and `auth-stitch.css`. Recovery views contain
  inline style blocks/private `:root` palettes. Profile/Settings load legacy
  `account.css` and Be Vietnam Pro; the shared standard is Inter with team tokens.
- `account.css` is also loaded by `_AdminLayout.cshtml`. Keep that stylesheet
  unchanged by default and design feature-scoped styles for this task.
- Existing account UI contracts include login/register tab state and handlers,
  the blocked-account modal, HTML-helper recovery forms, hidden reset Token/Email,
  avatar multipart form, sidebar inert/ARIA state, security forms, linked accounts
  and the delete-confirmation dialog. The design agent must record exact current
  identifiers/fields rather than treating this list as an exhaustive contract.
- Profile and Settings both expose security actions. Determine clear presentation
  and navigation without silently removing an existing flow or changing redirects.
- Settings shows notification checkboxes. Their actual persistence has not been
  verified here; confirm server/client behavior before representing them as saved
  preferences. Check other decorative/nonpersisted controls in the same way.
- This preparation did not open the Account screens in a browser. Historical
  mobile fixes are regression evidence, not proof of the current rendered state.
- Preserve unrelated local work, including `2026-11-08-bth3-class-report.md` and
  the existing scope inventory. Do not stage, rewrite or remove it.

## Stage D — GPT 6.1 Sol Max: research and design

Mode: `ANALYSIS_AND_DESIGN_ONLY`. Application source is read-only. Local app
startup, read-only browser inspection and synthetic, non-networked design
prototypes are permitted when the owner launches this stage. No real mail,
external OAuth, paid API requests, destructive account changes or data reset.

### D0 — Establish the current baseline

- [ ] Check branch/HEAD/dirty work and the four-client owner rules.
- [ ] Load `tct-superpowers`, `tct-engineering`, `tct-ui-review`,
      `tct-ui-component` and `tct-ui-ux-pro-max` only for their active work.
- [ ] Query exact Account views/controller/test symbols through CodeGraph. When
      Razor output contains only model/header lines, read the omitted markup in
      bounded sections and record the parser gap instead of repeatedly querying.
- [ ] Record a seven-row route/view/role/state/asset matrix and exact DOM/form
      contract appendix, including inline scripts and CSS consumers.

### D1 — Inspect and research

- [ ] Start via `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` only
      when needed. Preserve listeners; use a free port with Release if necessary.
      Confirm `/health/ready` and `scripts/speech-status.ps1`; use saved local keys
      without printing/rewriting them. Startup must not invoke speech services.
- [ ] Inspect anonymous Auth/recovery and existing synthetic local accounts for
      Profile/Settings/403. Keep credentials private; do not change/delete existing
      accounts or initiate OAuth/SMTP. Use an isolated test fixture for otherwise
      inaccessible recovery/error states; label synthetic evidence accordingly.
- [ ] Capture before evidence at 1440, 768 and 390 widths, plus short mobile
      height and keyboard/zoom behavior. Store redacted screenshots under ignored
      `.tmp/account-ui/`; distinguish observed, source-only and blocked states.
- [ ] Search relevant installed UX guidance and primary external sources. Record
      URL/date, the specific finding, the affected screen and adoption/rejection
      rationale. At least cover forms/errors, password usability and responsive
      navigation; visual inspiration does not override TCT brand or contracts.

### D2 — Choose and specify the design

- [ ] Compare a minimal token adoption with a fuller account-content reorganization
      within unchanged routes/shell. Recommend one and explain the real tradeoff.
      Complete a recommended draft without routine clarification stops; flag only
      owner decisions affecting brand, shared foundation or behavior.
- [ ] Specify all seven views: user task, content order, desktop/tablet/mobile
      layout, components/tokens, Vietnamese copy, keyboard/focus behavior, states,
      responsive changes and implementation files. Include long text, unavailable
      social linking, server errors, invalid reset, empty badges and avatar failure.
- [ ] Resolve the mobile account menu, duplicated security presentation and honest
      unsupported controls explicitly. Keep existing deep links/tab query behavior.
- [ ] Plan `tct-theme`, `_PageHeader`/`_StateMessage` reuse and feature-scoped CSS.
      Shared tokens/layouts remain read-only. A missing component becomes a separate
      proposal with consumers/contrast; it is not silently added to the foundation.

### D3 — Deliver something reviewable

- [ ] Create `docs/ui-audits/2026-11-08-account-ui-research.md`: current findings,
      sources, severity, evidence, assumptions and unverified states.
- [ ] Create `docs/design-system/account-ui-design.md`: recommended design,
      screen/state specs, contracts appendix, token/component and file maps. Its
      opening status is **Draft — awaiting owner review**, not self-approved.
- [ ] Create `docs/design-system/account-ui-preview.html`: clearly labelled static
      synthetic preview of all seven screens, login/register, representative error,
      pending and success states, desktop/mobile arrangements. Load real team CSS
      and existing assets; no network submissions, credentials, live endpoints,
      separate palette, new CDN/package or replacement design system.
- [ ] Create `docs/superpowers/plans/2026-11-08-account-ui-implementation.md` in
      English: exact files/batches, dependencies, state/DOM contracts, meaningful
      regressions, checks and unresolved boundaries. No empty TBD implementation.
- [ ] Review the preview in a browser, verify links/paths and hand off in Vietnamese
      with a short recommendation and remaining owner decisions. A preview is
      design evidence; it does not certify the future Razor implementation.

**Stage gate:** the owner reviews the design/preview and tells Gemini to implement
it. Shared/global/backend proposals require explicit scope authority separately.

## Stage I — Gemini 3.8 Flash High: approved local implementation

Mode: `IMPLEMENT_LOCAL` only after activation above. Read the approved design,
research and implementation plan. If source has moved, reconcile the exact
affected contracts and design deltas before editing; do not redo accepted research.

Default allowed implementation boundary:

- The seven Account views above; approved page-specific partials only under
  `Views/Account/` when explicitly named in the Stage D file map.
- Proposed new scoped assets `wwwroot/css/account-auth-ui.css`, `account-ui.css`,
  and, if needed, `wwwroot/js/account-auth-ui.js`, `account-ui.js`. The Stage D plan
  must confirm which are needed, wrapper selectors, asset order and handler owners.
- Meaningful Account UI rendering/behavior tests following existing test fixtures.
- Account design documents, an execution ledger, relevant bug-memory entries if
  actual bugs are fixed, and only the applicable adoption-tracker rows.

Read-only foundation: `account.css`, `auth-stitch.css`, `tct-learning-ui.css`,
`layout.css`, `site.css`, both shared layouts and shared partial implementations.
They may be reused; changing them is a separate scope decision. Controllers,
services, ViewModels, models/migrations, Program/appsettings/project/dependency
files and unrelated screens remain outside the default boundary.

### I0 — Capture baseline and implementation checklist

- [ ] Confirm owner acceptance and exact design revision; preserve dirty work.
- [ ] Record contracts and before screenshots in
      `docs/feature-audits/2026-11-08-account-ui-execution-ledger.md`.
- [ ] Run relevant existing regressions once; distinguish baseline failures.

### I1 — AU01 Auth

- [ ] Implement the approved responsive login/register composition, token-backed
      controls, labels, password visibility, current/new-password autocomplete,
      keyboard tab switching and clear errors/pending feedback.
- [ ] Preserve AJAX/native registration behavior, antiforgery, terms behavior,
      blocked-account modal, real redirects and current backend password rules.
- [ ] Verify click/Enter paths do not double submit or drop entered nonsecret
      values after failure. Never log/store passwords or add another UI timer to
      decide login success. Check existing dependency integrity rather than
      upgrading/replacing Bootstrap or SweetAlert as part of this UI change.

### I2 — AU02-AU04 Recovery

- [ ] Adopt one recovery composition with team tokens and standalone asset loading.
      Preserve HTML-helper actions, validation and reset hidden fields.
- [ ] Cover invalid input, generic confirmation, invalid/expired reset, mismatch,
      pending/error/success when supported by current server responses. Do not
      reveal account existence or invent a new result route.
- [ ] Check safe fixture-rendered token states; do not send mail or reset real users.

### I3 — AU05 Profile

- [ ] Implement approved identity/earned-badge presentation, navigation, responsive
      account menu, multipart profile form, avatar preview/error fallback and
      current account/subscription/security entry points.
- [ ] Preserve the prior avatar fix and mobile sidebar inert/focus behavior. Real
      progress stays real; absence of data is not replaced by invented percentages.

### I4 — AU06-AU07 Settings and access denial

- [ ] Implement approved security/linking/danger sections, honest preference states,
      validation/pending feedback and accessible confirmation/focus management.
- [ ] Keep field names, social-link certainty, delete-confirmation guard and role
      access behavior. Never execute deletion or external linking on an existing
      account just to demonstrate the UI. AU07 must remain HTTP 403.

### I5 — Review and acceptance

- [ ] Verify all seven screens/states and one Admin page that consumes account.css;
      smoke-check learner shell and one unrelated learning page for CSS spillover.
- [ ] Run build and focused existing Account/badge/shared-partial regressions, then
      relevant new rendering/JS behavior tests. Run the complete normal app suite
      once at closeout when the UI batches and focused checks pass.
- [ ] Verify at 1440, 768, 375/390, short mobile height and actual 200% zoom. Record
      emulation accurately. Check keyboard, no horizontal clipping, 44px icon
      targets, long Vietnamese/English text and rendered contrast/focus states.
- [ ] Update Account adoption rows only after observed conversion. Shared rules,
      hooks, trust and deployment settings are not changed for a page adoption.
- [ ] Review the diff and sensitive-data exposure; preserve unrelated work. Report
      local results and blocked states in Vietnamese. Leave Git/release stages
      untouched unless the owner separately grants them.

Candidate existing checks (verify against current source before running):

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~ProfileBadgeReadTests|FullyQualifiedName~SharedUiPartialsRenderTests"
```

Tests must exercise form/rendering/behavior/access contracts, not only class names
or hex values. Isolate mutations and fake mail/OAuth in test infrastructure. A
blocked recovery/browser flow remains blocked rather than counted as passed.

## External reference seeds checked during preparation

Opened 2026-11-08. Stage D should research the exact concern further as needed.

- [W3C WAI forms](https://www.w3.org/WAI/tutorials/forms/): labels, grouping,
  instructions and actionable feedback; use as behavior guidance with TCT styling.
- [GOV.UK password input](https://design-system.service.gov.uk/components/password-input/):
  visibility, appropriate autocomplete and password-manager/paste support. Keep
  required confirmation fields and server policy despite different example advice.
- [Bootstrap 5.3 validation](https://getbootstrap.com/docs/5.3/forms/validation/):
  retain native/server feedback and accessible associations; custom tooltip styling
  alone is not accessibility evidence. Check exact asset versions in the page.
- Installed `tct-ui-ux-pro-max` lookup: password visibility, mobile input behavior,
  error recovery and accessible names. Suggestions are not new project policy.

## Preparation status

- [x] Scope, current view/asset dependencies and relevant bug memory checked.
- [x] Official research seeds and installed UX lookup consulted.
- [x] Orchestration plan and English execution prompts prepared.
- [ ] Stage D research/design/preview performed by the owner's selected model.
- [ ] Owner accepts the design; Stage I implementation and acceptance completed.

This preparation changes documentation/prompts only. Its tooling/link checks do
not imply an app redesign, browser acceptance or deployment has taken place.

Authoring verification: `scripts/agent_tooling.py` passed; the complete Python
script suite ran 139 tests in 98.273s (138 passed, 1 skipped, 0 failures). All
three new documents passed UTF-8, relative-link, prompt-language, secret-pattern
and whitespace checks. Tracked application/source files remained unchanged.
