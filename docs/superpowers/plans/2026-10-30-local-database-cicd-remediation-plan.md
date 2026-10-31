# Kế hoạch hoàn thiện local database và CI/CD Nhân Hòa

Ngày: 30/10/2026. Owner đã đồng ý hướng đề xuất và giao triển khai setup local/CI/CD.
Một agent thực hiện và tự review; production chưa phát hành khi rollout gate còn tắt.

Nguồn: [review F1–F9](../../feature-audits/2026-10-30-local-database-cicd-review.md),
[hướng dẫn local/deploy](../../operations/local-development-and-nhanhoa-cicd.md).

## Bước 0 — Đã hoàn thành trong review

- [x] Đối chiếu provider, framework, startup migrations, URL local.
- [x] Kiểm tra SDK/runtime, SQL services, cổng MariaDB và User Secrets không lộ giá trị.
- [x] Đọc workflow và kiểm tra GitHub branch, tên Secrets, các run gần nhất.
- [x] Xác nhận credentials trong config trùng bản public `master`, không thử dùng.
- [x] Build Release và toàn bộ test: 614 pass; ghi rõ chưa có Linux run mới.
- [x] Probe offline: 8 migration, snapshot khớp model, sinh SQL idempotent.
- [x] Publish self-contained win-x64 thành công vào thư mục ignored local.
- [x] Ghi review, hướng dẫn, kế hoạch và prompt Cursor.
- [x] Ghi nhận gói Sinh Viên N518 / Plesk, domain tctenglish.site, MariaDB 10.5.23.
- [x] Xác nhận DB cấu hình là nhtctsnn_tctenglish từ xa; chưa có local override.
- [x] TCP và greeting DB thành công; kết nối VerifyFull lỗi server không hỗ trợ SSL.
  Chưa đăng nhập SQL, chưa kiểm tra số lượng dữ liệu hoặc history production.
- [x] Owner xác nhận database production; đọc backup SQL 26/09/2026: 52 bảng,
  8.260 rows, 5 migration history; đối chiếu 4 migration tháng 10 chưa có ở backup.
- [x] Xác định Plesk Git chỉ đang ở form Add Repository; post actions là khả năng
  cần kiểm chứng, chưa phải đường deploy đã hoạt động.

## Bước 1 — P0: Thu hồi credentials đã public (F1)

Phụ thuộc: owner có quyền trên Nhân Hòa, OAuth providers, SMTP và GitHub Secrets.
Files: `TCTEnglish/appsettings.json`, `.gitignore` nếu cần, hướng dẫn setup;
cấu hình production và GitHub Secrets là state ngoài repository.

- [ ] Owner/triển khai được ủy quyền rotate database password, OAuth secrets,
  SMTP password; kiểm kê credentials khác trong history để xử lý nếu có bằng chứng.
- [ ] Cập nhật các nơi dùng secret mới, kiểm tra tính năng bằng tài khoản test.
- [x] Thay giá trị tracked bằng cấu trúc/placeholder an toàn, chuyển local sang
  User Secrets. Không sửa `appsettings.json` nếu chưa được owner giao rõ.
- [x] Scan thay đổi và artifact, chỉ ghi tên/vị trí phát hiện, không ghi secret.
- [ ] Nếu chọn rewrite history, lập phạm vi/ảnh hưởng riêng và xin chỉ định owner;
  không coi rewrite là bước thay cho rotation.

Chấp nhận: credentials cũ không còn hiệu lực; bản release/artifact/repo mới không
chứa giá trị thật; kết nối ứng dụng dùng secret mới hoạt động. Ghi rõ phần kiểm
tra nào cần owner thực hiện, không suy ra từ việc tên secret tồn tại.

## Bước 2 — P1: Local MariaDB tách biệt (F2, F7)

Phụ thuộc: đã biết MariaDB 10.5.23; Docker là phương án local đề xuất vì máy đã
cài Docker Desktop. Đã khởi tạo container/database; xem ledger kiểm chứng cuối.
Files dự kiến: `README.md`, tài liệu operations, `.config/dotnet-tools.json`;
script kiểm tra local nếu hữu ích. Không cài dịch vụ hệ thống khi chưa được giao.

