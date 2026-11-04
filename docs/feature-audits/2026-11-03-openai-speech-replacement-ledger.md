# Ledger — OpenAI Speech-to-Text thay Whisper

Ngày: 2026-11-03. Plan: `docs/superpowers/plans/2026-11-03-openai-speech-to-text-replacement-plan.md`.

## Baseline (STT0) — pass

| Mục | Giá trị |
| --- | --- |
| Branch | `feature/chau-ui-performance-audit` |
| HEAD | `210bf2de12051db6238d4f53214aba4b73c3ce29` |
| Dirty/untracked | ~131+ entries preserved (không reset/stash/commit baseline) |
| Official model | `gpt-transcribe` → `POST /v1/audio/transcriptions` |
| Text model | `gpt-6-luna` / `low` (không đổi) |
| Startup before/after | missing same-condition baseline; chứng minh 0 local ASR spawn / 0 paid startup by design + launcher tests |

### Inventory replaced

WhisperHttpTranscriber, SpeechProcessingOptions Whisper URLs, Program DI, importer
fixed chunks+provider segments, Youtube source label writes, start-local Whisper
start, local-speech.ps1, tooling/whisper lock, fake whisper fixtures, Whisper live
tests, agent tasks Whisper status/stop, AGENTS/ops docs Whisper workflow.

### Historical allowlist (không blanket xóa)

Audit/plan/bug logs 2026-11-01 Whisper*; DB/history `whisper-audio`; Premium fixture
source string read-compat; ignored `.appdata/local/speech` cache unused.

---

## STT1 — options / credential resolver — pass

Files: `SpeechProcessingOptions.cs`, `OpenAiSpeechCredentialResolver.cs`,
`OpenAiSpeechCredentialResolverTests.cs`, `appsettings.json` Learning:Speech
(không ApiKey để null≠explicit empty).

Checks: precedence, explicit empty/invalid no fall-through, disabled, BaseUrl HTTPS/loopback.

---

## STT2 — OpenAiSpeechTranscriber — pass

Files: `OpenAiSpeechTranscriber.cs`, `OpenAiSpeechTranscriberTests.cs`,
`SpeakingInferenceLimiterTests.cs` migrated.

Contracts: multipart `speech.wav`+model; Bearer only; strict text JSON; empty→NoSpeech;
401/403/404→engine_auth; 429→engine_busy; response cap; cancel/lease; 1 attempt;
AllowAutoRedirect=false; EngineName=`openai-audio`.

---

## STT3 — Speaking assessment DI + guard — pass (R6 eviction)

Files: `SpeakingAssessmentAdmission.cs`, `SpeakingAssessmentService.cs`,
`SpeakingAssessmentAdmissionTests.cs`, `TestWebApplicationFactory` blocked credentials,
`Program.cs` DI + TimeProvider.

Checks: access→validate→per-user 10/min→transcribe; receipt EngineName/model;
factory blocks outbound even with host OPENAI_API_KEY. R6: expired buckets
evicted; `MaxTrackedUsers=16384`; capacity refuse without dropping active users.

---

## STT4 — PCM windows + import — pass (R1/R2 repair 2026-11-03)

Files: `PcmSpeechWindowPlanner.cs`, `AudioTranscriptImporter.cs`,
`AudioImportOptions.MaxTranscriptionRequests`, `YoutubeTranscriptService`
source `openai-audio-vad`, planner/importer tests, live import flag→`TCT_OPENAI_SPEECH_LIVE`.

Limits: window timing = sampleIndex/16000 (+padding), max 10s/window, not word
alignment; request cap 180; all-or-nothing; captions→0 cloud.

**R1 repair**: padding overlaps are union-normalized before hard ≤10s split;
importer validates every window before the first provider HTTP call (overlap →
0 HTTP). **R2 repair**: energy threshold clamped so noise×3.5 cannot exceed peak
in the varying-amplitude synthetic band; silence/quiet/noise/continuous kept.

---

## STT5 — launcher — pass

Files: `start-local.ps1` (no Whisper/FFmpeg auto), `speech-status.ps1` metadata-only,
retired `local-speech.ps1` + whisper lock/fixtures, `test_local_launcher.py`,
`pwsh_util.py`.

Checks: fake launcher 0 local ASR spawn; key restore; busy port before build;
speech-status no network.

---

## STT6 — tests/docs/agent clients — pass

Files: `agent_clients.py` + generated tasks; AGENTS.md, `.ai/README.md`, README,
quickstart/onboarding/group message/local-speech/openai-local-setup/speaking-import/
team-agent-setup; `check-local.cjs` (VC++ Whisper hard-req removed); script tests updated.

Live flag: `TCT_OPENAI_SPEECH_LIVE=1` (loopback Whisper flags must not enable cloud).

---

## STT7 — verification — pass (offline + fake browser) / live unverified

Review-repair retest 2026-11-03 (Debug; Release apphost still avoidable):

