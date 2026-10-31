# CodeGraph cho TCT English

Ngày tích hợp: **29/10/2026**. Nguồn:
[colbymchenry/codegraph](https://github.com/colbymchenry/codegraph), release
[v1.6.2](https://github.com/colbymchenry/codegraph/releases/tag/v1.6.2), commit
6560052a6f856855d3f71eee838fd66ccfa4285d.

## Cập nhật 2026-10-30: auto-sync theo checkout

Chủ dự án đã duyệt backup, rebuild bằng 1.6.2 và incremental auto-sync cho riêng
checkout hiện tại. Index mới ban đầu có **577 file, 9.657 node, 21.146 edge**,
metadata engine 1.6.2; status sau rebuild không còn pending. Các con số sau đó có
thể đổi theo source. Báo cáo: [auto-sync handoff](feature-audits/2026-10-30-codegraph-autosync-handoff.md).

`agent-tools.cjs mcp codegraph` dùng `launch.cjs --mcp-project`:

- Không có `.ai/codegraph.local.json`: adapter read-only như trước; không
  init/sync/watcher. Đồng đội pull tooling không tự nhận quyền ghi index.
- Local policy hợp lệ với `schemaVersion:1`, `mode:"auto-sync"`, `checkoutRoot`
  là real absolute root của checkout, `reviewedVersion:"1.6.2"`, `debounceMs:2000`:
  dùng daemon/catch-up/watcher upstream, incremental sync sau các lần save.
  Policy phải có index sẵn, không tạo/rebuild tự động. Sai root/version/schema
  bị từ chối; không đổi cờ `--mcp-readonly` thành writable.
- `mcp-autosync.cjs` chỉ chuyển tiếp `codegraph_explore` trong checkout đã duyệt;
  chặn tool ẩn, projectPath ra ngoài và batch request. Daemon native chia sẻ một
  writer cho nhiều client cùng checkout. Giữ tool filter explore và telemetry off.
- Policy và `.codegraph/` được Git bỏ qua. Không commit local approval/index;
  native MCP config của bốn client không đổi. Setup/folder-open/post-merge vẫn
  không index. Chỉ local opt-in đã duyệt cho phép server incremental sync.

Lần đầu một phiên IDE nạp launcher cũ cần restart/reconnect MCP để nhận routing
mới; script smoke không chứng minh mọi host đang mở đã reload. Daemon native
mặc định thoát sau khoảng 5 phút không còn client, rồi catch-up khi kết nối lại.
Auto-sync không chạy khi không có server; không cam kết zero-stale trong debounce.

**Tắt:** đặt local `mode:"read-only"` hoặc bỏ file policy rồi restart MCP của
checkout. Nếu daemon cũ còn chạy, dùng daemon manager của CodeGraph chọn đúng
project để dừng; chờ idle exit cũng được. Không dừng mọi tiến trình Node/IDE.
Đổi policy không thu hồi tức thì một server đã chạy bằng cấu hình cũ.

**Backup:** SQLite online backup API đã chụp cả committed WAL vào
`.tmp/codegraph-auto-sync/backups/20261030T044450Z/codegraph.db`, kèm manifest,
SHA-256 và quick_check=ok. Không restore/xóa index tự động; rollback dữ liệu cần
thao tác riêng khi các client đã ngừng dùng DB.

Kiểm thử cập nhật:

```powershell
node --test scripts/tests/test_codegraph_policy.cjs scripts/tests/test_agent_setup.cjs
uv run --project tooling/mcp --locked --offline python scripts/check_codegraph_autosync.py
uv run --project tooling/mcp --locked --offline python scripts/check_codegraph_mcp.py
uv run --project tooling/mcp --locked --offline python scripts/check_codegraph_mcp.py --project-mode
```

Fixture check tạo index/opt-in riêng ở `.tmp`, kiểm DB nodes thật cho
create/edit/delete, burst debounce, offline catch-up, hai client một daemon,
scope gate và trở lại read-only. Không sửa app để thử watcher. Smoke mặc định
ép `--mcp-readonly` và đối chiếu DB/WAL; `--project-mode` cần local approval.

Các mục bên dưới ghi nhận chế độ mặc định và lịch sử cài ban đầu ngày 29/10;
ngoại lệ auto-sync cục bộ ở trên thay thế trạng thái index cũ của checkout này.

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
