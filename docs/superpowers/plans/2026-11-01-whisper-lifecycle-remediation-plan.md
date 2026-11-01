# Plan sửa sau review Whisper lifecycle

Ngày01/11/2026. Scope: Plan 1, chưa chuyển OpenAI. Một agent Cursor Auto.
Nguồn: [review WH01–WH04](../../feature-audits/2026-11-01-cursor-whisper-lifecycle-review.md).
Base thực thi: current feature branch + implementation local của Cursor; không
reset/pull/check out branch khác để lấy tree sạch.
**Trạng thái: đã thực thi local remediation** — xem section Remediation trong
[ledger](../../feature-audits/2026-11-01-whisper-local-lifecycle-ledger.md).
Chưa commit/push; dừng để Codex rereview.

## Mục tiêu và ranh giới

Sửa4 lỗi ownership/cleanup/readiness/artifact, hoàn tất bằng chứng Plan 1 có thể
thực hiện và báo rõ gate còn Not run. Không commit/push/PR/merge/remote CI/deploy,
Plesk/gate, Gemini/OpenAI, framework/model upgrade hoặc reset DB. Bảo toàn process
Whisper owner đang chạy và tài liệu BTH2/prompts có trước. Fixture test được tạo
riêng, không dùng instance owner để cố ý làm hỏng state/DLL/response.

## R0 — Baseline và test cleanup trước (P1, prerequisite cho full suite)

- [ ] Đọc review, active sections của Plan 1, AGENTS và skill liên quan; CodeGraph
  trước indexed source. Ghi dirty paths và fingerprint các file ngoài scope.
- [ ] `scripts/tests/test_local_speech.py`: thay teardown lấy PID từ state bằng
  registry/handle fixture được ghi độc lập tại creation, kèm OS path/start time.
  Kill chỉ các fixture process đã chứng minh thuộc test. Cleanup trong finally
  kể cả assertion fail. Không taskkill PID4/System hoặc kill theo image name.
- [ ] Thay negative test PID4 bằng child/victim được test tự tạo, identity khác
  fixture engine; assert Stop từ chối và victim vẫn sống. Cleanup victim bằng
  handle riêng. Mock kill API để test state giả bất kỳ không tác động process thật.
- [ ] Regression WH02: state đổi PID, JSON hỏng/mất file, assert fail sớm và test
  aborted; không kill process ngoại lai, không để fake server mồ côi. Chạy focused
  tests này trước khi mở full Python discover.

## R1 — Ownership fail closed (P1, sau R0)

File: scripts/local-speech.ps1, fixture tests.

- [ ] Lấy expected executable/runtime từ artifact/argument đã kiểm tra độc lập;
  không dùng state.runtimeFileName để cấp quyền cho chính state. Missing/mismatch
  OS path/start time phải từ chối. Không dùng metadata command line để ghi đè
  kết luận sai executable từ OS. Xử lý wrapper test bằng contract explicit hẹp.
- [ ] Cả Start reuse, Status, Stop và failed-start cleanup dùng cùng invariant.
  Dùng process handle/captured identity đúng thời điểm; không reacquire PID rồi
  kill nếu identity thay đổi. Kiểm model/port/checkout consistency, không mất
  ownership của process cũ khi một Start mới lỗi.
- [ ] Test đỏ trước: actual OS path=pwsh/python/victim, expected=whisper, state
  command line cố ghi đường dẫn whisper; phải false. Test missing actual path,
  timestamp mismatch/PID reused, runtime field bị sửa, stop idempotent sau exit.
- [ ] Test khỏe: native pinned engine start/status/stop, wrapper fixture hợp lệ,
  restart/reuse/concurrent start không bị làm hỏng. Không kill engine owner để test.

## R2 — Pin toàn bộ CPU backend dùng thật (P2, sau R0)

Files: tooling/whisper/artifacts.lock.json, scripts/local-speech.ps1, tests.

- [ ] Derive SHA256 các backend từ ZIP b5454 đã đúng SHA lock; không pin theo
  cache đã sửa. Ghi danh sách runtime DLL có thể load, gồm CPU backends; reject
  missing/tampered và backend plugin không thuộc tập cho phép.
- [ ] Verify tại setup/cache reuse và trước execute. Mismatch không execute;
  chỉ thay artifact trong owned workspace cache, không xóa folder tùy ý/reparse.
- [ ] Regression: một CPU DLL3bytes, mất CPU DLL, thêm backend lạ và legacy cache
  có CPU DLL hỏng đều không vượt gate. Correct ZIP/cache vẫn dùng offline được.
