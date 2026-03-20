# Backlog Kiến Trúc TCT English (Ưu tiên)

Backlog này phản ánh codebase hiện tại sau đợt refactor tháng 03/2026, không
còn bám theo trạng thái trước khi tách controller.

Mục tiêu chính:
- Giữ vững các cải thiện về bảo mật và chống hồi quy
- Hoàn tất phần service extraction còn dang dở
- Chỉ sau đó mới mở rộng sang payment, AI, và các module học mới

Ảnh chụp nhanh codebase hiện tại:
- Ứng dụng ASP.NET Core MVC trên .NET 10, có các ranh giới hữu ích như
  `Areas/Admin`, `Hubs`, `Workers`, `Services`
- `HomeController` giờ chỉ còn dashboard và các trang public
- Các route legacy `/Home/*` vẫn được giữ, nhưng logic class/folder/set/study/chat
  đã nằm trong controller riêng
- `ViewModels/` là thư mục typed view model chuẩn duy nhất
- Service layer đã có nền tảng thật:
  `IClassService`, `IStudyService`, `IStreakService`, `IFileStorageService`,
  `IAvatarUploadService`, `IAppEmailSender`, `IYoutubeTranscriptService`
- Đã có test project riêng: `TCTEnglish.Tests`

## Những gì đã có sẵn

| Hạng mục | Trạng thái hiện tại | Bằng chứng |
|---|---|---|
| Regression coverage tối thiểu | Đã có và đang mở rộng | `Sprint1-4`, `CriticalFlowSqliteIntegrationTests`, `FolderSetIdorRegressionTests` |
| Tách `HomeController` | Đã xong | `ClassController`, `FolderController`, `SetController`, `StudyController`, `ChatController` đã nhận phần logic được tách |
| Chuẩn hóa current-user lookup | Đã xong cho MVC/API | `BaseController` + `CurrentUserIdExtensions` |
| Audit anti-forgery/bảo mật ở các flow đã đụng tới | Phần lớn đã xong | Chat upload, join class, folder/set mutation, learning API, admin mutation |
| Loại bỏ EF sync write ở request handler | Đã xong | Không còn `SaveChanges()` sync trong request handler |
| Tách shared streak logic | Đã xong | `IStreakService` + `StreakService` |
| Tạo abstraction cho file storage | Đã xong | `IFileStorageService`, `LocalFileStorageService`, `ImageUploadPolicies` |
| Tách service cho vocabulary pages | Đã xong | `VocabularyController` gọi `IStudyService` |
| Chuẩn hóa thư mục ViewModel | Đã xong | Không còn `TCTEnglish/ViewModel/`, chỉ còn `ViewModels/` |
| Structured logging baseline | Mới xong phần quan trọng | Các controller/service/hub chính đã dùng `ILogger<T>` |

## Việc còn lại theo ưu tiên

## P0 - Ổn định các vùng còn nóng

### 1. Hoàn tất service extraction cho các domain còn controller nặng

Lý do:
- Phần kiến trúc khó nhất đã qua.
- Rủi ro hồi quy tiếp theo hiện tập trung ở các controller vẫn còn trộn EF,
  validation, và business branching trực tiếp.

Các mục tiêu chính:
- `AccountController`
- `FolderController`
- `SetController`
- `SpeakingController`
- `Areas/Admin/UserManagementController`

Hướng làm:
- Tách theo workflow có ranh giới rõ, không cần tạo service cho mọi entity.
- Ưu tiên các lát như account security, folder/set mutations, speaking progress,
  admin user-management.

Điều kiện đạt:
- Controller chủ yếu làm orchestration.
- Validation, persistence, và authorization rules có thể tái dùng và test độc lập.

### 2. Quyết định rõ số phận của feature Goals

Trạng thái hiện tại:
- `GoalsController` chỉ trả về một trang tĩnh.
- Dashboard đã có đọc `User.Goal`.
- Trang Goals vẫn là UI mẫu, chưa phải flow thật.

Vì sao đây là P0:
- Người dùng đã nhìn thấy route này, nên đây là vấn đề sản phẩm nhìn thấy được.

Hai hướng hợp lý:
- Implement flow goal thật sự.
- Hoặc ẩn route/nav cho tới khi feature sẵn sàng.

Điều kiện đạt:
- Chỉ còn một workflow goals chính thức.
- Không còn hiển thị dữ liệu demo/tĩnh cho người dùng production.

### 3. Hoàn thiện vận hành lock/unlock tài khoản

Trạng thái hiện tại:
- `UserManagementController.BlockUser` đã set `LockExpiry` đúng kiểu future date.
- `AutoUnlockWorker` tồn tại và khỏe hơn trước.
- Worker vẫn đang bị config-gate và mặc định chưa bật.

Vì sao cần làm:
- Code đã tốt hơn, nhưng feature chưa vận hành trọn vẹn nếu config môi trường
  chưa được chốt an toàn.

