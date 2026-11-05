# Claude Code Execution Prompt: TCT English Shared Design System

**Owner and reviewer:** Nguyen Bao Chau (Chau), team lead.
**Execution client:** Claude Code; retain the model selected by the user.
**Mode:** `IMPLEMENT_SHARED_FOUNDATION`, local execution by one agent that also reviews its work.
**Language:** Write execution prompts in English. Communicate with the owner and
write the final handoff report in Vietnamese. Keep existing Vietnamese product
copy unless a copy change is explicitly within scope.

Apply the [project prompt standard](README.md); the shared
[execution template](../templates/execution-prompt.md) defines the task contract.
This prompt specializes that contract for the design-system task.

## 1. Objective and expected outcome

Each team member will design and implement the screens assigned to them. Your
task is to build a **shared design system and reusable UI foundation**. After
Chau reviews and merges this work, teammates should be able to pull the source
and use consistent colors, typography, spacing, layouts and components while
continuing to implement their own screens independently.

Deliver more than a descriptive DESIGN.md: provide working CSS and reusable
components, a component gallery, collaboration guidance and agent rules. Do not
redesign every feature screen or claim that delivering the foundation completes
the website redesign.

Retain ASP.NET Core MVC/Razor, Bootstrap and existing functionality. Support the
English-learning tasks, readable Vietnamese/English content and clear controls.
Use the existing TCT identity as the starting point. Propose brand changes,
primary-color changes or navigation removal separately for Chau to decide;
do not apply those changes implicitly.

## 2. Establish the current context

1. Read `AGENTS.md`, `CLAUDE.md` and the existing `DESIGN.md`. Do not create a
   second `design.md` with different capitalization.
2. Follow `tct-superpowers` for planning, diagnosis, review and verification;
   use `tct-engineering` for source discovery. Canonical skills are under
   `.agents/skills/`; `.claude/skills/` contains adapters to the same procedures.
   Do not repeatedly load both copies.
3. Use `tct-ui-component`, `tct-ui-review` and `tct-ui-ux-pro-max` at the relevant
   UI steps. Load `tct-ui-motion` only when changing motion and
   `tct-tooling-review` when changing agent guidance. Load only the active skill,
   rather than every skill at startup.
4. Consult the relevant sections of `docs/project-structure.md` and
   `.ai/context/coding-conventions.md`. Use `.ai/README.md` for a concrete routing
   question. Before fixing a bug, read the newest symptom-relevant entries in
   known-issues and bug-fix-log.
5. If `.codegraph/` exists, use CodeGraph first for indexed source. Start with
   exact layout/CSS/partial/symbol names and maxFiles1-3 where supported. Fresh
   verbatim output counts as read. Use bounded direct reads for missing/stale
   files or a specific gap. Never initialize/full-rebuild the index or enable
   incremental synchronization without the existing owner-approved local opt-in.

Historical plans and audits are supporting evidence, not current authorization.
Check whether an old finding still exists before acting on it. Do not repeat
completed fixes or restore retired local Whisper behavior. Use one agent for
investigation, implementation, review and verification; never delegate to
subagents, another model, another AI CLI or another chat.

## 3. Authority and scope boundaries

### Authorized local work

- Inspect the Git baseline, update from master using the current workflow and
  create `feature/team-design-system` if there is no suitable topic branch.
  Do not use a `codex/` branch prefix.
- Investigate, write a scoped plan, implement the shared foundation, review it
  and verify it locally. Once the requirements are sufficient, proceed beyond
  an audit or a plan to the authorized implementation.
- Update DESIGN.md; consolidate/add CSS tokens, shared styles and useful Razor
  shared partials. Integrate them minimally into verified learner/Admin layouts.
- Create documentation, the gallery and the handoff prompt. Update the relevant
  UI instructions in AGENTS.md.
- Change presentation JavaScript only when needed for component demonstrations
  or accessibility while preserving existing behavior and contracts.

