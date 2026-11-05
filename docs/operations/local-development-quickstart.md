# Local development: Docker MariaDB + ASP.NET Core

Target ứng dụng vẫn là net9.0 / EF 9.0.4 / Pomelo 9.0.0. SDK9 là lựa chọn khớp
target; SDK10/runtime10 cũng chạy được nhờ LatestMajor roll-forward hiện có.
Máy thành viên mới đọc [team onboarding](team-local-onboarding.md), chạy
`node scripts/check-local.cjs` và SETUP-AGENTS.cmd một lần; SETUP-LOCAL.cmd gọi
doctor rồi cùng launcher bên dưới. Không cần SSMS hoặc database server Windows.

Launcher đặt TEMP/TMP riêng cho các child process vào ignored
`.appdata/local/temp` trên cùng ổ với checkout, rồi phục hồi khi thoát/lỗi.
Checkout ở D sẽ ghi scratch/audio/build temp của phiên web vào D; không thay
TEMP/TMP toàn cục của Windows. User Secrets và backup luôn ở ngoài Git.

## Khởi động lần đầu

Cần Docker Desktop ở chế độ Linux containers và PowerShell 7.

```powershell
pwsh -NoProfile -File scripts/start-local.ps1
```

Script khởi động MariaDB 10.5.23 tương thích hosting tại `127.0.0.1:3307`, tạo
mật khẩu riêng, restore tool EF 9.0.4, áp dụng migration và chạy web ở
`http://localhost:5100`. Connection Docker/worker flags chỉ vào environment của
launcher và child process, được khôi phục khi thoát/lỗi; không ghi lại kho key.
Mật khẩu DB/backup ở ignored `.appdata/local`; key giữ trong User Secrets ngoài
Git. Volume riêng tên `tctenglish-local-mariadb-data`.
Sau migration, CLI bootstrap chỉ tạo sample khi chưa có user: ba tài khoản
Admin/Standard/Premium với mật khẩu ngẫu nhiên trong `.appdata/local/access.json`,
vocabulary/Writing/Speaking/Listening/Reading mẫu. DB đã có user không bị seed
lại hoặc đổi credential. Samples không thay bản backup dữ liệu thật của Châu;
fixture media/timings phục vụ phát triển, không phải khóa học đã nghiệm thu.

Muốn dùng một backup SQL ZIP cho database dev **đang trống**:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -BackupZip 'C:/path/to/backup.zip'
```

Script chỉ nhận ZIP có một SQL dưới 25 MB. Database đã có bảng sẽ được giữ nguyên,
không import đè khi chạy lại. Nếu import lỗi, giữ log và database để chẩn đoán;
không xóa volume có dữ liệu để thử lại. Giữ bản backup gốc ngoài Git.

`lower_case_table_names=1` được đặt từ lần khởi tạo volume đầu tiên để restore
database Windows vào Docker Linux. Không thay tùy tiện trên datadir đã tồn tại.
Version 10.5 chỉ dùng để tái hiện hosting cũ; lập kế hoạch nâng DB còn hỗ trợ riêng.

## Làm việc mỗi ngày

Entrypoint khuyến nghị (MariaDB + web; OpenAI speech khi có key):

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser
```

Web: `http://localhost:5100`. Speech cloud model: `gpt-transcribe` (không còn
local ASR loopback). Launcher dùng `AI__ApiKey` hoặc User Secrets `AI:ApiKey`;
nếu hai nguồn này chưa có key, tự nối `OPENAI_API_KEY` đã được owner cho phép vào
process web. Log chỉ báo nguồn/key configured, không in hay lưu lại key.
Sau khi nhập key một lần, cả4 AI Agent dùng lại qua cùng launcher. Không cần
đưa key vào prompt; startup không viết User Secrets kể cả khi thư mục có EFS.
Xem [OpenAI local setup](openai-local-setup.md) và [local speech](local-speech.md).
Chỉ DB: `-DatabaseOnly`. Status cấu hình speech (không gọi API trả phí):

```powershell
pwsh -NoProfile -File scripts/speech-status.ps1
```

Admin audio import cần FFmpeg pin riêng:

```powershell
pwsh -NoProfile -File scripts/local-media.ps1 -Action Setup
```

VS Code/Cursor task **TCT: Start local web** gọi cùng script; không gắn speech
API vào folderOpen.

F5/dotnet watch trực tiếp chỉ dùng khi bạn đã chủ động cấu hình connection local
riêng cho IDE/process đó. Chúng không nhận environment tạm của launcher đã thoát.
Dùng launcher ở trên để luôn có DB local và nguồn key đúng khi nhờ AI Agent mở web.

Nếu cổng5100 đã có process, launcher từ chối trước build để không báo nhầm app
cũ đã nhận code/key mới. Giữ process đó và chọn cổng trống, ví dụ
`-Configuration Release -WebPort 5102`, hoặc dừng đúng launcher do mình quản lý.
Chạy F5/dotnet watch không có bước nối OPENAI_API_KEY; dùng launcher khi AI Agent
được yêu cầu chạy đủ web + key. Khi sửa model dữ liệu, tạo/review migration
bằng EF tool local rồi `dotnet ef database update --project TCTEnglish`. Không
copy database dev đè production.

## Kiểm tra

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release
```

`/health/ready` trả 200 khi kết nối database được và không có migration pending.
Development chỉ cho database loopback. EF tooling dùng design-time factory,
không khởi động web, worker hoặc seeder khi sinh SQL/bundle.

Bootstrap giữ SMTP/OAuth/AI chưa cấu hình; thêm credential dev riêng vào User
Secrets khi cần. Database restore có thể chứa tài khoản, payment và email thật:
không dùng các bản ghi đó để gọi dịch vụ production. Ảnh upload cần bản sao file
riêng nếu muốn hiển thị đầy đủ; SQL backup không chứa các file upload.

Production không chạy migration khi khởi động; seeding mặc định tắt. Xem
[runbook Plesk](plesk-cicd-runbook.md) để phát hành bằng pipeline.

## Trạng thái máy đã thiết lập ngày 30/10/2026

Local `tctenglish_dev` đang dùng ZIP backup ngày 26/09: 57 tài khoản gốc,
36 bộ từ, 162 thẻ, đã nâng đủ bốn migration tháng 10. Có thêm một admin local
`admin@tct.local`; mật khẩu ngẫu nhiên lưu ở `.appdata/local/access.json`.
SSMS không cần cho database MariaDB này. Có thể dùng DBeaver/MySQL Workbench
hoặc MariaDB client, host `127.0.0.1`, port `3307`, database `tctenglish_dev`;
credential chỉ xem trong `.appdata/local/database.env` hoặc User Secrets.

Database hosting lúc kiểm tra ngày 30/10 có 59 tài khoản và 52 bảng. Bản sao mới
được lưu riêng ở `.appdata/local/hosting-backup-20261030.sql`, đối chiếu SHA256
với hosting, restore vào database kiểm tra rồi nâng bốn migration thành công.
Checksum sáu bảng được giữ nguyên và chạy lại không thay history. Bản kiểm tra
này không thay bản local bạn đang làm việc và không thay dữ liệu trên hosting.

## Provider văn bản và audio — Plan 2

Default dùng `gpt-6-luna`/`reasoning.effort=low`; audio dùng `gpt-transcribe`. Nhập key
local có che ký tự bằng `pwsh -NoProfile -File scripts/set-local-ai-key.ps1`,
rồi chạy `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`.
Launcher giữ DB/key, normalize đúng override Google cũ và provision FFmpeg pin.
Không gửi key vào chat. Xem [OpenAI local setup](openai-local-setup.md).
