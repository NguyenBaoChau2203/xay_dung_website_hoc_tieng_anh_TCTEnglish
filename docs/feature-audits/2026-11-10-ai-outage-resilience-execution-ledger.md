# AI outage resilience — execution ledger

Date: 2026-11-11. Branch: `feature/ai-outage-resilience`, created from `master`
at `e6521ad34f423da468a6b9c3c51ea0b9245bf104`. One agent. Original implementation
was local-only; the owner's final instruction now authorizes remaining fixes,
grouped commits, push, PR, merge to the actual default branch `master`, and
monitoring its staging release. Production stays disabled. Earlier no-Git/release
statements below describe the historical implementation stages.

## Phase 0

- Checkout was `master` at the planning baseline. The task branch was created there. It was not reset.
- Untracked BTH3 files were left untouched:
  - `.ai/prompts/bth3-class-report-logic-repair.md`
  - `docs/superpowers/plans/2026-11-08-bth3-class-report.md`
  - `docs/superpowers/plans/2026-11-09-bth3-logic-review.md`
- CodeGraph MCP was not in the available tool catalog. Contracts were read from current source. No index rebuild.
- No `Program.cs`, appsettings, csproj, workflow, secret, or release-gate edit.
- No EF entity or migration file changed. `dotnet ef migrations has-pending-model-changes` could not construct `DbflashcardContext` in that shell because the design-time host required the local connection and refused to start. It did not apply a migration.

## State contract

- Speaking assessment choice is `automatic` (default) or `self`, only inside pronunciation practice.
- Self-practice keeps one in-memory WAV for the current sentence. It does not call assess, progress, or `SpeechRecognition`, and it does not write storage.
- Switching sentence, pronunciation/dictation, or disposing revokes the object URL and rejects a late recorder callback via the recording epoch.
- A failed automatic attempt can offer **Chuyển sang tự luyện**. Returning to automatic drops the clip and does not upload it.
- Writing creation failure keeps the textarea and shows **Chọn bài có sẵn** to the current catalog URL and filters. It does not create a substitute exercise.
- Chat `circuit_open` is a known unsent failure. Other post-dispatch failures ask the learner to reload history and do not fall back from SignalR to HTTP.
- Internal answers whose usage model is `internal-chat-boundary` are labelled **Trả lời từ dữ liệu trong hệ thống**.
- The guard is process-local, per family (text, embedding, speech) and per endpoint/model/credential hash. Three completed transient outcomes open it for 30 seconds, capped at 120 seconds when Retry-After is longer. One probe is admitted after cooldown. Cancellation releases the probe. Restart clears it. IIS workers do not share it.

## Checklist

- [x] Phase 0 baseline and branch
- [x] Phase 1 self-practice and script regressions
- [x] Phase 2 notices, Writing catalog link, Chat provenance and unknown outcome
- [x] Phase 3 guard, client wiring without bootstrap edits, accounting tests
- [x] Phase 4 local browser checks that were reachable without a paid call
- [x] Phase 4 repair matrix R4-A/B/C, with the real back-navigation limit recorded below
- [x] R4-D lifecycle status reset after canceled recording or assessment
- [x] Phase 5 focused tests, full Node suite, full Release .NET suite, docs, bug log
- [x] Repair R1–R3 code and regressions; R4-A/B/C evidence recorded below

## Verification

| Check | Result |
| --- | --- |
| `AiOutageGuardTests` Release | 13 passed, 0 failed |
| Focused provider/speaking/writing/RAG filter | 250 passed, 1 skipped (`LiveSilenceWav_IsNoSpeech_WithinBudget`), 0 failed |
| `dotnet test TCTEnglish.Tests` Release | 1201 passed, 22 skipped, 0 failed, about 3 m 30 s |
| `node --test scripts/tests/*.cjs` | 152 passed, 0 failed, before the self-practice recording-status correction |
| Re-run of `test_ai_outage_ui.cjs`, `test_speaking_behavior.cjs`, `test_speaking_word_comparison.cjs`, `test_speaking_clay_interactions.cjs` | 35 passed, 0 failed, after that correction |
| `git diff --check` | clean |
| `/health/ready` on `http://localhost:5100` | `{"status":"ready"}` |
| Launcher speech line | `configured (User Secrets AI:ApiKey)`. This is metadata, not a live transcription. |