- [x] Cài/chạy MariaDB local đúng dòng tương thích, tạo database/user dev.
- [x] Đặt User Secrets loopback; kiểm tra environment/CLI override không trỏ host.
- [x] Pin EF tool 9.x phù hợp trong manifest nếu được giao triển khai tooling;
  không thay global tool 10 đang dùng cho dự án khác.
- [ ] Chạy migrations trên database mới, khởi động web và kiểm tra login, tạo set,
  ghi dữ liệu và restart. Không áp dụng thử lên production.
- [ ] Nếu restore data hosting, giữ history và ẩn dữ liệu nhạy cảm trước khi test.
- [x] Với backup owner gửi, cấu hình F9 `lower_case_table_names=1` trước lần khởi
  tạo datadir Docker đầu tiên, giữ nguyên ZIP ngoài Git; kiểm tra baseline row counts
  và 5 history IDs sau restore trước khi chạy EF. Không tạo fresh schema rồi import
  dump có DROP vào database đang dùng chung; dùng database dev độc lập.
- [x] Sửa README SQL Server/cổng cũ, ghi rõ profile `http` ở localhost:5100.

Chấp nhận: thao tác local chỉ ghi DB dev; schema mới tạo được; test thật qua web
hoạt động; clone mới có hướng dẫn tái lập. SQLite tests không thay thế tiêu chí này.

## Bước 3 — P1: Kiểm chứng đường nâng cấp database (F3)

Phụ thuộc: bước 1, database dev; backup/schema/history production được cung cấp
qua kênh phù hợp, không gửi credentials vào chat.
Files: `TCTEnglish/Migrations/*`, DbContext/snapshot nếu cần sửa có thẩm quyền,
integration tests/script kiểm chứng MariaDB; không sinh migration chỉ từ suy đoán.

- [x] Đọc newest/symptom-relevant bug memory trước mọi sửa lỗi.
- [x] Kiểm kê `__EFMigrationsHistory` trên bản sao và đối chiếu schema thật.
- [x] Backup có migration DROP đã áp dụng, có thêm history ID 20260509145534
  không còn source. Đối chiếu schema, không sửa/xóa history để làm số lượng khớp.
  Test 4 migration tháng 10 trên bản sao, xác minh dữ liệu và history sau nâng cấp.
- [x] Xác định `AddReadingUserTranslation` có pending hay không; nếu pending với
  dữ liệu đã có, thiết kế đường bảo toàn dữ liệu, không chạy DROP để vượt lỗi.
- [x] Không sửa migration đã phát hành hoặc ghi khống history; mọi phương án
  thay đổi baseline phải có bằng chứng, kế hoạch chuyển tiếp và owner review.
- [x] Test fresh install, upgrade từ bản sao, chạy lại không áp dụng trùng,
  unique/index/FK và bảo toàn các bản dịch/bình chọn.
- [x] Sinh SQL/bundle để review, kiểm tra Up có DROP/TRUNCATE/đổi kiểu mất dữ liệu.
  Các thao tác phá hủy vẫn cần owner cho phép riêng theo AGENTS.md.

Chấp nhận: không mất row/khóa liên kết đã có; migrations trên MariaDB thật pass;
chạy lại không thay dữ liệu ngoài chủ đích; có restore drill và báo cáo sai lệch.

## Bước 4 — P1: CI Linux đáng tin cậy (F4)

Files: `.github/workflows/deploy.yml` hoặc workflow CI riêng,
`TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs`, cấu hình test
liên quan; `Program.cs` chỉ khi đã được owner cho phép thay đổi.

- [x] Tái hiện/bổ sung bằng chứng cho lỗi inotify của run 40830674219 trên Linux.
- [x] Giảm hoặc tắt watcher/reload không cần thiết trong test host, kiểm tra
  disposal/runtime compilation; không bỏ tests/assertion hoặc chỉ tăng sysctl
  mà không xem nguồn tạo watcher.
- [x] Thêm pull_request CI, test không phụ thuộc secret production. Chưa cấu hình
  branch protection bắt buộc check ở GitHub Settings.
- [x] Thêm MariaDB service đúng dòng phiên bản, chạy kiểm tra migration ở bước 3.
- [ ] Pin SDK/tool/action cho release có thể tái lập; lưu TRX và artifact an toàn.

