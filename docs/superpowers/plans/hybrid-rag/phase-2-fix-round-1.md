# Phase 2 repair plan — review round 1

Prepared 2026-11-06 from [the actual review](../../../feature-audits/hybrid-rag/phase-2/review-round-1.md).
The reviewer is PLAN_ONLY for repairs. IMPLEMENT_LOCAL starts when the owner
launches this repair with the companion prompt. Work as one agent.

## Identity, authority and boundaries

- Branch: `feature/hybrid-rag-chatbot`; HEAD:
  `6af768bd32d16cc6d012d484ee82df4cab805c69`; preserve existing uncommitted work.
- Reviewed cumulative scope: 60 entries, fingerprint
  `ef1418c674f5e86aeb675cf32ae0b695149293101c9707551c6f4b49c4f4442a`.
  Predecessor Phase 1 approval: `764d370dc1b2482cd1b78954a8959dfd4706b16b5156ed00659a9301b729e7a9`.
- Required entry: Phase 2 CHANGES_REQUESTED, Phase 3–7 LOCKED and owner repair launch.
  Recompute hashes; investigate drift rather than overwriting another change.
- Existing Program.cs authority covers narrow chat-RAG DI only; no DI change is
  expected for these fixes. Preserve global OpenAiProviderClient and other AI consumers.
- No appsettings/project/dependency/schema/content edits, paid calls, secret writes,
  database reset, commit/push/PR/remote CI/merge/deployment or later-phase work.
  Apply migration/shared-data rules only if a real authorized dependency arises.
- Read AGENTS.md, prompt standard, review gates and the active source via CodeGraph.
  Use tct-superpowers, engineering, security, admin and UI-review inline. Load
  symptom-relevant bug memory before editing; append verified repairs afterward.

## Ordered checklist

### 1. HR-P2-R1-F01 (P1): preserve the selected admin operation

Files: `TCTEnglish/Areas/Admin/Views/AiManagement/Index.cshtml` form/submit handler;
`TCTEnglish/Areas/Admin/Controllers/AiManagementController.cs` action only if needed
to reject missing/invalid operation safely; focused admin regression tests.

- [ ] Reproduce native browser serialization before editing. The current disabled
      submitter drops `dryRun=true`; do not use a direct service call to stand in
      for the form path. Use an isolated fixture or disposable local test host.
- [ ] Carry the explicit selected operation through serialization while retaining
      duplicate-submit protection. Missing/invalid input must not silently select
      a mutation. Keep role Admin, antiforgery and POST/redirect contracts intact.
- [ ] Prove Dry-run calls only DryRunAsync and leaves manifest bytes, snapshot ID
      and snapshot directory contents unchanged, both with a missing index and
      with an existing valid index. Build must still call BuildAsync explicitly.
- [ ] Verify anonymous/Standard access is denied and a missing antiforgery token
      cannot mutate. Exercise the actual rendered admin page in a browser, including
      Dry-run, explicit build and repeat-submit feedback. Use authorized local
      launcher if starting the real app; preserve existing listeners/workloads.

### 2. HR-P2-R1-F04 (P2): enforce chunk limits before selecting evidence

Files: `Corpus/CorpusChunker.cs`, `Corpus/CorpusModels.cs`, snapshot validation/
serialization or adapter accounting only where needed; `AiRagCorpusBm25Tests.cs`
and focused companion tests. This precedes F02 because it may change span identity.

- [ ] Add a failing regression for the 29,700-character, 7,425-approx-token single
      sentence below MaxBodyCharacters. Include an oversized first/only sentence
      and an oversized sentence after a normal sentence.
- [ ] Enforce MaxChunkTokens 600 for accepted data. Use deterministic bounded
      source spans, or explicitly exclude an unrepresentable oversized source with
      a recorded reason/count. Do not silently truncate or relabel a substring as
      the original full sentence. Preserve original display text and accurate hashes.
- [ ] Keep heading/paragraph structure and bounded overlap coherent. Verify limits
      on the actual persisted/reloaded chunks and returned evidence, not constants
      or an in-memory mock alone. Account for separators in approximate counting.
- [ ] If IDs/chunk semantics change, version the format/algorithm coherently and
      reject old incompatible snapshots safely. Record rebuild instructions. Do
      not tune BM25 parameters or alter the frozen evaluation fixture.

### 3. HR-P2-R1-F02 (P1): retain the matched source span

Files: `Corpus/RetrievalCorpusService.cs` RetrieveAsync/ToHit, related transient
hit/chunk contracts as needed, corpus tests. Depends on the settled F04 span model.

- [ ] Add a failing multi-sentence body-only fixture with title/keywords that do
      not contain the search terms. Data: `Welcome to the lesson. The tidal harbor
      marker is visible at dawn.` Query: `zzzz tidal harbor marker qqqq`.
