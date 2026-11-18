# TCT English - Known Issues & Technical Debt

This document tracks the confirmed remaining issues after the March 2026
controller/service/view-model refactor. Agents should read this before
proposing follow-up work so they do not optimize already-completed items.

---

## Current Status Snapshot

- `HomeController` is now limited to dashboard/public pages; class, folder, set,
  study, and chat flows live in dedicated controllers while keeping legacy
  `/Home/*` routes.
- `ViewModels/` is the single feature view-model folder; the legacy
  `TCTEnglish/ViewModel/` folder is gone.
- `CurrentUserIdExtensions` plus `BaseController` now own claim parsing for MVC
  and API flows; direct inline `ClaimTypes.NameIdentifier` parsing in
  controllers has been removed.
- `TCTEnglish.Tests` exists and covers smoke routes, SQLite integration, SignalR
  chat upload authorization, vocabulary system pages, and folder/set anti-IDOR
  regressions.
- Earlier refactor blockers around `SpeakingController` includes, SignalR method
  name mismatches, class detail read-scope leakage, and split folder/set IDOR
  gaps are resolved.

---

## Known Bugs (Unresolved)

### CONTACT-REFRESH-ACCEPTANCE — backend repaired & Claymorphism UI completed locally, remaining scope 2026-11-17

- Contact step 01 repaired backend contracts, preserved failed/unknown forms, and added abuse guards.
- Contact step 02 adopted the shared Claymorphism system (`.tct-theme`, `_PageHeader`, `.tct-clay-surface`,
  `btn-send`, accessible UTF-16 code units counter, accessible Bootstrap accordion FAQs, focus announcement,
  and bfcache restore). 43/43 .NET tests and 7/7 Node UI interaction tests passed.
- Step 03 repaired invalid-submit prevention, stale ARIA, quick-login route,
  undefined CSS token/private palette and misleading result copy. Chrome verified
  anonymous appeal, 1440/1024/768/390/375px layouts, Tab/Enter, reduced motion and
  retained 503 form. Native 200% zoom remains unverified; 720px reflow is separate.
- Live-mail follow-up 2026-11-17: after the owner authorized configuration,
  sender/password were saved in local User Secrets with unrelated settings
  preserved. One actual Contact form submission returned SMTP Accepted; the
  owner confirmed its exact marker in Gmail Inbox. No retry or second message.
  The earlier missing-configuration blocker is resolved locally. The protected
  shared `_Layout.cshtml` footer still publishes unverified contact rows;
  the protected shared-UI cleanup proposal remains outstanding.
- Release constraint: the guard is in-process, not distributed. Production worker/
  proxy topology was not observed. Multiple workers need a separately reviewed
  shared admission/replay design; NAT/unconfigured proxy attribution can share a
  client partition. No hosting/configuration change was authorized or performed.
- [Backend contract](../../docs/feature-audits/2026-11-16-contact-refresh/backend-contract.md),
  [step-01 ledger](../../docs/feature-audits/2026-11-16-contact-refresh/01-backend-ledger.md) and
  [step-02 ledger](../../docs/feature-audits/2026-11-16-contact-refresh/02-interface-ledger.md).
  [Step-03 evidence and dispatch accounting](../../docs/feature-audits/2026-11-16-contact-refresh/03-integration-smtp-ledger.md).

### BILLING-ROLE-CHANGE-SIGNS-OUT — đề xuất cho Châu, confirmed 17/11/2026

- **Triệu chứng (đã tái hiện bằng luồng giả lập MoMo):** ngay sau khi đơn được xác nhận, yêu cầu tiếp theo của người mua bị chuyển tới `/Account/Login`. Cùng lỗi khi Premium hết hạn (worker hoặc trang thanh toán hạ vai trò) và khi admin cấp hoặc thu hồi Premium.
- **Nguyên nhân gốc:** `SubscriptionService` đổi `User.Role` (Standard ↔ Premium). `AccountSessionValidator.IsValidAsync` (gọi từ `OnValidatePrincipal` trong `Program.cs`) so vai trò trong cookie với vai trò trong DB; thấy khác thì từ chối cookie và đăng xuất. Thêm nữa, các nơi đọc **claim** vai trò (`SetController`, `SetController.Personal`, `VocabApiController`, `AiController`, `_AiChatLauncher`) chỉ thấy Premium sau khi đăng nhập lại.
- **Đã làm trong phạm vi thanh toán (D1 = TẠM):** trang Nâng cấp và trang đơn báo trước một lần "Sau khi thanh toán xong, bạn có thể được yêu cầu đăng nhập lại…"; script theo dõi đơn nhận ra phiên đã kết thúc (401 hoặc chuyển hướng tới trang đăng nhập) và đưa người dùng tới `/Account/Login?returnUrl=/Billing/Order/{mã}`; trang đơn và lịch sử luôn hiện đúng trạng thái sau khi đăng nhập lại; lịch sử và trang Premium hạ hạn ngay trên trang (`ExpireForUserAsync`) thay vì chờ worker mỗi giờ.
- **Chưa giải quyết được ở đó:** trang đăng nhập **bỏ qua `returnUrl`** (`account-auth-ui.js` luôn chuyển tới `data-home-url`; `AccountController.Login` không xử lý `returnUrl`), nên người mua đăng nhập lại sẽ về trang chủ, không về trang đơn.
- **Đề xuất sửa (mã của Châu, chưa áp dụng):**
  1. Trong `OnValidatePrincipal`, khi mọi kiểm tra khác đều hợp lệ và **chỉ vai trò khác**, thay principal bằng cùng các claim nhưng vai trò lấy từ DB và đặt `context.ShouldRenew = true`, thay vì từ chối. Giữ nguyên: `SecurityVersion` đổi → vẫn từ chối; tài khoản bị ẩn danh hoặc bị khóa → vẫn từ chối. Test cần có: nâng vai trò → vẫn đăng nhập với claim mới; đổi `SecurityVersion` → bị từ chối; ẩn danh/khóa → bị từ chối.
  2. Cho phép `returnUrl` cục bộ (`Url.IsLocalUrl`) sau khi đăng nhập thành công (truyền xuống `data-home-url` hoặc trả về trong JSON), để `LANDING-READING-LOGIN-RETURN` và luồng thanh toán cùng được sửa.
  3. Sau khi áp dụng, xóa câu cảnh báo trong `Pricing.cshtml`/`Order.cshtml` và dòng xử lý "phiên kết thúc" trong `billing-order.js`.
- Chọn D1 = SỬA LUÔN trong một lần chạy sau nếu muốn agent áp dụng đúng thay đổi tối thiểu ở mục 1.

### BILLING-MOMO-QUERY-UNVERIFIED — confirmed 17/11/2026

- `MoMoQueryClient` (xác nhận đơn MoMo mất IPN) dùng định dạng truy vấn công khai của MoMo v2 (`accessKey&orderId&partnerCode&requestId`, kết quả `resultCode`) và **chưa được thử với sandbox thật** (không có khóa trong repo, không gọi mạng). Nó chỉ chạy khi cổng được cấu hình đầy đủ; ở chế độ giả lập cục bộ luôn trả `Unknown`.
- Cần làm trước khi bật MoMo production: chạy một giao dịch sandbox, kiểm tra truy vấn trả `resultCode=0` kèm đúng số tiền, ghi kết quả vào `docs/payment-go-live-checklist.md`.
- Bảng trạng thái `vnp_TransactionStatus` của VNPay QueryDR không có trong repo: chỉ `00` (thành công) và `02` (lỗi/hủy) được tin; mọi giá trị khác là `Unknown` và không đổi đơn. Cần đối chiếu tài liệu VNPay khi có sandbox.

### BILLING-RECONCILIATION-WORKER-OFF — cấu hình khuyến nghị 17/11/2026

- `ReconciliationWorker__Enabled` mặc định `false` (và `start-local.ps1` ép `false` ở máy cá nhân). Khi IPN không tới được máy chủ (luôn xảy ra ở localhost, đôi khi ở staging/production), đơn chỉ được xác nhận khi người mua mở lại trang trả về hoặc bấm "Kiểm tra lại ngay".
- **Khuyến nghị cho staging và production:** `ReconciliationWorker__Enabled=true`, `IntervalMinutes=5`, `MinPendingMinutes=5`. Worker hỏi nhà cung cấp về đơn `pending` và đơn `expired` trong 24 giờ gần nhất (cả VNPay và MoMo), tối đa 12 lần mỗi đơn và cách nhau ≥ 20 giây; không bao giờ đánh dấu thất bại khi nhà cung cấp chưa trả lời dứt khoát. Cần chủ dự án bật trong cấu hình môi trường (không sửa appsettings trong repo).

### BILLING-ROLE-CLAIM-PREMIUM-CHECKS — confirmed 17/11/2026