Chấp nhận: full suite pass trên Linux ở cùng commit; PR không deploy production;
CI không có production credentials; không bỏ qua test project bị thiếu.

## Bước 5 — P1: Release app và database có phục hồi (F5, F6)

Phụ thuộc: bước 1–4; xác nhận FTPS/certificate, đường dẫn site, IIS module,
kiến trúc app pool, DB TLS/allowlist, quyền DDL và cơ chế backup/restore.
Files: `.github/workflows/deploy.yml`, scripts release/config/migrations,
`TCTEnglish/Program.cs` (cần thẩm quyền), health implementation và runbook.

- [x] Chọn cách chạy migration theo quyền thực tế: runner kết nối DB qua TLS,
  remote command trong mạng hosting, hoặc cơ chế được Nhân Hòa hỗ trợ.
- [x] Kiểm chứng Git post deployment actions của Plesk bằng capability probe an
  toàn trong thư mục staging ngoài web root. Nếu khả dụng, đánh giá migrator chạy
  tại hosting; không coi post actions chạy sau FTP, không auto pull master vào
  httpdocs, không bật hai luồng deploy cùng site; release chỉ bắt đầu sau CI pass.
- [x] Xử lý F8: endpoint hiện không hỗ trợ SSL; xác nhận TLS/tunnel/cơ chế nội bộ
  trước khi gửi credentials và chạy migration. Không suy ra quyền runner từ TCP
  thành công trên máy owner; không dùng kết nối không mã hóa làm phương án mặc định.
- [ ] Nếu chỉ FTP và DB không truy cập được, ghi blocker tự động hóa DB; không
  tạo endpoint HTTP chạy migration hoặc giả định FTP thực thi được exe.
- [x] Build app + bundle/SQL cùng commit; upload đúng publish output; dựng config
  bằng serializer từ step env, kiểm tra JSON với secret có quote/backslash/newline.
- [x] Dùng transport mã hóa cho tất cả bước, không tắt kiểm tra chứng chỉ.
- [ ] Serial production deploy, không hủy giữa chừng; backup/version/checksum và
  restore procedure rõ ràng; bảo toàn uploads, Data Protection keys, runtime data.
- [ ] Đưa migration production khỏi startup sang bước release; account migration
  riêng; timeout/exit code/log đã che dữ liệu nhạy cảm. Local có thể opt-in startup
  migration bằng async nếu được owner giao phạm vi này.
- [ ] Khi migration lỗi: dừng phát hành, không deploy code phụ thuộc schema mới.
- [ ] Khi upload lỗi: restore bộ file hợp lệ trước online; không còn `always()`
  xóa offline bất kể trạng thái. Nếu recovery lỗi, giữ maintenance và báo lỗi.
- [ ] Kiểm tra readiness có DB và smoke login/luồng chính sau phát hành; rollback
  code chỉ với schema tương thích, không auto Down/restore phá dữ liệu phát sinh.
- [ ] Diễn tập staging: thiếu secret, JSON đặc biệt, mất kết nối giữa upload,
  migration failure, health failure, hai deploy đồng thời, backup restore.

Chấp nhận: merge master phát hành đúng artifact và pending migration; dữ liệu
người dùng được giữ; run chỉ xanh sau readiness pass; các failure drill cho trạng
thái có thể phục hồi và thông báo rõ; có bằng chứng staging trước production.

## Bước 6 — P2: Tài liệu, vòng đời và bàn giao (F7)

- [x] Sửa `docs/ci-cd-deployment-guide.md` theo master/MariaDB và trình tự config
  thực tế; không khẳng định website cũ luôn an toàn khi FTP đang thay file.
- [x] Ghi cách tạo/review migration cùng feature, không sync nguyên DB local lên
  hosting; phân biệt schema, seed và dữ liệu người dùng.
- [ ] Lập việc nâng .NET LTS trước/sau mốc 10/11/2026 với khả năng tương thích
  EF/Pomelo/IIS được xác minh; không tự nâng dependency trong review này.
- [x] Chạy build/tests liên quan; nếu thay tooling chạy các kiểm tra tooling bắt
  buộc. Ghi đúng bằng chứng app, browser, MariaDB và Linux; không hoán đổi chúng.

