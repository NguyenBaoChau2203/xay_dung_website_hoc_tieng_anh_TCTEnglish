# 01 — Two Writing creation modes and honest offline fallback

Implementer: GPT 6.1 Sol High. IMPLEMENT_LOCAL only when invoked.
Read this pack's README for decisions/authority. Finish current recent-history
work before touching shared Writing files. Existing dirty work is the baseline.

Implementation status (2026-11-15): completed locally; see the exact contract and
verification ledger in `docs/feature-audits/2026-11-15-writing-ai-brief-hybrid/`.
The catalog-suggestion item is satisfied by not exposing that optional operation.
UI/browser binding remains step02; no paid or remote release stage ran.

## A. Establish contracts and regression baseline

- [x] Inspect StudyController.CreateWritingExerciseFromAi, IWritingService,
  WritingService.Generation.cs, WritingGenerationViewModels, WritingGenerationLog,
  private access/cleanup, current quota/clock and OpenAI transport/circuit.
- [x] Read symptom-relevant bug memory, especially unknown-outcome retry and
  the Writing/Premium generation tests. Do not apply Chat-specific rules blindly
  or turn the Chat/internal RAG provider into a free-form Writing generator.
- [x] Record the current API/status/role/day-boundary/limits and source-flow tests.
  Add meaningful red regressions for the new behavior before implementing it.

## B. Explicit request modes

Proposed evolution of the existing request; exact names must be fixed in the
backend handoff before the UI is coded:

| Field | Contract |
| --- | --- |
| mode | `from_text` (default for omitted legacy mode) or `from_brief`; reject unknown explicit values |
| sourceText | Full source only for from_text; preserve 20–4000-character limits and faithful alignment |
| brief | Description only for from_brief; proposed 20–1200 meaningful characters |
| level | Brief mode requires beginner/intermediate/advanced |
| contentType | Brief mode requires emails/diaries/essays/articles/stories/reports |
| length | Brief mode selects short/medium/long, with documented sentence bands 6–8/10–12/14–16 |
| tone | Brief mode selects an allowlisted neutral/friendly/formal register |
| idempotencyKey | Stable per logical operation, including retries and fallback confirmation |

Inactive fields are not silently used to infer a different mode. Validate allowed
combinations, Unicode/whitespace/length, conflicting structured versus free-text
requirements and bounded prompt size. Structured controls are the hard constraints;
surface obvious conflicts rather than quietly overriding them. Preserve role
policy: Standard/guest cannot create private premium exercises through fallback.

- [x] Keep the old source prompt semantically intact. Introduce a distinct
  brief-composition prompt that first authors coherent original content and
  returns full Vietnamese/English aligned sentences, metadata and paragraphs.
- [x] Treat user description/source as input data, not permission to change the
  system contract, reveal prompts/keys or call tools/URLs. No external retrieval
  is required to create a fictional practice scenario.
- [x] Reuse current OpenAI client/model settings; no global provider/model switch.
  Use one bounded logical deadline including existing transport retries, body
  reads and persistence. Do not multiply the current 90-second generation budget
  by adding an outer retry loop. Preserve cancellations and quota guards.
- [x] Validate both engines with the same typed structural checks: complete pairs,
  meaningful nonduplicate sentences, matching order, correct level/format, bounded
  counts/lengths/metadata and sensible paragraphs. Brief-selected level/type must
  not be silently replaced by existing enum fallback defaults.
- [x] Structural/length checks are not proof of semantic translation quality.
  Review representative language outputs and document unverified live AI quality.
  Do not make globally weaker assessment rules so malformed content can pass.

## C. Internal bilingual template engine

Implement a pure server-side generator and typed versioned catalog, for example
`Services/Writing/Generation/WritingTemplateCatalog.cs` and
`InternalWritingTemplateGenerator.cs`. These are proposed files, not existing APIs.
No second provider, HTTP service, Ollama, model downloads or extra AI requests.

Initial intent coverage, proposed for review:

| Template key | Topic | Format | Supported purpose |
| --- | --- | --- | --- |
| leave-request | work | emails | Politely request leave using explicit dates/name/reason choice |
| meeting-follow-up | work | emails | Summarize a meeting and agreed next steps |
| study-reflection | study | diaries | Reflect on a study session and next plan |
| transport-opinion | travel | essays | Present an opinion about public transport |
| study-habit | study | articles | Explain a practical learning habit |
| weekend-story | daily-life | stories | Tell a coherent fictional weekend story |
| community-cleanup | environment | reports | Report fictional community activity findings |

- [x] Provide reviewed beginner/intermediate/advanced versions of each initial
  intent:21 core language profiles covering all six formats. Advertise actual
  supported tone/length/slots per profile, not every theoretical combination.
  Initial short profiles can be 6–8 sentences; extra lengths need their own
  validated composition rules before being declared supported.
- [x] Use original curated bilingual phrase blocks and correct grammar/order.
  Variants are bounded reviewed alternatives, not random word replacement.
  Proper-name/date/number slots may be literal safe data with correct formatting.
  Arbitrary Vietnamese free text must not be pasted into an English sentence as
  a fake translation. Free-form reasons need supported choices or confirmation.
- [x] Return a typed coverage decision:Supported, NeedsTemplateConfirmation or
  Unsupported, including covered/missing constraints and actual capabilities.
  A keyword match only offers candidates. Unsupported topic/tone/length/detail
  never gets quietly mapped to an unrelated generic essay.
- [x] Build a fallback-confirmation step bound to current user, logical request,
  template version and normalized slot payload. Confirmed changed requirements
  are explicit, not a hidden rewrite of the original request. Revalidate at commit.
- [x] From-text outage cannot be solved by replacing the source with a template.
  Offer a clearly separate new template task or a public catalog suggestion,
  preserving the supplied text. Never claim an unsupported offline translation.
- [x] Catalog suggestion guard: no new suggestion/clone endpoint is implemented in step01.
  If added later, suggestions use only currently published public accessible content,
  respect filters where meaningful and contain no private lessons/answers.
  Choosing an existing lesson opens it; it does not silently clone it or report
  generation success. Template creation is a genuine new private exercise.
- [x] Typed source-managed template definitions have stable keys/versions and
  review tests. Existing public lesson packages remain unchanged. If the chosen
  implementation stores shared template entities, propose their typed delivery
  adapter first; no SQL dump or arbitrary-table importer.

## D. Failure policy and truthful origin

| Situation | Required behavior |
| --- | --- |
| Valid OpenAI result | Persist once, origin=openai; no template use |
| Circuit open, confirmed provider outage/rate/billing/config failure | Offer supported internal creation; distinguish operator issues from learner quota |
| AI returns malformed/incomplete/constraint-breaking content | Reject that content; supported template offer is allowed without claiming AI success |
| Invalid input, entitlement failure, application quota exhausted, cancellation or safety refusal | Preserve the reason; do not bypass it through templates |
| Unknown-after-dispatch/network timeout | Retain known/uncertain usage and application receipt; no blind OpenAI resend. Local fallback only after the owned generation attempt is terminal and exactly one application result can win |
| Database commit/response outcome uncertain | Recover the current operation/result first; never launch another creation as if nothing happened |
| Unsupported template requirements | Keep user's brief; show supported alternatives/explicit confirmation or catalog suggestions |

Separate inputMode, generationMethod=openai/internal_template, templateKey/version,
fallbackReason and support/capability state. Persist provenance for later private
cards/practice; never label template output "AI vừa sáng tác". Preserve old nullable
origin as legacy/unknown instead of fabricating history. Original generated
SourceType/access rules must remain compatible.

## E. Durable request, shared quota and private persistence

- [x] Replace in-memory-only correctness with durable user-scoped request receipt:
  unique `(UserId, IdempotencyKey)`, normalized payload hash and explicit state,
  linked result exercise, reserved quota/day and origin. Coordinate execution
  across independent processes and restart; memory caches remain optional only.
