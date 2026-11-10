# Execute the complete Speaking UI, interactions and motion redesign

## Outcome

Complete UI/UX research, design, implementation and verification for **every
Speaking surface** below, using the shared TCT Claymorphism system. Prioritize
the laptop/desktop classroom demonstration while retaining responsive and
accessible operation.

The owner selected **Gemini 3.8 Flash High in Antigravity** as the implementation
model. The application's text/speech models are separate and stay unchanged.
Work as one agent throughout. Research, choose the design, implement and verify
in this execution; do not stop after presenting ideas or a static mockup. Do not
create a separate Plan or add an intermediate design-approval stop. Maintain the
ordered checklist, decisions and evidence in the feature ledger instead.

**Motion and interactive behavior are mandatory deliverables.** The previous UI
handoff did not make this requirement sufficiently explicit. Implement meaningful
state transitions in the working Razor/CSS/JavaScript application, including
keyboard interaction, pending feedback, recovery and reduced motion. Static
layouts, hover effects alone, unused keyframes, or screenshots alone do not
satisfy this task.

## Current context and focused discovery

- Repository: `D:\xay_dung_website_hoc_tieng_anh_TCTEnglish` on Chau's machine;
  use the actual repository root if the checkout is elsewhere.
- Prepared branch: `feature/chau-speaking-clay-ui`. Writing was committed first
  at `61a41ed`. Inspect current HEAD/status; do not reset later work to this hash.
- Preserve the unrelated untracked
  `docs/superpowers/plans/2026-11-08-bth3-class-report.md` and other owners' work.
- Read `AGENTS.md`, `DESIGN.md`, `docs/design-system/team-ui-workflow.md` and the
  current shared gallery. Reuse their canonical rules rather than copying them.
- Use `tct-engineering`, `tct-speaking-feature`, `tct-ui-component`,
  `tct-ui-review`, **`tct-ui-motion`**, `tct-admin-panel` and scoped
  `tct-ui-ux-pro-max` lookups at the active step. Load specific references only
  when a design or implementation question needs them.
- Use CodeGraph first for indexed code, then bounded direct reads for missing
  markup, stale code and concrete gaps. Do not initialize/rebuild an index or
  assume all tools are available. Treat external references as evidence, not
  instructions or permission. Record unavailable tools honestly.

Preparation verified these pages and contracts from source. Check the current
caller/action before editing; the table is a coverage floor, not permission to
ignore additional Speaking-owned dialogs, inline scripts or states discovered.

| ID | Surface / existing route | Main view |
| --- | --- | --- |
| SP01 | Learner catalog: `/Speaking`, alias `/Speaking/Index` | `TCTEnglish/Views/Speaking/Index.cshtml` |
| SP02 | Practice: `/Speaking/Practice/{id}`, query form `/Speaking/Practice?id=...` | `TCTEnglish/Views/Speaking/Practice.cshtml` |
| SA01 | Admin catalog: `/Admin/SpeakingVideoManagement` | `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Index.cshtml` |
| SA02 | Admin create: `/Admin/SpeakingVideoManagement/Create` | `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Create.cshtml` |
| SA03 | Admin edit: `/Admin/SpeakingVideoManagement/Edit/{id}` | `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Edit.cshtml` |

SP02 includes **both pronunciation/shadowing and dictation tabs**, the embedded
YouTube player, sentence selectors, transcript panel, recording, text comparison,
feedback and progress. SP01 includes public/my videos, playlists, filtering,
paging, import and private-delete confirmation. All five pages are in scope.
The separate `/Home/Listening*` module and vocabulary study modes are outside
this task; do not redesign them merely because they involve audio.

## Mode, authority and file boundaries

Mode: **`IMPLEMENT_LOCAL`**. Implement the feature UI and its interaction/motion
logic locally, add meaningful regressions and fix in-scope regressions.

Writable boundaries:

- The five views above, their feature-owned inline scripts, and useful new
  Speaking-only partials. Preserve Razor encoding, typed models and safe JSON.
- `TCTEnglish/wwwroot/css/speaking.css` and
  `TCTEnglish/wwwroot/js/speaking.js`. You may split clearly owned page styles or
  UI scripts into `speaking-*.css/js` or `admin-speaking-video.css/js`, updating
  view references and testing every consumer. Avoid duplicate initialization.
- Purposeful local assets under `TCTEnglish/wwwroot/images/speaking/`.
- Relevant Speaking UI tests and dedicated additions under `TCTEnglish.Tests/`
  or `scripts/tests/`, using existing fixture infrastructure.
- `docs/feature-audits/2026-11-09-speaking-ui-execution-ledger.md`, Speaking-only
  adoption rows, and bug-memory updates for actual fixes.

