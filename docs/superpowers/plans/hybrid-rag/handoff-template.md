# Template: phase handoff evidence

This is a template, not a completed report. Create the actual handoff under the
active phase's audit directory. Replace every placeholder with facts, or explicitly
mark a check not run/blocked. Write the owner-facing report in Vietnamese.

## Identity and scope

- Phase and repair round, if any; plan/prompt used; branch; starting/ending HEAD.
- Initial dirty files and work preserved; files created/modified/deleted this phase.
- Actual protected-file/live-API grants and their limits; never credentials.
- Links to the current scope manifest, predecessor approval and relevant artifacts.

## Observable result

Describe the resulting behavior with concrete successful and refused/failed examples.
Separate behavior tested through fakes from actual browser/live-provider observations.
List checklist items completed, deferred by explicit owner decision, or blocked.

## Verification ledger

| Check | Exact command or browser steps | Environment/provider | Exit/result | Evidence |
| --- | --- | --- | --- | --- |
| Populate with actual checks | Do not insert example PASS values | Record actual context | Passed/failed/skipped/blocked | Actual artifact or concise output |

For regressions include the failing-before result and passing-after result when
applicable. For benchmarks record counts, denominators, hardware, corpus/index/model
versions and cold/warm conditions. A skipped provider/browser check is not pass.
For schema changes include migration/Designer/snapshot, pending-model result,
disposable MariaDB identity, upgrade/repeat and preserved-row/relationship evidence.
For content edits include selective export ownership/version/hash review results.

## Risk and review queue

List remaining defects, missing source coverage, unavailable checks and scope drift.
State whether previous approved behavior could be affected, and where the reviewer
should focus. Include a minimal local reproduction, without passwords or private rows.

## Final state

Set the active row to AWAITING_REVIEW or BLOCKED, link the real handoff and stop.
Do not write APPROVED, unlock another phase or describe local checks as CI/release.
End with a short Vietnamese message the owner can paste into Codex for review.
