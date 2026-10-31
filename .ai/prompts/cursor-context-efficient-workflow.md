# Cursor — Tiếp tục tác vụ với ngữ cảnh có giới hạn

Áp dụng quy tắc dưới đây cho **tác vụ hiện tại đã được chủ nhóm giao**. Một agent; không mở task/CLI AI phụ. Không khởi động lại điều tra hoặc đọc lại toàn bộ lịch sử chỉ vì nhận prompt này.

1. Tái sử dụng AGENTS.md/.ai/README.md và bằng chứng còn mới trong chat; chỉ đọc phần đã đổi hoặc chưa có. Nêu ngắn bước hiện tại và câu hỏi còn thiếu.
2. Chỉ nạp section của bước đang làm trong plan hiện tại, finding liên quan và skill đúng phần. Plan/ledger cũ, catalog role và vendor là tra cứu khi thiếu contract; không đọc cả bộ đầu phiên. Vẫn hoàn thành toàn bộ scope đã được giao.
3. Source đã index: gọi CodeGraph exact symbols, bắt đầu maxFiles 1–3 khi tool hỗ trợ; source verbatim còn mới tính là đã đọc. Không mở lại cùng file để “xác minh” output đó. Nếu file stale/chưa index hoặc thiếu đoạn để sửa, nêu lý do ngắn rồi bounded reads. Không tiếp tục dùng graph cũ hoặc query không liên quan thay source thật.
4. Context7 **chỉ nếu tool thật sự có trong host**: dùng cho tài liệu library/API bên ngoài đúng version; resolve một lần rồi reuse library ID. Không dùng nó để tìm private code/plan, không gửi source/log/secrets và không gọi cho mọi file. Nếu không có tool, báo một lần; dùng tài liệu chính thức khi cần, không giả đã gọi MCP.
5. Không tự init/full-rebuild hoặc gọi CLI sync. Server CodeGraph được incremental auto-sync khi có `.ai/codegraph.local.json` hợp lệ đã được owner duyệt; checkout khác mặc định read-only. Không tự tạo approval hoặc cài thêm Context7/MCP để đáp ứng một tên trong README.
6. Giữ kiểm quyền, caller/interface và regression cần thiết. Nạp theo câu hỏi cụ thể, không dùng hard cap số file làm thiếu bằng chứng. Không bỏ build/tests, không hứa phần trăm giảm token khi chưa đo usage.
7. Giữ mọi diff có sẵn và scope/quyền của task đang làm; không sửa ứng dụng từ một audit tooling, không commit/push. Báo phân biệt source đã đọc qua graph, fallback do stale/missing và docs API đã tra khi điều đó giúp chủ nhóm kiểm chứng.

Chi tiết audit nếu đang xử lý chính tooling: `docs/feature-audits/2026-10-30-agent-context-efficiency-review.md` và `docs/superpowers/plans/2026-10-30-agent-context-efficiency-plan.md`. Các tài liệu này là tra cứu; không yêu cầu agent Learning đọc cả hai để tiếp tục công việc.