Skipped .NET tests are the existing opt-in MariaDB/live rows. They were not enabled.

## Browser observations

Local app started with `scripts/start-local.ps1 -NoBrowser` on port 5100. Already signed in as the local admin. No new AI prompt was submitted.

- `/Speaking/Practice/53`: **Chấm tự động** starts pressed. **Tự luyện** switches the record button to **Thu và nghe lại** and shows the unassessed note. Playback container stays hidden until a clip exists.
- A self-practice recording start made no `/api/speaking/.../assess` or `/progress` request. The first build incorrectly said the clip would be sent for scoring; that status now says the clip is only for local playback.
- At a 1440px-wide viewport the choice group stayed inside the width. At a requested 390px emulation the layout viewport stayed about 627px, so a true 390px phone width was not observed. At CSS zoom 200% the choice group still fit the viewport width.
- `prefers-reduced-motion: reduce` was emulated and `matchMedia` reported it. Focus moved to **Chấm tự động**. The automation Enter key did not activate that button; mouse/script click did.
- `/Home/Writing/Exercises?level=beginner&contentType=articles` shows the AI create form, no catalog link before a failure, and the existing empty catalog **Không tìm thấy bài tập phù hợp**. The form's catalog URL is `/Home/Writing/Exercises?...#system-catalog-heading` and returns HTTP 200.
- `/AI/Chat` shows history and the composer. No message was sent.

Not observed: a finished self-practice playback control left on screen, a real 390px layout, keyboard activation of the choice button, 768px as a distinct width, a forced provider 401/429/5xx in the browser, or a paid speech/text request.

## Shared consumers

`ai-availability.js` is loaded only by Speaking practice, Writing exercises, Writing practice, full Chat, and embedded Chat. Layouts were not edited to install it. Text-transport changes still go through `OpenAiProviderClient`, so vocabulary, Reading, Listening, Writing, and Chat remain on the existing client. Tests for those consumers passed in the focused filter and the full suite.

## Limits

The guard is not distributed. A response lost after dispatch stays uncertain. Self-practice audio is tab memory only. Staging and production were not changed.

## Repair 2026-11-11 (R1–R4)

Same branch, still `e6521ad` plus uncommitted work. No commit, push, PR, CI, merge, deploy, secret, schema, or gate change. BTH3 files were not edited. The local server on port 5100 was left running. Browser checks used the already signed-in local admin and page-level `fetch` interception. No paid provider call and no real microphone recording.

### Red, then green

| Check | Before the fix | After the fix |
| --- | --- | --- |
| R1 filter `OlderRelease\|OlderCompletion\|OlderInFlightSuccess` | Failed 4, passed 1, total 5. Release admitted a second `Probe`. Success and nontransient admitted `Allowed`. The held HTTP success then produced `provider_unavailable` where `circuit_open` was required. The stale-transient case was the one pass. | `AiOutageGuardTests` 18 passed, 0 failed |
| R2 `Speech429_StopsReadingAfterTheErrorBodyBound` | Failed 1. Counting content reported 2,097,191 bytes read; the assertion range was 1–8193. | Included in the 42-test guard and speech run below |
| R3 billing classification | The review reproduction returned `engine_busy` for `insufficient_quota` and the helper showed the learner-too-fast copy. This repair did not capture a separate red xUnit line before the mapping change. | Same 42-test run, plus the Node billing case |

Post-fix commands for this repair:

