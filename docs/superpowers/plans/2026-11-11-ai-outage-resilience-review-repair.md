# AI outage resilience: review findings and repair plan

Date: 2026-11-11. Original reviews were REVIEW_ONLY. The final owner instruction
authorizes remaining fixes, grouped commits, push, PR, default-branch merge and
staging monitoring. Production remains off. Historical branch baseline:
`feature/ai-outage-resilience`, HEAD `e6521ad` plus uncommitted implementation.
Implementation model selected by the owner: Cursor, Grok 4.7 xhigh.

Original requirements: [outage plan](2026-11-10-ai-outage-resilience-plan.md).
Repair handoff: [execution prompt](../../../.ai/prompts/cursor-grok47-xhigh-ai-outage-resilience-review-repair.md).

## Current outcome after the final review

**All R1-R4-D repairs are verified closed locally.** The reviewer additionally
fixed a stale automatic-mode notice when switching to self-practice with no clip.
There are no remaining actionable findings in this reviewed scope. Earlier
findings below are history; do not reimplement the accepted repairs. See the
final section and execution ledger for checks, limits and publication authority.

## Initial review outcome

The core work is present: explicit self-practice, tab-memory recording playback,
no assessed progress in that mode, internal/lexical Chat preservation, a filtered
Writing catalog alternative, and separate provider-family circuits. Protected
bootstrap/configuration, schema and deployment files were not changed.

Do not close the original plan yet. Three reproducible defects remain, and the
browser acceptance matrix is only partially observed. This review did not fix
application code or alter Cursor's implementation/ledger.

## Verification evidence

- Full Node suite: **152 passed, 0 failed**.
- Tooling validator passed. Python tooling/scripts: **138 passed, 1 skipped,
  0 failed** out of 139 tests. Any staging-verifier text in this suite belongs to
  its synthetic fixtures; it does not establish a new live staging release.
- Document/link/whitespace checks passed. A 32-file candidate scan found no
  OpenAI-key or private-key patterns; this is a bounded pattern check, not a
  guarantee that every possible credential format was detected.
- Standard Release build and focused .NET suite: **250 passed, 1 live test skipped,
  0 failed**, 251 total. The filter included AiOutage, provider/speech clients,
  Writing local/optional/generation, shared provider consumers, hybrid retrieval,
  grounded phases 4/5, quotas and Speaking assessment.
- The first review run used `--artifacts-path` and failed 73 integration cases
  because the existing factory resolved a nonexistent content root. This was a
  review invocation problem, not an application regression. The standard-path
  rerun above passed; retain both facts rather than reporting that first run as
  product failures or hiding it.
- Ignored .NET reproduction harness compiled against the reviewed application
  assembly, not a copied circuit implementation. Three overlapping-request
  scenarios violate the probe admission contract.
- The real Speech 429 reader consumed **2,097,191 bytes** from a synthetic body
  despite truncating the string to 8,192 characters afterward.
- The actual 429 mapping returned `engine_busy` for `insufficient_quota`; the
  actual JS helper classified that result as the learner sending too quickly.
- Local `/health/ready` on port 5100 returned 200/ready. The review browser reached
  the login page, not an authenticated practice flow. No paid request or real
  microphone recording was made. Browser source-file access blocked a temporary
  local login helper; that helper was removed. The original saved access file
  was not modified and no credentials were printed.

Local artifacts, excluded from Git:

- `.appdata/local/test-results/ai-outage-review/ai-outage-review-standard.trx`
- `.appdata/local/test-results/ai-outage-review-standard.log`
- `.appdata/local/review/ai-outage/GuardRepro.csproj`
- `.appdata/local/review/ai-outage/Program.cs`
- `.appdata/local/review/ai-outage/reproductions.log`

The reproduction harness uses reflection only to exercise the existing private
Speech response mapping with synthetic HTTP content; it performs no HTTP call.
Do not ship it as a public diagnostic route or rely on its ignored path as the
team's only regression coverage. Add durable tests to the existing test project.

