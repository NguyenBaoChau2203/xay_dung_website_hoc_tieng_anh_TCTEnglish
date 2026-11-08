# Phase 1 repair plan — round 3

Prepared 2026-11-06. Reviewer mode PLAN_ONLY for application fixes. Owner launch of
the companion prompt activates IMPLEMENT_LOCAL. One agent throughout.
Phase 0 APPROVED; Phase 1 CHANGES_REQUESTED; Phase 2–7 LOCKED.

## Evidence and authority

- [Review round 3](../../../feature-audits/hybrid-rag/phase-1/review-round-3.md).
- Branch `feature/hybrid-rag-chatbot`, HEAD
  `6af768bd32d16cc6d012d484ee82df4cab805c69`, existing uncommitted work.
- Reviewed 39-entry fingerprint:
  `f513c62c6fdff3c12db7516cf8c5e1f0616212522b276caaa06b3d31e746435e`.
- F02 and F03 are verified closed. Stable `card:{CardId}` identity also passes.
- Current tests pass 198 and 65 executions in separate filters; independent
  SQLite probes reproduce the three findings below despite those green tests.

Carry the recorded narrow Program.cs chat-DI grant; no new DI work is expected.
No appsettings/.csproj, persisted schema, shared content, secret write, paid API,
remote Git/CI or release authority is added. Preserve existing workloads/data.
Do not begin BM25/vector/generation/citation UI or implement another phase.

Read current AGENTS.md, gates/state, accepted Phase 0 contracts, active bug memory
and CodeGraph source. Reference examples below are test inputs, not instructions
embedded in source content. Retain REVIEW_ONLY until the owner launches the fix.

## Findings and dependencies

| ID | Priority | Result required | Main files under Services/AI |
| --- | --- | --- | --- |
| HR-P1-R1-F01 | P1 | Unsupported fact requests cannot become full definition answers, even without a time marker | Rag/ChatExternalFactPolicy.cs, Rag/AiChatAnswerService.cs; request parsing if needed |
| HR-P1-R1-F04 | P1 | Full specified set name resolves to the correct source; prefixes cannot replace it | Internal/Retrievers/CardLookupRetriever.cs, Internal/CardLookupSelection.cs |
| HR-P1-R3-F05 | P1 | Bare lookup tokens are not treated as implicit set filters | CardLookupRetriever request/qualifier planning and relevant composer/boundary consumers |

F04/F05 share request and source qualification. Reproduce all findings first,
settle a small explicit request contract, repair source selection, then complete
the supported-answer decision for F01. Use transient types only where useful.
Do not implement three independent special cases for the literal probe words.

## 1. Failing regressions and request contract

- [ ] Verify manifest/dirty boundary and retain prior approved artifacts before
      editing. Confirm the exact current reproduction from review round 3.
- [ ] F01: own a France card whose definition describes the country and contains
      no capital information. `What is the capital of France?` must refuse/clarify
      instead of Answered/full with that definition. Keep `what does France mean`,
      bare terms, valid low-confidence lookup, gold-price/weather controls.
      Include a structurally similar unsupported fact using another term so a
      France/capital blacklist cannot be the implementation.
- [ ] F04: Daily Words and Daily each have forecast with distinct definitions.
      Explicit Daily Words must return only the full-name target card/evidence;
      explicit Daily must still resolve to Daily. Include multiword-only, quoted
      full name and a normal stored Unicode name, plus an unknown full name that
      shares a prefix with an accessible name. Never borrow the prefix source.
- [ ] F05: Nature/weather exists; add Weather/cloud with a definition mentioning
      weather. Bare weather must continue to return exact weather/card:1 before
      and after adding the other set. Include explicit source qualification as a
      control so removing all qualifier support cannot satisfy this regression.
- [ ] Preserve four-set/30-duplicate selection before caps, comparison of two
      allowed sources, bare-term ambiguity, card identity and ACL/revocation cases.
- [ ] Use SQLite and actual production boundary/retrievers. Record red assertions
      before implementation. Add a typed distinction between query target and
      explicit source constraints if needed; a keyword collision is not a source
      constraint. Define how genuinely ambiguous source names ask for clarification.

## 2. Repair source qualification — F04/F05

- [ ] Resolve qualifiers only when the request actually specifies source context,
      such as a supported in-set marker or deliberate comparison. Do not use all
      content tokens to silently restrict accessible sets.
