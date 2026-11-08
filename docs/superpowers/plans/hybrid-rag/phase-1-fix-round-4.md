# Phase 1 repair plan — round 4

Prepared 2026-11-06. Reviewer mode PLAN_ONLY for application fixes. An owner launch
activates IMPLEMENT_LOCAL for this scope. Use one agent. Phase 0 APPROVED;
Phase 1 CHANGES_REQUESTED; Phase 2–7 LOCKED.

## Evidence, scope and authority

- [Review round 4](../../../feature-audits/hybrid-rag/phase-1/review-round-4.md).
- Branch `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, uncommitted work.
- Reviewed 42-entry fingerprint:
  `ba644991f0284ee7a0b43eac3e1a5670b492f2b5941da0506be9b1b024bb502d`.
- Open: HR-P1-R1-F01 and HR-P1-R1-F04, both P1. Verified closed: F02/F03 and
  HR-P1-R3-F05. Preserve stable `card:{CardId}` identity and every access check.

Carry the recorded narrow Program.cs chat-DI grant without asking again; no DI
edit is expected. No new appsettings/.csproj, persisted schema/content, paid API,
secret write, remote Git/CI or release authority. Preserve data and workloads.
Do not implement BM25/vector/generation/citation UI or start another phase.

Use current AGENTS.md, gates/state, accepted Phase 0 contracts, relevant bug memory
and CodeGraph source. Application scope is the answer support policy/boundary,
transient request parsing and source-label parser/selection under Services/AI;
focused tests, bug log and handoff/manifest. No entity/migration change is needed.

## Ordered work and acceptance

### 1. Reproduce and correct the test oracle

- [ ] Verify scope/dirty boundary and preserve the approved predecessor chain.
- [ ] F01: France definition `a country; its capital is not provided in this card`.
      Both `What is the capital of France?` and `Thủ đô của France là gì?` must not
      be Answered/full. A stored-meaning request for France remains valid.
- [ ] Add a non-negated source that merely mentions an attribute without its value,
      plus another fact phrasing/entity. Do not fit the literal negation/country.
- [ ] Correct the Kenya oracle in
      `UnsupportedAttributeFact_RefusesUnrelatedDefinitionWhileSupportedAndLookupStillAnswer`:
      “the capital city is recorded here” does not supply a city. That input must
      not require full answer. A positive fact control, if retained, must contain
      an actual explicit synthetic value and verify that value in the answer.
- [ ] F04: Daily Study and Daily contain forecast with distinct definitions. Test
      quoted/unquoted Daily Study, explicit Daily, a label beginning with a current
      framing token, and a valid single-word label such as Study. Full label must
      resolve unchanged, or conservatively clarify; never substitute a prefix.
- [ ] Preserve generic contextual lookup `Giải thích forecast trong bộ thẻ của tôi`,
      Daily Words, stored Unicode names, nonexistent full-name prefix control,
      duplicate-name clarification, comparison and 30-duplicate pre-cap selection.
- [ ] Use relational SQLite and production boundary/retrievers; record real red
      assertions before implementation. Do not alter the frozen Phase 0 fixture.

### 2. Repair F04 by preserving explicit labels

- [ ] Separate target framing from the source label. Generic words in SetName are
      data, not stopwords to remove. Do not strip leading/trailing Study/Class/My/
      Notes/Reading or other LookupFraming tokens from a supplied literal name.
- [ ] Preserve quotation/source-span boundaries as needed before normalization;
      resolve the full normalized label against accessible server-owned SetIds.
      Keep original labels for display. Unknown full labels never borrow a prefix.
- [ ] Handle recognized generic phrases such as “trong bộ thẻ của tôi” explicitly,
      preserving F02, instead of applying generic word deletion to every name.
- [ ] Keep ambiguous names as clarification and deliberate comparison distinct.
      Source qualification remains before every cap, with all ACL predicates.
- [ ] Keep bare lookup free from implicit source filtering: the F05 before/after
      Weather regression must remain green. No unbounded card load or cap increase.

### 3. Repair F01 with a defensible supported-answer decision

- [ ] Define which answer kinds the Phase 1 deterministic baseline can establish
      from current evidence. Refuse/clarify when support is unknown. A safe baseline
      does not need unrestricted natural-language fact answering in this repair.
- [ ] Attribute keyword/entity overlap alone must never be proof of the requested
      value. Absence notes, generic mentions and unrelated examples remain
      insufficient. Do not repair this by adding a list of negation phrases.
- [ ] Route equivalent English/Vietnamese fact demand consistently. A token such
      as `la gi` or `meaning` anywhere in a sentence is not sufficient to declare
      the entire request a supported definition lookup.
- [ ] Apply support decisions consistently across retrieval, composition and typed
      outcome/scope, including paths not recognized by the previous narrow English
      prefix parser. Preserve actual stored-meaning, bare term/phrase, contextual
      low-confidence lookup, guide help and typed current-user facts.
- [ ] A conservative refusal/clarification is acceptable for fact kinds the local
      baseline cannot verify. If retaining a fact-answer path, demonstrate a real
      evidence value and supporting relation, not a word-presence heuristic. Do
      not add paid APIs, general knowledge fallback or new persisted schema.
- [ ] Review nearby positive controls for the same mistaken support assumption;
      do not claim full lookup of one term from a different card solely because
      its definition mentions the target. Keep controls semantically meaningful.

### 4. Verify and document

Run new regressions red then green, then these existing applicable filters. Add
any new test class explicitly if outside AiRagBoundaryAccessTests.

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixRound4 --filter 'FullyQualifiedName~AiRagBoundaryAccessTests|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiPhase4HardeningIntegrationTests|FullyQualifiedName~AiCancelAndStopIntegrationTests|FullyQualifiedName~AiQuotaReservationIntegrationTests|FullyQualifiedName~AiHistoryPagingIntegrationTests|FullyQualifiedName~AiCloseoutRegressionTests|FullyQualifiedName~OpenAiConsumerPipelineTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~HybridRagEvaluationBaselineTests' --logger 'console;verbosity=minimal'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixRound4 --no-build --filter 'FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~MlNet|FullyQualifiedName~CursorReviewProbeTests|FullyQualifiedName~RepairReviewProbeTests|FullyQualifiedName~ThirdReviewProbeTests' --logger 'console;verbosity=minimal'
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```