## R1 [P2] Older request completion can release a newer recovery probe

Files: `TCTEnglish/Services/AI/Resilience/AiOutageGuard.cs`, especially
`AiOutageCircuit.Release` (118-123), completion methods (76-115), and lease Finish.

Every lease calls the same circuit methods without an admission identity or
generation. `Release` unconditionally clears `_probeInFlight`, including for an
ordinary request admitted before the circuit opened. Success/nontransient
completion can also clear the newer probe/open state.

Reproduction using the current registry:

1. Keep an ordinary lease A in flight.
2. Complete three other admitted requests as transient failures; circuit opens.
3. Advance a fake clock by 30 seconds; admit recovery probe P.
4. Cancel/release A while P remains in flight.
5. A competitor is admitted as a second Probe; expected Rejected.

The same setup with A completing successfully or nontransiently admits an
ordinary competitor while P still owns recovery. This defeats the intended
single-probe gate and can restore a burst of provider calls during an outage.

Repair:

- Carry a circuit generation and/or explicit admission token in each lease.
- Release/settlement must affect only the state that lease owns. An older normal
  lease must not release the current half-open probe or reset a newer generation.
- Keep one-time settlement, cancellation cleanup, failure threshold, Retry-After,
  authority isolation, bounded registry and process-local semantics.
- Add deterministic tests for old cancellation, success and nontransient
  completion while a new probe is held. Also test stale transient completion and
  normal probe success/failure/cancellation without sleep-based timing.
- Exercise a real client with overlapping tasks at least once; do not test only
  direct state mutation. Check zero additional HTTP sends for rejected callers.

## R2 [P2] Speech 429 classification buffers an unbounded error body

File: `TCTEnglish/Services/Speaking/OpenAiSpeechTranscriber.cs`,
`IsBillingQuotaAsync` (222 onward).

`ReadAsStringAsync` downloads/buffers the entire content before `text[..8192]`.
The later truncation limits JSON parsing, not network read or allocation. It
bypasses the bounded-response behavior used by successful Speech responses.

Repair:

- Read a bounded byte stream with the existing cancellation/deadline token; stop
  at a documented small error-body limit, allowing at most a sentinel byte to
  detect oversize. Dispose the response/stream correctly.
- Parse only a complete, valid JSON object within the bound. Oversized, truncated,
  malformed or unreadable metadata must follow a safe existing failure path;
  do not log response content, keys, recordings or request details.
- Do not add a new package, protected setting or unbounded read helper.
- Add a counting/streaming HttpContent regression exceeding the limit, including
  absent/incorrect Content-Length, plus cancellation, malformed JSON and small
  valid `error.code` / `error.type` billing responses.
- Verify actual bytes consumed, not just length of the truncated parsed string.

## R3 [P2] Billing quota is shown as a learner rate-limit error

Files: `OpenAiSpeechTranscriber.cs`, MapResponseAsync 429 branch (167-178),
`wwwroot/js/ai-availability.js` (43-47), and the Speaking compatibility fallback
classifier in `wwwroot/js/speaking.js`.

The server detects `insufficient_quota` / `billing_hard_limit_reached` and keeps
them out of the transient circuit count, but still returns `engine_busy` and
"ASR đang quá tải". Speaking maps that code to HTTP 429; the browser displays
"Bạn đang gửi yêu cầu quá nhanh." Waiting or reducing the learner's request rate
does not repair the provider billing problem.

Repair:

- Preserve distinct internal/public typed outcomes for provider billing quota,
  temporary provider rate limiting and local admission/queue rejection.
- Prefer a compatible additive code/discriminator, using existing unavailable
  codes where they fit. Trace SpeakingAssessmentService and controller mapping;
  do not blindly change all 429 responses or expose provider response bodies.
- Ensure billing/quota/access uses the generic feature-unavailable notice and
  offers self-practice without blaming the learner. Actual rate limiting can
  retain a rate notice; respect usable retry information where supported.
