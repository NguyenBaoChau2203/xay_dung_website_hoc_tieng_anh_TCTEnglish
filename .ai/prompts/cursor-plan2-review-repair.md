# Cursor Auto — sửa review và hoàn tất Plan2

Owner giao bạn **thực thi**, một agent inline, trên nhánh hiện tại
`feature/chau-ui-performance-audit`. Không cần làm lại UI Plan1. Website giữ
OpenAI `gpt-6-luna`/reasoning `low`, audio Whisper; model IDE độc lập.

Đọc có trọng tâm:

1. AGENTS.md, skill tct-superpowers + tct-engineering; CodeGraph trước indexed source.
2. `docs/feature-audits/2026-11-02-cursor-plan2-review.md`.
3. `docs/superpowers/plans/2026-11-02-cursor-plan2-review-repair.md`.
4. Ledger `docs/feature-audits/2026-11-02-chau-logic-performance-ledger.md`.
5. Batch đang làm trong `docs/superpowers/plans/2026-11-02-chau-logic-performance-plan.md`;
   API/security/EF/domain skills theo batch, DESIGN.md khi sửa UI integration.

Review hiện tại **chưa pass**: full suite mới816pass/13fail/8skip/837; 9 probe
reviewer tái hiện lỗi còn đỏ; chỉ4JSsubmit tests pass. Cursor ledger hiện ghi
mid-handoff/partial, không được coi đó là Plan2 đã xong.

Thực thi tuần tự RP0–RP6 theo repair plan rồi **tiếp tục BE4–BE8 và phần BE0/BE5
còn thiếu**. Không dừng chỉ sau một nhóm tests xanh. Mỗi batch regression red→fix
→green, kiểm diff và cập nhật ledger. Nếu cần nối phiên, ghi next step cụ thể.

Các lỗi cần hiểu đúng:

- R1: hai DbContext reserve rồi settle ghi đè stale tracked counter; cùng
  reservation settle2lần cộng quota2lần. Cần identity/state atomic+idempotent,
  unknown/recovery, proof MariaDB nhiều context/instance. Serializable không tự
  refresh entity đã tracking. Không static lock rồi claim distributed correctness.
- R2:15success logs vẫn được gọi provider do bảng mới khởi tạo0. Không reset
  historical usage, không sửa seed/expected test để che bug. Backfill/transition
  đúng token và UTC-day, không counttrùng.
- R3: Started gửi sớm chưa đủ. Actual Hub parallel1 khiến Stop xếp sau Start.
  Test qua WebSocket/TestServer thật, không chỉ gọi service Stop. Không tăng global
  parallel mà bỏ qua class chat/scoped DbContext; không untracked Task.Run.
- R4: cleanup hở ngay sau prompt SaveChanges trước provider và sau reply/save/
  settle. State-aware cleanup token bounded, giữ exception/lease và reply đãcommit.
- R5: cap30/50 đã đổi consumer cũ nhưng chưa có endpoint/Loadolder. Phải có
  đường lấy đủ history, stable cursor và scroll/focus; context theo budget.
- R6/R7: prefix+lengthguard vẫn nhận malformed bcrypt→500; ChangePassword bỏ
  qua current-password với invalidhash và còn nhận1kýtự; Register nhận invalidemail.
  Enforce workflow validation, failclosed, reset consume đồng thời an toàn.
- R8:12unit tests cũ dùng EFInMemory nhưng dependency mới transaction. Dùng
  relational fixture đúng, không suppress/skip; hardening429fail là bug R2 thật.

9 probe nằm ở ignored `.tmp/cursor-plan2-review/ReviewProbeTests.cs` và TRX cùng
thư mục. Đưa regression cần thiết vào tests chính thức, giữ expected đúng; không
compile đồng thời cả file tạm lẫn bản chính. Nếu artifact không có, report đủ
trigger để dựng lại. Đây là evidence fail, không phải chứng chỉ acceptance.

Giữ toàn bộ dirty UI/docs/BTH2/realdata/workload hiện hữu, không reset/stash/revert
hay reformat tổng thể. Sửa source scope Plan2; Program chỉ DI/flags không secret
cần thiết. Additive schema theo tct-ef-migration: repo-localEF, Designer/snapshot,
upgrade/repeat/preservation MariaDB10.5.23 disposable và pendingmodel sạch.
Không edit applied migration/history, DROP/reset DB, nâng package tùy tiện.

Không paid/liveOpenAI, liveSMTP hoặc secret/key trong Git/log/argv. Không fake
scores/AIresult và không đổi credit/receipt/outbox/5GoalAreas/IDOR404/CSRF/roles.
Không agent phụ/CLI AI/chat khác. Không commit/push/PR/CIremote/merge/deploy/Plesk;
deploy gate giữfalse. Chỉ dừng để owner review khi phần local đã xong và blockers
ngoài môi trường được ghi rõ; không tự triển khai production để lấy bằng chứng.

Runlocal chỉ khi cần acceptance bằng launcher chuẩn, preserve busyports và chọn
free Releaseport; xác nhận health/Speech/keysource không echo key. Build/test
không tự startWhisper. Ordinarystartup không paid smoke. Fixtures tự tạo có
identity riêng, cleanup chính xác, không tác động user thật.

Cuối lượt báo tiếng Việt: findings đã đóng bằng chứng gì, số fresh tests pass/
fail/skip, MariaDB schema+concurrency, browser+perf trước–sau, files, risk/blockers
và next gate. Không blanket “DONE core/hoàn tất” khi BE4/6/7 chưa thực hiện.
