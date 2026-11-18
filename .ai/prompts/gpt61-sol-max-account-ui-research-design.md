# Execution Prompt: Account UI research and design — GPT 6.1 Sol Max

Owner update 2026-11-08: the selected visual direction is **Claymorphism for all
redesigned pages**, with laptop/desktop demo priority. Read the current DESIGN.md
and live shared gallery. Evaluate alternative compositions within this direction;
do not propose a competing flat theme or a replacement design system. Existing
form/security contracts and this prompt's analysis-only boundary remain intact.

## Outcome

Research the current TCT English Account experience and deliver a concrete,
reviewable design that Gemini 3.8 Flash High can later implement without guessing
layouts, states, component choices or form/JavaScript contracts. Use the existing
team design system. The owner chooses GPT 6.1 Sol Max in the IDE for this stage;
do not launch another model or silently substitute one. The application's text
and speech model configuration is unrelated and stays unchanged.

## Context and evidence

Read `AGENTS.md`, `.ai/prompts/README.md`, `DESIGN.md`,
`docs/design-system/team-ui-workflow.md`, and the active Stage D of
`docs/superpowers/plans/2026-11-08-account-ui-design-and-implementation.md`.
Use `.ai/templates/execution-prompt.md` for any new handoff prompt. Load the
relevant TCT engineering/UI skills, applying their procedures inline.

Expected branch: `feature/chau-ui-design-system`; preparation baseline `eed4e2c`.
Verify the actual checkout and preserve all existing work. Design-system PR #130
and RAG PR #131 were already merged at that baseline. Do not follow older
local-only ledger statements as current publication status.

Scope: AU01-AU07 in the plan: Auth (login/register), ForgotPassword,
ForgotPasswordConfirmation, ResetPassword, Profile, Settings and AccessDenied,
all under `TCTEnglish/Views/Account/`. Current entry routes are in the plan;
recovery confirmation is a flow result, not an assumed standalone GET.

Known preparation evidence, to recheck where it affects a decision:

- Auth/recovery are standalone; Profile/Settings use the learner layout and
  account navigation. AccessDenied uses `_Layout` and its action returns 403.
- `account.css` also styles Admin through `_AdminLayout.cshtml`. Recovery has
  private inline palettes. Account pages still load legacy fonts/styles.
- Important prior fixes include valid avatar preview/fallback, closed mobile
  sidebar inert state, keyboard/ARIA synchronization, honest earned badges and
  progress, safe local-password handling and truthful social-link certainty.
- Notification checkboxes exist in Settings; their persistence has not been
  established. Do not infer working settings from visible controls.
- No current rendered Account audit was done while preparing this prompt.

Use CodeGraph first for indexed source. If it returns only the Razor header,
read the omitted sections directly and record that concrete coverage gap. Use
Context7 only if exposed, for versioned external documentation, never private
source or secrets. Do not rebuild/index automatically or claim missing tools ran.

## Mode, authority and boundaries

Mode: `ANALYSIS_AND_DESIGN_ONLY`. When the owner launches this prompt, permitted
work is source inspection, web research, safe local browser inspection, written
design and a synthetic static preview. Application files remain read-only.
One agent performs research, design, review and verification sequentially.

No commit, push, PR, remote CI, merge, hosting or deployment authority is granted.
Keep production OFF. Preserve the current branch/work, accounts, local database,
saved keys and workloads. Do not modify native trust or global configurations.

Follow D0-D3 in the plan. Do not implement the app, create controller endpoints,
change password/authentication policies, add data persistence, reset accounts,
send real email, initiate external OAuth or make paid AI/audio requests. Do not
copy live keys, recovery tokens, private user data or account credentials into
web queries, documents, screenshots or the preview. Use synthetic examples.

## Deliverables

Create exactly these task outputs, adapting existing files if they already exist
without discarding previous evidence:

1. `docs/ui-audits/2026-11-08-account-ui-research.md`: a current-state audit and
   research findings with source URLs/access dates, observed evidence and blockers.
2. `docs/design-system/account-ui-design.md`: the implementation specification,
   opening status **Draft — awaiting owner review**. It may be Vietnamese for the
   owner; technical contracts and identifiers stay exact.
3. `docs/design-system/account-ui-preview.html`: a clearly labelled static preview
   with all seven screens, key states and responsive arrangements. Load actual
   shared CSS/existing assets. Use synthetic data and no live form/network actions.
   Do not make a new design system or add third-party dependencies.
4. `docs/superpowers/plans/2026-11-08-account-ui-implementation.md`: the final,
   English, source-specific implementation checklist for Gemini. Include exact
   files, dependencies, behavior tests and acceptance evidence per batch.

Screenshots and exploratory artifacts belong in ignored `.tmp/account-ui/`.
Do not modify unrelated report/plan files already present in the working tree.

## Research and design procedure

