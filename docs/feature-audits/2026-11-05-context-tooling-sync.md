# Kiểm tra công cụ ngữ cảnh và source mới — 05/11/2026

Owner yêu cầu kiểm tra/đồng bộ công cụ ngữ cảnh, cho phép commit/push/PR/CI/merge
master. Production tiếp tục OFF. Một agent thực hiện và tự review. Không cài thêm
dịch vụ ngữ cảnh, đổi native trust hoặc đưa index/approval/key vào Git.

## CodeGraph

- CLI của repo và MCP dùng bản pin1.6.2. Local approval hợp lệ, auto-sync gắn đúng
  checkout trên D, debounce2000ms. Không thay policy hoặc dùng global CLI.
- Native project-mode MCP được khởi tạo và truy vấn thực tế, cho phép catch-up
  incremental theo approval đã có. Chỉ tool codegraph_explore được công khai.
- Status sau kiểm tra:709files/12.400nodes/28.470edges, index up to date. Đây là
  snapshot của lần kiểm tra, không phải chỉ tiêu cố định cho các commit sau.
- Tool thực của phiên trả source hiện tại SharedDataCommand.RunAsync, gồm action
  migrate và ILogger từ PR128; SharedDataEngine.ApplyAsync và các tests mới có
  trong graph. Không init/full-rebuild hoặc sửa app để tạo một thay đổi giả.
- check_codegraph_mcp.py --project-mode đạt; check_git_mcp.py đạt initialize/
  discovery/git_status, đúng7read tools và từ chối direct git_add ở server.
- Index/approval là tài nguyên local ignored. Git chia sẻ code/config/rules,
  không chép SQLite index hoặc quyền ghi index sang laptop đồng đội.

## Context7 và bộ nhớ khác

- Không có Context7 trong tool list của phiên hoặc các project MCP config của
  Codex/Cursor/Claude/Antigravity. Các cấu hình cá nhân thông thường đã kiểm tra
  cũng không thấy server Context7; chỉ đọc metadata, không in headers/env/key.
- Context7 phục vụ tài liệu thư viện/API theo version; không có source index TCT
  trên máy để đồng bộ. Không gọi việc đọc README giới thiệu là đã cài hoặc đã
  chạy Context7. Routing hiện tại tiếp tục dùng có điều kiện, fallback official
  docs khi tool không có. Nguồn: [Context7 chính thức](https://github.com/upstash/context7#available-tools).
- Có thêm memory-server trong cấu hình Antigravity cá nhân ngoài project; một
  server dùng secret mount của dự án khác. Không ghi ngữ cảnh TCT vào kho đó,
  không đọc secret/file nội dung, không restart container hoặc sửa global config.
- Claude có candidate CodeGraph qua PATH trong cấu hình user. Project đã có
  launcher pin riêng; metadata không chứng minh IDE đang chọn route nào. Không
  thay global config; khi dùng Claude cần xác nhận đúng project MCP đang active.

## Quy tắc và tài liệu mới

- AGENTS.md là nguồn chung bốn công cụ, CLAUDE.md import nguồn đó. Các adapter
  skill tham chiếu canonical .agents/skills, không tạo bốn bản nội dung riêng.
- Prompt standard/template và prompt thiết kế mới được liên kết từ .ai/README.md
  và AGENTS.md. Agent đọc chúng từ checkout; không phải mọi tài liệu Markdown
  đều cần/được đưa vào code graph. Phiên dài cần đọc lại file đã thay đổi hoặc
  reload context/MCP theo khả năng client; không giả đã điều khiển IDE khác.
- Sau pull, đồng đội dùng scripts/pull-local.ps1 để cập nhật code/gói dữ liệu.
  CodeGraph bắt kịp khi server có approval đúng checkout; máy chưa có index phải
  chủ động tạo theo hướng dẫn, không nhận index/approval qua Git.

## Xác minh và giới hạn

Lần kiểm tra của task này: validator guidance đạt, Python135tests gồm134pass/
1skip/0fail (97.190s); Node bootstrap/policy19/19pass; native contracts4hosts/
4events và malformed input qua pwsh đạt. Doctor: project MCP/hooks của cả4client
matched. Kiểm MCP project-mode/Git thực tế và link/English/whitespace/secret-
pattern của10file thay đổi đều đạt. Đây là kiểm tra tooling/ngữ cảnh; không
thay cho nghiệm thu giao diện hoặc khẳng định mọi IDE đã reload/trust config.

Remote CI/merge/staging/production được đối chiếu theo run thực tế khi bàn giao;
không coi job skipped là đã deploy. Production gate không được bật bởi task này.
