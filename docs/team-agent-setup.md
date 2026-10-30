# Dùng chung toolkit giữa Codex, Claude Code, Cursor và Antigravity

Ngày cập nhật: 29/10/2026. Đây là hướng dẫn hiện tại cho các checkout mới.

Cập nhật 30/10: CodeGraph mặc định vẫn read-only. Chủ dự án đã bật incremental
auto-sync riêng checkout của mình qua `.ai/codegraph.local.json` được Git bỏ qua;
pull tooling không bật chế độ đó trên máy đồng đội. Xem [CodeGraph](codegraph.md)
để biết phạm vi, kiểm chứng và cách tắt. Setup/folder-open/post-merge không index.

## Đồng đội cần làm gì

1. Pull/checkout nhánh có đầy đủ các file nâng cấp và mở **thư mục gốc repo**.
2. Máy cần có **Git và Node.js 22+ kèm npm** trong PATH. Nếu vừa cài Node, mở lại
   IDE để nhận PATH mới. Không cần cài Python, uv hay PowerShell 7 riêng cho setup.
3. Trong IDE hỗ trợ VS Code tasks, cho phép workspace tin cậy và tác vụ tự chạy
   **TCT: Set up shared agent tools** khi được hỏi. `.vscode/tasks.json` khai báo
   folder-open task; khả năng hiện/chạy tác vụ còn phụ thuộc bản IDE đang dùng.
4. Nếu task không hiện, trên Windows **bấm đúp SETUP-AGENTS.cmd** ở thư mục gốc.
   Hoặc chọn Terminal > Run Task > TCT: Set up shared agent tools trong IDE hỗ trợ.
   Không cần nhập các lệnh PowerShell trước đây.
5. Xác nhận trust/kết nối MCP và hook của client khi được yêu cầu, rồi mở chat mới
   hoặc reload MCP. Không chia sẻ thông tin đăng nhập hoặc dữ liệu trust qua Git.

Kết nối MCP cũng tự phục hồi dependency đã khóa khi khởi động lần đầu. Nếu client
ngắt kết nối vì cài lần đầu mất nhiều thời gian, chờ task/file setup hoàn tất rồi
reload/retry MCP. Codex có startup timeout 120 giây; client khác có giới hạn riêng.

**Git pull lần đầu không tự chạy mã trong repo mới.** Nó không cài local hooks hay
tin cậy workspace thay người dùng. Sau một lần setup, post-merge hook có thể cập
nhật dependency khi pull tạo merge/fast-forward; không bảo đảm với rebase, thao tác
đổi nhánh hoặc Git client bỏ qua hooks. Khi mở lại IDE hoặc MCP, launcher cũng
kiểm tra fingerprint của lockfile. Nếu không cho phép auto task và không bật MCP,
dùng file bấm đúp. Không chỉnh user settings để ép quyền tự chạy.

## Những gì đã chia sẻ

| Client | Rules / skills | Native hooks | Native MCP |
| --- | --- | --- | --- |
| Codex | AGENTS.md, .agents/skills/tct-* | .codex/hooks.json | .codex/config.toml |
| Claude Code | CLAUDE.md import AGENTS.md và .ai/README.md; 16 adapter .claude/skills/tct-* | .claude/settings.json | .mcp.json |
| Cursor | AGENTS.md, .agents/skills/tct-* | .cursor/hooks.json | .cursor/mcp.json |
| Antigravity | AGENTS.md, .agents/skills/tct-*; .agent/skills còn cho bản cũ | .agents/hooks.json | .agents/mcp_config.json |

Các adapter chỉ trỏ về skill canonical; không duy trì các bản hướng dẫn nghiệp vụ
khác nhau. Role trong .ai/roles được cùng một agent áp dụng inline. Quy tắc này
áp dụng từng tác vụ; các thành viên vẫn cộng tác qua nhánh/PR bình thường.
Skill sẵn sàng để agent chọn theo tác vụ, không có nghĩa mọi skill được chạy ở
mọi lượt. Phiên bản client quá cũ có thể không hỗ trợ các định dạng hiện tại.

MCP dùng Node launcher chung, tìm repo qua Git nên không chứa đường dẫn D:/ hoặc
tên tài khoản của máy người cài. Git server chỉ đăng ký bảy tool đọc và từ chối
cả lời gọi trực tiếp đến tool ghi đã gỡ, độc lập với allowlist của client.
CodeGraph dùng adapter read-only 1.6.2 hiện có; không chạy upstream MCP chuẩn vốn
tự catch-up sync khi khởi động.

Hook có schema riêng cho từng client: nhận diện tên tool, session ID và output
đều được chuyển đổi rõ ràng. Gate một agent hoạt động ngay cả khi Python chưa
được cài. SessionStart nêu hướng dẫn setup khi cần. Hook không tải dependency;
PostToolUse/Stop kiểm tra tooling qua Python đã cài. Antigravity PostToolUse trả
object rỗng đúng contract; kết quả kiểm tra được ghi stderr, không giả lập khả
năng chèn context mà host không hỗ trợ. Stop không ép agent tiếp tục vòng lặp.

## Setup tự làm gì

- Khôi phục Python 3.13.5 và môi trường tooling/mcp/.venv theo uv.lock. Dùng uv
  0.11.16 có sẵn nếu khớp phiên bản; nếu thiếu, tải binary vào tooling/.runtime.
  URL release, commit, kích thước và SHA-256 được ghim trong bootstrap.lock.json;
  phải khớp trước khi giải nén. Không chạy curl | shell hoặc installer toàn máy.
