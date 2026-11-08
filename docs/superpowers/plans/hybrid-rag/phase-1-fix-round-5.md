# Phase 1 repair plan — round 5

Prepared 2026-11-06. Reviewer mode PLAN_ONLY for application fixes. An owner launch
activates IMPLEMENT_LOCAL for HR-P1-R1-F01 only. One agent. Phase 0 APPROVED;
Phase 1 CHANGES_REQUESTED; Phase 2–7 LOCKED.

## Evidence and authority

- [Review round 5](../../../feature-audits/hybrid-rag/phase-1/review-round-5.md).
- Branch `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, uncommitted cumulative work.
- Reviewed 44-entry fingerprint:
  `26a1d1a138ba648137ea3c85b9d1fdfe8e221efe65ccc615f9b04bcf41ebf73e`.
- Only **HR-P1-R1-F01 (P1)** remains open. F02/F03/F04/HR-P1-R3-F05 and stable
  `card:{CardId}` identity are verified; do not reopen their implementation scope
  unless a concrete dependency of this support decision requires a small change.

Retain the recorded narrow Program.cs chat-DI grant; no DI edit is expected.
No new appsettings/.csproj, persisted schema/content, secret write, paid API,
remote Git/CI or release authority. Preserve existing changes, workloads and data.
No BM25/vector/generation/citation UI or other phase in this repair.

Read current AGENTS.md, gates/state, accepted Phase 0 contracts, relevant bug memory
and current CodeGraph source. Expected source scope: Rag/AiChatAnswerService.cs,
Rag/ChatExternalFactPolicy.cs, narrowly needed transient request-kind/target parsing
and composer contracts under Services/AI, meaningful tests and evidence documents.

## Repair decision: conservative Phase 1 answer kinds

Earlier plans already allow refusal/clarification where the deterministic baseline
cannot verify an answer. Use that option to finish F01 instead of repeatedly
expanding free-text fact extraction. A card/guide keyword hit is a candidate, not
permission to label the whole request Answered/full.

- [ ] Establish a positive decision for the answer kind before full composition:
      recognized platform help, typed current-user facts, and genuine card-content
      lookup supported by the requested target. Keep these existing useful flows.
- [ ] Unknown/unsupported demand must default to clarification/refusal, even after
      an authorized lexical hit. Classifier confidence alone cannot establish
      support or authorization. Keep bounded lookup for low-confidence requests.
- [ ] Retire the speculative free-text attribute-value path from this Phase 1
      baseline. Letter/digit tokens, nearby words and the absence of a recognized
      fact frame cannot authorize a full fact answer. Richer grounded answering
      remains the separate Phase 4 scope; no paid call/model change here.
- [ ] Preserve explicit stored definitions for entities/terms. For a fact-like or
      uncertain sentence, the baseline may ask whether the user wants the stored
      definition; it must not present that definition as the requested fact.
- [ ] Reconcile the local LIMA9 test with this bounded baseline. It was added as a
      synthetic positive fact control, not an approved requirement for unrestricted
      attribute answering. Remove that full-answer expectation if it depends on
      the retired heuristic; keep an actual positive definition lookup control.
      Do not delete the negative support tests or weaken real platform controls.

This is a repair of unsupported full answers, not a waiver of grounding criteria.
The Phase 1 baseline becomes safer through abstention; future RAG capability still
requires the already planned review/acceptance gates.

## Ordered implementation and regressions

### 1. Reproduce the residual paths before editing

- [ ] Verify current manifest/dirty boundary and applicable Phase 0 predecessor.
- [ ] SQLite regression A: France definition `a country; its capital is not given
      in Unit12`, question `What is the capital of France?`. Must not be
      Answered/full or present the missing-fact note as the fact answer.
- [ ] SQLite regression B: France definition `a European country`; pair direct
      `What is the capital of France?` and polite
      `Can you tell me the capital of France?`. Both lack evidence for the fact and
      must produce a safe non-full outcome. Keep `what does France mean` working.
- [ ] Add meaningful metamorphic controls: adding/changing a lesson/reference ID
      to a missing-fact source cannot grant support; adding a polite wrapper to
      the same unsupported question cannot grant support either. Use another
      entity/relation to reject literal France/Unit12/prefix special cases.
- [ ] Retain original English/Vietnamese absence-note, mere mention and Kenya
      negative tests. Verify bare terms, explicit meaning, contextual forecast and
      low-confidence `Please check zephyr-window against my notes from class`.
- [ ] Test the actual production answer boundary/retrievers with SQLite. Record
      red assertions before implementation. The review scratch probe is only
      supplemental evidence; add permanent tests for the behavior.

### 2. Implement the support decision and align text/outcome

- [ ] Trace request kind/target, candidate retrieval, selected evidence, composition
      and final outcome. Add the smallest transient decision contract needed so
      full-answer eligibility cannot be inferred from snippet presence downstream.
- [ ] Apply the conservative answer-kind decision to every relevant card fallback
      path, including unknown sentences the prior attribute parser did not recognize.
      Do not patch only HasExplicitAttributeValue or prepend more regex checks.
- [ ] Keep source membership/ownership checks, bounded queries and stable IDs.
      Valid low-confidence card matches must still be usable when the lookup kind
      and requested content are established. Do not blanket-block OutOfScope labels.
- [ ] Match refusal/clarification text to its typed outcome and null/non-full scope.
      Merely changing a label while returning an unrelated full template is not a
      fix. Avoid retaining irrelevant evidence as proof of the unsupported fact.
- [ ] Keep global IAiProviderClient and non-chat consumers unchanged. Do not change
      quota/Stop/history/HTTP/SignalR behavior or introduce schema/content work.

### 3. Verify closed behavior and current scope

Run new regressions red then green, then the following existing filters. Add any
new test class explicitly if it is outside AiRagBoundaryAccessTests.

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixRound5 --filter 'FullyQualifiedName~AiRagBoundaryAccessTests|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiPhase4HardeningIntegrationTests|FullyQualifiedName~AiCancelAndStopIntegrationTests|FullyQualifiedName~AiQuotaReservationIntegrationTests|FullyQualifiedName~AiHistoryPagingIntegrationTests|FullyQualifiedName~AiCloseoutRegressionTests|FullyQualifiedName~OpenAiConsumerPipelineTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~HybridRagEvaluationBaselineTests' --logger 'console;verbosity=minimal'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixRound5 --no-build --filter 'FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~MlNet|FullyQualifiedName~CursorReviewProbeTests|FullyQualifiedName~RepairReviewProbeTests|FullyQualifiedName~ThirdReviewProbeTests' --logger 'console;verbosity=minimal'
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```