- Một số module quyết định Premium bằng **claim vai trò** thay vì `IPremiumAccessService`: `SetController` (`SuggestVocab`), `SetController.Personal`, `VocabApiController`, `AiController`, `_AiChatLauncher`, `ListeningService`/`SpeakingService`/`WritingService` đọc vai trò từ DB. Người mới mua chỉ thấy tính năng ở nhóm đầu sau khi đăng nhập lại (liên quan BILLING-ROLE-CHANGE-SIGNS-OUT).
- Đề xuất cho chủ từng module: dùng `IPremiumAccessService.HasFeatureAsync` (đọc DB, hết lỗi trễ claim). Các kiểm tra này không thuộc đợt sửa thanh toán và được giữ nguyên.

### NOTIFICATIONS-SHARED-FILES-FOR-CHAU — ghi chú bàn giao 17/11/2026

- Đợt làm lại thông báo (plan `docs/superpowers/plans/2026-11-17-notifications-complete.md`) chạm **ít dòng** vào code dùng chung/của Châu, theo quyết định D1 và D4 của Trọng. Châu xem lại khi hợp vào `master`:
  1. `Views/Shared/_Layout.cshtml` — **chỉ** nút chuông (`aria-haspopup="true"`, `aria-controls` chỉ khi đã đăng nhập, biểu tượng `aria-hidden`, huy hiệu dùng thuộc tính `hidden` thay cho `style="display:none"`) và huy hiệu ở menu bên (bỏ toàn bộ style nội dòng; kiểu nằm trong `notifications.css`). 5 dòng thêm / 5 dòng bỏ, không đổi gì khác.
  2. `Workers/NotificationWorker.cs` — **chỉ bốn chuỗi** (Title/Message của cảnh báo mất chuỗi ngày học và của "sắp nhận huy hiệu"): bỏ chữ "Streak" và emoji. Không đổi logic, batch hay outbox.
  3. `Models/NotificationType` — thêm thành viên `PremiumActivated = 8` (lưu dạng số nguyên, **không migration**; `has-pending-model-changes` rỗng).
  4. `NotificationController.Index` đọc cài đặt nhắc học qua `INotificationPreferenceService.ReadAsync(userId, OutboundDeliveryOptions.PushConfigured)` — **chỉ đọc**, hiển thị ở cột phải; lỗi đọc chỉ làm mất khung giá trị (còn nút "Đổi cài đặt" tới `/Account/Profile#section-notifications`).
  5. `Views/Shared/_NotificationDropdown.cshtml` nạp `notification-core.js` (một dòng) để có trước `notifications.js` ở cuối layout; `study-kit.css` chỉ thêm `.tn-page` vào các danh sách phạm vi.
- Không đổi: `Program.cs`, `appsettings*`, `.csproj`, migration, `LearningBusinessClock`, `Services/Notifications/**` (chỉ `NotificationDisplay`/`NotificationLinks`/`AnnouncementModels` được thêm), Goals/Streak/Account.

### NOTIFICATIONS-OLD-ROWS-KEEP-OLD-COPY — confirmed 17/11/2026

- Thông báo đã lưu trước đợt này giữ nguyên chữ cũ ("Streak 1 ngày — kỷ lục mới của bạn!", "Còn 1 bước nữa để mở khóa huy hiệu 'Khởi động'!" …). `NotificationDisplay.CleanTitle` chỉ bỏ emoji/ký hiệu **đầu tiêu đề**; thân tin nhắn cũ không bị sửa. Hết hạn tự nhiên sau 30 ngày (đã đọc) — không viết lại dữ liệu cũ (quyết định D3 = GIỮ).

### NOTIFICATIONS-ANNOUNCEMENT-NOT-SENT-LOCALLY — chưa kiểm chứng 17/11/2026

- CSDL local có tài khoản thật (user 4, email của Trọng) nên **không gửi thông báo admin** khi thử (quy tắc mục 7 của plan). Gửi theo lô, chống gửi đôi, loại tài khoản đã ẩn danh/khóa, liên kết cục bộ và giao dịch theo lô 500 được kiểm bằng test (SQLite một lần dùng + host kiểm thử); trang Lịch sử đã chạy trên MariaDB Docker với dòng thử của tài khoản mẫu (đã xóa theo khóa). **Chưa** chạy một lần gửi thật trên MariaDB: nếu muốn, làm trên bản sao có chỉ tài khoản mẫu.
- Số người nhận trong Lịch sử đếm các dòng còn lưu: sau 30 ngày bản đã đọc bị dọn nên con số giảm; nhóm cũ hơn 30 ngày được ghi "còn n bản lưu".

### NOTIFICATIONS-GOALS-PAGE-JQUERY — quan sát 17/11/2026

- Mở `/Goals` (từ liên kết "Xem huy hiệu" của thông báo) trong Chrome headless ghi hai lỗi console `ReferenceError: jQuery is not defined` (`jquery.validate.min.js`, `jquery.validate.unobtrusive.min.js`): `Views/Goals/Index.cshtml` include `_ValidationScriptsPartial` mà layout không nạp jQuery (cùng họ lỗi `SWEEP-ADMIN-LAYOUT-NO-JQUERY`). Trang Goals thuộc chủ khác nên **không sửa** ở đợt này; trang `/Notification` không có lỗi.

### NOTIFICATIONS-ADMIN-VISUAL-BATCH — đã xử lý 18/11/2026 (đợt admin-content-complete; mô tả dưới là tình trạng trước đó)

- Hai trang Admin thông báo (Gửi, Lịch sử) mới sửa logic và câu chữ (quyết định D2): nút và liên kết vẫn theo cỡ Bootstrap (nút "Gửi thông báo" cao 38 px, `btn-sm` nhỏ hơn 44 px), thẻ xem trước còn style nội dòng, layout Admin chưa nạp jQuery. Làm lại giao diện Clay cùng đợt Admin.

### ADMIN-CONTENT-SIDEBAR-LABELS-D1 — đề xuất cho Châu 18/11/2026

- Menu Admin (`Areas/Admin/Views/Shared/_AdminLayout.cshtml`, file của Châu) vẫn ghi "Vocabulary Sets" và "Listening Lessons" trong khi mọi trang bên trong đã là tiếng Việt ("Quản lý bộ từ vựng", "Quản lý bài nghe"). Quyết định D1 = KHÔNG nên layout chưa bị sửa.
- Đề xuất: đổi hai nhãn thành "Bộ từ vựng" và "Bài nghe" (hai dòng trong layout; cân nhắc cả "Speaking Videos", "Writing Exercises", "Reading Passages" để đồng nhất).

### ADMIN-CONTENT-SEEDER-NAME-RULE — quy tắc cần biết 18/11/2026

- `JsonVocabularySeeder` khớp bộ hệ thống theo (chủ sở hữu, tên, thư mục) và thẻ theo `Term` (không phân biệt hoa thường); khi bật đồng bộ lúc khởi động, thẻ không có trong tệp bị xóa. Vì vậy Admin **không đổi được tên** bộ hệ thống (bị chặn), đổi thư mục chỉ có cảnh báo, và sửa `Term` của thẻ có thể bị coi là thẻ mới ở lần đồng bộ sau.
- Để giữ thay đổi: bấm "Tải JSON" ở trang Thẻ của bộ và gộp vào `wwwroot/data/system-vocabulary.json` (cùng định dạng: mảng thư mục → bộ → thẻ). Chưa có công cụ tự động cập nhật tệp. Bộ tạo mới ở Admin luôn thuộc tài khoản `system@tct.local`, công khai.

### ADMIN-CONTENT-LEARNER-POLICY — chính sách 18/11/2026

- Nội dung của học viên (bộ thẻ cá nhân, bài nghe riêng của Premium): Admin chỉ xem, đặt riêng tư / chuyển về bản nháp và xóa, không sửa (D2). "Đặt riêng tư" tắt luôn liên kết chia sẻ vì liên kết cấp quyền bất kể `Visibility`; người đã đăng ký bộ thẻ hoặc xem qua thư mục/lớp vẫn thấy bộ đó.
- Bài nghe riêng không vào danh mục chung (`OwnerUserId == null && IsPublished`), nên cờ "xuất bản" của học viên không có tác dụng với catalog; Admin không thể xuất bản bài riêng.

### ADMIN-CONTENT-PROGRAM-DI-LINES — ghi chú bàn giao 18/11/2026

- `Program.cs` có thêm đúng hai dòng đăng ký `IAdminVocabularyService` và `IAdminListeningService` cạnh `IPersonalSetService` (quy tắc "không sửa Program.cs" của repo; có ủy quyền trong plan). Châu cần biết khi gộp nhánh.

### ADMIN-BILLING-SECONDARY-PAGES-BOOTSTRAP — chưa chuyển hết 18/11/2026

