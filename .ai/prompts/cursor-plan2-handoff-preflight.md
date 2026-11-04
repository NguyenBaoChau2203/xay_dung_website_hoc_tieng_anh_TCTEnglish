# Cursor Auto — bắt đầu Plan2 sau review Plan1

Owner giao thực thi trên local: reviewer đã kiểm lại5finding R1–R5 và cho chuyển
Plan2. Giữ nhánh feature/chau-ui-performance-audit và toàn bộ UI diff/tài liệu/BTH2.
Một agent implement/review/verify inline, không worker/chat/CLI/model khác.

Đọc AGENTS.md, closeout:
`docs/feature-audits/2026-11-02-plan1-ui-rereview-closeout.md`, rồi thực hiện:

1. `docs/superpowers/plans/2026-11-02-plan2-handoff-preflight.md`: F1 breakpoint
   Admin tại992px, F2 đổi source-regex assertions thành behavior regression thật
   cho submit guard. Ghi evidence; không làm lại toàn bộ giao diện.
2. Tiếp tục `.ai/prompts/cursor-auto-chau-logic-performance.md` và Plan2 BE0–BE8.

F1 chỉ đổi media/query Admin cho khớp Bootstrap lg; không sửa breakpoint của Main/
Account. F2 execute productionJS với fake pending transport/events, kiểm request
count/draft/retry, không test bản copy thuật toán hoặc thêm package không cần.

Giữ hợp đồng UI/API/SignalR, namespace/data/ownership/antiforgery, website Luna low
và Whisper. Model Cursor Auto là riêng. Preflight không sửa backend/config/schema;
Plan2 tiếp theo có scope/DI/migration authority giới hạn đúng tài liệu gốc.

Chạy local bằng launcher chuẩn, giữ listener bận và chọn free Release port nếu
cần. Account/DB fixture riêng, key không output/Git/argv; không paid API/live SMTP
trong regression. CodeGraph/skills theo AGENTS, không auto full index.

Không commit/push/PR/CI remote/merge/deploy/Plesk hoặc bật gate. Làm từng batch,
regression/measurement đúng scope; update ledger, bug memory cho fix verified,
pass/skip/unverified rõ ràng. Không dừng ở việc viết lại plan hoặc hỏi xác nhận
chung cho các bước owner đã giao. Hoàn tất Plan2 báo owner review.
