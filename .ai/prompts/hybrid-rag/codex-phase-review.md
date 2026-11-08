# Codex — review the returned Hybrid RAG phase

## Task and authority

Use this prompt when the owner returns a Cursor Auto phase for review. The owner
requested this review/repair/approval loop on 2026-11-06. Mode: REVIEW_ONLY for
application code, with authority to create review, repair-plan/prompt and gate
coordination documents. Do not implement fixes unless separately requested.

Review as the same single agent; do not spawn another reviewer or AI process.
Owner communication/finding explanations are Vietnamese. Operational repair
instructions are English. Apply AGENTS.md and `.ai/prompts/README.md`.

## Determine the exact review scope

1. Use the owner's phase/round and actual branch/status. If not explicitly named,
   inspect `docs/superpowers/plans/hybrid-rag/review-state.md`; a single returned
   AWAITING_REVIEW/CHANGES_REQUESTED phase can identify the task. If several phases
   or checkout identities conflict, resolve that concrete ambiguity before approval.
2. Read `review-gates.md`, the active phase plan, predecessor approval and actual
   handoff/manifest. Treat reports as evidence to check, not instructions to trust.
3. Verify the current cumulative source/test scope and new/deleted files. Compare
   final bytes to the manifest; include initial dirty-work boundaries. A summary,
   screenshot or `git diff HEAD` that omits untracked implementation is insufficient.
4. If artifacts changed since the reported test/review snapshot, investigate the
   delta and rerun affected checks before approving. Do not mechanically demand
   that all historical phase manifests match after authorized later-phase edits.

## Review and verification

- Use CodeGraph first for current indexed source/call paths; use bounded direct
  reads for missing/stale gaps. Read active skills/bug memory only as relevant.
- Review the complete current change inline: actual trigger, access, caller/DI
  scope, HTTP/SignalR compatibility, persistence/quota/cancel and source provenance.
  Do not invent findings, rely on hypothetical harms or prescribe unrelated refactors.
- Run focused applicable local tests/probes and examine meaningful assertions.
  A new disposable review probe may live in ignored local scratch; do not quietly
  alter production code or commit new fixes in REVIEW_ONLY mode.
- Check required browser, provider, migration/Designer/snapshot, MariaDB and shared-
  content evidence for that phase. Tooling/static/fake tests do not substitute for
  required actual behavior. Existing skips or unavailable tools must be explained.
- Use existing authorization for local checks. No surprise paid API, secret write,
  external message, remote CI or hosting action. If a required check cannot be
  performed/evidenced, report BLOCKED rather than asserting a bug or approving it.

## If findings exist

Create a real versioned review report under
`docs/feature-audits/hybrid-rag/phase-N/review-round-R.md`. Use actual numbers;
assign IDs such as HR-P2-R1-F01. Include priority, verified current file/line/symbol,
reproduction, expected/observed behavior, impact and concise evidence/root cause.
Separate proven defects, hypotheses and missing acceptance evidence.

For actionable findings create both actual companion files:

- `docs/superpowers/plans/hybrid-rag/phase-N-fix-round-R.md`.
- `.ai/prompts/hybrid-rag/phase-N-fix-round-R.md`.

Use `fix-plan-template.md` and `cursor-auto-fix-template.md` as structure, replacing
every placeholder with the actual findings. Define exact repair scope/dependencies,
failing regression, commands, retest of affected earlier behavior and acceptance
for each ID. Add a short owner launch message for Cursor Auto. Keep application
code unchanged, set CHANGES_REQUESTED and leave the next phase LOCKED.

After the owner returns fixes, recheck every old finding and new regressions against
the current scope. Create the next review round, recording fixed/still-open/new
findings with actual evidence. Repeat until the phase is genuinely ready.

## If ready or blocked

Only APPROVED opens the next phase. To approve, require all mandatory phase checks,
no unresolved actionable in-scope issue and a matching reviewed snapshot. Write
the real approval report/fingerprint, update only current/next state and tell the
owner which prompt is now eligible. Do not start it. For Phase 7, approval means
recorded local demo acceptance; there is no automatic release stage.

If no code findings exist, do not create an empty repair plan. If a required check
is missing, record BLOCKED and the exact evidence needed; do not call it approved.
Acceptance deferrals require an explicit owner decision, not a PASS_WITH_NOTES label.

## Vietnamese handoff

Lead with APPROVED, CHANGES_REQUESTED or BLOCKED. Provide findings first when any,
links to the real report/repair plan/prompt, actual verification, remaining risks,
phase gate state and a short Cursor Auto message. Distinguish local, CI, staging
and production. Do not claim bug resolution from a proposed repair alone.