| Suite | Result |
| --- | --- |
| Focused R1–R7 speech (Debug) | **66 passed / 0 failed / 1 skipped** (then +1 capacity → admission 4/4) |
| Full .NET Debug | **887 passed / 0 failed / 12 skipped** (899 total) |
| JS speaking/recorder/import | **12/12** |
| Python scripts discover | **Ran 94, OK skipped=1** (93 executed pass — not 94+1) |
| agent_tooling.py | verified |
| test_agent_setup.cjs | 9/9 |
| EF has-pending-model-changes | No changes since last migration |
| git diff --check | no whitespace errors (CRLF warnings on pre-existing dirty UI files) |
| Secret scan on new speech paths | no `sk-…` literals found |
| Live OpenAI speech | **not run** (flag off; owner opt-in required) |
| Browser (fake ASR) | **pass** — Debug `http://127.0.0.1:5130` + loopback fake `http://127.0.0.1:18765/v1/` with process-local key `fake-local-speech-key-not-paid`; Admin login; Speaking catalog + Practice/44; assess→commit→abort-cancel→replay via fake transcript; Admin Create copy OpenAI Speech-to-Text (no Whisper); viewport 390px; fake POST count advanced; **no paid OpenAI** |
| Startup cold/warm % | missing valid baseline; launcher/status prove 0 Whisper spawn / 0 paid speech at start |

Skipped include: OpenAiSpeechLive, AudioImportLive, OpenAi text live, MariaDB acceptance, LocalCatalogScale.

Old Release :5112 listener was already CLOSED (PID 7084 had residual DB socket only); new-source evidence uses Debug :5130 + fake ASR, not the old binary.

### Review repair R1–R7 — pass (offline)

| ID | Status | Evidence |
| --- | --- | --- |
| R1 | fixed | planner overlap union→split; importer pre-validate 0 HTTP on bad windows; real-planner+fake-provider import green |
| R2 | fixed | varying-amplitude regression nonempty; threshold clamp vs peak |
| R3 | fixed | shared `OpenAiSpeechLiveBudget` + HTTP handler; live silence + import wired; fake 8th OK / 9th blocked / 120s cap / cross-client |
| R4 | fixed | explicit empty/invalid shared AI key fail-closed; defaults omit empty ApiKey; credentials ToString redacted |
| R5 | fixed | IOException after 200 → unavailable/503; lease released |
| R6 | fixed | expired-user eviction; TrackedUserCount; MaxTrackedUsers=16384; capacity refuse without evicting active |
| R7 | fixed | `speech-status.ps1` Enabled/Model/BaseUrl/key precedence + KeyPresent; Admin Create OpenAI copy |

---

## STT8 — self-review / handoff — pass (repair close)

- No Whisper runtime callers remaining in active DI/launcher/tasks.
- Captions/SRT/VTT learner path unchanged (0 cloud audio).
- Shared live budget exists before any owner live flag enablement.
- No schema change; deploy gate untouched; no commit/push/PR.
- Bug log + known-issues updated for R1–R7.

### Owner gates trước 25/11

1. Opt-in live: `$env:TCT_OPENAI_SPEECH_LIVE='1'` (shared budget ≤8 POST / ≤120s audio — still unpaid until flag).
2. Optional: browser with real mic on paid key after offline review; Standard/Premium accounts beyond Admin local seed.
3. Teammate: một key local + `speech-status.ps1`; FFmpeg chỉ khi import.
4. Commit/PR/CI/merge/deploy chỉ khi owner ủy quyền riêng.

---

## Reviewer recheck + direct fixes — 04/11/2026

Owner yêu cầu review lại Cursor và fix trực tiếp nếu còn lỗi. R1–R7 đã được
đối chiếu source qua CodeGraph. Còn thiếu ở R3/R4/R7 và completion test đã được
reviewer sửa; xem [báo cáo mới](2026-11-04-openai-speech-rereview.md).

- **R3 follow-up:** WAV duration chỉ được tính sau xác minh RIFF/fmt/sample rate/
  byte rate/block align/chunk bounds; duplicate data/file bị chặn trước send.
  Giữ nguyên request content; stream payload và cross-client đúng120s/vượt120s
  được kiểm bằng fake transport. Không gọi paid API để test budget.
- **R4 launcher follow-up:** explicit-empty AI:ApiKey không fallback global;
  start-local không tạo key rỗng trên máy mới. Existing secret giữ nguyên;
  fixture fresh/existing-empty/startup-failure/port-busy xác nhận behavior.
- **R7 follow-up:** status đọc appsettings/Development/secrets/env đúng thứ tự;
  boolean sai không được configured, model trim, URL invalid redacted.
- **Completion proof:** fake HTTP trả từng transcript khớp fixture; DB assertions
  chứng minh1 credit/XP>0/1 completed video và replay không cộng thêm.
- **PCM:** thêm quiet-tone/near-silence-noise synthetic regressions; không coi
  đây là proof giọng người thật hoặc chất lượng nhận dạng cloud.

| Reviewer verification mới | Kết quả |
| --- | --- |
| Full .NET build/test optimized SpeechReview | **897 passed /0 failed /12 skipped**, tổng909 |
| JS recorder/Speaking/import | **12/12** |
| Python final scripts discover | **Ran99, OK skipped1** =98 executed pass |
| Native setup tests | **9/9** |
| agent_tooling.py / git diff --check | verified / sạch |
| Local speech metadata | configured, User Secrets AI:ApiKey, gpt-transcribe, VerifiedLive=false |
| GitHub deployment gate read-only | **false** |

Red→green evidence nằm ignored `.tmp/stt-rereview/`. Lượt Python đầu bắt2 test
launcher fail sau đổi key semantics; đã sửa root cause auto-empty và chạy lại
full99 test thành công. Không ẩn lần fail trung gian.

Reviewer không chạy lại browser, mic thật, paid live, provider acceptance hay
migration trong phiên này; browser fake ngày03/11 là evidence Cursor trước đó.
Không commit/push/remote CI/PR/merge/deploy. Có thể đóng phạm vi sửa local/offline;
paid speech/mic và readiness demo vẫn cần kiểm riêng theo owner gate.