- Danh sách đơn thanh toán dùng bộ giao diện chung; năm trang phụ (Chi tiết đơn, Gói đăng ký, Người dùng Premium, Chi tiết Premium, Tình trạng cổng) giữ nguyên mọi biểu mẫu/hành động và markup Bootstrap, chỉ đổi khung, biểu tượng, chữ tiếng Việt và định dạng tiền, được tạo kiểu lại bằng CSS `.ac-billing`. Muốn đồng nhất hoàn toàn cần viết lại từng trang (khoảng 1.200 dòng biểu mẫu). Còn lại tiếng Anh có chủ ý: tên cấu hình kỹ thuật (BaseUrl, ReturnUrl, IPN, TmnCode) và dữ liệu thô của nhà cung cấp.

### ADMIN-CONTENT-TEST-HOST-MEMORY — vận hành 18/11/2026

- Chạy toàn bộ `dotnet test` một lần (~3.500 test) hai lần bị host dừng vì thiếu bộ nhớ trên máy 16 GB đang mở Antigravity và Docker. Chạy ổn theo hai nhóm với `-- xUnit.MaxParallelThreads=2` (nhóm lọc `Admin|PersonalSet|Vocabulary|Listening|Billing|Notification`, rồi phần còn lại) sau `dotnet build-server shutdown`.

### LANDING-SCREENSHOTS-STALE — maintenance note 16/11/2026

- Trang Landing dùng ảnh chụp thật của trang Từ vựng (`hero-home-*.webp`), của một câu hỏi trong bài học (`lesson-card-*.webp`) và ảnh chia sẻ `og-landing.png` (1200 × 630). Khi giao diện Từ vựng hoặc bài học đổi, các ảnh này sẽ cũ đi và cần chụp lại.
- Cách chụp lại: đăng nhập tài khoản mẫu Premium (có tiến độ), cửa sổ 1440 × 900, hệ số điểm ảnh 2, ẩn launcher AI trong trang rồi chụp vùng nội dung (không có thanh bên, thanh trên, tên hay avatar); xuất WebP bằng canvas (`toBlob("image/webp", .8)`), rộng 2220 và 1110 px (≤ 97 KB và ≤ 44 KB), thẻ bài học rộng 1632 và 816 px. Ảnh chia sẻ: chụp 1200 × 630 phần đầu trang đã hoàn chỉnh, nén lại không mất dữ liệu cho ≤ 300 KB. Chi tiết trong plan `2026-11-16-landing-night-and-bug-sweep` mục 12 (N4, N8).

### LANDING-DARK-THEME-D1 — quyết định chờ chủ dự án 16/11/2026

- Landing mới có nền tối ("Đêm học"), khác ngôn ngữ Clay sáng mà nhóm dùng cho các trang đã làm lại. Đây là quyết định D1 của Trọng (trang độc lập, `Layout = null`, stylesheet riêng, token `--lp-*` có phạm vi `.lp`, không đổi file dùng chung).
- Cần báo Châu (chủ hướng thiết kế) trước khi hợp vào `master`. Nếu Châu muốn tông sáng, chỉ phải đổi khối token `.lp` trong `landing.css` và hai ảnh chụp.

### LANDING-READING-LOGIN-RETURN — confirmed 16/11/2026

- Triệu chứng: khách bấm `/Home/Reading` bị chuyển tới `/Account/Login` mà không có `ReturnUrl` (các trang Từ vựng, Nói, Premium đều có), nên sau khi đăng nhập không quay lại bài đọc.
- Landing đã đi vòng bằng liên kết `/Account/Login?returnUrl=%2FHome%2FReading` cho khách; nguyên nhân thật nằm ở bộ điều khiển trang Đọc (không thuộc đợt này).

### SWEEP-FOOTER-SOCIAL-HASH-LINKS — confirmed 16/11/2026

- Triệu chứng: footer dùng chung (layout chính) có 3 icon Facebook/GitHub/YouTube với `href="#"`; mọi trang dùng layout đó (ví dụ `/Home/Listening`) có 3 liên kết chết, không có tên truy cập.
- Nguyên nhân: footer nằm trong layout/partial dùng chung, không phải file của một module. Landing mới không sao chép phần này.
- Chưa sửa vì file dùng chung nằm ngoài phạm vi đợt này; chủ layout (Châu) quyết định bỏ icon hoặc thêm URL thật.

### SWEEP-ADMIN-LAYOUT-NO-JQUERY — confirmed 16/11/2026

- Triệu chứng: `_AdminLayout.cshtml` không nạp jQuery, nhưng `SpeakingVideoManagement/{Create,Edit}` và `WritingExerciseManagement/{Create,Edit}` vẫn include `_ValidationScriptsPartial` (jquery.validate) → `ReferenceError: jQuery is not defined` ở console (suy ra từ mã nguồn, chưa mở các trang này trong trình duyệt vì thuộc người khác).
- Các view của Trọng (SetManagement, ListeningManagement, NotificationManagement) đã gỡ include này; test `scripts/tests/test_owner_admin_views_sweep.cjs` canh lại khi layout có jQuery.
- PR140 (18/11/2026): ReadingManagement Create/Edit đã gỡ partial chết, thêm
  native required và giữ validation server. HTTP regression và browser sau
  restart không còn lỗi jQuery trên hai form Reading; không suy ra rằng các
  form Speaking/Writing nêu trên đã được sửa.
- Hướng sửa: chủ các module Speaking/Writing gỡ include tương tự, hoặc chủ layout admin nạp jQuery (đổi hành vi: bật validation phía client).

### SWEEP-LISTENING-YOUTUBE-THUMB-404 — confirmed 16/11/2026

- 18/11/2026: trang Admin sửa bài nghe và danh sách đã có ảnh dự phòng khi ảnh bìa lỗi; ảnh 404 vẫn do dữ liệu (video bài thử không có thật).

- Triệu chứng: `/Home/Listening` có một ảnh bìa trả 404 (`img.youtube.com/vi/9F8o8eH_Hms/hqdefault.jpg`), khung ảnh vỡ.
- Nguyên nhân: dữ liệu bài nghe trỏ tới video YouTube không còn; lỗi dữ liệu, không phải mã.
- Hướng sửa: admin gỡ hoặc thay bài nghe đó; có thể thêm ảnh dự phòng khi ảnh lỗi (đổi giao diện, chưa làm).

### SWEEP-ADMIN-SET-SAMPLE-IMAGES-404 — confirmed 16/11/2026

- 18/11/2026: trang Thẻ của Admin không còn yêu cầu ảnh nội bộ không tồn tại (hiện emoji hoặc chữ cái đầu); dữ liệu mẫu trong `system-vocabulary.json` vẫn trỏ tới ảnh thiếu và cần chủ nội dung sửa.

- Triệu chứng: `/Admin/SetManagement/Cards/11` có 10 ảnh thẻ `/images/animals/*.jpg`, `/images/greetings/hello.jpg` trả 404.
- Nguyên nhân: dữ liệu mẫu tham chiếu ảnh không có trong `wwwroot/images`; lỗi dữ liệu mẫu cục bộ.

### SWEEP-FOLDER-NO-H1 — confirmed 16/11/2026

- Triệu chứng: `/Home/Folder` không có `h1` (chỉ có `h2` "Your folders" / "Saved folders"), tiêu đề tab "Folders" bằng tiếng Anh; các trang khác của Trọng đều có đúng một `h1`.
- Chưa sửa vì đổi cấp tiêu đề/ngôn ngữ ảnh hưởng CSS và là việc thiết kế lại trang Thư mục (D3: chỉ sửa lỗi nhỏ).

### LISTENING-IMPORT-WAITS-FOR-AI — confirmed 13/11/2026

- Việc thêm bài nghe từ YouTube (`CreateOwnedLessonAsync`) vẫn chờ AI sinh quiz xong
  rồi mới trả lời (tối đa khoảng 20 giây của nhà cung cấp AI, cộng thời gian lấy phụ
  đề). Giao diện nay hiển thị trạng thái chờ trung thực ("có thể mất đến một phút"),
  và chú thích sai "without blocking" trong code đã sửa, nhưng hành vi chưa đổi.
- Hướng sửa gọn (cần quyết định riêng): tạo bài trước, sinh quiz nền hoặc để người
  dùng bấm "Tạo quiz bằng AI". Không làm trong đợt Listening để giữ nguyên hợp đồng
  `My/Create`.

### LISTENING-LEGACY-TOKEN-COMMENTS — confirmed 13/11/2026

- `listening-index.css` và `listening-practice.css` đã bị xóa, nhưng
  `DESIGN.md` (dòng ~363), `wwwroot/css/tct-learning-ui.css` (đầu file) và
  `docs/design-system/team-ui-workflow.md` (dòng ~139) vẫn nhắc
  `listening-index.css` là nơi dùng các token legacy. Các file chung này ngoài
  phạm vi đợt Listening; chủ design system (Chau) cập nhật khi dọn token legacy.
  Chỉ còn `auth-stitch.css` dùng các tên đó.

### LISTENING-AI-LIVE-NOT-VERIFIED — confirmed 13/11/2026

