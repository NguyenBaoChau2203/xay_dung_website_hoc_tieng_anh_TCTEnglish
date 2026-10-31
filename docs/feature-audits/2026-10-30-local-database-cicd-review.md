# Kiểm tra môi trường local và CI/CD Nhân Hòa — 30/10/2026

## Kết luận và phạm vi

Dự án đã có CI/CD upload ứng dụng lên Windows/IIS qua FTP và tự chạy EF
migration lúc ứng dụng khởi động. Chưa đủ bằng chứng để coi quy trình hiện tại
là một quy trình phát hành an toàn cho cả ứng dụng và database.

Yêu cầu hiện tại được xử lý ở chế độ kiểm tra, hướng dẫn và lập kế hoạch.
Không triển khai bản sửa, đổi secret, cài dịch vụ hệ thống hoặc chạy SQL trên
hosting. Các thay đổi mã nguồn đang có của owner được giữ nguyên.

Đã đối chiếu working tree, cấu hình máy, metadata GitHub Secrets, các lần chạy
Actions và file cấu hình trên nhánh public `master`. Không in giá trị secret,
không kết nối database production. Owner cho biết hosting có vẻ dùng MariaDB
và Windows; phiên bản và quyền truy cập cụ thể còn cần xác nhận.

## Hiện trạng đã xác minh

| Thành phần | Bằng chứng | Kết luận |
| --- | --- | --- |
| Database provider | `TCTEnglish/Program.cs:25` dùng `UseMySql` + `ServerVersion.AutoDetect`; csproj dùng Pomelo 9.0.0 | Mã hiện tại dùng MySQL/MariaDB, không dùng SQL Server |
| Framework | Hai csproj target `net9.0`; EF Core 9.0.4 | SDK 10 trên máy không tự đổi target ứng dụng |
| Máy phát triển | SDK 10.0.300, ASP.NET Core/.NET runtime 9.0.16 đã có | Có thể build/test target hiện tại |
| Database local | Không thấy dịch vụ MySQL/MariaDB, không thấy listener 3306/3307; Docker service đang dừng | Chưa xác minh được MariaDB local sẵn sàng; cài SSMS không giải quyết phần này |
| Kết nối Development | Chưa có User Secrets cho UserSecretsId của dự án; Development JSON không override DefaultConnection; cấu hình gốc trỏ host không phải loopback | Phải đặt connection local trước khi chạy ứng dụng |
| Startup | `TCTEnglish/Program.cs:213` gọi `Database.Migrate()` vô điều kiện, sau đó chạy seeders | Chạy ứng dụng có thể ghi vào database được cấu hình; không chạy thử với kết nối mặc định |
| URL local | `TCTEnglish/Properties/launchSettings.json` | Profile `http`: `http://localhost:5100`, môi trường Development |
| CI/CD | `.github/workflows/deploy.yml` | Push `master` hoặc dispatch; build/test; publish self-contained `win-x64`; upload FTP |
| IIS | `TCTEnglish/web.config` | Đã cấu hình ASP.NET Core Module V2, exe và Production |
| GitHub | Default branch `master`; repo public; các tên secret FTP, DB, OAuth, SMTP, Gemini, Pixabay, VNPay đã tồn tại | Không cần tạo lại toàn bộ từ đầu; chưa kiểm chứng giá trị còn hợp lệ |
| Migration | Kiểm tra offline trực tiếp DbContext với dialect MariaDB 11.4 minh họa | Phát hiện 8 migration; model/snapshot không có thay đổi chưa được ghi nhận; sinh được SQL idempotent |

## Bổ sung sau khi owner cung cấp thông tin hosting

- Owner xác nhận gói **Sinh Viên N518**, quản trị **Plesk**, domain
  `tctenglish.site`, MariaDB **10.5.23**. Chưa xác nhận FTPS, SSH, quyền chạy
  lệnh từ xa hoặc chính sách kết nối DB từ GitHub runner.
- Đối chiếu lại Development: kết nối lấy từ `TCTEnglish/appsettings.json`,
  database tên `nhtctsnn_tctenglish`, host không phải loopback, cổng 3306.
  Không có User Secrets hoặc environment override database ở process/user/machine;
  profile http cũng không override kết nối. Không thấy web local ở cổng 5100,
  MariaDB local ở 3306/3307 hoặc Docker engine đang chạy.
