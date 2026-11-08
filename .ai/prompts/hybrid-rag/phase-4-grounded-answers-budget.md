# Cursor Auto — Phase 4 only: grounded answers and budgets

## Entry and authority

Execute only `docs/superpowers/plans/hybrid-rag/phase-4-grounded-answers-budget.md`
on owner launch. Mode IMPLEMENT_LOCAL; require Phase 3 APPROVED, matching latest
scope and Phase 4 READY. Read review-gates/state, accepted contracts, active bug
memory and plan skills. Single agent; preserve dirty work and existing workloads.

Carry narrow recorded grants forward. This prompt grants no paid API, new protected
config, remote Git or release actions. Any persisted stage/accounting change must
ship its migration/provider evidence in this phase; do not defer it to Phase 5.

## Execute

1. Specify and test the complete turn/usage state machine before connecting the
   paid-stage clients. Include separate known/unknown embedding/generation spend,
   active-stage ownership, cleanup and recovery. Preserve the question-limit policy.
2. Implement strict answers: typed personal facts, current source sentence selection,
   facet/entity support, mixed requests, follow-up resolution and safe outcomes.
   Historical model text is not evidence. Copy factual source text server-side;
   no invented definitions/examples/translations or unconstrained fallback.
3. Add scoped constrained-output transport only if needed; verify official API
   schema and fake HTTP behavior without changing non-chat consumers.
4. Reserve conservative bounded context/embedding/output budget before paid stages.
   Connect hybrid retrieval to the actual chat boundary only with the lifecycle
   checks in place. Preserve one guard/reservation, idempotency and cancellation.
5. Resolve the current Pending/ActualTokens recovery race across conversations:
   another reserve must not terminal-settle an actively generating turn. Use a
   durable stage-aware design, preserve known spend and bound crash/unknown recovery.
   Do not let quota persistence flush pending assistant entities from another scope.
6. Add meaningful failing-before/passing-after tests for the plan's race, fault,
   cancel, budget, source-injection and unsupported-answer scenarios. Use barriers
   and separate scopes, not sleep-based claims of concurrency coverage.
7. If models/mapping change, apply tct-ef-migration now: impact assessment, generated
   migration+Designer+snapshot, pending-model check, disposable MariaDB 10.5.23
   upgrade/repeat/data and concurrency checks. Preserve history and local dev data.
8. Run focused integrated regressions/build and review the complete diff. Update
   bug memory for verified fixes; create lifecycle evidence and phase-4 handoff/scope.

## Stop

Satisfy Plan 4's required checks and distinguish fake-provider results from live
API evidence. Missing provider-specific migration/concurrency checks block approval.
Report in Vietnamese, set Phase 4 AWAITING_REVIEW and stop. Do not implement durable
citations/UI, approve this phase or unlock Phase 5 yourself.
