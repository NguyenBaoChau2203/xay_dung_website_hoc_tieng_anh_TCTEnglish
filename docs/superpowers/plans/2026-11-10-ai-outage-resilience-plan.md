# AI outage resilience and honest practice continuity

Date: 2026-11-10. Status updated 2026-11-11: local implementation and review
accepted. See the execution ledger for final checks and runtime limitations.
The owner now authorizes grouped commits, PR, merge to default `master`, and
staging monitoring. Original local-only instructions below are historical;
production remains disabled. Live publication evidence belongs to the final PR.
Implementation client selected by the owner: Cursor, **Grok 4.7 xhigh**.
Execution prompt: [Cursor handoff](../../../.ai/prompts/cursor-grok47-xhigh-ai-outage-resilience.md).

## 1. Outcome and authority

Keep learners moving through existing lessons when an external AI service fails.
Distinguish local evaluation, unassessed practice, genuine server assessment,
provider failure, and an uncertain submitted request. Preserve existing scores,
ownership, quotas, drafts, and completion credits.

This document plans the owner's accepted five improvements:

1. Speaking self-practice without automatic assessment.
2. Consistent, actionable AI availability messages.
3. Controlled outage and recovery verification.
4. A route from failed AI Writing generation to existing exercises.
5. A bounded process-local guard against repeatedly calling a failing provider.

The current task authorizes authoring this plan/prompt. The handoff is for local
implementation of these requirements when the owner gives it to Cursor. It does
not carry the previous UI integration's commit, push, PR, CI, merge, staging, or
production permissions into this new task. Keep production deployment off.

- Inspect the actual checkout before implementation. Planning baseline:
  `master`, commit `e6521ad34f423da468a6b9c3c51ea0b9245bf104`.
- If still on master, use `feature/ai-outage-resilience` for implementation;
  reuse it if it already owns this task. Preserve other branches and changes.
- Three pre-existing untracked BTH3 documents were present during planning.
  They are unrelated and must not be staged, overwritten, or deleted.
- Use one agent throughout. Apply review lenses inline.
- Do not edit `Program.cs`, appsettings files, project files, deployment workflows,
  hosting, keys, or release gates. This plan grants no protected-config authority.
- No database schema change is planned. Do not create a migration to persist
  availability, recording blobs, or self-practice counters.

## 2. Source evidence and limits

These are source observations, not evidence of a completed outage browser test.
Revalidate only the active step's contracts if the checkout has changed.

| Area | Verified starting behavior and source |
| --- | --- |
| Speaking assessment | `Services/Speaking/SpeakingAssessmentService.cs`, `AssessAsync`: an unavailable/not-ready/timeout transcriber returns 429/503/504 and `CanCommitProgress=false`. Successful text matching creates a server assessment. |
| Speaking progress | `Controllers/SpeakingController.cs`, `SaveSpeakingProgress`: needs a valid, owned, unexpired assessment; ignores client numeric scores; applies progress/credit atomically. |
| Browser recording | `wwwroot/js/speech-recorder.js`: `TctSpeechRecorder.createRecorder`, PCM16 mono 16 kHz WAV, maximum 15 seconds and payload bound, cancellation/resource cleanup. |
| Speaking UI | `wwwroot/js/speaking.js`: `assessAndCommitFromWav` currently submits a completed WAV automatically, then commits via `assessmentId`. Sentence/recording epoch guards already exist. Browser speech recognition is preview-only. |
| Writing assessment | `Services/WritingService.cs`: local policy first; optional feedback shares a 3-second budget. `Services/WritingAiEvaluationService.cs` returns no cloud feedback on provider/schema failure. |
| Writing status | `Services/Writing/WritingAssessmentPolicy.cs`: approved/exact answers and NeedsReview are distinct. Optional feedback must not turn NeedsReview into Accepted in Local mode. |
| Writing creation | `Services/WritingService.Generation.cs`, `CreateFromAiAsync`: provider-dependent creation, role/quota checks, replay handling, atomic persistence. No equivalent local generator exists. |
| Creation UI | `Views/Study/WritingExercises.cshtml`, `wwwroot/js/writing-exercises.js`: creation errors already show text, but there is no deliberate outage-to-existing-catalog flow. |
| Internal Chat | `Services/AI/Rag/AiChatAnswerService.cs` and `Services/AI/Internal/TemplateAnswerComposer.cs`: internal answers, current-user data and source excerpts can answer without a GPT generation call. |
| Hybrid Chat | `Services/AI/Rag/Corpus/HybridRetrievalService.cs`: failed embeddings can use `lexical_degraded`. `GroundedChatPipeline.cs` and `GroundedAnswerComposer.cs`: selector unavailable can produce ProviderUnavailable; do not manufacture an answer. |
| Provider transport | `Services/AI/OpenAiProviderClient.cs`: total deadline, bounded retries, error taxonomy and usage receipts. `OpenAiEmbeddingClient.cs` has its own admission/accounting; inspect it before wiring the guard. |
| Speech transport | `Services/Speaking/OpenAiSpeechTranscriber.cs`: typed failures; speech queue/admission and HTTP/body deadlines already exist. |
| Other consumers | `OpenAiConsumerPipelineTests.cs` covers vocabulary, Reading review, Listening quiz/translation, Writing and internal Chat. Shared text transport changes affect them even though their page redesign is outside this task. |
| Dependency setup | `Program.cs` already registers TimeProvider and typed/scoped clients. A new scoped guard instance would not protect across requests. Do not silently add a singleton registration to protected bootstrap. |

