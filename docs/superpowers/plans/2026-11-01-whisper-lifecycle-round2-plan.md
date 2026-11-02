# Plan sửa vòng2 — cleanup identity và inference schema

Owner follow-up01/11/2026 cấp quyền Codex kiểm tra và sửa trực tiếp để bắt đầu
Plan 2. Bước chốt: xác nhận WR01/WR02, thêm regression deadline readiness nếu
probe chậm còn vượt hạn; sửa tại chỗ, chạy checks và ghi ledger cuối. Plan/prompt
OpenAI giữ target ứng dụng GPT-6 Luna low; Codex GPT-6.1 Sol high là agent thực thi
theo lời đính chính của owner; chưa thực thi
provider migration, không commit/push/deploy. Không dừng ở một plan sửa khác.

Ngày01/11/2026. Scope local Plan 1. Một agent Cursor Auto.
Nguồn: [rereview WR01/WR02](../../feature-audits/2026-11-01-cursor-whisper-lifecycle-rereview.md).
Thực thi khi owner gửi prompt sửa. Giữ branch/diff hiện có, không reset/stash hoặc
đổi checkout làm mất việc. Không commit/push/PR/merge/CI remote/deploy/Plan 2.
Không đổi DB/schema, Gemini/OpenAI, model/binary version, Plesk hoặc trust.

## T1 — WR01: xác minh registry creation time trước cleanup (P2)

Files: scripts/tests/test_local_speech.py, fixture helper nếu cần.

- [x] Test đỏ trước: registered PID/path đúng với victim do test tạo nhưng
  start_time cũ. Mock chặn lệnh kill và khẳng định không có kill request. Không
  dùng PID System hoặc process của owner. Luôn cleanup victim bằng handle riêng.
- [x] Đọc OS executable path + creation time nhất quán, so với registry tại
  creation. Mismatch/missing phải từ chối. Dùng UTC ticks/format ổn định để tránh
  lỗi locale/độ chính xác; không tùy tiện chấp nhận timestamp bất kỳ vì path trùng.
- [x] Kill bằng handle/identity đã xác minh; tránh đọc một PID rồi reacquire
  không kiểm tra trước taskkill. Không suy authority từ state đang sửa để test.
- [x] Test thật teardown, không chỉ `_kill_registered`: state bị đổi PID, registry
  stale, JSON state hỏng, assert fail trước normal cleanup. Victim còn sống, fixture
  thật được dọn, không orphan. Thay assertion `x or not x` bằng điều kiện có nghĩa.
- [x] Giữ các happy paths/concurrent/foreign-port tests. Không thêm bypass hoặc
  bỏ cleanup để khiến suite xanh.

## T2 — WR02: validator theo JSON contract (P2, độc lập T1)

Files: scripts/local-speech.ps1, scripts/tests/test_local_speech.py,
scripts/tests/fixtures/fake_whisper_server.py. Adapter C# chỉ đọc làm contract.

- [x] Đọc MapVerboseJson/TryReadSegment hiện tại bằng CodeGraph; không sửa adapter
  chấp nhận schema lỏng hơn. Dùng JsonDocument hoặc cách giữ đúng root/array kinds;
  tránh pipeline unroll của ConvertFrom-Json.
- [x] Root bắt buộc object; error field hoặc thiếu cả text+segments phải reject.
  Có text thì bắt buộc string. Có segments thì bắt buộc array, từng item object.
- [x] Theo đúng optional segment fields của adapter: text missing/null được phép
  thành rỗng, text kiểu khác reject; start/end nếu có là finite number, end>=start
  với defaults hiện hành. Không tự yêu cầu thêm field mà adapter cho phép thiếu.
- [x] Quy tắc silence: missing text + zero segments phải reject; text rỗng rõ
  ràng là silence hợp lệ; segment-only có segment hợp lệ vẫn được phép.
- [x] Table-driven regression phải có bốn payload của review, segment.text số,
  timestamp string/overflow và invalid root/null/HTML/error; positive text-only,
  valid silence, valid segment-only và optional fields. Test đỏ trước rồi sửa.
- [x] Loopback HTTP test cho một schema sai ngoài `error`; Start/reuse/Status/
  Benchmark đều không ready hoặc không ghi sample thành công. Legitimate fake
  silence và live Whisper vẫn ready. Không inject lỗi vào owner port8092.

## T3 — Verify và handoff (sau T1/T2)

- [x] Chạy lifecycle suite Windows và full Python scripts; validator + Node setup.
- [x] Chạy Release build/relevant Speaking/Whisper tests, live silence với explicit
  loopback opt-in. Không rerun full app suite chỉ để tăng số test nếu không sửa
  C#/app behavior. Nếu có thay đổi mới làm rộng blast radius thì mở checks phù hợp.
- [x] Repro stale timestamp không còn kill request;4 schema payload sai bị reject;
  valid payloads vẫn accepted; WH01/WH04 không regress. Chỉ dùng fixture an toàn.
- [x] Artifact Verify và Status ready cho engine owner; không stop/restart nó
  chỉ để có test đẹp. git diff --check; review diff inline, update bug memory sau fix.
- [x] Cập nhật ledger remediation vòng2 với checks/số pass/skips thực và link
  rereview. Giữ các gate app→credit/overflow/mic/speech benchmark Not run cho đến
  khi thực sự chạy; hướng dẫn owner thử rõ ràng. Những bước acceptance trước đó
  còn trong plan vòng1, không cần tạo tính năng mới để thay việc nghiệm thu.
- [x] Báo tiếng Việt file/sửa lỗi/checks/process/remaining gates. Dừng cho Codex
  review lại, chưa tự sang OpenAI hoặc publish Git.

Acceptance: registry stale cùng executable bị từ chối cleanup; validator đồng
nhất với app trên các fixture; các case khỏe/live không bị hỏng. Không process
ngoại lai bị dừng, không sửa DB/model/secrets hoặc thay môi trường production.