- "Tạo quiz bằng AI" và "Dịch bằng AI" ở trang Luyện nghe chỉ được kiểm tra với
  câu trả lời server giả lập; chưa chạy với khóa AI thật. Cần một lần thử thủ công
  bằng khóa cục bộ của chủ dự án trước khi coi là đã xác nhận.

### STAGING-SOURCE-MEDIA-MISSING — confirmed05/11/2026

- Owner đã yêu cầu copy toàn bộ production DB sang staging. Đã hoàn tất9.209
  rows, giữ3test users/12migration, kiểm60bảng và copy27tệp có thật.94đường dẫn
  ảnh/tệp trong DB không có file tương ứng ở production (vocabulary/avatar/class/
  Reading/chat). Không có nguồn để phục hồi các file này trong snapshot.
- Cần file/backup media gốc hoặc thay thế nội dung được owner duyệt; không tạo
  ảnh giả hay gọi đó là lỗi copy staging. Chi tiết private và
  [audit](../../docs/feature-audits/2026-11-05-staging-full-data-copy-ledger.md).

### ADMIN-CLASS-MESSAGE-MENU-404 — confirmed05/11/2026

- Menu Admin có `/Admin/ClassManagement` và `/Admin/MessageManagement` nhưng
  checkout hiện không có controller tương ứng. Hai URL trả404 trong browser/API
  acceptance staging; User/Set/Writing/Premium pages trả200.
- Toàn bộ15classes/35classmessages đã copy và kiểm FK/checksum. Cần task giao
  diện/chức năng Admin riêng để quyết định triển khai hai màn hình hay chỉnh menu;
  không đổi quyền truy cập hoặc sửa feature trong task sao chép database.

### STAGING-HTTPS-RENEWAL — owner deferred05/11/2026

- Staging N518 đã chạy https://staging.tctenglish.site với DB/site/pool riêng;
  readiness200 sau Basic Auth, anonymous401, shared Writing/progress isolation
  và backup/restore/repeat schema đã kiểm. Không còn blocker “chưa có server”.
- Certificate hiện hợp lệ đến03/02/2027. Root Basic Auth từng chặn HTTP-01;
  issuance thành công khi app offline và tạm gỡ riêng bảo vệ staging, rồi đã
  khôi phục. Auto-renew qua auth chưa được nghiệm thu. Cần exception anonymous
  chỉ path ACME từ Nhân Hòa hoặc maintenance/reissue theo
  [runbook](../../docs/operations/staging-plesk-runbook.md). Không nới auth prod.
- Follow-up05/11: owner cấp key staging riêng; Chat200/STAGING_OK và Speech200/
  Completed/audio tổng hợp2.75s đã đạt sau khi khắc phục outbound IPv6 bằng env
  chỉ staging. Writing Local +cloud feedback; không copy key local. CD staging có
  source/job/15regressions, Environment chỉ master, webhook/manifest branch riêng;
  owner đã cho commit/PR/merge và bật riêng staging. Gate production vẫn false.
  Kích hoạt phải đối chiếu run/marker thật; xem [automation](../../docs/operations/staging-automation.md).
- Exception ACME cần Nhân Hòa xác nhận cấp server; [nội dung hỗ trợ](../../docs/operations/staging-https-support-request.md).
- **Owner quyết định sau đó05/11:** chứng chỉ hiện tại đến03/02/2027 đủ, không cần
  làm thêm tự gia hạn hoặc gửi ticket. Không mở lại phần này trong task hiện tại.
  CD staging/AI/data sharing vẫn hoàn thiện; production gatefalse.

### WRITING-DRAFT-OWNER: Writing draft keys are shared across accounts [P1] — fixed 04/11/2026
- **Was**: wwwroot/js/writing.js used wp_draft_ex{exercise}_{sentence} without owner.
- **Fix**: server-verified `CurrentOwnerUserId` → `data-owner-user-id`; namespace
  `wp_draft_u{owner}_ex{exercise}_{sentence}`; legacy unowned keys ignored; missing
  owner does not persist. Node harness `test_writing_draft_isolation.cjs` green.
- **Browser verified04–05/11**: Standard draft retained in localStorage; logout→
  Premium in the same context does not restore it. No owner data was cleared.
- **Stage02 follow-up14/11**: in-flight guard blocks repeated Ctrl+Enter; Accepted
  responses only clear a matching stored draft and preserve newer typing/selection.
  Node regressions and Chrome synthetic Razor capture verified same-user multi-tab
  preservation. Full live account switching was not repeated in this stage.

### WRITING-CONCURRENT-COMPLETION: Final submits deadlocked [P1] — fixed local14/11/2026
- **Was**: two MariaDB final submissions entered progress upsert concurrently and
  one failed with deadlock. Writing did not lock the user before attempt FK writes.
- **Fix**: user row FOR UPDATE at the start of the existing transaction; progress,
  credit and award intents remain atomic. MariaDB10.5.23 barrier, actual credit
  rejection/retry, outbox/cancellation and migration-repeat tests passed.
- **Boundary**: per-accepted-sentence confirmed practice days still belong to stage03;
  lifetime exercise credit/20XP stays once-only. See
  [stage02 audit](../../docs/feature-audits/2026-11-14-writing-goals/02-writing-backend.md).

### WRITING-REVIEW-STATE: NeedsReview can render as redo [P2] — fixed 04/11/2026
- **Was**: normalize dropped AssessmentStatus/ReviewRequired; label used “Làm lại”.
- **Fix**: snapshot/Razor/JS preserve status fields; label “Cần xem lại”; historical
  rows map via `WritingAssessmentPolicy.ResolveHistoricalStatus` from EvaluationSource.
- **Follow-up (same day UAT)**: `recentAttempts.statusLabel` also mapped NeedsReview
  → “Cần xem lại” via `ResolveHistoricalStatusLabel` (was binary “Cần sửa”).

### FINAL-POLISH-FINAL-CHECKS: C1.1 closed; R6 UAT advanced 04/11/2026

- **Owner closeout05/11**: rendered60role/viewport/routes; real Admin stale-form
  conflict/input preservation; Writing approved/local-exact/NeedsReview+reload;
  same-profile draft isolation; completion→Goals/badges/notifications +replay.
  Notifications CSRF400/non-owner404/read-all0; AI full/embed fake503; Speaking
  recorder/ASR fake, word chips literal-safe, NoSpeech no commit/mic-denied.
- **Fixed during review**: Speaking init independent of video API/late callback;
  mobile toast; NoSpeech persistent message; Goals overall percent/editor target
  for non-Vocabulary goals. New regression red→green. Full .NET929/0/18skip,
  Node58/0, Goals51/0. Browser Writing1/2→50%, replay unchanged.
- **Still separate acceptance**: paid API/real mic, broad all-feature owner UAT,
  hosting/staging provision and production. Web5100 restarted with Local Writing
  cloud enhancement disabled for fee-free UAT; saved key unchanged/configured.
  [Owner closeout](../../docs/feature-audits/2026-11-05-chau-final-polish-owner-closeout.md),
  [database collaboration](../../docs/operations/database-collaboration.md).

- **R1–R5 / RR2 / C1.1**: reviewer-verified; not re-implemented.
- **R6 advanced (local)**: synthetic Admin/Standard/Premium; Writing #23 Admin
  variant→Accepted + NeedsReview; Speaking demo **55+44** (53 not demo-approved);
  cold launcher→ready ~105s; 3-role matrix + A→B draft PASS. Live mic/paid pending.
  Not B0–B7 production closure.
- **UAT fix**: `recentAttempts` NeedsReview label was “Cần sửa”; now
  `ResolveHistoricalStatusLabel` → “Cần xem lại” (unit + live Practice/Data).
- Evidence: `.tmp/chau-r6-uat-ledger.md`, `.tmp/chau-final-polish-demo-manifest.md`,
  `.tmp/uat-startup-measure.md`,
  [final-checks plan](../../docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md),
  [code acceptance](../../docs/feature-audits/2026-11-04-chau-final-polish-code-acceptance.md),
  [active prompt](../prompts/chau-final-polish-schema-guard-uat.md).

Historical reviewer round2, before final-check fixes (not current status):

Reviewer lần hai04/11 confirms app R1–R5 via .NET913/0/14, Node54/0, guarded
MariaDB upgrade1pass +HTTP concurrency1pass and extra empty/existing/save-vs-edit
probes. No old P1 reproduced. **Remaining tests RR1/RR2 [P2]**:

- Upgrade baseline test still lacks fresh-history/schema guard, so rerun on an
  upgraded acceptance target can run Down/drop variants. Dry-run confirmed SQL;
  reviewer did not execute downgrade.
- Render-twice test edits fixture DOM itself instead of invoking production
  renderer. Actual renderer passed reviewer fake assess/commit flow, but tracked
  regression must run that behavior; strengthen concurrency barrier coverage too.