Relevant bug memory: `.ai/context/bug-fix-log.md`, sections on unknown Chat
transport outcomes (2026-11-03), atomic learning completion credits, and Speaking
UI lifecycle. `.ai/context/known-issues.md` records fixed draft-owner isolation
and NeedsReview reload handling. Preserve those fixes.

Unknown until implementation/verification: the current local port and running
process, real microphone permission on the demo laptop, actual browser rendering,
and current provider quota. A past successful request or HTTP 200 from an app
endpoint is not current GPT-call proof. Local fallback still needs the web app,
database and relevant media; it does not make the site available without internet.

## 3. Scope and file ownership

Read `AGENTS.md`, `DESIGN.md`, `docs/design-system/team-ui-workflow.md`, and the
active feature skills. Keep the accepted Claymorphism UI and laptop priority.

Expected existing files, paths relative to `TCTEnglish/`:

- Speaking: `Views/Speaking/Practice.cshtml`, `wwwroot/js/speaking.js`,
  `wwwroot/js/speech-recorder.js` only if the recorder contract needs an extension,
  `wwwroot/css/speaking.css`.
- Writing: `Views/Study/WritingExercises.cshtml`,
  `Views/Study/WritingPractice.cshtml`, `wwwroot/js/writing-exercises.js`,
  `wwwroot/js/writing.js`, `wwwroot/css/writing-exercises.css`,
  `wwwroot/css/writing.css`, corresponding typed ViewModels only when necessary.
- Chat: `Views/Ai/_ChatShell.cshtml`, `wwwroot/js/ai-chat.js`,
  `wwwroot/css/ai-chat.css`; inspect embedded/full-page shared consumers.
  Touch the launcher only if the new state actually crosses that boundary.
- Services: the provider, speech, Writing and grounded pipeline files listed in
  section 2, and existing error maps. Make controller changes only for required
  additive metadata/compatible error handling.

Proposed new files are design targets, not files that already exist:

- `Services/AI/Resilience/AiOutageGuard.cs`: small testable state machine/factory.
- `wwwroot/js/ai-availability.js`: optional shared presentation-only helper if it
  reduces duplication. It must never fetch, probe, retry, or own progress.
- Focused tests such as `TCTEnglish.Tests/AiOutageGuardTests.cs` and
  `scripts/tests/test_ai_outage_ui.cjs`; reuse existing test harnesses.

Do not change global theme/layouts to add a notice. Reuse existing state partials,
Bootstrap and tokens; if a shared helper/partial is changed, list its consumers
and prove existing states are preserved. No new package, CDN, local AI engine,
model download, provider replacement, or application-model change is needed.

Text remains `gpt-6-luna` / reasoning `low`; speech remains `gpt-transcribe`.
Grok 4.7 xhigh is the implementation model in Cursor, not a web application model.

## 4. Product decisions

### 4.1 Speaking modes and recording lifetime

Provide an explicit assessment choice inside pronunciation practice:

- **Automatic assessment:** retain the existing default behavior and server flow.
- **Self-practice:** record and play back locally; never call assessment/progress.

This is an assessment choice, not a replacement for the existing pronunciation /
dictation modes. Do not change dictation grading/progress rules incidentally.

Self-practice must be available before the first failed paid request. A failed
automatic request should offer switching to it, not silently upload again.
Keep one current-sentence recording in tab memory, with an accessible native
audio control or equivalent existing control. Stop playback before recording.
Keep the recorder's existing 15-second/audio-size limits.

