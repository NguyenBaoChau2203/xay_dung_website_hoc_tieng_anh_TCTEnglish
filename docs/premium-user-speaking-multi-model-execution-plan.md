# Premium User Speaking - Multi-Model Execution Plan

## 1. Current Task

Pivot the current in-progress Premium YouTube import work away from `Listening` and into the correct `Speaking` feature boundary.

The end state we want is:

- private user-imported YouTube videos inside `Speaking`
- learner section label `Bài nói của tôi`
- a Speaking-page import panel that remains visible to `Standard`, `Premium`, and `Admin`
- caption-first transcript acquisition
- Gemini fallback only when captions are missing or unusable
- English-only video/transcript support
- no translation
- reuse of the existing Speaking practice surface and speaking progress model
- no continued investment in the old Listening-only scaffold as the final product path

Important current-state note:

The workspace already contains a partial Listening implementation. Do not blindly continue it. Every phase must inspect that partial work first and decide what is worth reusing, porting, or dropping.

## 2. Feature Source Of Truth

Treat these documents as the source of truth for this pivot:

- `docs/premium-user-speaking-feature-plan.md`
- this execution plan

The old Listening docs are now historical context only for salvage:

- `docs/premium-user-listening-feature-plan.md`
- `docs/premium-user-listening-multi-model-execution-plan.md`

## 3. Shared Rules For Every Phase

Every model must:

- read `AGENTS.md` first and follow it strictly
- then read, in order:
  - `docs/project-structure.md`
  - `docs/architecture-prioritized-backlog.md`
  - `.ai/context/known-issues.md`
  - `.ai/context/coding-conventions.md`
- treat `docs/implementation_plan.md` as supporting context only if it exists
- treat `docs/branch-handoff-architecture-hardening.md` as historical context only
- inspect current local changes before editing and avoid overwriting unrelated work
- read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`
- preserve UTF-8
- not edit `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly approved

Core repo rules that every phase must preserve:

- keep work inside the correct post-refactor boundary
- do not add new domain logic to `HomeController`
- do not add new feature screens to `Views/Home/`
- use ViewModels for views
- keep controllers thin
- async I/O only
- anti-IDOR on owned reads and mutations
- anti-CSRF on every mutation
- use `GetCurrentUserId()` / `TryGetCurrentUserId()` from `BaseController`
- return `NotFound()` for ownership violations

Speaking-specific rules for this feature:

- learner label is `Bài nói của tôi`
- the feature belongs to `SpeakingController` and `Views/Speaking/`
- do not continue routing the final product through `StudyController` / `Listening`
- do not keep investing in `ListeningExercise*` as the final product direction
- keep the import UI visible for `Standard`, but gate actual import usage behind Premium/Admin access
- do not add user-selectable `Video Language` or `Subtitle` fields in this phase
- default the feature to English-only validation and reject non-English imports
- no translation in this phase
- Gemini is transcript fallback only
- private imported videos must not leak into the public speaking catalog
- `SaveSpeakingProgress` must be hardened for sentence -> video access ownership

Relevant repo skills and workflows for this task:

- Skills:
  - `.agent/skills/feature-scaffold/SKILL.md`
  - `.agent/skills/speaking-feature/SKILL.md`
  - `.agent/skills/api-endpoint/SKILL.md`
  - `.agent/skills/ef-migration/SKILL.md`
  - `.agent/skills/security-audit/SKILL.md`
  - `.agent/skills/ui-component/SKILL.md`
- Workflows:
  - `.agent/workflows/new-feature-flow.md`
  - `.agent/workflows/db-migration-flow.md`
  - `.agent/workflows/code-review-flow.md`
  - `.agent/workflows/bug-investigation.md`

## 4. Model Allocation Summary

