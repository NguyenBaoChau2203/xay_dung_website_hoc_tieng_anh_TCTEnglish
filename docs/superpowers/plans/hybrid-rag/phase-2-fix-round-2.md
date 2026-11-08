# Phase 2 repair plan — round 2

Prepared 2026-11-06 from [review round 2](../../../feature-audits/hybrid-rag/phase-2/review-round-2.md).
PLAN_ONLY during review; IMPLEMENT_LOCAL on explicit owner repair launch. One agent.
Repair only the still-open HR-P2-R1-F02 and HR-P2-R1-F03. Keep F01/F04 fixed and
finish the outstanding rendered-admin acceptance from repair round 1.

## Identity and scope

- Branch `feature/hybrid-rag-chatbot`; HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`; preserve all dirty work.
- Reviewed 64-entry fingerprint:
  `67ec08a66cc20aa6afc25b88750d184e11c1f9e949dbe92ceb9a7d8a817ed80a`.
  Verify final bytes and classify any drift before editing.
- Entry: owner repair launch, Phase 2 CHANGES_REQUESTED, Phase 3–7 LOCKED.
- Primary files: `TCTEnglish/Services/AI/Rag/Corpus/RetrievalCorpusService.cs`,
  `TCTEnglish/Services/AI/Rag/Corpus/LexicalAnswerAdmission.cs`,
  `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs`,
  `TCTEnglish.Tests/AiRagPhase2FixRound1Tests.cs` and focused companion tests.
- No DI/protected-config/package/schema/content change is expected. Existing narrow
  Program.cs chat-RAG grant remains narrow. Keep global OpenAiProviderClient.
  No paid call, secret write, DB reset, remote Git/CI/merge/deployment or Phase 3+.
- Read AGENTS.md, prompt standard, active review/gates and symptom-relevant bug
  memory. Apply engineering/superpowers inline; CodeGraph first where available.
  Reuse security/admin/UI procedures for the outstanding browser acceptance.

## Ordered work

### 1. Reproduce the residual support failures and correct the test oracle

- [x] Add meaningful failing regressions before implementation, with real corpus
      and the full production retriever set. The title `Daily notes` and keywords
      A1/Sea do not contain the target; do not rely on a title hit to rescue body evidence.
- [x] Keep the original F02/why/count controls passing; record new failures separately.
- [x] Correct the round-1 positive at `AiRagPhase2FixRound1Tests.cs:264`: `Where is
      the tidal harbor marker visible?` is not answered by `at dawn`. Retain this
      as a negative. Add correct positive time and location controls using matching
      questions/source data. Do not edit the frozen evaluation fixture.

### 2. HR-P2-R1-F02 (P1): select the actual subject-bearing span

- [x] Reproduce both queries from the review against body `Here you can find
      examples in this lesson. The tidal harbor marker is visible beside the gate.`:
      `Where can I find the tidal harbor marker in this lesson?` and `Can I find
      the tidal harbor marker in this lesson?`.
- [x] Repair ToHit/QueryOverlap so framing/function words do not replace the
      subject-bearing span. Use a bounded, deterministic content-relevance rule;
      do not special-case marker, this sentence or a sentence position.
- [x] Direct RetrieveAsync must retain the marker sentence with exact text/hash/ID.
      Returning no answer in the boundary alone does not repair lost retrieval.
      Preserve body-only matching, beginning/middle/end/later chunks, stable ties,
      v2 span limits and publication/access/hash rechecks.
- [x] Add variants with a different subject, framing word order and competing
      generic intro, plus a control where the first sentence really is relevant.

### 3. HR-P2-R1-F03 (P1): admit only supported relations/facets

- [x] Add the following independent negatives through the real boundary:

| Requested fact | Stored source data | Must not count as support |
| --- | --- | --- |
| Number of people near the marker | Marker was installed in 2008 | A year is not a population count |
| Why the marker glows at midnight | Marker was repainted because old paint faded | Cause of repainting is not cause of glowing |
| Where the marker is visible | Marker is visible at dawn | Time is not location |
| When the marker was constructed | Marker is visible at dawn | Visibility time is not construction time |
| Who built it / what color it has | Marker is visible at dawn | Unrecognized frame must not default to full Reference |

- [x] Replace the any-number/any-because/any-preposition and permissive Reference
      fallback as a proof of support. Establish support for the actual requested
      subject, relation and requested value/facets. Do not only broaden cue lists.
- [x] Keep Phase 2 conservative: a small explicit supported reference/extraction
      contract is acceptable; unsupported/unknown fact requests stay non-full
      with truthful clarification/insufficient/out-of-scope text and no unused
      evidence. Do not implement Phase 4 model generation or claim unrestricted
      language understanding. Do not merely relabel the same irrelevant answer.
- [x] Positive controls must demonstrate the fact actually present: a time query
      about visibility from dawn, a location query from `beside the eastern gate`,
      explicit reference to a stored passage, guide help, authorized card definition
      and current-user facts. Unknown or partly unsupported multi-facet questions
      must not become full by matching only one facet.
- [x] Recheck F02 through the boundary after direct retrieval is fixed. Retain all
      original anti-hallucination/access/quota/cancel/history/non-chat regressions.

### 4. Preserve closed fixes and complete browser acceptance

- [x] Keep explicit dry-run/build operation, rejection of missing/invalid operation,
      duplicate-submit guard, Admin authorization and antiforgery. Dry-run must
      preserve manifest bytes, snapshot ID and directory contents; Build promotes
      only when explicitly chosen. Do not change this flow just to rewrite it.
- [x] Keep v2 exact span reconstruction/hash, 600-token chunk/evidence caps and old
      v1 rejection. No chunker change is required for the two open findings.
- [x] Exercise the actual rendered admin route after login using the authorized
      canonical local launcher if starting the site. Preserve existing listeners;
      use a free port/isolated build configuration when required by AGENTS.md.
      Use saved local credentials securely if available; never print them or ask
      for a key in chat. Never call paid AI or export private data for this check.
- [x] Record missing/existing-index Dry-run, explicit build, operation selected,
      before/after manifest/snapshot, redirect feedback, duplicate-submit behavior,
      desktop/narrow layout and keyboard operation. Auth/antiforgery remain covered
      by HTTP regression. The copied-handler fixture is not a rendered-site pass.
      If a required login/runtime prerequisite is missing, report BLOCKED precisely.

## Checks and exit

Use a fresh configuration, for example `Phase2FixRound2`. Name additional test
classes with the `AiRag` prefix or explicitly add them to the filter. Capture each
new regression red before its repair and green afterward; no zero-discovery pass.

```powershell
$mainFilter = 'FullyQualifiedName~AiRag|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiPhase4HardeningIntegrationTests|FullyQualifiedName~AiCancelAndStopIntegrationTests|FullyQualifiedName~AiQuotaReservationIntegrationTests|FullyQualifiedName~AiHistoryPagingIntegrationTests|FullyQualifiedName~AiCloseoutRegressionTests|FullyQualifiedName~OpenAiConsumerPipelineTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~HybridRagEvaluationBaselineTests'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase2FixRound2 --filter $mainFilter --logger 'console;verbosity=minimal'
$extraFilter = 'FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~MlNet|FullyQualifiedName~CursorReviewProbeTests|FullyQualifiedName~RepairReviewProbeTests|FullyQualifiedName~ThirdReviewProbeTests'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase2FixRound2 --no-build --filter $extraFilter --logger 'console;verbosity=minimal'
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```

- [x] Re-run the development baseline with exact versions/counts/misses/resource
      measurements. No BM25 tuning or held-out scoring; preserve the frozen fixture
      canonical hash `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- [x] Review all tracked/untracked changes. Append an accurate bug-memory entry
      for verified fixes; do not describe the old relation-cue heuristic as complete.
- [x] Write `docs/feature-audits/hybrid-rag/phase-2/fix-round-2-handoff.md` in Vietnamese,
      refresh cumulative `scope-manifest.json` including executed plan/prompt and
      changed guidance, and identify F02/F03 addressed/pending with real evidence.
- [x] Finish Phase 2 AWAITING_REVIEW only when work/evidence is ready, otherwise
      BLOCKED with the concrete prerequisite. Phase 3–7 remain LOCKED. No self-approval.

Companion prompt: [.ai/prompts/hybrid-rag/phase-2-fix-round-2.md](../../../../.ai/prompts/hybrid-rag/phase-2-fix-round-2.md).
Owner launch: `Execute .ai/prompts/hybrid-rag/phase-2-fix-round-2.md in Cursor Auto. Repair the open F02/F03, preserve F01/F04, complete required browser evidence, and keep Phases 3–7 LOCKED.`
