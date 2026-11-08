# Template: evidence-based phase repair plan

Inactive template. Populate only after an actual review finds actionable defects.
Write execution instructions in English and the owner-facing findings in Vietnamese.
Do not present placeholders as discovered bugs or create an empty repair plan.

## Review identity and authority

Record phase/round, reviewed branch/HEAD/scope fingerprint and real review report.
Mode is PLAN_ONLY for the reviewer preparing the repair; future IMPLEMENT_LOCAL
activation belongs to the owner's repair request. Retain existing narrow grants.
Do not infer new protected-file, paid-API, data or remote Git/release authority.

## Findings to repair

For each real finding record: stable ID, P0/P1/P2/P3 priority, observed trigger,
current file/symbol/verified line, expected versus observed behavior, impact,
evidence and confirmed/suspected root cause. Mark unverified hypotheses explicitly.
State whether it was introduced by the phase or blocks an existing acceptance.

## Ordered repair checklist

For each ID define the minimal affected files, dependencies, failing reproduction,
implementation boundary and acceptance checks. Group only findings sharing a cause;
do not hide unrelated fixes in a broad refactor. Preserve prior approved contracts.
If a persisted model or supported shared content changes, include its real migration
or selective export requirements and provider/data-preservation checks now.

## Retest and exit

Map every finding to a meaningful regression and exact available check. Re-run
affected earlier phase checks when the fix touches their behavior. Specify any
required browser/provider evidence and actual authorized live limits. Include full
diff review and refreshed scope/handoff. All findings must be re-reviewed by Codex;
Cursor finishes AWAITING_REVIEW and keeps the next phase LOCKED.

## Required companion prompt

Create the actual `.ai/prompts/hybrid-rag/phase-N-fix-round-R.md`, binding the same
IDs, files, scope, commands and criteria. Include a short owner launch message.
Use the [prompt template](../../../../.ai/prompts/hybrid-rag/cursor-auto-fix-template.md).
