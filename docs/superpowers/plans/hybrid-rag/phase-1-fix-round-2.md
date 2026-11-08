# Phase 1 repair plan — round 2

Prepared 2026-11-06. Reviewer mode PLAN_ONLY for application fixes. An owner launch
of the companion prompt activates IMPLEMENT_LOCAL for the three remaining findings.
Use one agent. Phase 0 APPROVED, Phase 1 CHANGES_REQUESTED, Phase 2–7 LOCKED.

## Evidence and authority

- [Review round 2](../../../feature-audits/hybrid-rag/phase-1/review-round-2.md).
- Branch `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, uncommitted cumulative work.
- Reviewed 37-entry fingerprint:
  `606fc4edf058543087e93a0a689a3e08f1c0ad31d53f6a3311d886c8e16ee1b1`.
- Phase 0 canonical fixture hash:
  `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
- F03 is verified closed. F04 stable card identity is verified; preserve both.
  F01/F02/F04 remain open with concrete SQLite reproductions despite green tests.

Preserve the recorded narrow Program.cs chat-DI grant; no new DI change is expected.
No appsettings, project, persisted model, content/data package, paid API, secret,
remote Git/CI or release authority is added. Keep production/chat lifecycle and
non-chat IAiProviderClient contracts. Review alone does not authorize these fixes.

Read AGENTS.md, review-gates/state, current Phase 1 contracts, relevant bug memory
and current CodeGraph source. Do not start BM25, vector indexing, paid generation,
durable citations or UI work. Do not rewrite historical review reports.

## Findings and dependency order

| ID | Priority | Acceptance gap | Main files |
| --- | --- | --- | --- |
| HR-P1-R1-F01 | P1 | External fact questions still become full card answers | Rag/ChatExternalFactPolicy.cs, Rag/AiChatAnswerService.cs |
| HR-P1-R1-F02 | P1 | Contextual lookup omits its exact term before broad cap | Internal/Retrievers/CardLookupRetriever.cs; Internal/AiCardLookupQueryParser.cs if justified |
| HR-P1-R1-F04 | P2 | Explicit set is discarded before selection | CardLookupRetriever.cs, Internal/CardLookupSelection.cs, composer/boundary as required |

Paths in the table are under `TCTEnglish/Services/AI/`. Add focused tests in
`TCTEnglish.Tests/AiRagBoundaryAccessTests.cs` or a clearly named nearby suite.
Use only transient contracts for qualifiers/source identity. No mapped model fix
is needed. F02/F04 share retrieval planning: settle the request/qualifier before
bounded candidate selection; then complete F01 routing and run all regressions.

## 1. Establish meaningful failing regressions

- [ ] Verify the current manifest and dirty boundary before editing. Preserve the
      Phase 0 predecessor chain rather than overwriting its approved manifest.
- [ ] F01: an authorized `weather` card with a stored definition plus `What is the
      weather today?` must refuse the external fact and must not be Answered/full.
      Keep the exact gold-price test. Add paired explicit stored-meaning and bare
      term controls, including a low-confidence genuine lookup, so a blanket
      OutOfScope/English rejection cannot satisfy this repair.
- [ ] F02: 120 aaa-* cards mention forecast only in their definitions; the exact
      forecast card sorts after them. Test both `forecast` and
      `Giải thích forecast trong bộ thẻ của tôi`. The exact authorized card must
      rank first in both. Assert actual boundary answer content as well as retrieval.
      Include another user's exact card as a negative access control.
- [ ] F04: four authorized sets Alpha/Beta/Gamma/Sporting each contain match, with
      distinct definitions. `nghia cua match trong bo Sporting` must return only
      the Sporting meaning/evidence. Add a larger duplicate population so moving
      the same bug to the exact-tier cap cannot pass. Keep bare-term clarification,
      two-set selection, deliberate comparison and stable `card:{id}` controls.
- [ ] Use relational SQLite for these query regressions and production answer
      service/retrievers; a lifecycle provider stub cannot prove routing behavior.
      Record failing assertions before implementation. Existing probes in ignored
      round-2 scratch are supplemental evidence, not the only permanent tests.

## 2. Repair request planning and bounded retrieval — F02/F04

- [ ] Ensure meaningful single-token targets in contextual lookup requests reach
      an exact-priority query before broad cap. Distinguish target text from help
      wording and set qualifiers. Do not just special-case the word forecast.
