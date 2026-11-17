# 02 — Audit and repair Writing behavior

Model: GPT 6.1 Sol High. Local implementation/fixes only when invoked.
Prerequisite: stage 01 handoff. Preserve Gemini's UI changes and current contracts.

## Source map

- Controllers/StudyController.cs and existing Writing routes under /Home/Writing.
- Services/IWritingService.cs; WritingService.cs, .Catalog.cs, .PrivateExercises.cs,
  .Generation.cs; WritingRequestRateLimiter.cs.
- Services/Writing/WritingAssessmentPolicy.cs and WritingEvaluationOptions.cs.
- Models/UserWritingAttempt.cs, UserWritingSentenceProgress.cs,
  UserWritingExerciseProgress.cs, WritingAcceptedAnswer.cs; Writing ViewModels.
- Learning/CompletionCreditService.cs and outbox are integration boundaries.
  Goals practice-day orchestration is completed in stage 03, not duplicated here.

## Ordered work

- [x] Trace catalog→practice/hint→evaluate→attempt→progress→completion→credit.
  Reproduce actual defects with a meaningful failing regression before a fix.
  Read symptom-relevant bug memory, including NeedsReview persistence, account
  draft isolation and transactional completion. Historical fixes are not proof.
- [x] Verify Standard/Premium/downgraded roles, owner/private/published access,
  exercise/sentence pairing, deleted/unpublished lessons and cross-user requests.
  Parameterized ownership failures return404; preserve auth and antiforgery.
- [x] Verify empty/whitespace/oversized/malformed answers, normalization,
  punctuation/case/Unicode, approved alternatives, wrong tense/meaning and a
  plausible unapproved paraphrase. Local uncertain outcomes remain NeedsReview;
  the UI must not call every uncertain but valid paraphrase simply wrong.
- [x] Ensure hints/bootstrap responses do not reveal full answers before the
  intended workflow permits it. Treat submitted text/feedback as encoded text.
- [x] Optional feedback timeout/429/provider failure must not change the local
  accepted/revision/review decision, leak errors or grant fake pass/XP.
- [x] Verify attempts, best/accepted state, recent history, retry/edit/refresh,
  reload/resume, private draft identity, multi-tab stale responses and deleted
  sentences. A failed retry must not erase a prior accepted sentence silently.
- [x] Verify the final accepted sentence completes the exercise once, and that
  progress+completion+credit+notification intent commit together. Duplicate submit,
  concurrent final submit, cancellation and credit/DB failure cannot lose rewards
  or double-grant them. Fix only demonstrated defects within this boundary.
- [x] Record the persisted accepted-sentence event/transaction location for
  stage 03, including how replay differs from a fresh exercise completion. Do not
  mint new practice-day semantics in an unrelated controller or browser script.

## Acceptance matrix

| Case | Required result |
| --- | --- |
| Correct/approved sentence | Accepted state and persisted progress agree after reload |
| NeedsReview or rejected | No false completion/XP; accurate status and useful feedback |
| Foreign/private/unpublished ID | Access denied with the established HTTP contract |
| Final submit repeated/concurrent | One lifetime completion credit and award intent |
| Credit insert fails | No successful completed progress without its credit |
| Refresh or user switch | Own saved state only; no leaked draft or stale response |
| Optional engines unavailable | Honest local result; no fabricated cloud success |

Use the relevant existing Writing*IntegrationTests, assessment/status/draft Node
tests and completion transaction tests. Run MariaDB 10.5.23 for relational/concurrent
changes; SQLite alone is not sufficient evidence. Persistent changes trigger
tct-ef-migration; include migration/Designer/snapshot and data-preservation proof.
No new content batch or UI redesign in this stage. No paid external requests.

## Handoff

Write `02-writing-backend.md` in the shared ledger directory: confirmed/rejected
findings, fixes, routes/status contracts, regression evidence, accepted-sentence
integration point, schema impact and limits. Report in Vietnamese.

## Execution ledger — 2026-11-14

Completed locally in this checkout. Source-backed findings, red/green regressions,
MariaDB 10.5.23 evidence, browser limits and the accepted-sentence persistence seam
are recorded in [the stage 02 audit](../../../feature-audits/2026-11-14-writing-goals/02-writing-backend.md).
No remote Git/release work or paid API calls. Stage 03 was not started.