- Vì vậy **chạy local bằng cấu hình hiện tại sẽ dùng kết nối database từ xa**;
  chưa có bằng chứng đã tạo database dev riêng hoặc restore bản sao hosting.
  Chưa xác minh cấu hình Production thật trên Plesk/GitHub Secret có cùng database.
- TCP tới endpoint DB được cấu hình thành công. MySQL greeting không cần đăng
  nhập quảng bá `5.5.5-10.5.23-MariaDB`, khớp phiên bản owner cung cấp. Không gửi
  credentials trong bước đọc greeting.
- Probe độc lập dùng MySqlConnector 2.6.1 và yêu cầu `SslMode=VerifyFull` bị từ chối
  trước khi có session SQL: mã 1042, `Server does not support SSL`. Không hạ xuống
  kết nối không mã hóa, không chạy migration/seed hoặc SQL đọc/ghi trên server.
  Chưa biết số lượng dữ liệu và migration đã áp dụng; có thể kiểm tra qua
  phpMyAdmin trong phiên Plesk HTTPS của owner hoặc một đường kết nối bảo mật khác.
- Đây là kiểm tra cấu hình và khả năng kết nối, không phải xác nhận dữ liệu hiện
  tại trên website, không chứng minh database trống hay còn nguyên dữ liệu cũ.

## Bổ sung từ backup và ảnh Plesk owner gửi

Owner xác nhận `nhtctsnn_tctenglish` là database đang dùng trên Nhân Hòa.
Đã đọc ZIP trong Downloads bằng zipfile, phân tích SQL trong bộ nhớ, không
giải nén/import/chạy SQL, không in dữ liệu cá nhân hoặc credentials.

File `nhtctsnn_tctenglish_2026-09-26_16-56-30.sql (1).zip` có một SQL 1.271.333
byte. Footer ghi `Dump completed on 2026-09-26 16:57:18`; server MariaDB 10.5.23.
Dòng đầu ghi dump client 10.11.7, đây không phải phiên bản server.

Backup chứa 52 bảng, 8.260 row INSERT đã kiểm tra số lượng field khớp schema.
Số liệu tiêu biểu: users 57, sets 36, cards 162, folders 28, classes 15,
speakingvideos 14, speakingsentences 1.569, readingpassages 12, listeninglessons
13, writingexercises 20. Đây là số liệu snapshot 26/09, chưa xác nhận số liệu live
30/10; chưa có restore drill nên chưa tuyên bố backup phục hồi thành công.

History trong backup có 5 migration:

```text
20260429193206_InitialMariaDb
20260503111002_AddClassDB
20260506135633_AddUserFeatureTimeLogs
20260509145534_AddReadingUserTranslation
20260510020134_AddReadingUserTranslation
```

Migration có DROP trong F3 đã được ghi áp dụng trong snapshot này; bình thường
EF sẽ không chạy lại nó. Bốn migration tháng 10 trong source chưa có ở backup.
History có thêm `20260509145534_AddReadingUserTranslation` không còn file source
tương ứng; cần đối chiếu khi test restore/upgrade, không tự xóa hoặc ghi lại history.

52 tên bảng đều viết thường. Source/migrations dùng tên có chữ hoa, ví dụ
`Notifications`, `ClassMembers`, `LearningProgress`; xem F9 trước khi dựng Docker.
Không có CREATE DATABASE/USE, CREATE USER/GRANT hoặc DEFINER trong dump được
kiểm tra; có DROP TABLE/CREATE TABLE như một logical backup thông thường. Chỉ
restore vào database local riêng, không chạy file này lên production hiện có.

Ảnh Plesk: trang Git có nút Add Repository và form tạo repo; Repository URL
trống, Automatic được chọn, deployment directory mặc định `\httpdocs`, checkbox
Enable post deployment actions chưa bật. Đây là bằng chứng Git extension có sẵn,
chưa chứng minh repository đã kết nối, webhook đã chạy hoặc shell có quyền.

