# Writing Page Overhaul Plan (v4)

> Status: agent-executable phase runbook  
> Updated: 2026-04-06  
> Goal: let the user tell an AI agent "do Phase N" and have that agent know what to read, what boundary to stay in, which local skills/workflows to load, what to test before stopping, and whether the work is truly complete.

---

## 1. How to use this file

This file is the authoritative phase plan for the Writing overhaul.

Use it like this:

1. Pick one phase only.
2. Send the agent the global prompt in Section 2.
3. Replace `[TASK]` and `[AREA]` with the phase-specific values in Section 9.
4. The agent must execute only that phase, run that phase's verification gate, then stop.

The agent must not silently start a later phase.

---

## 2. Global prompt template

```text
@workspace
Task: [TASK]

Area hint: [AREA]

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Treat `docs/implementation_plan.md` as supporting context only and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Then read `docs/writing-page-overhaul-plan.md` and execute only the requested phase.
Do not start any later phase.

Use the area hint first to identify the correct post-refactor controller/view/service boundary.
Do not add new domain logic to `HomeController`.
Do not add new feature screens to `Views/Home/`.
Keep work inside the correct feature boundary unless the task is explicitly cross-cutting.

Inspect existing local changes before editing and do not overwrite unrelated work.
Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.

Preserve UTF-8. If Vietnamese text or UI/text files are touched, avoid encoding issues and run `python scripts/encoding_guard.py` if it exists.
The user has already pre-approved normal in-scope edits required to complete the requested phase, including direct edits to files such as `Program.cs` when they are genuinely necessary for that phase.
Do not pause to ask for permission for normal in-scope edits.
Only stop to ask if the work would require a destructive database/schema change, an out-of-scope cross-cutting change, secret/credential changes, or a sandbox/tool escalation that the platform itself requires approval for.

Follow core repo rules: use ViewModels for views, keep controllers thin, use async I/O, enforce anti-IDOR and anti-CSRF, use `GetCurrentUserId()` from `BaseController`, and return `NotFound()` for ownership violations.
For namespaces/imports, follow the conventions already used in the touched files and verify legacy `TCTVocabulary.*` references before changing them.

If this is a bug fix, read `.ai/context/bug-fix-log.md` and `.ai/context/known-issues.md` before editing, reuse an old fix only if the root cause matches, and update those files after verification if relevant.

If the platform supports subagents and the task is non-trivial, spawn one explorer subagent for the relevant area first; otherwise continue locally.

Complete the phase end-to-end, run the exact verification gate required by `docs/writing-page-overhaul-plan.md`, and stop after that gate.
Finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, phase gate status, and remaining risks or blockers before the next phase.
```

---

## 3. Locked findings and scope decisions

Future agents should treat these as already-audited realities unless the code changed materially:

- Writing already exists as a real learner flow: `StudyController`, `WritingExercise`, `WritingExerciseSentence`, learner views, hint/evaluate endpoints, and admin CRUD in `Areas/Admin/WritingExerciseManagement*`.
- Writing is not fully finished yet because:
  - Writing logic still sits in `StudyService.Writing.cs`
  - Writing AI still calls OpenAI directly instead of the provider abstraction
  - practice UI works but is not yet polished
  - list attempt/status metadata is fake because durable Writing progress does not exist yet
  - `EnsureWritingSchemaReadyAsync()` still performs request-time migration behavior
- Reusable AI layer:
  - `IAiProviderClient`
  - `GeminiProviderClient`
  - `AiContextMessage`
  - `AiProviderReply`
  - `AiProviderException`
  - `AiOptions`
- Do not reuse Writing through chat conversation abstractions:
  - no `IAiChatService`
  - no `IAiConversationService`
  - no forced `ConversationId` logging model
- Primary polished learner scope is `Beginner + Emails`.

Locked product rules:

- Learner flows stay in `StudyController` + `Views/Study/`
- Admin authoring stays in `Areas/Admin/*`
- No new domain logic in `HomeController`
- No new feature screens in `Views/Home/`
- Until durable progress exists:
  - do not pretend attempts are real
  - do not pretend status is real
  - do not keep fake status filters

Definition of completion:

- Baseline 100% = polished learner Writing flow for `Beginner + Emails` without fake metadata.
- Expanded 100% = baseline + true durable Writing progress/history/status.

---

## 4. Relevant repo-local skills and workflows

Read only what is relevant to the chosen phase.