| Check | Result |
| --- | --- |
| `FullyQualifiedName~AiOutageGuardTests\|FullyQualifiedName~OpenAiSpeechTranscriberTests` | 42 passed, 0 failed |
| Repair-plan focused filter (provider, speech, RAG, quota, Speaking assessment, Writing) | 259 passed, 1 skipped (`LiveSilenceWav_IsNoSpeech_WithinBudget`), 0 failed, 260 total, about 52 s |
| `node --test scripts/tests/*.cjs` | 153 passed, 0 failed. The earlier full Node count of 152 did not include the billing UI case. |
| `node --test scripts/tests/test_ai_outage_ui.cjs` | 10 passed, 0 failed, including “billing quota offers self-practice…” |
| `git diff --check` | clean |

Live and MariaDB opt-in tests stayed off. The original full Release suite of 1201 passed / 22 skipped predates these R1–R3 regressions and does not prove them.

### Browser evidence

Route `/Speaking/Practice/53`, signed in. A synthetic oscillator `MediaStream` completed one self-practice take.

- During playback the choice was `self`, `#spk-self-playback` was visible, `#spk-self-audio` had `controls`, the score read `—`, and the status was “Bạn vẫn có thể luyện và nghe lại. Bản thu này chưa được chấm tự động.” Object URL `blob:http://localhost:5100/0822e14d-e618-430a-b823-4bd9dc83bb17`. Speaking API calls: 0.
- Clicking sentence index 1 revoked that URL and hid the playback. A separate browser pass for mode switch or leaving the page after a finished clip was not repeated; those paths stay covered by the script tests.
- Clicking **Tự luyện** switched the choice. Focus could move to `#spk-choice-self`. Space and Enter keydown reached that button (`Enter@spk-choice-self`) and left `aria-pressed` false and the choice `automatic`. The automation key did not produce the browser's native button activation. Keyboard activation of both choice buttons remains unobserved.
- Measured layout, not the requested emulation alone:
  - 1440×900 override: `innerWidth` 1440, `clientWidth` 1425, `innerHeight` 900. The choice group fit that viewport.
  - 768×1024 mobile override: `clientWidth` 768, `innerWidth` 948, `innerHeight` 1264. Choice group left 349, right 919. It fits the 948px inner width and extends past the 768px client width.
  - 390×844 mobile override: `clientWidth` 390, `innerWidth` 948, `scrollWidth` 948, `innerHeight` 2052. Choice group width 570, left 349, right 919. The document scrolls horizontally. The practice card measured about 570–628px. This is an existing shell overflow, not a restyled phone layout.
- `documentElement` and `body` zoom were set to 200% / `2`. Computed zoom stayed `1`, so a real 200% zoom was not observed. With that unchanged scale the choice group still fit the 1440px viewport (`groupRight` 972). `prefers-reduced-motion: reduce` matched.
- Intercepted automatic takes, 4 assess calls and 0 progress calls: 401 showed “Phiên đăng nhập đã hết hoặc bạn không có quyền.” and hid the self-practice offer; `engine_busy` 429 showed “Bạn đang gửi yêu cầu quá nhanh.” with the offer; `quota_exhausted` 503 showed “Tính năng AI tạm thời chưa sẵn sàng.” with the offer; `Failed to fetch` showed “Dịch vụ đánh giá đang gián đoạn.” with the offer.

Writing `/Home/Writing/Exercises?level=beginner&contentType=articles`: one intercepted `POST /Home/Writing/Exercises/CreateFromAi` kept the textarea “Bài học viên giữ lại khi AI lỗi”, showed “Dịch vụ đánh giá đang gián đoạn.”, and linked **Chọn bài có sẵn** to `/Home/Writing/Exercises?level=beginner&contentType=articles&topic=all&status=all#system-catalog-heading`. That page showed **Không tìm thấy bài tập phù hợp**.

Chat:

- `/AI/Chat`: one intercepted send threw `Failed to fetch`. Status became “Chưa xác nhận được kết quả. Hãy tải lại lịch sử trước khi gửi lại.” The draft stayed. A second wait did not send again. The SignalR invoke count stayed 0, so this was the HTTP path.
- `/AI/Chat?embed=true`, title “TCT AI”: the same unknown failure sent once and kept the history message. A later manual submit of an intercepted `circuit_open` 503 showed “Dịch vụ đánh giá đang gián đoạn.” and kept the draft. Total send count was 2, one per submit.

### R4-A/B/C closure — 2026-11-11

The earlier keyboard and width notes above used an emulation whose `innerWidth` stayed near 948. They are historical. The closure check used headless Chrome through Playwright, with a real viewport and trusted keys, on `http://localhost:5100/Speaking/Practice/53`. Speaking POSTs were intercepted. The microphone was a synthetic oscillator. No paid provider call was made. R1–R3 application code was not changed. No .NET rerun was required.

Script regressions failed before the fix: Enter on **Tự luyện** had `defaultPrevented === true`; `pagehide` had no listener and did not stop a held recording. After the fix, `scripts/tests/test_ai_outage_ui.cjs` passed 13/13, including mode-switch cleanup. The full Node suite before that last assertion passed 156/156. `git diff --check` was clean for the touched source.

Trusted keyboard on the focused controls:

- Enter on `#spk-choice-self` set `aria-pressed` true, the choice to `self`, and the label to “Thu và nghe lại”. Microphone calls stayed 0.
- Space on `#spk-choice-automatic` restored automatic scoring and “Kiểm tra phát âm”. Microphone calls stayed 0.
- Tab moved focus from `#spk-choice-self` to `#btn-record`. Shift+Tab moved it back. The sentence stayed `(Câu hỏi 1/11)`.
- Shift+ArrowRight while `#spk-choice-self` was focused did not change the sentence.
- Enter on `#btn-record` produced exactly one click, one synthetic microphone call, and the label “Đang ghi âm...”. Stopping that take caused one intercepted `POST /api/speaking/2012/assess`. No progress POST occurred.

Layout after the scoped practice CSS, measured `scrollWidth` against `innerWidth`:

| Viewport | innerWidth | scrollWidth | Workspace | Choice group | Sentence strip |
| --- | --- | --- | --- | --- | --- |
| 1440×900 | 1440 | 1440 | 696px, inside the main column | right 987 | track 570/570 |
| 768×1024 | 768 | 768 | 736px, right 752 | right 723 | track 610/610 |
| 390×844 | 390 | 390 | 358px, right 374 | right 349 | visible track 239, internal scrollWidth 492 |
| 390 dictation | 390 | 390 | 358px | pane switched | player 342px wide |

The sentence chips extend past the 390px viewport inside `#sentence-scroll-container`; the document itself does not scroll horizontally. CSS zoom computed `2` at 1440px and `scrollWidth` stayed 1440. `prefers-reduced-motion: reduce` matched.

Lifecycle in that browser: a self-practice clip showed controls, score `—`, and the unassessed status. Changing to sentence index 1 revoked it. Leaving for `/` raised the revocation count to 2. Back returned to `/Speaking/Practice/53` with the playback hidden, no blob URL, the label “Kiểm tra phát âm”, microphone count 0, and no additional Speaking POST. Chrome reloaded that history entry instead of restoring a persisted page. The script test covers `pageshow` with `persisted: true`: one listener, no second upload, and a usable choice control. A real microphone recording and a paid provider request were not made.

### R4-D status after lifecycle cancellation — 2026-11-11

`releasePracticeForLifecycle` stopped the recorder and the pending assessment, then left `#spk-assessment-status` on the active sentence. A held self-practice recording still said “Đang thu để nghe lại. Bản này không gửi đi chấm.” A held assessment still said “Đang gửi máy chủ nhận dạng...”. The new script assertion failed on that self-practice sentence before the status update.