- [ ] Giữ binary/model pin hiện hành; không giải quyết bằng download latest hoặc
  bỏ hết CPU backend vì cần chúng cho máy không bật GPU.

## R3 — Readiness/Status/Benchmark theo inference contract (P2, sau R0)

- [ ] Dùng validation chung cho body: object JSON, không error, transcript/segment
  đúng type theo WhisperHttpTranscriber; test text/segments/timestamps sai kiểu.
  Legitimate silence response được ready. HTTP200/error/HTML/null/{} không ready.
- [ ] Áp dụng cùng validator cho cold start, reuse, Status và Benchmark. JSON
  failure có message chẩn đoán, không ghi số sample thành công. Deadline probe
  không kéo dài vô hạn readiness; fail startup cleanup chỉ owned child.
- [ ] Thêm fake server modes và real loopback HTTP regression. Không chỉ mock
  function trả Ok=true vì cần test cả serializer/probe nhận body.
- [ ] Test kill/retry có hạn khi cold fail; unknown/foreign port vẫn được giữ.
  Tái sử dụng owned engine đang unhealthy phải có policy rõ, không spawn trùng
  rồi overwrite state của process cũ.

## R4 — Hoàn tất acceptance và sửa bằng chứng đo lường (sau R1–R3)

- [ ] Đổi PeakWorkingSetMiB hiện là sample cuối thành tên đúng hoặc đo peak bằng
  PeakWorkingSet64/sampling có kiểm chứng; cập nhật header/ledger tương ứng.
- [ ] Kiểm logs startup thực: WMI hiện chưa redirect stderr. Cung cấp log engine
  hữu ích hoặc ghi chính xác giới hạn chẩn đoán, không hướng owner tới log chỉ có
  marker. Khi đổi spawn giữ hidden/detached và CLI return không bị pipe treo.
- [ ] Test launcher như behavior: DatabaseOnly không speech, SkipWhisper không
  đảm bảo ASR, NoBrowser vẫn speech, child env đúng rồi restore, cổng web bận không
  kill app. Không thay test này bằng string assertions đơn thuần.
- [ ] Chạy live speech+silence với fixture/provenance, app assess→receipt→progress→
  credit và reload/replay không lặp; đổi câu/hủy/timeout; overflow. Dùng tài khoản
  local test, disposable provider DB nếu cần; giữ dev data và hosting nguyên vẹn.
- [ ] Benchmark speech5/10/15s có web+DB+IDE, ghi số mẫu/p50/p95/CPU/RAM actual.
  Silence-only ghi riêng. Mic permission/browser nếu chưa thao tác được thì ghi
  Not run và bước owner kiểm, không fake pass và không mở Plan 2 tự động.

## R5 — Verify, review inline và handoff

Sau cleanup an toàn và các source fix cuối:

```powershell
uv run --no-project python -m unittest scripts.tests.test_local_speech -v
uv run --no-project python -m unittest discover -s scripts/tests -v
uv run --no-project python scripts/agent_tooling.py
node --test scripts/tests/test_agent_setup.cjs
dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --no-build
git diff --check
```

- [ ] Live ASR bằng explicit loopback env, không HTTP trong discovery; opted-in
  lỗi phải fail. MariaDB acceptance không được trỏ DB dev để làm test "đủ số".
- [ ] Rerun4 repro của review theo phiên bản regression an toàn: ownership=false,
  error JSON không ready, tampered CPU gate reject, teardown không kill foreign PID.
- [ ] Review toàn diff inline; cập nhật bug-fix-log đúng root cause sau fix; schema
  impact không có migration nếu không đổi persisted model. Không sửa lịch sử audit.
- [ ] Bổ sung remediation section vào ledger Plan 1, ghi số tests/skips thực và
  từng gate. Liệt kê process giữ chạy cho owner và cách Stop đã sửa, artifacts,
  commands local. Nêu remaining blocker, không lặp claim79/742 mà chưa chạy lại.
- [ ] Dừng cho Codex rereview. Không tự commit/push/merge hoặc chuyển OpenAI.

## Acceptance tổng

Không process không thuộc fixture/workspace bị dừng; pin backend đầy đủ; trạng
thái ready phản ánh inference schema hợp lệ; negative tests không tự nguy hiểm;
happy paths/live adapter/build/regressions pass. App/browser/performance chưa
test thì giữ Not run. Owner quyết định khi nào chấp nhận đủ để bắt đầu Plan 2.
