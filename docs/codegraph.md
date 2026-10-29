# CodeGraph cho TCT English

Ngày tích hợp: **29/10/2026**. Nguồn:
[colbymchenry/codegraph](https://github.com/colbymchenry/codegraph), release
[v1.6.2](https://github.com/colbymchenry/codegraph/releases/tag/v1.6.2), commit
6560052a6f856855d3f71eee838fd66ccfa4285d.

Cập nhật dùng chung nhóm: [hướng dẫn setup bằng nút/file](team-agent-setup.md).
MCP đã có cấu hình native cho Codex, Claude Code, Cursor và Antigravity. First-use
bootstrap khôi phục package đã khóa; không index/sync. Máy mới có thể chọn task
**TCT: Create CodeGraph index (explicit action)** khi nhóm quyết định tạo index.
Các câu lệnh PowerShell bên dưới vẫn là lựa chọn CLI thủ công, không phải bước
bắt buộc dành cho đồng đội.

## Đã cài những gì

- Package @colbymchenry/codegraph **1.6.2** tại tooling/codegraph/node_modules;
  package.json ghim phiên bản, package-lock.json giữ integrity của package và
  các bundle theo nền tảng. Bản Windows x64 được cài trên máy hiện tại.
- CLI riêng cho dự án: scripts/codegraph.ps1. CLI toàn máy **1.6.0** được giữ
  nguyên; gõ codegraph trực tiếp vẫn có thể chọn bản toàn máy từ PATH.
- MCP codegraph trong .codex/config.toml chỉ công khai codegraph_explore.
- Adapter đọc index hiện có; không gọi init/index/sync, không chạy watcher,
  daemon hoặc query-pool workers. Không cài agent AI phụ.
- Telemetry, kiểm tra cập nhật và fallback download được tắt trong launcher.
  Npm install được chạy với --ignore-scripts, không chạy trình cài toàn IDE.
- Giấy phép MIT và commit nguồn được ghi ở .ai/vendor/codegraph/LICENSE và
  .ai/tooling/sources.lock.json. Registry nguồn hiện có 12 repo và 84 file giữ lại.

## Sử dụng

Từ thư mục gốc dự án:

```powershell
pwsh -NoProfile -File scripts/codegraph.ps1 --version
pwsh -NoProfile -File scripts/codegraph.ps1 status
pwsh -NoProfile -File scripts/codegraph.ps1 explore "GetCurrentUserId"
```

Trong Codex, ưu tiên tool codegraph_explore với query là câu hỏi hoặc tên file/
symbol. Ví dụ: TCTEnglish/Controllers/BaseController.cs. MCP lấy project root từ
launcher, chạy được ngay cả khi host khởi động từ thư mục TCTEnglish/.

Cài lại dependency trên checkout khác:

```powershell
npm ci --prefix tooling/codegraph --ignore-scripts --no-audit --no-fund
```

Node/npm dùng để cài và khởi động launcher; engine chạy bằng Node đi kèm bundle.
Launcher không tự tải package thiếu. Sau thay đổi cấu hình, host có thể cần tải
lại project/session để nhận MCP mới. Chỉ báo tool đã hoạt động trong chat khi
thực sự gọi được qua host; test script là một mức xác minh riêng.

## Giữ quyền quyết định indexing của chủ dự án

Index hiện có: 497 file, 8.083 node, 17.689 edge; metadata engine 1.5.0. Có thay
đổi mới chưa được index. Không chạy lại index trong lần cài này.

MCP chuẩn của upstream tự chạy catch-up sync; --no-watch chỉ tắt watcher, không
loại bỏ bước sync lúc khởi động. Vì AGENTS.md cấm tự động init/index/sync, adapter
dùng MCPEngine({readOnly:true, watch:false, queryPool:false}) từ bản đã ghim.
Nó vẫn dùng protocol/session và công cụ explore của upstream. Đây là API nội bộ
được cố định theo phiên bản; khi nâng version phải chạy lại integration check.

Quan hệ/call graph có thể phản ánh index cũ. Khi source chưa được index hoặc
CodeGraph không bao phủ file, thông báo giới hạn và dùng công cụ đọc source phù
hợp. Không tự rebuild để che cảnh báo stale index. Chỉ index/sync khi chủ dự án
yêu cầu rõ ràng.

## Xác minh

```powershell
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
uv run --project tooling/mcp --locked --offline python scripts/check_codegraph_mcp.py
```

Integration check khởi tạo MCP từ thư mục con, xác nhận server 1.6.2 chỉ liệt kê
codegraph_explore, truy vấn source BaseController và tìm GetCurrentUserId. Hash
của database chính và WAL được đối chiếu trước/sau để kiểm tra không ghi index.
Không thay đổi application source, Program.cs, appsettings hoặc csproj.
WAL rỗng được coi như không có WAL; kết nối SQLite chỉ đọc vẫn có thể tạo nó.
WAL có dữ liệu và database chính vẫn được kiểm tra hash đầy đủ.

Kết quả lần cài: CLI trả về 1.6.2; codex mcp get codegraph --json đọc được cấu
hình enabled=true; MCP initialize/list/explore đều đạt và hash DB/WAL không đổi.
29/29 test tooling đạt; build solution có 0 warning/0 error; toàn bộ test ứng
dụng đạt 557/557. Tool MCP chưa xuất hiện trong danh sách tool của chat đang mở
tại thời điểm ghi nhận; nếu cần, tải lại project/session để host nhận cấu hình.

Muốn tắt MCP, đặt enabled=false cho mcp_servers.codegraph và tải lại host. Giữ
nguyên index .codegraph/ và cấu hình tct_git/lifecycle hooks; không dùng trình
uninstall toàn máy để gỡ một tích hợp chỉ thuộc dự án.