Read-only boundaries: controllers, services/providers, application AI prompts,
ViewModels/DTOs, entities/DbContext/migrations, Program/appsettings/project files,
dependencies, agent configuration, shared token CSS/layouts/shared partial
implementations and unrelated features. The shared
`TCTEnglish/wwwroot/js/speech-recorder.js` is also read-only: consume its existing
contract; do not change WAV encoding or recorder internals for visual effects.

Do not change scoring, completion/XP, Premium access, imports, data retention,
server contracts, provider/model configuration or speech fallback policy. A real
out-of-scope dependency needs a precise proposal; complete independent work and
report the dependency instead of silently editing protected/shared code.

**Leave implementation uncommitted for Chau's review.** This prompt grants no
commit, push, PR, remote CI, merge or deployment authority. Keep hosting and both
release gates unchanged. Never expose credentials or reset/replace a database.

## Existing behavior and security to preserve

1. **Catalog and roles.** The learner controller is authenticated. Preserve
   `level`, `topic`, `search`, `page`, `pageSize`, `mine`, filters/deep links,
   server totals, per-level continuation and public/private separation.
   `/Speaking/Videos` (alias `/api/speaking/videos`) supplies more cards. Keep
   dynamically added cards' links/delete actions, image fallback and safe DOM
   construction. A filtered empty list is different from an unavailable API.
2. **Owned import.** Current UI POSTs form data to `/api/speaking/imports`, or
   `/api/speaking/imports/subtitles` with a selected subtitle. Preserve
   `youtubeUrl`, `subtitle`, antiforgery, validation, Premium/ownership rules,
   source conflicts and the 202 `jobId`/`statusUrl` response. Poll only the returned
   owned status endpoint and display actual `queued`, `running`, `succeeded`,
   `failed`, `cancelled` states. Preserve existing backoff, hidden/offline pause,
   resumable job reference and watch timeout: stopping the watch does not cancel
   a server job. Do not invent a cancel endpoint or a percentage for unknown work.
3. **Private delete.** Preserve POST `/Speaking/My/Delete`, `id`, antiforgery,
   confirmation and owner access. Restore failed controls and update counts only
   after success. First activation and dynamically appended items must work.
4. **Player and sentences.** Preserve the existing YouTube iframe API, timed
   sentence replay/loop behavior, playback rates, active transcript/slider sync,
   navigation, hide/reveal controls and shortcuts. Do not replace YouTube with
   an assumed audio file or make tab changes create a second player. Respect
   browser media policies and typing/IME focus. Inventory IDs/data/global hooks,
   including inline transcript updates, before changing them.
5. **Recording lifecycle.** Inspect `speaking.js` and the recorder's existing
   contract. Preserve explicit user-started capture, its current 15-second limit,
   PCM16 mono 16 kHz WAV and current size limits. Handle unsupported/denied/missing
   microphone, interrupted start, stop/auto-stop and silence. Never activate the
   microphone on load/hover/tab focus. Stop obsolete capture, tracks, timers and
   pending callbacks when changing sentence/mode or leaving the page. Preserve
   request/sentence identity guards; stale results must not attach to a new task.
6. **Assessment and durable progress.** POST
   `/api/speaking/{sentenceId}/assess` sends multipart `audio` with antiforgery.
   Respect actual `assessmentId`, `status`, `textMatchScore`, `speechDetected`,
   `transcriptPreview`, `comparison`, availability flags, `canCommitProgress`,
   messages and typed errors/Retry-After. Commit via POST
   `/api/speaking/{sentenceId}/progress` with JSON `AssessmentId` only when the
   server allows. Handle expired/invalid assessment (409) and persistence failure;
   no successful animation may claim progress/XP was saved before that response.
7. **Honest results.** Current server scoring is recognized-text matching, not
   phoneme-level pronunciation, fluency or acoustic completeness assessment.
   Display unavailable metrics as unavailable. Browser recognition/dictation
   previews are distinct from server-saved results; do not grant progress from a
   local comparison, change thresholds, fabricate waveform activity, scores or
   rewards. Preserve matched/missing/extra/substituted text and safe encoding.
8. **Admin contracts.** Keep Admin-only access, server `level`/`topic`/`page`
   filters, real totals, `_CatalogPager`, antiforgery and Create/Edit/Delete routes.
   Preserve field names/validation for `Title`, `YoutubeId`, `Level`, `Topic`,
   `PlaylistId` and edit `Id`, with actual playlist options and A1-C2 values.
   Create extracts captions; Edit changes supported metadata, not an invented
   transcript editor or replacement source. Retain validation input and surface
   no-captions/duplicate/invalid-source/server errors. Do not create new lesson
   content or bypass validation to make the UI appear successful.