- Do not invoke SpeechRecognition/webkitSpeechRecognition in self-practice.
  Browser recognition can depend on a remote service and is not an offline ASR.
- Do not upload or write a self-practice recording to localStorage, IndexedDB,
  service-worker cache, server files or database. Do not create a fake transcript.
- A self-practice recording never gets an assessmentId, score, pass, XP, badge,
  streak or completion credit. Display **Chưa chấm tự động**; use no score/0%.
- Preserve valid historical server scores in their existing location, clearly
  separate from the current unassessed recording. Do not mark a sentence done.
- On sentence/mode change, replacement recording, logout/navigation or disposal:
  cancel pending work; stop microphone tracks, audio and timers; revoke object
  URLs and drop buffers. Keep epoch + sentence + mode ownership checks.
- Mic denied/unsupported remains a microphone error; never turn it into an
  OpenAI outage. The learner can still use available lesson playback.
- Recovery is user-initiated. Returning to automatic mode requires a fresh
  recording/submission; do not replay retained audio automatically.

### 4.2 Availability presentation

Use feature-specific outcomes with a common vocabulary. A notice should contain
a concise title, factual explanation and useful action. Example Vietnamese copy
below is product reference data; adapt phrasing to the existing UI.

| Condition | Meaning and example action |
| --- | --- |
| Local Writing result | **Đã đánh giá bằng bộ kiểm tra nội bộ**. Show the actual result/NeedsReview and label optional cloud feedback only if it exists. |
| Internal Chat answer | **Trả lời từ dữ liệu trong hệ thống** when provenance establishes it. Never label internal success as a GPT call. |
| Self-practice | **Bạn vẫn có thể luyện và nghe lại. Bản thu này chưa được chấm tự động.** |
| Transient failure / open guard | **Dịch vụ đánh giá đang gián đoạn.** Offer self-practice, existing lessons or an explicit later retry as appropriate. |
| Provider key/access/quota problem | **Tính năng AI tạm thời chưa sẵn sàng.** Learner-facing text should not ask teammates to paste server keys or expose provider details. |
| Rate limit | **Bạn đang gửi yêu cầu quá nhanh.** Respect a valid retry-after value; do not imply every 429 is exhausted daily quota. |
| Unknown submitted Chat outcome | **Chưa xác nhận được kết quả. Hãy tải lại lịch sử trước khi gửi lại.** Never auto-submit through another transport. |
| App login expired / forbidden / not found | Preserve the authentication/access outcome. Do not relabel it as provider downtime. |

Preserve current JSON fields, HTTP codes, SignalR names, grounding statuses and
request/stream IDs. Add fields only when callers require them. Feature errors do
not all use the same envelope: Speaking uses error/code, Chat can use errorCode,
and Writing creation has its own envelope. Adapt them explicitly instead of
renaming contracts globally. Do not infer server-wide availability from one tab.

Use semantic status text and existing Clay tokens; no color-only state. Keep
keyboard focus stable; asynchronous messages should be announced once, without
duplicated screen-reader alerts. Keep the layout useful at laptop height.

### 4.3 Writing continuity

Retain the local assessment policy and its existing shared 3-second optional
feedback budget. API/schema/refusal/timeout failures must not change a Local
Accepted/NeedsRevision/NeedsReview decision, award fake completion, or lose input.
Keep local draft keys owner-scoped and historical status reloads consistent.

When AI creation fails, show **Chọn bài có sẵn** and keep the entered source text.
Build the destination from the existing catalog route and validated filter state
(level/content type/topic where supported). The action is navigation/filtering,
not a generated substitute exercise. If the catalog is empty, show a truthful
empty state. Preserve role restrictions and private-exercise access.

Do not automatically retry creation, choose an exercise, erase the form, or claim
the fallback article was generated from the user's source. Preserve the existing
idempotency behavior and report uncertain persisted outcomes honestly.

### 4.4 Chat continuity

Keep the deterministic/internal-answer path first. Preserve user/source access
rechecks, published/source freshness, ambiguity and insufficient-evidence states.
Retain lexical fallback when embedding is unavailable. A failed selector is
allowed to return ProviderUnavailable; do not fabricate a fluent answer or
broaden retrieval to unauthorized/private sources to hide the error.

