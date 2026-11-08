# Phase 1 repair plan — round 1

Prepared 2026-11-06. Current mode PLAN_ONLY; owner Cursor launch activates
IMPLEMENT_LOCAL for these four verified findings. One agent throughout.
Phase 1 CHANGES_REQUESTED; Phase 0 APPROVED; Phase 2–7 remain LOCKED.

## Identity, authority and evidence

- [Actual review](../../../feature-audits/hybrid-rag/phase-1/review-round-1.md).
- Reviewed branch feature/hybrid-rag-chatbot, HEAD
  6af768bd32d16cc6d012d484ee82df4cab805c69.
- Returned 32-file fingerprint
  781f92c523d57554a0b96acfd9c704986e8c7ac4e74cf47c9fcd39a2d9ef483f.
- Existing tests passed (173 and 82 executions in separate filters); real behavior
  probes demonstrate the uncovered defects. Do not weaken assertions to hide them.

Preserve the recorded narrow Program.cs chat-DI grant and all prior work. No new
appsettings/project/model, paid API, remote Git/CI/release, secret-store or real
content authority. Expected scope is answer routing, card retrieval, transient
source contracts and meaningful tests. No entity/DbContext change is required.

Read AGENTS.md, review gates, the accepted Phase 0 contracts, relevant bug memory
and current CodeGraph source. These repairs do not authorize BM25/vector/generation,
durable citation UI, broad source authoring or another phase.

## Findings and dependency order

| ID | Priority | Result needed |
| --- | --- | --- |
| HR-P1-R1-F01 | P1 | Explicit outside-scope requests cannot become full card answers |
| HR-P1-R1-F02 | P1 | Exact authorized card matches survive more than 100 broad matches |
| HR-P1-R1-F03 | P2 | Legitimate two-character terms remain retrievable |
| HR-P1-R1-F04 | P2 | Distinct card identities and correct ambiguous-term clarification |

First reproduce all findings. Repair F02/F03 together at the candidate-query layer;
retain stable entity metadata for F04. Then complete boundary scope/outcome policy
F01/F04 and rerun the full relevant regressions. Do not couple unrelated changes.

## 1. Meaningful failing regressions before implementation

- [ ] F01: seed an authorized vang card; ask the actual gold-price question and
      verify outside-scope outcome/refusal, not Answered/full or a definition posing
      as the answer. Controls: ask its stored meaning explicitly and keep an
      unknown/low-confidence valid term such as zephyr-window answerable.
- [ ] F02: seed at least 120 lexically broad matches sorting before an exact card;
      ensure the exact authorized match is returned first and cannot be removed
      by a pre-ranking cap. Retain cross-user and former >500 unrelated tests.
- [ ] F03: seed go/up and a longer control. Test both direct lookup and ordinary
      card-help wording. Preserve stopwords and avoid unbounded short-token scans.
- [ ] F04: seed match in two owned sets with different definitions. Bare-term lookup
      must have actual clarification text/outcome, while source keys for distinct
      cards stay distinct and remain stable on repeated retrieval/reordering.
      Include an explicit request for a specified set as an unambiguous control.
- [ ] Prefer existing relational SQLite test patterns for query regressions, not
      only InMemory. Record provider limitations; inspect Pomelo translation or use
      an authorized disposable provider check when query translation changes.

## 2. Repair bounded candidate selection — F02/F03

Affected source: `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs`
and its parser only if the exact cause requires it.

- [ ] Select/rank exact and useful phrase matches before applying the small result
      window, using EF-translatable ranking or bounded separate query tiers.
- [ ] Do not fix the missing exact match by loading all authorized cards or removing
      every cap. Keep parameterized queries, stable ordering and cancellation.
- [ ] Allow legitimate exact short terms rather than dropping all terms below
      three characters. Preserve useful normalization and control noisy fallback.
- [ ] Preserve every owner/folder/saved/class membership predicate and revocation
      behavior; ranking priority never grants access to another user's card.
- [ ] Carry actual card/set identity and needed provenance forward through the
      transient result contract; do not attempt to recover entity identity from a
      title/definition string after retrieval.

## 3. Repair scope and answer decisions — F01/F04

Affected source: `Services/AI/Rag/AiChatAnswerService.cs`, a narrowly scoped policy
or classifier interface if justified, transient evidence/snippet contracts and
`TemplateAnswerComposer` where clarification text is composed.

- [ ] Distinguish clearly external questions from an unknown intent that can still
      be answered from the user's learning content. A keyword hit alone does not
      establish that a requested fact was supported. Do not block every low-
      confidence or OutOfScope classification; preserve valid internal fallback.
- [ ] Decide the final outcome and corresponding response behavior consistently.
      An ambiguous request must not be composed as a full answer and only labeled
      afterward; ask which source/meaning is intended when required.
- [ ] Use stable server-owned entity IDs in source identity. Same term in distinct
      cards/sets must not produce identical SourceKey. Avoid global title keys,
      retrieval-position keys and trusting source-provided identity metadata.
- [ ] Clarify only genuinely ambiguous bare-term requests. A specified accessible
      set/card should resolve; deliberate comparison of allowed sources is not
      automatically ambiguous. Current-source access checks still happen first.
- [ ] Keep global IAiProviderClient and non-chat behavior unchanged. No LLM/free-
      knowledge fallback, paid call or durable citation feature in this repair.

## 4. Verification and evidence

Use the actual test classes, not assumed filename-to-class correspondence. The
following commands are available on the reviewed checkout:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixReview --filter 'FullyQualifiedName~AiRagBoundaryAccessTests|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiPhase4HardeningIntegrationTests|FullyQualifiedName~AiCancelAndStopIntegrationTests|FullyQualifiedName~AiQuotaReservationIntegrationTests|FullyQualifiedName~AiHistoryPagingIntegrationTests|FullyQualifiedName~AiCloseoutRegressionTests|FullyQualifiedName~OpenAiConsumerPipelineTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~HybridRagEvaluationBaselineTests' --logger 'console;verbosity=normal'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixReview --no-build --filter 'FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~MlNet'
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```

- [ ] Verify new tests are discovered, fail before the repair and pass after it.
      Re-run the review behavior probes as a supplemental check; unit tests must
      cover the actual production boundary, not just lifecycle stubs.
- [ ] Keep HTTP/SignalR access, quota/cancel/history and non-chat consumer evidence.
      Record every skipped/blocked provider/browser check accurately.
- [ ] Preserve Phase 0 fixture canonical hash and accepted contracts unless a real
      reviewed change is necessary. Use isolated output if other processes lock a
      configuration. No dev database reset or paid live run is needed.
- [ ] Review the full diff and append bug memory only for actually verified fixes.
      If a persisted mapping unexpectedly changes, apply the migration workflow in
      this round; do not hide it or generate an empty migration for transient DTOs.

## Handoff and mandatory stop

Create `docs/feature-audits/hybrid-rag/phase-1/fix-round-1-handoff.md`, mapping every
finding to behavior/tests and real results. Refresh cumulative scope evidence for
modified/new implementation, tests and controlling repair guidance; preserve or
explicitly reverify the predecessor chain. Preserve historical review reports.

Record remaining limits without declaring findings resolved for the reviewer.
Set Phase 1 AWAITING_REVIEW, keep Phase 2–7 LOCKED and stop. Codex must verify the
actual fixes before Phase 2 can be READY. Report to the owner in Vietnamese.