- Keep billing nontransient for the circuit and preserve no-progress behavior.
- Test the actual transcriber -> assessment/controller response -> actual JS
  presentation path. A fabricated engine_busy fixture alone cannot prove that
  billing is classified correctly. Check code and type variants and the two
  non-billing busy cases separately.

## R4 Acceptance gap: complete observable browser evidence

Cursor's ledger checks Phase 4 while explicitly listing unobserved required
states. Keep the honest limits and mark the phase partial until those checks
are observed. Do not rewrite missing acceptance as passed.

Required local acceptance still includes:

- A completed self-practice recording and usable playback control, then replace,
  sentence/mode change and navigation cleanup; zero assessment/progress requests.
- Native keyboard activation of both assessment-choice buttons and focus states.
- Actual measured 768px and 390px layout widths, not a requested emulation whose
  document layout remains 627px; laptop height and 200% zoom.
- Local browser interception for representative auth/rate/provider/timeout cases,
  self-practice recovery, retained Writing input and catalog navigation.
- Full and embedded Chat notices plus no automatic transport replay on unknown
  dispatch outcomes. Keep provider/user data and screenshots private.

Use local synthetic accounts/lessons and test-only interception. No paid call,
staging fault, production change, public fault endpoint or local ASR install is
needed. A real microphone/provider quality test remains separate and optional.

## Ordered execution and completion checks

When the owner submits the repair prompt, work as IMPLEMENT_LOCAL on the current
task branch, one agent. Preserve all existing implementation and unrelated BTH3
documents. No commit/push/PR/CI/merge/deployment authority is granted.

1. [ ] Establish baseline and inspect the exact affected methods/tests. Read the
   relevant bug memory and original plan; keep scope at R1-R4.
2. [ ] Add failing R1 regressions, repair lease ownership, run guard/client tests.
3. [ ] Add failing R2 counting-stream regressions and implement bounded reads.
4. [ ] Add failing R3 end-to-end classification tests and repair the distinct
   outcome/presentation mapping, including local queue versus provider quota.
5. [ ] Finish R4 browser acceptance and retain actual widths/network evidence.
6. [ ] Run focused .NET and full Node suites, then broader app tests only if
   changed behavior or unresolved failures justify them. Review diff inline.
7. [ ] Update the execution ledger and append bug memory for actual fixes. Report
   any blocked check honestly; do not close the plan with required gaps.

