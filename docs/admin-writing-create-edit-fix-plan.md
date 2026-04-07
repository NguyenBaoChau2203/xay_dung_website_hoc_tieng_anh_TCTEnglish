# Admin Writing Create/Edit Fix Plan

> Status: investigation complete, phase-executable runbook  
> Updated: 2026-04-07  
> Scope: `Areas/Admin/WritingExerciseManagement*` create/edit failure on the admin Writing authoring flow

---

## 1. How to use this file

This file is the single source of truth for the admin Writing create/edit bug.

Use it like this:

1. Pick exactly one phase from Section 6.
2. Send the agent the prompt template from Section 2.
3. Replace `[TASK]` and `[AREA]` with the phase-specific values in Section 8.
4. The agent must execute only that phase, run that phase's verification gate, then stop.

The agent must not silently continue into a later phase.

---

## 2. Reusable agent prompt template

```text
@workspace Task: [TASK]

Area hint: [AREA]

Read `AGENTS.md` first and follow it strictly.
Then read, in order: `docs/project-structure.md`, `docs/architecture-prioritized-backlog.md`, `.ai/context/known-issues.md`, `.ai/context/coding-conventions.md`.
Treat `docs/implementation_plan.md` as supporting context only and `docs/branch-handoff-architecture-hardening.md` as historical context only.

Then read `docs/admin-writing-create-edit-fix-plan.md` and execute only the requested phase.
Do not start any later phase.

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

If this is a bug fix, read `.ai/context/bug-fix-log.md` and `.ai/context/known-issues.md` before editing, reuse an old fix only if the root cause matches, and update those files after verification if relevant.

If the platform supports subagents and the task is non-trivial, spawn one explorer subagent for the relevant area first; otherwise continue locally.

Complete the task end-to-end, run the phase verification gate exactly as required by `docs/admin-writing-create-edit-fix-plan.md`, and stop after that gate.
Finish with a concise Vietnamese summary covering: result, files changed, checks run, skills/workflows used, phase gate status, and remaining risks or blockers.
```

---

## 3. Current error state

### User-facing symptom

- On the admin Writing screen, submitting a valid form in:
  - `POST /Admin/WritingExerciseManagement/Create`
  - `POST /Admin/WritingExerciseManagement/Edit/{id}`
- returns back to the form with the generic error banner:
  - `Không thể tạo bài viết lúc này. Vui lòng thử lại.`
  - `Không thể cập nhật bài viết lúc này. Vui lòng thử lại.`

### Current behavior in code

- `WritingExerciseManagementController` catches any exception and only shows the generic banner.
- Because of that catch block, the real runtime exception is hidden from the admin UI.
- The GET screens can still load, so the failure is concentrated in the mutation path, not the page rendering path.

### What was checked during investigation

- Repo instructions and required docs were read first.
- Existing local changes in the Writing area were inspected and left untouched.
- The affected controller, view models, admin views, EF model, app startup DB configuration, and current tests were reviewed.
- `WritingAdminContentIntegrationTests` were run and passed locally.

### Important investigation result

The current strongest root cause is not form validation. It is the transaction pattern in the admin Writing controller under SQL Server runtime configuration.

---

## 4. Locked investigation findings

Treat these as already-established unless the code materially changes.

### Finding A - The app uses SQL Server retry execution strategy

- `TCTEnglish/Program.cs` registers `DbflashcardContext` with:
  - `UseSqlServer(...)`
  - `EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)`

This means the runtime uses SQL Server's retry execution strategy.

### Finding B - Admin Writing create/edit start manual transactions directly

- `Areas/Admin/Controllers/WritingExerciseManagementController.cs`
  - `Create(...)` calls `BeginTransactionAsync()` directly
  - `Edit(...)` calls `BeginTransactionAsync()` directly

Under SQL Server retry execution strategy, this pattern is expected to fail unless the whole transaction block is executed inside:

- `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)`

### Finding C - The repo already contains the correct local fix pattern

- `Areas/Admin/Controllers/SpeakingVideoManagementController.cs`
  - uses `CreateExecutionStrategy()`
  - then opens `BeginTransactionAsync()` inside that execution strategy block

This is the best repo-local reference implementation for fixing the Writing admin flow.

### Finding D - Existing tests do not expose the runtime mismatch