[Tài liệu Plesk Git](https://doc.plesk.com/en-US/obsidian/customer-guide/git-support/using-remote-git-hosting.75848/)
cho phép pull repo, webhook và lệnh sau deployment. Post actions thuộc luồng
Git deployment, không tự chạy theo FTP upload. Nếu được phép chạy exe/bundle ở
hosting thì có thể đặt migrator trong mạng hosting; cần test quyền thật. Không
trỏ source master thẳng vào web root: cần publish, CI gate và staging để release
theo đúng thứ tự. Không bật đồng thời hai luồng ghi cùng site.

## Phát hiện cần xử lý

### F1 — P0: Credentials đã có trên repository public

`TCTEnglish/appsettings.json:12`, `:16`, `:20`, `:27` chứa các giá trị không phải
placeholder cho OAuth, kết nối database và SMTP. Đối chiếu file nhánh `master`
bằng GitHub API xác nhận các giá trị này trùng với file local. Không thử sử dụng
credentials nên chưa kết luận chúng còn hoạt động.

Cần coi các credentials đó đã bị lộ: thay mới/revoke tại nhà cung cấp, cập nhật
GitHub Secrets và cấu hình hosting, đưa cấu hình local sang User Secrets, làm
sạch config tracked. Xóa nội dung ở commit mới không thu hồi được bản trong
history. Việc rewrite history, nếu chọn làm, cần kế hoạch riêng và owner cho phép;
rotation vẫn là bước đầu tiên.

### F2 — P1: Chưa tách local database khỏi kết nối hosting

Chưa có override local trong User Secrets hoặc environment variable chuẩn cho
DefaultConnection tại thời điểm kiểm tra. Với migration chạy lúc startup, lệnh
`dotnet run` dùng cấu hình mặc định có thể kết nối và sửa schema trên hosting.

Cần MariaDB local, database/user riêng và override kết nối chỉ tới loopback.
Tài liệu local phải yêu cầu hoàn thành bước đó trước khi chạy app hoặc EF CLI.

### F3 — P1: Migration cũ có thao tác phá hủy trong Up

`TCTEnglish/Migrations/20260510020134_AddReadingUserTranslation.cs:15` và `:16`
chạy `DROP TABLE IF EXISTS ReadingTranslationVotes` và
`DROP TABLE IF EXISTS ReadingUserTranslations`, rồi tạo lại hai bảng.

Điều kiện gây mất dữ liệu: hai bảng đã có dữ liệu nhưng migration này chưa được
ghi nhận trong `__EFMigrationsHistory`, khiến lần nâng cấp tiếp theo chạy Up.
Nếu migration đã được áp dụng, EF bình thường sẽ bỏ qua nó. Không kết luận
production đang ở điều kiện này vì chưa đọc database production.

Trước khi bật tự động nâng cấp: backup có thể restore; đối chiếu history/schema
trên bản sao database; chạy thử upgrade và kiểm tra dữ liệu. Không sửa tùy tiện
migration đã phát hành, không giả ghi history, không chạy DROP để vượt lỗi.

### F4 — P1: Latest GitHub Actions chưa qua CI trên Linux

[Run ngày 29/10/2026](https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish/actions/runs/40830674219)
build thành công, test thất bại, deploy bị skipped. Log có
`System.IO.IOException` về giới hạn 128 inotify instances/file descriptors.
Điều này giải thích ít nhất một nhóm lỗi HTTP 500 trong run, không chứng minh
mọi test failure chỉ có một nguyên nhân.

[Run ngày 20/05/2026](https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish/actions/runs/26138094979)
báo thành công. Đây là bằng chứng pipeline từng chạy, không xác nhận trạng thái
website hôm nay. Windows local pass không thay cho một run Linux mới trên cùng
commit. Cần xử lý nguồn tạo watcher trong test host/runtime compilation và xác
minh bằng Linux CI; không bỏ test để mở deploy.

### F5 — P1: Khi upload lỗi vẫn mở ứng dụng; thiếu recovery và serialization

`.github/workflows/deploy.yml:166` dùng `if: always()` để xóa `app_offline.htm`,
kể cả upload bị lỗi. Khi đã thay một phần DLL/config, thao tác này có thể mở
một bộ file không đồng nhất. Workflow không có concurrency cho production,
backup/restore release, hoặc kiểm tra readiness sau deploy.

Cần một deploy tại một thời điểm, release có thể khôi phục, kiểm tra kết quả
migration/upload và chỉ mở lại app khi bộ file hợp lệ. Khi không khôi phục được,
giữ maintenance và báo lỗi. Migration nên chạy trong bước phát hành riêng trước
khi mở ứng dụng, có credential và log riêng, thay cho startup production.

### F6 — P1: Secret rendering và transport chưa an toàn

`.github/workflows/deploy.yml:87` chèn `${{ secrets.* }}` trực tiếp vào heredoc
JSON. Mật khẩu có dấu nháy hoặc backslash có thể làm JSON không hợp lệ; nội suy
trực tiếp secret vào shell cũng cần được loại bỏ. Dùng biến môi trường của step
và JSON serializer, kiểm tra file mà không in nội dung bí mật.

Các bước curl tại dòng 150/169 dùng `ftp://`; FTP action không chỉ định giao
thức bảo mật. Cần xác nhận Nhân Hòa hỗ trợ FTPS hoặc một transport được mã hóa
khác, cấu hình nhất quán cho offline/upload/online và kiểm tra chứng chỉ.
Loại config Development, credentials gốc, uploads và dữ liệu runtime khỏi phạm
vi đồng bộ/xóa không phù hợp; artifact build không chứa production secrets.

### F7 — P2: Tài liệu và công cụ chưa đồng bộ

`README.md:31`, `:144`, `:159`, `:181` còn hướng dẫn SQL Server và cổng cũ.
`docs/ci-cd-deployment-guide.md` nhiều lần ghi `main`, nhưng nhánh thực tế là
`master`; hướng dẫn khôi phục khi lỗi và thứ tự ưu tiên cấu hình cũng cần sửa.
Global `dotnet-ef` trên máy là 10.0.7; repo chưa có local tool manifest. Nên pin
tool EF 9.x phù hợp với EF của dự án, không đổi global tool chỉ để làm dự án này.

.NET 9 hết hỗ trợ ngày 10/11/2026 theo
[chính sách Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
Lập nhánh nâng cấp .NET LTS và kiểm chứng Pomelo/EF/hosting riêng, không tự đổi
target framework trong lần review này.

### F8 — P1: Endpoint database hiện không hỗ trợ TLS

Probe sau khi owner cung cấp hosting xác nhận endpoint được cấu hình không
hỗ trợ SSL; connection gốc dùng `SslMode=Preferred`. TCP mở từ máy owner chưa
chứng minh runner GitHub được allowlist hoặc có quyền DDL.

Trước khi chạy migration từ runner phải có đường kết nối bảo mật: TLS trên
endpoint được Nhân Hòa hỗ trợ, hoặc tunnel/migrator bên trong mạng tin cậy.
FTP/FTPS không tự tạo đường chạy SQL. Nếu dùng phpMyAdmin qua Plesk HTTPS để
kiểm tra/backup thủ công thì vẫn phải phân biệt với tự động hóa release.

MariaDB 10.5 đã hết community maintenance từ 24/06/2025 theo
[MariaDB Foundation](https://mariadb.org/about/). Local có thể dùng phiên bản
10.5.23 để tái hiện môi trường cũ, nhưng cần kế hoạch nâng DB được hosting hỗ trợ;
không tự nâng major hoặc apply schema lên dữ liệu thật trong lần kiểm tra này.

### F9 — P1: Restore backup lowercase vào Docker Linux cần cấu hình case phù hợp

Backup Windows có 52 tên bảng lowercase, trong khi EF dùng nhiều tên PascalCase.
MariaDB Linux mặc định phân biệt hoa/thường; sau restore có thể gặp table not
found hoặc tạo bảng history khác tên nếu cấu hình không tương thích.

Dựng instance dev mới với `lower_case_table_names=1` trước khi khởi tạo datadir,
hoặc thiết kế phương án chuyển đổi tên bảng có kiểm chứng. Không đổi tùy tiện
tham số trên volume đã có dữ liệu. Test query qua EF, nhận đúng history sau
restore, chạy bốn migration mới và kiểm tra row counts/khóa liên kết. Xem
[tài liệu MariaDB](https://mariadb.com/docs/server/reference/sql-structure/sql-language-structure/identifier-case-sensitivity).

## Kiểm chứng

- `dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release --nologo --verbosity minimal`:
  thành công, 0 lỗi, 35 cảnh báo sẵn có.
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --nologo --verbosity minimal --logger "trx;LogFileName=local-cicd-audit.trx" --results-directory .tmp/local-cicd-audit`:
  614 pass, 0 fail, 0 skip, thời lượng test 1 phút 52 giây.
- TRX: `.tmp/local-cicd-audit/local-cicd-audit.trx` (ignored).
- Probe offline khởi tạo DbContext trực tiếp, không chạy host/seeders: 8 migration,
  `HasPendingModelChanges() == false`, SQL idempotent 113.808 ký tự. Dialect 11.4
  chỉ phục vụ sinh SQL, chưa xác minh phiên bản hosting.
- `dotnet publish TCTEnglish/TCTEnglish.csproj --configuration Release --runtime win-x64 --self-contained true --output .tmp/local-cicd-audit/publish --nologo --verbosity quiet`:
  exit 0. Artifact chỉ lưu local trong thư mục ignored, chưa upload lên hosting.
- Kiểm tra bốn tài liệu mới: các link file nội bộ tồn tại, code fence cân bằng,
  không có trailing whitespace. Không sửa source/config/workflow trong review.
- Backup inspection bằng script ignored: đọc ZIP/CRC và parse đủ INSERT;
  8.260 row khớp số lượng field của schema, không thực thi SQL. Đối chiếu history
  với các file migration hiện tại và giữ nguyên mọi dữ liệu nguồn.
- Chưa chạy SQL trên MariaDB thật, chưa thử restore/upgrade bản sao production,
  chưa kiểm tra browser local, FTP hoặc health production. Test SQLite/InMemory
  không chứng minh migration MariaDB chạy được.

## Tài liệu tiếp theo

- [Hướng dẫn local và lựa chọn CI/CD](../operations/local-development-and-nhanhoa-cicd.md).
- [Kế hoạch sửa theo ưu tiên](../superpowers/plans/2026-10-30-local-database-cicd-remediation-plan.md).
- [Prompt bàn giao Cursor](../../.ai/prompts/cursor-local-database-cicd-remediation.md).

## Cập nhật sau triển khai đã được owner chấp thuận

Các phát hiện ở trên ghi trạng thái review ban đầu. Sau đó đã dựng Docker dev,
restore ZIP 26/09, nâng migration và chạy local/browser thành công. Full workspace
suite 620/620; CI setup riêng 563/563 trên Linux, MariaDB upgrade/repeat pass và
đóng gói app/bundle Windows thành công.

Plesk đã xác minh Git, PowerShell 5.1/post actions và backup/DDL. Database live
ngày 30/10 có 59 users, 52 bảng, đúng 5 history IDs như backup cũ. Dump mới đã
được tải qua HTTPS, đối chiếu SHA256, restore Docker và nâng đủ 4 migration;
sáu bảng được đối chiếu giữ nguyên checksum. Không thay schema/source thật.

GitHub/Plesk release branch riêng và HTTPS POST webhook đã kết nối. Gate GitHub
false, manifest disabled; chưa bật phát hành production. Credentials cũ trong
Git history cần rotate; full staging failure drill còn chờ. Xem runbook mới và
ledger trong plan để tiếp tục, không dùng các dòng “chưa kiểm tra” lịch sử như
trạng thái hiện tại.

## Tham chiếu chính thức

- [EF: áp dụng migration và migration bundle](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).
- [Pomelo 9.0.0 và các phiên bản MariaDB đã kiểm thử](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/releases/tag/9.0.0).
- [User Secrets cho Development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-9.0).
- [FTP Deploy Action: protocol và các tùy chọn đồng bộ](https://github.com/SamKirkland/FTP-Deploy-Action/tree/v4.3.5).