- [ ] Re-run the round-4 behavioral scenarios on new binaries. Zero discovery,
      lifecycle stubs, stale output or static checks do not prove this behavior.
- [ ] Preserve F02/F03/F05, source identity, access/revocation, HTTP/SignalR,
      quota/Stop/history and non-chat provider controls. Keep safe password help
      and prompt/credential refusal. No browser/UI change is planned.
- [ ] If query construction changes, recheck actual production builders through
      Pomelo translation (MariaDB server target 10.5.23). Distinguish translation
      from DB execution. No disposable MariaDB/schema work is required by DTO-only
      changes; record actual limits without fabricating provider acceptance.
- [ ] Verify complete modified/untracked diff and frozen canonical hash
      `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
      Append bug memory for verified fixes only; preserve historical review reports.

## Deliverables and stop

Create `docs/feature-audits/hybrid-rag/phase-1/fix-round-4-handoff.md` in Vietnamese,
mapping both full finding IDs to red/green evidence, oracle corrections and limits.
Refresh cumulative `scope-manifest.json`, including implementation/tests/controlling
repair guidance and the auditable predecessor chain; exclude recursive result,
handoff and state files. Report actual local results; no CI/release claim.

Return Phase 1 AWAITING_REVIEW; Phase 2–7 remain LOCKED. Do not self-approve or
start Phase 2. Codex review round 5 must verify readiness. Companion prompt:
`.ai/prompts/hybrid-rag/phase-1-fix-round-4.md`.
