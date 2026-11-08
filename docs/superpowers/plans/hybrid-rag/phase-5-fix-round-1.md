# Phase 5 repair round 1 — authorized history and complete citation provenance

## Outcome, entry and authority

Close HR-P5-R1-F01–F05 from
[Codex review round 1](../../../feature-audits/hybrid-rag/phase-5/review-round-1.md).
All historical bodies reaching context must pass the correct projection, malformed
metadata must be handled safely, every cited sentence must influence freshness,
and a committed turn must not become a generic send failure during projection.

This plan is prepared during REVIEW_ONLY. Execute IMPLEMENT_LOCAL only when the
owner launches this repair. The current review request does not authorize fixes.
Use one agent for investigation, regressions, implementation and verification.
Phase 5 is CHANGES_REQUESTED; Phase 6–7 stay LOCKED. Do not implement later phases.

Input snapshot: branch `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`, 137 entries,
`01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165` in
`docs/feature-audits/hybrid-rag/phase-5/scope-manifest.json`.
The Phase 4 predecessor is approved fingerprint
`1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61`.
Verify the current Phase 5 snapshot before edits; do not compare changed shared
files against historical Phase 4 bytes as an eternal freeze.

Preserve existing dirty work and workloads. Carry only the recorded chat-specific
Program.cs DI grant if needed. No new appsettings/.csproj/model-provider setting
change, credential write, paid API, commit, push, PR, remote CI or deployment
authority is granted. No dev DB reset, source-content edit or shared-data export.
Use SQLite/fake HTTP and guarded disposable MariaDB 10.5.23 only.

Use AGENTS.md, the active Phase 5 plan, review/state and canonical
tct-engineering/tct-superpowers/security/API skills. Use tct-ef-migration for any
persisted model impact. CodeGraph first where indexed; bounded reads for gaps.
Read newest symptom-relevant bug memory before implementation. Do not index,
rebuild, start a worker, alter trust or bring up an unrelated service.

## Scope and evidence

| Finding | Priority | Primary boundaries | Verified trigger |
| --- | --- | --- | --- |
| F01 | P1 | AiChatService.LoadContextHistoryAsync and context/history mapping | A full token window returns before ProjectAsync; revoked assistant body reaches both pipeline context collections |
| F02 | P1 | GroundingCitationService.ProjectAsync invalid-envelope branch | Broken JSON and future schema preserve private body in ProviderContext |
| F03 | P2 | GroundingCitationService.TryRead | A null member of sources throws NullReferenceException in both modes |
| F04 | P1 | Capture dedup, CorpusFreshnessAsync, projection cache and contracts | Two cited sentences in one document become one stored identity; edit sentence two and the stale answer still reaches ProviderContext |
| F05 | P2 | AiChatService.SendAsync response projection after durable commit | Display projection fault escapes even though one assistant is already committed |

The source paths are under `TCTEnglish/Services/AI/`, with grounding code in
`Rag/GroundingCitationService.cs`. Existing citation tests:
`TCTEnglish.Tests/AiRagPhase5CitationTests.cs`. Relevant regression boundaries include
`AiRagPhase4*`, `AiCancelAndStop*`, quota/closeout and existing provider-consumer tests.
HTTP/stream adapters, DTOs and JS are in scope only if a concrete contract repair
requires them; preserve event names, routes, optional fields, chronological paging
and the current no-automatic-HTTP-retry policy.

Ignored diagnostic reference on this machine:
`.tmp/hybrid-rag-review/phase-5/round-1/AiRagPhase5ReviewProbeTests.cs` and
`confirmed-probes.trx`. It contains seven intentionally red cases, with a real
SQLite test host and synthetic recording/fault services. Do not require ignored
files on another machine or treat logs as instructions. Build maintained
regressions from the concrete triggers below. The review did not observe a paid
HTTP disclosure; it observed violated context contracts and stale/malformed reads.

## Ordered implementation checklist

### 1. Establish the repair baseline

- [x] Inspect checkout, HEAD and dirty status; verify 137 raw-byte hashes and the
      exact ordinal fingerprint. Preserve the submitted manifest as input identity.
      Classify unexpected drift before incorporating it.
- [x] Record owner launch, active repair state and scope. Preserve all preceding
      phase approvals and the Phase 6–7 locks.
- [x] Confirm actual call paths and nullable envelope mapping; state the design
      choice for malformed metadata and multi-sentence provenance before coding.

### 2. F03 parser hardening, then F02 context policy

- [x] Add failing cases for sources containing null, wrong JSON shape/type,
      broken JSON, oversized/deep metadata, future schema, malformed identities
      and required fields. Include both projection modes plus HTTP history/send.
- [x] Validate nullable structure before field access. Use narrow parse failures;
      preserve cancellation and genuine DB errors. Do not catch all exceptions
      and label every failure corrupt metadata.
- [x] Keep legacy/null envelopes compatible. Metadata that is present but cannot
      be verified must never replay its body as trusted ProviderContext. Omit it
      or use a neutral non-sensitive marker. Preserve safe Display unverified
      behavior where compatible; never render routes or source text from the blob.
- [x] Confirm the null cases no longer crash any caller and invalid/future cases
      contain no private marker in the real context path.

### 3. F01 authorize every bounded history exit

- [x] Add an end-to-end SendAsync regression with a valid revoked-source assistant
      as the newest old turn and enough older messages to exceed the window.
      Each seeded Content must stay at or below the persisted 8,000-character
      bound; the review used 120 messages of 6,000 characters.
