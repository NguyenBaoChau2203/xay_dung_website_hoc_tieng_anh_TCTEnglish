# Phát hành staging và dữ liệu nhóm

## Luồng đã chuẩn bị trong source

`.github/workflows/deploy.yml` giữ job production và gate
`PLESK_DEPLOYMENT_ENABLED` riêng. Job `staging` chỉ chạy trên `master`, sau build,
tests và MariaDB checks, khi `PLESK_STAGING_DEPLOYMENT_ENABLED=true`. PR không
deploy. GitHub Environment `staging` chỉ cho branch master và chứa ba secrets:

- `PLESK_STAGING_WEBHOOK_URL`
- `STAGING_PORTAL_USERNAME`
- `STAGING_PORTAL_PASSWORD`

Key OpenAI và connection string không cần lên GitHub. Chúng ở cấu hình hosting
riêng staging. Publisher tạo prerelease bất biến `staging-{runId}-{sha12}` và
nhánh `release/staging` chỉ chứa `release.json` + `deploy-staging.ps1`. Không
webhook trực tiếp mọi push source; cần CI pass trước.

Plesk Git staging trỏ đúng nhánh trên, deploy vào
`\private\tctenglish-staging-release`, post action:

```text
powershell.exe -NoLogo -NoProfile -NonInteractive -File .\deploy-staging.ps1 -ManifestPath .\release.json -SiteRoot ..\..\staging.tctenglish.site -StateRoot ..\tctenglish-staging-state
```

Private `\private\tctenglish-staging-state\hosting.json` có environment=Staging,
siteHost, connectionString của DB staging, portalUsername/portalPassword. Không
đưa file này vào branch hoặc public web root. Bản bàn giao owner-local là
`.appdata/local/staging/deployment-hosting.json` (ignored).

Script khóa run, kiểm root/site/DB/user cụ thể, kiểm SHA256 và archive traversal,
giữ keys/env/config/uploads; backup app/DB trước bundle migration. Chỉ gỡ maintenance
do run tạo. Nếu backup lỗi: chưa migrate, mở lại app cũ. Nếu migration/readiness
lỗi: giữ maintenance, log/backup private, không tự Down/restore DB. Chỉ promote
marker thành công sau ready. Publisher kiểm marker environment/commit/run/digest
và ready qua HTTPS Basic Auth, không gửi credentials qua redirect.

Sau thành công dọn ZIP/thư mục giải nén của chính run, giữ backup SQL/log và một
previous-app gần nhất. Không tự dọn failed runs, Recycle Bin hoặc dữ liệu nhóm.
Gói N518 có quota: theo dõi Plesk Usage, không để failed artifacts tăng vô hạn.

## Người dùng riêng, học liệu chung

Mọi người đăng nhập **cùng URL staging** bằng tài khoản app riêng. Admin tạo
Writing qua `/Admin/WritingExerciseManagement/Create`, chọn Published. Tất cả
người học trên staging thấy bài đã xuất bản; thêm bài không cần push Git.

Progress, Goals, lịch sử nộp bài và draft thuộc từng tài khoản. Bài AI/private
của một người vẫn riêng; muốn đưa thành học liệu nhóm cần Admin xuất bản trong
catalog chung. Không tự chia sẻ câu trả lời/draft riêng hay cấp Admin cho mọi
thành viên. Người mới có thể đăng ký tài khoản riêng trên staging sau lớp mật khẩu
ngoài; Châu cấp quyền học thử cần thiết qua Admin, không dùng chung tài khoản.

Docker mỗi máy không đồng bộ rows qua Git. Học liệu cần chia sẻ ngay thì tạo trên
staging. Migrations đồng bộ schema khi deploy, không đè DB bằng dump local. Seed
dữ liệu dùng chung từ source cần kế hoạch dữ liệu và script rerun an toàn riêng.

## AI staging

Nhập key riêng bằng `pwsh -NoProfile -File scripts/set-staging-ai-key.ps1` trong
PowerShell tại checkout. File bàn giao private mới, không sửa User Secrets local,
không overwrite key có sẵn, không truyền key bằng argv. Operator đưa giá trị vào
`.NET Core → Environment variables → AI__ApiKey` của **site274** qua HTTPS.

Text `gpt-6-luna`/low. Speech `gpt-transcribe`, Enabled=true. Writing giữ Local
làm quyết định chính, CloudEnhancementEnabled=true để thêm nhận xét có nhãn;
cloud lỗi không được tạo điểm giả. Không có Whisper. Teammates test qua staging
không cần giữ key server. Metadata200 chưa chứng minh host kết nối provider hoặc
live request thành công; ghi riêng kết quả trong audit. Giới hạn và quota API
cần được quản lý ở project test; không dùng key production.

Hosting đã cần hai override riêng site để OpenAI hoạt động:
`DOTNET_SYSTEM_NET_DISABLEIPV6=1`, `NO_PROXY=api.openai.com`. Probe .NET9 mặc định
và noProxy timeout; forceIPv4 metadata200/846ms. Sau cấu hình, Chat200 và Speech200
qua web với audio tổng hợp2.75s đã đạt. Deploy script giữ các hosting env variables
qua release. Không áp dụng cấu hình này toàn máy hoặc production khi chưa kiểm.

## Trạng thái kích hoạt

Source local đã có job/script/regressions; chưa gọi đây là CD đang hoạt động khi
chưa được owner cho commit/push/merge, tạo branch Plesk, cấu hình secrets/gate và
quan sát release job thật. Xem audit05/11 cho trạng thái hosting/AI/HTTPS hiện tại.
