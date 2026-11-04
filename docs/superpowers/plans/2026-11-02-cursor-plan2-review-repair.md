# Kế hoạch sửa review và hoàn tất Plan 2 — Cursor Auto

Ngày02/11/2026, nhánh `feature/chau-ui-performance-audit`, baseline210bf2d.
Trạng thái **PLANNED**, chỉ thực thi khi owner giao prompt tương ứng.

Review nguồn: [report](../../feature-audits/2026-11-02-cursor-plan2-review.md).
Plan gốc tiếp tục áp dụng: [BE0–BE8](2026-11-02-chau-logic-performance-plan.md).
Ledger: `docs/feature-audits/2026-11-02-chau-logic-performance-ledger.md`.
Không coi review này là quyền sửa/merge/deploy trong lượt review của Codex.

## Phạm vi/quyền khi owner giao thực thi

Một agent Cursor inline làm toàn bộ. Giữ UI Plan1, BTH2, dữ liệu, receipt/credit/
outbox/5GoalAreas, ownership404, antiforgery, roles và routes/contracts cũ.
Website `gpt-6-luna`/low, audio Whisper; không đổi model theo model IDE.

Giữ quyền có giới hạn từ prompt Plan2: sửa services/controllers/VM/tests/JS
integration, DI/config không secret tối thiểu trong Program.cs, additive migration
cần thiết theo EF procedure. Không nâng framework/package tùy tiện, sửa applied
migration, reset DB, DROP, sửa hosting hoặc đưa credentials/backup vào Git.

Chỉ local: không commit/push/PR/CI remote/merge/deploy; gatefalse. Regression
fake provider/SMTP, không live API trả phí. Dùng MariaDB10.5.23 disposable có
identity/guard đúng; bảo toàn dev DB hiện hữu và các listener/workload khác.

## Thứ tự và dependency

`RP0 → RP1/RP2 → RP3 → RP4 → RP5 → RP6 → BE4/BE5/BE6/BE7 → BE8`.
Một agent chạy tuần tự; dấu `/` chỉ các phần cùng batch, không phải delegation.
BE0 đo baseline phải hoàn tất trước khi sửa query ở BE4/BE6/BE7. Khôi phục test
harness sớm trong RP0/RP1 để mỗi batch có regression tin cậy.

### RP0 — Chốt snapshot và đưa regression vào fixture đúng

- [ ] Status/branch/diff manifest, không reset/stash/reformat toàn UI.
- [ ] Đọc R1–R8 và ledger. Ghi BE1–3 là chưa đóng, không còn “DONE core” blanket.
- [ ] Có thể tham khảo 9 probe ignored `.tmp/cursor-plan2-review/ReviewProbeTests.cs`;
  tạo test chính thức tại project tests theo từng batch. Nếu artifact không có,
  dựng lại đúng trigger/expected trong report, không bỏ qua regression.
- [ ] Với R8, chuyển tests dùng transaction khỏi EF InMemory sang SQLite
  relational fixture phù hợp, giữ lifetime/connection độc lập và defaults model.
  Không suppress transaction warning, skip case hoặc đổi expected429 thành200.
- [ ] Red→green ghi command và outcome, không yêu cầu cả suite xanh trước khi
  regression mới đã cố ý đỏ được sửa. Full suite phải xanh cuối cùng.
- [ ] Chuẩn bị MariaDB disposable riêng; xác nhận host/port/server version/name/
  runId/empty test identity trước migration. Không dùng DB hosting/dev làm disposable.

### RP1 — R1: reservation atomic, idempotent và recoverable

**Files:** AiDailyQuotaReservationService/interfaces/models; DbflashcardContext;
AiChatService caller; migrations/Designer/snapshot; quota integration tests.

- [ ] Red: A reserve100, B reserve100 bằng hai scope giữ sống; A settle20,
  B settle20. Expected pending0, settled2, pendingTokens0, settledTokens40.
- [ ] Red: cùng reservation settle2lần, release2lần, settle→release và replay
  khác scope/instance. Mỗi request có đúng một terminal outcome, không double count.
- [ ] Design ledger trước schema: requestId gắn user/ngày/payload identity;
  pending/settled/released/unknown, reserved/actual tokens, quota policy, cleanup.
  Cùng id+payload trả known outcome; changed payload409; outsider404.
- [ ] Update atomic ở DB hoặc locking/concurrency hợp lệ. SELECT tracking trong
  transaction mới không đủ để refresh entity. Retry sau conflict/deadlock phải
  có fresh state và idempotence; không giữ transaction khi gọi provider HTTP.
- [ ] Process crash/restart, unknown outcome, expiry/reconciliation có kiểm
  chứng. Không release mọi pending vô điều kiện rồi cho dùng vượt budget.
