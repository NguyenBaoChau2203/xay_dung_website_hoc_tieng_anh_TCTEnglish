# Phase 3 — embeddings, dense retrieval and RRF

Completion record, 2026-11-07: Phase 3 APPROVED after the owner-authorized local
repair/review closeout. See [round 2 evidence](../../../feature-audits/hybrid-rag/phase-3/review-round-2.md)
and [review-state](review-state.md) for the current gate and approved fingerprint.
Live provider/semantic acceptance remains in Phase 7 as specified below.

Entry: Phase 2 APPROVED, current scope verified, Phase 3 READY and owner launch.
Mode: IMPLEMENT_LOCAL. Skills: tct-superpowers, tct-engineering, security, and
tct-api-endpoint/tct-admin-panel for the chosen maintenance action. Tentative dates
12–14 Nov. Phase 4 owns activation of paid multi-stage chat and its durable budget.

## Outcome and authority

Implement a real embeddings transport, vector index/search and RRF over the same
eligible corpus as BM25. Demonstrate algorithm mechanics with deterministic tests.
Keep the ordinary chat path on Phase 2 until Phase 4 budget/answer integration is
reviewed. Real model quality/account access remain unverified without a live run.

Use the existing text transport/model unchanged. Proposed embedding model is
text-embedding-3-small with initial 1536 dimensions, separate options/client.
Before implementing transport, consult official versioned OpenAI documentation
for the exact embedding request/usage/error contract; do not copy text model
reasoning parameters into embeddings. Saved credentials are not proof of access.

Paid API work is optional here only with a prior explicit bounded grant. A proposed
development-only live allowance is at most 12 POSTs/40,000 input tokens, no text/
speech calls; calculate actual corpus/query batches first and ask for that concrete
scope if needed. This proposal is not authorization. Fake HTTP tests are required
now; live held-out and end-to-end acceptance are explicitly scheduled for Phase 7.

## Ordered checklist

- [ ] Confirm the accepted embedding/index contracts and actual narrow DI authority.
      Add provider-specific options without modifying protected appsettings/project
      files. Reuse saved-key precedence; never copy/print/persist key values.
- [ ] Implement bounded embedding HTTP calls with approved HTTPS endpoint rules,
      no redirects leaking Bearer tokens, deadline covering body/retry/backoff,
      cancellation, bounded response size and sanitized errors/logs.
- [ ] Validate response count and input index mapping; reject missing/duplicate
      vectors, wrong model/dimension, zero norm, NaN/Infinity and inconsistent usage.
      Handle 401/403, unavailable model, 429 including account quota, 5xx, malformed
      JSON and cancellation distinctly. No model substitution or unbounded retry.
- [ ] Generate embeddings only for changed approved public chunks during explicit
      rebuild. Cache by source content hash plus model/dimension/chunker version;
      never reuse another embedding space or silently mix old/new chunks.
- [ ] Complete the approved operator entrypoint: preferably a narrow existing Admin
      AI-management maintenance POST plus status/dry-run. Require Admin, antiforgery,
      bounded job admission, server-owned cache paths and explicit nonsecret budget
      inputs. Reject unreviewed/private corpus, arbitrary file paths and budget
      bypass. No new authoring screen or destructive database action.
- [ ] Add dense cosine search with immutable snapshots, stable ties and bounded
      top-k. Filter eligible candidates consistently with BM25 before fusing; recheck
      current access/hash on selected evidence before any external call or output.
- [ ] Implement RRF with documented initial k=60 and equal weights, source dedup and
      final evidence budget. Do not add BM25 scores directly to cosine scores or
      map fused scores to a correctness probability.
- [ ] Add explicit retrieval modes hybrid, lexical_degraded and unavailable with
      reasons. Corrupt/missing vector index or failed query embedding cannot silently
      become an unconstrained generation call. Keep cancellation and bounded caches.
- [ ] Prove with fixtures: exact-match lexical winner, semantic-vector winner,
      fused ordering, same-source overlap, no overlap, empty branch, ties, revoked
      source, model mismatch and index promotion failure. Synthetic vectors test
      math only; do not cite them as actual language-understanding evidence.
- [ ] Compare lexical-only, dense-only and hybrid on development cases using actual
      recorded vectors if an authorized live run exists, with provenance/version
      and known limitations. Otherwise record semantic-quality comparison pending
      Phase 7; no fake benchmark or held-out threshold tuning.
- [ ] Document request/token usage and measured corpus/RAM/retrieval cost; review
      source/transport/operator surface and produce handoff/scope manifest.

## Acceptance and phase boundary

Required now: real HTTP contract implementation covered by recording fakes,
deterministic cosine/RRF/index correctness, protected maintenance boundary if added,
versioned provenance, source-scope checks and a safe still-lexical ordinary chat path.
The implementation can be reviewed without sending paid requests; report exactly
which tests use fakes and which observations use real embeddings. Phase 7 must
still block final demo acceptance if genuine provider/semantic evidence is missing.

Run focused new embedding/vector/RRF/admin-access tests, affected corpus/access
regressions and build. New endpoints require anonymous/non-Admin/CSRF negative
tests, including zero API requests on rejection. No startup embedding/speech call.

Set Phase 3 AWAITING_REVIEW and stop. Do not activate paid chat stages, implement
quota state changes or start Phase 4 before Codex approval and owner launch.
