# Chốt Plan 1 — Whisper local và chuẩn bị Plan 2

Ngày: 01/11/2026, UTC+7. Nhánh: `codex/whisper-local-lifecycle`.
Baseline HEAD: `03c9c5ce8d30e84a5320985b90255bbbccf922ec`.
Một agent Codex kiểm tra, sửa và xác minh trực tiếp theo quyền owner mới nhất.
Diff vẫn local, chưa commit/push/CI remote/PR/merge/deploy.

## Kết luận và phạm vi

**Plan 1 đủ điều kiện chuyển sang triển khai Plan 2 ở local.** Không còn lỗi
chặn trong phạm vi lifecycle, artifact/probe contract và luồng app→Whisper đã
kiểm tra. Các review WH01–WH04 và WR01–WR02 trước đó là lịch sử; bằng chứng
chốt trong tài liệu này cập nhật kết quả mới, không xóa các hạn chế nghiệm thu.

Ứng dụng chưa chuyển từ Gemini sang OpenAI. Target Plan 2 của **website** vẫn
là `gpt-6-luna` + `reasoning.effort=low`; Whisper xử lý audio. Owner chọn
**Codex GPT-6.1 Sol high làm agent thực thi**, không đưa Sol/high vào API web.
Plan 2 chỉ được thực thi khi owner giao bước đó; phiên này chốt Plan 1 và sửa
tài liệu chuẩn bị. Production/Plesk/gate không thay đổi.

## Review Cursor và sửa bổ sung

| Hạng mục | Kết quả chốt |
| --- | --- |
| WH01 — ownership runtime | Dùng OS executable path, creation time và artifact đã verify; commandLine trong state không cứu path sai. Stop/failed-start cleanup chỉ nhắm process đã xác minh. Regression pass. |
| WH02 — teardown registry | Không kill theo state giả. Registry độc lập dùng PID/path/start, victim do test tạo; không đụng System/process owner. Regression pass. |
| WH03/WR02 — readiness schema | JsonDocument giữ đúng root/array kinds; error/malformed/root array/primitive segment/end-before-start/segments rỗng không có text bị reject. Silence có text rỗng hợp lệ vẫn ready. Regression pass. |
| WH04 — DLL provenance | Lock pin b5454, model, required DLL và 9 CPU backend DLL; thiếu/sai/thừa bị từ chối. Offline Verify và tamper regression pass. |
| WR01 — registry timestamp | So OS path + creation time, skew 2s; timestamp cũ/thiếu/sai không tạo taskkill request. Repro và full teardown regression pass. |
| Lỗi bổ sung — deadline readiness | Trước sửa, deadline 1s vẫn nhận success sau 2.717761s. Probe có timeout riêng/minimum 5s. Đã dùng Stopwatch chung, cap timeout/sleep theo thời gian còn lại và từ chối success trễ. Hai regression loopback đỏ→xanh. |

Sửa trực tiếp trong `scripts/local-speech.ps1`; regression mới
`scripts/tests/test_speech_readiness_deadline.py`; cập nhật runbook và bug log.
Không sửa Program.cs, appsettings, csproj, adapter C# runtime hoặc persisted model.
Không cần migration cho các thay đổi này.

## Bằng chứng ứng dụng với Whisper thật

Thêm `TCTEnglish.Tests/WhisperLiveAppAcceptanceTests.cs`, opt-in qua
`TCT_ASR_LIVE_BASEURL` và `TCT_ASR_LIVE_SPEECH_WAV`. Discovery không gọi engine;
thiếu opt-in thì skip, cấu hình explicit sai/file thiếu/engine lỗi thì fail.

Test dùng authentication/antiforgery của app, DI `WhisperHttpTranscriber` thật,
HTTP assess cho 3 câu rồi progress, reload và replay receipt. Assertions kiểm
assessment engine whisper.cpp, điểm lưu, video hoàn tất và **chính xác một
completion credit**. Database SQLite riêng của TestWebApplicationFactory;
không đổi dữ liệu dev/hosting hoặc hardcode điểm để pass.

Fixture speech tạo offline bằng Windows SAPI, Microsoft David Desktop en-US,
câu “Good morning everyone”, PCM16 mono16kHz, 2.09s, 66,926bytes. Whisper nhận
đúng câu. SHA256:
`8c7f20e48d19fbcbe3f3f9025d799c4a68a23766c62db4a7e8b372069372d757`.
WAV/provenance chỉ dưới `.tmp/whisper-final-check/`, không đưa audio vào Git.
Không dùng Google/OpenAI TTS hoặc API key.

## Checks thực sự chạy trong lần chốt

