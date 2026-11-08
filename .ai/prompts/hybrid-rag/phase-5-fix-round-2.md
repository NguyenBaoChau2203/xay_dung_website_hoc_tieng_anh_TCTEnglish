# Cursor — Phase 5 repair round 2 only

## Outcome and context

Complete the residual cases of HR-P5-R1-F02 and HR-P5-R1-F05, and correct the
manifest predecessor reference HR-P5-R2-F06. Preserve the verified F01/F03/F04
repairs. Do not start Phase 6 or 7.

Read:

- `docs/feature-audits/hybrid-rag/phase-5/review-round-2.md`
- `docs/superpowers/plans/hybrid-rag/phase-5-fix-round-2.md`
- `docs/superpowers/plans/hybrid-rag/review-state.md` and `review-gates.md`
- `docs/feature-audits/hybrid-rag/phase-5/scope-manifest.json`
- AGENTS.md, active metadata/history contracts and relevant skill/source only.

Verified input: 140 entries, fingerprint
`6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`, branch
`feature/hybrid-rag-chatbot`, HEAD `6af768bd32d16cc6d012d484ee82df4cab805c69`.
Verify this snapshot before edits. Its predecessor fingerprint identifies the
137-entry Phase 5 review, despite the stale Phase 4 path in predecessorManifest.

## Mode and authority

Execute on owner launch only, as one agent in IMPLEMENT_LOCAL for this repair.
The review itself granted no implementation. Preserve dirty work, running
workloads, real data and existing phase approvals. Phase 6–7 remain LOCKED.

No paid API, credential write, commit, push, PR, remote CI, deployment, dev DB reset,
shared-content edit or new protected config/project/model-provider setting change
is authorized. These fixes do not require a new migration. Carry existing narrow
DI authority only if a concrete need exists; do not widen it.

Use CodeGraph first for indexed source, bounded reads for gaps, and canonical
engineering/debugging/security/API procedures. Read symptom-relevant bug memory.
No subagents, index rebuild, trust bypass or unrelated cleanup.

## Execute

1. Verify entry identity/state and add maintained failing regressions for the
   review's exact triggers. Follow the linked plan, including control cases.
2. F02: require the sources field and its valid array structure according to the
   current envelope contract. Missing sources must be unverified and must remove
   the old body from ProviderContext. Preserve true legacy NULL metadata and
   explicit valid empty arrays/source-free outcomes. Keep narrow parse failures.
3. F05: define a finite post-commit projection deadline with an appropriate
   independent cancellation token. Test a projection that waits for cancellation,
   not an immediate synthetic throw. Timeout/error/cancel must return safe content
   while preserving accepted identity, one assistant/envelope and one settlement.
   Do not return unauthorized raw text or abandon tasks using disposed DbContext.
   Verify normal reply, HTTP/service, stream completion and later authorized reload.
4. F06: make the next manifest's predecessor reference match the reviewed 140-entry
   fingerprint. Use a real historical artifact or clearly named review reference;
   remove the misleading optional path if necessary. Never fabricate historical
   hashes or point the predecessor at a mutable self manifest.
5. Run the linked plan's build, focused/full regressions and appropriate provider/
   pending-model checks on final source. Preserve previous tests and no-automatic-
   retry behavior. No paid calls. Review diff, update verified bug memory, validate
   links/whitespace and recompute the final cumulative manifest.

## Deliver and stop

Write `docs/feature-audits/hybrid-rag/phase-5/fix-round-2-handoff.md` in Vietnamese
with actual checks, exact counts/skips, schema/provider evidence and limits. Refresh
`scope-manifest.json`; its predecessorFingerprint is the reviewed 140-entry
`6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`.

Return Phase 5 AWAITING_REVIEW and keep Phase 6–7 LOCKED. Do not self-approve.
Distinguish local from CI/production and report genuine blockers rather than
counting skips or unrun checks as passes.
