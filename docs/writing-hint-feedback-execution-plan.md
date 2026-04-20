# Writing Hint and Feedback Execution Plan

> Status: agent-executable phase runbook  
> Updated: 2026-04-07  
> Scope: Writing practice hint + AI evaluation + AI rewrite + final review

---

## 1. How to use this file

This file is meant to replace a long custom prompt for this Writing sub-slice.

Use it like this:

1. Tell the agent to read this file.
2. Tell it to execute exactly one phase.
3. Do not let it continue to the next phase until the current phase passes its gate.

Minimal invocation examples:

- `@workspace Read docs/writing-hint-feedback-execution-plan.md and execute only Phase 1.`
- `@workspace Read docs/writing-hint-feedback-execution-plan.md and execute only Phase 3.`
- `@workspace Read docs/writing-hint-feedback-execution-plan.md and execute only Phase 6.`

The agent must not silently start a later phase.

---

## 2. Embedded operating contract

Any agent executing a phase from this file must treat the following as mandatory:

1. Read `AGENTS.md` first and follow it strictly.
2. Then read, in order:
   - `docs/project-structure.md`
   - `docs/architecture-prioritized-backlog.md`
   - `.ai/context/known-issues.md`
   - `.ai/context/coding-conventions.md`
3. Treat `docs/implementation_plan.md` as supporting context only.
4. Treat `docs/branch-handoff-architecture-hardening.md` as historical context only.
5. Use the phase area hint first to identify the correct post-refactor controller/view/service boundary.
6. Do not add new domain logic to `HomeController`.
7. Do not add new feature screens to `Views/Home/`.
8. Keep work inside the correct feature boundary unless the phase explicitly requires a narrow cross-cutting change.
9. Inspect existing local changes before editing and do not overwrite unrelated work.
10. Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and `.ai/context/`, then follow the repo's existing rules, skills, workflows, and patterns.
11. Preserve UTF-8. If Vietnamese text or UI/text files are touched, avoid encoding issues and run `python scripts/encoding_guard.py` if it exists.
12. Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless explicitly requested.
13. Follow core repo rules:
    - use ViewModels for views
    - keep controllers thin
    - use async I/O
    - enforce anti-IDOR and anti-CSRF
    - use `GetCurrentUserId()` from `BaseController`
    - return `NotFound()` for ownership violations
14. For namespaces/imports, follow the conventions already used in touched files and verify legacy `TCTVocabulary.*` references before changing them.
15. If the work is acting as a bug fix, read `.ai/context/bug-fix-log.md` and `.ai/context/known-issues.md` before editing, reuse an old fix only if the root cause matches, and update those files after verification if relevant.
16. If the platform supports subagents and the phase is non-trivial, spawn one explorer subagent for the relevant area first; otherwise continue locally.
17. Complete the chosen phase end to end, run that phase's verification gate, and stop.
18. Finish with a concise Vietnamese summary covering:
    - result
    - files changed
    - checks run
    - skills/workflows used
    - remaining risks or blockers

---

## 3. Locked product decisions

Agents should treat these as fixed product decisions for this plan unless the user explicitly changes them.

1. `Hint` does **not** use AI.
2. Clicking `Hint` should reveal the current sentence's English translation, sourced from `WritingExerciseSentence.EnglishMeaning`.
3. `Hint` must reveal the translation only on demand through the hint endpoint. Do not preload `EnglishMeaning` into the initial learner HTML or JSON payload.
4. `Chi tiet danh gia` should use the existing Gemini-backed Writing evaluation path and should stay short, clear, and actionable.
5. `Cau goi y chinh lai` should use AI when the learner sentence is not yet acceptable.
6. If the learner answer fails, `suggestedRewrite` should normally be present and useful.
7. Feedback text should not talk about a hidden "teacher reference" or internal grading guide.
8. It is acceptable for learner-facing `Hint` and learner-facing `SuggestedRewrite` to expose a concrete English sentence for the current line, because that is now an intentional product requirement.
9. The Writing learner slice stays inside:
   - `StudyController`
   - `IWritingService` / `WritingService`
   - Writing-specific supporting services
   - `Views/Study/`
   - `wwwroot/js/writing.js`
   - `wwwroot/css/writing.css`
10. Do not route new logic through `HomeController`.
11. Do not rebuild the generic AI chat stack for this feature. Reuse the existing provider abstraction only where Writing evaluation/rewrite truly needs it.

---

## 4. Current code truths

These are the known starting realities in the current codebase:

1. The learner routes already exist in `StudyController`.
2. The hint endpoint exists, but it currently returns static guidance from `BuildHintTitle(...)` and `BuildHintText(...)` rather than a real translation.
3. Writing AI evaluation already exists through `IWritingAiEvaluationService` and `WritingAiEvaluationService`.
4. The Gemini provider abstraction is already wired through `IAiProviderClient` and `GeminiProviderClient`.
5. The front-end currently keeps `lastEvaluation` only in browser runtime state, so a reload or fresh page open loses detailed feedback and suggested rewrite.
6. `UserWritingAttempt` stores attempt metadata, but not the AI feedback snapshot needed to rebuild the full feedback panel after reload.
7. The initial practice payload correctly avoids exposing `EnglishMeaning`, and that safety rule should remain in place outside the on-demand hint response.
8. `BuildReviewText(...)` currently flattens all feedback into one long string, which is a known readability bottleneck for the learner review panel.
9. `ContainsReferenceAnswerLeak(...)` is a known hotspot because a hard-block policy there can suppress otherwise useful learner-facing rewrites.

Supporting code entry points for this plan:

- `TCTEnglish/Controllers/StudyController.cs`
- `TCTEnglish/Services/IWritingService.cs`
- `TCTEnglish/Services/WritingService.cs`
- `TCTEnglish/Services/IWritingAiEvaluationService.cs`
- `TCTEnglish/Services/WritingAiEvaluationService.cs`
- `TCTEnglish/Models/WritingExerciseSentence.cs`
- `TCTEnglish/Models/UserWritingAttempt.cs`
- `TCTEnglish/ViewModels/WritingIndexViewModel.cs`
- `TCTEnglish/Views/Study/WritingPractice.cshtml`
- `TCTEnglish/wwwroot/js/writing.js`
- `TCTEnglish/wwwroot/css/writing.css`
- `TCTEnglish.Tests/Sprint1SmokeTests.cs`
- `TCTEnglish.Tests/WritingAiEvaluationIntegrationTests.cs`

---

## 5. Phase map

| Phase | Name | Required? | Main outcome |
| --- | --- | --- | --- |
| 1 | Hint Contract Replacement | Required | `Hint` returns the current sentence translation without AI |
| 2 | AI Evaluation Tightening | Required | AI feedback becomes short and product-aligned |
| 3 | AI Rewrite Quality and Exposure Policy | Required | weak answers get a useful rewrite and aligned exposure rules |
| 4 | Feedback Persistence and Reload Recovery | Required | detailed feedback survives refresh / re-entry |
| 5 | UI and Fallback Alignment | Required | learner-facing copy and states match the new backend truth |
| 6 | Final Integrated Review | Required | full review, regression pass, and risk verdict |

---

## 6. Phase details

Do exactly one phase at a time.

If a phase gate fails, stop and report the blocker. Do not start the next phase.

---

## Phase 1 - Hint Contract Replacement

**Area hint:** `StudyController + WritingService + WritingPractice front-end hint flow`

**Goal**

Replace the static learner hint with a real translation of the currently selected sentence, without using AI.

**Do**

1. Keep the existing auth and rate-limit boundary for the hint endpoint.
2. Change the Writing hint service flow so the returned learner hint is based on the selected sentence's `EnglishMeaning`.
3. Keep the lookup narrow:
   - validate the exercise
   - validate the sentence belongs to that exercise
   - keep the published-scope checks
4. Keep `EnglishMeaning` out of:
   - the initial practice payload
   - the exercise list payload
   - any learner HTML that loads before the click
5. If the old generic-tip classification helpers no longer add real value, simplify or retire their greeting/closing/question branching rather than preserving it by habit.
6. Use a stable learner-facing hint title instead of the old heuristic titles. A simple format such as `Reference translation - Sentence {N}` is preferred over greeting/closing-specific labels.
7. Update the hint payload shape only if necessary; prefer a small change if the current ViewModel can carry the new copy cleanly.
8. Update the front-end wording so the learner understands this is a direct translation hint for the current line, not a generic writing tip.
9. Update tests that currently assume hint text is generic instead of a direct translation.

**Do not**

- do not add AI here
- do not preload all `EnglishMeaning` values to the client
- do not weaken auth or rate limiting

**Prepare next phase**

Leave a stable hint contract so the AI evaluation phases can assume the hint problem is solved and do not need to revisit the hint transport again.

**Verification gate**

```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing|Sprint1SmokeTests"
```

