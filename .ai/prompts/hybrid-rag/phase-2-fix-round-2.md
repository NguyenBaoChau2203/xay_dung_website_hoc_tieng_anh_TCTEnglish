# Cursor Auto — Hybrid RAG Phase 2 repair round 2

## Outcome and context

Repair only the still-open HR-P2-R1-F02 and HR-P2-R1-F03. The basic round-1
examples pass, but generic framing still replaces subject evidence and relation
cue words still admit unsupported full answers. Preserve closed F01/F04 and
complete the outstanding rendered-admin browser acceptance.

Read AGENTS.md, `.ai/prompts/README.md`, the execution-template contract and:

- `docs/feature-audits/hybrid-rag/phase-2/review-round-2.md`.
- `docs/superpowers/plans/hybrid-rag/phase-2-fix-round-2.md` (exact checklist/checks).
- `docs/superpowers/plans/hybrid-rag/review-gates.md` and `review-state.md`.
- Phase 2 `fix-round-1-handoff.md` and `scope-manifest.json`.

Reviewed branch: `feature/hybrid-rag-chatbot`; HEAD:
`6af768bd32d16cc6d012d484ee82df4cab805c69`; 64-entry fingerprint:
`67ec08a66cc20aa6afc25b88750d184e11c1f9e949dbe92ceb9a7d8a817ed80a`.
Recompute hashes, inspect drift and preserve existing dirty work. Use current
CodeGraph source first where available and bounded reads for gaps. Read relevant
bug memory. Apply engineering/superpowers/security/admin/UI procedures inline.

## Mode and authority

IMPLEMENT_LOCAL starts only on explicit owner repair launch. Require Phase 2
CHANGES_REQUESTED and Phase 3–7 LOCKED. Work as one agent. Review alone did not
authorize application edits. Scope is the two open findings, their regressions
and outstanding local browser evidence, with preserved closed-fix controls.

Primary files: `TCTEnglish/Services/AI/Rag/Corpus/RetrievalCorpusService.cs`,
`TCTEnglish/Services/AI/Rag/Corpus/LexicalAnswerAdmission.cs`,
`TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs`, plus tests. No DI,
appsettings/project/dependency/schema/shared-content change is expected. Preserve
OpenAiProviderClient and all non-chat consumers. Do not add embedding, dense
retrieval, model generation or later phases. No paid API, secret write, reset DB,
commit/push/PR/CI/merge/deployment authority is granted.

## Ordered execution

1. Verify entry and mark only Phase 2 IN_PROGRESS. Write meaningful failing
   regressions for residual F02/F03 using real corpus and all production retrievers.
   Do not use the old narrow passing cases as proof that the root cause is fixed.
2. Correct the invalid positive oracle in
   `AiRagPhase2FixRound1Tests.UnsupportedLexicalQuestion_DoesNotBecomeFullAnswer`:
   `Where ... visible?` is not answered by `at dawn`. Keep it negative; use a time
   query for dawn or a true location sentence for a positive. Keep the frozen
   evaluation fixture unchanged.
3. Repair F02 ToHit/QueryOverlap. With title `Daily notes`, A1/Sea keywords and
   body `Here you can find examples in this lesson. The tidal harbor marker is
   visible beside the gate.`, both review queries must retain the marker span
   instead of s1. Framing/common words must not dominate subject-bearing terms.
   Prove direct retrieval, exact text/hash/ID, body-only matching and rechecks;
   boundary refusal alone is not a retrieval fix. Test another subject/order too.
4. Repair F03 admission based on actual requested fact, not any number/because/
   preposition. An installation year is not population, cause of repainting is
   not cause of glowing, dawn is not a location or construction time, and who/
   color requests must not default to supported Reference. Follow the negative
   matrix and correct positive controls in the plan. Unknown/partially supported
   requests remain non-full with truthful text and no unused evidence. A bounded
   conservative deterministic reference/extraction contract is sufficient for
   Phase 2; do not build Phase 4 or add phrase blacklists to pass probes.
5. Preserve F01 explicit operation/rejection/auth/antiforgery/no-write Dry-run,
   F04 v2 exact spans/600 cap/old-v1 rejection, Phase 1 card target/field/access,
   typed live facts, quota/cancel/history and non-chat provider behavior.
6. Run the plan's exact test/build/tooling checks with fresh Phase2FixRound2
   binaries and explicit new-test discovery. Record new red/green regressions,
   actual counts/failures/skips and development baseline. Do not tune BM25 or
   score held-out cases. SQLite is not MariaDB provider execution evidence.
7. Complete actual rendered-admin browser acceptance after login. Use the
   canonical authorized local launcher if starting the app; preserve existing
   workloads, use saved local credentials securely and never echo them. Check
   Dry-run/build/redirect/duplicate submission and before/after snapshot state,
   narrow/desktop layout and keyboard. The isolated copied handler is not this
   acceptance. If a necessary runtime/login prerequisite is unavailable, record
   BLOCKED and the exact missing evidence instead of claiming a pass.
8. Review the complete tracked/untracked diff and append accurate bug memory for
   verified fixes. Write `docs/feature-audits/hybrid-rag/phase-2/fix-round-2-handoff.md`
   in Vietnamese and refresh cumulative `scope-manifest.json`, including executed
   plan/prompt and changed guidance. Exclude result/state/manifest self-digests.

## Acceptance and handoff

Use the repair plan's observable criteria for each finding; preserve positive
stored-data behavior and closed fixes. Do not just relabel unrelated answer text
or change fixtures/oracles to declare missing facts supported. No self-approval.

Report findings addressed/pending, changed files, commands/results, browser evidence
and remaining risks in Vietnamese, separating local/CI/hosting. Finish Phase 2
AWAITING_REVIEW when ready or BLOCKED with the concrete prerequisite. Keep
Phase 3–7 LOCKED and stop; do not start Phase 3.

Owner launch: Execute `.ai/prompts/hybrid-rag/phase-2-fix-round-2.md` in Cursor Auto.
Repair the open F02/F03, preserve F01/F04, complete required browser evidence,
and keep Phases 3–7 LOCKED.