- `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs` swaps the app DB to in-memory SQLite.
- SQLite test setup does not mirror the SQL Server retry execution strategy from production/runtime.
- Therefore the current create/edit integration tests can pass while the real admin runtime still fails.

### Finding E - Secondary hygiene issues exist in the same area

These are not the primary root cause of the create/edit failure, but they should be reviewed if the files are already being touched:

- legacy namespace usage: `TCTVocabulary.*`
- mojibake/encoding corruption in several admin Writing strings
- mutation paths are still controller-heavy and could use clearer helper boundaries

### Finding F - Edit currently replaces sentences wholesale, which can affect historical attempts

- `Areas/Admin/Controllers/WritingExerciseManagementController.cs`
  - `Edit(...)` removes all `WritingExerciseSentences`
  - then recreates replacement sentence rows
- `Models/DbflashcardContext.cs`
  - `WritingExerciseSentence -> UserWritingAttempts` is configured with cascade delete

This means the runtime create/edit fix overlaps with an existing data-retention risk:

- if `UserWritingAttempts` data exists
- and edit continues to replace sentence rows wholesale
- then historical attempt rows linked to deleted sentence rows are permanently lost during edit

Instead of deferring this risk or hoping no live data exists, Phase 1 must update the `Edit` logic to modify sentences in-place (updating existing IDs based on index/order) rather than performing a catastrophic `RemoveRange`.

### Working root-cause statement

The admin Writing create/edit flow likely fails at runtime because the controller opens a manual EF Core transaction directly while the app is configured with SQL Server retry execution strategy. This mismatch is consistent with add/edit failing every time in the live app while the SQLite-based integration tests still pass locally.

### Required evidence note

The execution-strategy mismatch is the strongest current root-cause hypothesis, but closure should prefer evidence over confidence. If a SQL Server-backed manual reproduction or application log captures the concrete exception text, add that evidence to this file before broadening scope or declaring the investigation complete.

---

## 5. Scope boundaries for the fix

Stay inside these boundaries unless a phase explicitly says otherwise:

- Primary controller: `Areas/Admin/Controllers/WritingExerciseManagementController.cs`
- Primary views:
  - `Areas/Admin/Views/WritingExerciseManagement/Create.cshtml`
  - `Areas/Admin/Views/WritingExerciseManagement/Edit.cshtml`
  - `Areas/Admin/Views/WritingExerciseManagement/_ExerciseForm.cshtml`
  - `Areas/Admin/Views/WritingExerciseManagement/_ExerciseFormScripts.cshtml`
- Primary view models:
  - `Areas/Admin/ViewModels/WritingExerciseManagementViewModel.cs`
- Supporting model/context references only as needed:
  - `Models/DbflashcardContext.cs`
  - `Models/WritingExercise.cs`
  - `Models/WritingExerciseSentence.cs`

Do not move this work into `HomeController`.
Do not rebuild admin Writing CRUD from scratch.
Do not modify `Program.cs` for the bug fix; the planned fix should adapt the Writing controller/service code to the already-configured runtime behavior.
Do not treat `UserWritingAttempts` history impact as an optional cleanup note if the target environment already has live attempt data.

---

## 6. Phase map

| Phase | Name | Required? | Main outcome |
|---|---|---|---|
| 1 | Runtime Fix | Required | Create/Edit stop failing under SQL Server retry strategy |
| 2 | Regression and Diagnostics Hardening | Required | The bug is covered and future failures are easier to trace |
| 3 | Hygiene Follow-up | Recommended | Touched admin Writing files are safer to maintain |
| 4 | Final Review and Close Gate | Required | Manual re-test, risk review, and explicit close verdict |

---

## 7. Phase details

## Phase 1 - Runtime Fix

Goal:
- Fix the actual create/edit runtime failure without broadening scope beyond the admin Writing authoring flow.