**Required manual checks**

1. Open Writing practice as an authenticated learner.
2. Click `Hint` on several different lines.
3. Confirm the returned text is the current sentence translation, not a generic tip.
4. Confirm switching to another sentence changes the hint correctly.
5. Confirm page source / initial JSON still does not expose `EnglishMeaning`.

Stop after the gate passes.

---

## Phase 2 - AI Evaluation Tightening

**Area hint:** `Writing AI evaluation service + WritingService evaluation mapping`

**Goal**

Keep the current Gemini-backed evaluation path, but make the returned review short, clean, and aligned with the desired learner experience.

**Do**

1. Refine the prompt in `WritingAiEvaluationService` so the AI returns concise Vietnamese feedback.
2. Add explicit output bounds to the prompt:
   - each feedback field should usually be at most one short sentence
   - the target length is roughly <= 30 words per field unless a little more is truly needed
3. Keep feedback actionable and brief:
   - short overall feedback
   - short meaning feedback
   - short grammar feedback
   - short naturalness feedback
   - short word-choice feedback
4. Add an explicit prompt instruction: do not teach grammar theory; only point out the concrete problem and the practical fix.
5. Keep strict JSON parsing and existing provider failure fallback behavior.
6. Keep the response structure compatible with the existing evaluation ViewModel unless a very small contract refinement is clearly needed.
7. Review how `ReviewText` is built in `WritingService` and adjust it if the current assembly produces text that is too long or repetitive.
8. Preferred review rendering:
   - preserve field boundaries with `\n`, or
   - render fields separately in the UI if that can be done narrowly and safely
   - do not keep one long flat paragraph if it hurts readability
9. Keep all evaluation text learner-facing. Do not add internal/provider/debug copy to the learner payload.
10. Update AI integration tests so they assert concise, learner-facing behavior rather than the old loose placeholder behavior.

**Do not**

- do not move Writing into AI chat abstractions
- do not remove rule-based fallback
- do not make the response verbose again in JS if the backend is concise

**Prepare next phase**

Leave a stable short-feedback contract so the rewrite phase can focus on correction quality instead of reworking the whole evaluation shape again.

**Verification gate**

```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing|Sprint1SmokeTests"
```

**Required manual checks**

1. Submit one good learner answer.
2. Submit one weak learner answer.
3. Confirm `Chi tiet danh gia` is short, readable, and not essay-like.
4. Confirm AI fallback still works when the provider is stubbed/failing in tests.

Stop after the gate passes.

---

## Phase 3 - AI Rewrite Quality and Exposure Policy

**Area hint:** `WritingAiEvaluationService + WritingService rewrite mapping + exposure rules`

**Goal**

Make `Cau goi y chinh lai` genuinely useful when the learner answer is semantically off, grammatically weak, or unnatural.

**Do**

1. Refine the AI prompt so that:
   - if the learner answer passes, `suggestedRewrite` may be empty
   - if the learner answer fails, `suggestedRewrite` should normally be present
2. Keep the rewrite learner-facing and natural, not robotic.
3. Align exposure policy with the locked product decision:
   - feedback text must not mention hidden grading references
   - learner-facing `suggestedRewrite` may expose a concrete corrected English sentence
4. Revisit `ContainsReferenceAnswerLeak(...)` explicitly; this method is a known hotspot for empty rewrite behavior.
5. Keep leak protection for narration fields such as overall/meaning/grammar/naturalness/word-choice feedback.
6. Do not keep a hard-block policy that discards the whole AI result just because `suggestedRewrite` is close to the best learner-facing answer.
7. Preferred direction:
   - keep narration fields guarded against reference-answer leakage
   - allow `suggestedRewrite` to remain learner-usable under the locked product decision
8. Add a server-side guard for bad provider output:
   - if `passed == false` and rewrite is empty or meaningless, fall back safely
   - the fallback can remain modest, but it must not leave the learner with an empty correction area when the sentence clearly failed
   - a minimal fallback such as `Try again with a closer meaning and a more natural English sentence.` is acceptable if a stronger safe rewrite is unavailable
9. Update tests for:
   - failed answer -> useful rewrite present
   - passed answer -> rewrite may be empty
   - feedback does not narrate hidden internal reference text

**Do not**

- do not silently reintroduce the old "never show the answer anywhere" rule for this feature slice
- do not allow raw provider/debug text into the learner UI

**Prepare next phase**

Leave a stable evaluation snapshot contract so the next phase can persist and restore exactly what the learner saw.