Focused commands (normal output layout avoids the review content-root issue):

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter 'FullyQualifiedName~AiOutage|FullyQualifiedName~OpenAiProviderClient|FullyQualifiedName~OpenAiSpeech|FullyQualifiedName~OpenAiConsumerPipeline|FullyQualifiedName~AiRagHybridRetrieval|FullyQualifiedName~AiRagPhase4|FullyQualifiedName~AiRagPhase5|FullyQualifiedName~AiQuotaReservation|FullyQualifiedName~SpeakingAssessment|FullyQualifiedName~WritingLocal|FullyQualifiedName~WritingOptional|FullyQualifiedName~PremiumWritingGeneration'
node --test scripts/tests/*.cjs
git diff --check
```

Keep live/MariaDB test opt-ins off unless separately authorized. Use the normal
local launcher and saved key without rewriting it; preserve an existing server.
If browser tooling cannot access a local credential file, do not print/copy keys
into chat or assume the user has logged into that tool's browser session.

Definition of done: R1-R3 regressions fail before their fixes and pass afterward;
existing guard/accounting/assessment/consumer checks stay green; R4 has actual
evidence or remains explicitly incomplete. No protected config/schema/content
package change is expected. Final report in Vietnamese with local/CI/staging/
production separation and remaining limitations.

## Second review: backend closed, Speaking acceptance still open

Same branch/HEAD plus uncommitted changes. No application source was changed by
the reviewer. Independent results: Release build/focused .NET **259 passed,
1 live skipped, 0 failed**; full Node **153 passed, 0 failed**.

The updated registry rejects all three old-request cancellation/success/
nontransient probe scenarios. The updated Speech error reader consumes 8193
bytes at most: 8192 metadata bytes plus one oversize sentinel. Small billing
metadata now maps to quota_exhausted. R1-R3 must stay closed unless a new relevant
regression actually fails.

Authenticated checks used the already-installed Playwright CLI and a dedicated
reviewer-owned Chrome session. Saved local test credentials were read without
printing or rewriting them. Synthetic audio replaced the microphone and Speaking
POSTs were intercepted in the browser. No paid call/progress write reached the app.

Observed: a finished self-practice clip plays successfully (synthetic 0.256s), has
native controls, an unassessed label and em-dash score. Replacement and mode
switch revoke its URL. Zero Speaking POSTs occur in that self-practice flow.
1440px and 768px document widths fit. Rendered CSS zoom reached computed 2 and the
choice group fit at 1440px; this is not a claim of native browser/OS zoom.

### R4-A [P1] Global shortcuts override native focused controls

Location: `TCTEnglish/wwwroot/js/speaking.js`, onKeyDown lines 1699-1721.
The document listener skips INPUT/TEXTAREA only and prevents default Enter,
Space and Tab even while a button is focused. This is not an automation defect.

Trusted browser keyboard results, focused on `#spk-choice-self`:

- Enter leaves self=false/automatic=true, invokes getUserMedia once and starts
  recording. Stopping the synthetic take attempts one assess POST (intercepted).
- Space leaves self=false and executes the video shortcut.
- Tab leaves focus on the same button and changes sentence 1 to sentence 2.

Add failing actual-script regressions, then scope shortcuts outside interactive/
editable controls, respect default prevention/composition/modifiers, and never
hijack Tab/Shift+Tab. Preserve documented background shortcuts. Native Enter/
Space on each choice must change the choice once with zero microphone/network/
video side effects; on the record button they must cause exactly one native
click. Check both focus directions through choice/audio controls in a real browser.

### R4-B [P2] Speaking content overflows the 390px viewport

The practice-page descendants in `wwwroot/css/speaking.css` are the target, not
an assumed shared-shell bug. Real layout measurements:

- innerWidth 390, clientWidth 390, scrollWidth 627.
- Workspace/progress/player/pronunciation panels measure 611px.
- Choice group x 41.5..601.5, width 560px.
- `.main` measures 390px, margin-left 0 and min-width 0; 768/1440 fit.

Inspect intrinsic grid/flex minimum widths, sentence strip, player, and score/
controls. Apply page-scoped min-width/minmax/wrapping corrections where needed.
Do not mask overflow with global clipping, scaling or tiny text, or redesign
the shared shell. Preserve shared Clay tokens and laptop composition. Verify
actual 390px scroll width fits (only measured subpixel tolerance), important
controls remain reachable, inner sentence scrolling stays contained, long text
wraps, both modes/playback work and 768/1440 retain their current fit.

### R4-C [P2] Media disposal is not registered for page lifecycle

Location: `wwwroot/js/speaking.js`, dispose/init/export lines 1728-1769. The
Speaking view/script/shared callers do not bind dispose to pagehide/navigation.
Directly calling dispose in an isolated test does not prove lifecycle wiring.

Browser check with a synthetic blob fixture and actual app controls:

- Before pagehide: clip visible/attached; revocation count 0.
- After dispatching pagehide with persisted=true: unchanged; revocation count 0.
- After explicitly calling exported dispose: clip hidden/detached; revocation 1.

A real navigation check also observed no app-level revocation. This is missing
application cleanup/cached-page handling, not a claim that browsers always leak
microphones or retain destroyed documents. Wire idempotent media cleanup to an
appropriate lifecycle event and keep cached-page restoration usable without
duplicate handlers or automatic recording/uploads. Test a held recording,
finished clip, pending assessment, repeated cleanup and restored navigation.

### Remaining execution and acceptance

1. [ ] Add failing R4-A regressions, fix shortcut scope and verify native keys.
2. [ ] Fix R4-C lifecycle binding/restoration and test event-driven cleanup.
3. [ ] Fix R4-B using scoped CSS and measure 390/768/1440/laptop height/zoom.
4. [ ] Run actual-script/recorder and full Node checks. Preserve R1-R3 backend
   regressions; rerun focused .NET if backend changes or new concerns justify it.
5. [ ] Update the execution ledger/bug log with actual fixes and browser evidence;
   do not close the original plan while a required case fails.

Ignored evidence: `.appdata/local/review/ai-outage-rereview/` contains backend
recheck output, keyboard/layout/playback/lifecycle scripts/logs and a 390px PNG.
Do not commit these artifacts or temporary authentication state. Current prompt
authority remains one-agent IMPLEMENT_LOCAL only, with no Git/release stage.

## Third review: R4-A/B/C accepted, one status repair remains

Reviewed on 2026-11-11 at the same branch/HEAD with uncommitted implementation.
Review-only: no application source, key, schema, hosting or Git stage was changed
by the reviewer. Only this repair plan and its current execution prompt changed.

### Verified repairs and fresh checks

- Native Chrome Enter and Space on both choice buttons select the intended
  mode with zero microphone calls. Tab/Shift+Tab move focus without changing
  the sentence. Enter on Record produces one click and one microphone call.
- A completed synthetic self-practice take plays through native audio controls,
  shows score `—`, and submits no assessment/progress request. Playback duration
  was 0.256 seconds; this was synthetic audio, not the physical microphone.
- pagehide stops held tracks, clears finished blob URLs, aborts a held assessment
  and rejects its late success. Repeated persisted pageshow does not start work
  or duplicate the record action. No stale progress POST occurs.
- Real navigation revokes the clip (session-local counter 1 -> 2). Back returns
  without a clip. The browser reloaded the history entry; actual BFCache reuse
  was not established. Persisted lifecycle behavior was exercised by dispatched
  PageTransitionEvents and actual-script tests.

| Stable viewport | Document scroll width | Workspace width |
| --- | --- | --- |
| 1440 x 900 | 1440 | 696 |
| 1366 x 768 | 1366 | 622 |
| 768 x 1024 | 768 | 736 |
| 390 x 844 | 390 | 358 |

390px dictation also fits. The sentence strip is intentionally internally
scrollable (239px client, 492px scroll width). Rendered CSS zoom was measured
at `2` after allowing layout to settle, with document width 1440; this is not
native browser chrome zoom/OS scaling. Reduced-motion media emulation matched.

- Fresh Release build/focused .NET run: **259 passed, 1 skipped, 0 failed**.
  The skip is the existing opt-in live silence test, not a passing live test.
- Fresh full Node suite: **156 passed, 0 failed**.
- Independent added status regression probe: **13 passed, 1 failed** in an
  ignored copy of the 13-test UI fixture plus the reviewer regression. This
  expected red reproduces R4-D; it does not modify the tracked test suite.
- `git diff --check` passed. Protected bootstrap/config/project/workflow paths
  have no changes. No new paid call or staging/production test was made.

Initial reviewer harness attempts needed a corrected pane selector, a settled
layout read for zoom, and a sessionStorage revocation counter because console
events during navigation can be lost. The completed browser run passes its
keyboard, layout, playback and resource assertions; the status probe below is
the remaining application failure. A screenshot attempt waiting for external
fonts timed out; earlier current-source screenshots were inspected instead.
These harness corrections are not application defects or passing full acceptance.

### R4-D — P2: reset the active status when lifecycle cleanup cancels work

**File:** `TCTEnglish/wwwroot/js/speaking.js`,
`releasePracticeForLifecycle` (around 1751-1761), plus status rendering called
from recording/assessment. The routine aborts work and resets button state but
never replaces `#spk-assessment-status`.

**Reproduction:** start an automatic take; hold the assessment response; dispatch
pagehide then persisted pageshow. Tracks are ended, the clip is gone, and the
button correctly returns to `Kiểm tra phát âm`, but status remains
`Đang gửi máy chủ nhận dạng...`. A held self-practice recording similarly retains
`Đang thu để nghe lại. Bản này không gửi đi chấm.` after cancellation. Both the
Chrome check and an independent actual-script regression reproduce the issue.

**Impact:** the restored page says it is still sending/recording when work has
already stopped. This is a presentation-state defect; no stale progress write,
automatic upload or retained microphone was observed.

**Smallest repair and dependencies:**

1. [ ] Promote a meaningful regression into `scripts/tests/test_ai_outage_ui.cjs`
   for a held recording and pending assessment across pagehide/persisted pageshow.
   Keep the current resource/late-response assertions; assert status as well.
2. [ ] In lifecycle cleanup, render an honest idle/cancellation state when active
   work was canceled. Capture necessary state before aborting. Keep selected mode,
   stored scores/progress and existing ownership/epoch checks. Do not advertise a
   playable clip after revoking it or claim that a dispatched request was never
   processed; cancellation is not proof of server rollback.
3. [ ] Make repeated events harmless and keep pending rejection/late completion
   from restoring the old active status. Preserve meaningful already-completed
   states when no active work was canceled; avoid unconditional score resets.
4. [ ] Run the focused actual-script tests and full Node suite. Recheck both cases
   in the browser using synthetic audio and intercepted assessment responses.
   Verify controls remain usable, no automatic retry, no extra assessment/progress
   POST, no new score/XP, no active recording/sending copy after cancellation.
5. [ ] Update the execution ledger and bug log with actual evidence and limits.
   Preserve accepted R1-R4-C code. No backend/config/schema/Git/release expansion.

Evidence is ignored under `.appdata/local/review/ai-outage-third-review/`:
`browser-acceptance.log`, `browser-final.log`, and `status-regression.log`.
The temporary browser authentication file is removed after review. Do not commit
browser storage, local credentials, screenshots or these scratch fixtures.

## Final review and owner-authorized integration — 2026-11-11

- [x] R4-D: held recording/assessment cancellation renders a stopped state;
  idle results, selected mode and stored scores remain unchanged.
- [x] Repeated restoration and late responses do not restore active copy or
  write progress. Current-source browser checks include native keys, playback,
  real navigation, 1440/1366/768/390 widths and rendered CSS zoom 2.
- [x] Reviewer repaired automatic -> self empty-clip notice after a red regression.
- [x] Full Release tests on .NET 9.0.16: 1210 passed, 22 opt-in skips, 0 failed.
  The initial .NET 10 CLR-aborted run remains documented, not counted as passing.
- [x] Full Node: 158 passed. Python: 138 passed, one skip. Tooling validator passed.
- [x] Reviewed source/config/data boundaries and candidate secret scan. No new
  schema, shared-content package or hosting setting is needed.

Publication checklist (authorized by the owner's final instruction):

1. Commit backend, UI/regressions and documentation in coherent groups, excluding
   the three unrelated BTH3 files and all private/runtime artifacts.
2. Push the feature branch; create a PR to actual default `master` (no `main`
   branch exists). Wait for all PR checks; resolve failures/conflicts if any.
3. Merge the verified PR. Follow the exact merge commit's Actions runs, staging
   job, expected public marker and authenticated `/health/ready`.
4. Confirm production release stays skipped and its gate stays false. Synchronize
   the local default branch with `scripts/pull-local.ps1`, preserving Docker data
   and unrelated work. Attach actual live evidence to the PR/owner handoff.

Publication is not claimed complete by these pre-commit checks. Synthetic/local
tests do not establish live provider or staging outage acceptance. No new paid
request or actual BFCache reuse was observed. The initial .NET 10 internal CLR
crash was not diagnosed as an application defect; the matching .NET 9 run passed.
