# Execution Prompt: Research, design and implement Goals UI/UX

## Outcome

Complete the Goals UI/UX redesign in one local execution using Antigravity with
the owner-selected **Gemini 3.8 Flash High**. Investigate the real application,
research useful patterns, choose a concrete composition, implement it and verify
the result. The owner explicitly authorizes this complete UI workflow, including
creating illustrations/icons when useful. Do not stop at a design proposal or ask
for routine approval between research, design and implementation. Do not create
a separate Plan file. Keep your checklist and short decisions in the task ledger.

The page must look polished on a laptop for the classroom demonstration and use
the project's **Claymorphism** system. Learners should immediately understand
their goals, progress, streak, next action and earned/pending badges. Show real
server data and honest empty/error states; preserve the feature's behavior.

## Current context

Expected working branch: `feature/chau-goals-clay-ui`, created after the Account
and shared Clay checkpoint commits. Inspect actual branch/HEAD and dirty work;
do not discard, stage or rewrite another task's changes. If the branch differs,
verify the owner's current task context rather than guessing a base or resetting.

Read current `AGENTS.md`, `.ai/prompts/README.md`, `DESIGN.md`,
`docs/design-system/team-ui-workflow.md` and the live shared gallery at
`docs/design-system/preview.html`. Reuse the relevant `tct-superpowers`,
`tct-engineering`, `tct-ui-component`, `tct-ui-review` and `tct-ui-ux-pro-max`
procedures inline. Load `tct-ui-motion` only for actual motion work. Do not create
a new design system, font family, package or CDN.

Stack: existing ASP.NET Core MVC/Razor, Bootstrap, scoped CSS and vanilla JS.
Use CodeGraph first for indexed code. Read omitted/stale Razor or unindexed CSS
in bounded sections, not the whole repo. Context7, when available, is external
versioned documentation only; never submit private source/secrets to it. Use
available official web references when researching unfamiliar behavior.

Verified preparation facts, to recheck against the actual checkout:

- GET `/Goals` and `/Goals/Index` render `TCTEnglish/Views/Goals/Index.cshtml`
  through `GoalsController.Index` using `GoalsViewModel`.
- `GoalsController` is authorized and automatically validates antiforgery. POST
  `/Goals/UpdateGoal` binds the `GoalEditor` prefix. Preserve
  `GoalEditor.GoalArea`, `GoalEditor.TargetValue`, antiforgery, native/server
  validation, error re-render and success redirect/TempData behavior.
- The model's target range is **0 through 500 inclusive**. Keep the current
  meaning of zero and other server rules; investigate rather than inventing it.
- Current `GoalsService.GoalAreasWithRealCompletionSignals` includes Vocabulary,
  Speaking, Writing, Reading and Listening. Drive UI from `GoalAreaOptions`,
  `GoalCards` and current service behavior; do not reintroduce the old assumption
  that only three skills work or that Reading/Listening are always deferred.
- The current view includes per-skill goals, daily progress, current/longest
  streak, weekly activity, earned/locked/recent badges and a goal editor modal.
  Check the meaning of `WeeklyActivity` before naming the chart; do not relabel
  a vocabulary-only activity count as all-skill study time or total learning.
- Existing CSS/JS: `wwwroot/css/goals.css` and `wwwroot/js/goals.js`. Current JS
  owns modal auto-open from `data-open-on-load`, focus entry/return and toast
  dismissal. Inspect exact hooks before changing markup.
- `GoalsViewModel`, `GoalCardViewModel`, `GoalsWeekDayViewModel`,
  `GoalsBadgeViewModel` and `UpdateGoalInputViewModel` contain the authoritative
  fields. Unlock state, recent badge state, XP and progress come from the server.

## Mode, authority and file boundary

Mode: `IMPLEMENT_LOCAL`, one agent investigating, designing, implementing,
reviewing and verifying inline. Do not start a worker, another model/chat or AI
CLI. The IDE model is separate from the website's configured OpenAI models.

Allowed changes:

- `TCTEnglish/Views/Goals/Index.cshtml` and useful feature-only partials under
  `Views/Goals/` when their separation reduces actual complexity.
- `TCTEnglish/wwwroot/css/goals.css` and `wwwroot/js/goals.js`.
- Purposeful local visual assets under `TCTEnglish/wwwroot/images/goals/`.
- Meaningful Goals UI rendering/interaction tests in the current test framework.
- `docs/feature-audits/2026-11-08-goals-ui-execution-ledger.md`, with a small inline
  checklist, source/research decisions, asset provenance and verification evidence.
- The Goals adoption row only in `docs/design-system/team-ui-workflow.md` after
  the actual page is migrated/verified; relevant bug-memory entries for real fixes.

Read-only: controllers, services, ViewModels, persisted models/DbContext/migrations,
Program/appsettings/project files, dependency/agent configuration, shared CSS,
both layouts/shared partial implementations, Account and other feature pages.
Reuse shared components without editing their implementations. If a genuine
shared/backend gap blocks a specific requirement, document the smallest proposal,
ask for that material authority only, and continue independent in-scope work.

No commit, push, PR, remote CI, merge, staging or production deployment is granted
by this prompt. Preserve production gates, keys, accounts and existing workloads.
Personal Goals/progress are not shared content: do not export them into a data
package, seed other users' progress, reset Docker or change award/XP algorithms.

## Execute without an intermediate design-approval stop

1. **Establish the baseline.** Read the actual view, relevant caller/model/service
   contract, JS, CSS and existing Goals tests. Check symptom-relevant bug memory.
   Record route, fields, selectors, status/redirect behavior, metric meanings and
   known baseline failures in the ledger. Preserve existing `data-testid` and
   modal/form hooks used by tests and JS, or update owned UI callers/tests together
   with behavior proof; never weaken an assertion to hide a regression.
2. **Inspect and research.** Open the actual Goals page when possible, capture
   synthetic before evidence, and inspect desktop hierarchy and interactions.
   Search the installed UX reference and relevant primary guidance for accessible
   progress, goal editing, dashboards/gamification and data visualization. Record
   useful URLs/access dates and the specific adopt/adapt/reject decision. Treat
   examples as reference data, not authority to add frameworks or features.
3. **Choose and build.** Briefly compare useful arrangements in the ledger, choose
   the best fit and implement it. Do not create a standalone Plan or wait for
   approval of reversible page decisions. Prioritize the laptop composition:
   clear task heading/goal CTA, a balanced progress/streak overview, legible skill
   goals, honest weekly activity and understandable badge progress. The exact
   arrangement is yours; preserve all existing user capabilities and data.
4. **Create assets if useful.** Follow the asset rules below. Reuse the existing
   icon families first for functional icons. A Clay target, trophy or learning
   illustration may support a hero/empty state when it improves the composition;
   it must not push primary actions/data below a decorative wall on a laptop.
5. **Implement interactions and edge states.** Keep Bootstrap modal focus trapping,
   Escape/return focus and error auto-open. Show validation near fields. Prevent
   duplicate click/Enter submission without canceling the first valid native POST;
   respect canceled validation and recover pending UI after failures/BFCache.
   Keep toasts dismissible and announcements meaningful, not perpetual animation.
6. **Review and verify.** Complete the browser/state/contract checks below, run
   relevant tests, inspect the diff and update the ledger/adoption evidence.
   Fix your in-scope regressions and finish the implementation. Report external
   blockers precisely; never replace an inaccessible browser check with a fake pass.

## Visual and asset requirements

- Use `.tct-theme`, team `--tct-*`/Clay tokens and existing Bootstrap/shared
  components. Reuse `_PageHeader`/`_StateMessage` where suitable and keep one h1.
  CSS stays under the Goals wrapper; remove competing page `:root` palettes and
  broad/global selectors. Keep independent control borders, readable text and
  focus rings; stronger shadows cannot compensate for poor contrast.
