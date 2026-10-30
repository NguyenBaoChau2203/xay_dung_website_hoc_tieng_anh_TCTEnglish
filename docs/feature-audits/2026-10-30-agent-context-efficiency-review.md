# Review việc nạp ngữ cảnh, CodeGraph và Context7

Ngày 2026-10-30 (UTC+7). Phạm vi: hướng dẫn agent, skill/role, lifecycle hooks, prompt Learning vòng 2 và khả năng MCP quan sát được. Một agent thực hiện; không sửa ứng dụng, cấu hình MCP cá nhân, package hoặc index.

**Cập nhật sau audit:** owner đã cấp quyền A5. Backup/rebuild và auto-sync cục bộ
đã triển khai; xem [báo cáo bàn giao](2026-10-30-codegraph-autosync-handoff.md).
Các mục CT02/chưa sync bên dưới là trạng thái tại thời điểm audit ban đầu.

## Kết quả kiểm tra

### CT01 — Prompt/hướng dẫn yêu cầu nạp quá rộng: đã chỉnh

Prompt Learning vòng 2 cũ yêu cầu đọc báo cáo mới, plan mới, plan F0–F9 cũ, ledger, architecture/conventions/known issues và bug log trước khi làm. Chỉ bốn file prompt/plan/plan cũ/report đã cộng 721 dòng, chưa tính các tài liệu khác. Phân cấp tài liệu trong AGENTS cũng có câu yêu cầu đọc đầu task, dễ khiến agent nạp lại nhiều nội dung.

Đã chuyển sang nạp theo bước: tái sử dụng root instructions hiện có; ban đầu lấy scope/quyền/thứ tự, tới bước C nào đọc mục đó; báo cáo lấy đúng RR đang xử lý, ledger lấy hàng cần biết. Plan cũ, vendor và toàn bộ role không còn là bundle phải đọc đầu phiên. Không giảm phạm vi C0–C9 hoặc bỏ regression cần thiết.

### CT02 — CodeGraph hoạt động, index chưa theo kịp source: còn cần chủ dự án quyết định

MCP `codegraph_explore` gọi được trong phiên Codex hiện tại. Pinned CLI 1.6.2 status quan sát đầu audit:

- 497 file; 8.083 node; 17.689 edge.
- 68 file mới và 45 file sửa chưa đồng bộ tại thời điểm đo.
- Index được tạo bởi engine 1.5.0. Các con số pending tiếp tục thay đổi nếu Cursor đang sửa code.

Adapter tại `tooling/codegraph/mcp-readonly.cjs` dùng `readOnly:true`, `watch:false`, `queryPool:false`; không catch-up sync/watcher. Đây là thực thi chủ đích quy tắc của chủ dự án, không phải cài package thất bại. File stale/chưa index buộc agent lấy source trực tiếp để tránh dùng nội dung hoặc call graph cũ. Một query tên file tooling chưa index còn trả về symbol không liên quan; không được tiếp tục truy vấn rộng như thể kết quả đó phủ đúng file.

Đã bổ sung quy tắc: exact symbols, khởi đầu maxFiles 1–3 khi host hỗ trợ; source verbatim còn mới được tính là đã đọc, không mở lại; fallback có giới hạn cho stale/unindexed/gap hoặc text cần edit. Không áp dụng giới hạn file cứng làm bỏ ownership/caller/tests.

**Chưa sync/index.** AGENTS quy định việc này thuộc chủ dự án; đổi skill/prompt không tự cấp quyền ghi index. Cài CodeGraph mới hơn cũng không tự cập nhật graph cũ. Integration check đã xác nhận hash database/WAL không đổi.

### CT03 — Context7 mới thấy ở tài liệu tham khảo, chưa xác nhận server đang chạy

Trước khi thêm hướng dẫn trong audit, tìm kiếm repo chỉ thấy Context7 ở `.ai/vendor/addy-agent-skills/context-engineering.md:247`, trong bảng giới thiệu MCP. Đó là bản hướng dẫn được nhập từ GitHub, không phải cấu hình khởi chạy Context7.

Đã kiểm tra có giới hạn, không in credentials:

- Project `.mcp.json`, `.cursor/mcp.json`, `.agents/mcp_config.json`, `.codex/config.toml`: có `tct_git` và `codegraph`, không có server Context7.
- Project search cả cấu hình/tài liệu bị ignore, loại `.git`, `.tmp`, dependencies, bin/obj và dữ liệu riêng; không thấy cấu hình Context7 ngoài các hướng dẫn vừa thêm.
- User Cursor MCP config thông thường không tồn tại; Cursor User settings không có chuỗi Context7. Các metadata plugin/extension đã kiểm tra không thấy Context7.
- Danh sách tool của phiên Codex hiện tại có CodeGraph, không có Context7. Các cấu hình cá nhân đã kiểm tra cũng chưa cho thấy Context7. Đây không phải xác nhận live trạng thái mọi IDE/profile trên máy.

Context7 cung cấp tài liệu/code example của thư viện; `resolve-library-id` xác định library và `query-docs` truy vấn tài liệu. Nó không tự tìm source private, plan hay bug log trong project. Nguồn chính thức: [Context7 README](https://github.com/upstash/context7#available-tools).

Đã thêm routing có điều kiện: chỉ gọi nếu host cung cấp tool, dùng version/API cụ thể, resolve một lần rồi reuse ID; không gọi cho mọi file hoặc gửi source/private logs. Nếu thiếu tool, báo một lần rồi dùng tài liệu chính thức khi cần. **Chưa cài hoặc thêm server Context7**, tránh tạo một bản trùng khi chưa xác định được cài đặt người dùng đang nhắc tới.

### CT04 — Role/hook không nạp cả codebase; thiếu nhắc tái sử dụng context: đã chỉnh

Ba role ban đầu chỉ 12–15 dòng, là các góc review dùng inline; không tạo subagent. SessionStart chèn một đoạn policy, không đưa toàn bộ tài liệu/source vào chat. PreToolUse đang bảo vệ single-agent; PostToolUse/Stop kiểm tooling và cache theo nội dung, không phải bộ quản lý token hoặc proxy bắt buộc mọi lệnh đọc phải qua CodeGraph.

Đã chỉnh implementer lens, skill engineering/superpowers và SessionStart pointer để reuse instructions/source, nạp active step và phân biệt CodeGraph với Context7. Giữ nguyên command/schema hook native, trust records, guard single-agent và chế độ index read-only. Không thêm hook chặn đọc file: làm vậy sẽ cản sửa source stale và file chưa index.

Đã sửa nhãn database trong AGENTS/engineering từ SQL Server sang Pomelo MySQL theo source hiện tại, tránh hướng agent tìm tài liệu provider sai. Không đổi database hoặc cấu hình ứng dụng.

## Kiểm chứng

- `uv run --no-project python scripts/agent_tooling.py`: passed.
- Python tooling unittest: 41/41 passed.
- `node --test scripts/tests/test_agent_setup.cjs`: 9/9 passed.
- `scripts/check_team_hooks.py`: bốn native host commands, bốn events và malformed inputs passed. Đây là script/contract checks, không xác nhận mọi IDE đã trust/invoke hook mới.
- MCP CodeGraph initialize/list/explore C# source: passed; database/WAL SHA-256 giữ nguyên, không sync/index.
- Build solution đã thử trong lúc source Learning thay đổi đồng thời: fail CS1513 tại `CompletionCreditService.cs:206`, CS1022 tại dòng 235 ở thời điểm đó. Hash nhiều file ứng dụng khác baseline đầu audit; lượt này không sửa các file đó hoặc kết luận lỗi phát sinh từ tooling. Cần kiểm tra lại sau khi tác vụ ứng dụng kết thúc; kết quả build này không phải nghiệm thu Learning vòng 2.

Không có dữ liệu billing token trước/sau hoặc log đầy đủ từ phiên Cursor để đo mức tiết kiệm. Thay đổi này loại chỉ dẫn đọc trùng và nạp quá rộng; không cam kết phần trăm giảm token. Agent vẫn phải đọc source/test cần thiết để sửa đúng, đặc biệt với index stale và tác vụ nhiều feature.

## Bàn giao

- Plan: `docs/superpowers/plans/2026-10-30-agent-context-efficiency-plan.md`.
- Prompt dùng tiếp trong Cursor: `.ai/prompts/cursor-context-efficient-workflow.md`.
- Prompt/plan Learning vòng 2 hiện đã được cập nhật cách nạp context; không cần quay lại đọc toàn bộ lịch sử để áp dụng quy tắc mới.
- Còn hai cổng vận hành: owner quyết định cập nhật CodeGraph index; xác định/kết nối Context7 ở đúng host nếu người dùng muốn dùng. Không đánh đồng repo hướng dẫn, MCP config và tool đang gọi được.