| Phase | Model | Why this model |
|---|---|---|
| Phase 0 | Claude Opus in Antigravity | Best for pivot triage, architecture mapping, and deciding how to salvage the current Listening scaffold |
| Phase 1 | GPT-5.3 Codex in Visual Studio / GitHub Copilot | Best for bounded schema/entity/migration work inside the IDE |
| Phase 2 | GPT-5.4 in Codex app | Best for backend service extraction, transcript import orchestration, and controller/service boundary cleanup |
| Phase 3 | Claude Sonnet in Antigravity | Best for learner flow, UI integration, and keeping the Speaking UX coherent |
| Phase 4 | GPT-5.4 in Codex app | Best for hardening ownership, progress access, cleanup paths, and test coverage |
| Phase 5 | Gemini 3.1 Pro (High) in Antigravity | Best for a fresh review pass with independent context |
| Phase 6 | GPT-5.4 in Codex app | Best for fixing review findings and stabilizing the feature |
| Phase 7 | Claude Opus in Antigravity | Best for the final closure audit and the 100% complete decision |

## 5. Handoff Protocol Between Models

At the end of every phase, the model must produce a handoff packet in this exact shape:

```markdown
## Phase Handoff
Phase completed: <phase name>
Model used: <model name>
Ready for next phase: YES|NO

Scope completed:
- ...

Files changed:
- ...

Key rules/policies implemented:
- ...

Commands run:
- ...

Verification results:
- ...

Open risks or blockers:
- ...

If approval is needed before the next phase:
- ...

Next phase should start from:
- ...
- ...
```

Before saying `Ready for next phase: YES`, each phase must also answer:

- Was the assigned scope actually completed?
- Was verification run or explicitly deferred with reason?
- Are blockers listed clearly?
- Does the handoff give the next model enough context to continue without rediscovery?

The next phase must read:

- `docs/premium-user-speaking-feature-plan.md`
- `docs/premium-user-speaking-multi-model-execution-plan.md`
- the latest previous phase handoff packet

## 6. Phase 0 - Pivot Audit And Salvage Map

Assigned model:

- Claude Opus in Antigravity

Primary goal:

- inspect the current partial Listening worktree and convert it into an exact Speaking implementation map

Area hint:

- `SpeakingController + Areas/Admin/SpeakingVideoManagementController + current partial Listening files + YoutubeTranscriptService + DbflashcardContext + AccountController + Speaking ViewModels/Views + TCTEnglish.Tests`

Required skills/workflows:

- `feature-scaffold`
- `speaking-feature`
- `security-audit`
- `new-feature-flow`
- `bug-investigation`

Work to complete:

- audit the current Listening scaffold and classify each relevant file:
  - keep as-is
  - port into Speaking
  - stop using / supersede
- decide the recommended Speaking schema shape
- identify the exact file set for schema changes, service extraction, UI integration, progress hardening, account cleanup, and tests
- decide the cleanest transcript acquisition design for Speaking using the existing caption-first + Gemini fallback work
- map the English-only validation path and the Standard-visible-but-gated import UX
- produce a clean Phase 1 starter map

Phase 0 exit gate:

- the pivot path from Listening -> Speaking is unambiguous
- every major requirement is mapped to files/layers
- the next phase can start coding without architecture rediscovery

## 7. Phase 1 - Speaking Schema And Entity Refactor

Assigned model:

- GPT-5.3 Codex in Visual Studio / GitHub Copilot

Primary goal:

- make the persistence layer match the new Speaking direction

Area hint:

- `SpeakingVideo + SpeakingSentence + User + DbflashcardContext + Migrations + schema-facing tests`

Required skills/workflows:

- `ef-migration`
- `feature-scaffold`
- `db-migration-flow`

Work to complete:

- extend `SpeakingVideo` for owner-scoped private imports
- add owner/source/transcript/import metadata fields
- decide and implement the minimum-safe handling for no-translation sentence rows
- ensure the schema direction matches English-only import policy without adding user-selectable language/subtitle fields
- add indexes for owner list and duplicate-per-owner protection
- configure `OwnerUserId -> Users` with `DeleteBehavior.NoAction`
- keep the migration additive and safe

Phase 1 exit gate:

- the storage design matches the Speaking feature plan
- private imports have a safe place to live
- the next phase can build backend logic without inventing persistence details

## 8. Phase 2 - Service Extraction And Import Backend

Assigned model:

- GPT-5.4 in Codex app

Primary goal:

- implement the Speaking backend slice for private YouTube import and reduce `SpeakingController` heaviness

