# Phase 1 closeout repair — review round 6

Owner explicitly requested Codex to finish remaining issues during review round 6
on 2026-11-06. Mode IMPLEMENT_LOCAL for the remaining F01 defect, using the same
agent for implementation, review and verification. Do not hand another repair
back to Cursor when this authorized work can be completed here.

Baseline: feature/hybrid-rag-chatbot, HEAD
6af768bd32d16cc6d012d484ee82df4cab805c69; 46-entry returned fingerprint
2f8ca1d0d125a456703985a5f8606242f53548de4704218f4a9557e378f2d16d, verified.
F02/F03/F04/F05 remain closed. Retain current narrow Program.cs DI grant; no new
protected configuration, schema, content, paid API or remote Git/release work.

## Verified residual and repair

F01: `Explain why France has seasons` and its Vietnamese counterpart return
Answered/full from a France card defining only `a European country`. A second
forecast case reproduces the same error. `Explain France` and real definition
lookups are positive controls. Presence of explain/meaning is not a parsed lookup
request; every accepted lookup must carry a complete target, and selected card
evidence must match it, rather than any query token.

- [x] Add failing production-boundary SQLite regressions for causal questions,
      equivalent wording, partial target hits, actual phrase lookup and valid
      lookup controls. Retain all earlier unsupported fact tests.
- [x] Parse supported lookup frames into a transient kind/complete target. Unknown
      input remains unsupported; do not blacklist why/vì sao as the entire fix.
- [x] Bind selected cards to the complete normalized requested target and required
      stored field. Preserve source qualification, bounded retrieval and IDs.
- [x] Keep response text, evidence, outcome and scope consistent for missing or
      unsupported targets; preserve typed platform/user-data flows and lifecycle.
- [x] Run focused red/green tests, full Phase 1 regression filters, independent
      probes and appropriate broader chat/provider checks. Run tooling validation,
      Python suite and diff checks; keep Phase 0 fixture/predecessor evidence.
- [x] Append verified bug memory, write fix-round-6-handoff and review-round-6,
      refresh cumulative manifest, review full diff and approve only when every
      mandatory Phase 1 check is met. Only then make Phase 2 READY; do not start it.

No new migration or shared-data package is expected for transient parsing and
answer selection. No browser/UI change is planned. Existing Pomelo production
query translation remains required; distinguish it from executed MariaDB proof.
Finish with a Vietnamese report and the eligible Phase 2 prompt if approved.

Verification: red regression group 11 failed/2 passed; green boundary 36/36,
Phase 1 main filter 215/215 and provider/ML/review probe filter 65/65.
The independent 30-observation SQLite probe now satisfies every previously
unsupported fact/cause scenario and preserves the positive source controls.
Final tooling validator passed; Python suite ran 135 tests (134 passed, one
non-Windows rejection contract skipped on Windows). All application tests above
have zero skips. Final scope/approval evidence is recorded in review-round-6.md.