- Chạy npm ci cho CodeGraph theo package-lock.json với --ignore-scripts,
  --no-audit, --no-fund. Không dùng @latest hoặc cài plugin hàng loạt.
- Dùng khóa cục bộ để các IDE khởi động đồng thời không ghi chồng dependency.
  Chỉ ghi dấu hoàn tất sau khi cài thành công. Lỗi mạng có thể retry bằng cùng
  nút/file setup. Lockfile không đổi thì không chạy lại trình cài.
- Bật core.hooksPath=.githooks cho checkout này. Dừng và báo nếu có hook manager
  khác hoặc hook Git mặc định đang hoạt động; không ghi đè cấu hình của đồng đội.
- Chạy validator tooling. pre-commit chỉ đọc staged content; không tải package,
  sửa file, stage, commit hoặc push. post-merge cập nhật runtime, không chạy agent.

Các file runtime, .venv, node_modules, session cache và index vẫn được ignore.
Setup không đổi cấu hình home, user trust, appsettings, project .NET hay database
ứng dụng. Cần mạng ở lần đầu; sau setup có thể đặt TCT_AGENT_TOOLS_OFFLINE=1 để
cấm tải dependency. Không thể khôi phục một package chưa từng có khi offline.

Runtime bootstrap có manifest cho Windows x64/ARM64, macOS x64/ARM64 và Linux
glibc x64/ARM64; dùng tar của hệ điều hành để giải nén. Kiểm chứng cài thật trong
lượt này thực hiện trên Windows x64. Các nền tảng khác cần smoke check thực tế;
không coi có manifest là đã chứng minh chạy tốt trên mọi máy.

## CodeGraph trên máy mới

Database .codegraph/codegraph.db không đi qua Git. Setup và MCP **không tự tạo,
index hoặc sync**. Khi chưa có index, dùng source tools thông thường. Nếu nhóm
quyết định tạo index cho checkout đó, chọn task riêng:

**TCT: Create CodeGraph index (explicit action)**

Task này thực sự rebuild index cục bộ và chỉ chạy khi người dùng chủ động chọn;
không phải dependency của folder-open/setup/post-merge. Không chạy nó chỉ để làm
biến mất cảnh báo. Index cũ cũng có thể stale; xem docs/codegraph.md.

## Kiểm tra và bảo trì

Các lệnh sau dành cho người bảo trì; đồng đội dùng nút/file setup ở trên:

```text
node scripts/agent-tools.cjs setup
node scripts/agent-tools.cjs validate
node --test scripts/tests/test_agent_setup.cjs
uv run --no-project python -m unittest discover -s scripts/tests -v
uv run --no-project python scripts/check_team_hooks.py
uv run --project tooling/mcp --locked --offline python scripts/check_git_mcp.py
uv run --project tooling/mcp --locked --offline python scripts/check_codegraph_mcp.py
```

scripts/agent_clients.py giữ các định nghĩa client đã review để validator đối
chiếu. --write là lệnh bảo trì có chủ đích, không chạy lúc mở IDE/pull/setup;
review/merge tùy chỉnh trước khi tạo lại file managed. Client dùng shared launcher
nhưng vẫn cần smoke check và review schema khi nâng phiên bản IDE hoặc package.

Tắt tự setup bằng cách không cho phép folder-open task; sau khi đã bật Git hooks,
có thể gỡ core.hooksPath nếu vẫn trỏ .githooks. Tắt MCP trong client tương ứng
để ngăn first-use bootstrap. Không dùng bypass trust/sandbox. Có thể tiếp tục đọc
skill/rule mà không chạy các công cụ tự động.

## Nguồn định dạng host

- [Claude Code skills](https://code.claude.com/docs/en/skills),
  [hooks](https://code.claude.com/docs/en/hooks),
  [MCP](https://code.claude.com/docs/en/mcp).
- [Cursor skills](https://cursor.com/docs/skills),
  [hooks](https://cursor.com/docs/hooks), [MCP](https://cursor.com/docs/mcp).
- [Antigravity skills](https://antigravity.google/docs/skills),
  [hooks](https://antigravity.google/docs/hooks/),
  [MCP](https://antigravity.google/docs/mcp/).
- [VS Code automatic tasks](https://code.visualstudio.com/docs/debugtest/tasks#_run-behavior).
- [uv 0.11.16 release](https://github.com/astral-sh/uv/releases/tag/0.11.16).

Protocol/script tests kiểm tra launcher và contract; chúng không tự chứng minh
client của đồng đội đã nạp/trust hoặc tự chạy hook. Build/test ứng dụng là bước
kiểm chứng riêng, không được suy ra từ hook tooling.

Kết quả bàn giao: 41/41 test Python, 9/9 test Node; smoke hook bốn host và hai MCP
đạt; build 0 warning/error. Setup checkout sạch không có uv trong PATH đạt và
chạy lại offline không gọi trình cài. File .cmd và post-merge hook chạy thật đạt.
Lượt test .NET đầu dừng ở 523 bài đã đạt rồi treo; chạy lại toàn bộ với giám sát
timeout đạt 557/557. Chưa kết luận nguyên nhân treo đầu đã được sửa. Chi tiết và
đường dẫn log nằm trong docs/superpowers/plans/2026-10-29-team-agent-setup.md.