Area hint:

- `SpeakingController + speaking service files + YoutubeTranscriptService + YoutubeUrlHelper + OperationResult + admin speaking create flow`

Required skills/workflows:

- `api-endpoint`
- `speaking-feature`
- `security-audit`
- `new-feature-flow`

Work to complete:

- extract or introduce a speaking service boundary
- implement `POST /Speaking/My/Create`
- implement `POST /Speaking/My/Delete`
- keep `GET /Speaking/Practice/{id}` compatible with both public and private items
- reuse caption-first + Gemini fallback transcript acquisition
- enforce English-only acceptance for imported videos/transcripts
- keep duplicate-per-owner protection
- keep no-translation discipline
- keep `Standard` users blocked at usage time with a clear upgrade response instead of hiding the UI
- persist private imported videos into the Speaking domain
- refine existing transcript helper code instead of rewriting it blindly

Important approval rule:

- if this phase truly cannot be completed without editing `Program.cs`, `appsettings.json`, or `.csproj`, stop and surface the exact minimum approval needed instead of editing silently

Phase 2 exit gate:

- a Premium/Admin user can import a private Speaking item from YouTube through backend code
- the implementation lives in the Speaking boundary
- controller growth is reduced, not worsened

## 9. Phase 3 - `Bài nói của tôi` UI And Practice Integration

Assigned model:

- Claude Sonnet in Antigravity

Primary goal:

- integrate the learner-facing Speaking UI without polluting the public catalog

Area hint:

- `Views/Speaking/Index.cshtml + Views/Speaking/Practice.cshtml + Speaking ViewModels + speaking.css/js`

Required skills/workflows:

- `ui-component`
- `speaking-feature`
- `security-audit`

Work to complete:

- add the learner section `Bài nói của tôi`
- add a Speaking-page import panel visually inspired by the user mockup
- simplify that panel to YouTube-only: no `Video Language` field and no `Subtitle` field
- keep the panel visible for `Standard`, `Premium`, and `Admin`
- add empty/loading/error states
- add locked-shell cards for downgraded owners
- surface a Premium upgrade prompt when `Standard` tries to use the panel
- keep public search/filter behavior scoped to public catalog only
- adjust the practice page so private no-translation items do not break UI
- keep the experience mobile-aware and Speaking-first

Phase 3 exit gate:

- the Speaking page shows both public catalog and private owner content correctly
- the import panel is visible to `Standard` and returns a clear Premium upgrade message on use
- private imported items can reach the existing practice surface
- no translation UI breakage remains for user-imported items

## 10. Phase 4 - Security Hardening, Cleanup, And Regression Coverage

Assigned model:

- GPT-5.4 in Codex app

Primary goal:

- harden the risky lifecycle edges before review

Area hint:

- `SpeakingController + speaking service + AccountController + DbflashcardContext behavior + TCTEnglish.Tests`

Required skills/workflows:

- `security-audit`
- `bug-investigation`
- `code-review-flow`

Work to complete:

- verify anti-IDOR across all private Speaking list/read/delete paths
- harden `SaveSpeakingProgress` so sentence writes require accessible parent video
- verify downgrade lock behavior across list/create/practice/delete
- verify Standard-visible import UI still blocks actual import usage correctly
- add account deletion cleanup for private user-imported Speaking content
- add or extend tests for entitlement, Standard upgrade prompting, English-only validation, duplicate-per-owner, caption-first, Gemini fallback, locked-shell downgrade behavior, private practice access, progress endpoint access hardening, and account deletion cleanup

Phase 4 exit gate:

- the main ownership and lifecycle risks are covered
- account deletion does not leave orphaned private Speaking data
- the feature has a release-safe regression baseline

## 11. Phase 5 - Independent Review Pass

Assigned model:

- Gemini 3.1 Pro (High) in Antigravity

Primary goal:

- perform a fresh review and report findings only

Area hint:

- `all changed Speaking-import files + relevant tests + pivot diff from Listening direction`

Required skills/workflows:

- `security-audit`
- `code-review-flow`

Work to complete:

- review the final Speaking implementation against the feature plan, this execution plan, and `AGENTS.md`
- prioritize bugs, regressions, ownership leaks, downgrade-policy gaps, transcript-source issues, no-translation scope creep, and missing tests

Do not implement fixes in this phase unless the user explicitly asks.

Phase 5 exit gate:

- the next phase receives a bounded list of findings or an explicit no-findings verdict

## 12. Phase 6 - Fix Findings And Stabilize

Assigned model:

- GPT-5.4 in Codex app

Primary goal:

- resolve accepted review findings and rerun verification

Area hint:

- `all files touched by review findings + tests + supporting bug context if applicable`

Required skills/workflows:

- `bug-investigation`
- `code-review-flow`

Work to complete:

- fix accepted Phase 5 findings
- rerun the relevant verification slices
- update `.ai/context/bug-fix-log.md` only if a real bug/regression was confirmed and fixed
- update `.ai/context/known-issues.md` only when applicable

Phase 6 exit gate:

- accepted findings are resolved
- remaining risks are explicitly listed and justified
- the project is ready for a true closure review

## 13. Phase 7 - Final Closure Review And 100% Done Gate

Assigned model:

- Claude Opus in Antigravity

Primary goal:

- decide whether the Speaking pivot is fully complete and the plan can be closed

Area hint:

- `all changed Speaking files + all relevant tests + speaking plan docs + latest handoff`

Required skills/workflows:

- `code-review-flow`
- `security-audit`

Work to complete:

- review the final state against the feature spec and all phase exit gates
- answer explicitly:
  - is the feature implemented end-to-end in Speaking?
  - is `Bài nói của tôi` correct?
  - is caption-first behavior implemented correctly?
  - is Gemini fallback used only as fallback?
  - is there still no translation scope creep?
  - are public catalog and private imports separated correctly?
  - is `SaveSpeakingProgress` ownership-safe?
  - is account deletion cleanup complete?
  - are tests strong enough to close the plan?

Required output:

- `READY TO CLOSE` or `NOT READY TO CLOSE`
- if not ready, list the exact remaining items and the smallest remediation phase needed

## 14. Paste-Ready English Prompts

Use the following prompts as the starter text for each model.

### Phase 0 Prompt - Claude Opus in Antigravity

```text
@workspace Task: Execute only Phase 0 from `docs/premium-user-speaking-multi-model-execution-plan.md` for the Premium User Speaking pivot.

Area hint: SpeakingController + Areas/Admin/SpeakingVideoManagementController + current partial Listening files + YoutubeTranscriptService + DbflashcardContext + AccountController + Speaking ViewModels/Views + TCTEnglish.Tests

Read `docs/premium-user-speaking-multi-model-execution-plan.md` first and follow Phase 0 exactly.
Then read `docs/premium-user-speaking-feature-plan.md`.

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Treat `docs/implementation_plan.md` as supporting context only if it exists and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.
Keep work inside the correct feature boundary unless the task is explicitly cross-cutting.

Inspect existing local changes before editing and do not overwrite unrelated work.
Audit the current partial Listening scaffold first and decide what should be kept, ported into Speaking, or superseded.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8. If Vietnamese text or UI/text files are touched, avoid encoding issues and run `python scripts/encoding_guard.py` if it exists.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

This feature must end up as:
- `Bài nói của tôi`
- a Speaking-bound private YouTube import flow
- a Speaking-page import panel that remains visible to `Standard`
- caption-first transcript acquisition
- Gemini fallback only when captions are missing or unusable
- English-only video/transcript acceptance
- no translation

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-speaking-multi-model-execution-plan.md`.
Finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 1 Prompt - GPT-5.3 Codex in Visual Studio / GitHub Copilot

