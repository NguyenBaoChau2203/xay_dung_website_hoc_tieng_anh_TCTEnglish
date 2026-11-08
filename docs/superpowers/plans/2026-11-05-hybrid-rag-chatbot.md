# Hybrid RAG chatbot implementation plan — 2026-11-05

Status: researched proposal, not an implemented feature. Execute with one agent.
Execution update 2026-11-06: the owner requires per-phase Cursor Auto execution,
Codex review and repair loops. The [phase pack](hybrid-rag/README.md) and its
[review gate](hybrid-rag/review-gates.md) supersede this overview's execution order.
Use only one eligible phase prompt; do not execute the steps below end to end.
Branch created: `feature/hybrid-rag-chatbot`; research base: `6af768b`.
Owner demo: **2026-11-25**, local first. Dates below are proposed work windows,
not completed milestones or guaranteed delivery estimates.

## Outcome and authority

Support platform navigation, the authenticated user's data, and English knowledge
present in lessons/cards that user can access. Require evidence and useful refusal
or clarification. Do not promise arbitrary-question coverage or zero model errors.

Current request authorizes creating the local branch, researching the supplied
video/current system and preparing this proposal. Application implementation and
protected-file edits are not claimed as completed or approved by this document.
When the owner requests implementation, carry that authority forward and complete
the authorized local work. `Program.cs` requires express authority for the narrow
DI changes below. `appsettings.json` and project-file edits require their own
applicable grant; prefer existing dependencies and environment/options binding.
Commit, push, PR, remote CI, merge, staging and production remain separate stages.

Scope excludes server/framework upgrades, arbitrary web search, unrestricted SQL,
chatbot write actions, new admin authoring screens and private data synchronization.

Read [research/evidence](../../feature-audits/2026-11-05-hybrid-rag-research.md),
AGENTS.md and only the active step's skills/source. Historical April AI plans are
supporting evidence, not current provider/authority instructions.

## Architecture decisions

1. Add a chat-specific `IAiChatAnswerService`/`AiRagAnswerService` boundary under
   `TCTEnglish/Services/AI/Rag/` (proposed new names). Keep `IAiProviderClient`
   bound to the existing OpenAI transport for Reading/Writing/Vocab consumers.
   `AiChatService` delegates answering to the chat-specific boundary while retaining
   identity, conversation guard, persistence, quota, cancel and cleanup ownership.
2. Add a typed evidence contract: stable source key, kind/entity key, title,
   authorized local route, text/sentence IDs, content hash/version, capture time,
   visibility scope and relevant live facts. These are proposed fields, not
   existing properties on `KnowledgeSnippet`.
3. Implement real BM25 lexical ranking and semantic vector ranking over a bounded
   approved public corpus. Fuse ranked lists with RRF, initially equal weights
   and k=60, then tune on a development set. RRF is not answer confidence.
4. Query personal facts and private content with current EF/service access
   predicates; do not place them in the common index. Private-card lookup must
   select matches before limiting results. Respect public/class/owner/entitlement
   semantics from the feature services; ownership alone is not a universal rule.
5. Prefer a derived disk snapshot on D and immutable in-process indexes for the
   small local demo, subject to measured corpus/RAM/latency limits. Use separate
   lexical/vector interfaces to permit a later search backend. No new DB vector
   type or infrastructure is required for this initial search index.
6. Use existing text model/settings. Propose a separate embeddings client/model
   (`text-embedding-3-small`, initially 1536 dimensions) after bounded live access
   verification is authorized. Never send text-generation model settings blindly
   to the embeddings endpoint. Do not assume account access from a saved key.
7. Default demo answer mode is strict: server-rendered typed facts/templates and
   server-validated source sentence selection. The model selects allowed IDs;
   the server copies source text. Free-form paraphrasing requires a separate
   measured decision and must not be described as guaranteed grounded.
8. Keep prompts, retrieved content and conversation history separate. Historical
   assistant text is not factual evidence. Re-resolve referenced sources and their
   access on follow-up questions; ambiguous references ask for clarification.

## Delivery checklist and dependencies

### Step 0 — scope, corpus inventory and evaluation baseline (05–07 Nov)

- [x] Create branch, confirm starting HEAD/clean tree and inspect active provider.
- [x] Read video description and its full platform-provided Vietnamese ASR captions;
      distinguish these observations from Hybrid RAG engineering recommendations.
- [x] Record owner date, local environment and domain scope.
- [ ] Verify all 41 guides against current rendered features and legacy route
      forwarding; inventory published lessons/cards without exporting private data.
- [ ] Identify the approved adapter for any shared-content edit; follow
      `scripts/shared-data.ps1 -Action Status` and the shared-data workflow only
      when an authorized content change needs it. Do not run sync as discovery.