| Phase | Relevant files |
|---|---|
| 1 | `.agent/workflows/new-feature-flow.md`, `.agent/skills/feature-scaffold/SKILL.md` |
| 2 | `.agent/workflows/new-feature-flow.md`, `.agent/skills/api-endpoint/SKILL.md` |
| 3 | `.agent/skills/security-audit/SKILL.md`, `.agent/skills/api-endpoint/SKILL.md`, `.agent/workflows/code-review-flow.md` |
| 4 | `.agent/skills/ui-component/SKILL.md` |
| 5 | `.agent/workflows/code-review-flow.md`, `.agent/skills/security-audit/SKILL.md` |
| 6 | `.agent/workflows/db-migration-flow.md`, `.agent/skills/ef-migration/SKILL.md`, `.agent/skills/feature-scaffold/SKILL.md` |
| 7 | `.agent/skills/admin-panel/SKILL.md`, `.agent/skills/data-seeder/SKILL.md`, `.agent/workflows/new-feature-flow.md` |
| 8 | `.agent/workflows/code-review-flow.md`, `.agent/skills/security-audit/SKILL.md` |

---

## 5. Phase map

| Phase | Name | Required? | Main outcome |
|---|---|---|---|
| 1 | Service Boundary Extraction | Required | Writing exits `StudyService` into its own service slice |
| 2 | AI Provider Integration | Required | Writing AI uses provider abstraction with fallback |
| 3 | Security and Runtime Hardening | Required | auth/rate-limit/runtime contract is stable |
| 4 | Practice UI Overhaul | Required | focused learner UI |
| 5 | Honest List UI and Cleanup | Required | fake metadata removed, risky/orphan code cleaned up |
| 6 | Durable Writing Progress | Optional | true attempts/status/history persistence |
| 7 | Admin and Content Alignment | Required for broader readiness | admin/content side matches learner promise |
| 8 | Final Integrated Review | Required | regression pass and explicit completion verdict |

---

## 6. Phase details

For every phase below, the agent must first load the matching repo-local skills/workflows from Section 4, then execute only the requested phase.

## Phase 1 - Service Boundary Extraction

Goal:
- Move Writing learner logic out of `IStudyService` / `StudyService.Writing.cs` into `IWritingService` / `WritingService` without changing product behavior yet.

Do:
1. Create `IWritingService` and `WritingService`.
2. Move Writing public methods from `IStudyService`/`StudyService.Writing.cs` into the new service.
3. Keep `StudyController` thin and delegate Writing routes to `IWritingService`.
4. Remove moved Writing members from `IStudyService` only after controller wiring is complete.
5. Normalize touched Writing namespaces carefully; verify each legacy `TCTVocabulary.*` change before changing it.

Approval gate:
- The user has already pre-approved normal in-scope edits for this phase.
- Only stop if the work becomes destructive, out-of-scope, secret-bearing, or requires tool-enforced escalation approval.

Prepare next phase:
- Leave one clear orchestration point for Writing evaluation so Phase 2 can swap AI internals without reopening service boundaries.

Verification gate:
```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "FullyQualifiedName~Sprint1SmokeTests"
```

Stop after the gate passes.

---

## Phase 2 - AI Provider Integration

Goal:
- Replace the direct OpenAI Writing call with the reusable provider abstraction while keeping rule-based fallback.

Do:
1. Create `IWritingAiEvaluationService`.
2. Create `WritingAiEvaluationService` using `IAiProviderClient`, `AiContextMessage`, `AiProviderReply`, and `AiProviderException`.
3. Remove direct OpenAI HTTP logic from the Writing slice.
4. Keep Writing out of `IAiChatService` / `IAiConversationService`.
5. Add strict JSON parsing and fallback handling.
6. If score fields are introduced, keep AI and fallback outputs aligned.

Approval gate:
- The user has already pre-approved normal in-scope edits for this phase.
- Only stop if the work becomes destructive, out-of-scope, secret-bearing, or requires tool-enforced escalation approval.

Prepare next phase:
- Leave auth and runtime behavior unchanged so Phase 3 can harden them cleanly.

Verification gate:
```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing|Sprint1SmokeTests"
```

Also required:
- Add or update targeted tests for provider success, provider failure -> fallback, invalid JSON -> fallback, and no reference-answer leakage.

Stop after the gate passes.

---

## Phase 3 - Security and Runtime Hardening

Goal:
- Harden practice auth, anti-abuse behavior, and runtime safety.

Do:
1. Apply the locked auth matrix consistently across:
   - `WritingPractice`
   - `WritingPracticeData`
   - `WritingHint`
   - `EvaluateWritingSentence`
2. Add Writing-specific rate limiting. Do not silently share chat request buckets unless the user explicitly wants that.
3. Keep anti-forgery on evaluate.
4. Remove request-time migration behavior from Writing request handling.
5. Keep controllers thin and contracts consistent.

Approval gate:
- The user has already pre-approved normal in-scope edits for this phase.
- Only stop if the work becomes destructive, out-of-scope, secret-bearing, or requires tool-enforced escalation approval.

Prepare next phase:
- Leave a stable backend contract so Phase 4 can redesign the UI without redoing endpoint behavior.

Verification gate:
```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing|Sprint1SmokeTests"
```