- R6 browser/demo/measurement remains pending. [Rereview](../../docs/feature-audits/2026-11-04-chau-final-polish-rereview.md),
  [focused plan](../../docs/superpowers/plans/2026-11-04-chau-final-polish-final-checks.md),
  [Cursor prompt](../prompts/chau-final-polish-final-checks.md).

### BUG-007: AutoUnlockWorker is disabled by default in the current config
- **Symptom**: Expired locks are not auto-cleared in the default app
  configuration; users rely on lazy unlock during login or manual admin unlock.
- **Root Cause**: `AutoUnlockWorker` exits early unless
  `BackgroundJobs:AutoUnlockWorkerEnabled` is set to `true`, and the shipped
  config does not currently enable it.
- **When it triggers**: Any environment that uses the current appsettings files
  without an override.
- **Recommended fix**: Add environment-specific configuration and validate
  `LockExpiry` data before enabling the worker broadly.
- **Status**: Confirmed unresolved / operational gap.
- **Local hardening03/11**: Conditional unlock is committed before email;
  fake SMTP timeout/cancel and worker retry/batch isolation are verified.
  Lazy-login uses the same conditional transition. The default-off rollout
  remains intentional until environment/data validation and owner release
  authority; this entry is not resolved by tests alone.

### BUG-008: Startup seeding always performs a full JSON sync — resolved 2026-10-30
- **Symptom**: Application startup still reads and synchronizes
  `wwwroot/data/system-vocabulary.json` on every boot, increasing startup cost
  and mutating the database during normal app launch.
- **Root Cause**: `Program.cs` always calls `JsonVocabularySeeder.SeedFromJsonAsync(...)`;
  the seeder is now upsert-based, but it still scans and saves each startup.
- **When it triggers**: Every application startup.
- **Recommended fix**: Gate seeding by environment/flag, or add a cheap
  change-detection guard before the expensive sync path.
- **Status**: Resolved by the explicit `Database:SeedOnStartup` gate (default false).
  Local restore keeps seeding off in User Secrets; release migration uses the
  design-time factory and does not start the web host or seeders.

---

## Technical Debt

### TD-001: HomeController split is complete, but the boundary must stay enforced [LOW]
- **Files**: `Controllers/HomeController.cs`,
  `Controllers/ClassController.cs`, `Controllers/FolderController.cs`,
  `Controllers/SetController.cs`, `Controllers/StudyController.cs`,
  `Controllers/ChatController.cs`
- **Issue**: The main split is done and `HomeController` is down to dashboard
  and public pages, but legacy `/Home/*` routes still make it easy to drift new
  feature work back into the wrong controller.
- **Impact**: Future maintenance will regress quickly if new domain logic is
  added back into `HomeController`.
- **Recommended fix**: Keep all new class/folder/set/study/chat behavior in the
  dedicated feature controllers and services only.
- **Effort**: Small.

### TD-002: Service extraction is still partial [MEDIUM]
- **Files**: `Controllers/AccountController.cs`,
  `Controllers/FolderController.cs`, `Controllers/SetController.cs`,
  `Controllers/SpeakingController.cs`,
  `Areas/Admin/Controllers/UserManagementController.cs`
- **Issue**: `IClassService`, `IStudyService`, `IStreakService`, and
  `IFileStorageService` are now in place, but several controllers still own
  validation, EF orchestration, and branching business rules directly.
- **Impact**: These areas are harder to unit-test and remain the next likely
  regression hotspots.
- **Recommended fix**: Extract the next bounded services around
  account/profile/security, folder/set mutations, and admin user-management
  workflows.
- **Effort**: Medium.

### TD-003: AccountController is still a large mixed-responsibility controller [MEDIUM]
- **Files**: `Controllers/AccountController.cs`,
  `Views/Account/Auth.cshtml`, `Views/Account/ForgotPasswordConfirmation.cshtml`
- **Update 2026-11-11**: Profile editing, security challenges, habit reads and
  account deletion now go through `TCTEnglish/Services/Accounts` and
  `Services/Learning`. The controller still owns login, OAuth and reset HTTP.
  Review repaired atomic proofs/security retries, cookie/hub invalidation,
  multi-tab/idle telemetry, habit/history boundaries, delivery lease/privacy and
  deletion safeguards. Local MariaDB/browser evidence and remaining live-channel,
  native zoom and dependency limits are in
  `docs/feature-audits/2026-11-11-account-functional-execution-ledger.md`.
- **Issue**: Auth, social login, profile, settings, password reset, and account
  deletion all live in one controller, with a mix of typed models and
  `ViewBag`-driven UI messages.
- **Impact**: High churn, weak compile-time safety for some auth screens, and
  harder regression testing.
- **Recommended fix**: Split profile/security/account-lifecycle concerns into
  smaller services or slices, and finish the move to typed view models.
- **Effort**: Medium.

### TD-004: Goals rollout contracts can drift if dashboard/challenge wiring and docs/tests diverge [LOW]
- **Files**: `Views/Home/Index.cshtml`, `Controllers/HomeController.cs`,
  `Services/GoalsService.cs`, `docs/architecture-prioritized-backlog*.md`,
  `.github/copilot-instructions.md`, `AGENTS.md`,
  `TCTEnglish.Tests/GoalsPhase*.cs`
- **Issue**: The Goals rollout is live across dashboard/challenge and broader
  goals flows, but product/docs/test contracts can drift if feature changes
  bypass `IGoalsService` or update active/deferred areas without synchronized
  documentation and phased regression coverage.
- **Impact**: Product inconsistency, missing activity/XP telemetry, and weaker
  guidance for future changes.
- **Recommended fix**: Keep dashboard values model-driven, ensure daily
  challenge writes activity through `IGoalsService.RecordLearningActivityAsync(...)`
  so counters/streak/XP share one transaction and business instant,
  and keep docs plus phased goals tests aligned with the live reward contract.
- **Effort**: Small ongoing.

### TD-005: Secrets remain in source-controlled appsettings [SECURITY]
- **Files**: `appsettings.json`
- **Issue**: OAuth credentials, SMTP credentials, and connection-string secrets
  are still committed in the default config file.
- **Impact**: Serious exposure risk if the repository is copied, published, or
  shared outside the intended environment.
- **Recommended fix**: Move secrets to User Secrets for local development and
  environment variables / secret store for deployed environments.
- **Effort**: Small, but coordinated.

### TD-006: Pagination is still missing in some user-facing queries [MEDIUM]
- **Files**: `Controllers/HomeController.cs`, `Services/ClassService.cs`
- **Issue**: Admin user management is paginated, but public search results and
  class discovery/search flows still materialize full result sets.
- **Impact**: Performance risk as the dataset grows.
- **Recommended fix**: Introduce paging parameters plus `.Skip()` / `.Take()`
  on search-heavy views and endpoints.
- **Effort**: Small per screen.

### TD-007: Database index review still has not been codified [MEDIUM]
- **Columns to review**: `LearningProgress.UserId`, `LearningProgress.CardId`,
  `ClassMember.ClassId`, `ClassMember.UserId`, `SpeakingVideo.Level`,
  `SpeakingVideo.Topic`
- **Issue**: The refactor improved query shape, but high-traffic lookup columns
  still need an explicit index review.
- **Impact**: Latency risk on larger datasets and production filtering.
- **Recommended fix**: Add a focused migration after validating the current SQL
  Server execution plans.
- **Effort**: Small.

### TD-008: Structured logging is only partially normalized [LOW]
- **Files**: Critical flows already use `ILogger<T>`, but not every controller
  and service emits equally rich structured events.
- **Issue**: Logging baseline now exists in key controllers/services, yet some
  secondary flows still have thinner diagnostics.
- **Impact**: Debugging production incidents outside the covered hotspots will
  still be slower than necessary.
- **Recommended fix**: Continue standardizing structured logs as the remaining
  controllers are service-extracted.
- **Effort**: Small ongoing.

### TD-009: Test-only SQLite native dependency advisory [SECURITY]

- **Confirmed 2026-11-11**: TCTEnglish.Tests resolves
  SQLitePCLRaw.lib.e_sqlite3 2.1.10 and the fresh audit reports High
  GHSA-2m69-gcr7-jv3q. The application project does not resolve this package and
  still uses MariaDB; this is a separate test-infrastructure dependency issue.
- **Status**: The authorized AngleSharp1.10.1/MySqlConnector2.6.2 app maintenance
  passed review. Its remaining weekly-email test failure and parser-coverage gap
  were repaired; those fixes do not resolve this SQLite advisory.
- **Follow-up**: Assess an EF9-compatible provider/native-bundle replacement with
  loaded SQLite>=3.50.2, then verify SQLite-specific transaction/schema/concurrency,
  full suite and test audit. The advisory lists no patched release of the same
  affected2.x native package; do not merely bump to2.1.11 or suppress the warning.
  Test-project dependency changes need their own bounded authority, as the original
  two-pin launch explicitly excluded that project file. Preserve model/history/data.