```text
@workspace #solution Task: Execute only Phase 1 from `docs/premium-user-speaking-multi-model-execution-plan.md` for the Premium User Speaking pivot.

Area hint: SpeakingVideo + SpeakingSentence + User + DbflashcardContext + Migrations + schema-facing tests

Read `docs/premium-user-speaking-multi-model-execution-plan.md` first and follow Phase 1 exactly.
Then read `docs/premium-user-speaking-feature-plan.md` and the latest Phase 0 handoff packet.

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Treat `docs/implementation_plan.md` as supporting context only if it exists and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.

Inspect existing local changes before editing and do not overwrite unrelated work.
Refine the schema for the Speaking pivot instead of continuing the old Listening persistence direction.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

Read `.agent/skills/ef-migration/SKILL.md`, `.agent/skills/feature-scaffold/SKILL.md`, and `.agent/workflows/db-migration-flow.md` before editing schema.

Do not add user-facing `Video Language` or `Subtitle` selectors in this phase. The feature should stay English-only by policy, not by user choice.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-speaking-multi-model-execution-plan.md`.
Implement the phase end-to-end, run relevant verification if available, and finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 2 Prompt - GPT-5.4 in Codex app

```text
Task: Execute only Phase 2 from `docs/premium-user-speaking-multi-model-execution-plan.md` for the Premium User Speaking pivot.

Area hint: SpeakingController + speaking service files + YoutubeTranscriptService + YoutubeUrlHelper + OperationResult + admin speaking create flow

Read `docs/premium-user-speaking-multi-model-execution-plan.md` first and follow Phase 2 exactly.
Then read `docs/premium-user-speaking-feature-plan.md` and the latest Phase 1 handoff packet.

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Treat `docs/implementation_plan.md` as supporting context only if it exists and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.

Inspect existing local changes before editing and do not overwrite unrelated work.
Refine and port the useful import logic from the partial Listening work instead of rewriting blindly.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested. If Phase 2 truly cannot be completed without one of those changes, stop and explain the exact minimal approval needed instead of editing them.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

This phase must end with:
- a Speaking-bound private import flow
- caption-first transcript acquisition
- Gemini fallback only when YouTube captions are missing or unusable
- English-only video/transcript acceptance
- no translation
- a clear upgrade response when `Standard` tries to import
- reduced controller heaviness compared with the current `SpeakingController`

Read `.agent/skills/api-endpoint/SKILL.md`, `.agent/skills/speaking-feature/SKILL.md`, `.agent/skills/security-audit/SKILL.md`, and `.agent/workflows/new-feature-flow.md` before editing.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-speaking-multi-model-execution-plan.md`.
Complete the phase end-to-end, run relevant verification, and finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 3 Prompt - Claude Sonnet in Antigravity

```text
@workspace Task: Execute only Phase 3 from `docs/premium-user-speaking-multi-model-execution-plan.md` for the Premium User Speaking pivot.

Area hint: Views/Speaking/Index.cshtml + Views/Speaking/Practice.cshtml + Speaking ViewModels + speaking.css/js

Read `docs/premium-user-speaking-multi-model-execution-plan.md` first and follow Phase 3 exactly.
Then read `docs/premium-user-speaking-feature-plan.md` and the latest Phase 2 handoff packet.

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Treat `docs/implementation_plan.md` as supporting context only if it exists and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

This phase must produce a learner Speaking flow with:
- the required label `Bài nói của tôi`
- a Speaking-page import panel inspired by the user mockup
- no `Video Language` field
- no `Subtitle` field
- a Standard-visible panel with Premium gating on actual use
- locked-shell cards for downgraded owners
- no leakage of private items into public filters
- a practice surface that still works when imported items have no translation text

Read `.agent/skills/ui-component/SKILL.md`, `.agent/skills/speaking-feature/SKILL.md`, and `.agent/skills/security-audit/SKILL.md` before editing UI or access-sensitive code.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-speaking-multi-model-execution-plan.md`.
Implement the full Phase 3 slice end-to-end, run relevant verification if available, and finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 4 Prompt - GPT-5.4 in Codex app

