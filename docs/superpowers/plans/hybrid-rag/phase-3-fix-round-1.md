# Phase 3 repair plan — round 1

Prepared 2026-11-06 from [review round 1](../../../feature-audits/hybrid-rag/phase-3/review-round-1.md).
Mode now: PLAN_ONLY. IMPLEMENT_LOCAL activates on the owner's repair launch.
Repair HR-P3-R1-F01–F04 as one agent. Keep Phase 4–7 LOCKED.

## Identity, authority and file boundary

- Branch `feature/hybrid-rag-chatbot`; HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`. Preserve all existing dirty work.
- Returned 81-entry scope fingerprint:
  `dbee216f70cb1a9b2475e3e5b9a0e4478b3996165247853ad8b19083f971339b`.
  Verify this manifest before editing; classify actual drift instead of resetting.
- Entry: Phase 3 CHANGES_REQUESTED and owner repair launch. Phase 2 approval
  remains the historical prerequisite, not a demand to revert authorized Phase 3.
- Primary implementation: `Services/AI/{OpenAiEmbeddingClient,IOpenAiEmbeddingClient,
  EmbeddingOptions}.cs`, `Services/AI/Rag/Corpus/{VectorIndexService,VectorIndexStore,
  RetrievalCorpusService}.cs`, all under `TCTEnglish/`; focused tests in
  `TCTEnglish.Tests/AiRagHybridRetrievalTests.cs` or new `AiRag*` companions.
- Add a small RAG admission/attempt-budget helper if justified. Existing owner
  grant permits narrow chat-RAG DI registrations in `TCTEnglish/Program.cs`;
  use it if the shared coordinator needs registration. Do not capture scoped
  DbContext in a singleton. Preserve global `IAiProviderClient` and text consumers.
- No appsettings/project-file/model-setting/secret changes, paid API, remote Git,
  CI, deploy, DB reset, schema or shared-content edit is expected or authorized.
  A dependency needing additional authority must be identified concretely.
- Apply AGENTS.md, tct-superpowers, tct-engineering, security/admin procedures
  and the prompt standard. CodeGraph first; use bounded reads for omitted gaps.

## Evidence and repair order

The independent review probe is in ignored
`.tmp/hybrid-rag-review/phase-3/round-1/Probe/`, with `behavior-probes.json`.
It is a local aid, not a delivered portable regression. Put the behavioral
assertions in repository tests and show each meaningful failure before its fix.

### 1. Reproduce all four defects before implementation

- [ ] F01: production OpenAiEmbeddingClient + recording HTTP, budget one request,
      response 429/Retry-After=0 then success. Current observed: two HTTP sends,
      ApiCalls=1 and promoted. Assert the hard attempt ceiling and accurate count.
- [ ] F02: Vietnamese input under a chars/4 estimate of 7, token ceiling 10,
      valid fake usage 20. Current observed: usage-exceeded with reported usage 0.
      Assert safe admission and preservation of known usage on failure. These
      are declared fake counts, not a real tokenizer or live quality measurement.
- [ ] F03: two independent request scopes/DbContexts and one approved root/store,
      gate provider call one, start call two. Current observed: the second enters
      before first releases and both promote. Assert shared bounded admission.
- [ ] F04: 65 public chunks, unpublish during the first 64-item embedding batch.
      Current observed: second one-item batch is still sent and all 65 promote.
      Assert no later send of revoked text and no stale promotion.

### 2. F01 + F02 — enforce and report one operation budget

- [ ] Define an operation-scoped ledger for actual HTTP attempts and known input
      usage. Enforce maxRequests immediately before every send, including retry;
      one method invocation is not necessarily one POST. Stop before an attempt
      with no remaining allowance. Preserve 429/5xx/deadline/caller-cancel semantics.
- [ ] Share this ledger across batches and the transport retry path. Choose a
      minimal contract change; keep unrelated text/speech consumers unchanged.
- [ ] Retain chars/4 only as a labelled estimate. For the hard token ceiling use
      validated tokenization or a documented conservative upper bound. Plan each
      batch from remaining allowance and avoid submitting a known-unsafe batch.
      Do not add a tokenizer dependency through an unauthorized .csproj edit.
- [ ] When a valid response contains known usage, account for it before testing
      overage or subsequent vector/cache/promotion validation. A failed job must
      report all known usage/attempts; an unknown outcome must not be labelled 0.
      Handle checked-add overflow without hiding already known cost.
- [ ] Test retry with exhausted/available allowance, exhausted budget after prior
      batch, safe token preflight, actual-usage overage, failed promotion and
      cancellation after a paid-success fake. Preserve the last valid index.
- [ ] Keep this scope to maintenance-job accounting/reporting. Do not implement
      Phase 4 daily quotas, paid chat orchestration or persistent conversation data.

### 3. F03 — share admission across request scopes

- [ ] Use a shared root-scoped lease/coordinator covering planning, cache lookup,
      embeddings, cache writes and promotion, with bounded wait/busy behavior.
      Keep VectorIndexService and EF request work scoped and sequential.
- [ ] Re-read the plan/cache after acquiring a lease. A queued second job must
      reuse completed cache or stop busy, not repeat stale paid work.
- [ ] Verify release after cancellation/failure and preserve concurrent readers.
      A store commit lock or UI button guard alone does not satisfy this finding.
- [ ] Test separate scopes against the same root, not only two calls on one
      service instance; include the actual DI lifetime or an equivalent host test.

### 4. F04 — recheck source eligibility before every send and promotion

- [ ] Revalidate authoritative publication/access-to-common-corpus/hash and the
      expected lexical snapshot before each pending batch and retry that would
      send its text. Checks must occur before the external boundary, not just
      after all payloads are sent. Keep immutable source/chunk identities.
- [ ] On drift/revocation stop safely before the next send; do not auto rebuild,
      overwrite content, skip access checks or silently change the source scope.
- [ ] Revalidate the final eligible corpus/hash and snapshot immediately before
      promotion, including all-cache-hit builds. Keep the last valid index and
      preserve known cost/cache results from still-valid earlier work.
- [ ] Add separate fixtures for unpublish, content change, public-to-private
      transition where supported and snapshot replacement between batches/retries.
      Recheck downstream retrieval still excludes revoked sources.

### 5. Complete evidence and return for review

- [ ] Preserve deterministic cosine/RRF/stable ties/dedup, model/dimension/hash
      validation, cache reuse/failure recovery and no-provider ordinary chat.
- [ ] Keep Admin/antiforgery/missing-invalid-operation/budget negatives with zero
      provider sends. Maintain separate lexical and vector forms.
- [ ] Complete native Tab/Shift+Tab/Enter checks for **Dry-run vector** on the
      actual authenticated admin route through the canonical local launcher;
      preserve existing listeners and use a free port/configuration as necessary.
      Verify one operation, duplicate-submit guard, unchanged vector manifest,
      clear redirect feedback and narrow/desktop layout. Never press live embed
      without a bounded owner grant; embedding action can be tested with fakes.
- [ ] Record actual usage/attempt/corpus/index-byte/time/RAM evidence with its
      environment and limitations. Fake vectors prove mechanics only. No live
      semantic-quality comparison or held-out tuning is required in this repair.
- [ ] Review final tracked/untracked diff. Append bug memory for verified fixes,
      refresh cumulative scope including this executed plan/prompt, write
      `docs/feature-audits/hybrid-rag/phase-3/fix-round-1-handoff.md` in Vietnamese.
      End Phase 3 AWAITING_REVIEW, or BLOCKED for a real missing requirement.
      Do not self-approve or start Phase 4.

## Verification

Use a fresh configuration such as `Phase3FixRound1`; do not compile into an output
folder while its testhost/web executable is running. Include new test discovery.

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase3FixRound1 --filter 'FullyQualifiedName~AiRag|FullyQualifiedName~OpenAiProviderClientTests' --logger 'console;verbosity=minimal'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase3FixRound1 --no-build --filter 'FullyQualifiedName~AiCancelAndStopIntegrationTests|FullyQualifiedName~AiQuotaReservationIntegrationTests|FullyQualifiedName~AiCloseoutRegressionTests|FullyQualifiedName~OpenAiConsumerPipelineTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~AiProductionClassifierIntegrationTests|FullyQualifiedName~HybridRagEvaluationBaselineTests' --logger 'console;verbosity=minimal'
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```

Run the broader app suite once after focused repairs pass. Provider-specific
migration/shared-data checks apply only if actual persisted/source-content changes
occur; do not create empty migrations/packages. Distinguish skipped opt-in tests.

Companion prompt: [phase-3-fix-round-1.md](../../../../.ai/prompts/hybrid-rag/phase-3-fix-round-1.md).
