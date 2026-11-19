# AI voice conversation implementation ledger

Date: 2026-11-18. Mode: IMPLEMENT_LOCAL. One agent; no release authority.
Branch: `feature/ai-voice-conversation`; baseline `e6be0e140ed73b9610cee9495934e82046b5b073`.
The three untracked planning documents predate implementation and are preserved.

## P0 — decisions and evidence in progress

- Use the pinned OpenAI example `1215a24fb4725742c7b88094812b7ba18c46041c`.
  Downloaded reference source into ignored `.appdata/local/ai-voice-upstream`.
  Only selected adapted sources/tests and MIT notice will be retained in `third_party`.
- Adapt audio helper, coordinator, SDP policy, readiness, event projection and
  session cleanup patterns. New TCT code supplies DB ownership, durable lease,
  accounting, catalog integration and private history. No audio engine is added.
- Use built-in .NET HttpClient/ClientWebSocket. SDK unified-call/sideband fit is
  unproven; no NuGet, Node server or frontend framework is proposed.
- Protocol: Realtime GA, multipart `POST /v1/realtime/calls`, validated Location,
  `wss://api.openai.com/v1/realtime?call_id=...`, session.update/updated and
  `POST /v1/realtime/calls/{id}/hangup`. GPT-Live and Realtime beta are excluded.
