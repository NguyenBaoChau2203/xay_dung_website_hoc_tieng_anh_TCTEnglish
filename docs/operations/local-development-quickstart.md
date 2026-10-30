# Local development: Docker MariaDB + ASP.NET Core

Target ứng dụng vẫn là net9.0 / EF 9.0.4 / Pomelo 9.0.0. Có thể build bằng
SDK .NET 10; cần runtime ASP.NET Core/.NET 9 để chạy target hiện tại.

## Khởi động lần đầu

Cần Docker Desktop ở chế độ Linux containers và PowerShell 7.

```powershell
pwsh -NoProfile -File scripts/start-local.ps1
```

Script khởi động MariaDB 10.5.23 tương thích hosting tại `127.0.0.1:3307`, tạo
mật khẩu riêng, lưu User Secrets, restore tool EF 9.0.4, áp dụng migration và
chạy web ở `http://localhost:5100`. Mật khẩu/backup chỉ ở `.appdata/local` và
User Secrets ngoài Git. Volume riêng tên `tctenglish-local-mariadb-data`.

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

Nếu đã setup một lần, bật Docker Desktop rồi F5 trong IDE hoặc:

```powershell
docker compose -f compose.local.yml up -d
dotnet watch --project TCTEnglish run --launch-profile http
```

Nếu cổng 5100 đang có web đang chạy, dừng instance đó trước khi chạy instance mới.
Khi sửa model, tạo/review migration bằng EF tool local rồi `dotnet ef database
update --project TCTEnglish`. Không copy database dev đè production.

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
