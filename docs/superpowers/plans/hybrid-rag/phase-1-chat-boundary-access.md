# Phase 1 — chat boundary, access and safe internal baseline

Entry: Phase 0 APPROVED, latest scope verified, Phase 1 READY and owner launch.
Read [gates](review-gates.md), [state](review-state.md), accepted Phase 0 contracts
and active bug memory. Mode: IMPLEMENT_LOCAL for the chat boundary/access fixes.
Skills: tct-superpowers, tct-engineering, tct-security-audit; API/domain skills for
any affected contract. Dates are tentative 08–09 Nov; acceptance controls progress.

## Outcome and scope

Put chat through a dedicated internal answer boundary, fix proven access/retrieval
defects and establish safe deterministic answers/refusals. This phase does not
claim Hybrid RAG. Keep other consumers on the unchanged IAiProviderClient transport.

Established source: `Services/AI/AiChatService.cs`, `IAiProviderClient.cs`,
`Internal/InternalKnowledgeProvider.cs`, `Internal/Retrievers/{SpeakingRetriever,
LearningAreaProgressRetriever,CardLookupRetriever}.cs` under `TCTEnglish/`;
feature access services and `TCTEnglish/Program.cs` where justified.
Proposed new boundary: `TCTEnglish/Services/AI/Rag/IAiChatAnswerService.cs` and its
implementation/contracts; use approved Phase 0 names rather than inventing duplicates.

## Authority and preserved behavior

Local launch permits this phase's implementation. Before Program edits establish
the owner's express narrow chat-RAG DI grant. Once granted, record and reuse its
scope in later phases instead of asking repeatedly. Do not edit appsettings or
project files, change current models, seed private data or expand Admin powers.
Preserve identity helpers, NotFound for IDOR, antiforgery, conversation ownership,
quota semantics, Stop/cancel, historical rows and non-chat AI HTTP contracts.

## Ordered checklist

- [ ] Read Phase 0 approval and reconcile accepted contracts with current source.
      Record baseline failing behavior before each meaningful fix.
- [ ] Add the chat-only answer abstraction and adapt AiChatService; keep the
      orchestration/guard/quota/persistence lifecycle in the existing owner service.
      Do not globally replace IAiProviderClient with an internal/RAG provider.
- [ ] Wire minimal DI with proper scoped/singleton lifetimes. Singleton indexes
      must not capture DbContext or a scoped retriever. Verify scope disposal.
- [ ] Default chat answers to authorized internal facts/templates and current guide
      evidence; missing evidence refuses/asks clearly. No unconstrained OpenAI
      fallback. Keep the richer semantic/generation path inactive until Phase 4.
- [ ] Fix Speaking catalog/suggestion/count access against canonical feature rules;
      distinguish published/public versus owner-private and Premium entitlement.
      Include counts, titles and fallback, not only selected result bodies.
- [ ] Fix card retrieval so relevant authorized candidates are selected before a
      result cap; provide stable ordering and bounded data access. Test a match
      beyond the former first-500 window. Reuse verified class/shared access rules
      where the product grants them; do not add arbitrary cross-user search.
- [ ] Verify class membership and revoked/locked sources before snippet creation.
      Minimize notification/billing details: current-user typed facts only, no full
      chat/payment export. Use feature business-date semantics for today's counts.
- [ ] Replace the naive password substring block for legitimate help, while
      maintaining refusal of credential/prompt disclosure. Source-contained attacks
      remain untrusted data. Do not use classifier scores as authorization.
- [ ] Define all typed outcomes and deterministic refusal/clarification text.
      Weak intent confidence must not be the only reason to reject an authorized
      guide/card match; no-confidence cases still have bounded evidence lookup.
- [ ] Add meaningful regressions and append bug memory for verified fixes. Keep
      unrelated known issues unchanged. Review SQL projections/limits and EF I/O.

## Required regression matrix

| Trigger | Expected evidence |
| --- | --- |
| User A asks for B's private video/card/class or count | No title/body/ID/count leaks and no content sent to the provider |
| Membership removed or Premium downgraded | Same access result as the canonical feature, including fallback |
| Matching card beyond 500 unrelated cards | Correct authorized match found with stable bounded query behavior |
| Safe password-change question versus credential extraction | Help is allowed; secret extraction is refused |
| Chat through HTTP and SignalR | New boundary used; existing identity/guard/quota/cancel semantics preserved |
| Reading/Writing/Vocab suggestion calls | Existing OpenAI transport remains; no chat refusal/prompt injected |

Use recording fakes to assert what crosses the provider boundary. Do not call paid
APIs. Discover actual existing tests and add focused `AiRag` boundary/access tests.
Run relevant OpenAiConsumerPipelineTests, current chat cancel/quota/history and
retriever suites, plus build. Required checks may use isolated outputs if locked.
Verify new filters discover tests; a zero-test run is not success.

## Gate and handoff

Acceptance requires the regression matrix, current contracts and scope evidence,
not a renamed provider or a prompt-only refusal claim. No persisted model/content
change is expected; if genuinely necessary, assess/ship its migration or selective
shared-data export in this phase rather than deferring dependent work silently.

Write Phase 1 handoff/manifest, record actual Program authority and remaining gaps,
set AWAITING_REVIEW and stop. Phase 2 stays LOCKED pending approving Codex review.
