# Ledger — Plan 1 Whisper local lifecycle

Ngày: 01/11/2026, UTC+7. Agent: Cursor Auto (một agent).
Nhánh: `codex/whisper-local-lifecycle` (từ `master` `03c9c5c`).
Phạm vi: **local only** — không commit / push / PR / merge / deploy / Plesk.

## W0 baseline

| Hạng mục | Giá trị lúc thực thi |
| --- | --- |
| CPU | Intel i5-11400H, 6C/12T |
| RAM | 15.7 GiB total; free lúc đầu ~2.6 GiB, lúc benchmark ~3.0–3.7 GiB |
| Disk D | ~7.4 GiB free |
| Listeners lúc đầu | 5100 / 3307 / 8092 trống |
| Dirty sẵn có (giữ nguyên) | prompts/plans Whisper+OpenAI + `2026-10-31-bth2-report.md` (ngoài scope) |
| CodeGraph MCP | Không có tool `codegraph_explore` trong session; `.codegraph/` có index local, không rebuild |
| PowerShell 7 | Đã cài winget `Microsoft.PowerShell` 7.6.9 (MSIX). Alias `WindowsApps\pwsh.exe` 0 byte — automation phải dùng binary MSIX thật hoặc in-process PS7 |

## Pin artifact

Nguồn metadata: `tooling/whisper/artifacts.lock.json`

| Item | SHA256 | Ghi chú |
| --- | --- | --- |
| `whisper-bin-x64.zip` b5454 | `6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb` | Reuse `.tmp/asr-acceptance-2930` sau hash OK |
| `ggml-tiny.en.bin` | `921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f` | 77,704,715 bytes; MIT HF |
| `whisper-server.exe` | `a97456e747f28e7bb09c1dac35f40d94404c5ecffa74aaaaa7a3753ea6543121` | + DLL pin trong lock |
| Runtime | `127.0.0.1:8092`, threads=2, `-ng`, `en` | ModelIdentity `whisper.cpp-b5454-tiny.en` |

Cache runtime: `.appdata/local/speech/` (gitignored). Không commit binary/model.

## State machine / ownership

`absent → starting → ready → stopped/failed`
State: `.appdata/local/speech/state.json` (checkout, runId, PID, start time, paths, hashes, port, commandLine).
Start dùng **Win32_Process.Create** để tránh grandchild giữ pipe stdout của Python/`capture_output` (CLI không thoát).
Stop chỉ khi identity khớp; không `taskkill /IM`.

## Kiểm thử đã chạy

| Gate | Kết quả | Chi tiết |
| --- | --- | --- |
| `scripts.tests.test_local_speech` (7) | **pass** | setup/hash fail, ownership start/status/stop, concurrent reuse, foreign port, SkipWhisper contract |
| `unittest discover -s scripts/tests` | **pass** | **79** tests / 53.7s |
| `scripts/agent_tooling.py` | **pass** | working tree verified |
| `node --test scripts/tests/test_agent_setup.cjs` | **pass** | 9/9 |
| `dotnet build/test` Release | **pass** | **742** passed, **3** skipped (MariaDB acceptance env), 0 failed |
| Live `WhisperLiveSilenceAcceptanceTests` với `TCT_ASR_LIVE_BASEURL=http://127.0.0.1:8092` | **pass** | Adapter → `NoSpeech` / `no_speech` |
| Benchmark silence 5/10/15s (3 samples mỗi mức) | **measured** | Xem bảng dưới — **không** chứng minh độ chính xác tiếng nói |
| Full app assess→receipt→progress→credit / đổi câu | **Not run** | Cần web đăng nhập + fixture lời nói có provenance |
| Browser mic / permission | **Not run** | Không tự động hóa được |
| Overflow 1 running + 4 queued | **Not run** | Chưa đo trong đợt này |

### Benchmark (silence-only)

Cold Start wall (Setup reuse + Start + readiness): ~22s lần đo này.
Working set serving: ~164–168 MiB (khớp mốc tham khảo cũ).

| Clip | Samples (s) | p50 (s) | Peak WS MiB | Free RAM GiB |
| --- | --- | --- | --- | --- |
| 5s | 2.061, 2.329, 2.082 | 2.082 | 166.1 | 3.7 |
| 10s | 3.743, 3.584, 2.167 | 3.584 | 167.8 | 3.3 |
| 15s | 4.090, 2.444, 2.269 | 2.444 | 166.0 | 3.0 |

Interactive timeout app vẫn 15s; silence 15s p50 ~2.4s trên máy này khi Whisper đã warm. Threads vẫn 2; chưa đo 4.

## Launcher / docs

- `scripts/start-local.ps1`: DB → Whisper (mặc định) → web; `-SkipWhisper`, `-DatabaseOnly`, `-NoBrowser`; env `Learning__Speech__*` cho child rồi restore.
- `scripts/local-speech.ps1`: Setup/Start/Status/Stop/Benchmark.
- VS Code tasks (không folderOpen): Start local web + Whisper / Status / Stop — đồng bộ `scripts/agent_clients.py`.
- `AGENTS.md` + quickstart + `docs/operations/local-speech.md` cập nhật.