| Check | Kết quả | Evidence local ignored |
| --- | --- | --- |
| Python full scripts suite | **88 passed, 0 failed** (135.139s) | `.tmp/whisper-final-check/python-final.log` |
| Node agent setup | **9 passed, 0 failed** | `.tmp/whisper-final-check/node-final.log` |
| `uv run --no-project python scripts/agent_tooling.py` | pass | `.tmp/whisper-final-check/tooling-final.log` |
| Release build + targeted real speech-through-app test | build succeeded; **1 passed** | `.tmp/whisper-final-check/app-live-speech.log` |
| Full Release application suite với explicit live ASR env | **743 passed, 0 failed, 3 skipped / total746** | `.tmp/whisper-final-check/application-final.log`, `application-final.trx` |
| Live silence và live speech-through-app | cả hai được thực thi trong full suite, không skip | TRX/full log ở trên |
| Stale registry start_time repro | không taskkill request | `.tmp/whisper-final-check/registry-final.log` |
| Total startup deadline regression | red trước sửa, **2 passed** sau sửa | `.tmp/whisper-final-check/deadline-red.log`, `deadline-green.log` |
| Offline artifact Verify | verified executable/model/DLL lock | helper `-Action Verify -Offline -SkipLegacyCache` |
| Daily launcher | MariaDB ready → Whisper ready → web ready; health **200** | `.tmp/whisper-final-check/launcher-smoke-retry.log` |

Ba test MariaDB acceptance bị skip là dual Speaking completion, badge batch và
expired worker lease. Không có provider acceptance mới cho những test này trong
phiên này. Không gọi full suite là “toàn bộ acceptance pass”. Release build còn
5 warning có sẵn của fixture/nullability/EF1002; không có build error mới.

## Đo nhanh trên máy Châu

Máy i5-11400H, 16GB RAM; engine CPU tiny.en, 2 threads, không cần GPU.
Ba mẫu mỗi độ dài, audio synthetic lặp từ fixture ở trên; đo sau full suite.

| Clip | Thời gian 3 mẫu (giây) | Median | Sample p95 |
| --- | --- | --- | --- |
| 5s | 2.615 / 2.472 / 2.600 | 2.600s | 2.615s |
| 10s | 2.752 / 2.515 / 2.303 | 2.515s | 2.752s |
| 15s | 13.066 / 11.121 / 12.009 | 12.009s | 13.066s |

Process lifetime PeakWorkingSet64 ghi nhận **166.7MiB**, WS cuối các lượt
144–155MiB, RAM máy còn4.8–4.9GiB lúc đo. Evidence:
`.tmp/whisper-final-check/benchmarks.json`. Mẫu nhỏ không phải p95 thống kê ổn
định hoặc độ chính xác với người thật. Không phải đo tải có kiểm soát đồng thời
web+MariaDB+IDE. Clip15s khá sát ngân sách inference15s; chờ hàng đợi/tải máy có
thể làm timeout. Không tự đổi model, dùng GPU hoặc tăng deadline để che kết quả.

## Trạng thái local lúc handoff

Lệnh thực tế: `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`.
Lần đầu Docker Desktop cold start vượt timeout60s của launcher; sau Docker sẵn
sàng đã chạy lại thành công. Không restart/xóa container hay volume dự án khác.
Đây là giới hạn cold start của môi trường, không tính lần đầu là pass.

- MariaDB `tctenglish-mariadb-local`: healthy, bind `127.0.0.1:3307`.
- EF update: **No migrations were applied. The database is already up to date.**
- Whisper PID **13092** lúc handoff, `http://127.0.0.1:8092`, tiny.en b5454.
  PID13360/24532 trong ledger cũ là lịch sử. Luôn dùng Status thay vì tin PID cũ.
- Web PID **2748** lúc handoff, `http://localhost:5100`, `/health/ready` trả
  HTTP200 `{"status":"ready"}`. Health này chứng minh DB/app; speech kiểm riêng.
- Web/MariaDB/Whisper được giữ chạy để owner test. Ctrl+C phiên launcher chỉ
  dừng web; dừng Whisper bằng `scripts/local-speech.ps1 -Action Stop`.

## Phần chưa nghiệm thu và bước tiếp

- Browser mic thật, quyền mic, accent/độ ồn của người dùng: **Not run**.
- Accuracy benchmark với người thật và tải đồng thời có kiểm soát: **Not run**.
- MariaDB concurrency acceptance, staging recovery, production deploy: **Not run**.
- Live OpenAI/API key/model access/quota: thuộc Plan 2, **chưa chạy**.

Những mục này không ngăn triển khai provider local ở Plan 2; không được suy ra
chất lượng pronunciation hoặc production đã được nghiệm thu. Agent Plan 2 giữ
diff Plan 1 và các workload hiện có, không checkout/reset làm mất thay đổi.

- [Plan 2 — website Luna low, audio Whisper](../superpowers/plans/2026-11-01-openai-provider-migration-plan.md)
- [Prompt Codex thực thi Plan 2](../../.ai/prompts/codex-openai-provider-migration.md)
- [Runbook speech và lệnh live acceptance](../operations/local-speech.md)
