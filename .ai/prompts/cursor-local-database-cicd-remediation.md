# Cursor — local MariaDB và CI/CD Nhân Hòa

## Chế độ và thẩm quyền

Một agent làm toàn bộ điều tra/triển khai/review/test. Owner đã đồng ý triển khai
local Docker, sửa cấu hình startup/publish và thiết lập CI/CD trong phiên 30/10.
Các bước này đã thực hiện; đọc trạng thái mới dưới đây trước khi tiếp tục.
Không hỏi lại quyền đã có. Production CD vẫn tắt; việc phát hành thật, rotation
tại provider và diễn tập lỗi đầy đủ là phần còn lại, không tự coi đã hoàn tất.

Đọc AGENTS.md hiện hành, giữ nguyên thay đổi có sẵn. Reuse nội dung đã đọc;
CodeGraph trước cho indexed source, bounded reads cho config/docs hoặc phần
thiếu/stale. Không index/rebuild, không agent phụ. Context7 chỉ cho tài liệu thư
viện ngoài nếu tool thực sự có; không gửi config/private code vào dịch vụ đó.

## Đọc tập trung

1. `docs/feature-audits/2026-10-30-local-database-cicd-review.md` — F1–F9.
2. `docs/superpowers/plans/2026-10-30-local-database-cicd-remediation-plan.md` —
   chỉ bước đang được giao và dependency của bước đó.
3. `docs/operations/local-development-and-nhanhoa-cicd.md` — hướng dẫn owner.
4. Skill cần cho bước đang làm: tct-superpowers, tct-engineering,
   tct-ef-migration, tct-security-audit; tooling review nếu thay tooling.

## Trạng thái mới nhất sau thực thi ngày 30/10

- Local http://localhost:5100 chạy MariaDB Docker 10.5.23, User Secrets loopback;
  bản ZIP 26/09 đã restore và nâng đủ migration. Admin local xem
  `.appdata/local/access.json`, không in mật khẩu vào chat.
- Bản sao mới từ hosting có 59 users/52 bảng/5 history; đã dump private, tải HTTPS,
  so SHA256, restore Docker, nâng 4 migration và kiểm tra sáu table checksums.
- Full workspace suite 620/620; setup PR #120 Linux 563/563; app/bundle Windows
  và SQL idempotent đã build thành công. Hai migration tháng 10 chưa phát hành
  được sửa thứ tự thay index hỗ trợ FK trong working tree; không gom feature
  đang sửa vào PR setup. Không sửa migration May đã áp dụng.
- Plesk Git/post actions/PowerShell 5.1, backup client và quyền DDL được xác minh.
  Nhánh release riêng trong private đã Automatic; HTTPS POST webhook trả 204 và
  preflight chạy. GET trả 404 nên publisher dùng POST và có regression tests.
- GitHub Secret PLESK_WEBHOOK_URL đã lưu; PLESK_DEPLOYMENT_ENABLED=false,
  manifest bootstrap enabled=false. Website/schema thật chưa được phát hành.
- Credentials đã gỡ khỏi config tracked nhưng giá trị cũ còn public Git history:
  cần rotate tại DB/OAuth/SMTP provider rồi cập nhật config hosting.
- Dùng runbook mới `docs/operations/local-development-quickstart.md` và
  `docs/operations/plesk-cicd-runbook.md`; tránh thực hiện lại các probe/restore
  đã có bằng chứng. Không xóa volume dev hoặc ghi đè dữ liệu đang làm việc.

## Bằng chứng review ban đầu (lịch sử, đã có thay đổi sau đó)

- Mã hiện dùng Pomelo MySQL/MariaDB 9.0.0, EF 9.0.4, net9.0; `Program.cs`
  auto-detect server rồi `Database.Migrate()` vô điều kiện lúc startup.
- Local có SDK 10.0.300/runtime 9.0.16; chưa thấy MariaDB ở service/cổng thông
  thường, chưa có User Secrets của project. Connection mặc định trỏ host ngoài.
- Hosting owner xác nhận Sinh Viên N518 / Plesk, domain tctenglish.site,
  MariaDB 10.5.23; greeting từ endpoint cấu hình khớp phiên bản này.
- DB Development cấu hình là nhtctsnn_tctenglish từ xa, không có local override.
  TCP mở nhưng probe VerifyFull lỗi 1042: Server does not support SSL. Chưa có
  SQL session đọc rows/history; không hạ xuống plaintext để kiểm tra. Quyền
  FTPS/SSH/runner allowlist và cơ chế backup chưa xác nhận. Xem F8 của review.
- Owner xác nhận DB production và gửi backup dump ngày 26/09/2026: 52 bảng,
  8.260 rows, 57 users/36 sets/162 cards; 5 migration history gồm migration DROP
  đã áp dụng. 4 migration tháng 10 còn thiếu ở snapshot; history có thêm ID
  20260509145534 không còn source. Chưa restore drill hoặc xác minh dữ liệu live.
- 52 tên bảng backup đều lowercase, EF dùng nhiều PascalCase: Docker Linux mới
  cần lower_case_table_names=1 từ khởi tạo datadir, xem F9. Không xóa history/volume.
- Ảnh Plesk là form Add Repository có URL trống và checkbox post deployment actions;
  chưa cấu hình Git kết nối. Nếu post actions thực thi được, có thể đánh giá bundle
  chạy tại hosting, nhưng không auto pull source master vào httpdocs hoặc bỏ CI gate.
- GitHub default branch master, repo public; các tên secrets deploy đã có.
- Credentials DB/OAuth/SMTP trong tracked config trùng nhánh public master.
  Không in, sao chép vào tài liệu hoặc thử credentials; ưu tiên rotation.
- Latest CI run 40830674219 ngày 29/10/2026 fail test trên Linux, có lỗi inotify;
  deploy skipped. Một run ngày 20/05 báo thành công, không chứng minh host hôm nay.
- Local Release build: 0 lỗi/35 warnings; test: 614 pass/0 fail/0 skip.
- Publish self-contained win-x64 đã exit 0; chỉ tạo artifact local, chưa deploy.
- Offline probe: 8 migration, model khớp snapshot, sinh SQL idempotent được;
  chưa chạy migrations trên MariaDB thật. Không coi SQLite tests là migration test.
- `20260510020134_AddReadingUserTranslation.Up` có DROP hai bảng. Phải đối chiếu
  history/schema trên bản sao trước nâng cấp; không tự chạy DROP hoặc ghi khống history.

## Thực thi khi được giao

Ưu tiên rotation và local isolation, xác minh migration MariaDB, sửa CI Linux,
rồi release workflow có backup/migration/FTPS/concurrency/readiness/recovery.
Theo checklist và acceptance criteria của plan. Dựng config bằng serializer;
không nội suy secret trực tiếp vào JSON/shell. Chỉ upload publish output.
Không copy DB local đè production; không tự sinh migration lúc deploy.

Nếu Nhân Hòa chỉ cho FTP và không cho runner kết nối DB/remote command, nêu rõ
blocker tự động hóa DB và lựa chọn khả thi. Không tạo public migration endpoint.
Không sửa migration đã phát hành chỉ để fresh install xanh. Với bug fix đọc
symptom-relevant bug memory và bổ sung regression có ý nghĩa trước implementation.

Sau mỗi bước được giao: cập nhật checklist, review diff inline, chạy checks đúng
phạm vi. Handoff tiếng Việt: thay gì, file nào, kết quả kiểm chứng, việc owner cần
làm và blocker còn lại. Không push/dispatch/deploy production nếu chưa được giao.