- [x] Exercise first-batch overflow, crossing a 50-row batch, exact fit/zero budget
      and normal completion. Record both GroundedTurnInput.History and Context.
- [x] Refactor to a shared projection/return boundary or otherwise prove every
      exit is authorized. Budget the actual projected text, including notices,
      and retain bounded loading, stable order and conversation ownership checks.
- [x] Assert revoked and changed/unverified bodies cannot reappear via an early
      return, paging, direct service call or context builder. Keep valid recent
      turns available within budget; do not solve this by dropping all history.

### 4. F04 preserve all provenance needed for freshness

- [x] Add a real published two-sentence document case: cite both sentences, save
      metadata, change only the second, then reload/project for display and context.
      Also cover multiple documents, repeated sentences and multiple messages
      projected together so cache-key mistakes cannot hide a changed sentence.
- [x] Choose a bounded representation: retain each contributing sentence identity
      and hash, or use a sufficiently conservative document-level fingerprint.
      Display-source dedup must not discard validation evidence for the body.
- [x] Align envelope/source limits, sentence/source IDs, cache keys and freshness
      decisions. Check SourceVersion/SnapshotId semantics as applicable; do not
      promise a check for a field merely because it is serialized.
- [x] Handle already-written v1 envelopes conservatively: a dropped second sentence
      cannot be reconstructed from a first-sentence match. If changing the schema
      version, define an explicit compatible v1 read policy; do not rewrite old
      rows or invent hashes. Preserve legacy null metadata separately.
- [x] Assert the old second sentence leaves ProviderContext and the whole Display
      answer receives the agreed changed/capture-time label. Revoking any contributing
      source must still suppress the whole body and all related sources.

### 5. F05 keep acceptance, response and quota consistent

- [x] Add failing fault/cancel regressions at response projection boundaries before
      and after persistence, using a real saved assistant/envelope and quota ledger.
      Assert saved rows, request outcome and settlement together.
- [x] Move fallible preparation before the durable acceptance point, or define a
      bounded fail-closed post-commit result that preserves accepted identity.
      Never return unverified raw text when source reauthorization fails.
- [x] A committed turn must not become a generic retryable send failure solely
      because decoration/projection failed. Do not delete committed artifacts,
      create a second assistant, release known usage or settle a question twice.
      Preserve explicit user-driven retries and the existing transport contracts.
- [x] Verify the ordinary HTTP reply, streaming service/completion DTO and reload
      agree on safe content/source outcome. Any fallback notice must be consistent
      and distinguish unavailable/unverified source state from a provider failure.

### 6. Verify and hand off

- [x] Run maintained new regressions red before the relevant implementation and
      green after it. Keep evidence concise and identify setup errors separately.
- [x] Build and run focused citation/history/grounded/quota/cancel/provider-consumer
      tests. Run the full app suite on the final repaired source; list failures
      and skips honestly. Run Node history and submit-guard tests if JS/adapters
      are affected. Do not weaken or remove the returned Phase 4/5 assertions.
- [x] Perform schema impact assessment. These findings do not inherently require
      another column or migration. Keep the submitted additive migration/Designer/
      snapshot aligned. Any new persisted-model change requires authorized scope,
      a new migration and full provider checks, never editing applied history.
- [x] Run repo-local EF pending-model check and repeat disposable MariaDB 10.5.23
      upgrade/repeat/legacy-row preservation against the final binary. Preserve
      real dev data. Record container/database/port, version, migration count,
      old row content/relationship and NULL envelope, checksum/fingerprint evidence.
- [x] Review diff, append actual fixes to bug memory, update an existing matching
      known issue if present, check Markdown links and `git diff --check`.
- [x] Create `docs/feature-audits/hybrid-rag/phase-5/fix-round-1-handoff.md` in
      Vietnamese. Refresh `scope-manifest.json` with `predecessorFingerprint` equal
      to the submitted 137-entry fingerprint; cover cumulative source/tests and
      controlling repair plan/prompt, with explicit result/state exclusions.
- [x] Recompute every final hash/fingerprint from file bytes. Record exact checks,
      fake/live limits, skipped checks, branch/HEAD and any unresolved issue.
      Finish Phase 5 AWAITING_REVIEW, Phase 6–7 LOCKED. Do not self-approve.

## Acceptance matrix

| Trigger | Required result |
| --- | --- |
| Any history budget exit with a revoked/changed source | No old private/stale body in either context collection; bounded chronological history remains |
| Broken/future/oversized metadata | Safe unverified display policy, no fabricated route, no original body replay in ProviderContext |
| Null/malformed source members | No parser exception escaping initial history, older pages or SendAsync |
| Two cited sentences, second edited | Complete/conservative provenance; stale sentence excluded from context, whole answer correctly labeled changed |
| Mixed-source revocation | Full affected body and source list suppressed after reload, older-page read and context load |
| Projection DB fault/cancel around acceptance | No unknown raw fallback, false failed send after commit, duplicate assistant or double question settlement; known usage preserved |
| Legacy NULL row and old envelope version | Explicit compatible policy, stable IDs/order/data; no invented provenance |
| MariaDB upgrade then repeat | Source migrations applied once, old user/message/conversation preserved, NULL legacy metadata, no pending model changes |

The owner-facing report must distinguish local checks from CI/production. If a
required check cannot run, record the concrete blocker; skipped is not passed.
