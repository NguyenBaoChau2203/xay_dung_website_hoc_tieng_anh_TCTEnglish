# Phase 2 — corpus, provenance and BM25 index

Entry: Phase 1 APPROVED, latest reviewed scope matches, Phase 2 READY and owner launch.
Mode: IMPLEMENT_LOCAL. Read gates/state, approved Phase 0 contracts and Phase 1
handoff. Skills: tct-superpowers, tct-engineering, security and applicable source-domain
skills. Tentative dates 10–11 Nov. This phase implements lexical retrieval, not vectors.

## Outcome and file boundary

Build a bounded approved corpus with stable provenance, reproducible sentence/chunk
IDs, immutable index snapshots and genuine BM25 ranking. Rebuild is explicit and
does not run paid operations or copy private data into a common index.

Read the accepted FAQ/source matrix, existing `WebsiteGuideRetriever` and the
relevant published lesson service/entity projections. Proposed new code lives under
`TCTEnglish/Services/AI/Rag/`; preserve the chat boundary established in Phase 1.
Proposed runtime output: ignored `.appdata/local/ai-rag` on the D checkout, outside
wwwroot. No framework/dependency/DB-vector upgrade is in scope.

## Ordered checklist

- [x] Verify predecessor approval and all source changes since it. Confirm current
      DI authority; narrow existing registrations only where applicable.
- [x] Define typed allowlisted corpus adapters for reviewed guides and public/
      published lesson text, using actual feature publication/entitlement rules.
      Published is not assumed to mean readable by every role. Retain a source kind,
      entity key, route, required access, version/hash and authoritative recheck.
- [x] Exclude private cards/drafts/videos, users, chats, payments, recordings,
      arbitrary uploads, source code, agent prompts and unrestricted docs folders
      from shared snapshots. Private queries stay on the authorized live path.
- [x] Use only verified guide claims. Fixing source content requires reviewed scope
      and the shared-data policy: identify an allowlisted adapter, selective export,
      stable ownership/version/hash; never substitute a SQL dump or generic importer.
      If an adapter is missing, record the dependency and do not invent a package.
- [x] Normalize lexical matching for Vietnamese/English without altering source
      display text. Preserve meaningful multiword terms and source field boundaries.
      Use deterministic sentence IDs and paragraph/heading chunk boundaries.
- [x] Start with roughly 300–600 approximate tokens per chunk and minimal overlap,
      but validate actual corpus lengths and caps. Record tokenizer limitations;
      the existing approximate counter does not prove exact model context sizes.
- [x] Implement BM25 with documented formula/parameters (initial k1=1.2, b=0.75
      are hypotheses), body/title/keyword field handling, zero-length documents,
      repeated terms, stable ties and bounded candidate/result counts. Add corpus
      fixtures proving ranking, not tests that merely repeat implementation output.
- [x] Create a build/validation service and the Phase 0 approved explicit rebuild
      boundary. No automatic build during ordinary startup, chat or folder-open.
      Prepare dry-run counts/source hashes and limits; no embeddings exist yet.
- [x] Write new snapshots to a task-owned ignored staging directory; validate schema,
      source/dimension placeholders, checksums and allowed paths before promotion.
      Keep last valid snapshot until atomic manifest replacement succeeds. Do not
      recursively delete computed paths without containment/ownership verification.
- [x] Reject corruption, truncated files, unsupported schema and mixed versions.
      Handle concurrent readers/rebuilds using immutable snapshots and one bounded
      build lock. Cancellation/failure preserves the last usable index.
- [x] On query recheck current publication/access/hash before evidence leaves the
      server. Removed/changed sources fail closed even if the old index still exists.
      An index is a candidate cache, not authorization or authoritative source truth.
- [x] Integrate lexical candidates into the chat boundary without unconstrained
      provider calls. Report missing/stale index separately from out-of-scope or no
      evidence. Keep existing live-fact/private paths and source budgets bounded.
- [x] Add deterministic index/ranking/access/failure tests, review diff and record
      actual local corpus counts, snapshot bytes, build/query time and RAM estimate.

## Required checks

| Case | Acceptance |
| --- | --- |
| Query matches lesson body but not title/keywords | Correct eligible source is retrievable |
| Repeated terms, long versus short text, ties and empty corpus | Documented BM25 behavior and stable results |
| Rebuild cancelled, disk write failure, bad checksum or concurrent read | Last valid snapshot remains usable; no partial promotion |
| Source unpublish/change or role/membership revoke after build | Old evidence does not reach response/provider/cache |
| Private/draft source mixed into ingestion input | Adapter rejects/excludes it; snapshot has no private rows |
| Traversal/absolute override/foreign cache path | Build refuses before filesystem mutation |
| Existing personal facts and non-chat AI features | Approved Phase 1 behavior remains intact |

Run focused new corpus/BM25/index tests and affected Phase 1 regressions plus build.
Check new test discovery and ignored output status. Do not call a fake dense branch
Hybrid RAG or claim recall on a nonexecuted live corpus. No schema change is expected;
any actual persisted change triggers migration rules in this phase.

## Exit evidence

Deliver code/tests, corpus manifest specification, build/failure procedure and
lexical development-set baseline with exact denominator/versions. Preserve held-out
cases for final evaluation. Include real package export evidence only if content
changed. Create handoff/scope, set Phase 2 AWAITING_REVIEW and stop; Phase 3 stays LOCKED.
