# Speaking: complete feature UAT through Chrome DevTools MCP

Prepared: 2026-11-13, Asia/Bangkok. Status: execution plan; this document is not
evidence that the planned browser/real-microphone cases have passed.

Execution prompt:
[gemini-speaking-chrome-devtools-full-uat.md](../../../.ai/prompts/gemini-speaking-chrome-devtools-full-uat.md).
Owner-selected executor: **Gemini 3.8 Flash High**, one agent.

## Outcome and authority

Exercise every currently implemented Speaking workflow through the real local
interface, with network and persisted-state evidence: public catalog, personal
YouTube import, subtitle upload, practice/transcript/player, recording, automatic
assessment, self practice, dictation, saved progress, completion and access rules.
Audit Speaking Admin operations using dedicated fixtures too. Produce reproducible
findings for the owner/Codex to fix; do not implement application fixes in this run.

Mode: **TEST_AND_REPORT_LOCAL**. Authorized: source inspection, Chrome DevTools
MCP interaction, test-owned fixture creation/deletion, read-only DB verification,
existing relevant tests and reports. Preserve the actual branch/dirty work.
Current branch observed: `fix/speaking-catalog-transcript-polish`; HEAD `9d55f6f`.
Re-measure at execution time; do not pull, reset, switch branches or commit.

**Explicit live-scoring authorization received in this chat:** use the already
saved local key for at most **10 live assessment requests**, each recording at
most **10 seconds**. The owner will help speak into the real microphone when
requested. Carry this authorization into the receiving chat; do not ask again
for the same grant. Count every submitted real request, including retries,
silence and requests that time out after dispatch. Track total submitted seconds
as well (maximum100). This grant covers learner assessment, not long-video
transcription, paid import fallback, TTS, AI suggestions or batch ASR.

No source/config/schema/credential/role changes to make tests pass. No Git write
stages, PR, remote CI, staging or production work. Do not exercise payment, SMTP,
account deletion or reset existing users. Auth is a prerequisite/access check,
not a full Auth audit. No parallel AI agents.

## Current verified context and important unknowns

- At planning time `http://localhost:5100/health/ready` returned `ready`. A listening
  port/readiness check does not prove the process contains current HEAD. Identify
  the actual checkout binary and its launcher before claims about current code.
- The local DB contains95 public Speaking videos: A1=18,A2=15,B1=17,B2=15,C1=15,
  C2=15. All95 have ImportStatus=ready. There are17,831 SpeakingSentence rows
  across the DB, including content outside the public batch. Inventory actual
  public/private/selected counts without treating that total as public coverage.
- The new catalog contains90 items (15/level) in
  `data/speaking/reach-a1-c2-catalog.json`. The old Listening batch is retired;
  the active public Listening catalog contains6 original lessons.
- The transfer ledger records16,262 cues on90 new videos and local-UAT publication.
  It explicitly leaves acoustic alignment, whole-speech review and audible QA
  pending. `ready`, nonempty cues or DOM highlights do not close that content gate.
- `SpeakingController` requires authentication; routes are listed below. Learner
  personal import permits Premium/Admin, uses captions or uploaded English SRT/VTT,
  and uses asynchronous jobs. It is not an unlimited ASR import service.
- Assessment uses `gpt-transcribe` and a server-side text-match calculation.
  `fluencyAvailable` and `completenessAvailable` are false in the current contract.
  Do not interpret text match as a validated phoneme/pronunciation/prosody score.
  Inspect whether product labels accurately describe what was measured.
- Current completion constants: passing sentence score>=70; video requires
  `max(1,ceil(totalSentenceCount*0.7))` distinct passing sentences. A server receipt
  is required for progress; client numeric scores are ignored. Recheck the source.
- UI features include automatic/self choices, local recording playback, a
  dictation tab with hints/slow replay/video hiding, transcript selection and
  keyboard shortcuts. An AI suggestion control exists; verify meaningful behavior
  rather than marking a CSS active-state toggle as an implemented feature.
- Chrome DevTools MCP is exposed in this authoring session; Gemini must discover
  its own actual connection. A Companion Page snapshot with page_id=null is not
  proof of an attached application tab.

