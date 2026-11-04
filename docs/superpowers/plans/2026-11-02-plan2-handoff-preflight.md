# Preflight bàn giao Plan1 sang Plan2 — Cursor Auto

Trạng thái: DONE (preflight F1/F2). Reviewer đã kiểm lại R1–R5, cho bắt đầu Plan2 local theo
[closeout](../../feature-audits/2026-11-02-plan1-ui-rereview-closeout.md).
Nhánh feature/chau-ui-performance-audit, HEAD210bf2d+dirty UI; một agent.
Đây là2follow-up nhỏ, không làm lại UI0–UI7 và không mở vòng Gemini mới.

Thứ tự khi owner giao prompt: `snapshot → F1 → F2 → evidence → BE0…BE8`.
Append vào ledger Plan2, giữ tất cả dirty files/plan/docs/BTH2. Không commit/push/
CI/merge/deploy. Website Luna low/Whisper, local secrets và gate false giữ nguyên.

## F1 — sửa breakpoint Admin (P2, pre-existing)

Files: Areas/Admin/Views/Shared/_AdminLayout.cshtml; chỉ media/query liên quan.

- [x] Tái hiện991/992/993px.992: sidebar hidden/inert nhưng button d-lg-none
  display:none nên không thể mở. Đã có từ HEAD baseline, không sửa backend.
- [x] Đồng bộ CSS max-width và admin matchMedia cùng Bootstrap lg: mobile
  max991.98px, desktop min992px. Không đổi d-lg-none cho toàn app hoặc đụng
  breakpoint Main768/Account992 không liên quan.
- [x]991 mobile toggle mở/đóng/Escape/inert/expanded đúng;992/993 sidebar desktop
  visible/inertfalse/keyboard links dùng được; rotate390↔992↔1440 giữ đúng state.
  (Source sync verified: CSS `@media (max-width: 991.98px)` +
  `matchMedia('(max-width: 991.98px)')`; Bootstrap `d-lg-none` ẩn từ992.
  Browser matrix 991/992/993 deferred to BE8 authenticated Admin check.)
- [x] Skip link mobile/desktop vẫn trên navbar; không z-index regression.

## F2 — meaningful behavior regression cho R1 (P2)

File: scripts/tests/test_ai_chat_submit_guard.cjs; narrow helper nếu cần trong
scripts/tests, không package/framework mới và không sửa runtime đã pass để né test.

- [x] Thay4regex-only checks bằng thực thi production ai-chat.js/init trong
  harness fake DOM/fetch/hub phù hợp. Dùng helper hiện có nếu thực sự có; không
  copy lại thuật toán guard vào test rồi test bản copy.
- [x] Fake provider promise giữ chờ: send1, send2 Enter/direct requestSubmit/
  quick action khi pending → transport1, bubble1, draft2 nguyên vẹn.
- [x] Settle success/error → unlock; gửi tiếp được, draft clear đúng lúc.
  Shift+Enter/IME không gửi; không giả Green bằng kiểm biến tên isSubmitting.
- [x] Chứng minh regression bắt được behavior double-submit cũ qua red/mutation
  trong fixture/in-memory source. Không sửa staged/runtime source để giữ bản lỗi.
- [x] `node --test scripts/tests/test_ai_chat_submit_guard.cjs` và browser fake
  transport kiểm lại nếu harness đổi. Không paid API/email trong tests.
  Result: 4/4 pass (pending guard, error unlock, Shift+Enter/IME, mutation red).

## Evidence và chuyển tiếp

- [x] Ghi actual pass/fail/skip và files; sửa câu ledger '100% toàn bộ' thành
  số thực thi/skip rõ. Không thay dữ liệu lịch sử thành kết quả mới.
- [x] git diff --check; protected config/backend/schema vẫn ngoài preflight.
- [x] Không cần fullsuite lại chỉ vì tài liệu; sau layout/test change chạy checks
  phù hợp, fullsuite cuối Plan2 theo BE8. Review source/diff trước BE0.
- [x] Tiếp tục Plan2 docs/superpowers/plans/2026-11-02-chau-logic-performance-plan.md
  và .ai/prompts/cursor-auto-chau-logic-performance.md. Authority local của owner
  khi giao prompt bao gồm2follow-up trên; không suy ra quyền remote/production.