**Verification gate**

```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing|Sprint1SmokeTests"
```

**Required manual checks**

1. Submit an answer with wrong meaning.
2. Submit an answer with mostly correct meaning but poor grammar.
3. Submit an awkward but understandable answer.
4. Confirm failed cases produce a helpful rewrite.
5. Confirm the review text stays short while the rewrite stays concrete.

Stop after the gate passes.

---

## Phase 4 - Feedback Persistence and Reload Recovery

**Area hint:** `UserWritingAttempt + WritingService loading/persistence + practice payload restoration`

**Goal**

Ensure `Chi tiet danh gia` and `Cau goi y chinh lai` survive refresh and re-entry, instead of disappearing back to placeholders.

**Do**

1. Extend persistence so the last evaluation snapshot needed by the learner UI can be restored after reload.
2. Prefer an additive, Writing-specific persistence shape. Typical stored fields may include:
   - `SummaryTitle`
   - `SummaryText`
   - `ReviewText`
   - `MeaningFeedback`
   - `GrammarFeedback`
   - `NaturalnessFeedback`
   - `WordChoiceFeedback`
   - `SuggestedRewrite`
3. Recommended additive column spec for `UserWritingAttempt`:

| Column | Type | Nullable | Purpose |
| --- | --- | --- | --- |
| `SummaryTitle` | `nvarchar(200)` | Yes | banner title |
| `SummaryText` | `nvarchar(500)` | Yes | banner body |
| `ReviewText` | `nvarchar(2000)` | Yes | rendered overall review snapshot |
| `MeaningFeedback` | `nvarchar(500)` | Yes | meaning feedback snapshot |
| `GrammarFeedback` | `nvarchar(500)` | Yes | grammar feedback snapshot |
| `NaturalnessFeedback` | `nvarchar(500)` | Yes | naturalness feedback snapshot |
| `WordChoiceFeedback` | `nvarchar(500)` | Yes | word-choice feedback snapshot |
| `SuggestedRewrite` | `nvarchar(1000)` | Yes | learner-facing corrected sentence |

4. If schema changes are required, keep them additive only and keep newly added feedback columns nullable.
5. Update `WritingService` loading so the most recent evaluation snapshot per sentence can be reconstructed into the practice payload.
6. Update ViewModels as needed to carry last-evaluation details in a typed way.
7. Update `WritingPractice.cshtml` and `writing.js` so initial page load can rehydrate the feedback area from server-provided snapshot data.
8. Keep the current ownership checks and published-scope rules intact.
9. Update tests for:
   - submit -> reload -> feedback still present
   - submit -> re-open practice -> rewrite still present for the sentence

**Approval and safety gate**

- If this phase requires a migration, it must remain additive.
- If the generated migration contains `DropTable` or `DropColumn`, stop immediately and ask the user.
- Generate the migration only as part of implementation evidence unless the user explicitly asks for it to be applied to a real target database.

**Prepare next phase**

Leave the UI with restored truth from the backend so the front-end cleanup phase can focus on presentation, not on missing data.

**Verification gate**

```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing|Sprint1SmokeTests"
```

**Required manual checks**

1. Submit a sentence and confirm the review/rewrite appears.
2. Refresh the page.
3. Re-select the same sentence.
4. Confirm the review/rewrite is restored instead of placeholder text.
5. If a migration was introduced, confirm it is additive and explain whether it was only generated, applied locally, or applied to a target database.

Stop after the gate passes.

---

## Phase 5 - UI and Fallback Alignment

**Area hint:** `Views/Study/WritingPractice + wwwroot/js/writing.js + writing.css`

**Goal**

Make the learner-facing Writing feedback panel honest, clear, and aligned with the new backend behavior.

**Do**

1. Update UI copy so each section reflects its real purpose:
   - `Hint` = current sentence translation on demand
   - `Chi tiet danh gia` = short review
   - `Cau goi y chinh lai` = corrected English sentence when needed
2. Remove misleading copy that says detailed AI feedback is unavailable when AI is actually being used.
3. Keep fallback states honest:
   - no submission yet
   - AI evaluation used
   - rule-based fallback used
   - rate limit
   - session expired
   - unexpected error
4. Keep the DOM stable where practical so existing test selectors do not break unnecessarily.
5. Prefer learner-readable review rendering:
   - show individual feedback fields separately, or
   - preserve line breaks in `ReviewText`
   - do not collapse the whole review into one dense paragraph if avoidable