### Scope limits

- Do not redesign all feature screens or change business rules, scoring,
  AI/quota behavior, access control, routes, HTTP/JSON/SignalR contracts,
  persisted models, database contents or seed data.
- Do not modify `Program.cs`, appsettings, `.csproj`, controllers, services,
  entities, DbContext or migrations to support the gallery. Do not introduce
  a public application route solely for demonstrations.
- Do not switch frameworks, add packages/CDNs/fonts/icon families, change the
  build pipeline or edit pinned vendor files. Reuse existing assets and verify
  actual dependency versions from source.
- Do not change native hooks, MCP configuration, trust records or global IDE
  settings. Keep AGENTS.md as the shared policy source; CLAUDE.md already imports
  it. Do not create four independent copies of the design rules.
- Do not commit, push, create PRs, run remote CI, merge or deploy. Chau will
  authorize those stages after review. Do not change Plesk gates or hosting.

Record the initial branch, HEAD and working-tree status. Preserve existing work
and services. Do not reset, clean, stash or switch branches over existing work.
When it is safe to update, use:

```powershell
pwsh -NoProfile -File scripts/pull-local.ps1
```

If an older checkout does not contain the wrapper, first obtain the master
version containing it according to onboarding, then run the wrapper. A successful
code pull with failed data synchronization is not completion. Preserve data,
report the failure and resolve prerequisites/conflicts before dependent work.

Ask only for genuinely missing information or material decisions, and continue
independent work while waiting. Routine implementation choices within this scope
are authorized. Record out-of-scope work as a dependency/proposal instead of
assuming authority to implement it.

## 4. Execute in reviewable batches

Save a checklist with concrete files, dependencies and acceptance criteria in:
`docs/superpowers/plans/2026-11-05-team-design-system.md`.
Track the baseline, decisions, batches, diff and verification evidence in:
`docs/feature-audits/2026-11-05-team-design-system-ledger.md`.
After each batch, review the diff, perform appropriate checks, record a
checkpoint and continue. Keep context focused on the active batch.

### DS0: Investigate and choose a source-compatible structure

- Map learner/Admin layouts, CSS load order, existing variables/tokens,
  fonts/icons, shared partials and selectors consumed by JavaScript. Read only
  the backend needed to understand a render contract. Do not scan the entire
  repository, vendor tree or unrelated views.
- Select representative shell/Dashboard, catalog, practice, form and Admin table
  screens. Check their real roles and states without redesigning those screens.
- Identify the current authoritative sources, duplication, conflicts and affected
  consumers. Record findings with concrete source or browser evidence.
- Decide whether to reuse existing files or create new ones based on that
  evidence. Do not introduce a competing token/style layer just to satisfy a
  predetermined directory structure.

### DS1: Define standards and implement working tokens

Update the existing DESIGN.md as the canonical visual specification. Preserve
its metadata structure where compatible. Distinguish proposed versus implemented
rules and mandatory standards versus page-level choices.

Specify actual values, token names, scope and examples for:

- Primary/background/surface/text/border, semantic colors and disabled/focus.
- Fonts supporting Vietnamese/English and phonetics; type scale, weights,
  line heights, reading line lengths, heading hierarchy and zoom behavior.
- Spacing, control/target sizes, corner radii, shadows and layers/z-index.
- Header/sidebar/content shell, page titles/breadcrumbs/actions, grids/container
  widths and responsive behavior at existing breakpoints. Avoid conflicting
  sources for layout offsets.
- Purposeful motion, reduced motion, contrast and keyboard focus.

Implement CSS custom properties and scoped class conventions, reusing or
consolidating existing tokens. Document CSS load order, Bootstrap relationships
and override rules. Avoid broad resets or blanket !important overrides. Do not
change root typography or broad selectors without inspecting affected consumers.
Documented tokens must match the working code.

### DS2: Build components and integrate the shared foundation

Provide reusable patterns for:

- Page headings, descriptions, breadcrumbs and action groups.
- Primary/secondary/destructive/icon buttons and pending/disabled states.
- Inputs/selects/checkboxes, labels/helpers/validation, search/filter toolbars.
- Cards/lists, tables/responsive overflow, badges and pagination.
- Modals, toasts/alerts and loading/empty/error/success feedback.

Each component needs its HTML/class structure or partial API, copyable usage
examples, variants/states and guidance on when to use it. Do not wrap every small
element in an abstraction or partial. Use Razor partials where reuse is useful,
with typed ViewModels where necessary; never pass raw EF entities to views.
Reuse existing Bootstrap modal/focus behavior and existing feedback helpers
instead of introducing competing implementations.

Integrate shared CSS minimally into the actual layouts. Preserve navigation and
collapse behavior, Tag Helpers/form actions, antiforgery, validation, role
conditions, IDs/data attributes and DOM relationships consumed by JavaScript,
media controls or SignalR. Do not change behavior to simplify a screenshot.
Check legacy consumers and list screens that require later adoption. Do not
expand into a full-site rewrite or impose the same content layout on Admin and
practice screens.

### DS3: Deliver a working component gallery

Create `docs/design-system/preview.html` as a local gallery, or reuse an existing
suitable gallery. Reference the actual shared CSS/assets through repository-
relative paths. Do not create a different visual implementation just for the
preview or copy pinned vendor files into it.

Demonstrate tokens/typography, buttons, forms/validation, search/filter,
cards/tables/pagination, modals/feedback, UI states and desktop/mobile shells.
Use clearly labeled synthetic data. Forms must not submit to application
endpoints, and the gallery must not call paid AI/media APIs. Verify real Razor/
JavaScript consumers in the application where needed. A static gallery does not
prove server rendering, RBAC or complete application interactions.

### DS4: Document team collaboration and shared agent rules

Create `docs/design-system/team-ui-workflow.md`, covering:

1. Canonical file locations, gallery usage and token/class/partial examples.
2. The page workflow: pull/sync, topic branch, scope identification, component
   selection, scoped CSS, browser/function checks and review.
3. Mandatory versus flexible choices: shared colors/fonts/controls/shell;
   page owners choose a task-appropriate content arrangement within those rules.
4. Responsibility boundaries between shared foundation/layout/tokens and
   feature-specific views/CSS/JS. Use actual team assignments from the repository
   or owner; do not invent contributor assignments.
5. Changes to global CSS/layout require separate shared-impact review. Missing
   tokens/components need a documented proposal and consumer assessment before
   broad use; do not bypass the standards with visually inconsistent copies.
6. How to track legacy screens awaiting adoption and avoid competing definitions.
7. A PR checklist covering responsive behavior, keyboard/focus/contrast, long
   text, states, behavior/selectors/contracts and privacy-safe screenshots.
8. Each machine retains its Docker and key. UI-only changes need no empty data
   package. Actual shared-content changes follow shared-data-workflow; never
   use a database dump for team synchronization.

Update the relevant UI section of AGENTS.md so Codex, Cursor, Claude Code and
Antigravity all read DESIGN.md and the workflow, reuse components and keep CSS
scoped. Preserve security/database/Git/release rules and local trust boundaries.

Create `.ai/prompts/implement-page-with-team-design-system.md` **in English**,
with placeholders for the assigned screens/routes, user task, file/team ownership
and page-specific requirements. Reference the shared standards rather than
copying all of DESIGN.md. Preserve behavior/Git boundaries and require evidence
and a Vietnamese handoff report. Write the teammate workflow in Vietnamese for
the team; keep technical names and code examples exact.

Link the workflow/gallery from an appropriate docs index so teammates can find
them. Avoid multiple documents repeating the same standards.

### DS5: Review, verify and hand off

Review the source/diff yourself. Verify the acceptance criteria below and record
actual results/blockers. Distinguish source inspection, browser checks and
automated tests. Identify unverified pages/roles/states and the page-level
adoption work left to teammates. Never mark unobserved behavior as verified.

