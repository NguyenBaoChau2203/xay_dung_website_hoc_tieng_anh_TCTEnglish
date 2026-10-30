# GitHub Actions → Plesk release

## Luồng phát hành

Pull request/push master chạy build, toàn bộ test, rồi một MariaDB 10.5.23 tạm
để kiểm tra nâng từ baseline, index khóa ngoại, checksum dữ liệu và chạy lại.
CI tạo app win-x64 self-contained, migration bundle và SQL idempotent. Artifact
không chứa config Development, production credentials hoặc runtime uploads.

Chỉ job release trên master dùng quyền ghi GitHub và secret webhook. Job này
chạy khi repository variable `PLESK_DEPLOYMENT_ENABLED=true`. Mặc định để false
cho tới khi hoàn tất kiểm chứng và credentials bị public đã được thay mới.

Job tạo GitHub release bất biến và cập nhật **chỉ hai file** trên nhánh
`codex/production-release`: `release.json`, `deploy-plesk.ps1`. Plesk Git pull
nhánh đó vào `\private\tctenglish-release`, không kéo source master vào web root.
Post action chạy script bên trong hosting, không cần GitHub kết nối DB qua mạng.

## Cấu hình Plesk một lần

Hosting đã được xác minh: Windows/Plesk, PowerShell 5.1, runtime .NET 9,
`C:\Program Files (x86)\Plesk\MySQL\bin\mariadb-dump.exe`, truy cập GitHub HTTPS.
SDK hosting chỉ tới .NET 8 nên luôn build app/bundle tại CI.

1. Tạo nhánh bootstrap `codex/production-release` với `release.json` có
   `enabled=false` và script đã review. Bootstrap chỉ ghi preflight trong private.
2. Plesk Git: chọn repo dự án, nhánh này, thư mục `\private\tctenglish-release`.
3. Post deployment action:

```text
powershell.exe -NoLogo -NoProfile -NonInteractive -File .\deploy-plesk.ps1 -ManifestPath .\release.json -SiteRoot ..\..\httpdocs -StateRoot ..\tctenglish-release-state
```

4. Kiểm tra `private/tctenglish-release-state/preflight.json`, cấu hình Production
   hiện có và quyền backup/DDL. Không đưa credentials vào branch hoặc action command.
5. Lưu HTTPS webhook do Plesk cấp vào GitHub Secret `PLESK_WEBHOOK_URL`, giữ URL
   ngoài log. Dùng hostname đúng chứng chỉ Plesk, không dùng IP sai tên chứng chỉ.
6. Sau staging/backup restore drill và rotation, bật Automatic trong Plesk và
   variable GitHub. Webhook chỉ được gọi bởi job release sau CI pass.

Không chạy hai đường deploy lên cùng site. Pipeline cũ dùng FTP được thay bằng
luồng GitHub/Plesk HTTPS. FTPS đã phản hồi TLS nhưng chứng chỉ self-signed;
không hạ kiểm tra chứng chỉ để tiếp tục dùng FTP.

## Script thực hiện gì?

Khóa một release, bỏ qua release cũ, tải HTTPS artifact và kiểm tra SHA256,
chặn ZIP path traversal, giữ config Production từ hosting. DB identity phải
đúng database ứng dụng trên máy hosting; migrator và backup dùng loopback.

Script giữ bản sao file app trong private, đưa IIS vào maintenance, dump database,
rồi chạy bundle. Migration thành công mới copy bộ file app, giữ uploads/logs,
mở IIS và kiểm tra `/health/ready`. GitHub cũng kiểm tra public commit marker và
readiness trước khi báo phát hành thành công.

Khi backup thất bại trước migration, app cũ được mở lại. Khi migration đã bắt đầu
hoặc release mới không ready, giữ maintenance và các backup/log riêng. Không tự
Down migration/restore DB vì có nguy cơ mất dữ liệu hoặc DDL đã áp dụng một phần.
File lỗi tổng hợp không chứa connection string. Credentials chỉ được dùng ở
hosting và file private, không đi qua artifact hoặc GitHub release.

## Phục hồi

Xem `private/tctenglish-release-state/last-failure.json` và thư mục run được ghi
ở đó: `database-before.sql`, `previous-app`, `migration.log`, `backup-errors.log`.
Đối chiếu history/schema và migration lỗi trước khi quyết định tiếp tục hoặc
khôi phục. Chỉ mở app sau khi bộ file/schema đã được xác minh tương thích.

Không xóa `app_offline.htm` một cách vô điều kiện. Script không gỡ maintenance
do operator tạo trước đó. Không ép chạy lại run đã có thư mục attempt; cần
review trạng thái và tạo run mới. Dọn các release/backup cũ theo quota sau khi
xác nhận có bản backup độc lập có thể restore; script không tự xóa backup.

## Kiểm chứng trước khi bật production

- Full suite trên Linux; migration smoke MariaDB thật; artifact credential check.
- PowerShell 5.1 parse/preflight, chặn đường dẫn ra ngoài private, chặn config có
  secret và uploads; không xóa maintenance có sẵn.
- Restore bản sao SQL, kiểm tra checksum dữ liệu, nâng cấp và chạy lại.
- Diễn tập lỗi backup, migration, copy và readiness trên staging; private logs
  đủ để phục hồi, website thật không bị dùng làm nơi thử failure modes.

Việc Git pull/post action hoạt động hoặc CI xanh chưa tự chứng minh đã có
restore drill production hay credentials đã rotate. Ghi riêng bằng chứng này.