Never add a new cloud-to-cloud fallback, an automatic SignalR-to-HTTP replay after
dispatch, or a freeform generation path. Preserve all known/unknown usage and
quota settlement behavior, including reply-persistence failure and recovery.

### 4.5 Repeated-failure guard

Implement a minimal circuit-style guard after the continuity paths work. It is
an optimization across requests, not a replacement for existing retries,
timeouts, cancellation, speech admission, or per-user limits.

- Use independent families for text generation, embeddings and speech. Within a
  family, isolate different endpoint/model/credential authorities so one failing
  configuration does not poison another. Never log/store a raw credential as a
  registry key; any opaque discriminator stays process-local and non-serialized.
- Share state across scoped/typed clients in the same app process. With current
  protected bootstrap, a small bounded process-local default registry plus an
  optional explicit test seam is acceptable. Keep the state machine separate
  from the registry, inject its clock for tests, and prevent cross-test leakage.
  Do not edit Program.cs to register a new service. If another architecture is
  chosen, it must satisfy these constraints without hidden bootstrap work.
- Proposed defaults: three consecutive completed transient logical failures open
  the affected circuit for 30 seconds. Reset the failure count on success.
  A provider Retry-After may extend that interval within a documented cap of
  120 seconds. Do not count every internal retry as a separate logical failure.
- While open, return a typed unavailable/circuit-open outcome before HTTP send.
  No sleeps, background probe, startup provider call, or browser polling.
- After cooldown, exactly one user-initiated request probes that family/key;
  concurrent callers fail fast. Probe success closes; transient failure reopens.
  Cancellation/validation failure must release the probe lease and not deadlock.
  A nontransient probe result must release the lease and end transient-outage
  suppression while retaining its ordinary error; never keep a stuck probe.
- Count provider 5xx, internal transport timeout/network failure, and temporary
  provider rate limiting. Do not count caller cancellation, local admission,
  validation/authorization, malformed user input, missing key, authentication,
  exhausted billing quota or policy refusal as a transient provider outage.
  Speech currently maps several conditions to engine_busy: distinguish local
  admission/queue rejection from an actual provider 429 before counting. Inspect
  bounded provider error metadata when separating rate limits from billing quota;
  never expose response bodies or change existing public errors unnecessarily.
- Do not add retries to user actions. Existing retry deadlines and uncertain
  transport outcomes remain authoritative. A rejected local guard is known
  **not dispatched**; a lost response after dispatch remains **uncertain**.
- Chat accounting needs explicit integration: a pre-dispatch guard rejection
  must clear only that stage's provisional unknown allowance and add no invented
  usage. Preserve usage already recorded by earlier stages. Audit the specific
  catch branches in GroundedChatPipeline, embeddings and quota journals before
  adding a new error code; the existing general catch can conservatively record
  unknown usage and is not automatically correct for a local guard rejection.
- Keep readiness based on the app/database, not the external provider. Restart
  resets a process-local guard; multiple IIS processes do not share it. Record
  this limitation rather than claiming a distributed circuit breaker.

## 5. Ordered implementation checklist

Complete each phase inline. Update the execution ledger as work progresses;
reconcile this checklist with current source, not an old model's report.

### Phase 0 — Baseline and contract map

- [ ] Inspect branch/HEAD/diff, preserve unrelated documents, select task branch.
- [ ] Create `docs/feature-audits/2026-11-10-ai-outage-resilience-execution-ledger.md`.
- [ ] Record actual routes, script order, envelopes and recording lifecycle.
- [ ] Inspect existing tests before extending them. Read the symptom-relevant
  bug-memory sections listed above. Record current shared consumers.
- [ ] Run focused baseline tests below; diagnose existing failures separately.

### Phase 1 — State contract and Speaking self-practice (P1)

- [ ] Define the minimal presentation/state model and transitions in the ledger.
- [ ] Add failing actual-script tests for manual recording/no POST, mode changes,
  stale completions, object URL cleanup, mic errors and failed-assessment recovery.
- [ ] Implement the explicit assessment choice and current-recording playback.
- [ ] Preserve automatic assessment/progress and historical score rendering.
- [ ] Keep all transient recording state in memory; no new persistence model.
- [ ] Verify manual practice causes zero assess/progress requests and zero reward
  mutations, including after automatic failure and delayed callbacks.

### Phase 2 — Common notices and Writing/Chat integration (P1)

- [ ] Add/extend a presentation helper only if useful; no network logic in it.
- [ ] Normalize supported error inputs at each feature boundary, keeping existing
  contracts. Test non-JSON responses and unauthenticated redirects explicitly.
