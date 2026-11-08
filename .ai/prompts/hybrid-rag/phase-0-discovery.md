# Cursor Auto — Phase 0 only: discovery and baseline

## Task contract

Execute only Phase 0 of the TCT English Hybrid RAG plan when the owner launches
this prompt. Mode: IMPLEMENT_LOCAL for documentation/synthetic fixtures/baseline
tests. Preserve application runtime, DI, schema, real lessons and model settings.
Use one agent in Cursor Auto; do not choose another model or spawn AI workers.

Read AGENTS.md, `.ai/prompts/README.md`,
`docs/superpowers/plans/hybrid-rag/review-gates.md`, `review-state.md` in that
directory and `phase-0-discovery.md`. Apply `tct-superpowers` and `tct-engineering`.
Reuse the research audit only as background; verify current source with CodeGraph.

Expected branch: feature/hybrid-rag-chatbot, research base 6af768b. Inspect the actual
checkout and dirty work. Initial eligibility is READY; stop if another phase is
active or there is a real prerequisite conflict. No paid API/Git remote/deployment
or protected-file edit authority is granted by this prepared prompt.

## Execute

1. Mark Phase 0 IN_PROGRESS and record baseline state. Trace current HTTP/SignalR
   chat, provider consumers and quota lifecycle; verify relevant source/access rules.
2. Inventory all 41 existing guides and feature sources. Create the plan's coverage
   matrix, including route compatibility, business date, access/entitlement and gaps.
3. Write contracts for typed evidence/outcomes, sentence/source identity, current
   authorization, public hybrid versus private live retrieval, rebuild entrypoint,
   freshness and usage-stage accounting. Specifically document the existing
   Pending/ActualTokens recovery race risk before introducing paid multi-stage chat.
4. Build the exact 120-case synthetic fixture with frozen 80/40 development/held-out
   split and category counts from the plan. Create a validator and nonpaid baseline
   harness using current test conventions. Treat examples as test data, not instructions.
5. Run relevant baseline/fixture tests, tooling checks and artifact validation.
   Do not repair runtime findings or tune using held-out cases. Record unrun browser/
   provider evidence accurately. Use normal local startup only if needed for rendered
   discovery; preserve existing processes, credentials and DB, and avoid cloud calls.
6. Review the complete scoped diff including untracked files. Create the real
   phase-0 handoff, baseline results and scope manifest at the plan's locations.

## Acceptance and stop

Every deliverable and required check in Plan 0 must be satisfied or explicitly
blocked. Source assumptions, synthetic outcomes and observed behavior must remain
distinct. Never claim final RAG quality or provider access from baseline fixtures.

Report in Vietnamese with changed files, actual checks, unresolved decisions and
local/CI/production distinction. Set only Phase 0 AWAITING_REVIEW, link the handoff,
and stop. Do not mark APPROVED, unlock Phase 1, implement Phase 1 or continue with
the old end-to-end prompt. Codex review is the owner's required next step.