Also required:
- Add/update integration tests for public routes, auth-required routes, missing anti-forgery rejection, and rate-limit behavior.

Stop after the gate passes.

---

## Phase 4 - Practice UI Overhaul

Goal:
- Turn `WritingPractice` into a focused learner experience without reopening backend contract decisions.

Do:
1. Refactor `WritingPractice.cshtml`, `writing.js`, and `writing.css`.
2. Prefer a focused practice layout over the current heavy two-column feel.
3. Keep the learner centered on current sentence, answer box, hint/submit, and clear feedback.
4. Add browser draft persistence.
5. Make loading, auth/session expiry, rate limit, and fallback states clear.
6. Keep all UI work inside `Views/Study/` and Writing-specific assets.

Prepare next phase:
- Leave stable DOM hooks and a visually coherent UI so cleanup work does not reopen layout direction.

Verification gate:
```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing|Sprint1SmokeTests"
```

Also required manual checks:
- selection
- submit
- hint
- loading
- local draft restore
- mobile sanity

If `scripts/encoding_guard.py` exists and text/UI text changed, run it.

Stop after the gate passes.

---

## Phase 5 - Honest List UI and Cleanup

Goal:
- Remove fake Writing list metadata and clean up risky/orphan code.

Do:
1. Remove or hide fake attempt counts.
2. Remove or hide fake status badges and fake status filters.
3. Remove or isolate unused/risky helpers that could expose reference answers.
4. Keep text-normalization helpers only if they still solve real data-quality problems.
5. Re-run a quick code-review pass on the Writing slice.

Prepare next phase:
- Leave the learner slice in one honest state:
  - baseline-clean without fake metadata, or
  - clearly ready for optional durable persistence in Phase 6

Verification gate:
```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing|Sprint1SmokeTests"
```

Also required:
- confirm no learner HTML/JSON leaks `EnglishMeaning`
- confirm the list page no longer presents fake progress as real

Stop after the gate passes.

---

## Phase 6 - Durable Writing Progress (optional)

Goal:
- Add true per-user Writing progress/history/status so list metadata becomes real.

Operational rollout note:
- Shipping the code and committing the migration is not enough to make progress accumulate in a running environment.
- Writing progress/history/status starts accumulating only after the additive Writing progress migration is actually applied to the real SQL Server database used by that environment.
- If that live SQL Server step has not happened yet, treat the environment as code-ready but not rollout-complete for durable Writing progress.

Only start this phase if the user explicitly wants:
- real attempts
- real status filters
- real resume/progress/history
- additive schema work

Do:
1. Design a narrow additive Writing progress model.
2. Update learner services to use true progress.
3. Keep ownership and auth rules correct.
4. Review generated migration carefully.

Approval gates:
- The user has already pre-approved additive in-scope migration/schema work for this plan.
- If any migration contains `DropTable` or `DropColumn`, stop immediately and ask the user.
- Also stop if the work becomes out-of-scope, secret-bearing, or requires tool-enforced escalation approval.

Prepare next phase:
- Leave a stable persistence model if final review needs to judge expanded 100% completion.

Verification gate:
```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing"
```

Also required:
- document migration impact
- confirm migration is additive and reversible
- explicitly state whether the migration was only generated locally, applied to a local/dev database, or applied to the target live SQL Server environment
- include the live rollout checklist: backup target SQL Server database, apply the migration there, restart/recycle the app if required by the environment, then verify new `UserWritingAttempts` rows appear after a real learner submission

Stop after the gate passes.

---

## Phase 7 - Admin and Content Alignment

Goal:
- Align existing admin Writing authoring and content readiness with the learner flow.

Do:
1. Reuse the existing admin CRUD flow; do not rebuild it.
2. Ensure `Beginner + Emails` content is clean and dependable.
3. Keep any admin/content changes narrow and Writing-specific.
4. If seed/content text changes are needed, keep them safe and idempotent.

Prepare next phase:
- Leave the codebase ready for a final verdict on whether the learner promise matches admin/content reality.

Verification gate:
```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing"
```

Also required manual checks:
- admin can create/edit/publish Writing content
- learner side consumes published content correctly

Stop after the gate passes.

---

## Phase 8 - Final Integrated Review and Completion Verdict

Goal:
- Review the whole Writing slice, run the final regression pass, and decide whether the task is truly complete.

Do:
1. Run one final code-review pass across all touched Writing files.
2. Run one final security review pass across the Writing slice.
3. Run the broadest reliable Writing-related regression slice.
4. Manually re-check the main learner flow.
5. If Phase 6 was done, include durable progress/history in the review.

The final verdict must answer all of these explicitly:
1. `Baseline 100% complete:` Yes or No
2. `Expanded 100% complete (with durable progress/history):` Yes or No
3. `Can this task be closed now:` Yes or No
4. `If No, exactly which phase or blocker remains:` short list only