- [ ] Keep explicit term/phrase precedence, normalization and stable ordering.
      Preserve exact short go/up support and stopword handling already approved.
- [ ] Resolve a supplied accessible set qualifier before candidate/result caps.
      Apply that qualifier in the authorized query/ranking, not only to the final
      three snippets. Do not load all cards or fix this by increasing constants.
- [ ] Retain owner, folder owner, saved-folder and live class membership filters
      for every tier. Set selection never grants access. A missing/inaccessible
      named set must not silently yield a different set's full answer.
- [ ] Preserve EntityId/ContainerId from server-owned DB projections and stable
      `card:{id}` keys. Never infer identity from editable metadata/title.
- [ ] Keep clarification text/outcome/evidence consistent. If a qualifier cannot
      be resolved unambiguously, clarify or refuse according to existing contracts.
      Do not reintroduce arbitrary multi-card full answers for an ambiguous lookup.
- [ ] Inspect generated Pomelo SQL/translation for the changed bounded queries
      using the pinned project provider, or run an authorized disposable MariaDB
      check. A translation-only check need not connect to a database. Record what
      was actually verified; do not call SQLite hosting-provider acceptance.

## 3. Repair external-fact promotion — F01

- [ ] Verify the requested answer type is supported by the retrieved evidence.
      An outside fact about today's weather is not a definition lookup even when
      the user owns weather. Keep deterministic local behavior in this phase.
- [ ] Distinguish explicit fact requests, ambiguous unknown requests and actual
      stored-content lookups. Use conservative refusal/clarification when evidence
      does not establish the requested fact. Keep bare unknown valid terms usable.
- [ ] Handle the English reproduction and relevant Vietnamese/English paired
      controls. Adding only weather/today strings to a blacklist is insufficient
      without a policy that checks lookup versus external-fact demand.
- [ ] Preserve safe password help, credential/prompt refusal, and current guide
      lookup. Do not introduce a paid/general-knowledge fallback or change the
      global provider used by Reading/Writing/Vocabulary consumers.

## 4. Verification

Run the newly added regressions first and confirm discovery, then the affected
broader suites. The reviewed checkout supports these commands:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixRound2 --filter 'FullyQualifiedName~AiRagBoundaryAccessTests|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiPhase4HardeningIntegrationTests|FullyQualifiedName~AiCancelAndStopIntegrationTests|FullyQualifiedName~AiQuotaReservationIntegrationTests|FullyQualifiedName~AiHistoryPagingIntegrationTests|FullyQualifiedName~AiCloseoutRegressionTests|FullyQualifiedName~OpenAiConsumerPipelineTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~HybridRagEvaluationBaselineTests' --logger 'console;verbosity=minimal'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixRound2 --no-build --filter 'FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~MlNet|FullyQualifiedName~CursorReviewProbeTests|FullyQualifiedName~RepairReviewProbeTests|FullyQualifiedName~ThirdReviewProbeTests' --logger 'console;verbosity=minimal'
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```

- [ ] Include any new separate test class explicitly in filters; zero tests is
      not success. Preserve HTTP/SignalR, quota/cancel/history and non-chat evidence.
- [ ] Rerun the round-2 behavioral scenarios against current binaries; do not
      accidentally load the older Phase1Review/Phase1ReviewRound2 output.
- [ ] Record exact results and provider evidence. No browser/UI change is planned;
      do not fabricate browser, live API, CI or deployment acceptance.
- [ ] Verify the frozen Phase 0 hash and applicable predecessor digests. Review
      the complete modified/untracked diff; append bug memory for verified fixes.

## Handoff and stop

Create `docs/feature-audits/hybrid-rag/phase-1/fix-round-2-handoff.md`. Map F01/F02/F04
to red/green evidence and remaining limitations. Keep F03 and the completed identity
part of F04 as preserved regressions. Refresh cumulative `scope-manifest.json`,
including new/changed source, tests and controlling repair guidance, excluding
recursive review/handoff/state results. Keep the predecessor chain auditable.

Set Phase 1 AWAITING_REVIEW; keep Phase 2–7 LOCKED. Do not mark APPROVED or start
Phase 2. Codex review round 3 must verify these repairs before any gate opens.

Companion prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-2.md`.