- [ ] Return a relevant sentence/span from the matched chunk instead of the first
      hash-valid sentence. Preserve source key, exact text/hash and stable ID.
      Do not synthesize a quote or route from query text. Avoid unrelated low-score
      excerpts entering the chat answer as evidence for a different subject.
- [ ] Test matches near the beginning/middle/end, a heading-first chunk, a later
      chunk and repeated matches/ties. Source recheck must still happen before
      any selected evidence is returned, including changed/deleted/unpublished data.

### 4. HR-P2-R1-F03 (P1): separate lexical candidates from supported answers

Files: `Rag/AiChatAnswerService.cs` TryLexicalAnswerAsync/BuildLexicalEvidence,
minimal support-selection contracts if needed, real boundary regression tests.
Depends on F02 for positive controls, but write negative regressions first.

- [ ] Reproduce unsupported cause/count requests using the single-sentence source
      `The tidal harbor marker is visible at dawn.` This must fail independently
      of the multi-sentence F02 bug. Use the production retriever set plus the
      real lexical service, not a boundary constructed with only CardLookupRetriever.
- [ ] Stop treating every positive BM25 score as proof of a complete answer. Keep
      deterministic supported extraction/reference behavior within Phase 2. If
      a requested fact is unsupported, ask for clarification or return a truthful
      insufficient/out-of-scope response. Do not ship a free model answer or
      implement Phase 4 generation. Do not only change the outcome label while
      leaving unrelated text presented as an answer.
- [ ] Ensure `Why does the tidal harbor marker glow at midnight?`, `How many people
      live near the tidal harbor marker?` and equivalent paraphrases cannot return
      full or attach Add-card/Flashcard as supporting evidence. No phrase blacklist
      or special case keyed to the synthetic subject.
- [ ] Retain a positive body-only lesson retrieval/extract control, correct guide
      help, typed current-user facts and exact authorized card lookups. Keep missing/
      corrupt/stale-index handling distinct and do not weaken access rechecks.

## Verification and exit

Use one fresh configuration such as `Phase2FixRound1`. Record each regression red
before its fix and green afterward, with exact discovered counts. The following
are verified class names; select additional new regression classes explicitly.

```powershell
$mainFilter = 'FullyQualifiedName~AiRagCorpusBm25Tests|FullyQualifiedName~AiRagBoundaryAccessTests|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiPhase4HardeningIntegrationTests|FullyQualifiedName~AiCancelAndStopIntegrationTests|FullyQualifiedName~AiQuotaReservationIntegrationTests|FullyQualifiedName~AiHistoryPagingIntegrationTests|FullyQualifiedName~AiCloseoutRegressionTests|FullyQualifiedName~OpenAiConsumerPipelineTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~HybridRagEvaluationBaselineTests'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase2FixRound1 --filter $mainFilter --logger 'console;verbosity=minimal'
$extraFilter = 'FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~MlNet|FullyQualifiedName~CursorReviewProbeTests|FullyQualifiedName~RepairReviewProbeTests|FullyQualifiedName~ThirdReviewProbeTests'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase2FixRound1 --no-build --filter $extraFilter --logger 'console;verbosity=minimal'
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```

- [ ] Browser evidence includes selected operation and resulting snapshot state;
      a copied isolated handler alone is not full rendered-admin acceptance.
- [ ] Record any actual provider check separately. SQLite does not certify MariaDB;
      schema is unchanged, so do not generate an empty migration.
- [ ] Re-run the development baseline with exact corpus/chunker/normalizer versions,
      denominator, misses, bytes, build/query time and RAM estimate. Keep held-out
      scoring at zero; preserve fixture canonical hash
      `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- [ ] Review the complete tracked/untracked diff and ensure no earlier Phase 1
      F01–F05/card-key/access/quota/cancel/history/non-chat regressions return.
- [ ] Write `docs/feature-audits/hybrid-rag/phase-2/fix-round-1-handoff.md`, refresh
      the cumulative scope manifest including execution guidance, distinguish
      addressed/pending findings and actual checks/limitations in Vietnamese.
- [ ] Set only Phase 2 AWAITING_REVIEW and stop. Phase 3–7 remain LOCKED. Cursor
      cannot mark APPROVED or represent its own repair as Codex verification.

Companion: [.ai/prompts/hybrid-rag/phase-2-fix-round-1.md](../../../../.ai/prompts/hybrid-rag/phase-2-fix-round-1.md).
Owner launch: `Execute .ai/prompts/hybrid-rag/phase-2-fix-round-1.md in Cursor Auto. Repair only HR-P2-R1-F01–F04, return AWAITING_REVIEW, and keep Phases 3–7 LOCKED.`
