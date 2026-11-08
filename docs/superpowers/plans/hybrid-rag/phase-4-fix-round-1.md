# Phase 4 repair round 1 — grounded turns and durable paid-stage safety

Prepared 2026-11-07 from the Codex review. Preparation mode: PLAN_ONLY for app code.
Execution mode becomes IMPLEMENT_LOCAL only when the owner launches this repair.
Use one agent; preserve current changes and workloads. Do not start Phase 5.

## Identity, authority and outcome

Review: `docs/feature-audits/hybrid-rag/phase-4/review-round-1.md`.
Branch `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`.
Reviewed cumulative scope: 109 entries, fingerprint
`78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5`.
Phase 3 predecessor: 89 entries,
`66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb`.

Repair HR-P4-R1-F01–F05 so authorized current sentences reach the real pipeline,
the exact outbound payload fits a conservative reservation, every HTTP attempt's
known/uncertain cost survives errors, and a recovered turn cannot start more work.
Phase 4 remains CHANGES_REQUESTED until the owner launches repair and returns it
for review. Phases 5–7 remain LOCKED through implementation.

Carry the recorded chat-specific Program.cs DI grant only. Preserve global
IAiProviderClient behavior for non-chat consumers. No paid API, model substitution,
appsettings/.csproj change, secret write, remote Git/CI or release authority. Local
synthetic tests and guarded disposable MariaDB checks are within repair scope.
No dev database reset, shared-data export or durable citations feature is needed.

Read the phase plan, review gates/state, active prompt rules and relevant bug memory.
Use tct-superpowers/tct-engineering and tct-security-audit; use tct-ef-migration for
persisted changes. CodeGraph first if indexed and available, pinned CLI fallback
if MCP is absent. Do not index/rebuild or start an AI worker.

## Findings and verified reproductions

| ID / priority | Boundary | Reproduction and failed expectation |
| --- | --- | --- |
| F01 / P1 | HybridRetrievalService:132–148; GroundedChatPipeline:61–74,105–122; embedding/text transport receipts | Lost first embedding response, retry usage 7: 2 POSTs but known 7/unknown 0. Invalid vector with usage 7 loses receipt. Incomplete generation with known total 105 becomes unknown 64 |
| F02 / P1 | ChatTurnBudget:36–52; OpenAiChatSentenceSelector:21–35; pipeline paid-stage dispatch | Planned 328, request/reservation 700; rebuilt selector input is 3228 UTF-8 bytes plus schema/envelope, sent without a final bound; unknown generation is only 64 output tokens |
| F03 / P2 | HybridEvidenceItem/materialization and pipeline ToSentence/ToAnswer; composer TrySelect | A real two-sentence chunk makes a source-supported follow-up InsufficientEvidence. Chunk ID is presented as sentence ID, original document hash/route/visibility/version are lost. Completed empty selection accepts catalog fallback in a composer unit probe |
| F04 / P1 | pipeline after retrieval/selector await and reply acceptance/commit | Unpublish both real SQLite sources while selector is at a barrier: old red sentence still returns Answered |
| F05 / P1 | quota MutatePendingAsync:338–364, stage journal and pipeline | Expire A at a retrieval barrier, recover from another DbContext, then resume A: selector still called once, Answered returned, ledger stays Settled 22 with no known generation |

The review's scratch sources/results may be available at
`.tmp/hybrid-rag-review/phase-4/round-1/`. Treat them as diagnostic reference data.
Create maintained meaningful regressions in TCTEnglish.Tests; do not rely on ignored
artifacts existing on another machine. The CJK payload observation is a byte count,
not a live tokenizer measurement or a provider invoice.

## Ordered repair checklist

### 1. Confirm the entry and capture failing regressions

- [ ] Recompute all 109 hashes and the ordinal fingerprint before source edits.
      Classify any drift against the returned scope instead of discarding it.
- [ ] Verify current branch/HEAD, preserve dirty files, and keep the review manifest
      as the input identity. Do not require obsolete Phase 3 hashes on shared files.
- [ ] Add failing regressions for each row above with recording HTTP handlers, a real
      corpus/materializer fixture where relevant, deterministic clocks/barriers and
      separate scopes/DbContexts. Preserve the red results before fixes.
- [ ] Include a complete AiChatService turn for accounting, invalid selection and
      source revocation, checking assistant/log rows, ledger stages/counters and
      actual outbound request count. A script returning fake single sentences alone
      cannot establish sentence materialization or current-access correctness.