The cleanup now records whether a recording or a send was in progress before it aborts. Only that case replaces the status with “Đã dừng thao tác trên trang này. Nếu bản thu đã được gửi, hãy tải lại kết quả trước khi thu lại.” A finished local clip that was only revoked says the clip was removed and the stored score remains. An idle stored result is left unchanged. A repeated `pageshow` does not restore the active sentence, and a late assessment response does not change the score.

Evidence:

| Check | Result |
| --- | --- |
| `R4-D lifecycle cancellation clears active recording and sending status` before the fix | Failed. Status stayed “Đang thu để nghe lại. Bản này không gửi đi chấm.” |
| `node --test scripts/tests/*.cjs` after the fix | 157 passed, 0 failed |
| Chrome `/Speaking/Practice/53`, synthetic microphone, assessment route held open | Idle status “Chưa có điểm đã lưu cho câu này.” and score `0` stayed after pagehide/pageshow. Self-practice status changed to the stop sentence, choice stayed `self`, microphone count stayed 1. The sending status changed to the same stop sentence, score stayed `0`, and the only Speaking POST was one intercepted `/api/speaking/2012/assess`. A second persisted `pageshow` and the late response left that status and score in place. |

No .NET rerun. No backend, schema, secret, or release change. The earlier limit remains: a real Back navigation reloaded the page instead of restoring a persisted document.

## Final owner-authorized review and publication preparation — 2026-11-11

R1-R4-D are independently verified closed. One further mode-notice defect was
reproduced and repaired by the reviewer: automatic -> self with no clip kept
the automatic notice. The new failing regression now passes; choosing self
renders an idle self-practice notice without changing stored scores or sending.

| Fresh verification | Result |
| --- | --- |
| Full Release application tests, test host .NET 9.0.16, `DOTNET_ROLL_FORWARD=LatestPatch` scoped to the invocation | 1210 passed, 22 opt-in skips, 0 failed, 1232 total |
| Full Node suite after final notice fix | 158 passed, 0 failed |
| Python scripts/tooling | 138 passed, 1 skipped, 139 total |
| Agent tooling validator | passed |
| Independent formerly failing status probe | passed, along with the then-current 14 UI tests |
| Current-source Chrome keyboard/playback/lifecycle/late-response/layout checks | passed with synthetic audio and intercepted assessment |
| Candidate credential/private-key/archive scan | 34 candidate files, no matching patterns; three unrelated BTH3 files excluded |
| Diff whitespace and protected config/schema/workflow scope | clean; no protected changes |

The first unrestricted local full test run crashed in the CLR while running on
.NET 10 (0x80131506), after 290 passes and one skip. It is an aborted run, not a
passing full suite. The repeat used the installed .NET 9 runtime, matching the
project target/CI, with the environment restored afterward. No project/runtime
configuration was edited to hide the failure. The cause of the .NET 10 CLR crash
was not established; .NET 10 runtime compatibility is not certified here.

Chrome verified the final status after held recording and assessment cancellation,
repeated persisted events and late response; tracks ended and no progress POST
occurred. A real navigation revoked its clip (session counter 1 -> 2); Back
returned with no blob. Actual BFCache reuse was not observed; persisted lifecycle
events were dispatched explicitly. Self audio played (synthetic 0.256 seconds).
Widths 1440/1366/768/390 matched document scroll widths, including 390 dictation.
Rendered CSS zoom 2 and reduced motion were observed. No real mic/paid AI request.

GitHub default is `master`; remote has no `main`. Before publication the actual
repository gates were `PLESK_STAGING_DEPLOYMENT_ENABLED=true` and
`PLESK_DEPLOYMENT_ENABLED=false`. Source publication and live staging evidence
will be attached to the resulting PR and owner handoff after CI completes; the
checks above alone do not claim a new staging deployment.

No persisted model/query-schema/shared content was changed, so this task needs
no new migration or content package. Runtime recordings, keys, browser state,
test results and private staging files remain ignored. Three unrelated BTH3
documents remain outside these commits. One agent performed the review/fix.