1. Establish branch/HEAD, dirty-work boundaries, tools and relevant history. Read
   the symptom-relevant Account entries in `.ai/context/bug-fix-log.md` and
   `.ai/context/known-issues.md`; do not treat old fixed symptoms as current bugs.
2. Trace each view to AccountController, bound ViewModels, form actions, validation,
   inline JS and asset consumers. Build a contract appendix with exact field names,
   selectors/IDs, callbacks, tab/deep-link behavior and response/redirect shapes.
   Include HTML-helper forms, antiforgery and hidden reset Token/Email.
3. Inspect actual pages where safe. Use the current local launcher and saved keys
   automatically; preserve listeners and use a free port if needed. Verify readiness
   and metadata-only speech status. Use existing synthetic local test access, never
   paste credentials into chat. Safe local login is allowed. For reset/confirmation/
   blocked states, use an isolated existing fixture or document blocked coverage;
   never manufacture production tokens or mutate existing real users for evidence.
4. Research primary guidance for accessible forms, recovery/error feedback,
   password usability and responsive account navigation. Use the installed
   `tct-ui-ux-pro-max` search for targeted UX questions without regenerating DESIGN.md.
   Reuse the official W3C/GOV.UK/Bootstrap sources linked in the plan. Record each
   useful decision as source -> finding -> affected screen -> adopt/adapt/reject.
   Visual references may inform content arrangement but cannot replace TCT brand,
   tokens, Inter, existing icons, Bootstrap or Razor. Do not import reference code,
   libraries, branding or new features. Preserve required confirm-password and
   server policies even when an external example recommends something different.
5. Compare two realistic source-compatible approaches: modest token adoption and
   a more substantial page-content reorganization within unchanged routes/shell.
   Recommend one, explain tradeoffs briefly, and produce a concrete draft. Avoid
   routine questions already answered by the repo. Separate owner decisions that
   affect shared foundation/brand/behavior from reversible page-level decisions.
6. Specify every screen and relevant state using the requirements below. Build
   a static preview that matches the recommended specification, not a generic
   mood board. Inspect the preview in an available browser and record what ran.
7. Write Gemini's ordered implementation checklist and self-review the complete
   deliverables against all seven screens. End with a Vietnamese handoff; stop
   before app edits. Owner approval and a later prompt launch activate Stage I.

## Required design detail

For each AU ID supply: main user task; layout/content order at 1440/768/375-390;
short-height behavior; component/token mapping; Vietnamese product copy; focus,
Tab, Escape and pending/error behavior; exact implementation files; contracts to
preserve; and acceptance scenarios. State which states were observed, fixture-only,
inferred from source or blocked. Define appropriate empty states, not empty states
for screens that cannot be empty.

- Auth: login/register active tab, social sign-in entry points, input/autofill,
  password visibility, terms, validation, duplicate-submit/network errors and
  blocked-account feedback. Keep backend password requirements unchanged; expose
  existing inconsistencies as findings, not as permission to change security.
- Recovery: one recognizable composition across request/confirmation/reset,
  generic non-enumerating messaging, expired/invalid state and login return path.
- Profile: real identity, avatar upload/fallback, earned badges, empty data,
  navigation/deep links, and existing subscription/security entry points.
- Settings: security/linking/danger sections, accurate linked/unconfirmed states,
  real save feedback, confirmation protection and explicitly identified controls
  with no persistence. Propose honest disabled/informational presentation where
  appropriate; leave new backend work outside the implementation boundary.
- AccessDenied: clear explanation and useful internal navigation, retaining 403.

Use team tokens/classes and scoped styles. Default new assets are
`wwwroot/css/account-auth-ui.css`, `account-ui.css` and, only if needed,
`wwwroot/js/account-auth-ui.js`, `account-ui.js`. Specify initialization/ownership
when moving inline handlers. Keep legacy shared stylesheets and shared layouts
read-only. Missing shared tokens/components need a separate documented proposal;
they must not block independent page decisions or be silently implemented.

## Acceptance and handoff

- All seven screens have actionable specs, state coverage and exact file mapping.
- Source contracts are verified; unresolved backend/shared dependencies are clear.
- Primary research is cited with decisions, not a collection of unexplained links.
- Preview covers the recommended composition and key states without live actions.
- Responsive, keyboard, contrast and zoom checks have real evidence or explicit
  limitations. A static mockup does not prove Razor rendering, RBAC or SMTP/OAuth.
- Gemini's plan is ordered and testable, with no unspecified implementation TBDs
  in the recommended in-scope design. Outstanding owner decisions are isolated.
- No runtime/source/config/data changes or publication took place.

Report in Vietnamese: recommended design and why, the four output paths, how to
open the preview, actual source/browser/research checks, unresolved states and
specific owner decisions. Do not mark the design approved yourself, fabricate
model/tool use or provide hidden chain-of-thought. Do not continue into Gemini's
implementation stage in this session.