## 5. Local execution and verification

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

Reuse the locally saved key. Never ask for keys in chat or print, copy or rewrite
User Secrets during startup. The launcher starts MariaDB and the web; speech uses
OpenAI `gpt-transcribe`, with no local Whisper engine. Check `/health/ready` and
`scripts/speech-status.ps1`. Configured metadata is not proof of live account or
quota acceptance. Do not call paid APIs, send real email, train models, import
YouTube or record learning progress just to inspect UI.

For an occupied port or locked binary, preserve processes whose ownership has
not been established. Use an available port/configuration according to AGENTS.md;
do not claim an older instance is running new code. A missing key does not block
the gallery or non-AI UI checks. If browser/login access is unavailable, continue
source/gallery work and record the limitation. Use authorized local test
accounts; never bypass access controls or modify real accounts to reach Admin.

- Inspect desktop1440, tablet768 and mobile375/390, 200% zoom, long Vietnamese/
  English text, focus/Tab/Escape, active navigation, validation and real overflow.
- Target at least 4.5:1 contrast for normal text and 3:1 for large text. Do not use
  color alone to communicate meaning. Token names/source alone do not prove
  rendered contrast or accessibility acceptance.
- Record gallery and representative page evidence, including learner/Admin
  layouts when accessible. Keep local screenshots under ignored
  `.tmp/team-design-system/`. Do not commit credentials/emails/PII/tokens in
  screenshots. Clearly label synthetic gallery content.
- Run `dotnet build` and relevant smoke/behavior checks. Do not write tests that
  merely assert each CSS color/class. Where presentation JS changes risky
  state/focus/duplicate-submit behavior, add meaningful regressions and establish
  the failing behavior before the fix when appropriate.
- Since this task changes AGENTS.md/agent guidance, run required tooling checks:

  ```powershell
  uv run --no-project python scripts/agent_tooling.py
  uv run --no-project python -m unittest discover -s scripts/tests -v
  ```

  Bootstrap/native hook changes remain out of scope. Record a dependency if one
  is needed rather than silently expanding scope. Tooling checks do not replace
  application/browser tests or prove that another teammate's IDE loaded/trusted
  the instructions.
- For a real bug fix, record root cause and verification in the bug log and
  update a relevant known issue. Ordinary styling work needs no invented bug.

## 6. Completion criteria

- [ ] DESIGN.md contains concrete standards matching working tokens/code and
      identifies mandatory versus page-specific decisions.
- [ ] Shared CSS works in relevant layouts without competing sources of truth;
      document actual impact and legacy screens awaiting adoption.
- [ ] Components provide examples/APIs/states and work with existing Razor/
      Bootstrap conventions.
- [ ] The gallery uses the shared assets and opens successfully; claimed
      desktop/mobile behavior and interactions have evidence beyond a mockup.
- [ ] Teammates can implement their assigned screens without choosing a new
      color/font/spacing/shell system. Shared-file change rules are explicit.
- [ ] AGENTS.md and the English handoff prompt reference the same standards for
      all four clients.
- [ ] Business rules/database/access/routes/contracts remain intact; no secrets/
      PII are exposed and no useful functionality/state is removed for appearance.
- [ ] Relevant build/checks pass, or exact blockers and unverified items are
      recorded without false completion claims.
- [ ] The diff is reviewed; no unauthorized commit/push/remote CI/PR/merge/deploy.

Report in Vietnamese: outcome, changed files, how to open the gallery, how page
owners use the foundation, actual checks/browser evidence, legacy work/limits
and decisions requiring Chau's review. State clearly that the shared foundation
is implemented locally, while the whole website has not been redesigned or
published to GitHub/hosting. Leave the local changes for Chau to review and
authorize commit/PR stages. Finish the authorized work instead of ending with
an offer to implement it later.