```text
Task: Execute only Phase 4 from `docs/premium-user-speaking-multi-model-execution-plan.md` for the Premium User Speaking pivot.

Area hint: SpeakingController + speaking service + AccountController + DbflashcardContext behavior + TCTEnglish.Tests

Read `docs/premium-user-speaking-multi-model-execution-plan.md` first and follow Phase 4 exactly.
Then read `docs/premium-user-speaking-feature-plan.md`, `.ai/context/bug-fix-log.md`, and the latest Phase 3 handoff packet.

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Treat `docs/implementation_plan.md` as supporting context only if it exists and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

This phase must harden:
- ownership and anti-IDOR
- sentence -> video access checks in `SaveSpeakingProgress`
- downgrade lock behavior
- Standard-visible import gating behavior
- delete behavior
- account deletion cleanup
- regression coverage for caption-first + Gemini fallback, English-only validation, Standard upgrade prompting, and owner-only access

Use `.agent/skills/security-audit/SKILL.md`, `.agent/workflows/bug-investigation.md`, and `.agent/workflows/code-review-flow.md` as relevant.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-speaking-multi-model-execution-plan.md`.
Complete the phase end-to-end, run relevant verification, and finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 5 Prompt - Gemini 3.1 Pro (High) in Antigravity

```text
@workspace Task: Execute only Phase 5 from `docs/premium-user-speaking-multi-model-execution-plan.md` for the Premium User Speaking pivot.

Area hint: all changed Speaking-import files + relevant tests + pivot diff from Listening direction

Read `docs/premium-user-speaking-multi-model-execution-plan.md` first and follow Phase 5 exactly.
Then read `docs/premium-user-speaking-feature-plan.md`, `.ai/context/bug-fix-log.md`, and the latest Phase 4 handoff packet.

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Treat `docs/implementation_plan.md` as supporting context only if it exists and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor boundary.
Do not edit code in this phase unless the user explicitly asks.
Focus on review findings first: bugs, regressions, ownership gaps, downgrade-policy gaps, transcript-source mistakes, no-translation scope creep, or missing tests.
Also check that the Speaking-page import UI stays visible for `Standard` and that non-English imports are rejected correctly.

Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.
Use `.agent/skills/security-audit/SKILL.md` and `.agent/workflows/code-review-flow.md` if relevant.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-speaking-multi-model-execution-plan.md`.
Finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 6 Prompt - GPT-5.4 in Codex app

```text
Task: Execute only Phase 6 from `docs/premium-user-speaking-multi-model-execution-plan.md` for the Premium User Speaking pivot.

Area hint: all files touched by review findings + tests + supporting docs if a verified bug was fixed

Read `docs/premium-user-speaking-multi-model-execution-plan.md` first and follow Phase 6 exactly.
Then read `docs/premium-user-speaking-feature-plan.md`, `.ai/context/bug-fix-log.md`, and the latest Phase 5 handoff packet.

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Treat `docs/implementation_plan.md` as supporting context only if it exists and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

Use `.agent/workflows/bug-investigation.md` and `.agent/workflows/code-review-flow.md` as needed.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-speaking-multi-model-execution-plan.md`.
Complete the phase end-to-end, run relevant verification, update supporting bug context only if applicable, and finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 7 Prompt - Claude Opus in Antigravity

```text
@workspace Task: Execute only Phase 7 from `docs/premium-user-speaking-multi-model-execution-plan.md` for the Premium User Speaking pivot.

Area hint: all changed Speaking files + all relevant tests + speaking plan docs + latest handoff

Read `docs/premium-user-speaking-multi-model-execution-plan.md` first and follow Phase 7 exactly.
Then read `docs/premium-user-speaking-feature-plan.md`, the latest Phase 6 handoff packet, and any final verification evidence from previous phases.

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Treat `docs/implementation_plan.md` as supporting context only if it exists and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor boundary.
This is a closure review only. Do not edit code unless the user explicitly asks.

Review whether the implementation is 100% complete and whether the plan can be closed.
You must explicitly answer `READY TO CLOSE` or `NOT READY TO CLOSE`.
If not ready, list the exact remaining items and the smallest remediation phase needed next.

Specifically review:
- `Bài nói của tôi` flow correctness
- Standard-visible import UI correctness
- caption-first behavior
- Gemini fallback correctness
- English-only enforcement
- no-translation discipline
- public/private catalog separation
- `SaveSpeakingProgress` access safety
- account deletion cleanup safety
- test sufficiency

At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-speaking-multi-model-execution-plan.md`.
Finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```
