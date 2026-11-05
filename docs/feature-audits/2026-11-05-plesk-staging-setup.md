# N518/Plesk staging — kết quả05/11/2026

Owner yêu cầu kiểm tra hosting, đủ khả năng thì thiết lập ngay. Owner tự đăng
nhập Plesk và Hostinger trong Chrome riêng. Một agent thực hiện; không commit/
push/merge/remote CI hoặc bật production. [Runbook](../operations/staging-plesk-runbook.md).

## Khả năng xác minh trực tiếp

Plesk Account: Sinh Vien N518 Active; subdomains0/Unlimited,MySQL1/2 trước setup,
disk362.7MB/2GB,DB4.6MB/2GB; permissions hosting/DNS/subdomains/IIS pool/Git/backup
On. Dedicated IIS pool On, web deploy Off. Có .NET Toolkit và File Manager.
MariaDB10.5.23; runtime triển khai là win-x64 self-contained của net9.0 hiện tại.
Không cần mua server mới để có staging ban đầu.

DNS authoritative ở Hostinger, không ở Plesk: aster/helios.dns-parking.com.
Chỉ thêm A staging→103.28.36.90 TTL300, giữ A@ và CNAMEwww. Public resolver và
resolver local xác nhận A mới; website chính vẫn200.

## Thiết lập và nghiệm thu

- Site274, root `\staging.tctenglish.site`, dedicated IIS pool riêng64bit,
  loadUserProfile=true. Một document root bị auto-fill nối trùng lúc tạo đã được
  sửa trước upload; thư mục trùng chưa được dùng làm app root. Đã kiểm chỉ có
  file mặc định và App_Data trống rồi chuyển riêng folder thừa vào Recycle Bin;
  root staging/httpdocs giữ nguyên, readiness vẫn200.
- DB `nhtctsnn_tctenglish_staging`, user mới `nhtctsnn_tct_stage`, all-databases
  unchecked khi tạo. Production DB hiện có không được migrate/import/reset.
- Schema SQL idempotent EF9.0.4 từ migration hiện có, import795queries;12history
  đến AddWritingAcceptedAnswers,12badges,Users0 trước bootstrap. Không sinh schema
  hoặc sửa history bằng tay. Historical cleanup DROP IF EXISTS trong migration
  đã review không đụng bảng có dữ liệu của owner; target ban đầu hoàn toàn trống.
- Build/publish180MB,prepare-publish loại Development/User Secrets/runtime data;
  ZIP gốc80288948bytes SHA256
  `873E50998290D2B132DF26CA5EEE3D47F46EC3D47A57F9036B75B8B4037B774C`.
  Chia5ZIP dưới50MB/upload; extract đúng root, giữ offline đến khi TLS/config sẵn.
- Config copy riêng Staging, không sửa Program/appsettings/csproj tracked. Host
  environment Staging +DB connection đúng site đã đối chiếu nội bộ, không in
  password. Keys API local/production không sao chép. Speech tắt, Writing Local;
  worker/SMTP/payment side effects được giới hạn theo runbook.
- HTTPS Let's Encrypt valid đến03/02/2027; root Basic Auth ban đầu làm ACME401,
  cấp lại thành công khi giữ offline và tạm gỡ auth, sau đó khôi phục root+user.
  TLS probe thật không bỏ qua certificate checks. Auto-renew qua auth chưa đạt,
  cần exception/maintenance theo runbook, không báo đã nghiệm thu auto-renew.
- Ba tài khoản ngẫu nhiên được tạo qua Register UI. Cấp Admin/Premium cho đúng
  synthetic user1/3 trong DB staging, guard email/name/role; Standard user2 giữ
  nguyên. Không thay role người dùng thật; private credential files bị Git ignore.
- Admin tạo Writing1; hai learner mở cùng học liệu200,Admin403. Standard goal2
  hoàn tất bài→1/2,50%;Premium goal5→0/5,0%. Reload/chạy lại schema giữ riêng biệt.
- `/health/ready` authenticated200; `/tct-release.json`200, environmentStaging,
  dirty snapshot/base8203e30 được ghi rõ; assembly SHA256
  `D4D5FF7530DB8117C9D6776EA3B4141B365705FF5210543F93C7246CB5084944`.
- Anonymous staging401; authenticated appsettings.json/Staging.json/web.config,
  .git/HEAD và ZIP sau dọn đều404. Không dùng skipped live tests làm bằng chứng.
- Backup79581bytes đã restore vào DB Docker riêng, counts3/12/12/1/2/1 như runbook.
  SQL backup không có USE/CREATE DATABASE để trỏ khỏi target restore. Không reset
  DB local hoặc dừng container/workload khác. Repeat schema trên hosting cũng
  giữ đúng các counts và goals. Ảnh Goals mobile đã được xem trực tiếp.
- GitHub variable đọc mới2lần: PLESK_DEPLOYMENT_ENABLED=false. Production root GET200.
  Không sửa Plesk prod root/pool/Git release repo/webhook/DB/cert hoặc main DNS.

Ứng dụng là **snapshot local đã review** của worktree hiện tại, không phải code
đã được push/merge master trong lần này. Không thêm CD staging tự động. Bộ test
929pass/18skip/.NET,Node58 và Python21 là evidence ngay trước publish cùng source;
không chạy lại full chỉ vì upload file hoặc thay cấu hình hosting.

## Giới hạn và bằng chứng riêng

Chưa cấu hình OpenAI/OAuth/SMTP/payment thật cho staging; chưa paid/live/mic.
Chưa nghiệm thu tự gia hạn certificate qua Basic Auth; quy trình thủ công đã
thực hiện và có runbook. Gói đã dùng2/2MariaDB DB, staging/prod dùng chung quota
subscription, không phải cách ly tài nguyên như hai VPS.

Browser UI có nhiều AJAX race/selector timeout: đã đọc lại state trước retry;
không coi timeout sau click là mutation thất bại. SQL editor ban đầu bị navigation
ghi đè bằng SELECT Badges; đã đối chiếu actual rows và dùng form SQL hiện có với
CSRF đúng để áp role/query metadata, luôn ghi rõ database staging. Không log
token/cookie/password hoặc private response contents.

Chrome native launch qua shell bị auto-review chặn “blocked by policy”; không
thử bypass bằng process/tool khác. Owner tự mở Chrome bằng lệnh được hướng dẫn,
đăng nhập trực tiếp; agent chỉ kết nối cửa sổ đó qua loopback CDP. Không hạ kiểm
tra TLS hoặc native trust. Dùng các browser context synthetic riêng để nghiệm thu.

Private evidence: `.tmp/staging-plesk/evidence`, publish/schema logs, marker/hash;
credential/backup ở `.appdata/local/staging`, không đưa vào Git. Backup hash và
restore target có trong runbook. Không cung cấp password qua chat cuối.
