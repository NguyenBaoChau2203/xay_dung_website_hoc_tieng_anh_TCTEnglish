# Cursor Auto — Phase 0 repair round 2 only

## Outcome, entry and authority

Repair **HR-P0-R2-F01** from
`docs/feature-audits/hybrid-rag/phase-0/review-round-2.md` using
`docs/superpowers/plans/hybrid-rag/phase-0-fix-round-2.md`.
This completes the remaining R1-F02 scenario-consistency gap. R1-F01/F03/F04/F05
are resolved by round-2 review and need only regression preservation.

On owner launch use IMPLEMENT_LOCAL for test-validator code and phase evidence
only. Require Phase 0 CHANGES_REQUESTED and Phase 1–7 LOCKED. Read AGENTS.md,
prompt authoring rules, review gates/state and the actual repair plan. One agent
in Cursor Auto; inspect the actual branch/HEAD/dirty files with CodeGraph first.

Reviewed branch feature/hybrid-rag-chatbot, HEAD
6af768bd32d16cc6d012d484ee82df4cab805c69, returned fingerprint
b4205acda7cc68a5e0ec94a1c68410b6d7ee3181381bdebfec09f69d8ea65ce2.
Account for newly added round-2 review/coordination files without discarding work.

## Scope

Edit HybridRagFixtureValidator.cs, HybridRagEvaluationBaselineTests.cs and only
necessary test helpers/evidence/verified bug memory. Keep the frozen revision-2
fixture, 120 stable cases, 80/40 split and canonical hash unchanged. No application,
protected config/project, migration, real shared content, key, paid API, remote Git,
CI or deployment changes. Do not implement Phase 1 or Phase 4 behavior.

## Execute in order

1. Reproduce the four round-2 probes on in-memory clones and add meaningful failing
   regressions: unexhausted quota, provider outcome with scenario none, cancelled
   outcome with scenario none, and tombstone targeting an undeclared case source.
2. Add generic consistency checks across the supplied scenario, expected outcome
   and declared source scope. Keep environment preparation independent: compare
   existing setup to the label, never create setup from labels/question text/IDs.
3. Require actual quota exhaustion for that expected outcome, compatible explicit
   fault/cancel state for the corresponding outcomes, and declared-source membership
   for targeted scenarios. Retain all existing schema/hash/access/stale checks.
4. Prove the original fixture remains valid and the four malformed clones now fail
   for the correct reasons. Preserve LF/CRLF and earlier mutation regressions.
5. Run all applicable commands/acceptance in the repair plan. Review the complete
   diff, confirm unchanged fixture hash, update verified bug memory and create the
   fix-round-2 handoff plus refreshed cumulative source manifest. Keep prior reviews.

## Handoff and stop

Report in Vietnamese: failing-before/passing-after, actual counts/commands, source
scope, unchanged fixture and remaining limits. Set Phase 0 AWAITING_REVIEW, keep
Phase 1–7 LOCKED and stop. Do not self-approve or open/start another phase.

## Owner launch message

```text
Execute .ai/prompts/hybrid-rag/phase-0-fix-round-2.md only. Repair HR-P0-R2-F01, preserve the frozen revision-2 fixture, run the required checks, and stop at AWAITING_REVIEW with Phase 1–7 LOCKED. Do not implement Phase 1.
```