- [ ] Label actual local/cloud provenance and NeedsReview correctly in Writing.
- [ ] Preserve internal and lexical Chat answers, source states, full/embed
  consistency, unknown-outcome/history recovery and disabled-send restoration.
- [ ] Add **Chọn bài có sẵn** to failed Writing creation, keep input/filters and
  navigate to the actual published catalog. Test empty and access-restricted data.

### Phase 3 — Provider guard and accounting (P2, included in scope)

- [ ] Write failing state-machine tests using a fake clock, no wall-clock sleeps.
- [ ] Implement the bounded process-local registry and isolated policy state.
- [ ] Wire text, embedding and speech boundaries without bootstrap/config edits.
- [ ] Add tests proving fail-fast/no HTTP, one probe, recovery, family/key
  isolation, cross-request persistence and deterministic test isolation.
- [ ] Test known-unsent versus unknown-sent accounting in the grounded pipeline;
  preserve receipts, quotas, leases, cancel handling and stale source checks.
- [ ] Run the other provider-consumer tests: vocabulary, Reading review, Listening
  quiz/translation and Writing generation. No redesign of teammates' pages.

### Phase 4 — Controlled outage and browser acceptance (P1)

- [ ] Use test-only fake HTTP/transcriber/provider implementations and browser
  route interception against the local app. Never add a publicly reachable fault
  route, diagnostic header, query switch or key-revealing status endpoint.
- [ ] Check every row of the acceptance matrix in section 6, recording actual
  assertions, provider call counts, state/data checks and unobserved cases.
- [ ] Verify the real local pages and interactions at desktop/laptop first,
  then 768 and 390 pixels, 200% zoom, keyboard and reduced motion.
- [ ] Check console errors, request counts, retained input, stopped microphone,
  URL cleanup, score provenance and access isolation; screenshots alone do not
  establish these properties.
- [ ] Keep staging untouched. Use synthetic local data; no paid API or real user
  recordings are required for deterministic acceptance. Real mic/provider tests
  remain separate owner opt-ins and must be reported separately.

### Phase 5 — Integration verification and handoff

- [ ] Run focused tests, full Node suite, Release build/full relevant .NET suite.
- [ ] Check pending model changes with the existing provider-safe EF procedure;
  do not run migrations against production or reset the developer's Docker.
- [ ] Review the diff inline for access, antiforgery, privacy, lifecycle, accounting
  and shared-consumer regressions. Fix in-scope findings and recheck affected tests.
- [ ] Update relevant operations docs and append bug memory only for actual fixes.
- [ ] Record evidence/limitations, leave changes local for the owner's review.

Dependencies: Phase 0 precedes all work; Phase 1's state contract feeds Phase 2.
Phase 3 follows the established failure semantics. Phase 4 is prepared throughout
and completed after Phase 3. Do not defer all regression coverage to the end.

## 6. Acceptance matrix

| Case | Required observable outcome |
| --- | --- |
| Missing/unusable key | Core site/ready remains usable with a healthy DB; no outbound AI call; useful per-feature fallback, no secret disclosure. |
| Self-practice before any API failure | PCM recording and playback work; zero SpeechRecognition/assess/progress requests; no completion/XP changes. |
| Self-practice resources | Replace, sentence switch, pronunciation/dictation switch, navigation/dispose revoke URLs, stop tracks/timers and reject old callbacks. |
| Mic denied/unsupported | Accurate mic/browser message and usable lesson playback; no fake provider failure or recorder retry loop. |
| Automatic speech 401/403, 429, 5xx, timeout, invalid body | Compatible failure, controls recover, current recording can be played where safe; no progress mutation or fabricated transcript/score. |
| NoSpeech | Distinct from downtime and manual mode; no progress commit; previously earned score is not replaced by 0. |
| Automatic speech success/replay | Uses real server assessment; records progress once; completion/credit replay remains idempotent. |
| Speech assessment/progress ownership | Foreign/private/expired/consumed assessment cannot award progress; CSRF/access responses remain unchanged. |
| Local Writing under cloud outage/3s timeout | Accepted/NeedsRevision/NeedsReview, draft, history and rewards agree before/after reload; no optional output changes pass. |
| Writing generation failure | Original text remains, no partial exercise/log success; catalog navigation preserves valid filters and honors publication/access. |
| Writing catalog empty | Truthful empty state; no invented replacement lesson or automatic AI retry. |
| Internal Chat without provider | Stored data/guide queries still return authorized factual answers; success is not labeled a GPT call. |
| Embedding failure | Lexical fallback remains scoped and source-current; unavailable index or missing evidence is reported truthfully. |
| Selector failure | ProviderUnavailable/clarification as appropriate; no fabricated source answer. |
| Unknown Chat transport outcome | No automatic resend over HTTP/SignalR; history recovery available; no falsely free/unprocessed label. |
| Circuit opens | Three qualifying final failures; next request sends no HTTP; text/embedding/speech and separate authorities remain isolated. |
| Circuit recovery/concurrency | Fake clock advances cooldown; one admitted probe; others fail fast; success recovers without user reload requirement or background paid requests. |
| Circuit accounting | Pre-dispatch block adds zero provider attempts/usage, releases only unsent stage allowance; earlier known/uncertain usage survives. |
| Caller cancel / app auth failure | Does not count as provider failure; no auto retry, leaked lease, pending spinner or stale UI update. |
| Other provider consumers | Vocabulary/Reading/Listening contracts and data preservation remain green under text transport changes. |
| Privacy | No raw keys, request/audio bodies or private lessons in logs/notices/Git; recordings remain tab-memory only. |