Verification gate:
```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build
```

If the full test suite is not reliable, run the widest safe Writing-related slice and clearly state what was and was not run.

Also required final re-check:
- no fake learner metadata unless true persistence exists
- no direct OpenAI Writing call remains
- no request-time migration remains in Writing request handling
- auth/CSRF/rate-limit behavior matches the chosen contract
- no reference answer leaks to learner HTML/JSON

Stop only after the verdict is explicit and evidence-backed.

---

## 7. Exit criteria

Baseline completion requires:
- Phases 1 to 5 done
- Phase 7 done if admin/content alignment was needed by touched work
- Phase 8 says `Baseline 100% complete: Yes`
- Phase 8 says `Can this task be closed now: Yes`

Expanded completion requires:
- baseline completion
- Phase 6 done
- the durable-progress migration applied on the live SQL Server environment where progress is expected to accumulate
- Phase 8 says `Expanded 100% complete (with durable progress/history): Yes`

---

## 8. Notes for future agents

1. Do not silently broaden baseline scope into expanded scope.
2. Do not rebuild admin authoring from scratch.
3. Do not claim real attempts/status/history unless durable persistence really exists.
4. Do not move Writing into `HomeController` or `Views/Home/`.
5. For this plan, the user has already pre-approved normal in-scope edits required by the chosen phase, including `Program.cs` when genuinely needed.
6. That pre-approval does not bypass destructive-change review, secret/credential safety, or tool/sandbox escalation rules enforced by the platform.

---

## 9. Ready-to-send phase values

Use Section 2 and replace `[TASK]` / `[AREA]` with one of the following.

### Phase 1
- `[TASK]`: `Execute only Phase 1 from docs/writing-page-overhaul-plan.md. Extract the Writing learner slice out of IStudyService/StudyService into IWritingService/WritingService without changing user-visible behavior yet. Respect all approval gates. Do not start Phase 2.`
- `[AREA]`: `StudyController + Services + ViewModels (Writing learner slice)`

### Phase 2
- `[TASK]`: `Execute only Phase 2 from docs/writing-page-overhaul-plan.md. Replace the Writing direct OpenAI call with the reusable AI provider abstraction, keep rule-based fallback, add targeted tests, and stop after the Phase 2 verification gate. Do not start Phase 3.`
- `[AREA]`: `Services/Writing + Services/AI + Writing evaluate flow`

### Phase 3
- `[TASK]`: `Execute only Phase 3 from docs/writing-page-overhaul-plan.md. Harden the Writing practice contract with the locked auth matrix, Writing-specific rate limiting, and removal of request-time migration behavior. Add or update the required tests and stop after the Phase 3 verification gate. Do not start Phase 4.`
- `[AREA]`: `StudyController + Writing learner endpoints + AI rate limiting + Writing runtime safety`

### Phase 4
- `[TASK]`: `Execute only Phase 4 from docs/writing-page-overhaul-plan.md. Overhaul the WritingPractice UI into a focused learner experience without reopening backend contract decisions. Run the required verification gate and stop. Do not start Phase 5.`
- `[AREA]`: `Views/Study/WritingPractice + wwwroot/js/writing.js + wwwroot/css/writing.css`

### Phase 5
- `[TASK]`: `Execute only Phase 5 from docs/writing-page-overhaul-plan.md. Remove fake Writing list metadata, clean up risky/orphan Writing code, verify no reference-answer leaks remain, and stop after the Phase 5 verification gate. Do not start Phase 6 or Phase 8.`
- `[AREA]`: `Views/Study/WritingExercises + ViewModels/Writing* + Writing service cleanup`

### Phase 6
- `[TASK]`: `Execute only Phase 6 from docs/writing-page-overhaul-plan.md. Add durable Writing progress/history/status persistence only if the work can remain additive and safe. Follow all migration approval gates, run the required verification, and stop after the Phase 6 verification gate.`
- `[AREA]`: `Models + DbflashcardContext + Migrations + Writing learner service`

### Phase 7
- `[TASK]`: `Execute only Phase 7 from docs/writing-page-overhaul-plan.md. Align the existing admin Writing authoring flow and content readiness with the learner Writing experience, keep the scope narrow, run the required checks, and stop after the Phase 7 verification gate. Do not start Phase 8.`
- `[AREA]`: `Areas/Admin/WritingExerciseManagement + Writing content readiness`

### Phase 8
- `[TASK]`: `Execute only Phase 8 from docs/writing-page-overhaul-plan.md. Perform the final integrated Writing review, run the final regression pass, and give the explicit baseline/expanded/close-task verdict required by the plan. Do not implement new feature work unless a review finding absolutely requires a blocking fix and you clearly state it.`
- `[AREA]`: `Writing end-to-end learner flow + admin support + AI integration + tests`