- **Evidence**: docs/feature-audits/2026-11-11-dependency-security-maintenance-ledger.md
  and docs/superpowers/plans/2026-11-11-dependency-security-maintenance.md.

### TD-010: Goals migration rollout checklist is not documented strongly enough [MEDIUM]
- **Files**: `Migrations/*Goals*`, `Models/DbflashcardContext.cs`, `Services/GoalsService.cs`
- **Issue**: Goals schema migrations are additive and safe by design, but there is
  no explicit deployment checklist for backup/apply/verify order on real SQL
  Server environments.
- **Impact**: Increased release risk when applying goals migrations at scale
  without a repeatable operator runbook.
- **Recommended fix**: Add and follow a rollout checklist: database backup,
  migration apply order, post-deploy query verification, and seed/idempotency
  validation for badge records.
- **Effort**: Small.

### TD-011: Practice Learn/Match/Test state is device-local [LOW]
- **Files**: `wwwroot/js/practice-core.js` (`createStore`), `wwwroot/js/practice-learn.js`, `practice-match.js`, `practice-test.js`, `practice-hub.js`
- **Issue**: Learn rounds, Match best times, the test draft and per-set stats (`tct:set-stats:{setId}` `learnPercent` / `testPercent` / `matchMs`) live in `localStorage` / `sessionStorage` on one browser. There is no migration or server table by design (plan P3).
- **Impact**: A new browser, a cleared profile or private mode starts Learn from the first round and shows no record or "Lần trước" figure; nothing is lost on the server because review progress is still written through `UpdateCardProgress` when "Tính vào ôn tập" is on. Learn/Match progress is not shared between devices.
- **Recommended fix**: Only if owners want cross-device records: add a small per-user table (migration with MariaDB verification) and keep the browser copy as a cache.
- **Effort**: Medium.

### TD-012: AI chat launcher can cover the edge of the practice start button [LOW]
- **Files**: `wwwroot/css/practice.css` (`.tp-side`, `.tp-start-bar`), `Views/Practice/Index.cshtml`
- **Issue**: At 1200px and wider the hub's setup panel is a sticky right column. On a viewport shorter than about 800px the fixed AI launcher (68px box, bottom-right) can sit over the last ~20px of the "Bắt đầu luyện" button. The label is not covered. The feedback sheet, the test submit bar and the phone layout keep their own clearance.
- **Impact**: Cosmetic; the button stays clickable except under the launcher circle.
- **Recommended fix**: Only with an owner decision about the launcher (shared shell file): give it a reserved gutter or let pages ask it to move. The study kit cannot do this safely alone.
- **Effort**: Low.


Sweep 16/11/2026 xác nhận thêm: "Bắt đầu luyện" (`/Practice`, 1440), "Bắt đầu chặng 1" (`/Practice/learn`, 390) và "Học bài mới" (`/Vocabulary`, 390, Premium) bị nút AI che một phần. Cùng cách xử lý: chừa khoảng trống dưới cùng cho launcher.
### TD-013: `POST /api/LearningApi/record` does not check that the user may view the card [MEDIUM]
- **Update 14/11/2026**: the system Vocabulary pages no longer use this endpoint; they save through `POST /Vocabulary/Review/Record`, which accepts system cards only (404 otherwise). The endpoint is still open to any card id for personal sets and other callers, so the fix below is still needed (Châu).
- **Files**: `Controllers/LearningApiController.cs` (`Record`, the `LearningProgresses` lookup by `cardId` and the current user)
- **Issue**: Any card id is accepted. A signed-in user can create a progress row, and earn Goals/XP credit, for a card they cannot open (for example a private set of another user). The Vocabulary lesson only ever sends ids of system cards it was given, so the new screens do not depend on this.
- **Impact**: Authenticated users could probe or farm credit through card ids. File belongs to another owner (Châu); it was not changed in the Vocabulary rebuild.
- **Recommended fix**: Before saving, require that the card's set is system-owned, public, owned by the user or shared with them (same rule as `PersonalSetService`); answer `NotFound()` otherwise. Pin it with a regression test.
- **Effort**: Low.