- [ ] Re-run round-5 scenarios on new binaries. Zero test discovery, stale output,
      lifecycle provider stubs and tooling alone do not prove boundary behavior.
- [ ] Keep F02/F03/F04/F05, source keys, full/Unicode/framing-word set names,
      comparisons/ambiguity, access/revocation, safe password help, prompt/credential
      refusal, typed user facts and lifecycle/non-chat assertions green.
- [ ] Preserve existing Pomelo production-builder translation test; recheck it if
      query construction changes. No schema change/MariaDB execution is required
      by a transient decision fix. Record translation versus execution honestly.
- [ ] Review complete modified/untracked diff, append verified bug memory and keep
      canonical fixture hash
      `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.

## Handoff and mandatory stop

Write `docs/feature-audits/hybrid-rag/phase-1/fix-round-5-handoff.md` in Vietnamese.
Map HR-P1-R1-F01 to real red/green evidence, the explicit conservative answer-kind
decision, changed synthetic controls and capability limits. Refresh cumulative
scope manifest including source/tests/controlling guidance and an auditable
predecessor chain; exclude recursive manifest/result/handoff/state files.

Return Phase 1 AWAITING_REVIEW, Phase 2–7 LOCKED. Do not self-approve or start
Phase 2. Codex review round 6 must verify the result. Companion execution prompt:
`.ai/prompts/hybrid-rag/phase-1-fix-round-5.md`.