- [ ] Create an owner-readable coverage matrix: topic/source/access/last reviewed/
      answerable questions/missing content. Missing source means content work or
      refusal, not an instruction to invent an answer.
- [ ] Establish a 120-case synthetic evaluation set: 25 navigation, 25 authorized
      lesson/card questions, 20 personal facts, 15 out-of-scope, 15 injection or
      cross-user/access cases, 10 ambiguous/mixed requests, 10 stale/error/cancel.
      Include Vietnamese diacritics/no diacritics, English, paraphrases, multi-turn
      and a matching card beyond the old first-500 window. Cases may carry several
      risk tags. Store expected source keys/facts, not one rigid answer string.
- [ ] Split development and held-out evaluation examples before threshold tuning.

Affected established files: `wwwroot/data/ai/website-guides.json`, relevant feature
services/routes, existing retriever tests. Proposed new artifacts:
`TCTEnglish.Tests/Fixtures/AI/hybrid-rag-evaluation.json` and
`docs/feature-audits/2026-11-05-hybrid-rag-implementation-ledger.md`.
Do not create empty placeholders before those deliverables exist.

Acceptance: exact source inventory and labeled baseline results; unknown source
coverage, costs and provider access remain explicitly unknown until measured.

### Step 1 — chat boundary and access enforcement (08–10 Nov; depends on 0)

- [ ] Read relevant bug memory before behavior fixes; write failing regressions
      for the active-path grounding gap and specific access defects first.
- [ ] Add the chat-specific answer boundary; wire only `AiChatService` through it.
      Add narrow `Program.cs` registrations only with explicit authority.
- [ ] Reuse/adapt retrievers selectively. Repair Speaking retrieval/count scope,
      card candidate selection, class membership and paid/private access before
      activating those paths. Query independent EF work sequentially when it
      shares the request DbContext; parallel search over immutable indexes is safe.
- [ ] Treat classifier confidence as a routing hint. A low-confidence intent
      must not discard a well-supported corpus answer without retrieval. A high
      confidence intent must not bypass evidence/access requirements.
- [ ] Replace overly broad security substring decisions with a scoped policy that
      accepts legitimate account-help questions such as “How do I change my
      password?” while refusing requests to reveal credentials. Add both cases
      as meaningful regressions; keyword matching alone is not an injection defense.
- [ ] Introduce typed outcomes: `answered`, `needs_clarification`,
      `insufficient_evidence`, `out_of_scope`, `temporarily_unavailable`.
- [ ] Require access checks before content is returned or sent to any provider;
      do not leak unauthorized titles/counts/citations through fallback or errors.

Established files: `TCTEnglish/Program.cs`, `TCTEnglish/Services/AI/AiChatService.cs`,
and `Internal/Retrievers/{SpeakingRetriever,LearningAreaProgressRetriever,CardLookupRetriever}.cs`
under `TCTEnglish/Services/AI/`, plus source feature services as narrowly necessary.
Proposed tests: `AiRagAccessTests`, `AiRagChatBoundaryTests`.

Acceptance: user A cannot retrieve user B's private title/body/count; class removal,
unpublish and downgrade invalidate access. Non-chat AI consumer contracts still pass.

### Step 2 — corpus, indexing and genuine hybrid retrieval (11–14 Nov; depends on 1)

- [ ] Define typed allowlisted adapters for reviewed FAQ and public/published
      lesson text. Do not index source code, agent instructions, entire docs trees,
      user/chat/payment rows, secrets, runtime recordings or arbitrary uploads.
- [ ] Chunk by meaningful headings/paragraphs, initially 300–600 approximate tokens
      with small overlap where needed; preserve provenance and sentence IDs.
      Measure actual token budgets for Vietnamese/English; do not split IDs away
      from text, or depend on an unverified tokenizer claim.
- [ ] Use an explicit local index-build operation; no new paid startup worker.
      Record content hash, version, source IDs, model/dimension/chunker. Rebuild
      changed chunks, handle deletion, reject mixed models/dimensions/NaN vectors.
      Build to a new snapshot then atomically promote; retain the last valid one.
- [ ] Store generated snapshots in ignored `.appdata/local/ai-rag` on D, outside
      public assets. Check ignored status; do not commit derived local DB contents.
- [ ] Build BM25 over normalized title/body/keywords, preserving original text.
      Add dense cosine search over the same eligible chunks, top 20 per retriever
      as an initial hypothesis, RRF, deduplication and final evidence budget of
      roughly 4–6 chunks. Tune these values on development cases, not held-out data.
- [ ] Revalidate final source hash/publication/access against current authoritative
      data before use. Never feed stale changed/private content to a model. Removed
      sources fail closed and queue a rebuild; do not re-embed a whole corpus in chat.
