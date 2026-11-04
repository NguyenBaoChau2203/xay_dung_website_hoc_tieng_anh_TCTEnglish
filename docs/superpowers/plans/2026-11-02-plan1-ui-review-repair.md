# Repair Plan1 sau review Gemini — 02/11/2026

Trạng thái: PLANNED, chưa fix. Nhánh feature/chau-ui-performance-audit, HEAD210bf2d
+dirty UI Plan1. Một agent inline. Owner hiện yêu cầu review; chỉ thực thi khi
owner giao prompt repair, không tự coi review là quyền sửa app.

Đọc [review](../../feature-audits/2026-11-02-gemini-plan1-review.md), AGENTS.md,
DESIGN.md, relevant bug memory và tct-superpowers/tct-engineering/tct-ui-review.
Không làm lại UI0–UI7 toàn bộ, không làm Plan2 hoặc rewrite transport backend.

## Thứ tự

`R0 snapshot → R1 submit guard → R2 avatar → R3 short viewport → R4 sidebar → R5 Admin skip link → R6 verification/ledger`.
Ghi mỗi bước vào `docs/feature-audits/2026-11-02-plan1-ui-repair-ledger.md`.
Preserve22 UI files/diff đã có,5tài liệu audit/plan gốc và BTH2 chưa track.
Không commit/push/CI/merge/deploy, gate giữ false. Không schema/backend/config change.

## R0 — baseline

- [ ] Branch/status, snapshot diff/checksum các file sẽ sửa; không reset/stash.
- [ ] CodeGraph exact symbols trước, đọc omitted gaps có mục đích.
- [ ] Launcher chuẩn khi cần; busy ports giữ workload, dùng free Release port;
  app health/speech Status/key source, không echo credential hoặc paid smoke.
- [ ] Dùng fixture riêng và fake provider/DOM cho repro; không user/content thật.
  Test không gửi email/AI/YouTube hoặc train model. Cleanup fixture sau acceptance.

## R1 — P1, một lượt gửi tại một thời điểm

**Files:** wwwroot/js/ai-chat.js; focused JS behavior tests trong scripts/tests
hoặc test harness hiện có phù hợp (xác minh trước khi chọn framework).

- [ ] Red behavior: pending fake HTTP promise, Enter tin1 rồi type tin2/Enter;
  expect1request và draft tin2 còn nguyên. Direct requestSubmit khi sending cũng
  phải bị guard; không chỉ kiểm sendBtn.disabled.
- [ ] Add atomic synchronous in-flight state tại submit trước append/clear;
  guard click/Enter/quick action/HTTP/SignalR dùng cùng state. Keyboard vẫn giữ
  Shift+Enter và IME, không gửi sớm ở composition.
- [ ] Pending user được soạn tiếp; request bị từ chối không xóa draft hoặc thêm
  duplicate bubble. Sau success/error/cancel UI đúng state, lần sau gửi được.
- [ ] Finally chỉ kết thúc lượt hiện tại; không re-enable khi lượt khác đang chạy.
  Không thay requestId/quota/hub protocol, backend dedup thuộc Plan2.
- [ ] Green same/new conversation, HTTP/SignalR fake, click/Enter/programmatic,
  failure retry/IME/Shift+Enter; no paid API. Browser native Enter kiểm lại.

## R2 — P2, avatar state transition

**Files:** Views/Account/Profile.cshtml và avatar style nếu cần.

- [ ] Red file PNG via DataTransfer trên user chưa avatar: read succeeds nhưng
  img display:none. Test tương tự sau lỗi saved avatar URL.
- [ ] Valid preview hiện img và ẩn initials sau load thành công; error khôi phục
  fallback đúng, không để hidden style từ trạng thái cũ giữ mãi. Preserve binding,
  upload validation/server endpoints; không upload tự động khi chọn file.
- [ ] Repeated selection/cancel/error không phá trạng thái hiện có; không thêm
  default-avatar404 hoặc fake CDN. Verify no-avatar/broken-avatar/saved-avatar.

## R3 — P2, log co theo viewport

**Files:** wwwroot/css/ai-chat.css; shared Chat markup chỉ nếu cần, giữ selectors.

- [ ] Red390×500: wrapper127/log218, DOM20rows, scrollTop=max406 nhưng last
  line489–510 >wrapperBottom427. Test actual last line intersection, không chỉ
  min-height>=220 hoặc document.scrollWidth.