### TD-014: System Vocabulary content is small, its names promise more, and some pictures are not ours [MEDIUM]
- **Files**: `wwwroot/data/system-vocabulary.json`, `Models/JsonVocabularySeeder.cs`
- **Issue**: The file holds 3 folders, 8 sets and 92 cards. "1000 từ tiếng Anh thông dụng" has 20 cards and "3000 từ vựng Oxford A1" has 10. 62 cards hotlink pictures from a third-party shopping CDN (`down-vn.img.susercontent.com`), and some local seed paths (`/images/greetings/*.jpg`) do not exist on disk (the lesson shows a letter tile instead). The seeder matches sets by name and deletes cards missing from the JSON, so renaming a set would strand learner progress.
- **Impact**: Learners see real counts (the UI never repeats the set name's number), but the names overpromise; pictures can disappear or change without notice.
- **Recommended fix**: Owner decision: rename carefully (add new sets, keep old ones) or expand the content with reviewed, additive data and a validator; host or drop the pictures. Do not reuse a publisher's word-list name for new content.
- **Effort**: Medium (content work).

### TD-018: Folders: shared-file edits for the owner of the layout, and two leftovers [LOW]
- **Files**: `Views/Shared/_Layout.cshtml`, `wwwroot/js/layout.js`, `Views/Shared/_CreateFolderDialog.cshtml`, `Controllers/FolderController.cs`, `Views/Folder/*`
- **Issue**: The folders work (2026-11-16) edited two shared files that belong to Châu: in `_Layout.cshtml` the "Thư viện của bạn" group is active for the Set controller, the "Thư mục của tôi" entry is removed, the footer link points to `/Sets` and the create-folder modal is replaced by the `_CreateFolderDialog` partial (ids `newFolderBtn`, `folderModal`, `cancelFolderBtn` kept); in `layout.js` the create-folder block was rewritten (counter, blank guard, pending state, bfcache reset, reopen on error, focus return, `#tao-thu-muc` link). Leftovers: `FolderController.BuildFolderPageViewModelAsync` is now unused, and the legacy `Views/Folder/*` pages (reachable at `/Home/FolderDetail/{id}` for class links and old bookmarks) still use the old look.
- **Impact**: None for users. A reviewer of the layout should look at those two blocks; a later cleanup can delete the dead method and move the legacy folder detail onto `/Sets/Folders/{id}`.
- **Recommended fix**: Châu reviews the two shared-file blocks in the PR; remove the unused method and retire `Views/Folder/*` once class links point to the new folder page (`Views/Class/**` is another owner's).
- **Effort**: Small.

### TD-017: Practice has no folder scope, so the folder page has no "Luyện từ đến hạn" [LOW]
- **Files**: `Controllers/PracticeController*.cs`, `Views/Set/Folder.cshtml`
- **Issue**: `/Practice` takes one `set` or a list of words and cannot practise a whole folder, so the plan's optional "Luyện x từ đến hạn" button was left out of the folder page.
- **Impact**: A learner practises a folder set by set.
- **Recommended fix**: If wanted, add a folder scope to Practice (a source rule, not a view change) and then the button; needs the Practice contract to be extended with its tests.
- **Effort**: Small to medium.

### TD-016: Spaced repetition: level kept in `RepetitionCount`, fixed daily target, system cards only [LOW]
- **Files**: `Services/Vocabulary/SpacedRepetitionScheduler.cs`, `Services/StudyService.Vocabulary.cs`, `LearningProgress`
- **Issue**: The memory level (0-6) lives in the existing `RepetitionCount` column, legacy rows are read through a mapping (Mastered below level 4 reads as 4, Learning reads as 0) and are normalised only when recorded again. The daily target is the constant 10 words. Personal sets keep the old schedule (1 h to 7 d).
- **Impact**: A legacy row can show a different level than the learner expects until it is next reviewed; two ladders exist side by side. Suspended rows are no longer counted as due on the Vocabulary pages.
- **Recommended fix**: Only if wanted: a migration with an explicit level column, a settings page for the target, and the same ladder for personal sets (needs the owner of `LearningApiController`).
- **Effort**: Medium.

### TD-015: Vocabulary lessons are computed, so their membership shifts when cards change [LOW]
- **Files**: `Services/Vocabulary/VocabularyLessonBuilder.cs`
- **Issue**: Lessons are groups of about 10 cards by `Card.Topic` and `CardId`; nothing is stored. When an admin adds or deletes cards of a course, lesson numbers and the words inside a lesson can change.
- **Impact**: Progress is per card, so nothing is lost; a learner may see "Bài 4" hold different words than yesterday. The recommended next lesson always follows current data.
- **Recommended fix**: Only if it matters: store lesson numbers per card in a migration. Otherwise accept and avoid bulk re-topicking of live courses.
- **Effort**: Medium.

---

## Architecture Warnings

### NEVER do these in this codebase

1. Modify `Program.cs`, `appsettings.json`, or any `.csproj` without an explicit request.
2. Generate migrations with `DropTable` or `DropColumn` without approval and a rollback plan.
3. Reintroduce inline `ClaimTypes.NameIdentifier` parsing into controllers.
4. Use `.Result` or `.Wait()` on async paths.
5. Pass EF entities directly to Razor views.
6. Bypass ownership/membership checks on any parameterized read or mutation.
7. Replace typed view models with `ViewBag` on feature pages.

### Be careful with these areas

1. `AutoUnlockWorker` is config-gated; enabling it needs data/config validation.
2. `JsonVocabularySeeder` currently mutates data on startup; do not make it more expensive accidentally.
3. SignalR class access rules must stay aligned with HTTP class-detail rules.
4. File uploads should continue to go through `IFileStorageService` plus `ImageUploadPolicies`.
5. Legacy `/Home/*` routes are preserved intentionally; route changes can break bookmarks and tests.

---

## Recently Resolved / Verified

SETS-CARD-DETAILS-NONPREMIUM-NULL, resolved 16/11/2026: `refs.imageResults` in `set-card-form.js` `setData` is now guarded, so the "Chi tiết" panel opens for Standard accounts and the edit page logs no error. Regression test: `scripts/tests/test_set_card_form_nonpremium.cjs`.

Pre-merge security04/11: pending browser bundle upgraded DOMPurify3.2.6 to
3.4.17 with immutable upstream source/hash/license and portable vendor bytes.
Manifest/Razor-path regressions and Chromium production chat hydration pass.
Known affected dependency removed; no claim of comprehensive XSS assurance.
Current pre-merge evidence:
[full diff audit](../../docs/feature-audits/2026-11-04-full-diff-review-and-merge.md).

Local storage04/11: large shared caches and project temp moved to D with C→D
junctions and supported user environment preferences; C gained~11.8GiB free.
**Host recovery verified, updated later04/11:** owner re-entered a working key;
metadata200 for both models and one tiny text200/completed/OK were verified.
Ordinary launcher then attempted to replace User Secrets and failed EFS, leaving
the active file absent. Local fix now removes startup credential writes, uses
child env for DB/worker flags and guards explicit key replacement with backup.
Owner initially deferred, then re-entered the key and explicitly requested a
restart. The corrected launcher now runs on5100 with text/speech configured
from User Secrets AI:ApiKey, ready200. Actual secret-file length/mtime/protection
attributes remained unchanged through startup. No new paid probe was run after
this restart: configured is not current account/quota/ASR quality verification.
Global fallback presence alone is also not proof of a valid key. Do not reuse the protected D copy as
active store or weaken file protection. See
[storage audit](../../docs/feature-audits/2026-11-04-local-storage-on-d.md).
Follow-up: [local key startup audit](../../docs/feature-audits/2026-11-04-local-key-startup-safety.md).

UI/logic closeout03/11: local Plan1/2 implemented and verified, including
MariaDB quota/migration/concurrency, SQL Writing/Admin pagination, Speaking
dynamic-card safety/actions/counts, AI unknown-outcome handling, SMTP/worker
ordering, awarded-only Profile badges and lazy launcher usage.
Evidence and explicit external gates:
[closeout](../../docs/feature-audits/2026-11-03-plan1-plan2-local-closeout.md).
This does not resolve whole-controller extraction debt, production release,
real-microphone quality or clean-PC acceptance.

Team local bootstrap (01/11/2026): a fresh Docker DB now gets sample accounts and
learning fixtures through an explicit Development-only CLI; existing users/data
and credentials are preserved. No real backup/keys/volumes are shipped in Git.
Prerequisites/onboarding and cold MariaDB/login evidence:
[team rollout](../../docs/feature-audits/2026-11-01-team-local-rollout.md).

Plan2 (01/11/2026): runtime text provider OpenAI `gpt-6-luna`/low. **Superseded for
ASR (03/11/2026):** active Whisper retired; Speaking/import use OpenAI
`gpt-transcribe` with measured PCM windows (`openai-audio-vad`). Review-repair
R1–R7 (overlap normalize, shared live budget, credentials/IOException/admission/
status/Admin copy) closed offline + fake-provider browser; paid live still
owner opt-in. Reviewer04/11 additionally hardened verified WAV budget,
launcher missing-vs-empty key handling, status config/privacy and persisted
completion/replay assertions. Full897/0/12 and script/JS checks verified;
no new reviewer browser/mic/live run. See
[rereview](../../docs/feature-audits/2026-11-04-openai-speech-rereview.md) and
[speech replacement ledger](../../docs/feature-audits/2026-11-03-openai-speech-replacement-ledger.md).
Earlier OpenAI text migration evidence remains in
[migration ledger](../../docs/feature-audits/2026-11-01-openai-provider-migration-ledger.md).
Paid live speech / real-mic acceptance and production release remain owner gates.

Whisper retirement follow-up04/11: unused loopback test helper removed; legacy
prompts superseded; startup fixture traps retired engines/downloads/processes.
The old .appdata/local/speech directory is absent. Follow-up uninstall04/11:
43engine/model/archive files109780875bytes were deleted directly by the owner
after tool policy rejection, and the unused Whisper Docker image was removed.
No installation binaries/models/archives remain in retired-speech-2026-11-04.
Six audio/log evidence files remain unchanged; two stale state/lock metadata files
1426bytes remain after a separate policy rejection. They are never read/started.
See [uninstall audit](../../docs/feature-audits/2026-11-04-whisper-uninstall.md) and
[retirement audit](../../docs/feature-audits/2026-11-04-whisper-runtime-retirement.md).

Plan2 follow-up (01/11/2026): owner-approved OPENAI_API_KEY now works through the
local launcher when app env/User Secrets have no key. Key source is reported
without its value; the mapping is child-process-only and cleaned up on failure
or exit. Occupied ports fail before build so an older web is not reported as
using new API settings. All four AI clients follow the shared launcher rule.

| Item | Current state |
|------|---------------|
| Speaking detail include issue | Fixed in `SpeakingController.Practice` |
| SignalR method naming mismatch | Server/client names now match (`SendMessage`, `ReceiveMessage`, `JoinClass`) |
| Class detail private graph leak | Fixed through sanitized outsider view + service/controller split |
| Folder/set split-controller anti-IDOR gaps | Fixed and covered by `FolderSetIdorRegressionTests` |
| Dashboard SQLite random ordering failure | Fixed with provider-safe fallback logic |
| Duplicate `ViewModel/` vs `ViewModels/` folders | Normalized to `ViewModels/` only |
| BUG-006 Goals placeholder page | Resolved with real `GoalsController` + `IGoalsService` read/write flow and integration tests |
| Goals business-date badge/streak alignment | Stage03 (2026-11-14): injectable UTC+7 operation clock, shared streak expiry for Goals/Profile/dashboard, persisted Accepted partial/replay practice, unique daily bonus/lifetime completion and typed study/goal-day projections; see stage03 ledger |
| Goals phase-5 documentation closeout | Resolved by removing stale placeholder descriptions from backlog/instruction docs and aligning known-issues with real Goals state |
| Goals phase-7 closure and reward hardening | Resolved with atomic speaking/writing completion transitions, removal of request-time writing migration, new replay regression coverage, and an explicit rollout close decision |
| Daily challenge trusted client `correctCardId` | Resolved by server-signed challenge token validation in `HomeController.CheckAnswer` |
| CI/CD test suite 14 failures (billing, speaking, DI, writing, AI host) | Resolved across 5 root cause groups; full suite verified green at 449/449 (2026-04-30) |

Catalog correction13/11/2026: the exact90 selected videos belong to Speaking.
All90 wrong Listening rows are now retired locally via immutable version2
withdrawals; original6 public and7 private lessons remain. Public topic/filter
privacy fixes are retained. After owner authorization, five inaccessible videos
were replaced with verified same-level sources; original withdrawal keys remain
unchanged. All90 current videos have real captions and complete measured audio,
and all90 are now available in local Speaking for owner UAT, with95 public
videos including the5 originals. This follows the owner's later clarification
that local testing precedes staging/team review. Full content QA remains pending.
CTC pilots produce219 A1 and108 A2 English cues without overlaps; low acoustic
confidence, non-speech classification and required review evidence remain
unresolved. The catalog now defaults to subject groups and retains CEFR filters.
A real transcript-click initialization bug was fixed: selecting another cue now
updates both reference text and highlight. No paid ASR budget/call is approved.
Existing caption `>>` speaker markers are now removed in practice presentation,
including the selected reference/dictation text; stored source cues, timing and
learner history stay intact. Old video44 is browser-verified with121 clean cues.
Private Speaking YouTube import follow-up (13/11/2026): the player/captions API
can report VideoUnavailable for a video whose public transcript is available.
The runtime caption service now falls back to the public transcript panel with
source timing validation and no ASR. The owner's gOMypAhVaXE imports successfully
as a private video with27 cues. Failed imports show an accessible error popup;
reload resumes the saved job without a duplicate POST. Public panel structure is
an upstream dependency; unavailable captions still fail explicitly with SRT/VTT
guidance. See the private-speaking-youtube-import-fix execution plan.
Speaking result restoration follow-up (13/11/2026): shared practice code now
restores numeric/word feedback and metric availability across sentence navigation
and reload. Batched receipt lookup is bound to current user and retained progress;
zero scores stay assessed. The duplicate center reference is removed and the
side transcript has one accessible hide/show control at every width. Regression
coverage includes public/private videos, multiple sentences and separate users.
Historical progress without a source receipt retains its numbers only.
Speaking catalog reconciliation (13/11/2026): Gemini's uncommitted controls were
already in the current Speaking checkout, with incomplete carousel/page CSS and
an extra closing div breaking theme containment. Shared layout is corrected;
the owner's one-line topic carousel/icons, no-reload filtering/history and8-card
slides are retained. Async public-section loading accounts for pages absent from
the current DOM; private cards and practice fixes are preserved. Chrome MCP and
144 shared fixture checks pass. The final state stays on the current local
Speaking branch; no unrelated branches or production were merged.
Existing Guest Listening practice404 and native200% zoom remain unverified/
unchanged. Current evidence and exact blockers:
[Speaking transfer ledger](../../docs/feature-audits/2026-11-13-speaking-transfer-transcript-ledger.md).

Speaking component scores follow-up (13/11/2026): both placeholder metrics are now
implemented for the shared practice flow. Completeness estimates attempted reference
positions; fluency explicitly estimates timing/pauses/pace from PCM using the existing
energy planner. Three nullable receipt columns preserve availability and method
version. Historical transcripts can recover completeness; historical fluency needs
a new recording. Best-take metrics/comparison and measured zero restore consistently.
The ring and bars share a row at1440/768/390 without overflow. Core .NET37, Node35
and disposable MariaDB10.5.23 upgrade/repeat/preservation1 pass; broader focused
.NET46 pass/1 existing concurrency acceptance skipped. Local migration is applied,
backup is ignored, web/readiness are available on5100. Fluency remains a coaching
estimate, with no phoneme/prosody claim; paid real-microphone and native200% zoom
acceptance were not run. See speaking-component-scores execution plan.


Speaking recent videos completion (14/11/2026): the partial Gemini scaffold is
now implemented locally. Personal recent history is server-side, access-filtered,
limited to six distinct cards and independent of scores/catalog filters. A
concurrent first-insert timestamp race is covered by a failing-then-passing
regression and monotonic update. Account anonymization explicitly clears visits.
Local MariaDB migration preserves all old row data; provider/concurrency and
browser acceptance are recorded in
[recent-videos ledger](../../docs/feature-audits/2026-11-13-speaking-recent-videos-ledger.md).
Native200% zoom remains unverified; no known recent-history defect remains.


PR136 CI portability (14/11/2026): catalog regression no longer reads ignored
local transcript drafts. Public acquisition summaries now provide cue digests,
counts and measured-duration bounds on clean runners. Raw media/private receipts
stay ignored; content acoustic QA remains pending. See PR136 release ledger.

PR136 staging import (14/11/2026): release43538144629 timed out while its hosting
import remained active under maintenance. Adoption cost is bounded by the focused
follow-up; full disposable MariaDB catalog/repeat and guarded recovery tests pass.
Staging is not recovered until the new expected marker and readiness are observed.
See docs/superpowers/plans/2026-11-14-staging-shared-data-performance.md and PR136
release ledger; production stays disabled.


Writing/Goals stage04 completion (14/11/2026):10 new server-earned awards and safe
mystery projection are implemented locally; all old12 IDs/codes and earned history
remain. Unknown/invalid-rule grants and reproduced cross-user deadlocks are fixed.
Broad application361 pass/24 gated skip; disposable MariaDB10.5.23 final29/29;
Node25/25; build/pending-model clean. Local catch-up adds7 silent retroactive
awards after a63-user dry-run; repeat0, old42 grants/XP/streak/history preserved.
Stage05 now expands the local public Writing catalog from10 to118 lessons
(108 original new lessons, exactly6 in each of18 level/format cells); the13/25/50
milestones have enough public content by count. Advanced36 completes108/108.
Final112 focused tests pass; advanced fresh MariaDB10.5.23 roundtrip/repeat,
canonical379-release Validate/Sync twice and all-three zero-create reruns pass.
All42,409 original rows and44,209 pre-advanced rows/72 tables,343 previous package
versions and production.json are preserved. Same-agent editorial evidence covers
all108 lessons across the three authoring batches; no independent teacher review.
No fake credits or threshold reduction. See05-content-matrix.md,
05-beginner-ledger.md,05-intermediate-ledger.md and05-advanced-ledger.md. Stage06
still owns final tier/filter/earned-secret visual binding and native zoom/live
login acceptance. See docs/feature-audits/2026-11-14-writing-goals/04-badges.md and
04-badges-contract.md. No CI/staging/production or paid-call claim.

Writing/Goals stage07 integration (14/11/2026): supersedes the stage06-pending
note above. Header totals, filter keyboard/stale state, Goals label associations,
paused-target copy and narrow legend/history defects are fixed and verified.
Final full1,575 pass/50 gated skip, focused98 and Node46 pass; MariaDB29 plus
combined108-package repeat/preservation1 pass. Real-cookie account isolation,
earned mystery/Profile and24 viewport cases are evidenced in ledger07. Native
browser200% zoom remains a manual check; live paid providers remain out of scope.
Initial three full-suite failures did not reproduce; their original logs remain.

Writing recent exercises completion (15/11/2026): personal visit history now
includes initialized practice pages without submissions, across catalog levels
and formats, without learning credit. Separate additive schema, current access
checks, account cleanup locking and monotonic concurrency are implemented locally.
Final full1,584 pass/51 gated skip, Node42 pass and disposable MariaDB provider
checks are recorded in
[Writing recent ledger](../../docs/feature-audits/2026-11-15-writing-recent-exercises-ledger.md).
Native200% zoom and a genuinely restored bfcache/native focus account-switch
session remain manual observations; controlled lifecycle/owner-mismatch tests
pass. Native Back in the inspected Chrome uses a fresh document because of
no-store/websocket restrictions. Instant-close tracking is best-effort; unsent
drafts remain browser-local. Initial isolated/full classifier flake did not
reproduce in the final full suite. No shared content or release authority changed.
Writing brief backend step01 (15/11/2026): strict composition from a description,
durable private receipt/recovery, shared UTC five-success quota and21 limited
bilingual template profiles are implemented locally. Matched free descriptions
require explicit acceptance of the frame and omitted details; no arbitrary offline
translation/topic coverage is promised. Refusal/cancel/entitlement/application
quota never bypass through a template. Fake-provider/MariaDB concurrency and
restart evidence do not certify live model quality. Step02 UI binding and browser
acceptance remain outstanding; migration was verified on disposable MariaDB, not
applied to the existing dev web. See
[step01 ledger](../../docs/feature-audits/2026-11-15-writing-ai-brief-hybrid/01-backend.md)
and its backend contract. No paid call or remote release was performed.

Writing step02 review (15/11/2026) supersedes the UI-pending note above: receipt
storage/recovery, stale callbacks/account activation, false cancel success,
shared quota display, bilingual previews/slot labels and390px overflow are fixed.
Full1,630 pass/53 skip and Node72 pass; real browser/MariaDB flows are documented
in [final review](../../docs/feature-audits/2026-11-15-writing-ai-brief-hybrid/03-review-and-local-handoff.md).
Native200% zoom remains unverified. One accidental live generation after browser
reload used741 known tokens (cost unknown); remaining checks used a process-level
loopback boundary. The review must not be reported as having zero paid calls.
Final local web uses port5100 and saved configuration; no remote release.

System Vocabulary rebuild (14/11/2026): `/Vocabulary/*` is now three screens (home with
"Học tiếp", course page with lessons and a word list, three-step lesson) on the shared
Clay kit. Automatic grading replaces the four self-rating buttons; each finished word is
saved once through the unchanged `LearningApi/record`. Old views, `vocabulary-system.css`
and the old service methods were removed. Open items for owners: TD-013 (record access
check, Châu), TD-014 (content size, names, pictures), TD-015 (computed lessons). Safari
and Firefox, and a real speech voice, were not checked. See the Vocabulary rebuild plan
section 12 for evidence.

System Vocabulary spaced repetition and polish (14/11/2026): every system word has a memory
level 0-6 (10 min, 1, 3, 7, 14, 30, 60 days) computed on the server from the lesson answers
(`POST /Vocabulary/Review/Record`); the home shows the daily ring, the due words ("Ôn
ngay"), "Trí nhớ của bạn" and "Lịch ôn 7 ngày tới"; course covers have illustrations and
words have emoji pictures. See TD-016 and the SRS polish plan section 12.