- [ ] Missing/corrupt/stale index or embedding timeout uses eligible lexical/live
      sources if sufficient; otherwise return the typed unavailable/insufficient
      result. Never fall back to unconstrained OpenAI or silently substitute another
      model. Record retrieval mode for evaluation/diagnostics.
- [ ] Measure local corpus size/RAM and p50/p95 retrieval; verify a case won by
      semantic search and a case won by lexical exact match. Compare lexical-only,
      dense-only and hybrid retrieval on identical held-out cases.

Proposed code: `Services/AI/Rag/{Evidence,Bm25Retriever,VectorRetriever,
ReciprocalRankFusion,KnowledgeIndex,KnowledgeIndexBuilder,AiEmbeddingClient}.cs`
(names tentative; align with existing conventions during implementation).

Acceptance: demonstrable lexical and dense branches with versioned provenance,
negative access checks, deterministic fixture tests and measured retrieval quality.
Lexical-only degradation is reported as degradation, not a hybrid success.

### Step 3 — evidence-based answers, budgets and durable sources (15–18 Nov; depends on 2)

- [ ] Implement an evidence sufficiency policy evaluated on labeled cases: query
      facet coverage, exact requested entity/facts, retriever signals and ambiguity.
      Never use raw RRF or ML.NET probability as an answer-correctness percentage.
- [ ] Render personal facts by typed templates. For source-based answers require
      valid selected sentence IDs and copy the authorized source text server-side;
      fixed Vietnamese framing may organize the answer without adding facts.
- [ ] Treat retrieved text as untrusted data; ignore embedded instructions, avoid
      exposing Admin routes/prompts, and limit history/query/evidence separately.
      Cite every factual section; if a requested explanation/translation is absent,
      say so rather than completing it from model memory.
- [ ] Extend provider support for constrained selection only where needed. If
      structured output is used, preserve old transport callers and verify the
      exact Responses API contract against official docs and fake HTTP tests.
- [ ] Keep one conversation guard and one question reservation across the chat
      operation. Include bounded retrieval/query-embedding and final context in
      pre-call budget planning; source injection must not occur after an apparently
      completed token-budget check. Cancel stops retrieval as well as generation.
- [ ] Track embedding and generation usage separately. Known embedding spend must
      be settled even when generation/validation fails; do not release a reservation
      as unused after a successful paid embedding call. Preserve known-usage recovery
      and committed-reply behavior. Define refusal/question-count semantics explicitly
      and verify Standard's existing 15-question policy instead of silently changing it.
- [ ] Add optional structured grounding/source metadata to HTTP replies, stream
      completion, message ViewModels and history. Preserve existing field names,
      events and legacy rows; include the same metadata after reload and pagination.
- [ ] Persist an optional versioned grounding envelope on `AiMessage` (proposed
      nullable serialized metadata field); use existing content for answer text.
      Store only required source references/version and safe presentation metadata,
      not extra personal source snapshots. Reauthorize source metadata on read.
      Do not return stale private titles from stored citations after access changes.
      If a historical answer body contains a now-revoked lesson/class source,
      suppress that answer body on history reads and when building provider context,
      with an explicit unavailable-source notice. Hiding its citation alone is
      insufficient. Apply the same policy to mixed-source answers and cached text.
- [ ] Apply `tct-ef-migration` for that persisted change: assess MariaDB-compatible
      nullable text mapping, generate migration + Designer + snapshot together,
      verify upgrade/repeat/preserved historical rows on disposable MariaDB 10.5.23,
      check pending model changes. No applied-migration edits or destructive drops.

Established files: `Models/AiMessage.cs`, `Models/DbflashcardContext.cs`,
`ViewModels/AI/{ChatReplyDto,AiChatMessageViewModel}.cs`,
`Realtime/AiStreamMessages.cs`, `Controllers/AiController.cs`,
`Services/AI/{AiChatService,AiConversationService,AiStreamingService,OpenAiProviderClient}.cs`.
Only touch files needed by the final contract; this list is a review boundary,
not an instruction to rewrite every file.

Acceptance: invented evidence IDs are rejected; valid IDs with unrelated facts
do not count as supported answers; numeric facts match the live service; no
assistant reply persists before validation; sources survive reload without
cross-user exposure; cancellation/quota/non-chat consumers retain regressions.

### Step 4 — source UI, resilience and local acceptance (19–21 Nov; depends on 3)

- [ ] Follow DESIGN.md and `tct-ui-component`; add a compact “Nguồn” area with
      encoded titles, safe local links and accessible controls in full/embed chat.
      Do not insert raw model HTML or accept model-provided href values.
- [ ] Preserve quick actions, Enter submit guard, Stop, retry and history order.
      Validate source display for HTTP and SignalR completion and paged old messages.