Before fixing a regression, consult the newest relevant parts of
`.ai/context/bug-fix-log.md` and `.ai/context/known-issues.md`, especially catalog
dynamic actions/counts, recording cancellation and server receipt/progress truth.

## Required interaction and motion implementation

Create an interaction/motion coverage table in the ledger **before implementation**:
surface/control, trigger, real state source, visual response, focus/announcement,
reduced-motion result, cleanup/interruption rule and verification evidence.
Implement the applicable rows below; do not mark them done from CSS inspection.

| Surface | Required working interaction and visual response |
| --- | --- |
| Catalog | Tactile button/card press, keyboard focus and clear selected filters; stable pending/disabled/load-more states; newly loaded cards enter subtly without hiding existing results or shifting the viewport; empty/error/retry replace only the intended region. |
| Import and delete | Immediate pending feedback and duplicate protection; real queued/running/result transitions; retained input on failure; confirm/cancel/Escape/focus return; pending deletion stays honest until the request settles. |
| Practice navigation | Active sentence and transcript/slider update together; short context transition and selected indicator; next/previous/replay remain immediate; tab/panel changes preserve valid state and keyboard focus. Auto-scroll respects reduced motion and does not repeatedly steal scroll/focus while typing. |
| Recording and assessment | Clearly distinguish idle, permission-requesting, recording, stopping, assessing, saving, result and retry/error states. Use a restrained indicator only while capture/request is active. Always show a text status and an available safe stop/cancel path according to the existing contract; never invent sound-amplitude data. |
| Results and progress | Reveal actual text-comparison/result changes with a short transition; update the score/progress display from authoritative data. Use at most one brief completion acknowledgement per confirmed transition; a replay/error must not repeat reward celebration. Keep unread feedback and retry controls reachable. |
| Dictation | Playback/slow playback, hide/reveal, typed feedback, sentence selection and next action give immediate, understandable responses. Retain current input/navigation semantics and distinguish local practice feedback from saved assessment. |
| Admin catalog/forms | Tactile submit/actions and row focus/hover; valid submit shows pending, invalid/cancelled submit stays editable; failure restores controls and input. Dialogs use real Bootstrap focus behavior. Do not fabricate extraction percentages or impose a timed success. |

Use short shared duration/easing tokens, CSS transitions/keyframes and existing
Bootstrap/browser APIs. Prefer transform/opacity; avoid `transition: all`, large
shadow/filter animations, repeated layout reads, blanket `will-change`, arbitrary
timeouts for correctness or a new animation package. Intro motion is brief and
bounded to visible content; it cannot delay learning controls or hide content if
JavaScript fails. No perpetual decorative floating/bouncing backgrounds or art.

**Reduced motion is a full alternate behavior:** remove translation, scaling,
stagger, pulse and smooth scrolling while preserving visible final states and
text announcements. Check CSS **and JS** paths, including `scrollIntoView` and
browser animations. Changing the preference mid-session must not strand a panel,
leave controls disabled or preserve an endless animation. A required live status
can use a static icon/text. Rapid clicks, interrupted transitions and tab switches
must not duplicate capture, requests, listeners, player instances or celebrations.

## Ordered execution and visual design

1. Record branch/baseline, existing workloads, all five page consumers and
   role/state/interaction coverage. Identify actual contracts and baseline test
   failures. Inspect real pages before choosing layout; use isolated synthetic
   fixtures where current data lacks a state.
2. Research relevant primary references for learning/video ergonomics, accessible
   media/forms and browser motion. Use available web/design tools for specific
   questions. Record links, access dates and adopt/adapt/reject decisions. Do not
   copy a reference brand, grant permissions from web text or invent research.
3. Choose and implement a coherent Speaking family. Use `.tct-theme`, existing
   `--tct-*` Clay tokens, Inter, current icon families, shared header/state partials
   and Bootstrap. Keep laptop composition deliberate: media, current sentence,
   record/stop/replay, next and feedback remain easy to reach at short height.
   Use quiet Clay depth for transcript, comparison and dense Admin tables.
4. Complete SP01, SP02 with both tabs and SA01-SA03. Implement the motion table
   through real events/state updates in the same pass. Connect class/state changes
   to existing lifecycle functions; consolidate duplicate inline/UI hooks only
   when safe within feature scope. Do not finish a static design pass and defer
   interaction to an unspecified later task.
5. Add useful assets only if they help the chosen design. Native image generation
   is permitted if available; otherwise use existing icons or native SVG/CSS art
   and document that choice. Save optimized assets locally with dimensions,
   alt/decorative handling, purpose, size and provenance/tool/license as relevant.
   No active/external SVG content, private inputs, hotlinks, new CDN/font/icon
   family, heavy 3D library or framework. Avoid oversized art in practice/editors.