- Official sources opened: [WebRTC](https://developers.openai.com/api/docs/guides/voice-webrtc?api=realtime),
  [server controls](https://developers.openai.com/api/docs/guides/voice-server-controls?api=realtime),
  [VAD](https://developers.openai.com/api/docs/guides/realtime-vad),
  [hangup](https://developers.openai.com/api/reference/resources/realtime/subresources/calls/methods/hangup).
  Pages contain both Live and Realtime sections; only the Realtime sections apply.
- Freeze HTTP routes/DTO fields from plan section 4.3. Offer/answer max 256 KiB
  UTF-8; exactly one active audio media, no data channel/video/extra media.
- Reversible v1 choices: English conversation, Vietnamese UI, finalized private
  transcripts without raw audio; interrupted assistant text explicitly marked.
  Standard 180 seconds/day/session; Premium 900 seconds/day, 600/session;
  one active lease/user, finite global capacity. Defaults remain disabled.
  These limits do not authorize API spend or enable availability.
- Preserve existing AI text model and speech key resolver. No secret writes.
- Hosting requirements remain unverified: HTTPS, outbound HTTPS/WSS, unbuffered
  same-origin SSE, continuously running worker and restart recovery. No Plesk edits.
- Model/reasoning selection is owner-requested Sol 6.1 High; this tool session
  has no capability to verify or change its coding-model selection.

## Protected-file proposal (owner approved)

Only proposed change to `TCTEnglish/Program.cs`, after existing AI options binding:

```csharp
TCTEnglish.Services.AI.Voice.AiVoiceServiceCollectionExtensions
    .AddAiVoiceConversation(builder.Services);
```

The extension will register scoped entitlement/topic/quota/session/history services,
singleton resource registry and provider transport, `AI:Voice` options and cleanup
hosted service; reuse existing TimeProvider, scope factory and credential resolver.
The dedicated named HttpClient disables redirects and HTTP logging. The worker
must never create a provider call and must not contact the provider on an ordinary
disabled startup with no pending cleanup. Pending hangups remain recoverable.
No `appsettings.json` or `.csproj` change is needed. No package is added.
Owner explicitly approved this exact Program.cs change during implementation.
Applied one registration line. appsettings.json and all .csproj files are untouched.

## Phase status

| Phase | Status | Evidence / blocker |
| --- | --- | --- |
| P0 | Offline implementation verified | Pinned MIT source, fake HTTP/readiness, effective config validation; owner approved one Program.cs registration. Live compatibility unverified. |
| P1 | Local implementation/provider evidence | Additive migration; entitlement, lease/quota, UTC and account scrub tests; MariaDB 10.5.23 upgrade/repeat pass. |
| P2 | Local implementation | Typed catalog registry, guarded stable-key seed, six exported immutable v1 packages; Validate/Sync pass. Export/import/conflict regression passes. |
| P3 | Local implementation/fake evidence | Create/ACK/SSE, final transcript/usage dedupe, retained pending cleanup, lifecycle/API tests. Real protocol/account access not tested. |
| P4 | Local implementation/fake media evidence | Two upstream helpers adapted; 16 node behavior tests pass. No real microphone/audio acceptance. |
| P5 | Local UI with partial browser acceptance | Actual disabled page/catalog plus fake conversation/CRUD/history across widths; exact zoom/real role matrix still unobserved. |
| P6 | Local implementation/provider evidence | Account deletion scrubs private content and retains cleanup; recovery/no-new-call and provider proofs; regression rerun below. |
| P7 | Permitted local review/verification finished | Broad offline regression, pending model, package and browser checks below. Paid/live and hosting gates stay blocked; exact zoom/full real-role matrix not certified. |

### P1 evidence

- Additive `20261118132210_AddAiVoiceConversation`, Designer and snapshot generated
  using repo EF 9.0.4. Eight new tables; no old table/column changes in Up.
- Six entitlement tests pass (Standard/Admin/legacy/expired/revoked).
- Two persistence tests pass (Stop before reserve in a new scope; unknown outcome
  retains reservation/lease, confirmed settle is idempotent).
- `scripts/verify-mariadb.ps1 -Container tct-migration-check-voice -Database tct_migration_check`:
  exit 0. MariaDB 10.5.23 disposable at loopback port 33317; baseline upgrade,
  baseline user values preserved, repeat migration unchanged; 27 migrations.
  No dev/hosting DB reset. Test password generated into ignored env file, not logged.
- Quota decision refined: dispatched calls consume the full reserved window
  conservatively on confirmed closure; time allowance is not a provider invoice.
  Pending/unknown outcomes are never refunded automatically. UTC boundary caps a
  new reservation at midnight. Audio/text token details remain separate counters.
- Domain suite initially hung; isolated tests pass after removing recursive
  serialization of computed topic instructions (JsonIgnore). Full rerun pending.

### P0 protocol/model refinement

- Official [gpt-realtime model page](https://developers.openai.com/api/docs/models/gpt-realtime)
  marks the old alias deprecated. Chose `gpt-realtime-2.1` for the disabled default,
  documented in the current [model page](https://developers.openai.com/api/docs/models/gpt-realtime-2.1).
  No paid call/account availability proof. Input transcript uses `gpt-transcribe`,
  allowed by the [Realtime session schema](https://developers.openai.com/api/reference/resources/realtime/client-events).
- Creation includes server model. Sideband update omits immutable model; the
  session.updated ACK must still confirm its effective value and all requested
  settings (no tools, instructions, VAD, interruption, voice and transcript model).
- Explicit provider 4xx rejection except 408 differs from unknown dispatch/5xx;
  no successful call is inferred from timeouts. If call-ID persistence fails,
  adapter attempts bounded hangup; simultaneous DB/hangup failure remains an
  operator reconciliation limitation, never a claimed refund.
- All 18 retained upstream files/license have SHA-256 matching provenance.json.

### P2 delivery

- Seeded six public catalog roots in existing Docker without overwriting rows.
- Status command exit 0. Export IDs 1–6 to `team.voice.{introductions,cafe,travel,
  daily-routine,hobbies,interview}` v1: all exit 0. Reviewed all six packages:
  one AiVoiceCatalogTopic each, no owners/references/private records/assets.
- `shared-data.ps1 -Action Validate -SkipBuild`: exit 0, 386 immutable releases,
  17,914 record revisions. `-Action Sync -DatabaseReady -SkipBuild`: exit 0,
  6 unchanged voice roots; personal data preserved. Fresh last-sync receipt at
  `2026-11-18T13:48:27.5850524Z`, catalog SHA-256
  `b059a2da53547989a0c2dfc8e53cc3b0e013307d3c53b3f45a32cf5117302b6a`.
- Typed export/import/rerun/conflict regression passes using isolated SQLite;
  source seed rerun preserves local edits; private kinds are rejected.

### P3/P6 tests and fixes found during implementation

- HTTP tests exercise antiforgery, same-origin, stale Premium cookie, other-owner
  topic mutation/history/SSE/Stop/Start, and explicit premium_required responses.
- Fake lifecycle verifies Stop during provider creation, sideband setup failure,
  retained failed hangup/retry, deduped final text and separate audio/text tokens.
- Added tests for recovery with an old durable heartbeat and Stop during audio
  playback. Recovery regression initially failed (expected one hangup, actual zero):
  transition to closing inherited a future setup cleanup timestamp. Fixed by
  scheduling the first cleanup immediately while preserving retry backoff.
- Added durable five-reservations/minute bound before provider I/O; foreign UUID
  Start now returns 404. Capacity, lease and quota mutation share one DB lock.
- MariaDB domain evidence: independent connections admit one lease; UTC crossing
  keeps the original reservation; account scrub removes private content and
  allows user deletion while preserving call ID. Three passed in initial run.

### P4/P5 browser and JS evidence

- 16 node fake-media/coordinator cases pass; combined existing chat submit/history/
  outage and speech-recorder regression: 40 passed, 0 failed/skipped.
- Browser helper/coordinator retain source copyright/license and are formatted
  vanilla JS. No new frontend packages/frameworks or service added to the app.
- Playwright already cached on the machine was used for local Chrome QA; no
  dependency files were changed. Browser script/artifacts remain ignored under
  `.appdata/local/ai-voice-tests`. Saved local login was consumed privately, never
  printed. Chrome MCP sourcePath rejected its workspace root, so terminal-based
  Playwright was used instead. A temporary login script was removed.
- Actual server: authenticated disabled feature page, six catalog topics,
  one h1 and disabled Start verified. `/health/ready` 200; speech configured,
  VerifiedLive=false. No API activation or paid calls.
- Fake API/media: Start, speaking state, mute, Stop (tracks/peer closed once),
  interrupted transcript, private topic create/edit/delete, history read/delete,
  mic refusal with no create request, Premium lock, quota error and empty catalog.
- Widths: 1440x1000, 768x1024, 390x844, 375x812, 720x500 reflow approximation.
  First browser run found mobile controls below 40px due to legacy 14px root;
  fixed min touch geometry at 44px. Later run: 10 checks, no page errors/overflow.
- First sidebar-collapsed screenshot used the wrong toggle selector; rerun with
  actual `menuToggleBtn` is required before claiming collapsed acceptance.
- Exact 200% browser zoom, real audio/interruption latency, real Standard/Premium/
  expired browser sessions, real network/ICE behavior, contrast/forced colors and
  complete keyboard/modal matrix are not yet certified.

### Local verification limitations and reruns

- Initial web launch overlapped shared-data Status and hit the sync.lock guard;
  retried after Status completed. No lock/trust bypass. Healthy launch then passed.
- A build while task-owned web PID 6400 was running hit MSB3021/3027 on Windows.
  Verified this was the task's 5107 listener, stopped it and reran successfully.
- Broad offline regression after recovery fix: 211 passed, 0 failed, 7 skipped
  (four isolated voice MariaDB tests and three existing shared-data MariaDB tests).
  Further focused additions/reruns are recorded at final handoff.
- EF pending-model check: exit 0, no pending model changes.
- No commit, push, PR, remote CI, merge, staging or production work.

### Additional review corrections

- A meaningful regression reproduced double settlement after a late setup
  continuation changed an already-accounted session's state: expected 180 seconds,
  observed 360. Settlement now guards AccountingState independently of lifecycle
  State, setup failure preserves closed state, and activation rechecks cancellation
  after attaching the sideband resource. The retained lease/reservation tests pass.
- Maximum Vietnamese topic (2,000 characters) reproduced escaped snapshot overflow.
  Storage uses compact Unicode JSON with an explicit 6,000-character preflight;
  no truncation, schema change or unencoded HTML rendering is introduced.
- Deleting an active private topic now writes durable cancellation and stops its
  local sideband; the worker handles hangup. A failing HTTP regression preceded
  this fix. Custom Start rechecks topic access/deletion before dispatch.
- Factory boundary now tests the .NET sideband connection URI/private credential,
  mutable session.update payload, unrelated ACK rejection and effective ACK.
- Expanded MariaDB upgrade fixture initially compared JSON DateTime.Kind (Z versus
  no Z), which MariaDB DATETIME does not retain. Compare persisted timestamp ticks
  instead; this is a test correction, not a production time mapping change.

### Final provider and regression results

- Latest broad offline test command (Release, filtered voice/Premium/SharedData/
  account deletion/text history/cancel/RAG boundary/UI resilience/baseline):
  **216 passed, 0 failed, 7 skipped, 223 total**, exit 0. Seven skips are four voice
  disposable-provider tests plus three pre-existing shared-data MariaDB tests.
- The four voice provider checks were then run explicitly against this task's
  MariaDB 10.5.23 container. Upgrade/preservation/public-package repeat/Unicode
  check **passed** in `ai-voice-mariadb-final.log`; all five final HTTP security
  tests also passed in that invocation. Two other tests hit finite global capacity
  because earlier failed-run fixtures were intentionally retained. This was test
  isolation, not a production quota failure. Test-only capacity is now 100 for
  per-user proofs, and newly created synthetic reservations are settled by the
  test itself; earlier rows are not reset. The three quota/lifecycle provider
  cases then **3 passed, 0 failed**, exit 0, in `ai-voice-mariadb-domain-final.log`.
  No failed invocation is counted as a full-suite pass.
- Provider upgrade proof creates a new database inside the guarded disposable
  container, migrates to the prior feature baseline, inserts synthetic user/chat/
  subscription, upgrades twice, verifies preserved fields/timestamps and imports
  six public packages twice with identical IDs. Maximum Vietnamese scenario is
  also stored and round-tripped on MariaDB. No SQLite substitution for that proof.
- Disposable container stopped after tests; retained for inspection. Existing
  Docker dev DB and all pre-existing rows were preserved.
- Final standalone Release build: exit 0, 0 errors, 24 existing warnings outside
  the new feature files. No appsettings.json or .csproj change.
- Final node chat/voice/recorder suite: **40 passed, 0 failed/skipped**; syntax checks
  for all three voice scripts exit 0. Git whitespace and shared immutable-history
  check exit 0. EF model remains aligned with the generated migration.

### Shared UI impact review

- `_ChatShell`: full `Views/Ai/Chat.cshtml` and `ChatEmbed.cshtml` consumers.
  Added a navigation link; existing chat IDs, selectors, forms and scripts kept.
- `_Layout`: learner-shell consumers. Existing full chat already suppresses its
  floating launcher. New Voice route now does the same because the floating
  launcher obscured Save on a low mobile viewport. Source condition changes only
  AiVoice; Home/other learner routes keep the launcher, full Chat stays suppressed,
  embedded layout remains independent. No shared CSS/token/ID changed.
- Browser revealed a second low-viewport issue: modal form wrapping prevented
  the Bootstrap scrollable body from shrinking; Save was below a 390x500 viewport
  (bottom 618px). Scoped flex form fixes it without changing shared modal styles.
- Modal shown/hidden events focus the title and restore the initiating control.
  Focus assertions wait for completed Bootstrap transitions; geometry alone is
  not accepted as proof that the Save button is unobscured.

### Final browser handoff

- Final browser script exit 0: **12 checks, 0 page errors**. Two create requests
  were intercepted fake requests (successful sample and quota error); no paid
  request was issued. Script uses fake microphone/peer/EventSource and API routing
  for enabled conversation states; the actual server remains disabled.
- Confirmed 1440 sidebar expanded and genuinely collapsed via menuToggleBtn,
  768, 390 and 375 widths without horizontal overflow; 720x500 only approximates
  200% reflow. Native browser zoom 200% remains NOT OBSERVED.
- Low-height modal at 390x500 now keeps Save inside the viewport and its center
  hit-test reaches the Save button. Tab remains in the modal, Escape closes it,
  and focus returns to Create after the hidden event. Screenshot inspected.
- Actual full/embedded chat still renders chatWindow and voice navigation with
  correct _self/_blank target. Home still renders the global launcher; Voice
  does not render it. No launcher IDs/selectors or shared token CSS were changed.
- Browser artifacts: `.appdata/local/ai-voice-tests/{browser-evidence.json,
  actual-disabled.png,desktop-open.png,desktop-collapsed.png,tablet.png,mobile.png,
  small-mobile.png,mobile-modal.png,chat-page.png,chat-embed.png}`. These are ignored
  local evidence, not runtime recordings or published assets.
- Local web is left running at `http://localhost:5107/AI/Voice`, health 200.
  Latest startup shared sync receipt: `2026-11-18T14:24:35.5991332Z`, same reviewed
  catalog SHA-256 listed above. Saved speech configuration still does not prove
  account access. No live mic, paid voice, CI, staging or production acceptance.
- Remaining unobserved matrix: exact native 200% zoom, forced-color/contrast audit,
  full real Standard/Premium/expired browser roles, physical mic/permission changes,
  real ICE/network/provider failures and measured interruption/latency. HTTP/domain
  and fake behavior evidence is recorded separately and does not certify these.

### Owner-authorized local voice preview — 2026-11-18

- Owner asked why Start remained disabled and then explicitly approved live local
  testing (reply: "cho phép thoải mái") to the proposed USD 1 / total 60-second
  trial. This authorizes enabling the local preview; no release authority changes.
- Restarted only this task's verified 5107 web executable. Its child-process
  environment sets AI:Voice:Enabled=true, Standard/Premium daily and session
  allowances to 60 seconds, MaxConcurrentSessions=1. Source defaults stay disabled;
  no protected configuration, User Secrets, credentials or dev rows are rewritten.
- No automatic call or microphone submission: the owner initiates through Start.
  The UI capability/button check does not call the session-create API. Paid call,
  model entitlement and actual audio remain unverified until owner interaction.
- USD 1 is the authorized trial budget, not a provider-enforced monetary cap;
  the direct-WebRTC orphan/unknown-outcome limitations documented above still apply.
- Verification exit 0: health 200; real authenticated topics API available=true,
  dailySeconds=60, used/reserved=0; browser Start enabled, Mute/Stop disabled before
  connection, secureContext=true, six topics. Zero session-create requests and no
  microphone submission. Ignored evidence: `live-enabled-evidence.json` and
  `live-enabled.png` under `.appdata/local/ai-voice-tests`.

### Owner-reported Stop pending state — 2026-11-18

- Symptom: Stop showed a red cleanup-pending message and required another click.
  Safe lifecycle metadata showed the owner's one call already `closed/settled`,
  with one hangup attempt and no pending hangup before diagnostic retries.
  Authenticated close probes twice returned HTTP 200 with `closed:true`.
  The original browser response was not captured; concurrent cleanup is a
  reproduced cause of HTTP 202, rather than a claimed recording of that request.
- Root cause verified in source/tests: the page threw immediately on the valid
  HTTP 202 `{closed:false,cleanupPending:true}` response. No follow-up check ran
  after the worker completed. Separately, repeated Stop set NeedsHangup back to
  true even for a closed/settled call; the worker skips closed rows.
- Fix: use bounded confirmation of the same session ID (one-second spacing,
  30-second total wait budget, each request bounded by remaining time/15 seconds).
  Pending is informational; microphone/peer close immediately, Start stays blocked,
  confirmation clears the notice and refreshes allowance/history. Timeout or
  transport failure retains the same attempt for manual Stop retry. Repeated
  cancellation of a closed call leaves NeedsHangup=false and never repeats hangup.
- Red/green: the repeated-Stop .NET regression failed with NeedsHangup=true before
  the fix. Three added JavaScript behavior tests failed before the helper existed.
  Final focused .NET run: **54 passed, 0 failed, 4 MariaDB tests skipped**, exit 0;
  existing warnings remain. No schema change in this follow-up. Node voice suite:
  **19 passed, 0 failed/skipped**, syntax checks and git diff --check exit 0.
  Existing chat/outage/recorder Node regressions also pass: **24 passed**, exit 0
  (the microphone-denial case emits its expected simulated not-allowed error).
- Browser: five checks passed at 1440 and 375 widths, zero page errors. Intercepted
  responses 202/202/200 returned the UI to ready without another click; microphone
  was already stopped during pending; allowance refreshed; a shortened test-only
  deadline retained the attempt and manual retry confirmed closure. Two fake
  session-create requests, zero real creates or paid calls from these checks.
  Screenshots inspected; ignored evidence in `stop-browser-evidence.json`,
  `stop-pending.png`, `stop-confirmed.png`. Browser initially ran before startup
  finished and received connection refused; rerun after listening passed.
- Restarted only the verified task-owned 5107 web process through start-local.ps1,
  retaining the authorized child-only 60-second preview configuration. Launcher
  build succeeded, migration reported up to date, readiness HTTP 200. Actual
  authenticated closed-session probes again returned 200/closed twice, usage
  60/60 and reserved=0. No quota reset/refund, credential edit or new paid create.
  The full reserved 60-second allowance was consumed by the owner's dispatched
  call according to the disclosed conservative policy; it is not measured speech
  duration or a verified OpenAI invoice. Real audio/latency is still unverified.
- Owner action: reload `/AI/Voice` (Ctrl+F5) to load the versioned scripts. Git,
  remote CI, staging and production remain untouched.

### Local Admin allowance correction — 2026-11-18

- Owner reported exhaustion before useful testing and identified the account as
  Admin. Source confirms Admin resolves to Premium entitlement; it does not bypass
  voice accounting. The prior child-process trial configuration overrode both
  tiers to 60 seconds daily. One dispatched call conservatively consumed that
  entire reservation; this does not prove 60 seconds of actual speech.
- In response to that request and the existing broad approval for local testing,
  restored the source-default daily tiers in the local process: Standard=180,
  Premium/Admin=900. Kept each session at 60 seconds and global concurrency at one.
  This supersedes the earlier 60-second *daily* preview limit, and is a finite
  900-second daily allowance, not unlimited Admin calls or a provider dollar cap.
- Restarted only the verified task-owned 5107 executable via start-local.ps1.
  These settings are child-only environment overrides; no protected file, saved
  secret, persisted role, usage row, transcript or accounting policy changed.
- Verified real saved-local-account browser/API: available=true,
  canCreateCustom=true (Admin/Premium entitlement), dailySeconds=900,
  usedSeconds=60, reservedSeconds=0, remainingSeconds=840. Start enabled;
  Mute/Stop disabled before connection, secureContext=true, six catalog topics.
  Zero session-create requests, no microphone submission and no paid test call.
  Ignored evidence: `admin-allowance-evidence.json`, `admin-allowance.png`.
  Launcher build succeeded and readiness returned HTTP 200. Source behavior was
  unchanged, so no additional application unit tests were warranted for this
  runtime configuration correction. Live audio remains unverified.

### Real readiness and conversation recovery — 2026-11-18

- Owner reported the blue pending-close state after speaking then pressing Stop.
  Metadata showed both allocated owner calls had identical created/heartbeat/
  activity timestamps and zero finalized turns: neither had reached active.
  Both were already closed/settled, not ongoing orphan calls. Stop is whole-session
  termination; the page did not clearly distinguish it from submitting a turn.
- Reused official Realtime docs (client session.update, effective ACK and WebRTC
  unified-call guide) and the pinned sidebandReady.ts. Added sanitized mismatch
  diagnostics: requested field paths and bounded public model/transcription/voice
  identifiers only. No instructions, provider IDs, SDP, transcripts, keys or raw
  error/event payloads are logged. A privacy regression covers this projection.
- Two owner-authorized real-provider diagnostics used a synthetic silent input,
  no physical microphone. First isolated audio; deeper field diagnostics identified
  `audio.input.transcription.language`. The provider ACK matched gpt-realtime-2.1,
  gpt-transcribe and marin, but did not preserve the requested language hint exactly.
  Both starts timed out/returned 503 and were confirmed closed/settled. Safe field
  evidence retained in ignored `readiness-mismatch.txt`. No raw ACK was retained;
  the exact differing language value is not claimed.
- A failing regression removed the optional language echo and demonstrated that
  exact readiness rejected the otherwise correct ACK. Fix omits the optional
  fixed-language hint, using provider language detection, while still validating
  every requested security/audio configuration field. No generic readiness bypass,
  arbitrary model acceptance, credential/configuration-file or schema changes.
- UI copy now says to wait for listening, pause after a sentence to get the AI
  response, and use **Dừng phiên** to end the whole conversation. IDs and existing
  button contracts are preserved. Turn/Stop help uses semantic text and describedby.
- Final focused .NET suite: **56 passed, 0 failed, 4 MariaDB skips** (60 total),
  exit 0. Node voice suite: **19 passed**, zero failures/skips. Launcher Release
  build and migration check succeed, readiness HTTP 200. Syntax/whitespace pass.
- Real-provider acceptance after fix: one actual app session, generated Windows
  SAPI English greeting routed into a virtual microphone. HTTP start 200; peer
  connected in 7488 ms for this one run; four finalized turns including user and
  assistant, 15517 inbound audio bytes, remote audio element playing. Stop disabled
  after confirmed closure, microphone track ended, page ready. No human listening,
  physical microphone, measured conversational latency or interruption quality claim.
  Ignored `live-conversation-evidence.json` records safe counters; generated WAV is
  synthetic scratch on D, not a user recording or a committed/published artifact.
- Fake-browser Stop regression still passes: 202/202/200 without another click,
  immediate local media teardown, bounded pending/manual retry, five checks/zero
  page errors. New copy/labels and no horizontal overflow checked at 1440, 768,
  390 and 375. Native zoom/keyboard matrix was not repeated for this copy change.
- Final local DB metadata: seven closed/settled records (five allocated calls,
  including the owner's two calls and this turn's three diagnostic/acceptance
  calls; two cancellation tombstones), zero pending sessions/reservations.
  Allowance consumed=300/900, reserved=0, remaining=600 seconds. Response token
  counters: input audio 48, output audio 134, input text 166, output text 63;
  these are sideband observations, not a verified provider invoice or dollar cap.
  No refunds, quota reset, user/history deletion, extra package or shared-data edit.
- Local-only preview remains on 5107 with Standard daily 180, Admin/Premium daily
  900, both sessions 60 and concurrency one, child-process overrides only. No
  commit/push/PR/remote CI/hosting action. Owner should Ctrl+F5 before retrying.

## Checks

- `git status --short --branch`, `git rev-parse HEAD`: exit 0, baseline confirmed.
- First reference fetch attempted with unavailable `python` alias: exit 1;
  retried using the repository's `uv run --no-project python` runtime.
- Initial agent paid/live mic tests: NOT RUN before owner approval. The later
  owner-authorized preview and owner-triggered call are recorded above; no extra
  paid call was created during Stop diagnostics or browser regression checks.
- `dotnet test ... --configuration Release --filter FullyQualifiedName~AiVoiceRealtimeProtocolTests --no-restore`:
  first exit 1 (new implementation types absent), then exit 0: 23 passed, 0 failed, 0 skipped.
- CI, staging, production: NOT RUN; no commit/push/PR/deploy authorized.
