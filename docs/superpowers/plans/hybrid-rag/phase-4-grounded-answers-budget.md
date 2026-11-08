# Phase 4 — strict answers, conversation and durable usage lifecycle

Completion record, 2026-11-07: Phase 4 APPROVED after the owner-authorized local
review/repair closeout. See [round 2 evidence](../../../feature-audits/hybrid-rag/phase-4/review-round-2.md)
and [review-state](review-state.md) for the final scope and current gate. Phase 5 is
READY for owner launch; live provider/semantic acceptance remains in Phase 7.

Entry: Phase 3 APPROVED, latest scope matches, Phase 4 READY and owner launch.
Mode: IMPLEMENT_LOCAL. Skills: tct-superpowers, tct-engineering, security,
tct-ef-migration for any persisted lifecycle change, and relevant API/domain skills.
Tentative dates 15–17 Nov. This is the first phase allowed to connect semantic
retrieval and constrained text selection to the actual chat lifecycle.

## Outcome and source boundary

Answer from authorized facts/evidence, preserve useful refusal and follow-up behavior,
and make multi-stage provider spend safe under concurrent conversations, cancel,
validation failure and restart. Fake-provider end-to-end tests are mandatory;
live calls still need a separate bounded grant.

Inspect current `AiChatService`, `AiContextBuilder`, `AiDailyQuotaReservationService`,
`IAiProviderClient`, `OpenAiProviderClient`, `AiConversationExecutionGuard`, existing
quota ledger/entity/mapping and all approved RAG contracts. Preserve non-chat
transport semantics. Do not modify appsettings/project files without express authority.

## A. Answer policy and conversation checklist

- [ ] Implement query intent/facet handling without treating classifier confidence
      as proof. Separate a safe greeting, authorized personal facts, source-based
      questions, ambiguity, unsupported content and actual service unavailability.
- [ ] Treat historical assistant content and user-provided claimed facts as
      untrusted conversation, never as authoritative evidence. Resolve follow-up
      references to current accessible source IDs; ask when multiple referents fit.
- [ ] Define mixed-request behavior: answer only supported allowed parts, explicitly
      identify unanswered parts, and do not blend outside facts into supported text.
- [ ] Check evidence sufficiency per requested facet, relevant entity and required
      fact. Do not use a raw RRF/cosine score threshold as answer correctness.
- [ ] Default to server-rendered typed facts/templates. For source questions model
      output selects only valid supplied sentence IDs and allowed presentation
      structure; server copies their exact text. No model-written factual additions,
      invented teaching examples, translations or grammar absent from the source.
- [ ] Reject unknown IDs, unrelated selected fragments, wrong entity, unsafe links,
      malformed/partial output, empty selection and instruction-like text presented
      as an answer. Valid IDs alone do not prove the question was actually answered;
      deterministic facet rules plus labeled cases and review must establish support.
- [ ] Use fixed safe refusal/clarification/extractive fallback where appropriate.
      Typed temporarily_unavailable must remain distinct from outside scope.
- [ ] Keep system policy, user messages, history and evidence in separate inputs;
      retrieved commands cannot override policy or authorize tools/SQL/web/actions.
      Preserve exact evidence-to-source provenance in the transient answer result.
- [ ] If structured output transport is added, verify current official Responses
      contract and fake HTTP payloads. Scope it to explicit chat request options;
      do not alter non-chat consumers or assume JSON schema proves factual accuracy.

## B. Budget, concurrency and persistence checklist

- [ ] Specify the lifecycle state machine before editing: admission/authorized
      retrieval/query embedding/context selection/generation/validation/commit/
      settlement/recovery. Define known, unknown and no-spend failure states.
- [ ] Preserve one conversation execution guard and one question reservation per
      logical turn. Compute a conservative upper bound before paid stages, covering
      embedding input plus final context/history/evidence/output; never add context
      after the only budget check. Keep unknown oversized input out of API calls.
- [ ] Track embedding and generation usage separately, with cumulative known spend
      and stable request identity. Token-budget arithmetic is not a currency invoice.
      Preserve Standard's existing 15-question policy; state how deterministic
      answers/refusals and failed turns count and test that agreed policy.
- [ ] Fix the verified recovery hazard: current ReserveQuestionAsync settles any
      Pending ledger with ActualTokens>0. A second conversation must not settle the
      first turn between embedding and generation. Use reviewed stage/active-lease/
      terminal-intent semantics or an equivalent durable design; an in-process flag
      alone is insufficient across scopes/processes. Recovery must skip live work.
- [ ] Persist known spend safely without flushing chat entities from the request
      DbContext. Do not prematurely terminal-settle a live stage or release known
      embedding usage after later failure. Terminal finalize remains idempotent.
- [ ] Define expired/crashed-stage reconciliation, including cases where an HTTP
      timeout leaves provider usage unknown. Do not label unknown charges as zero
      or create permanent unbounded reservations; record the conservative policy,
      lease expiry/recovery ownership and operator-visible limits.
- [ ] Cancellation must stop embedding/retrieval/generation and use bounded cleanup.
      Preserve no-orphan prompts/assistant rows and do not trigger duplicate paid
      retries after a committed reply or after the UI receives a recoverable error.
- [ ] Validate complete answer/evidence before persisting assistant success text.
      Persist known spend even when no valid answer is accepted. Keep current
      committed-reply and known-usage recovery guarantees from the bug memory.
- [ ] If stage-aware tracking changes persisted entities/mapping, assess it now
      with tct-ef-migration. Ship additive migration, Designer and snapshot in this
      phase, with nullable/default-safe fields and no old-history edits. Do not defer
      lifecycle-dependent schema to Phase 5. Report a verified no-migration rationale
      only if the final persisted mapping truly does not change.

## Required regression matrix

| Scenario | Required result |
| --- | --- |
| Unsupported/ambiguous/injected source or model output | Correct typed refusal/clarification; no invented success text |
| Follow-up uses previous assistant fabrication or revoked source | Fabrication does not become evidence; access is rechecked |
| Embedding succeeds, generation fails/invalid/cancelled | Known spend retained and settled; no accepted assistant artifact |
| Two conversations for one user overlap between paid stages | No early recovery/finalize, double question or undercounted second stage |
| Request scope closes/process restarts after known spend | Durable bounded recovery and correct counters |
| Timeout with unknown usage, late reply, repeated cleanup | Defined conservative behavior and idempotent accounting |
| Final context reaches budget cap | No excess outbound request; safe shrink/refuse before spending |
| Cancel before/after commit and client retry | No orphan/double charge/duplicate assistant; existing Stop works |

Add failing regressions before fixing lifecycle behavior. Use controlled barriers,
separate scopes/DbContexts and fault injection, not timing-only Task.Delay tests.
Run affected chat/consumer/cancel/history/quota regressions and build. For relational
changes/concurrency provide disposable MariaDB 10.5.23 upgrade/repeat and actual
counter/data preservation evidence; SQLite alone cannot approve provider behavior.
Use repo-local EF for generation/pending-model check. No destructive dev DB reset.

## Exit and boundaries

Deliver working grounded chat through fake providers, outcome/facet tests, lifecycle
diagram/state table, usage-policy tests, any required reviewed migration and current
scope/handoff. Live source links remain transient until Phase 5 persists them;
Phase 6 owns user-facing source controls. Record what cannot yet survive reload.

Set Phase 4 AWAITING_REVIEW and stop. Phase 5 remains LOCKED. Required missing
MariaDB/lifecycle evidence is BLOCKED, not an approving local-only shortcut.