- [x] Include mode, all active constraints/fields and template version/slots in
  payload identity. Same key/same payload returns or recovers the same operation;
  same key/different payload returns conflict, not someone else's result.
- [x] One logical operation can go through AI failure → awaiting template choice
  → one template success without creating/counting two exercises. Repeated
  confirmation and a late response cannot both publish. Genuine cancellation
  stops work; terminal operations cannot be resurrected by a stale callback.
- [x] Use short reservation/claim and finalization transactions. Do not hold a
  database transaction or user-row lock across the OpenAI HTTP call. Use a bounded
  execution lease/fencing state and revalidate current account/access at commit,
  including concurrent anonymization, role change or cancellation.
- [x] Keep Premium's five successful private creations per documented current
  UTC day across **both modes and both engines**; retain current Admin policy.
  Do not silently introduce a UTC+7 reset change in this feature. Show local
  reset time accurately if the UI displays it. Count/reserve across instances.
- [x] Failure/no match/preview/awaiting choice/opening a catalog lesson does not
  consume a successful-creation slot. Reservations have bounded expiry/recovery
  and cannot block an account indefinitely. A replay consumes no extra slot.
  Application quota and provider token/billing accounting are separate.
- [x] Specify/test the quota-day assignment across UTC midnight and expired
  fallback offers. New receipts and legacy success logs must not count the same
  creation twice; unchanged offers cannot reserve a stale day's allowance forever.
- [x] Preserve known/uncertain OpenAI usage even if final output is a template;
  do not report the whole operation as zero provider usage after a timeout.
  Internal composition makes zero external calls, but the earlier AI attempt
  may have incurred cost. Tests must not claim exactly-once provider billing.
- [x] Save exercise, ordered sentence pairs, request result, provenance, success
  receipt and quota settlement in the appropriate atomic transaction. Exercises
  remain owned/private/unpublished and usable in current practice/assessment.
  Creating one grants no study progress, XP, streak, badge or recent visit until
  the appropriate separate existing event happens.
- [x] Protect read/recovery/confirmation APIs with real user ownership and
  antiforgery for writes. Derive identity on server; never trust user/engine flags
  sent by the browser. Preserve legacy success/status fields, and version new
  offer/recovery outcomes clearly in the handoff.
- [x] Keep raw prompts, keys, private text and slot PII out of operational logs
  and Git. Minimize persisted request data, define cleanup/retention, and extend
  actual account deletion/anonymization for new receipts/reservations.

## F. Migration, tests and handoff

- [x] Follow tct-ef-migration for request/provenance persistence. Ship additive
  migration+Designer+snapshot preserving current badge and recent-visit migrations.
  Inspect real current dirty source before generation; do not edit applied history.
- [x] Verify MariaDB 10.5.23 upgrade/repeat, FK/index behavior, multi-connection
  cap/replay races, cancellation/commit failure and preservation of current rows.
- [x] Test legacy from_text, brief prompt/constraints, both origins, all 21 internal
  core profiles/slots, unsupported cases, source-preservation, roles/CSRF/IDOR,
  five-slot shared quota, recovery after lost response/restart, same-key conflict,
  bounded timeout, refusal/cancel and zero-network internal composition.
- [x] Compare reward/attempt/progress rows before and after creation; verify
  access/assessment of resulting exercises through the current service contract.
- [x] Use fake OpenAI/provider fault injection for ordinary tests. No paid live
  generation, model downloads or hosting changes are authorized by this plan.
- [x] Run Release build, focused Writing generation/private/availability/quota/
  recent-history regressions and provider checks; full suite after integration
  when feasible. Preserve unrelated failures honestly rather than weakening tests.

Write `docs/feature-audits/2026-11-15-writing-ai-brief-hybrid/01-backend.md` and
`backend-contract.md`: actual routes, request/result examples, durable states,
capabilities, quota/day policy, origin fields, migration checks and limits.
Report in Vietnamese; UI work starts after that real contract is available.