## Blocker còn lại

Đã xác minh Git/post actions, backup client/quyền DDL, schema/history thật,
backup mới và restore/upgrade Docker, Linux CI và artifact. Migrator chạy tại
hosting qua loopback nên không cần kết nối DB plaintext từ GitHub.

Còn: credentials cũ trong public Git history chưa được rotate tại provider;
chưa có staging để diễn tập đầy đủ backup/migration/copy/readiness failure và
chưa phát hành app/schema lên production. Giữ GitHub gate false cho tới khi
hoàn tất các điều kiện này. Checklist dưới đây được chứng minh theo ledger;
các yêu cầu failure drill/rotation không được coi là hoàn thành theo CI xanh.


## Kết quả triển khai trong phiên hiện tại

- Docker Desktop đã được khởi động. Container tctenglish-mariadb-local, MariaDB
  10.5.23 pin digest, bind 127.0.0.1:3307, volume riêng, lower_case_table_names=1.
- Backup 26/09 đã restore thành công. Nâng cả 4 migration tháng 10 thành công sau
  sửa thứ tự index FK trong 2 migration chưa phát hành. Sáu bảng users/sets/cards/
  classes/readingusertranslations/readingtranslationvotes giữ nguyên counts/checksum.
- User Secrets trỏ loopback; credentials đã gỡ khỏi appsettings tracked. Credentials
  cũ trong history vẫn cần owner/provider revoke/rotate, chưa được coi là đã thu hồi.
- Web local http://localhost:5100 đang chạy; readiness 200, đăng ký/login và Admin
  qua browser thành công. Dev account chỉ ở local, thông tin .appdata/local/access.json.
- Full workspace suite 620/620 pass; deploy safety tests 8/8, tooling suite đã
  chạy lại đủ 49/49 và agent_tooling.py pass;
  fresh MariaDB baseline/upgrade/repeat check pass.
- Plesk authenticated session: Git clone và cmd/post actions thực thi được trong
  private, PowerShell 5.1, backup client có sẵn, GitHub HTTPS outbound 200. Website
  production HTTPS 200; source/root/schema production chưa được phát hành/thay đổi.
- Tạo bootstrap branch codex/production-release với enabled=false và Plesk repo
  nhận vào private/tctenglish-release. Preflight thực tế đã chạy; postaction
  được đặt lại script release và Automatic. HTTPS POST webhook trả 204, GET 404.
  PLESK_WEBHOOK_URL đã lưu GitHub Secret, không in URL vào log.
- Backup mới 30/10: 59 users, 52 bảng, 5 history IDs, grants có DDL. Dump private
  1.406.414 bytes đã đối chiếu SHA256, restore Docker, nâng 4 migration và chạy
  lại không thay đổi checksum của sáu bảng hay history. Local dev vẫn giữ ZIP
  26/09 và admin riêng, không thay dữ liệu owner đang làm việc.
- PR #120 (codex/local-cicd-setup) chỉ chứa setup, không gom các feature đang sửa.
  CI cuối 41057658970 đã qua Linux 563/563, MariaDB và tạo đầy đủ Windows app/
  bundle/SQL idempotent, có POST webhook regression check. PR đã merge thành
  ddea475542c1000d5bfc6beb038ff1481e956d81 trên master. Working tree đã đồng bộ
  HEAD/index; so hash xác nhận giữ nguyên nội dung đủ 175 file modified/untracked.
  Check tooling trên master lỗi tải action tạm thời, SHA kiểm chứng tồn tại;
  rerun attempt 2 đã pass, không đổi pin để né lỗi dịch vụ.
- CI trên master 41058398407 cũng đã hoàn tất SUCCESS: build/tests/MariaDB/
  Windows publish/migration bundle/artifact pass; release job SKIPPED đúng gate.
- PLESK_DEPLOYMENT_ENABLED=false; không báo production CD đã hoàn tất cho tới khi
  preflight/backup/rollback drill, credentials và approval phát hành đáp ứng.
- Diagnostic Git repo tctenglish-cicd-check đã gỡ; folder private diagnostic
  chuyển Recycle Bin. Container migration regression tạm đã xóa sau đối chiếu
  label; container/volume dev và các workload Docker có sẵn của owner được giữ.