Do:
1. Re-read the affected admin Writing controller and the local reference pattern in `SpeakingVideoManagementController`.
2. Update the Writing create/edit mutation flow so any manual transaction is executed through `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)`.
3. Keep anti-forgery, ViewModel repopulation, validation, and current route shape unchanged.
4. Ensure the create/edit catch blocks continue to use `_logger.LogError(ex, "...")` with enough context so the real exception is not effectively swallowed during the manual verification block.
5. Keep controller behavior thin where practical; if needed, extract a small private helper instead of duplicating transaction logic.
6. Preserve current success/failure UX unless a small diagnostic improvement is safe and low-risk.
7. Fix the data retention risk during edit: Instead of `RemoveRange` and recreating all sentences, update the existing `WritingExerciseSentences` in-place by matching their collection order. Only add extra new sentences or remove trailing excessive ones so that existing `UserWritingAttempts` are not cascade-deleted by a simple typo fix.

Prepare next phase:
- Leave a clear seam so Phase 2 can add regression protection without reworking the business flow again.

Verification gate:
```bash
dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "FullyQualifiedName~WritingAdminContentIntegrationTests"
```

Gate rule:
- Do not rely on previously built test binaries from an earlier run.

Manual check required before stopping:
- On a SQL Server-backed runtime or the target environment, verify:
  - create draft article succeeds
  - create published article succeeds
  - edit an existing article succeeds
- Capture one of the following in the phase notes:
  - the concrete SQL Server/runtime exception that was reproduced before the fix, or
  - a short statement that no pre-fix reproduction/log capture was available
- If no SQL Server-backed runtime is available for manual verification, record the phase as not fully closed and carry that blocker forward explicitly.

Stop after the gate and manual check notes are recorded.

---

## Phase 2 - Regression and Diagnostics Hardening

Goal:
- Make the bug harder to reintroduce and make future failures easier to diagnose.

Do:
1. Add or update targeted regression coverage around the admin Writing create/edit flow.
2. Add a focused guard for the SQL Server execution-strategy requirement. If a real SQL Server test is not practical, add the smallest reliable structural/runtime guard that still catches reintroduction of the direct-transaction pattern.
3. Review logging in the create/edit catch paths so future incidents leave enough context in logs:
   - route/action
   - admin id
   - exercise id where available
   - title where safe
4. Confirm delete flow was not accidentally regressed while fixing create/edit.
5. Extend regression coverage beyond the current two happy-path tests so the touched admin flow also proves:
   - create draft remains valid
   - edit full text still re-splits sentences as expected
   - the existing publish-toggle learner visibility behavior still holds
6. Prove the edit data retention: add a test that ensures editing a writing exercise preserves the original `WritingExerciseSentence` primary keys whenever possible, proving that historical `UserWritingAttempts` will not be wiped out.

Note:
- Because integration tests use SQLite and do not reproduce SQL Server execution strategy behavior, do not over-engineer this phase by introducing a real SQL Server container or large DI rewiring just for this guard.
- A small structural regression guard plus explicit documentation of the test limitation is acceptable if it reliably prevents the direct-transaction pattern from slipping back in.
- Acceptable structural guard examples in this repo include a focused source/shape test that verifies the Writing admin controller uses `CreateExecutionStrategy()` around the create/edit transaction path and does not revert to direct top-level `BeginTransactionAsync()` in those actions.

Prepare next phase:
- Leave the touched area stable so cleanup work can stay narrow and not reopen the runtime fix.

Verification gate:
```bash
dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "WritingAdmin|Writing|Sprint1SmokeTests"
```

Also required:
- Record exactly what the new regression guard does and what it still cannot prove without SQL Server.
- Record whether the phase outcome depends on a structural guard, runtime SQLite behavior, SQL Server-backed manual verification, or a combination of those.

Stop after the gate passes.

---

## Phase 3 - Hygiene Follow-up

Goal:
- Clean up the touched admin Writing area only where it directly improves maintainability and reduces follow-up risk.

Do:
1. Normalize only the touched files that are already part of the fix:
   - repair mojibake Vietnamese strings if they are edited anyway
   - preserve UTF-8
2. Review whether touched files should stay on legacy `TCTVocabulary.*` namespaces for consistency with nearby code, or whether a safe namespace normalization is warranted in the exact touched slice.
3. Keep the scope narrow:
   - no broad namespace migration
   - no unrelated admin redesign
   - no feature expansion
4. If `scripts/encoding_guard.py` exists, run it after text changes.

Prepare next phase:
- Leave the area readable enough that the final review can focus on behavior and risk instead of text corruption noise.

Verification gate:
```bash
dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "FullyQualifiedName~WritingAdminContentIntegrationTests"
```