- [ ] Cả daily-question/token budget, Standard/Premium/Admin và day UTC giữ đúng.
- [ ] Additive schema mới theo `tct-ef-migration`; không edit migration đã apply.
  Migrate upgrade/repeat/preservation và pending-model check; snapshot cùng diff.

**Acceptance:** red cases trên pass; MariaDB ít nhất2contexts/instances với
barrier chứng minh đúng, không chỉ SQLite hoặc static semaphore.

### RP2 — R2: bảo toàn quota đang dùng khi nâng cấp

**Dependency:** policy/data authority RP1 đã rõ.

- [ ] Red:15success logs hôm nay, bảng quota chưa có row; usage15 và send429,
  providerCalls0. Với14logs và2requests cạnh tranh chỉ1slot được cấp.
- [ ] Tương tự historical token usage, failure logs không tính thành success;
  records ngày trước/ngày UTC boundary đúng.
- [ ] Chọn backfill/lazy initialize có transition marker/transaction đúng;
  không import lại logs mới đã được settle và nhân đôi usage. Kiểm khi schema
  đã tồn tại nhưng row chưa có, không chỉ migration lần đầu.
- [ ] UI usage, quota reserve và observability phải phản ánh policy thống nhất,
  có định nghĩa pending; không giữ hai counter bất đồng không giải thích.
- [ ] MariaDB disposable upgrade từ schema trước migration với synthetic
  history; repeat no-op, data/checksums/history còn nguyên, không invent migration IDs.

**Acceptance:** test hardening cũ429 pass; historical/migration/repeat pass trên
MariaDB và không reset quota trong ngày.

### RP3 — R3: Stop và transport intent qua đường thật

**Files:** AI methods ClassChatHub, streaming service/interface, optional cancel
endpoint/handle, Realtime messages, ai-chat.js và focused integration tests.

- [ ] Giữ red TestServer WebSocket JSON SignalR từ report: fake provider bị
  barrier giữ; Started received; cùng connection gửi Stop; provider phải cancel
  khi chưa được release output. Kiểm actual HubOptions/scheduler.
- [ ] Chọn cancel channel/handle user+connection/request hoặc scheduling bounded
  có scope độc lập. Không tăng global parallel rồi làm class chat dùng cùng
  DbContext đồng thời; không Task.Run/fire-and-forget/untracked work.
- [ ] Stop trước Started/đang provider/sau committed reply, disconnect/reconnect;
  chỉ request sở hữu bị hủy, không cancel tab/user khác hoặc che IDOR.
- [ ] HTTP fallback chỉ khi biết chưa xử lý; disconnect/unknown completion không
  tạo provider call thứ2 cùng intent. Gắn identity với RP1 nếu cần, không claim
  remote exactly-once billing. Kiểm cả full/embed UI và state/unlock/Stop feedback.

**Acceptance:** service tests và transport/scheduler tests pass; class chat
auth/membership/message tests không regress. Fake provider, không paid smoke.

### RP4 — R4: cleanup theo giai đoạn toàn lifecycle

**File:** AiChatService + interceptors/barrier regression tests.

- [ ] Red ngay sau prompt commit trước history: expected0orphan/draft và provider0.
- [ ] Test cancellation khi history/context/reserve/provider/save-reply/finalize;
  xác định rõ before-send, unknown provider result, reply committed.
- [ ] Một failure/finalization policy bao phủ mutation từ đầu, cleanup token
  riêng bounded và best effort không thay original exception. Release lease luôn.
- [ ] Không cleanup mất reply đã commit; không nhả tokens không rõ billing;
  nếu finalize lỗi, request có state recovery/known result để retry an toàn.
- [ ] Existing conversation giữ messages trước đó; new draft chỉ xóa phần của
  request này, không ảnh hưởng request khác hoặc real user data.

**Acceptance:** từng barrier case/new+existing pass, retry không duplicate, state
reservation khớp database; MariaDB khi test transaction/concurrency.

### RP5 — R6/R7: Account fail closed và validation đầy đủ

**Files:** AccountController + narrow service/VM nếu cần, Account tests.

- [ ] Red hash prefix/length hợp lệ nhưng cost/salt/checksum lỗi → generic login
  failure không500; valid bcrypt vẫn login được. Verify library đang pin, không
  đổi package tùy tiện. Catch lỗi parse cụ thể, không swallow mọi exception.
- [ ] Malformed/legacy non-social hash không được bỏ qua current-password verify
  để đổi password. Phân biệt local/social/invalid; social flow giữ đúng scope,
  không auto-link provider hoặc tự đặt password qua malformed path.
