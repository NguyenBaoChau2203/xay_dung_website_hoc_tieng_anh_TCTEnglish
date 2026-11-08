# Cursor Auto — Phase 0 repair round 1 only

## Outcome, evidence and mode

Repair HR-P0-R1-F01 through F05 from
`docs/feature-audits/hybrid-rag/phase-0/review-round-1.md` by following
`docs/superpowers/plans/hybrid-rag/phase-0-fix-round-1.md`.
Current phase state is CHANGES_REQUESTED; Phase 1–7 remain LOCKED.
Mode on the owner's explicit repair launch: IMPLEMENT_LOCAL for Phase 0 synthetic
fixtures, validator tests and discovery/contracts/evidence documents only.

Read AGENTS.md, `.ai/prompts/README.md`, review-gates/state and the actual repair
plan. Work as one Cursor Auto agent; no AI delegates/model switching. Verify the
actual branch/HEAD/dirty work against the reviewed branch feature/hybrid-rag-chatbot,
HEAD 6af768bd32d16cc6d012d484ee82df4cab805c69 and returned scope fingerprint
f671a9f1a49282d5cab76474efe132e557dcaa07d482ba35c8decc6bb81b6cdd.
Account for the review/repair coordination files added after that snapshot.

## Scope and authority

Allowed scope: `TCTEnglish.Tests/Fixtures/AI/hybrid-rag-evaluation.json`,
`TCTEnglish.Tests/HybridRagEvaluationBaselineTests.cs`, necessary focused fixture
validator helpers/tests, Phase 0 audit/contract/baseline/manifest artifacts and
verified bug memory. Optional `.gitattributes` pinning must target only this fixture.

No application runtime, Program.cs, appsettings.json, .csproj, real shared content,
schema, credential store, paid API, remote Git/CI or deployment changes. Preserve
all existing work. Do not start Phase 1 or implement Phase 4 quota behavior.

## Ordered execution

1. Inspect current source via CodeGraph, reproduce the reviewed oracle/scenario/
   newline issues and read the relevant line-ending bug memory. Keep historical
   fixture revision/hash and the round-1 review intact.
2. F01: audit all 120 oracle cases. Correct Quiz/Matching's unrelated answers and
   any other proven question/fact mismatch. Specify supported facets and typed
   derived expected values where necessary, not only verbatim source containment.
   Preserve 120 stable IDs and the category-specific 80/40 split; record revisions.
3. F02: define explicit deterministic scenario/action setup for faults, quota,
   cancel, deletion/unpublish/stale hash and access transitions. Never derive world
   state from a user's claim, expectedOutcome or case ID. Add mechanical validators
   and meaningful malformed-clone negative tests without changing the runtime.
4. F03: hash the frozen fixture using a documented canonical UTF-8/LF definition.
   Test LF/CRLF equivalence and changed-content rejection. Keep raw scope identity
   distinct; updating a machine-specific raw constant alone is insufficient.
5. F04: correct quota documentation against actual reserve/finalize counter code.
   Settled keeps the used-question sum; Released is different. Retain the supported
   generation-token race without inventing a returned Standard question slot.
6. F05: correct discovery provenance: `.codegraph` exists in the reviewed checkout;
   directory presence and Cursor MCP capability are separate observations. Use
   available graph/pinned CLI or accurately record fallback. No init/rebuild/sync.
7. Write the semantic oracle audit, rerun the exact applicable checks in the repair
   plan and review the full scoped diff, including untracked files. Update only
   verified bug memory and actual new baseline measurements; keep old results labeled.
8. Produce fix-round-1-handoff.md and the refreshed cumulative scope manifest,
   mapping every finding to real evidence. Include controlling repair guidance,
   exclude recursive/result/state files as the review protocol specifies.

## Acceptance and mandatory stop

Every finding's repair criteria in the plan must be evidenced. Mechanical tests
cannot replace semantic oracle review; fake/synthetic checks cannot claim model
quality. No paid API/browser/schema change is required for this repair scope.

Report in Vietnamese: each ID addressed/pending, files, commands/counts, remaining
limits and local-only status. Set Phase 0 AWAITING_REVIEW, keep Phase 1–7 LOCKED and
stop. Do not mark APPROVED or claim Codex has verified your fixes.

## Owner launch message

```text
Execute .ai/prompts/hybrid-rag/phase-0-fix-round-1.md only. Repair HR-P0-R1-F01 through F05, run the required checks, preserve Phase 1–7 as LOCKED, and finish AWAITING_REVIEW with a Vietnamese handoff. Do not implement Phase 1.
```