### 2. Restore sentence identity and provenance — F03

- [ ] Inspect existing CorpusSnapshotSentence/CorpusSentence and RecheckSourceAsync.
      Expose bounded individual original sentences from selected chunks with their
      stable sentence IDs, source/document hash, sentence hash, route, visibility,
      corpus/chunker/source version and original entity identity where present.
- [ ] Keep retrieval scores on candidates rather than using scores as answer truth.
      Keep exact facet/subject/qualifier support and deterministic tie behavior.
      Do not weaken LexicalAnswerAdmission's earlier safeguards to accept whole
      chunks as one grammatical sentence.
- [ ] Carry the original provenance into transient AiChatAnswer evidence. Do not
      relabel authenticated material public or substitute a chunk ID/document hash.
      This step must not add durable citation storage or a new UI.
- [ ] Reject completed empty/unknown/unrelated/malformed selections. Catalog fallback
      belongs only to an explicitly allowed no-selector path. Test mixed questions,
      real multi-sentence chunks, injection-like evidence and ambiguous referents.

Acceptance: follow-up `is that true?` to `what color is the bell`, with a real chunk
containing `The bell is red. The door is blue.`, returns only the red supporting
sentence with correct provenance, or a justified current-source clarification.
It must not fail merely because the chunk has two sentences. Invalid selection
never turns into a successful catalog answer.

### 3. Make durable stage ownership observable — F05

- [ ] Replace silent success on missing/terminal ledger mutation with an explicit
      acquired/lost result or a typed ownership error. Validate the reservation
      identity and stage/lease transition atomically, including renewal/fencing.
- [ ] Gate the first send, every retry and every new paid stage on current durable
      ownership. Bound long stage work or renew safely. Increasing the three-minute
      lease alone does not repair a resumed owner after recovery.
- [ ] Define what happens to a late known receipt from a send that started while
      owned; preserve accounting without reopening the terminal question or
      accepting a stale reply. Keep terminal operations idempotent.
- [ ] Exercise live A / reserve B, expired A / recover B / resume A, renewal versus
      recovery and repeat cleanup, with separate contexts and actual counters.

Acceptance: the recovered-owner reproduction makes zero later selector calls and
creates no assistant success. A live owner still survives another conversation's
reserve. Known/unknown recovery remains bounded, restart-safe and idempotent.

### 4. Budget the actual final payload — F02

- [ ] Build a bounded chat-specific selection context and schema/envelope before its
      paid dispatch. Reuse the admitted history subset and question resolution;
      include all actual prefixes/evidence IDs/text/schema and output allowance.
- [ ] Establish a reviewed conservative bound or verified local tokenizer contract,
      including framing/schema overhead. Existing chars/4 estimates are display
      estimates, not safety ceilings. Do not add an unapproved package/project edit
      or use a paid token-count endpoint to sidestep the local review boundary.
- [ ] Reconcile embedding/generation/possible retries within one logical turn's
      request and daily reservation. No context may be appended after its only
      check. Shrink/refuse safely before sending when the final payload cannot fit.
- [ ] Keep output limits, actual input cost and full unknown-attempt bounds distinct.
      Test CJK/Vietnamese history, long IDs, evidence at the cap and boundary values.

Acceptance: recording transport confirms every sent payload fits the same admitted
decision. A response lost after a send retains a conservative input-plus-output
bound. The 3228-byte fixture is shrunk or refused under the strict policy; a 64-token
output cap cannot stand in for the whole generation charge.

### 5. Connect per-attempt receipts and uncertain costs — F01

- [ ] Reuse/adapt Phase 3's EmbeddingInvocation/operation ledger for query embedding.
      Count and admit each actual POST including retries; preserve both known
      receipts and unknown bounds from earlier attempts. Keep freshness/ownership
      callbacks effective on each send.
- [ ] Add equivalent scoped chat transport reporting for generation. Record a valid
      usage receipt independently of JSON/selection/refusal/incomplete-output
      validation, and preserve it through caller cancellation and cleanup faults.
- [ ] Distinguish no-send preflight rejection, a sent response with known usage and
      a sent outcome with unknown usage. Never infer an actual send from a generic
      exception and never replace known usage with zero or a smaller unknown bound.
- [ ] Persist receipts with bounded cleanup that survives caller cancellation.
      Keep cumulative known and uncertainty separate through settlement/recovery.
      Do not overwrite an earlier uncertain retry when a later attempt succeeds.