Điều kiện đạt:
- Hành vi auto unlock được bật rõ ràng theo từng môi trường.
- Dữ liệu lock hiện có được kiểm tra trước khi rollout.
- Manual unlock, lazy unlock lúc login, và worker cho kết quả nhất quán.

### 4. Bổ sung test cho các domain chưa được service hóa hoàn toàn

Bộ test hiện tại đã là nền khá tốt, nhưng giá trị tiếp theo nằm ở:
- account/profile/settings/password flows
- admin speaking-video management
- upload edge cases và cleanup behavior
- worker/config behavior cho lock expiry

Điều kiện đạt:
- Các controller còn nhiều logic đều có regression coverage trước vòng refactor tiếp theo.

## P1 - Dọn nốt phần nền tảng

### 5. Rà lại chiến lược seeding lúc startup

Trạng thái hiện tại:
- `Program.cs` vẫn seed từ `system-vocabulary.json` ở mỗi lần khởi động.
- Seeder đã là kiểu upsert nên giảm rủi ro duplicate.
- Nhưng startup vẫn còn scan và có thể ghi DB ở mọi lần boot.

Hướng làm:
- Gate seeding theo environment hoặc feature flag.
- Cân nhắc thêm cơ chế change detection nhẹ trước khi vào sync path nặng.

### 6. Gỡ secrets khỏi config nằm trong source control

Trạng thái hiện tại:
- OAuth, SMTP, và connection string vẫn nằm trong `appsettings.json`.

Hướng làm:
- Dùng User Secrets cho local development.
- Dùng environment variables hoặc secret store cho môi trường deploy.

### 7. Bổ sung pagination và review index ở các chỗ refactor chưa chạm tới

Những việc còn đáng làm:
- Phân trang cho các flow search/discovery public.
- Review index cho learning progress, class membership, và speaking filter.

Vì sao làm ở giai đoạn này:
- Phần ổn định/bảo mật đã đủ tốt để chuyển sang tối ưu query hiệu quả hơn.

### 8. Tiếp tục rollout structured logging

Trạng thái hiện tại:
- Các luồng quan trọng đã dùng `ILogger<T>`.
- Baseline hiện tại đủ giữ, nhưng chưa phủ hết mọi controller/service.

Hướng làm:
- Khi refactor nốt từng controller còn lại, chuẩn hóa luôn shape log cho
  request/user/class/set và các failure case.

## P2 - Để sau khi roadmap đã rõ

### 9. Payment và subscription domain

Chỉ làm khi premium plan hoặc paid order đã được chốt thật sự.

Giữ lại từ review cũ:
- payment service riêng
- callback idempotent
- cập nhật trạng thái transaction-safe
- payment và order là hai khái niệm riêng

Chưa nên làm ngay:
- build full payment architecture trước khi requirement ổn định

### 10. AI chat và streaming UX

Chỉ nên làm sau khi:
- service extraction còn lại đã xong
- xử lý user/request identity đã ổn định
- logging và rate limiting đã rõ ràng

SignalR vẫn là lựa chọn hợp lý vì app đã có sẵn, nhưng chưa phải hạng mục nền
tảng cần làm tiếp ngay.

### 11. Content model dùng chung cho Reading/Writing/Listening/Grammar

Nên chờ tới khi ít nhất một module mới có requirement authoring và
progress-tracking cụ thể.

Cách rollout an toàn hơn:
- làm một module mới có scope nhỏ trước
- kiểm chứng authoring/query/reporting needs
- rồi mới quyết định có đáng gom vào `ContentItem` chung hay không

## Những việc không nên làm ngay

- Viết lại thành modular monolith đầy đủ
- Thêm repository pattern đè lên EF Core
- Thiết kế polymorphic content schema lớn trước khi module mới đầu tiên ra mắt
- Chuyển cloud storage khi media growth chưa đủ lớn để gây đau vận hành
- Redesign domain quy mô lớn trước khi các controller còn lại được ổn định

## Thứ tự triển khai gợi ý

### Sprint A - Ổn định
- Tách service tiếp cho account/folder/set
- Quyết định hướng xử lý feature Goals
- Bổ sung regression coverage cho các vùng còn nhiều logic

### Sprint B - Hardening vận hành
- Chốt config rollout cho lock/unlock
- Gate hoặc tối ưu startup seeding
- Chuẩn hóa thêm structured logging

### Sprint C - Dọn phần scale
- Thêm pagination cho các flow search/discovery
- Review và thêm database indexes cần thiết
- Đánh giá lại mức sẵn sàng trước khi làm roadmap lớn

## Điều kiện đạt trước khi bắt đầu Payment, AI, hoặc module học mới

Nền tảng được xem là đủ tốt khi:
- các flow còn controller nặng đã có service boundary rõ
- goals/account/lock behavior thống nhất ở mức sản phẩm
- các route quan trọng vẫn có smoke/integration coverage
- current-user lookup và authorization rules được giữ chuẩn hóa
- startup behavior có thể dự đoán và cấu hình theo môi trường
- structured logging ổn định trên các domain quan trọng