## Schema / deploy

- **Không** đổi EF entity/DbContext/migration.
- Production gate / Plesk / CI workflow: **không đụng**.
- Diff local **chưa commit**.

## Process còn chạy phục vụ owner

| Dịch vụ | PID (lúc ledger) | Endpoint | Stop |
| --- | --- | --- | --- |
| whisper-server | **13360** (vòng2 handoff; Status ready) | `http://127.0.0.1:8092` | `pwsh -NoProfile -File scripts/local-speech.ps1 -Action Stop` |

MariaDB/web: không bị kill bởi Plan 1; nếu cần full stack:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

## Review diff (inline)

- Không hardcode secret; lock chỉ metadata.
- Ownership/stop an toàn; foreign port refuse.
- Tránh `Get-NetTCPConnection` / `Get-CimInstance` trên hot path (stall máy này).
- Binary chỉ dưới `.appdata` / `.tmp` ignored.

## Blocker / residual cho Codex

1. Full E2E speaking WAV→credit và mic browser: Not run.
2. Speech-accuracy benchmark cần fixture tiếng Anh có license (silence không đủ).
3. Dừng Plan 1 tại đây; **chưa** sang OpenAI / Plan 2.

## Remediation WH01–WH04 (2026-11-01, sau review Cursor)

Nguồn: [review](2026-11-01-cursor-whisper-lifecycle-review.md),
[plan sửa](../superpowers/plans/2026-11-01-whisper-lifecycle-remediation-plan.md).
Không commit/push; owner Whisper **PID 24532** vẫn ready trên `:8092` sau đợt sửa.

### Sửa đã làm

| ID | Thay đổi |
| --- | --- |
| WH02 | Teardown dùng registry PID/path/start độc lập lúc Start; không taskkill theo state.json. Negative tests dùng victim child + mock, không PID4/System. |
| WH01 | `ExpectedRuntimePath` từ arg/artifact đã verify; OS path missing/mismatch → false; `state.commandLine` không cứu path sai. Stop re-check trước kill; failed-start cleanup qua `Stop-ProcessIfStillOwned`. |
| WH04 | `cpuBackendDlls` hash đầy đủ trong lock; verify thiếu/sai/thừa; Action `Verify` assert không auto-extract. |
| WH03 | `Test-InferenceResponseBody` theo contract adapter; probe.Ok = HTTP2xx ∧ schema; silence hợp lệ vẫn ready; error JSON không ready. |
| R4 | Benchmark dùng `PeakWorkingSet64` sampling + `WorkingSetSampleMiB`; docs ghi rõ log WMI chỉ marker. |

### Checks chạy lại trong đợt remediation (không tái dùng số cũ)

| Check | Kết quả |
| --- | --- |
| `unittest scripts.tests.test_local_speech -v` | **11 pass** / 0 fail |
| `unittest discover -s scripts/tests -v` | **83 pass** / 0 fail (~90s) |
| `scripts/agent_tooling.py` | **pass** |
| `node --test scripts/tests/test_agent_setup.cjs` | **9 pass** |
| `dotnet test` Release `--no-build` | **742 pass**, **3 skip** MariaDB acceptance |
| Live silence `TCT_ASR_LIVE_BASEURL=http://127.0.0.1:8092` | **1 pass** |
| Repro WH01/WH02/WH03/WH04 regressions trong test_local_speech | **pass** |

### Vẫn Not run

- App speech→assessment→progress→credit / reload-replay / overflow
- Mic/browser permission
- Speech-accuracy benchmark 5/10/15s (có provenance) dưới tải web+DB+IDE

### Schema

Không migration / không đổi persisted model.

### Handoff

Diff local chưa commit. Dừng để **Codex rereview**; chưa Plan 2 / OpenAI.

## Remediation vòng 2 — WR01/WR02 (2026-11-01)

Nguồn: [rereview](2026-11-01-cursor-whisper-lifecycle-rereview.md),
[plan vòng2](../superpowers/plans/2026-11-01-whisper-lifecycle-round2-plan.md),
[prompt](../../.ai/prompts/cursor-auto-whisper-lifecycle-round2.md).
Local only; không commit/push/PR/merge/deploy. Trước handoff engine đã
`stopped` (PID 24532 cũ không còn); Start owned lại → **PID 13360** `ready`
trên `http://127.0.0.1:8092` và giữ nguyên sau suite (không Stop chỉ để test).

### Sửa đã làm