## Focused source map

| Boundary | Current files |
| --- | --- |
| HTTP/access/progress | `TCTEnglish/Controllers/SpeakingController.cs` |
| Catalog/role/import projection | `TCTEnglish/Services/SpeakingService.cs` |
| Async import/leases | `TCTEnglish/Services/Speaking/SpeakingImportJobService.cs`, `TCTEnglish/Workers/SpeakingImportWorker.cs` |
| Captions/parser | `TCTEnglish/Services/YoutubeTranscriptService.cs`, `Services/Speaking/SpeakingSubtitleParser.cs` |
| Assessment/WAV/limits | `Services/Speaking/SpeakingAssessmentService.cs`, `SpeechProcessingOptions.cs`, `WavAudioValidator.cs`, `SpeakingAssessmentAdmission.cs` |
| Real provider | `Services/Speaking/OpenAiSpeechTranscriber.cs`, `OpenAiSpeechCredentialResolver.cs` |
| Presentation/scoring text | `Services/Speaking/TranscriptDisplayText.cs`, `SpeakingTextMatch.cs`, `SpeakingTextAlignment.cs` |
| Learner UI | `TCTEnglish/Views/Speaking/Index.cshtml`, `Practice.cshtml`, `_CatalogVideoCard.cshtml` |
| Browser behavior | `TCTEnglish/wwwroot/js/speaking.js`, `speech-recorder.js`, `ai-availability.js`, `time-tracker.js` |
| Admin UI/operations | `TCTEnglish/Areas/Admin/Controllers/SpeakingVideoManagementController.cs`, corresponding views under `Areas/Admin/Views/SpeakingVideoManagement/` |
| Design | `DESIGN.md`, `docs/design-system/team-ui-workflow.md`, scoped Speaking CSS |
| Existing evidence | `docs/feature-audits/2026-11-13-speaking-transfer-transcript-ledger.md`, symptom-relevant bug memory |

Use CodeGraph first for indexed source. Direct reads are for missing Razor/JS
bodies, stale files and a specific contract gap; do not preload the entire repo.

## Execution order and environment

1. **Inventory** branch/HEAD, app process/build, DB health, actual Chrome tools,
   fixture accounts, available mic/audio device and selected videos. Establish
   baseline console/network errors so unrelated third-party noise is attributable.
   Record relevant working-tree hashes as well as HEAD. If another editor changes
   Speaking during testing, preserve its work, flag the evidence boundary and
   rerun affected cases against an identified build rather than mixing versions.
2. **Set up isolated roles** Guest, Standard, Premium A, Premium B and Admin.
   Reuse saved synthetic local accounts where available. Read credentials from
   supported ignored local files; never print/store them in reports or screenshots.
   If a required second account is unavailable, ask for the missing fixture while
   continuing independent tests. Never upgrade an owner's real account for a case.
3. **Inventory every rendered control/action** in catalog, import, practice and
   Admin pages and map it to a test ID. Add a case for any feature discovered beyond
   the matrix; remove a case only with evidence that the feature does not exist.
4. **Run catalog/access/playback first**, then private URL/subtitle import and
   deletion, practice/self/dictation, bounded live assessment, persistence/rewards,
   failure cases, Admin, responsive/a11y and final cleanup/report.
5. Checkpoint the case matrix after each group and every costly/import operation.
   A blocked live branch does not block all offline/browser checks. Do not spend
   a long session only researching, repeatedly rebuilding or endlessly polling.