- [ ] Sửa min-height220px trên clipped flex child (cả2media declarations) và
  parent sizing/scroll strategy. Default390×844 vẫn có log>=220 khi đủ chỗ.
  Short viewport ưu tiên đọc hết nội dung/compose, không height hack/ẩn overflow.
- [ ] Matrix full+embed,390×844/390×500/landscape, metrics/history open/closed,
  textarea1–6lines, long messages và200% text/zoom. Real mobile keyboard nếu không
  có thiết bị phải ghi unverified, không đổi emulation thành physical pass.

## R4 — P2, hidden navigation không nhận focus

**Files:** wwwroot/js/layout.js; Account/Profile/Settings scripts, Admin shared
layout toggle script; CSS nhỏ có scope nếu needed.

- [ ] Red native Tab Goals390 sau logo/search/bell/avatar đi vào link sidebar
  đóng ngoài x=-226..-15. Account toggle mở nhưng aria-expanded vẫn false.
- [ ] State helper cập nhật class/aria-expanded/inert (hoặc equivalent) từ trạng
  thái thật khi init/open/close/outside-click/Escape/breakpoint resize.
- [ ] Mobile closed links không có trong tab order; open keyboard vào panel,
  close trả trigger trước inert; desktop visible phải bỏ inert. Không aria-hidden
  container đang giữ focus. Sidebar/dropdown có semantics đúng, không blanket trap.
- [ ] Main breakpoint768, Account992, Admin theoCSS hiện tại: verify riêng,
  không lấy1breakpoint cho mọi layout. Account/Admin expanded sync đúng.
- [ ] Native Tab/Shift+Tab/Enter/Space/Escape, pointer outside, rotate desktop↔
  mobile; Home/Vocabulary/Class/Folder và Profile/Settings/Admin không regress.

## R5 — P2, skip link Admin luôn nhìn thấy khi focus

**File:** Areas/Admin/Views/Shared/_AdminLayout.cshtml; scoped Admin style.

- [ ] Red390×844: link focus ở x8/y8 nhưng hit-test tâm trả navbar đang che.
  Desktop1440 là control không lỗi, không bỏ kiểm cả2breakpoints.
- [ ] Focused skip link đủ layer/position để nằm trên navbar/overlay; giữ href
  admin-main-content/main id. Không global z-index links hoặc xóa navbar sticky.
- [ ] Native Tab nhìn thấy focus, Enter skip tới main, không bị che/cắt khi mobile
  sidebar mở. Thử zoom200%, desktop và account/main shell không regress.

## R6 — evidence và handoff

- [ ] Focused meaningful regressions từng finding; app build fresh. Nếu shared
  contracts thay đổi, full suite cuối với đúng binaries; không --no-build Debug cũ.
- [ ] Lệnh đã chạy được ở review (không đổi .sln):

  ```powershell
  $env:TCT_OPENAI_LIVE='0'
  $env:TCT_OPENAI_FEATURE_LIVE='0'
  dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Plan1Review -p:Optimize=true --logger "trx;LogFileName=plan1-ui-repair.trx" --results-directory .tmp/plan1-ui-repair --verbosity quiet
  ```

  Cấu hình riêng giữ path bin/{Configuration}/net9.0 chuẩn và tránh locked app.
  Không dùng -c Plan1Review với solution chưa khai báo cấu hình đó. Ghi timestamps/
  TRX/actual pass-fail-skip; không truyền secret qua argv hoặc bật live flags.
- [ ] Đồng bộ ledger Gemini: bảng status đầu/cuối, mapping U01–U11 theo audit;
  U10 AccessDenied vẫn Plan2. Không tự thêm email outbox/identity schema vào BE6.
  Append correction cho evidence101old binaries, không viết lại lịch sử thành pass.
  Matrix phải dùng route/view thật (SP03 Study/Speaking, WR02 WritingExercises,
  WR03 WritingPractice); unverified thì giữ nhãn, không invent SpeakingManage/History.
- [ ] Update bug-fix-log chỉ bugs đã sửa/verify, không claim25views hoặc tất cả
  acceptance nếu mới test vài happy paths. `git diff --check` không chứng minh
  cú pháp; build/behavior mới là bằng chứng tương ứng.
- [ ] Review cuối inline, diff check, báo files/checks/risks. Dừng local cho owner
  review; chưa chuyển Plan2, không remote stages hoặc production.