| ID | Thay đổi |
| --- | --- |
| WR01 | `_kill_registered` đối chiếu OS path + creation time (skew 2s); thiếu/mismatch/stale → không taskkill. Test stale start_time + cùng executable (mock). Full `tearDown` với state.pid/startTime giả, bỏ tautology. |
| WR02 | `Test-InferenceResponseBody` dùng `JsonDocument`; hard-fail theo MapVerboseJson/TryReadSegment; Action `ValidateProbeBody`; table positive/negative (gồm 4 payload rereview + overflow/`text:null`); fake mode `segments-empty` loopback không ready. |

WH01/WH04 regressions giữ nguyên (Stop sai executable; CPU tamper Verify). Adapter C# không đổi.

### Checks mới (handoff Cursor Auto vòng2 — không tái dùng số review cũ)

| Check | Kết quả |
| --- | --- |
| `unittest discover -s scripts/tests -v` | **86 pass** / 0 fail (~154s) |
| `node --test scripts/tests/test_agent_setup.cjs` | **9 pass** |
| `uv run --no-project python scripts/agent_tooling.py` | **pass** |
| `dotnet test` Release filter WhisperHttpTranscriber\|SpeakingInferenceLimiter\|WhisperLiveSilence | **27 pass** / 0 fail / 0 skip (live silence owner `:8092`) |
| Artifact `Verify -Offline -SkipLegacyCache` | **pass** |
| Owner `Status` sau suite | **ready**, Pid **13360**, WS≈164 MiB |
| Repro WR01 stale start_time | không taskkill request |
| Repro WR02 4 payload sai + extras | Rejected; silence/segment-only hợp lệ Accepted |
| `git diff --check` (speech scripts) | pass |

### Vẫn Not run

- App speech→assessment→progress→credit / reload-replay / overflow
- Mic/browser permission
- Speech-accuracy benchmark 5/10/15s (có provenance) dưới tải web+DB+IDE

Hướng dẫn owner nghiệm thu thủ công: đăng nhập web local với
`Learning__Speech__WhisperHttpBaseUrl=http://127.0.0.1:8092`, ghi mic/WAV thật,
chạy assess→progress→credit và (nếu cần) overflow/benchmark lời nói có provenance.
Không tự động hóa trong đợt này.

### Schema

Không migration / không đổi persisted model / không đổi adapter C#.

### Handoff vòng2

Diff local chưa commit. Dừng để **Codex review lại**; chưa Plan 2 / OpenAI.

## Codex chốt review và sửa trực tiếp — 01/11/2026

Owner đã cấp quyền sửa những phần còn lỗi để bắt đầu Plan 2. Kết quả hiện hành:
[final closeout](2026-11-01-whisper-plan1-final-closeout.md). Các số PID/kết quả
vòng trước ở trên là lịch sử, không đại diện cho trạng thái lúc handoff mới.

- Xác nhận Cursor đã sửa WH01–WH04/WR01–WR02; registry/schema/tamper regressions pass.
- Sửa lỗi bổ sung: readiness deadline1s từng nhận HTTP success sau2.717761s.
  Dùng Stopwatch chung, cap probe timeout/retry sleep bằng phần còn lại, từ chối
  success trễ. Regression mới loopback delayed/fast:2/2 pass sau red trước sửa.
- Thêm live app acceptance opt-in: app auth/antiforgery→Whisper thật→3 assessments
  →progress→reload/replay→chính xác1credit, trên SQLite fixture riêng. Dùng speech
  Microsoft David tạo offline có provenance; không dùng/đổi dữ liệu local dev.
- Full Python **88/88**; Node **9/9**; tooling validator pass; Release build pass;
  full app **743 pass /0 fail /3 skip** MariaDB acceptance. Live silence và live
  speech-through-app được chạy. Không coi ba skip là pass.
- Benchmark synthetic 3mẫu/clip: median5s=2.600s,10s=2.515s,15s=12.009s;
  PeakWorkingSet64≈166.7MiB. Mẫu nhỏ, chưa đo mic người thật/tải đầy đủ;15s sát
  inference deadline15s. Xem report cho mẫu gốc/giới hạn.
- Launcher cold Docker lần đầu timeout60s; Docker ready rồi retry thành công.
  MariaDB3307 healthy, không có pending migration; Whisper8092 PID13092, web5100
  PID2748, health200 lúc handoff. Giữ services chạy; Status xác định PID hiện tại.
- Không đổi persisted model/Program/appsettings/csproj/provider runtime; không
  commit/push/CI remote/PR/merge/deploy/Plesk/gate.

**Plan 1 đủ cho triển khai Plan 2 local.** App speech→credit/reload/replay đã có
bằng chứng thật mới, thay cho trạng thái “Not run” ở các vòng trước trong phạm vi
synthetic speech + SQLite. Mic browser, human accuracy, MariaDB race/staging và
production vẫn Not run. Plan 2 ứng dụng giữ **GPT-6 Luna low**, owner dùng **Codex
GPT-6.1 Sol high thực thi**; kế hoạch/prompt đã sửa đúng đính chính, chưa migration
provider và chưa gọi OpenAI.