Stop after the gate passes.

---

## Phase 4 - Final Review and Close Gate

Goal:
- Re-test the complete admin Writing create/edit loop, review remaining risks, and decide whether the bug can be closed.

Do:
1. Perform one final code review over all touched admin Writing files.
2. Re-test the full admin mutation flow:
   - create draft
   - create published
   - edit title/topic/preview only
   - edit full text and confirm auto-split still works
   - toggle publish state and verify learner-side visibility behavior still matches expectations
3. Re-run the agreed automated checks.
4. Update `.ai/context/bug-fix-log.md` strictly following its required format, and remove or update the item in `.ai/context/known-issues.md` if this bug was listed there.
5. Explicitly review remaining risks before closure.
6. Confirm the data retention risk was handled: verify that the code implements in-place sentence edits instead of blanket `RemoveRange`.

The final review must explicitly answer:
1. `Create bug fixed:` Yes or No
2. `Edit bug fixed:` Yes or No
3. `Covered by automated regression:` Yes or No
4. `Covered by SQL Server-backed manual verification:` Yes or No
5. `Can this bug be closed now:` Yes or No
6. `If No, what exact blocker remains:` short list only

Verification gate:
```bash
dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "WritingAdmin|Writing|Sprint1SmokeTests"
```

Final risk review checklist:
- Does the fix still work on the real SQL Server-backed environment?
- Is there still any hidden generic catch path that would mask the next runtime error too much?
- Are historical `UserWritingAttempts` safe now because editing an exercise updates sentences in-place instead of using wholesale sentence replacement (`RemoveRange`)?
- Did any touched admin Writing strings keep mojibake/encoding corruption?
- Were unrelated local Writing changes preserved?
- Were the automated gates run against freshly built test binaries rather than stale outputs?

Stop only after the verdict is explicit.

---

## 8. Ready-to-send phase values

Use Section 2 and replace `[TASK]` / `[AREA]` with one of the following.

### Phase 1
- `[TASK]`: `Execute only Phase 1 from docs/admin-writing-create-edit-fix-plan.md. Fix the admin Writing create/edit runtime failure caused by the current transaction pattern, keep the scope narrow to the existing admin Writing CRUD flow, run the Phase 1 verification gate, and stop. If SQL Server-backed manual verification is unavailable, stop with an explicit blocker instead of assuming closure.`
- `[AREA]`: `Areas/Admin/WritingExerciseManagement create/edit mutation path`

### Phase 2
- `[TASK]`: `Execute only Phase 2 from docs/admin-writing-create-edit-fix-plan.md. Add regression and diagnostics hardening for the admin Writing create/edit bug, include a clear structural guard if SQL Server test parity is not practical, run the Phase 2 verification gate, and stop.`
- `[AREA]`: `Areas/Admin/WritingExerciseManagement + TCTEnglish.Tests Writing admin regression coverage`

### Phase 3
- `[TASK]`: `Execute only Phase 3 from docs/admin-writing-create-edit-fix-plan.md. Apply narrow hygiene cleanup to the touched admin Writing files only where it directly supports the create/edit fix, run the Phase 3 verification gate, and stop.`
- `[AREA]`: `Areas/Admin/WritingExerciseManagement touched files`

### Phase 4
- `[TASK]`: `Execute only Phase 4 from docs/admin-writing-create-edit-fix-plan.md. Perform the final review, manual re-test checklist, risk review, and explicit close verdict for the admin Writing create/edit bug. Do not broaden scope into new feature work, and do not close the bug if SQL Server verification or `UserWritingAttempts` impact remains unresolved.`
- `[AREA]`: `Admin Writing create/edit end-to-end verification`

---

## 9. Notes for future agents

1. Do not treat SQLite test success as proof that the SQL Server runtime bug is fixed.
2. Use `SpeakingVideoManagementController` as the first local pattern to compare against.
3. Keep the fix inside the admin Writing boundary unless the investigation proves a deeper shared abstraction is required.
4. Do not silently clean the whole Writing module while addressing this bug.
5. If the runtime error turns out to differ from the execution-strategy mismatch, update this file first with the new evidence before broadening the implementation.
6. If no SQL Server-backed manual verification environment exists, say that plainly; do not let the phase wording imply a stronger conclusion than the evidence supports.