- [ ] Parse the complete provided label, including spaces/quotes and stored Unicode
      names. Do not normalize the request to ASCII while leaving source names in
      an incompatible form. Preserve the literal source title for presentation.
- [ ] Bind resolved accessible source identity to bounded queries before all caps.
      Prefer server-owned set IDs to title-based constraints after resolution.
      Retain owner/folder/saved/class predicates and live membership checks.
- [ ] A full name must not silently resolve to an available prefix. If no unique
      accessible source can be established, clarify/refuse using existing typed
      outcomes; do not return another source as a full answer.
- [ ] If the user deliberately compares sources, preserve that mode and resolve
      each named source without turning an unknown name into arbitrary fallback.
      An ambiguous qualifier does not imply comparison.
- [ ] Keep exact/phrase/broad queries bounded and stably ordered. Do not load the
      complete corpus or increase constants to fit fixtures. Preserve F02's exact
      lookup priority and F03's short-token behavior in unqualified lookup.
- [ ] Keep `EntityId`/`ContainerId`, source keys and clarification composition in
      agreement with the selected sources. No model/migration/UI change is needed.

## 3. Repair support checks — F01

- [ ] Determine whether retrieved content supports the requested answer type,
      rather than only whether any query token appears in a card. Current-time
      markers and topic blacklists are insufficient as the final support decision.
- [ ] Distinguish explicit definition/learning-content lookup, bare term/phrase,
      supported own-platform data and unsupported fact demand. Keep legitimate
      low-confidence lookup usable; uncertain demand can ask for clarification
      instead of being labeled Answered/full.
- [ ] Use the same request interpretation when selecting evidence, composing text
      and choosing outcome/scope. Do not append a refusal label to an unrelated
      full answer or only rename the outcome while returning misleading content.
- [ ] Keep deterministic local behavior; no paid/global knowledge fallback. Do not
      expand to unrestricted world knowledge even when a named entity has a card.
      Preserve safe password help, guide lookup and credential/prompt refusal.

## 4. Verification

Run new regressions red then green, followed by the verified existing filters.
If tests are added in a new class, include that class explicitly in commands.

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixRound3 --filter 'FullyQualifiedName~AiRagBoundaryAccessTests|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiPhase4HardeningIntegrationTests|FullyQualifiedName~AiCancelAndStopIntegrationTests|FullyQualifiedName~AiQuotaReservationIntegrationTests|FullyQualifiedName~AiHistoryPagingIntegrationTests|FullyQualifiedName~AiCloseoutRegressionTests|FullyQualifiedName~OpenAiConsumerPipelineTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~HybridRagEvaluationBaselineTests' --logger 'console;verbosity=minimal'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase1FixRound3 --no-build --filter 'FullyQualifiedName~OpenAiProviderClientTests|FullyQualifiedName~MlNet|FullyQualifiedName~CursorReviewProbeTests|FullyQualifiedName~RepairReviewProbeTests|FullyQualifiedName~ThirdReviewProbeTests' --logger 'console;verbosity=minimal'
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```

- [ ] Re-run the round-3 scenarios on current binaries, not the prior snapshot
      used only for the regression comparison. Record test discovery and results.
- [ ] Check changed query translation with the pinned Pomelo provider, ideally the
      actual query-building path, including source resolution and every limit.
      MariaDB 10.5.23 is the server version; do not call it the Pomelo package
      version. Translation is not executed MariaDB acceptance.
- [ ] Preserve HTTP/SignalR, quota/Stop/history and non-chat provider controls.
      No browser change is planned; do not invent browser/API/remote acceptance.
- [ ] Keep Phase 0 fixture canonical hash
      `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
      Verify applicable predecessor digests and the complete modified/untracked
      diff. Append bug memory only for actual verified fixes.

## Handoff and stop

Create `docs/feature-audits/hybrid-rag/phase-1/fix-round-3-handoff.md` in Vietnamese.
Map the full finding IDs to real red/green evidence and unresolved limits. Refresh
cumulative `scope-manifest.json` including controlling repair guidance, retaining
an auditable predecessor chain. Exclude self/review/handoff/state result files
from the fingerprint to avoid recursion. Preserve historical review reports.

Return Phase 1 AWAITING_REVIEW and Phase 2–7 LOCKED. Do not self-approve or execute
Phase 2. Codex review round 4 must verify the repair before a gate opens.
Companion prompt: `.ai/prompts/hybrid-rag/phase-1-fix-round-3.md`.