Cover success, fallback, recovery and cancellation transitions, not just static
strings. Extend actual JS/HTTP/service harnesses; do not copy implementation
logic into tests and call that a regression test.

## 7. Verification commands and evidence

Baseline focused .NET command (PowerShell; adapt the filter only after discovery):

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter 'FullyQualifiedName~SpeakingAssessment|FullyQualifiedName~OpenAiSpeech|FullyQualifiedName~WritingLocal|FullyQualifiedName~WritingOptional|FullyQualifiedName~PremiumWritingGeneration|FullyQualifiedName~OpenAiConsumerPipeline|FullyQualifiedName~AiRagHybridRetrieval|FullyQualifiedName~AiRagPhase4|FullyQualifiedName~AiRagPhase5|FullyQualifiedName~AiQuotaReservation|FullyQualifiedName~OpenAiProviderClient'
```

This filter can select live tests which correctly skip without their opt-in;
never enable those flags to make a count appear complete. Add the new guard/test
classes to the focused filter. Run focused Node scripts as each feature changes.

Final integration checks:

```powershell
node --test scripts/tests/*.cjs
dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release
git diff --check
```

The solution filename above was verified during planning; recheck it if the
checkout changes. A docs-only authoring pass does not require these app builds.
Use the repo-local EF command/procedure if checking pending model changes; record
the exact invocation and database/provider boundary. Do not guess an EF command
that could migrate the normal local database. SQLite tests do not establish
MariaDB compatibility for a new schema or database-query change.

For local browser testing:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

Respect an existing listener. Use a free port with `-Configuration Release
-WebPort <free-port>` rather than terminating an unowned process. Saved keys are
read automatically; no rewriting User Secrets. Do not log key values. Check
`/health/ready`; `scripts/speech-status.ps1` describes configuration, not a live
provider result. Startup must not make a paid speech/text probe.

Record commands, pass/fail/skip counts, actual-script tests, browser routes,
roles, viewport/state, network call counts and disposable data assertions in the
ledger. Do not change staging keys/settings to create outage evidence. A local
fake-provider pass is not a staging outage or paid-provider acceptance pass.

## 8. Deliverables and definition of done

- Working local Speaking self-practice, truthful per-feature notices, Writing
  catalog alternative and bounded provider guard with actual regressions.
- Execution ledger at the path in Phase 0, with concise decisions, shared-impact
  consumers, baseline/final evidence, skipped checks and remaining limits.
- Update `docs/operations/local-speech.md` and relevant local AI documentation
  with the user-visible fallback/recovery flow; do not duplicate this entire plan.
- No new entity/schema, content package, protected config or runtime credential
  change is expected. If approved shared content is actually changed, follow the
  export/package workflow; do not export personal data for this task.
- No unrelated BTH3/theme/teammate-page changes and no unrequested Git/release work.
- All required local checks pass or the task is explicitly reported incomplete
  with the blocking check and evidence. Skipped/unobserved cases stay explicit.
- Vietnamese handoff: behavior by feature, files, checks, risks, local/staging/
  production separation, and a short sequence the owner can use to test the UI.

Owner review can authorize later commit/PR/merge and a staging release. Those
stages are not part of this local implementation handoff. Production stays off.