- [ ] Register/UpdateEmail/ChangePassword/Reset validation field/format/length/
  confirm hiện hành; reject trước BCrypt/SaveChanges. Một-character password
  và invalid email không được persist. Trim/normalize theo contract có test.
- [ ] Không check toàn ModelState của shared VM một cách mù quáng khiến Email
  workflow yêu cầu cả Current/NewPassword. Tách input phù hợp nếu cần.
- [ ] Reset valid/expired/null/reuse và hai POST đồng thời: một consume thành
  công, lần còn lại rejected, không last-writer-wins đổi mật khẩu lần2.
- [ ] Anti-forgery/anonymous/valid local/OAuth marker,403 admin vs404ownership
  vẫn đúng. Không gửi SMTP thật, không enumerate email.

**Acceptance:** probe lỗi account green + existing login/reset/settings tests;
không đánh dấu BE1done khi mới thêm length guard Register.

### RP6 — R5 và phần còn lại BE3: history có giới hạn nhưng truy cập đủ

**Files:** AiConversationService/interfaces/DTOs; AiController/VMs; Chat/_ChatShell;
ai-chat.js; paging/query/browser tests. Đọc DESIGN.md trước thay UI integration.

- [ ] Page30conversations/50messages và clamp100, metadata/cursor opaque validate;
  endpoint owner404, không nhận userId do client đưa.
- [ ] UI More/Load older thật cho cả history list và message window; giữ order,
  scroll position/focus, no duplicate/gap khi timestamps bằng nhau.
- [ ] HTTP test36conversations/120messages từ report; thêm5000messages, không
  materialize toàn DB/lần đầu render đầy. Không mất đường mở conversation cũ.
- [ ] Context query theo batch/token budget4600 như BE3, không tùy tiện cắt100
  messages làm đổi policy âm thầm; nếu có cap policy phải rõ và test boundary.
- [ ] Full/embed, back/forward/querystring, send/delete/active title/history đúng.

**Acceptance:** old history truy cập đủ qua UI/API và SQL payload có giới hạn;
test ties/cursors/outsider/invalid cursor pass.

## Hoàn tất các batch Plan2 chưa thực hiện

Không dừng sau khi sửa R1–R8. Tiếp tục checklist gốc, cập nhật từng batch:

| Batch | Công việc và gate còn thiếu |
| --- | --- |
| BE0 trước tối ưu | Synthetic scale fixtures200/2000catalog,50/500/5000history; warmup5 + ít nhất30samples; query count/rows/bytes/allocations/latency, cùng môi trường trước–sau |
| BE4 | SQL filters/count/paging Speaking public/my/playlist preview và Writing status/attempt aggregates; giữ resume/history/access/credit; EXPLAIN MariaDB và không duplicate index |
| BE5 | Fake SMTP transport seam, caller cancellation/deadline/result semantics, HTML encoding/logging; unlock consistency/idempotency; worker defaultoff |
| BE6 | Đo rồi giảm full Goals load cho Profile badges và reuse/lazy daily usage trong request; không award trênGET/shared private cache/parallel EF cùng context |
| BE7 | Pin assets đúng bản/provenance/SRI, CSS cascade/defer không pháinit; trace3mẫu trước–sau, noXSS/consoleerror và sanitized diagnostics |
| BE8 | Fresh full app suite0fail + provider migration/concurrency + browser Standard/Premium/Admin, Plan1 mobile/keyboard, readiness/ASR đúng prerequisite; cleanPC/mic/hosting chưa chạy ghi unverified |

Không biến việc thiếu một external gate thành lý do dừng mọi phần độc lập.
Không chạy deploy/Plesk để lấy acceptance. Không báo savings ước tính thành đo thật.

## Handoff bắt buộc

- [ ] Ledger có từng R1–R8: red trigger → implementation → command → green result,
  schema/data risks, provider/browser/perf evidence và limits.
- [ ] Full suite mới: không còn13fail và các regression mới cũng pass; không dựa
  số816pass để bỏ qua failure. Tests tối ưu Configuration riêng nếu Release DLL
  đang lock; phải fresh build đúng source, ghi đúng configuration.
- [ ] `node --test scripts/tests/test_ai_chat_submit_guard.cjs`, relevant JS/app
  tests, `git diff --check`, EF pendingmodel và MariaDBchecks theo schema scope.
- [ ] Bug log chỉ thêm fix đã verify; known issue nào chưa đóng ghi đúng trạng thái.
- [ ] Git stage vẫn local dirty để owner review; không tự commit/push/CI/merge.
  Đọc gatefalse nếu báo production state; không tạo release.

Prompt: [Cursor execution](../../../.ai/prompts/cursor-plan2-review-repair.md).