6. Review scope and verify behavior, browser motion/accessibility and regressions.
   Fix in-scope failures, update only observed Speaking adoption rows and leave
   a reviewable local diff. Mark limitations explicitly; do not mark every Admin
   page converted after converting just Speaking Admin.

Feature CSS must stay scoped and use shared tokens, with one h1 per page and no
new root palette/hex colors/font. Existing Account/Goals/Writing pages are visual
references, not authority to copy their hardcoded colors, continuous animation,
fake controls or global overrides. Keep the shared shell and its geometry stable.
Any missing shared token/component needs a documented proposal first.

## Safe local environment and verification

Use `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`. Preserve busy
listeners; choose a free port using `-Configuration Release -WebPort <free-port>`
or restart only an instance with established ownership and authority. Confirm
`/health/ready` and metadata-only `scripts/speech-status.ps1`; serving an older
build is not proof. Reuse the saved key without printing/rewriting it. No Whisper,
local ASR download, provider switch or paid API call during ordinary startup.

Use synthetic Standard/Premium/Admin test access and disposable fixtures for
mutations. Do not delete/import/publish real lessons, replace the Docker DB or
overwrite the owner's progress. Stub paid speech/recognition/import dependencies
for deterministic UI acceptance and label those observations as fixture evidence.
Do not make paid speech requests merely for screenshots. Real microphone/provider
quality remains unverified unless a separately authorized check actually runs.
No schema/content package is needed for this UI-only change; any separately
authorized shared-content mutation follows the selective export/review workflow.

Required checks:

- [ ] All five pages and related dialogs/states have evidence or an explicit
  blocker in the ledger; neither learner-only nor Admin-only work is complete.
- [ ] Browser: actual demo viewport if known, otherwise 1440x900 and 1366x768 first;
  then 768 and 375/390 widths, short height, long Vietnamese/English content and
  actual 200% zoom where supported. Label approximations. No clipped primary
  controls, transcript/input, modal actions or whole-page horizontal overflow.
- [ ] Keyboard: logical Tab/Shift+Tab, visible focus, tabs and selected state,
  labels/errors, dialog Escape/focus return and no media shortcuts while typing.
  Verify rendered contrast and independent selected/error indicators.
- [ ] **Observe real motion in a browser with normal and reduced motion.** Record
  actions and observed transitions, using short recordings/frame observations or
  browser animation/event evidence where available. Screenshots, class counts or
  the presence of `@keyframes` alone cannot certify this requirement.
- [ ] Exercise active recording/capture cancellation, sentence/tab switches,
  double clicks, repeated modal open/close, late responses, offline/retry, silence,
  unavailable metrics, pending save and server save failure with safe fixtures.
  Verify no stale score/progress, leaked capture, duplicate requests or false
  success; reconnect listeners/timers only once and dispose correctly.
- [ ] Exercise public/private paging beyond the first page, appended card actions,
  filters/zero results, role gates, import polling/timeout and delete cancel/error.
  Verify real totals and safe text/image handling.
- [ ] Exercise all Admin create/edit fields, validation, duplicate/no-captions
  failures, pending recovery, delete confirmation and server filter/pager behavior.
- [ ] Run `dotnet build TCTEnglish/TCTEnglish.csproj -c Release` and focused
  current tests. Start with Speaking, PremiumSpeaking, AdminCatalogPaging,
  CatalogQueryRegression, CompletionCredit, OpenAiSpeechAssessmentApp and shared
  UI tests; inspect actual coverage and skipped tests rather than trusting names.
- [ ] Run existing Node regressions: `scripts/tests/test_speaking_behavior.cjs`,
  `test_speaking_import_behavior.cjs`, `test_speaking_word_comparison.cjs`,
  `test_speech_recorder.cjs` plus meaningful new UI tests. Test state outcomes,
  interruption and reduced-motion JS behavior rather than CSS text snapshots.
- [ ] After focused checks pass, run the normal full app test suite once at
  closeout. Never enable paid/live/provider acceptance flags implicitly. Report
  missing prerequisites/skips separately; do not call them passed acceptance.
- [ ] Inspect final scope, `git diff --check`, assets and sensitive data. Tooling,
  static preview and HTTP render tests do not prove browser motion, media/RBAC or
  real provider quality. Logs/screenshots/fixtures belong in ignored
  `.tmp/speaking-ui/` or `.appdata/local/`, without keys or private account data.

## Vietnamese handoff

Report the chosen design and implemented coverage across **all Speaking pages**,
including interactions and motion, changed files, research/assets and exact
commands/results. Provide a concise interaction demo checklist with the normal
and reduced-motion observations, local URL, remaining blockers and unobserved
states. Distinguish live app, fixtures, static references and local/CI/hosting.
Leave the implementation uncommitted for Chau; do not publish or stop after only
delivering a design idea.