- [ ] Keep non-chat request payloads, errors/retry semantics and registration stable;
      new behavior must be opt-in through chat request options/callbacks if the
      shared text client changes. Preserve the Phase 3 maintenance tests.

Acceptance: two-POST lost-response/retry retains unknown-first + known-7; invalid
vector retains known 7; incomplete generation retains known 105. Attempt limits,
final context cap and daily reservation stop additional sends before overspend.
Verify actual settled counters, not merely journal properties or a returned DTO.

### 6. Recheck before external dispatch and answer commit — F04

- [ ] With F03 provenance, recheck current source access/publication/hash/snapshot
      before sending selection evidence and again after awaiting the provider,
      immediately before accepting/committing the selected text.
- [ ] Reject stale/revoked/private/changed evidence with a safe typed outcome. Keep
      paid usage; do not persist the old source as an accepted assistant answer.
- [ ] Add barriers for unpublish, content hash change, public-to-private transition
      and snapshot replacement, with valid selected IDs from the old catalog.
      Check both external payload count and assistant/log artifacts.

Acceptance: the review's unpublish-while-selecting case cannot return/store the red
sentence as success. A source still authorized and unchanged continues to answer.

### 7. Schema/provider and complete verification

- [ ] Assess persisted changes from the ownership/receipt design with tct-ef-migration.
      The existing AddAiQuotaTurnStage migration/Designer/snapshot must remain
      consistent. Never edit applied migrations/history; append a new additive
      migration if the repair changes persisted mapping. No destructive Up.
- [ ] Build in an unused configuration such as Phase4RepairRound1 to preserve owner
      processes. Run the new failing-then-passing regressions, all affected Phase
      0–3 RAG/access/provider tests and chat/cancel/quota/history/consumer regressions.
- [ ] Run the complete application suite once the code is final. The first review
      focused run had one promotion test failure that passed isolated and in the
      later full suite; preserve that test's assertions and report any recurrence.
- [ ] Run repo-local EF pending-model check. A design-time metadata check alone is
      not provider proof and must not be reported as a MariaDB database upgrade.
- [ ] Verify upgrade/repeat/checksum/old rows on a disposable MariaDB 10.5.23 and run
      actual live/expired/recovered ownership/receipt tests there. Review could not
      repeat these because the Docker Linux engine was unavailable. Missing engine
      is a real verification blocker, not a passed check or authority to reset dev DB.
- [ ] scripts/verify-mariadb.ps1 currently fixes Release/--no-build. Use its guarded
      disposable baseline/upgrade/repeat checks with an already-built Release only
      when safe; otherwise reproduce those exact checks with the isolated output
      and document commands. Do not change tooling or terminate owner workloads
      simply to make that script run.
- [ ] Run `git diff --check`, relevant tooling validation if tooling changed, and
      inspect the final complete diff for accidental authority/fixture/data drift.

Commands whose configuration/filter can be adapted to the final test names:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase4RepairRound1 --filter 'FullyQualifiedName~AiRag|FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~AiCancelAndStop|FullyQualifiedName~AiQuotaReservation|FullyQualifiedName~AiCloseout|FullyQualifiedName~OpenAiConsumerPipeline'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase4RepairRound1 --no-build --no-restore
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext --configuration Phase4RepairRound1 --no-build
```

For MariaDB opt-in, use the repository's sentinel/run-id/connection guards and only
a verified disposable loopback target. Never print credentials or copy production
data. The fixture may clean only its explicitly guarded acceptance database.

## Handoff and stop

- [ ] Append bug memory only for fixes actually made and verified.
- [ ] Write `docs/feature-audits/hybrid-rag/phase-4/fix-round-1-handoff.md` in Vietnamese:
      each finding addressed/pending, exact commands/results/skips, migration and
      MariaDB evidence, fake-versus-live limits and remaining blockers.
- [ ] Refresh the cumulative Phase 4 manifest, adding all repair source/test/guidance
      artifacts, and re-read/recompute every final hash/fingerprint. Exclude result/
      handoff/state/manifest files per protocol; preserve the returned input identity.
- [ ] Set only Phase 4 AWAITING_REVIEW (or BLOCKED for genuinely missing mandatory
      evidence) and stop. Keep Phases 5–7 LOCKED. Do not self-approve.

Execution prompt: `.ai/prompts/hybrid-rag/phase-4-fix-round-1.md`.