- [ ] Bound cache sizes/TTL. Partition personalized cache by user/access scope,
      source version and relevant conversation state. Recheck access before return;
      invalidate on logout/role/membership/content changes. Do not share fact answers.
- [ ] Log request IDs, source keys/version, retrieval mode/outcome, latency and
      usage without full prompts, private content, credentials or hidden reasoning.
      Detect unavailable/rejected/stale sources and provider failures separately.
- [ ] Test on the exact local demo machine and accounts, including mobile/full/
      embed, keyboard, source link navigation, A→logout→B, no history and long text.
      Distinguish fake-provider browser checks from live end-to-end answers.
- [ ] Use `scripts/start-local.ps1 -NoBrowser`, a free port/configuration if an
      existing process must be preserved, then `/health/ready` and
      `scripts/speech-status.ps1`. Startup must not call speech or bulk embeddings.

Established UI: `Views/Ai/{Chat,ChatEmbed,_ChatShell}.cshtml`, `wwwroot/js/ai-chat.js`,
`wwwroot/css/ai-chat.css`; load `tct-api-endpoint` for HTTP contract changes,
`tct-ui-review` before browser acceptance, and relevant domain skills for source edits.

Acceptance: sources are usable on all chat entry paths, no unsafe links or injected
markup, API outage does not produce invented text, and existing workloads survive.

### Step 5 — freeze and rehearsal (22–24 Nov; depends on 4)

- [ ] Prepare 15–20 demo questions across navigation, personal facts, lessons,
      follow-ups, refusal, ambiguity and access controls; use synthetic data only.
- [ ] Run the held-out evaluation and a separate adversarial/access suite. Proposed
      gates: Recall@5 ≥90% on answerable source questions; correct supported answer
      ≥95% on in-scope answerable cases; no unauthorized information or unsupported
      factual addition in the evaluated safety cases; correct refusal/clarification
      on all labeled hard-negative demo cases. Record denominators and failures.
- [ ] Record coverage separately from refusal: a system refusing every question
      fails the answerable gate. Report per-topic results and unsupported topics.
- [ ] Proposed local warm retrieval p95 ≤300 ms and complete-answer p95 ≤8 s with
      working API/network; record actual sample size, hardware, cold/warm and
      provider latency. Change scope if needed; do not relabel failed targets.
- [ ] Rehearse cold startup and network loss/recovery. Prebuild/validate index,
      confirm saved key/account quota by an explicitly authorized bounded live run,
      and freeze reviewed corpus/model/index versions. Do not present cached output
      as a fresh live answer; personal facts remain live.
- [ ] Record exact startup/ready URLs and fallback procedure in a demo runbook.
      Prepare screen-readable refusal examples. Keep 24 Nov as a buffer, not a
      feature-expansion day. Demo is 25 Nov; no production deployment is implied.

## Verification commands and evidence

Commands below are established repository checks for the future implementation,
not checks claimed to have passed during research. Preserve running processes;
use an isolated build configuration/output if ordinary build outputs are locked.

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --filter 'FullyQualifiedName~AiRag|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~OpenAiConsumerPipelineTests|FullyQualifiedName~AiCancelAndStopIntegrationTests|FullyQualifiedName~AiHistoryPagingIntegrationTests|FullyQualifiedName~AiQuotaReservationIntegrationTests'
node --test scripts/tests/test_ai_chat_submit_guard.cjs scripts/tests/test_ai_chat_load_older_order.cjs
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext
git diff --check
```

Add meaningful new retrieval/access/citation/index/budget tests before claiming
success; the existing filter alone does not prove RAG. Run the broader test suite
once the integrated change passes focused checks. Provider-specific schema checks
must use disposable MariaDB; SQLite is not provider proof. Live API evaluations are
opt-in with explicit request/token budgets; no surprise paid bulk evaluation.

## Risks, decisions and current ledger

- Unknown corpus size/quality and exact live account/model access: resolve in 0/2.
- Narrow `Program.cs` DI authority is still required for runtime implementation.
  No dependency/appsettings edit is assumed. The optional persisted grounding
  field requires the migration work in step 3 if chosen during implementation.
- Private semantic retrieval is outside the common index. Start with authorized
  lexical/live retrieval and disclose that limit; expand only with measured need.
- No prompt-only guarantee or self-rated confidence proves factual support.
  Strict selection reduces invented additions; source correctness remains a risk.
- No current implementation, baseline eval, app build, browser acceptance or paid
  provider run is claimed by this proposal. Research/document checks are separate.
- No schema/content changed during research; no migration/shared package now.
  Future supported content edits need selective export/review in the same change.
- Append bug memory only when a fix is actually implemented and verified. Report
  new findings now; do not mark known issues resolved from a plan.

Handoff in Vietnamese: outcomes, changed files, actual tests/measurements, coverage,
risks/blocked checks and local versus CI/staging/production status.
