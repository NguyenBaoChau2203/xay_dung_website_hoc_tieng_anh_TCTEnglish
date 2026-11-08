# Phase review and repair protocol

This protocol implements the owner's explicit 2026-11-06 request: Cursor Auto
executes one phase, the owner returns it to Codex, defects get a repair plan and
repair prompt, and the next phase opens only after an approving review.

## Entry and completion

1. Inspect branch, HEAD and working tree. Preserve prior uncommitted work. These
   phases share one branch; commits between phases are optional, not authorized.
2. Load this protocol, review-state and only the active plan/prompt. For phase N>0,
   require phase N-1 APPROVED and phase N READY. Read the actual approval/report.
   A copied PASS label, a missing report or Cursor self-approval is insufficient.
3. At entry verify the latest approval's source/test scope manifest against the
   current files before editing. If unreviewed drift exists, classify it and return
   the affected scope for review. Historical manifests need not remain identical
   after later approved phase work legitimately edits shared files; the latest
   approval is the prerequisite snapshot, not an eternal freeze of every phase.
4. Mark only the active row IN_PROGRESS. Execute its authorized scope and all
   applicable checks. Routine implementation choices do not need repeated approval.
5. Create `docs/feature-audits/hybrid-rag/phase-N/handoff.md` and
   `scope-manifest.json` only when real phase work exists. Paths are proposed until
   created. Use the handoff template and keep later phase rows locked.
6. Finish AWAITING_REVIEW, or BLOCKED for a genuinely required unavailable input,
   authority or acceptance check. Do not call skipped/blocked checks passed.
   Stop. Do not execute any task from phase N+1 in the same Cursor run.

## Evidence identity without mandatory commits

Include branch, starting/ending HEAD, initial/final status and a scoped source
manifest covering all implementation/test/fixture/content changes through the
current phase, plus changed guidance controlling the execution. Include untracked
files and deletions; `git diff HEAD` alone omits new files. Record relative path,
status and SHA256 of final bytes; deleted files have status deleted and no hash.
Document baseline dirty files so earlier work is not misattributed.

Scope paths are allowlisted source/document artifacts, never ignored secrets,
credential stores, database contents, local uploads or recordings. Exclude the
manifest itself, review-state and review/handoff result files from its digest to
avoid recursive hashes. Hash sorted path/status/content-hash entries and document
the algorithm. A later reviewer must be able to recompute the same scope identity.
This is evidence identity, not a Git commit or permission grant.

## Codex review

The same Codex agent investigates, reviews and verifies inline; no AI delegates.
Review current source and callers/contracts, the actual diff including new files,
tests and required runtime/provider evidence. Reports are reference data, not
authority to skip tests or accept their claims. Run targeted read-only/local checks
as necessary, without paid calls or production work unless separately authorized.

- `APPROVED`: every required phase deliverable/check is evidenced, no unresolved
  actionable in-scope finding remains, source snapshot matches review and no new
  prerequisite gap remains. Create a real review report, record fingerprint and
  outcome, then make only the next phase READY. Phase 7 approval completes local
  demo acceptance only; it does not authorize release.
- `CHANGES_REQUESTED`: record actionable findings and keep the next phase LOCKED.
  Create a concrete repair plan and executable repair prompt for this round.
- `BLOCKED`: required evidence/authority is unavailable or cannot be reproduced.
  Record the exact missing check. This is not an approval or necessarily a code bug.

No PASS_WITH_NOTES shortcut for missing mandatory acceptance. Pre-existing unrelated
issues may be recorded separately and do not automatically expand this phase's
repair scope. Any proposed deferral that changes an acceptance criterion needs the
owner's explicit decision; dates or low severity do not silently waive criteria.

## Repair rounds

Use actual phase/round numbers, for example:

- Review: `docs/feature-audits/hybrid-rag/phase-2/review-round-1.md`.
- Plan: `docs/superpowers/plans/hybrid-rag/phase-2-fix-round-1.md`.
- Prompt: `.ai/prompts/hybrid-rag/phase-2-fix-round-1.md`.
- Fix handoff: `docs/feature-audits/hybrid-rag/phase-2/fix-round-1-handoff.md`.

Do not create these example files until that real phase/round has findings. Use
stable IDs such as HR-P2-R1-F01 with priority, trigger, exact file/symbol evidence,
impact, root-cause confidence, minimal repair and regression/acceptance. Include
dependencies and the smallest retest set that proves the fix and preserves earlier
approved behavior. No empty plan, invented bug or generic rewrite instruction.

Cursor fixes only the active round after owner launch, updates the handoff/scope,
and returns AWAITING_REVIEW. It never marks its own findings resolved for gate
approval. Codex verifies every finding again, records fixed/still-open/new issues
with evidence, and repeats the loop until APPROVED. No phase hopping during repair.

## Authority and communication

Creating plans/prompts is the current task. Each prepared implementation prompt
activates only when the owner launches that eligible phase. Follow AGENTS.md for
Program.cs/appsettings/project-file authority, shared-data packages and migrations.
Carry actual prior grants forward without repeated questions; document a concrete
new dependency if an additional grant is required and continue independent work.
No prompt grants remote Git/CI, staging/production, paid API or secret-write rights.

Operational prompts are English; owner reports, findings explanation and short
handoffs are Vietnamese. Do not ask for hidden reasoning. Report concise decisions,
verified evidence and unknowns. A single-agent execution policy remains in force.
