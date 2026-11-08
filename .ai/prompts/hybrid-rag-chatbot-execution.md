# Hybrid RAG — dispatcher for one reviewed phase

Updated 2026-11-06. The owner's newer instruction requires Cursor Auto to execute
one phase, stop for Codex review, repair actual findings, and receive approval
before the next phase. This dispatcher replaces the earlier end-to-end prompt.

## Resolve the active phase

Read AGENTS.md, `.ai/prompts/README.md`,
`docs/superpowers/plans/hybrid-rag/README.md`, `review-gates.md` and
`review-state.md` in that plan directory. Use the owner's requested phase when
eligible. With no phase number, Phase 0 is the only initially READY phase. If
there is a returned phase awaiting review or fixes, keep that gate active rather
than starting a new phase. Do not treat this file as authority for implementation.

## Execute exactly one task

On explicit owner launch, open only the matching
`.ai/prompts/hybrid-rag/phase-N-*.md` and its plan. Inspect actual checkout/status,
verify the prerequisite approval/scope, and follow that phase's exact authority,
checks and handoff. Do not run multiple phase prompts or legacy overview steps.

Protected-file, paid-API, remote Git/CI/release and data authority still follow
AGENTS.md and actual owner grants. Work as one agent. Preserve current source,
workloads, databases and saved credentials. Prompts do not create new grants.

## Mandatory stop

Cursor ends the active phase or repair round at AWAITING_REVIEW, with the real
handoff/scope evidence and a Vietnamese report. Only the owner-requested Codex
review can mark APPROVED and make the next phase READY. Cursor never self-approves
or automatically starts the next phase. If required evidence is missing, report
BLOCKED. Real findings require a specific repair plan/prompt; no empty repair plan.

For the initial task use
[`Phase 0 prompt`](hybrid-rag/phase-0-discovery.md). For returned work use
[`Codex review prompt`](hybrid-rag/codex-phase-review.md).