6. If styling changes are needed, keep them Writing-specific and narrow.
7. Re-check that the hint section does not imply "generic advice" once it is now returning a translation.
8. Re-check that the rewrite section is empty only when it should truly be empty.
9. Re-check source-note/fallback copy so it no longer claims "detailed AI feedback is unavailable" in cases where AI is in fact being used successfully.

**Do not**

- do not reopen controller/service architecture here
- do not add unrelated UI redesign work

**Prepare next phase**

Leave a polished, coherent learner flow for the final integrated review.

**Verification gate**

```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing|Sprint1SmokeTests"
```

**Required manual checks**

1. Selection flow
2. Hint flow
3. Submit good answer
4. Submit weak answer
5. Refresh recovery
6. Rate-limit state
7. Session-expired state
8. Narrow mobile sanity check

If `scripts/encoding_guard.py` exists and UI text changed, run it before closing the phase.

Stop after the gate passes.

---

## Phase 6 - Final Integrated Review

**Area hint:** `Writing learner flow end to end + Writing tests + persistence + front-end states`

**Goal**

Run the final review pass, test the whole Writing hint/evaluation/rewrite slice, and decide whether the work is truly ready.

**Do**

1. Run one final code-review pass across all touched Writing files.
2. Re-check security boundaries:
   - auth
   - anti-forgery
   - ownership scope
   - no unintended preload of `EnglishMeaning`
3. Re-check product behavior:
   - hint is on-demand translation only
   - evaluation is short
   - failed answers get a rewrite
   - reload preserves prior feedback if Phase 4 was completed
4. Run the broadest reliable Writing-related regression slice.
5. Re-run the main manual learner flow from start to finish.
6. Write an explicit final verdict with remaining risks, if any.

**The final verdict must answer all of these explicitly**

1. `Hint behavior complete:` Yes or No
2. `Short AI evaluation complete:` Yes or No
3. `AI rewrite behavior complete:` Yes or No
4. `Reload/persistence behavior complete:` Yes or No
5. `Can this task be closed now:` Yes or No
6. `If No, exactly what remains:` short list only

**Verification gate**

```bash
dotnet build TCTEnglish/TCTEnglish.csproj
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build
```

If the full suite is noisy in the environment, run the widest safe Writing-related slice and state clearly what was and was not run.

**Required final re-check**

1. `Hint` does not require AI.
2. `Hint` does not leak all sentence translations in initial payloads.
3. `Chi tiet danh gia` is short and readable.
4. `Cau goi y chinh lai` is useful when the learner answer fails.
5. Reload does not wipe the learner-visible evaluation snapshot.
6. `ContainsReferenceAnswerLeak(...)` or equivalent guard logic no longer suppresses useful learner-facing rewrites incorrectly.
7. No misleading fallback copy remains.
8. No new route/controller boundary violations were introduced.

Stop only after the verdict is explicit and evidence-backed.

---

## 7. Exit criteria

This plan is complete only when:

1. Phases 1 through 6 are done.
2. Phase 6 says:
   - `Hint behavior complete: Yes`
   - `Short AI evaluation complete: Yes`
   - `AI rewrite behavior complete: Yes`
   - `Reload/persistence behavior complete: Yes`
   - `Can this task be closed now: Yes`

---

## 8. Notes for future agents

1. Do not turn `Hint` back into a generic tip system.
2. Do not turn `Hint` into an AI call unless the user explicitly changes the product decision.
3. Do not preload all answers to the client just because `Hint` now reveals one sentence on click.
4. Do not keep long essay-like AI feedback.
5. Do not claim the reload problem is solved if the data still disappears on refresh.
6. Keep the Writing learner slice narrow and honest.

---

## 9. Ready-to-send phase commands

These short commands are enough. The agent should get the rest from this file.

### Phase 1

`@workspace Read docs/writing-hint-feedback-execution-plan.md and execute only Phase 1.`

### Phase 2

`@workspace Read docs/writing-hint-feedback-execution-plan.md and execute only Phase 2.`

### Phase 3

`@workspace Read docs/writing-hint-feedback-execution-plan.md and execute only Phase 3.`

### Phase 4

`@workspace Read docs/writing-hint-feedback-execution-plan.md and execute only Phase 4.`

### Phase 5

`@workspace Read docs/writing-hint-feedback-execution-plan.md and execute only Phase 5.`

### Phase 6

`@workspace Read docs/writing-hint-feedback-execution-plan.md and execute only Phase 6.`
