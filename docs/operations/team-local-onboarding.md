# Thiết lập máy thành viên — Docker, OpenAI speech và AI Agent

Áp dụng cho Windows x64, cập nhật03/11/2026. Pull code cung cấp source/config/
scripts; mỗi máy tự có Docker volume, tài khoản local và key. **Không cần SSMS,
SQL Server hoặc cài MariaDB riêng trên Windows.** Database chạy MariaDB10.5.23
trong Docker để tương thích hosting hiện tại; không tự nâng version provider/DB.

## 1. Kiểm tra và cài prerequisites còn thiếu

| Phần mềm | Yêu cầu / nguồn chính thức |
| --- | --- |
| Git | [Git for Windows](https://git-scm.com/downloads/win) |
| Node.js và npm | Node22+ kèm npm; [Node download](https://nodejs.org/en/download) |
| PowerShell | PowerShell7, lệnh pwsh; [Microsoft install](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows) |
| .NET | SDK9+ và ASP.NET Core runtime9+; [Microsoft .NET](https://dotnet.microsoft.com/download/dotnet/9.0). SDK9 là lựa chọn khớp target; SDK10/runtime10 chạy được nhờ LatestMajor roll-forward hiện có. Không đổi net9.0 chỉ vì SDK mới. |
| Docker Desktop | [Windows install/WSL2 prerequisites](https://docs.docker.com/desktop/setup/install/windows-install/); mở Docker ở chế độ Linux containers, bật virtualization/WSL2 theo tài liệu |
| AI IDE | Codex, Cursor, Claude Code hoặc Antigravity; đăng nhập tài khoản của mình |

Sau khi cài, mở lại terminal/IDE để nhận PATH. Trong thư mục gốc repository:

```powershell
node scripts/check-local.cjs
```

Doctor chỉ đọc trạng thái/versions, không cài phần mềm hoặc dừng/reset Docker.
Nó báo từng mục OK/MISSING và link cài. Khi Docker đang khởi động, chờ engine
Linux sẵn sàng rồi kiểm lại. FFmpeg pin (audio import) hiện hỗ trợ **Windows x64**;
macOS/Linux/Windows ARM cần profile artifact riêng. Speech dùng OpenAI cloud;
không cần CUDA/GPU/local Whisper.

## 2. Pull và setup agent một lần

Từ05/11/2026 có [gói dữ liệu dùng chung](shared-data-workflow.md). Sau lần clone
và setup, dùng `pwsh -NoProfile -File scripts/pull-local.ps1` để pull code và nhập
gói đã review vào Docker riêng. Không sao chép database/volume/key giữa các máy.

```powershell
git switch master
git pull --ff-only
```

Nếu có thay đổi local chưa lưu, xử lý trên nhánh riêng trước; không reset để pull.
Mở thư mục gốc repo, chạy **SETUP-AGENTS.cmd** một lần (hoặc task tự setup trong
IDE), rồi xác nhận trust/MCP/hooks khi IDE hỏi. Không chuyển file trust hoặc
settings cá nhân qua Git. Toolkit khôi phục Python/uv/CodeGraph dependencies đã
lock; không cần cài Python/uv/CodeGraph global và không tự tạo index.
Xem [team agent setup](../team-agent-setup.md).

## 3. Nhập key Châu cấp trên từng máy

Nhận key qua kênh riêng. Agent có thể mở helper để người dùng nhập key có che ký tự:

```powershell
pwsh -NoProfile -File scripts/set-local-ai-key.ps1
```

Key nằm trong .NET User Secrets ngoài repo, không sửa appsettings/source và không
đưa key vào lệnh/argv/history/README/PR/GitHub. Launcher ưu tiên **AI__ApiKey →
User Secrets AI:ApiKey → OPENAI_API_KEY**. Chỉ cần nhập một lần hoặc khi thay key.
Mở web không ghi lại User Secrets; connection Docker/worker flags chỉ được truyền
cho child process và khôi phục khi thoát. Key helper giữ file cũ/protected backup
nếu thao tác thay file lỗi; không tắt EFS/ACL để sửa quyền.
Nếu nhận401/quota, kiểm key/account; không đổi model hoặc quay lại Gemini.
Website dùng **gpt-6-luna/low** cho text và **gpt-transcribe** cho audio; model
AI IDE của thành viên là lựa chọn độc lập. Không cần key để chạy Writing Local và
các tính năng không dùng cloud; Speaking ASR báo NotReady rõ khi thiếu key (không
fake điểm). SMTP/OAuth/payment cần credential dev riêng nếu test các phần đó.

## 4. Chạy toàn bộ local

Bấm **SETUP-LOCAL.cmd** hoặc yêu cầu AI Agent chạy lệnh:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

Script tạo database Docker/volume riêng, truyền connection local vào child process, update migration
đã commit, bootstrap mẫu khi chưa có user, rồi chạy web. Ordinary startup **không**
tải/start local ASR và **không** gọi Speech API trả phí. FFmpeg pin chỉ cần khi
Admin/legacy audio import: `scripts/local-media.ps1 -Action Setup`.
Lần đầu cần mạng để tải image/NuGet; lần sau dùng cache.
Không cài SQL Server/SSMS, không dùng connection database hosting để code local.

| Thành phần | Mặc định |
| --- | --- |
| Web | http://localhost:5100 |
| MariaDB | 127.0.0.1:3307 / tctenglish_dev |
| Speech | OpenAI `gpt-transcribe` khi key configured; `scripts/speech-status.ps1` |
| Tài khoản mẫu | admin@tct.local / student@tct.local / premium@tct.local |
| Mật khẩu mẫu | Sinh ngẫu nhiên riêng mỗi máy trong **.appdata/local/access.json**; mở riêng trên máy, không in/share file |

Dữ liệu mẫu gồm vocabulary/cards, Writing, Speaking/Listening fixture và Reading.
Speaking/Listening là **dữ liệu mô phỏng phục vụ phát triển**, không phải bộ phụ
đề chính xác của media demo. Test media/caption thực cần import nguồn hợp lệ hoặc
bản backup riêng; không coi fixture là chất lượng khóa học đã được nghiệm thu.
Bootstrap chỉ chạy bằng CLI local với Development và loopback/tctenglish_dev/
tct_dev; database đã có user không bị seed/đổi role/reset password. Chạy lại giữ
edits và file account cũ. Không gọi API cloud/SMTP/payment để bootstrap.

Database/model/key/uploads không tự đồng bộ qua Git. Các máy có cùng schema và
fixture ban đầu, sau đó dữ liệu phát triển độc lập. Web +DB staging chung đã chạy
tại https://staging.tctenglish.site để kiểm thử tích hợp; xem
[runbook staging](staging-plesk-runbook.md) và [quy trình database nhóm](database-collaboration.md).
Châu cấp thông tin truy cập qua kênh riêng; không lấy credentials bằng Git.
Docker local vẫn độc lập. Goal/tiến độ trên staging thuộc từng tài khoản. Nếu cần giống dữ liệu Châu
đang có, Châu chuyển **backup riêng** rồi import khi database còn trống:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -BackupZip 'C:/private/tctenglish-backup.zip'
```

Không đưa backup vào Git; không xóa volume có dữ liệu để import lại. Backup có
user thì bootstrap mẫu bỏ qua, tài khoản/password theo backup. File uploads cần
bản sao riêng. Mật khẩu MariaDB local và key của Châu không đi cùng source.

## 5. Làm việc cùng AI Agent mỗi ngày

AGENTS.md là quy định chung cho cả4clients. Có thể dùng prompt:

> Đọc AGENTS.md, kiểm prerequisites và chạy web local bằng scripts/start-local.ps1
> -NoBrowser để có MariaDB và key đã lưu trên máy. Xác nhận web readiness,
> speech-status và nguồn key, không in key. Giữ dữ liệu/workload hiện có; chỉ test
> local, chưa push/deploy. Nếu thiếu prerequisite, báo đúng mục và link cài.

Agent không start web hoặc gọi paid Speech/API trên folderOpen/build/test/MCP/
hooks thường. Cổng bị chiếm phải kiểm process; không báo app cũ đã nhận key mới,
không taskkill chung. Nếu cần giữ instance đó, dùng configuration/cổng trống,
ví dụ Release5102. Chỉ restart đúng instance do task quản lý; đổi key cần web
process mới. Ctrl+C chỉ dừng web; MariaDB volume tiếp tục chạy. Xem
[speech runbook](local-speech.md) cho status/import/live opt-in.

## 6. Git/PR/CI và production

Tạo nhánh feature/fix/chore, làm local và mở PR vào master; review/merge theo quy
định owner. Push/PR có thể chạy CI build/tests, **không đồng nghĩa production
deploy**. PLESK_DEPLOYMENT_ENABLED đang false. Chỉ Châu cho phép bật release sau
khi test master ổn định; không sửa Plesk/gate, push deployment branch hoặc gọi
webhook thủ công để bypass. Schema changes vẫn ship migration/metadata/snapshot
và provider checks cùng feature, không đẩy database local đè hosting.

Luồng staging được chuẩn bị riêng: sau merge `master`, CI đạt và gate
`PLESK_STAGING_DEPLOYMENT_ENABLED=true` thì tự backup/migrate/cập nhật
https://staging.tctenglish.site; production vẫn theo gate riêng đang false.
Xem [staging automation](staging-automation.md). Admin xuất bản học liệu trên
staging thì cả nhóm thấy sau khi tải lại, không cần push. Dùng tài khoản app riêng
để giữ progress/draft riêng; bài AI/private cá nhân không tự trở thành catalog chung.
