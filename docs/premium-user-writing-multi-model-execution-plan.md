# Premium User Writing - Multi-Model Execution Plan

## 1. Current Task

Implement the Premium User Writing upgrade end-to-end based on:
- `docs/premium-user-writing-feature-plan.md` as the feature spec
- this file as the execution, handoff, and model-assignment plan

The implementation target includes:
- private AI-generated `WritingExercise` content for `Premium` and `Admin`
- locked-shell behavior when an owner later downgrades to `Standard`
- hard-delete of all private Writing content during account deletion
- dedicated persisted quota/logging for writing generation
- server-side duplicate-submit protection for `CreateFromAi`
- dedicated AI generation timeout/token/output-budget handling
- ownership hardening across list, practice, hint, evaluate, and delete flows
- regression coverage strong enough to close the plan with confidence

If `docs/implementation_plan.md` does not exist, skip it and continue.

## 2. Shared Rules For Every Phase

Every model must:
- read `AGENTS.md` first and follow it strictly
- then read, in order:
  - `docs/project-structure.md`
  - `docs/architecture-prioritized-backlog.md`
  - `.ai/context/known-issues.md`
  - `.ai/context/coding-conventions.md`
- treat `docs/implementation_plan.md` as supporting only
- treat `docs/branch-handoff-architecture-hardening.md` as historical only
- treat `docs/premium-user-writing-feature-plan.md` as the feature source of truth
- inspect existing local changes before editing
- stay inside the correct post-refactor boundary and never add new domain logic to `HomeController`
- preserve UTF-8 and avoid Vietnamese text encoding regressions
- avoid editing `Program.cs`, `appsettings.json`, or any `.csproj` unless the user explicitly approves it

If a phase concludes that `Program.cs`, `appsettings.json`, or `.csproj` changes are truly unavoidable:
- stop before editing them
- explain exactly why they are needed
- list the minimum required change
- hand that approval need to the user before continuing

Core repo rules that all phases must keep enforcing:
- thin controllers
- ViewModels for views
- async I/O only
- anti-IDOR on every owned read/write/delete path
- anti-CSRF on every mutation
- `GetCurrentUserId()` from `BaseController`
- `NotFound()` for ownership violations

Relevant local repo skills and workflows for this task:
- Skills:
  - `.agent/skills/ef-migration/SKILL.md`
  - `.agent/skills/api-endpoint/SKILL.md`
  - `.agent/skills/security-audit/SKILL.md`
  - `.agent/skills/ui-component/SKILL.md`
- Workflows:
  - `.agent/workflows/new-feature-flow.md`
  - `.agent/workflows/db-migration-flow.md`
  - `.agent/workflows/code-review-flow.md`
  - `.agent/workflows/bug-investigation.md` when a regression or production-shaped bug appears

## 3. Model Allocation Summary

| Phase | Model | Why this model |
|---|---|---|
| Phase 0 | Gemini 3.1 Pro (High) in Antigravity / Codex | Best for architecture mapping, cross-file reasoning, and risk-based execution prep |
| Phase 1 | GPT-5.3 Codex in Visual Studio / GitHub Copilot | Best for bounded schema and persistence-layer implementation inside the IDE |
| Phase 2 | GPT-5.4 in Codex app | Best for cross-cutting backend slice integration and agentic implementation |
| Phase 3 | GPT-5.3 Codex in Visual Studio / GitHub Copilot | Best for bounded learner-flow UI/ViewModel/controller adjustments |
| Phase 4 | GPT-5.4 in Codex app | Best for lifecycle hardening, account deletion, and regression testing |
| Phase 5 | Gemini 3.1 Pro (High) in Antigravity / Codex | Best for independent review and gap-finding with fresh context |
| Phase 6 | GPT-5.4 in Codex app | Best for fixing review findings and running the final implementation sweep |
| Phase 7 | Gemini 3.1 Pro (High) in Antigravity / Codex | Best for final closure review and 100% completion gate |

## 4. Handoff Protocol Between Models

At the end of every phase, the model must produce a handoff packet in this exact shape:

```markdown
## Phase Handoff
Phase completed: <phase name>
Model used: <model name>

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

The next model must read:
- `docs/premium-user-writing-feature-plan.md`
- `docs/premium-user-writing-multi-model-execution-plan.md`
- the previous phase handoff packet

No phase should silently continue if the previous handoff packet is missing.

## 5. Phase 0 - Architecture Mapping And Ready-To-Implement Handoff

Assigned model:
- Gemini 3.1 Pro (High) in Antigravity / Codex

Primary goal:
- build a precise implementation map so later coding phases do not guess

Area hint:
- `StudyController + WritingService + Services/AI + DbflashcardContext + AccountController + ViewModels/Writing* + Views/Study + TCTEnglish.Tests`

Must read in addition to the global docs:
- `docs/premium-user-writing-feature-plan.md`
- `.ai/context/bug-fix-log.md`
- relevant files under:
  - `TCTEnglish/Controllers/StudyController.cs`
  - `TCTEnglish/Services/WritingService.cs`
  - `TCTEnglish/Services/AI/`
  - `TCTEnglish/Models/DbflashcardContext.cs`
  - `TCTEnglish/Controllers/AccountController.cs`
  - `TCTEnglish.Tests/`

Required skills/workflows:
- `new-feature-flow`
- `security-audit`
- `bug-investigation`

Work to complete:
- map every required feature behavior from the feature plan to exact files and layers
- decide the most likely file set for:
  - schema changes
  - generation service
  - create endpoint
  - downgrade locked-shell flow
  - account deletion hard-delete path
  - quota/logging
  - idempotency / duplicate-submit guard
  - regression tests
- identify whether any unavoidable approval-requiring changes may be needed later
- identify the minimum viable test matrix for the whole feature
- leave a clean "Phase 1 starter map" so coding can begin without re-discovery

Phase 0 deliverables:
- file-by-file implementation map
- recommended order of edits
- list of approval risks if any
- test plan draft
- clean handoff packet for Phase 1

Phase 0 exit gate:
- every major requirement from `docs/premium-user-writing-feature-plan.md` is mapped to at least one file or slice
- no unresolved ambiguity remains except explicit approval items
- Phase 1 can start coding without doing architecture discovery again

## 6. Phase 1 - Schema, Quota Log, And Persistence Foundations

Assigned model:
- GPT-5.3 Codex in Visual Studio / GitHub Copilot

Primary goal:
- implement the additive schema and persistence foundation with minimal scope creep

Area hint:
- `Models + DbflashcardContext + Migrations + persistence-facing tests`

Likely files:
- `TCTEnglish/Models/WritingExercise.cs`
- `TCTEnglish/Models/DbflashcardContext.cs`
- new model(s) for writing generation logging if needed
- `TCTEnglish/Migrations/*`
- `TCTEnglish/Migrations/DbflashcardContextModelSnapshot.cs`
- schema-focused tests in `TCTEnglish.Tests/`

Required skills/workflows:
- `ef-migration`
- `db-migration-flow`

Work to complete:
- add the `UserId` relationship to `WritingExercise`
- add `SourceType` now if the phase confirms it is worth doing while the schema is already open
- configure the `User -> WritingExercise` FK as `DeleteBehavior.NoAction` / `ReferentialAction.NoAction`
- add the public/private-supporting indexes required by the feature plan
- prefer a dedicated persisted writing-generation log table instead of directly reusing `AiRequestLog`
- make the migration additive and rollback-safe
- add schema/regression tests for delete behavior and index/relationship shape where appropriate

Do not do in this phase:
- full create-from-AI service logic
- learner-flow UI changes
- account deletion logic unless it is required only to keep the schema compiling

Phase 1 deliverables:
- additive migration
- updated entity and context mapping
- persisted quota/logging schema ready for backend use
- schema verification notes
- handoff packet for Phase 2

Phase 1 exit gate:
- migration shape is safe and reviewed
- FK delete behavior matches the feature plan exactly
- the next phase can write backend logic without inventing storage design

## 7. Phase 2 - AI Generation Backend Slice

Assigned model:
- GPT-5.4 in Codex app

Primary goal:
- implement the create-from-AI backend slice end-to-end, excluding the learner UI polish

Area hint:
- `StudyController + new WritingGenerationService + Services/AI + ViewModels + writing generation quota/idempotency`

Likely files:
- new `IWritingGenerationService` and implementation
- generation request/response ViewModels
- `TCTEnglish/Controllers/StudyController.cs`
- `TCTEnglish/Services/AI/` only if truly needed inside allowed boundaries
- writing-generation quota/log persistence layer
- tests for service/controller behavior

Required skills/workflows:
- `api-endpoint`
- `security-audit`
- `new-feature-flow`

Work to complete:
- implement the create-from-AI service boundary
- enforce Premium/Admin create entitlement
- enforce persisted daily quota for writing generation
- implement server-side duplicate-submit protection
- pass `CancellationToken` through the generation path
- use dedicated writing-generation timeout/token/output budget rules, not the chat defaults blindly
- parse AI JSON defensively and never save partial/broken data
- keep `IsPublished = false` server-owned
- keep the transaction inside `CreateExecutionStrategy().ExecuteAsync(...)`

Important approval rule:
- if this phase truly cannot be completed without `Program.cs` or config changes, stop and surface the exact required change instead of editing those files silently

Phase 2 deliverables:
- backend create endpoint
- generation service
- quota logging write path
- idempotency / replay protection
- service/controller tests
- handoff packet for Phase 3

Phase 2 exit gate:
- a Premium/Admin user can create a private writing exercise through backend code only
- duplicate submissions do not create duplicate content or double-burn quota
- malformed AI output cannot leave orphaned data

## 8. Phase 3 - Learner Flow Integration And Downgrade Locked Shell

Assigned model:
- GPT-5.3 Codex in Visual Studio / GitHub Copilot

Primary goal:
- integrate the new private content into the existing learner flow with the locked-shell downgrade policy

Area hint:
- `WritingService + StudyController + ViewModels/Writing* + Views/Study + wwwroot/js/writing* + wwwroot/css/writing*`

Required skills/workflows:
- `ui-component`
- `security-audit`
- `new-feature-flow`

Work to complete:
- update list/data/practice-related reads so owner private content can appear in the correct area
- keep public system content fully separated from private owner content
- implement the locked-shell behavior for owners who are now `Standard`
- ensure locked-shell users can still see minimal metadata in `Bài viết của tôi`
- block practice/data/hint/evaluate/delete for downgraded owners until they upgrade again
- preserve the existing public Writing experience for everyone else
- keep new UI inside the correct Writing/Study boundary, not `Views/Home/`

Phase 3 deliverables:
- updated learner flow and UI
- downgrade lock-shell behavior
- entitlement-aware actions
- UI/controller/service verification notes
- handoff packet for Phase 4

Phase 3 exit gate:
- Premium/Admin owner can use their private writing content
- downgraded Standard owner sees locked items only
- no private exercise leaks into the public catalog

## 9. Phase 4 - Lifecycle Hardening, Account Deletion, And Regression Coverage

Assigned model:
- GPT-5.4 in Codex app

Primary goal:
- harden the dangerous lifecycle edges and bring regression coverage to a release-safe baseline

Area hint:
- `AccountController + WritingService + StudyController + DbflashcardContext behavior + TCTEnglish.Tests`

Required skills/workflows:
- `security-audit`
- `bug-investigation`
- `code-review-flow`

Work to complete:
- update the account deletion path to hard-delete all private Writing content before removing the user
- keep the account deletion flow inside execution strategy + transaction safety
- verify ownership rules on every private Writing read/mutate path
- add or extend regression tests for:
  - downgrade entitlement behavior
  - ownership and anti-IDOR
  - create-from-AI quota and duplicate-submit behavior
  - account deletion hard-delete path
  - no-FK-blocker deletion behavior

Phase 4 deliverables:
- hardened account deletion path
- expanded regression suite
- clear test results
- handoff packet for Phase 5

Phase 4 exit gate:
- the feature no longer has obvious lifecycle holes
- account deletion does not fail because of the new Writing FK
- the main risk areas are covered by tests

## 10. Phase 5 - Independent Review Pass

Assigned model:
- Gemini 3.1 Pro (High) in Antigravity / Codex

Primary goal:
- review the implemented work independently and report findings only

Area hint:
- `all changed Premium User Writing files + relevant tests`

Required skills/workflows:
- `security-audit`
- `code-review-flow`

Work to complete:
- perform a fresh review of the changed files against:
  - `docs/premium-user-writing-feature-plan.md`
  - this execution plan
  - `AGENTS.md`
  - the bug-fix patterns already present in the repo
- prioritize bugs, regressions, data-loss risks, entitlement leaks, test gaps, and missing handoff assumptions
- do not implement fixes in this phase unless the user explicitly asks

Phase 5 deliverables:
- severity-ordered review findings
- explicit note if no findings are left
- handoff packet for Phase 6

Phase 5 exit gate:
- the next phase has a clear, bounded list of findings to resolve or confirm as already handled

## 11. Phase 6 - Fix Findings, Verify, And Update Supporting Records

Assigned model:
- GPT-5.4 in Codex app

Primary goal:
- resolve the independent review findings and finish the implementation sweep

Area hint:
- `all files touched by review findings + tests + supporting docs if a verified bug was fixed`

Required skills/workflows:
- `bug-investigation`
- `code-review-flow`

Work to complete:
- fix Phase 5 findings
- rerun the relevant verification slices
- if a real bug was fixed, update:
  - `.ai/context/bug-fix-log.md`
  - `.ai/context/known-issues.md` only when applicable
- make sure the handoff packet clearly states what is now resolved

Phase 6 deliverables:
- finding fixes
- updated verification results
- supporting context updates if applicable
- handoff packet for Phase 7

Phase 6 exit gate:
- all accepted findings are resolved
- remaining risks are explicitly listed and justified
- the project is ready for a true closure review

## 12. Phase 7 - Final Closure Review And 100% Done Gate

Assigned model:
- Gemini 3.1 Pro (High) in Antigravity / Codex

Primary goal:
- decide whether the Premium User Writing plan can be closed completely

Area hint:
- `all changed feature files + all relevant tests + both Writing plan docs + Phase 6 handoff`

Required skills/workflows:
- `code-review-flow`
- `security-audit`

Work to complete:
- review the final state against the feature spec and all phase gates
- answer explicitly:
  - is the feature implemented end-to-end?
  - is the downgrade policy implemented correctly?
  - is account deletion fully hard-delete-safe?
  - is quota/logging settled cleanly?
  - is duplicate-submit protection present on the server?
  - is AI generation config hardening sufficient?
  - are tests strong enough to close the plan?
  - is anything still missing before calling it 100% complete?

Required output:
- `READY TO CLOSE` or `NOT READY TO CLOSE`
- if not ready, list the exact remaining items and the smallest next remediation phase

Phase 7 exit gate:
- the review says `READY TO CLOSE`
- or it provides an exact short remediation list before a final re-run of Phase 7

## 13. Paste-Ready English Prompts

Use the following prompts exactly as the starter text for each model.

### Phase 0 Prompt - Gemini 3.1 Pro (High) in Antigravity / Codex

```text
@workspace Task: Execute only Phase 0 from `docs/premium-user-writing-multi-model-execution-plan.md` for the Premium User Writing implementation.

Area hint: StudyController + WritingService + Services/AI + DbflashcardContext + AccountController + ViewModels/Writing* + Views/Study + TCTEnglish.Tests

Read `docs/premium-user-writing-multi-model-execution-plan.md` first and follow Phase 0 exactly.
Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Then read `docs/premium-user-writing-feature-plan.md`.
Treat `docs/implementation_plan.md` as supporting context only if it exists, and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.
Keep work inside the correct feature boundary unless the task is explicitly cross-cutting.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8. If Vietnamese text or UI/text files are touched, avoid encoding issues and run `python scripts/encoding_guard.py` if it exists.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

Read `.ai/context/bug-fix-log.md` before making conclusions because this feature touches known Writing migration and transaction failure patterns.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-writing-multi-model-execution-plan.md`.
Finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 1 Prompt - GPT-5.3 Codex in Visual Studio / GitHub Copilot

```text
@workspace #solution Task: Execute only Phase 1 from `docs/premium-user-writing-multi-model-execution-plan.md` for the Premium User Writing implementation.

Area hint: Models + DbflashcardContext + Migrations + persistence-facing tests

Read `docs/premium-user-writing-multi-model-execution-plan.md` first and follow Phase 1 exactly.
Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Then read `docs/premium-user-writing-feature-plan.md` and the latest Phase 0 handoff packet.
Treat `docs/implementation_plan.md` as supporting context only if it exists, and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.
Keep work inside the correct feature boundary unless the task is explicitly cross-cutting.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8. If Vietnamese text or UI/text files are touched, avoid encoding issues and run `python scripts/encoding_guard.py` if it exists.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

Read `.agent/skills/ef-migration/SKILL.md` and `.agent/workflows/db-migration-flow.md` before editing schema.
Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-writing-multi-model-execution-plan.md`.
Implement the phase end-to-end, run relevant verification if available, and finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 2 Prompt - GPT-5.4 in Codex app

```text
Task: Execute only Phase 2 from `docs/premium-user-writing-multi-model-execution-plan.md` for the Premium User Writing implementation.

Area hint: StudyController + new WritingGenerationService + Services/AI + ViewModels + writing generation quota/idempotency

Read `docs/premium-user-writing-multi-model-execution-plan.md` first and follow Phase 2 exactly.
Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Then read `docs/premium-user-writing-feature-plan.md` and the latest Phase 1 handoff packet.
Treat `docs/implementation_plan.md` as supporting context only if it exists, and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.
Keep work inside the correct feature boundary unless the task is explicitly cross-cutting.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8. If Vietnamese text or UI/text files are touched, avoid encoding issues and run `python scripts/encoding_guard.py` if it exists.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested. If Phase 2 cannot be completed without one of those changes, stop and explain the exact minimal approval needed instead of editing them.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

Read `.agent/skills/api-endpoint/SKILL.md`, `.agent/skills/security-audit/SKILL.md`, and `.agent/workflows/new-feature-flow.md` before editing the endpoint slice.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-writing-multi-model-execution-plan.md`.
Complete the phase end-to-end, run relevant verification, and finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 3 Prompt - GPT-5.3 Codex in Visual Studio / GitHub Copilot

```text
@workspace #solution Task: Execute only Phase 3 from `docs/premium-user-writing-multi-model-execution-plan.md` for the Premium User Writing implementation.

Area hint: WritingService + StudyController + ViewModels/Writing* + Views/Study + wwwroot/js/writing* + wwwroot/css/writing*

Read `docs/premium-user-writing-multi-model-execution-plan.md` first and follow Phase 3 exactly.
Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Then read `docs/premium-user-writing-feature-plan.md` and the latest Phase 2 handoff packet.
Treat `docs/implementation_plan.md` as supporting context only if it exists, and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.
Keep work inside the correct feature boundary unless the task is explicitly cross-cutting.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8. If Vietnamese text or UI/text files are touched, avoid encoding issues and run `python scripts/encoding_guard.py` if it exists.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

Read `.agent/skills/ui-component/SKILL.md` and `.agent/skills/security-audit/SKILL.md` before editing UI or access-sensitive learner flow code.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-writing-multi-model-execution-plan.md`.
Implement the full Phase 3 slice end-to-end, run relevant verification if available, and finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 4 Prompt - GPT-5.4 in Codex app

```text
Task: Execute only Phase 4 from `docs/premium-user-writing-multi-model-execution-plan.md` for the Premium User Writing implementation.

Area hint: AccountController + WritingService + StudyController + DbflashcardContext behavior + TCTEnglish.Tests

Read `docs/premium-user-writing-multi-model-execution-plan.md` first and follow Phase 4 exactly.
Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Then read `docs/premium-user-writing-feature-plan.md`, `.ai/context/bug-fix-log.md`, and the latest Phase 3 handoff packet.
Treat `docs/implementation_plan.md` as supporting context only if it exists, and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.
Keep work inside the correct feature boundary unless the task is explicitly cross-cutting.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8. If Vietnamese text or UI/text files are touched, avoid encoding issues and run `python scripts/encoding_guard.py` if it exists.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

Use `.agent/skills/security-audit/SKILL.md`, `.agent/workflows/bug-investigation.md`, and `.agent/workflows/code-review-flow.md` as relevant.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-writing-multi-model-execution-plan.md`.
Complete the phase end-to-end, run relevant verification, and finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 5 Prompt - Gemini 3.1 Pro (High) in Antigravity / Codex

```text
@workspace Task: Execute only Phase 5 from `docs/premium-user-writing-multi-model-execution-plan.md` for the Premium User Writing implementation.

Area hint: all changed Premium User Writing files + relevant tests

Read `docs/premium-user-writing-multi-model-execution-plan.md` first and follow Phase 5 exactly.
Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Then read `docs/premium-user-writing-feature-plan.md`, `.ai/context/bug-fix-log.md`, and the latest Phase 4 handoff packet.
Treat `docs/implementation_plan.md` as supporting context only if it exists, and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
Do not edit code in this phase unless the user explicitly asks.
Focus on review findings first: bugs, regressions, data-loss risk, ownership gaps, entitlement gaps, missing tests, or plan mismatches.

Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.
Use `.agent/skills/security-audit/SKILL.md` and `.agent/workflows/code-review-flow.md` if relevant.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-writing-multi-model-execution-plan.md`.
Finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 6 Prompt - GPT-5.4 in Codex app

```text
Task: Execute only Phase 6 from `docs/premium-user-writing-multi-model-execution-plan.md` for the Premium User Writing implementation.

Area hint: all files touched by review findings + tests + supporting docs if a verified bug was fixed

Read `docs/premium-user-writing-multi-model-execution-plan.md` first and follow Phase 6 exactly.
Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Then read `docs/premium-user-writing-feature-plan.md`, `.ai/context/bug-fix-log.md`, and the latest Phase 5 handoff packet.
Treat `docs/implementation_plan.md` as supporting context only if it exists, and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.
Keep work inside the correct feature boundary unless the task is explicitly cross-cutting.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8. If Vietnamese text or UI/text files are touched, avoid encoding issues and run `python scripts/encoding_guard.py` if it exists.
Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

Use `.agent/workflows/bug-investigation.md` and `.agent/workflows/code-review-flow.md` as needed.

Do not start later phases.
At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-writing-multi-model-execution-plan.md`.
Complete the phase end-to-end, run relevant verification, update supporting bug context only if applicable, and finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```

### Phase 7 Prompt - Gemini 3.1 Pro (High) in Antigravity / Codex

```text
@workspace Task: Execute only Phase 7 from `docs/premium-user-writing-multi-model-execution-plan.md` for the Premium User Writing implementation.

Area hint: all changed feature files + all relevant tests + both Writing plan docs + Phase 6 handoff

Read `docs/premium-user-writing-multi-model-execution-plan.md` first and follow Phase 7 exactly.
Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Then read `docs/premium-user-writing-feature-plan.md`, the latest Phase 6 handoff packet, and any final verification evidence from previous phases.
Treat `docs/implementation_plan.md` as supporting context only if it exists, and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
This is a closure review only. Do not edit code unless the user explicitly asks.

Review whether the implementation is 100% complete and whether the plan can be closed.
You must explicitly answer `READY TO CLOSE` or `NOT READY TO CLOSE`.
If not ready, list the exact remaining items and the smallest remediation phase needed next.

At the end, produce the exact `## Phase Handoff` packet required by `docs/premium-user-writing-multi-model-execution-plan.md`.
Finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, and remaining risks or blockers.
```