Use the existing web on port5100 if it is this checkout/current build. If startup
or an owned-process rebuild is necessary, follow AGENTS.md and use:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
pwsh -NoProfile -File scripts/speech-status.ps1
```

Preserve a foreign process and report that precise blocker; do not silently use
5101 or stop arbitrary dotnet/browser processes. Startup must not transcribe audio.
Speech status must be reported as configured vs live-verified separately.

## Chrome DevTools MCP protocol and evidence levels

Discover available names/schemas: list_pages/new_page, navigate_page, take_snapshot,
click/fill/press_key, upload_file, evaluate_script, list/get_network_requests,
list/get_console_messages, resize/emulate, take_screenshot and optional screencast.
Names may have client prefixes. Invoke list_pages, open/select a dedicated localhost
tab/context and prove a snapshot of the actual Speaking page before testing.

- Use fresh snapshot element IDs for interactions. Drive normal success paths
  through visible controls, not by directly calling internal JS functions.
- `evaluate_script` is useful for read-only DOM/player measurements, request
  correlation, browser-origin negative API checks and same-origin state queries.
  Never mutate DOM, SPK_SENTENCES, response data or score widgets to manufacture a
  pass. A direct fetch proves a request contract, not that a visible button works.
- Use isolated contexts per account if actually supported. Otherwise perform real
  logout/login and isolate cookies/storage; two tabs in one context share cookies.
  Include the intended account/role in every persisted-state check.
- Chrome tools do not automatically provide OS microphone permission, an audible
  hearing capability or generic CDP interception. Verify capabilities before using
  them; do not invent tools such as setPermission/interceptRequest.
- For reproducible failure cases use supported temporary page-scoped interception
  only if available, or existing isolated fake-provider tests. Record the method
  and restore it. Never rewrite saved keys/global config or stop the owner's DB
  to simulate an outage. Native browser zoom and CSS zoom are different checks.
- Classify evidence as **REAL_UI**, **LIVE_PROVIDER**, **REAL_MIC**, **SAMPLE_AUDIO**,
  **SIMULATED_BROWSER**, **AUTOMATED_OFFLINE**, **READ_ONLY_DB** or **SOURCE_ONLY**.
  One case may have multiple levels. Never report mocked/silent audio as real-mic
  quality evidence, nor source inspection as browser acceptance.
- Collect only relevant sanitized request/response fields. Redact cookies,
  Authorization, antiforgery tokens, keys and signed media URLs; keep raw HAR/audio
  and detailed private state out of Git. Do not copy user recordings into shared
  content packages or export personal learning progress.

## Fixtures and side effects

Use a unique run prefix, e.g. `spk-uat-20261113-<suffix>`, on synthetic accounts,
test-owned private clips, subtitle files and receipts. Baseline existing public
video/sentence IDs/hashes, selected account progress and Listening counts. Ordinary
interaction may legitimately add test-account progress; do not modify owner history.

Prepare a minimal private fixture (for example3 short cues) for completion/replay
tests, using explicitly identified synthetic subtitle/reference text and matching
sample audio or owner recordings. This fixture tests the software, not transcript
quality of a real public video. Do not publish synthetic subtitles as real catalog
captions. Preserve the source provider's genuine cues for a separate real URL import.

Use a verified short captioned YouTube source for one genuine URL import. Include
one caption-retrieval failure to verify truthful handling; uploaded SRT success
does not make a broken paste-only YouTube workflow pass. Reuse this test user's
fixture when checking duplicates, ownership and deletion; never delete one of the95
public videos or another user's private item. Prefer no persistent public mutations.

Admin edit/delete on public content must use a fresh synthetic fixture only.
Prefer an isolated disposable test host/database for Admin mutation cases. Never
export synthetic UAT data into the real shared catalog; if a local fixture must
be retained, identify it explicitly for cleanup and leave shared package hashes
unchanged. Ordinary test fixture creation is not a reviewed content release.
Admin Create currently calls a broader transcript path with possible paid audio
fallback; validate invalid submissions or test its success with a guaranteed
caption response in an isolated fake-provider environment unless extra authority
exists. Do not accidentally spend the learner assessment grant on Admin ASR.

## Feature test matrix

Each row needs PASS/FAIL/BLOCKED/NOT_APPLICABLE, actual steps, expected vs actual,
evidence and the tested role. Split multi-part rows when one branch is blocked.

### A — Entry, catalog and roles

| ID | Action / variation | Acceptance |
| --- | --- | --- |
| A01 | Open `/Speaking` and `/Speaking/Index` authenticated | Current page renders; no unhandled app exception; header/links and totals agree. |
| A02 | Guest requests catalog/practice/API | Configured cookie/auth challenge applies; no private data. Verify actual redirect/JSON behavior instead of inventing401. |
| A03 | Standard/Premium/Admin open public practice | Public content is usable under the actual contract; no unintended Premium lock. |
| A04 | Subject grouping and CEFR A1–C2 | All public cards reachable with correct counts; all95 at baseline accounted for without duplication. |
| A05 | Search Vietnamese/English, accent, case, long/no-match input | Accurate result intersection, counters and honest empty state; no stale cards. |
| A06 | Combine topic + CEFR + search, reset, back/reload | Stable results/URLs and filter state according to observed contract. |
| A07 | Pagination/continuation/playlist preview, rapid clicks | No missing/duplicate cards, correct next page, stable loading/error/retry. |
| A08 | My Videos and empty state per user | Only that user's clips and totals; public/private search does not leak titles/topics. |
| A09 | Standard import/locked private behavior; Premium downgrade fixture | Upgrade guidance and server denial; public practice still usable. Do not change real roles. |
| A10 | Card click/keyboard, sample link, hero/add/upgrade controls | Every visible action reaches its promised destination and works beyond cosmetic toggles. |

### B — Paste a YouTube link and asynchronous private import

| ID | Action / variation | Acceptance |
| --- | --- | --- |
| B01 | Premium A pastes a real captioned watch URL and presses Add | Browser POST→202/job polling→terminal success→exactly one owned private clip with genuine cues. |
| B02 | Valid youtu.be, rawID/embed/shorts forms actually supported | Normalize according to verified YoutubeUrlHelper contract; unsupported forms fail clearly. |
| B03 | Empty/whitespace, malformed/non-YouTube URL, invalid ID | Useful validation; no created job/video on invalid input; controls recover. |
| B04 | Invalid/private/deleted/unavailable/no-English/empty200 caption source | Truthful terminal failure with retry/subtitle path; no fake ready video or hidden paid ASR. |
| B05 | Double click, Enter repeat, same canonical ID and alternate URL | No duplicate owned video or competing durable jobs; feedback explains existing state. |
| B06 | Same source public vs private; same source in Premium B | Ownership remains separate; no public promotion or cross-user data changes. |
| B07 | Queued/running/succeeded/failed/cancelled cases | Status matches server; error distinct from completion; spinner/buttons settle. Simulate rare states safely. |
| B08 | Hidden tab/offline/reload while job running | UI resumes or offers the recorded job correctly; server work not falsely marked canceled. No excessive polling. |
| B09 | Request failure/status404/401/malformed response/5min watch deadline | Visible recoverable state; no endless busy button. Bound waits; inspect once, report and move on. |
| B10 | Import success count, open created private practice, reload | Correct owner/title/video/cues and persistent My Videos count; public95 unchanged. |

### C — Subtitle upload and private deletion

| ID | Action / variation | Acceptance |
| --- | --- | --- |
| C01 | Upload valid English SRT and VTT through visible picker | Correct filename and submitted file; completed owned clip with expected cue order/times. |
| C02 | BOM/UTF-8, apostrophes/line breaks, harmless markup/speaker markers | Readable literal-safe text with intended formatting; no script execution. |
| C03 | Empty/wrong extension/unreadable/malformed/too-large subtitle | Existing server bounds enforced (controller2MiB; parser1MiB/2,000 cues/default30min); clear error. Revalidate actual limits. |
| C04 | Overlap/reversed/negative/out-of-order/out-of-range cues | Invalid data rejected before publication; no incomplete ready row. |
| C05 | Valid public >30min practice vs learner >30min subtitle import | Full public tail works; ordinary upload restrictions reported honestly, not globally bypassed. |
| C06 | Duplicate source plus different subtitle content, retry after failure | Source/job identity handling verified; no duplicate/premature overwrite of progress-bearing cues. |
| C07 | Delete dialog Cancel/Escape/backdrop/focus | No deletion on cancellation; title/selection correct; keyboard returns to trigger. |
| C08 | Confirm delete on test-owned private clip | Server/DOM/count update agree, deleted practice is unavailable and no orphan access. |
| C09 | Failed delete/offline/403/404 then retry | Card/count preserved on failure; useful error; no false success or stuck modal. |
| C10 | Delete/import another user's ID with A/B sessions | Non-owner404/no write, protected job polling and private practice inaccessible. |

### D — Player, transcript and navigation

| ID | Action / variation | Acceptance |
| --- | --- | --- |
| D01 | Load practice with iframe early/late/error | Controls initialize independent of YouTube API timing; visible useful failure if unavailable. |
| D02 | Play/pause/replay/next/previous and first/final cue | Actual player time/audio agrees; no out-of-bounds reference or undefined text. |
| D03 | Click transcript row, numbered chip and dictation chip | Same selected cue, reference, active highlight and player seek; test early/middle/final. |
| D04 | Hide/show text then select another cue and restore | No stale original text, unintended answer leak or wrong selected reference. |
| D05 | A–B loop/current-sentence boundary and rapid navigation | Actual replay bounds follow cue; no previous-cue loop, clipped speech or hidden duplicate timers. |
| D06 | Marked/practiced indicators and server-backed reload | Scores/checkmarks/progress consistent and attributable to saved assessment, not cosmetic click state. |
| D07 | Long transcripts, auto-scroll, mobile collapse | Active row reachable without jumping away from controls; no horizontal overflow. |
| D08 | Caption display `>>`, special characters, Vietnamese empty translation | Safe readable display; meaningful empty translation; raw source preserved. |
| D09 | Late video cues >30min and final-tail samples per level | Player reaches real cue times and English speech matches the intended utterance. Content mismatch is a separate finding. |
| D10 | Space, Enter, Shift+Left, Right; textareas/buttons/IME/modifiers | Actual shortcut contract works without hijacking typing, native button activation or Tab. |

### E — Recording, self practice and lifecycle

| ID | Action / variation | Acceptance |
| --- | --- | --- |
| E01 | Microphone granted, start/stop via normal record control | Real permission/device used, one recording session, reference video paused, buttons/status clear. |
| E02 | Mic denied/dismissed/no device/device busy | Explain and recover after permission change; no phantom score/save. |
| E03 | Rapid start/stop, empty/very short recording, duration cap | No multiple streams, empty-upload loop or recording beyond validated bounds. |
| E04 | Choose Tự luyện, record and listen back | Actual local playback; explicitly unassessed; no assess/progress request or earned XP. |
| E05 | Self→automatic→self with existing audio | Clip lifecycle and selected mode consistent; no automatic paid resend on toggles. |
| E06 | Navigate cue/mode while recording/awaiting response | Stale result cannot apply to a new cue; input/score snapshot remains attributable. |
| E07 | Hide/leave page, reload/back-forward cache | Tracks/AudioContext/objectURLs/requests cleaned appropriately; no unseen recording or duplicate listeners. |
| E08 | Rerecord and replay own audio | Previous clip released; selected clip belongs to current user/cue; no persistence of private audio without design support. |
| E09 | Owner real-mic positive and intentionally mismatched sentence | Verify audible input, actual server response and UI, not just getUserMedia success. Label user assistance. |
| E10 | Unsupported recording browser features | Recoverable unsupported-state rather than broken controls or fabricated fallback score. |

### F — Automatic assessment and scoring semantics

| ID | Action / variation | Acceptance |
| --- | --- | --- |
| F01 | Correct/reference sample and owner utterance | Actual POST with valid WAV→transcript/comparison/score→progress flow, within live grant. ASR variation documented. |
| F02 | Missing/substituted/extra words or unrelated speech | Score/word chips reflect server transcript; words safe to render; do not assert an arbitrary exact acoustic score. |
| F03 | Silence/non-speech | Honest NoSpeech/zero/unassessed result, no usable assessment receipt/progress/reward. |
| F04 | Invalid audio/truncatedWAV/wrong rate/stereo/oversize/>limit | Correct validation error before provider call; no DB mutation or paid validation bypass. |
| F05 | Fluency/completeness and pronunciation/AI copy | Unsupported metrics are not presented as measured0 or real pronunciation/prosody scores. Record misleading labels/actions. |
| F06 | Timeout/503/auth/quota/429 busy + Retry-After | Correct error and self-practice offer; spinner clears; no fake100, stale score or automatic paid retry loop. Use fakes for induced provider faults. |
| F07 | Score request canceled/navigated after dispatch | No reassignment to other cue; preserve ambiguous sent-request count and allow safe recovery. |
| F08 | New transcript comparison vs old score, long/HTML-like tokens | Clear attempt identity; matched/missing/extra/substituted annotations literal-safe and readable. |
| F09 | Server success but progress POST fails; retry | Assessment/result distinguished from saved progress; no green saved state without persistence; reuse valid receipt rather than retranscribe. |
| F10 | Gợi ý cải thiện từ AI button and other advertised AI actions | Capture actual panel/result/network. A class toggle alone is not meaningful advice; no extra paid LLM call under assessment grant. |

### G — Persistence, completion, rewards and isolation

| ID | Action / variation | Acceptance |
| --- | --- | --- |
| G01 | Score saved; reload same clip/new tab/login session | Same user's persisted result and cue markers restore; no raw client-only success. |
| G02 | Lower/higher subsequent server-backed score | Current source best-score policy respected; lower score does not silently replace best persisted result. |
| G03 | Client submits forged numeric100 without assessmentId | Server409/contract rejection and no progress/reward. |
| G04 | Receipt expired/wronguser/wrongcue/consumed/replay | Rejected transaction/no unauthorized writes. Test expiry in isolated fixtures, not by changing machine clock or owner DB. |
| G05 | Small private fixture with>=70 on70% distinct cues | Required count=ceil(0.7*N), one completion/reward; no repeated-cue count inflation. Use offline fake receipts for threshold branches beyond live budget. |
| G06 | Repeat completion/save refresh/two tabs/duplicate click | No duplicate XP, streak, completion credit, badge or notification; compare baseline persisted counts. |
| G07 | Failure during save/reward transaction | No partial progress/receipt consumption/reward mismatch; isolate injected faults from primary live server. |
| G08 | Dictation hint/self recording/cosmetic done state | Cannot claim automatic server assessment or Speaking completion/XP. |
| G09 | Testuser B sees A's personal progress/job/video/assessment | No leakage of references/transcripts/private result; non-owner mutation returns404 where required. |
| G10 | Goals/activity/time tracking linkages | Legitimate completion/time updates attributable to synthetic account; hidden/idle/double-tab behavior follows current tracker. No reset of owner goals. |

### H — Dictation, accessibility, UI and Admin

| ID | Action / variation | Acceptance |
| --- | --- | --- |
| H01 | Dictation type correct/wrong/empty with case/punctuation variants | Genuine input feedback against selected cue; no false server-score/XP claim. |
| H02 | Hint/reveal/slow0.5x replay/show-hide video | Meaningful state, correct speed restoration, cue retained, no unintended answer reveal after next. |
| H03 | Switch modes with typed draft/record/result | No stale reference, duplicated player or score leak; actual persistence contract recorded. |
| H04 | Keyboard Tab/ShiftTab/Enter/Escape, transcript button semantics | All controls reachable/operable, visible focus, modal focus trap/return; shortcuts avoid text input. |
| H05 | 1440x900,1366x768,768x1024,390x844; limited laptop height | Controls readable/reachable, long text wraps, no horizontal clipping/overlap. |
| H06 | Real Chrome200% zoom; reduced motion; contrast/labels/live status | Verify actual zoom capability; CSS zoom/reflow is separate fallback. Unobserved200% remains blocked. |
| H07 | Cold/repeat navigation, catalog long list, rapid filtering | No reproducible app console exception, duplicate events/pollers or permanently stuck loader. |
| H08 | Admin list filters/pagination/search + Standard/Premium access | Correct roles/counts and no private learner metadata in public catalog. |
| H09 | Admin Create validation, edit fixture title/level/topic/playlist | Verify existing controls/routes; edit only a test fixture and restore/clean it. No paid ASR fallback. |
| H10 | Admin delete only test fixture; confirm failure/cancel/refresh | Correct server/DOM/count preservation, public95 and original sentences never removed. |

This is a minimum80-row matrix, not a fixed cap. Add discovered controls and split
branches into individual results. Full feature coverage is not all32.6h watched.

## Bounded live-scoring schedule

Reserve no more than10 real requests/100 seconds including dispatched retries:

- Up to3 matching short cue attempts on the small private fixture.
- Up to1 known mismatch and1 silence case.
- Up to2 owner live-microphone attempts (matching/mismatching); coordinate the
  visible cue and record start. Do not pretend an audio fixture is the owner.
- Keep3 attempts in reserve for a necessary retry or another representative cue.
  Do not spend the reserve just to reach10. A request aborted after dispatch still
  counts. Do not induce rate limits by sending extra live requests.

Use existing prerecorded/synthetic PCM samples for software fixtures, clearly
labeled. No paid TTS or long catalog-audio transcription. Run boundary, TTL,
concurrency, quota and unavailable-provider cases offline/fake where needed.
When the owner must talk, ask concisely which visible sentence to read, wait for
readiness/permission, then record. Continue other checks while awaiting a reply.
OS permission or audio output unavailable is BLOCKED, never a manufactured pass.

## Evidence, defects and review handoff

Deliver:

1. `docs/feature-audits/2026-11-13-speaking-full-uat-ledger.md`: environment/HEAD,
   capability map, fixture scope, actual feature inventory and live request counter.
2. `docs/feature-audits/2026-11-13-speaking-full-uat-cases.csv`: ID, area, role,
   evidence_level, status, expected, actual, evidence_ref, finding_id. Include
   denominator and separate PASS/FAIL/BLOCKED/N_A counts.
3. Detailed ignored evidence under `.appdata/local/speaking-full-uat/<run-id>/`:
   screenshots, sanitized network summaries, fixture IDs, before/after readouts
   and optional screencasts. Portable report links must not assume those ignored
   artifacts exist on a teammate's machine. Keep private audio off Git.
4. Findings grouped by P0–P3, each with route/role, precise trigger, observed vs
   expected result, consequence, fresh evidence, candidate source location and
   smallest proposed correction. Distinguish confirmed failure from source-only
   suspicion. Reference existing bug history instead of duplicating stale issues.
5. If findings exist, create a repair plan in
   `docs/superpowers/plans/2026-11-13-speaking-uat-repair.md`, a scoped English
   repair prompt in `.ai/prompts/codex-speaking-uat-repair.md`, and a short Codex
   launch message. These are deliverables for a future explicitly authorized fix,
   not permission to modify the application during the test. Do not create an
   empty repair plan when no actionable findings exist.

Each case record needs reproducible actions, actual browser/network/DB evidence
as appropriate and current role. A screenshot of95 cards cannot pass import,
assessment or persistence. A success toast cannot pass DB persistence. An HTTP200
without audio playback cannot pass transcript alignment. Live provider failure
is not proof of a client bug; separate environment failure from handling failure.

Read-only checks can batch-check public95 metadata/cue bounds/pages; behavior
samples must span six levels, both sources, an original video, a new video and
a >30min final cue. For content QA record exactly which audio was heard and where;
leave the transfer ledger's unsampled acoustic gates open. Do not silently mark
whole-catalog quality approved from a sampled feature UAT.

Run existing focused tests as supplemental evidence, not a substitute for Chrome:
`SpeakingAssessmentIntegrationTests`, `SpeakingImportJobLifecycleTests`,
`SpeakingImportReliabilityTests`, `PremiumSpeakingLifecycleIntegrationTests`,
`SpeakingTranscriptDisplayTests`, plus the relevant `scripts/tests/test_speaking_*.cjs`
and `test_speech_recorder.cjs` selected from actual scope. Do not run paid live
acceptance suites or destructive native fixtures against the owner's database.
No source change means no mandatory full app build/migration solely for this report.

Finish by restoring emulation/interception/permissions only as changed for this
run, stopping this run's active mic tracks, deleting only identified test-owned
content where safe, and reporting retained synthetic records if cleanup would
damage history. Verify95 original public Speaking roots/cues, six original active
Listening lessons and private baselines remain, subject to legitimate concurrent
owner edits. Leave localhost5100 ready and the owner's other browser tabs intact.

Vietnamese final handoff: what passed, concrete failures ordered by impact,
blocked/manual items, actual live request/seconds count, cleanup result, links
to ledger/cases/repair artifacts and Speaking URL. This is local UAT only, with
no CI/staging/production success claim.
