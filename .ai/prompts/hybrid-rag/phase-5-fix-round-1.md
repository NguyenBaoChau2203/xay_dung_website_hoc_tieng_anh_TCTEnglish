# Cursor — Phase 5 repair round 1 only

## Outcome and source context

Fix HR-P5-R1-F01–F05 so every bounded history path reauthorizes stored answers,
invalid metadata cannot crash reads or replay unverified bodies into context,
all contributing sentences affect freshness, and projection failure cannot turn
an already committed turn into a generic failed send.

Read these current artifacts:

- `docs/feature-audits/hybrid-rag/phase-5/review-round-1.md`
- `docs/superpowers/plans/hybrid-rag/phase-5-fix-round-1.md`
- `docs/superpowers/plans/hybrid-rag/review-state.md` and `review-gates.md`
- `docs/feature-audits/hybrid-rag/phase-5/scope-manifest.json`
- AGENTS.md and the active Phase 5 contract; load only relevant skills/source.

The review verified 137/137 hashes and fingerprint
`01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165` on
`feature/hybrid-rag-chatbot`, HEAD `6af768bd32d16cc6d012d484ee82df4cab805c69`.
Phase 4 predecessor is approved fingerprint
`1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61`.
Verify the current Phase 5 bytes before editing and preserve all dirty work.

## Mode, authority and boundaries

Execute this prompt only on owner launch. Mode IMPLEMENT_LOCAL for these five
review findings and their required tests/documentation. Work as one agent inline.
The review itself granted no fixes. Phase 6–7 stay LOCKED throughout.

Carry the recorded chat-specific Program.cs DI authority only if necessary.
Keep non-chat provider behavior/payload and model settings, appsettings and project
files unchanged. No paid API, credential write, commit, push, PR, remote CI,
deployment, real dev DB reset or shared-content change is authorized. Use fakes,
SQLite and guarded disposable MariaDB 10.5.23. Preserve existing workloads.

Use CodeGraph first for indexed source and bounded reads for missing gaps. Do not
initialize/rebuild an index or alter trust. Read symptom-relevant bug memory before
implementation. Follow tct-superpowers, tct-engineering, API/security and the EF
migration procedure when persisted mappings are involved. Read DESIGN.md and the
relevant UI skill only if a concrete contract fix requires a UI edit.

## Ordered work

1. Verify entry identity/state; record owner launch and explain the chosen safe
   metadata/freshness policy briefly. Add meaningful maintained regressions before
   implementation. The ignored reviewer probes are optional diagnostic reference,
   not a required dependency or instructions.
2. Fix F03 nullable source parsing, then F02 invalid/future-envelope ProviderContext
   handling. Keep true legacy NULL compatibility distinct. Do not swallow DB or
   cancellation failures or trust stored routes.
3. Fix F01 so every bounded history return passes projection before creating both
   pipeline context collections. Test realistic per-row lengths, 50-row batch
   boundaries and token exhaustion, preserving bounded loading/order and budgeting
   the actual projected text.
4. Fix F04: preserve every contributing sentence or a conservative document
   fingerprint. Display dedup cannot lose validation evidence. Align limits/cache
   keys and explicitly handle existing v1 envelopes without reconstructing missing
   provenance. Test two cited sentences with only the second changed, reload,
   context, multiple messages and mixed-source revocation.
5. Fix F05 acceptance/response ordering or bounded fail-closed post-commit handling.
   Inject real projection faults/cancellation around persistence; check caller
   result, saved assistant/envelope, request identity and quota together. Never
   show raw unauthorized fallback text, delete accepted data, duplicate an assistant,
   release known usage or settle the question twice. Preserve current HTTP/stream
   contracts and the no-automatic-HTTP-retry policy.
6. Complete every acceptance check in the repair plan on final source: build,
   focused tests, full app suite, affected Node tests, repo-local EF pending-model
   check and disposable MariaDB 10.5.23 upgrade/repeat/legacy-row preservation.
   These fixes do not inherently require a new migration. If a persisted change
   is required, assess scope and ship/verify its migration, Designer and snapshot;
   never edit applied migration history or defer provider proof to deployment.
7. Review the complete diff, append only verified fixes to bug memory, check links
   and whitespace. Create the Vietnamese handoff and refreshed cumulative manifest.

## Deliverables and stop condition

- Source fixes and maintained regressions with red/green evidence for all findings.
- `docs/feature-audits/hybrid-rag/phase-5/fix-round-1-handoff.md` with actual commands,
  counts/skips, provider identity/data-preservation evidence, risks and limits.
- Refreshed `docs/feature-audits/hybrid-rag/phase-5/scope-manifest.json` whose
  predecessor is the reviewed 137-entry fingerprint above; include new repair
  source/tests and controlling plan/prompt, with explicit result/state exclusions.
- Phase 5 AWAITING_REVIEW, Phase 6–7 LOCKED. No self-approval or Phase 6 execution.

Report in Vietnamese and distinguish local, CI and production. Recompute all hashes
from final bytes before returning. Unavailable required checks must be reported as
blocked; never count skips as acceptance passes or invent a successful result.