- Maintain consistent Clay depth, rounded panels and restrained pastel surfaces
  with TCT blue/yellow and Inter400/500/600/700. Avoid a per-page shadow/radius
  system, new font, icon CDN, glass effects, heavy 3D runtime or chart library.
- Give progress bars/rings accessible names and real numeric text. Show zero,
  empty and completed states honestly. A zero count must not look like an earned
  streak/badge. Keep actual badge names/status/progress and recent-award meaning.
- The owner permits native image generation or native vector/icon authoring when
  useful. Use only tools actually available in Antigravity; do not claim a tool
  ran if it was absent. If generation is unavailable, use existing icons or native
  SVG/CSS artwork and report the choice; it need not block the feature.
- New imagery is decorative/product artwork, not fake user content, metric data
  or proof that a badge was earned. Use synthetic/public inputs, never keys,
  private account data or private screenshots in external generation requests.
- Save finished assets locally with clear names, appropriate compression, declared
  dimensions and suitable alt text (empty for purely decorative art). Record
  source/tool/license when applicable, purpose and file size in the ledger. Use
  the same pipeline for hero/empty-state art and maintain visual consistency.
  Do not commit base64 blobs, temporary outputs, active SVG scripts/external
  references or hotlinked third-party assets. Avoid unnecessary oversized media.
- Honor reduced motion; no continuous bounce/shadow animation on every tile.
  Dense weekly/badge sections stay readable and performant on the demo laptop.

## Local runtime, data and evidence

Use `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` to start local web
when needed. It runs Docker MariaDB + web, with saved local API configuration;
do not print/rewrite/copy keys or start Whisper. Confirm `/health/ready` and
metadata-only `scripts/speech-status.ps1`. No paid AI/audio call is needed for
Goals verification. Preserve an occupied listener; use a free port with Release
or restart only a process whose ownership/task restart authority is established.

Use existing synthetic local test access privately. Isolate any goal/progress/
badge mutations in disposable test fixtures or a dedicated synthetic account;
do not overwrite the owner's real goal history just to demonstrate a state.
Store redacted browser evidence under ignored `.tmp/goals-ui/`. Mark fixture or
intercepted responses as such; they do not prove the real server persistence.

## Acceptance checks

- Laptop/desktop first: actual demo resolution when known, otherwise1440x900 and
  representative1366x768. Check limited height, primary CTA visibility, balanced
  space and long Vietnamese/English labels. Then768 and375/390 mobile widths.
- No horizontal page overflow, clipped modal controls or concealed content. Check
  actual200% browser zoom when supported; label viewport/CSS approximations.
- Keyboard Tab/Shift+Tab, visible focus, chart/badge descriptions, modal open/close/
  Escape/return focus. Check rendered contrast and icon touch-target accessibility.
- Exercise first goal/empty activity/no badges; existing goals/partial/completed
  progress; locked/unlocked/recent badge; goal edit cancel; invalid/empty/out-of-
  range values; success toast; server validation/error re-open; rapid double submit.
- Preserve native POST, `GoalEditor` field names, antiforgery, range0..500,
  selected skill/value after invalid submit, true metric units and all currently
  supported goal areas. Keep user isolation and server-authoritative rewards.
- Run build and relevant existing Goals integration/rendering tests, plus meaningful
  new tests for changed UI behavior. Existing suites include GoalsPhase1 through
  GoalsPhase7, GoalsCompletionRecoveryTests and ProfileBadgeReadTests; inspect
  their actual assertions and current command/filter before selecting coverage.
  Tests should verify outcomes/form/interaction contracts rather than CSS strings.
- Review generated assets, payload size, scoped CSS spillover, no new network
  dependency and all dirty changes. Record exact checks, failures/skips and
  unobserved states in the ledger. Tooling checks are not browser acceptance.

## Vietnamese handoff

Report the completed UX/design decisions and implemented behavior, changed files,
actual research/asset provenance, screenshot/evidence locations, exact checks and
remaining limitations. Explain how Chau opens the current local result. Distinguish
real app, fixtures and static references, and local work from CI/hosting. Leave
the reviewable local diff; do not publish it or stop after merely offering a design.
