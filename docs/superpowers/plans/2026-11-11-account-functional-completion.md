# Account: hoàn thiện hồ sơ, cài đặt, thói quen học và vòng đời tài khoản

Ngày: 11/11/2026, Asia/Bangkok. Trạng thái: kế hoạch dựa trên source, **chưa triển khai**.

## 1. Kết quả cần đạt và phân công thực thi

Người dùng có thể đổi tên/ảnh/email/mật khẩu an toàn; nhìn thấy mã tài khoản ổn
định và thống kê có căn cứ; tự cấu hình nhắc học Web Push và email tổng kết tuần;
xóa tài khoản của mình với chính sách dữ liệu rõ ràng. Không thay các con số mẫu
bằng một bộ con số mẫu khác.

Chủ dự án đã chọn **đầy đủ Web Push + email tuần**. Cursor với **Grok 4.7 xhigh**
sẽ triển khai theo prompt đi kèm. Một agent thực hiện, tự review và kiểm chứng.
Sau khi chủ dự án báo Cursor hoàn tất, Codex được yêu cầu review diff thực tế và
sửa tồn đọng trong cùng phạm vi, không chỉ viết thêm báo cáo.

Lượt soạn tài liệu là `PLAN_ONLY`: không sửa code ứng dụng, schema, dữ liệu hay
secrets; không gửi thư/push. Prompt thực thi chuyển sang `IMPLEMENT_LOCAL` khi
chủ dự án chủ động gửi đoạn chat khởi chạy. Không có quyền commit/push/PR/CI
remote/merge/staging/production trong kế hoạch này.

## 2. Baseline và các phát hiện đã xác minh

Checkout lúc khảo sát: `fix/staging-uat-polish`, HEAD `c908f66`. Đã có thay đổi UI
của chủ dự án/công cụ khác: Account Profile/Settings, account CSS/JS, hai layout,
loader, layout CSS, gallery, bug log; có thêm page-loader CSS và test dropdown.
Các tài liệu BTH3 và checklist UAT cũng chưa được commit. **Giữ nguyên toàn bộ**;
Cursor phải chụp baseline mới, không coi mọi khác biệt với HEAD là phần mình viết.
Lượt kiểm tra tài liệu thấy `git diff --check` còn báo blank line cuối bug log và
trailing whitespace trong Profile.cshtml thuộc diff đang có; chưa sửa các file
đó. Hai tài liệu bàn giao mới đã được kiểm tra riêng về whitespace, Markdown
fences, control characters, placeholder và sự tồn tại của đường dẫn source.

Đã đọc `.ai/prompts/README.md`, template execution prompt, DESIGN.md và team UI
workflow. Dùng CodeGraph cho source; chỉ đọc trực tiếp các phần Razor/top-level
Program mà graph không cung cấp ngoài header. Context7 không được expose trong
phiên khảo sát; đối chiếu tài liệu chính thức ở mục 11 khi cần API bên ngoài.

| Phát hiện từ source hiện tại | Ý nghĩa và việc phải làm |
| --- | --- |
| `AccountController` đã có Profile/UpdateProfile, Settings/UpdateEmail/ChangePassword/DeleteAccount | Hoàn thiện và hồi quy các luồng hiện có, không tạo một bộ endpoint cạnh tranh. |
| `UpdateProfileViewModel` gộp cả field đọc Email/Role/thống kê với field nhập; UpdateProfile redirect khi ModelState sai | Tái hiện submit thực tế và kiểm tra implicit validation của field không post; tách DTO nhập, giữ lỗi/giá trị nhập khi validation thất bại. Chưa tuyên bố đã tái hiện bằng browser. |
| `AvatarUploadService` xóa ảnh cũ ngay sau lưu file mới, trước khi controller lưu DB | DB save thất bại có thể để bản ghi trỏ vào ảnh cũ đã xóa; cần quy trình stage/commit/cleanup và hồi quy lỗi. |
| Profile dùng `FullName.GetHashCode()` và fallback `8892` để hiện mã | Hash tên không phải định danh ổn định; phải bỏ cả hash và fallback. |
| Profile hiện cố định 20 từ/ngày, progress 65%, 20:00–21:00, 85%, “Phát âm & Từ vựng”, “2.5x” | Nguồn dữ liệu và ý nghĩa từng field chưa tương ứng với giao diện. Thay bằng VM có dữ liệu thật/unknown, bỏ claim không có căn cứ. |
| Profile khẳng định bảo mật “Tốt”, email chính chủ, HTTPS và “an toàn 100%” | Không suy ra xác minh email, khả năng đăng nhập hay HTTPS từ một nhãn tĩnh. Render trạng thái có bằng chứng, bỏ bảo đảm tuyệt đối. |
| `UserDailyActivity` có số đếm 5 kỹ năng; `UserGoal` chỉ có target hiện tại và UpdatedAt | Có thể tái sử dụng số đếm học thật; chưa đủ lịch sử target để tính tỷ lệ đạt chỉ tiêu trong quá khứ. |
| `UserFeatureTimeLog` chỉ có ngày/kỹ năng/số giây; `time-tracker.js` đếm thời gian ở trang | Không suy ra giờ thường học từ ngày; chưa phân biệt idle, nhiều tab, heartbeat trùng. `TimeTracker.start()` còn gán lại biến feature khai báo const. |
| TrackFeatureTime dùng IgnoreAntiforgeryToken; JS đưa token vào query string | Phải sửa hợp đồng telemetry có CSRF đúng cách; không phát tán token qua URL/log. Không chỉ bật lại filter làm beacon hỏng. |
| Tracker đang gắn ở ListeningPractice, QuizMode, ReadingStudy, Study, Study/Speaking, WriteMode | Thiếu Speaking/Practice và WritingPractice chính; phải kiểm tra coverage. WriteMode của bộ thẻ không được lẫn với Writing exercise trong thống kê mới. |
| `NotificationController`, `NotificationService`, `NotificationWorker` và event-key/outbox đã tồn tại | Trung tâm thông báo trong web không phải tính năng trắng. Giữ contract count/list/mark-read, tích hợp preference/delivery mới. |
| Hai màn Account có fieldset Push/email disabled, giờ nhắc cố định | Chưa có luồng cấu hình hoạt động cho các tùy chọn trong ảnh. |
| UpdateEmail đổi địa chỉ ngay; ChangePassword có nhánh social marker bỏ kiểm tra hash | Thêm chứng minh quyền sở hữu/reauth có hạn; marker SOCIAL_LOGIN không phải bằng chứng provider hay quyền đổi mật khẩu. |
| DeleteAccount không nhận confirm/reauth từ server; CONFIRM hiện chỉ khóa nút ở JS | Gõ chữ ở client không bảo vệ POST trực tiếp. Phải kiểm tra ở server và kiểm chứng toàn bộ đồ thị dữ liệu. |
| Cookie config hiện thấy thời hạn 30 ngày, chưa thấy handler kiểm tra security version | Kiểm tra toàn bộ auth pipeline ở B0; bổ sung cơ chế vô hiệu hóa phiên sau thay đổi nhạy cảm/xóa tài khoản, gồm hub khi cần. |

Vị trí quan trọng: `TCTEnglish/Controllers/AccountController.cs`,
`ViewModels/{UpdateProfileViewModel,SecuritySettingsViewModel}.cs`,
`Views/Account/{Profile,Settings}.cshtml`, `wwwroot/{css/account-ui.css,js/account-ui.js}`;
`Services/{AvatarUploadService,LocalFileStorageService,ImageUploadPolicies}.cs`;
`Controllers/LearningApiController.cs`, `wwwroot/js/time-tracker.js`,
`Models/{User,UserGoal,UserDailyActivity,UserFeatureTimeLog,LearningCompletionCredit}.cs`;
`Services/Notifications/LearningBusinessClock.cs`, `Services/BusinessDateHelper.cs`;
`Controllers/NotificationController.cs`, `Services/NotificationService.cs`,
`Workers/NotificationWorker.cs`, `Models/DbflashcardContext.cs`, `Program.cs`.
Đường dẫn mới bên dưới là đề xuất, phải ghi tên thực tế trong execution ledger.

## 3. Quyết định sản phẩm và công thức

### 3.1. Mã tài khoản và dữ liệu tổng quan

- Mã hiển thị được tạo **ở server** từ khóa UserId bất biến: tiền tố `TCT-` cộng
  UserId định dạng thập phân tối thiểu 8 chữ số, culture invariant. Ví dụ dữ liệu
  thử: UserId 42 → `TCT-00000042`. Số lớn hơn 8 chữ số không bị cắt.
- Đây là mã tài khoản công khai để đối chiếu, không phải mã thiết bị, token hay
  bí mật bảo mật. Không dùng nó làm bằng chứng quyền truy cập. Không đổi PK,
  không tạo thêm bảng/cột hay migration chỉ cho mã hiển thị này.
- Đổi tên/email/ảnh, restart và chuyển máy không đổi mã khi UserId giữ nguyên.
- Goal hôm nay lấy từ UserGoals + UserDailyActivities theo business date; 0 là
  tạm dừng. Role/gói/expiry lấy service quyền/gói đang có, không suy expiry từ role.
- CreatedAt không biết thì hiện chưa có dữ liệu; email chưa có proof thì chưa xác
  minh. Dùng Request.IsHttps để nói về request hiện tại, không hứa hệ thống luôn HTTPS.
- Avatar hiện có policy JPG/JPEG/PNG, tối đa 2 MiB và kiểm tra signature; giữ giới
  hạn này, đồng bộ hướng dẫn UI. Không cần thêm thư viện ảnh/CDN chỉ để preview.

### 3.2. Thói quen học: dữ liệu và nhãn trung thực

Tái sử dụng số đếm hoàn thành đã được server xác nhận. Telemetry về tương tác
chỉ là **ước tính thời gian hoạt động**, không phải bằng chứng phát âm tốt, đã
hoàn thành bài hay xứng đáng nhận XP. Đặc biệt Tự luyện không được tính thành
Speaking completion tự động.

| Chỉ số | Quy tắc chốt cho bản đầu |
| --- | --- |
| Khoảng xem | 7 hoặc 28 ngày, theo business timezone hiện tại của dự án; hiển thị ngày bắt đầu/kết thúc và thời điểm cập nhật. Không âm thầm thay timezone của Goals. |
| Thời gian luyện ước tính | Tổng khoảng tương tác hợp lệ đã được server chấp nhận, bỏ hidden/idle và hợp nhất khoảng trùng. Chỉ gồm dữ liệu telemetry mới có coverage; không trộn số giây legacy như thể cùng chất lượng. |
| Ngày có luyện tập | Ngày có ít nhất 60 giây hoạt động hợp lệ hoặc có một hoạt động học được server xác nhận. Hiện số ngày và mẫu số được quan sát; không tự tô lịch trước khi có dữ liệu. |
| Khung giờ thường học | Bucket theo giờ của thời gian hoạt động hợp lệ, quy đổi từ UTC. Hiện khi có tối thiểu 3 ngày khác nhau và tổng ít nhất 10 phút; chọn bucket nhiều giây nhất, tie-break cố định theo giờ tăng dần. Đổi nhãn “hiệu quả” thành “thường học”; chưa kết luận giờ đó cải thiện trí nhớ. |
| Kỹ năng luyện nhiều nhất | Xếp theo thời gian hoạt động hợp lệ ở 5 kỹ năng; hiện tỷ trọng và đồng hạng nếu bằng nhau. Chưa có thời gian thì hiện chưa đủ dữ liệu, không mặc định phát âm/từ vựng. |
| Tỷ lệ mục tiêu hoàn thành | 100 × số goal-day đạt target / số goal-day hợp lệ, làm tròn số nguyên để hiển thị. Một goal-day là một kỹ năng với target > 0, active tại cuối ngày đó. Chỉ tính ngày đã kết thúc có lịch sử target đầy đủ trong khoảng xem; không tính hôm nay đang dở. |
| Ví dụ tỷ lệ | 17 goal-day đạt trong 20 goal-day hợp lệ → 85%. Hiện cả 17/20 và chú thích “hôm nay chưa tính”, không gắn thêm từ “đều đặn” như một kết luận khác. |
| Streak/huy hiệu/XP | Dùng service và completion ledger hiện tại, không tự cộng hoặc suy từ heartbeat. Huy hiệu Profile vẫn chỉ đọc huy hiệu đã trao. |

Lịch sử target: tạo một baseline từ **thời điểm bắt đầu theo dõi**, sau đó ghi
phiên bản khi đổi mục tiêu trong cùng transaction. Có thể suy target cuối ngày
từ các phiên bản này, kể cả ngày user không mở web. Ngày trước baseline là unknown;
không áp target hôm nay ngược cho lịch sử. Nếu thay đổi nhiều lần trong ngày,
lấy phiên bản cuối ngày theo quy tắc đã nêu. Baseline giữa ngày bắt đầu có đủ
coverage cho tỷ lệ từ ngày trọn vẹn tiếp theo. Không tạo hàng giả để lấp quá khứ.

Gợi ý hành động dùng quy tắc giải thích được: chưa có dữ liệu → bắt đầu một bài;
ít ngày học → chọn lịch nhắc; có mục tiêu chưa đạt → dẫn tới kỹ năng tương ứng.
Bỏ nhãn “AI Spaced Repetition” trên thẻ thống kê nếu không có thuật toán đó tại
đây, bỏ “tăng 2.5x”; không gọi LLM để tính các thống kê này.

### 3.3. Theo dõi hoạt động có giới hạn rõ ràng

- Tracker mới có session ID do server cấp, sequence/cumulative checkpoint và
  heartbeat khoảng 30 giây. Chỉ chạy trên trang luyện thật; không chạy trên
  Profile, Settings, dashboard, catalog hoặc khi chỉ mở folder.
- Dừng khi hidden, khi không có tương tác 90 giây, khi pagehide/logout; tự khôi
  phục đúng khi quay lại. Playback người dùng chủ động ở trang luyện có thể là
  tương tác; không tính autoplay bị bỏ quên. Không thu nội dung gõ, audio hay transcript.
- Server giới hạn thời gian nhận theo elapsed thực, thời lượng hữu hạn và lease;
  rejects payload âm/quá lớn, feature/session giả, session của người khác. Retry
  cùng sequence không cộng thêm. Chứng minh nhiều tab không nhân đôi wall time
  bằng cơ chế server atomic lease hoặc hợp nhất khoảng, không chỉ BroadcastChannel.
- Feature/mode lấy mapping trang đã xác minh: Flashcard/Quiz/WriteMode là Vocabulary;
  video Speaking và bộ thẻ Speaking là Speaking; WritingPractice mới là bài Writing.
  Ghi riêng mode nếu cần, giữ tương thích các thống kê legacy của dashboard.
- CSRF: fetch có token header đúng cấu hình; nếu giữ beacon thì form body có token
  để framework validate, hoặc dùng fetch keepalive. Không token query string,
  không IgnoreAntiforgeryToken, không cho client tự gửi UserId.
- Dữ liệu cũ chỉ có LoggedDate vẫn đọc được. Có thể giữ field UTC/session nullable
  hoặc thêm bảng telemetry phiên bản mới; không tự điền giờ cho dữ liệu cũ. Không
  double-write làm dashboard cộng cả v1 và v2 cho cùng khoảng hoạt động.

### 3.4. Email, mật khẩu và reauthentication

- Name: trim, required, giới hạn theo cột DB; không nhận Role/Email/XP từ DTO sửa tên.
  Validation trả lại đúng tab, lỗi theo field, dữ liệu vừa nhập và toàn bộ VM đọc.
- Avatar: lưu file mới trước; chỉ xóa ảnh cũ sau DB commit. DB fail dọn file mới,
  giữ ảnh cũ; race hai upload không xóa ảnh thắng cuộc. Delete file phải kiểm tra
  đường dẫn tuyệt đối trong thư mục cho phép; không xóa URL ngoài/local path tùy ý.
- Đổi email: local account cần current password hợp lệ hoặc proof reauth mới có
  TTL ngắn. Gửi xác nhận tới địa chỉ mới; email đăng nhập cũ còn hiệu lực cho tới
  lúc confirm thành công. Token ngẫu nhiên đủ mạnh, DB chỉ lưu digest, bound với
  user/new email/purpose/security version, TTL 30 phút, single-use và rate limit.
- GET từ email chỉ render trang xác nhận; mutation ở POST có CSRF và kiểm tra user.
  Normalize theo contract Login hiện có; xử lý email trùng và race bằng unique
  index hiện tại, không để 500. SMTP chưa cấu hình thì báo chưa thực hiện được,
  không đổi email lặng lẽ. Không yêu cầu chủ dự án paste key/mật khẩu vào chat.
- Đổi mật khẩu: verify current password với hash hợp lệ; khớp confirm; không trùng
  mật khẩu cũ; giữ policy hiện hành trừ khi có lý do và kiểm tra các luồng liên quan.
  Kiểm tra giới hạn hash/độ dài đầu vào, không im lặng truncate mật khẩu dài.
- Social-only: SOCIAL_LOGIN marker không đủ để bypass current-password check.
  Muốn đặt mật khẩu đầu tiên hoặc làm thao tác nhạy cảm phải có proof mới qua
  email hiện tại hoặc reauth provider đã được chứng minh, không bịa trạng thái linked.
- Security version: đổi password/email, reset password, block hoặc delete làm
  phiên cũ mất hiệu lực theo policy thống nhất. Current session chỉ được giữ nếu
  được reissue đúng claim mới. Cookie thiếu version sau rollout phải có policy
  rõ; ưu tiên yêu cầu đăng nhập lại thay vì coi là hợp lệ vô hạn. Kiểm tra cả
  phiên hub/stream đã kết nối để user bị xóa/khóa không tiếp tục thao tác.
- Clear reset/reauth/email-change challenge cũ khi thay đổi security state; CSRF,
  ownership và rate limit cho mọi mutation. Không đổi kiến trúc OAuth toàn ứng dụng.

### 3.5. Tùy chọn, Web Push và email tuần

- Preferences lưu server theo user: daily reminder on/off, giờ nhắc, timezone của
  lịch nhắc, push opt-in, weekly-email opt-in. Mặc định **không opt-in gửi ra ngoài**.
  20:00 là giá trị gợi ý trước khi lưu, không phải lịch đã hoạt động của mọi người.
- Giữ timezone thống kê học hiện có; nếu lịch nhắc dùng timezone riêng, UI nêu rõ
  chúng khác nhau. Validate timezone; tính lịch qua TimeProvider/clock có thể fake.
- Profile tab Cài đặt và `/Account/Settings` dùng cùng dịch vụ/partial/DTO phù hợp,
  không hai bản preference lệch nhau. Reload/tab switch vẫn đúng trạng thái đã lưu.
- Phân biệt desired preference với trạng thái khả dụng: disabled / permission
  needed / denied / unsupported / needs configuration / subscribed / sending /
  sent / failed / unknown. Không đánh dấu đã bật chỉ vì checkbox được tick.
- Web Push thật gồm service worker nhận push, subscription mỗi thiết bị, server
  transport VAPID, click notification mở route same-origin an toàn. Chỉ xin quyền
  khi người dùng chủ động bấm; tắt/denied/unsupported không làm hỏng các phần khác.
- Subscription endpoint/keys là dữ liệu riêng: giới hạn kích thước, bảo vệ log,
  HTTPS/provider allowlist phù hợp, chống SSRF/private addresses và redirect;
  không cho POST biến app thành HTTP client gửi tới URL bất kỳ. Validate DNS ở
  lớp kết nối nếu cần. Kiểm tra binding/rebind thiết bị khi đổi tài khoản, để user
  sau không nhận push/nội dung của user trước. Xóa/revoke đúng owner/device.
- Chọn thư viện transport có sẵn giao thức chuẩn, không tự viết Web Push crypto.
  Đề xuất pin `Lib.Net.Http.WebPush` **3.3.1**, kiểm tra restore/net9/advisories trước
  khi thêm. Không đổi pin nền tảng hay thêm frontend package. Nếu package không
  phù hợp, báo đúng blocker/dependency thay vì âm thầm đổi thư viện.
- Worker lập lịch đọc preference mới nhất trước khi gửi, bỏ user blocked/deleted,
  bỏ khi người dùng tắt hoặc đã hoàn thành mục tiêu nhắc. Nhiều instance cùng
  chạy phải dùng unique key + lease fencing/checkpoint và bounded batches.
- Daily reminder: tối đa một reminder logic/user/business date; mỗi subscription
  hợp lệ tối đa một delivery cho reminder đó. Catch-up sau restart chỉ trong
  60 phút sau giờ nhắc, không dồn lịch nhiều ngày; hết cửa sổ đánh dấu expired.
- Weekly email: thứ Hai 08:00 theo timezone đã chọn, tổng kết tuần trước từ thứ
  Hai đến hết Chủ nhật theo business timezone của thống kê học; giờ gửi dùng
  timezone lịch nhắc, không đổi biên dữ liệu thống kê. Chỉ gửi tới địa chỉ đã
  được xác minh và có opt-in. Tuần
  không hoạt động có thể gửi bản tổng kết 0 trung thực khi đã opt-in. Catch-up
  tối đa 24 giờ; không gửi bù nhiều tuần. Có đường dẫn quản lý/tắt đăng ký rõ ràng.
- Outbox/delivery ledger có unique identity theo user/channel/kỳ/subscription,
  retry hữu hạn với backoff, ghi attempt/outcome và xử lý 404/410 revoke subscription.
  SMTP không cung cấp exactly-once end-to-end: timeout sau dispatch phải giữ
  outcome unknown và chính sách không tự retry mù, không hứa “không bao giờ trùng”.
- Tách email/push gửi ra ngoài khỏi thông báo trong app. Tắt email không được xóa
  inbox; giữ các endpoint count/list/mark-read và notification EventKey hiện có.
  Không sửa diện rộng module Notifications của đồng đội ngoài phần tích hợp cần thiết.
- Typed options có default an toàn; private VAPID/SMTP lấy qua store hỗ trợ/env.
  Không tự sinh/ghi/ghi đè User Secrets trong task implement nếu chưa được cấp
  riêng thao tác cấu hình. Nếu thiếu, hoàn thiện code + fake transport acceptance,
  báo provider-live chưa kiểm chứng và chỉ rõ metadata cấu hình cần có.
- Host phải còn chạy mới lập lịch được. IIS sleep/server stop/network/trình duyệt
  hạn chế có thể trì hoãn push; không cam kết giờ chính xác hay giao tới máy đã tắt.
  Không đổi Plesk/Always On/gates trong task này; ghi dependency vận hành khi bàn giao.

### 3.6. Xóa tài khoản: an toàn, có thể kiểm chứng

- POST nhận confirm text có tên và proof reauth; server kiểm tra cả hai, không dựa
  vào disabled button. Đúng user hiện tại, chống CSRF, rate limit và double-submit.
- Lập ma trận **mọi FK tới User và các nội dung sở hữu** trước khi sửa xóa. Bao gồm
  class/members/messages, set/card/folder/saved references, Speaking/Writing/Listening,
  progress/attempts/assessments/import jobs, AI/conversations/quotas/logs, Goals,
  badges/completion/outbox, telemetry/history, email challenges, prefs/subscriptions,
  payment/subscription/admin audit và shared-content bindings. Không chỉ nav collection.
- Chính sách mặc định: xóa dữ liệu cá nhân/private; thu hồi đăng nhập/challenges/
  subscription, hủy queued deliveries/jobs. Nếu bắt buộc giữ tham chiếu chung,
  chỉ giữ tombstone kỹ thuật đã ẩn danh không còn khả năng đăng nhập/khôi phục;
  không chấp nhận chỉ đặt IsDeleted rồi giữ nguyên email, tên, hash, avatar và dữ liệu học.
- Với shared content/class có người dùng khác đang phụ thuộc: không cascade xóa
  tiến độ/nội dung của họ. Nếu chưa có flow chuyển chủ an toàn thì chặn xóa với
  lý do/hướng xử lý rõ; không tự tạo hệ thống chuyển chủ mới hay nhận owner bất kỳ.
- Không xóa ledger thanh toán để làm FK pass; bảo toàn tham chiếu với tombstone
  tối thiểu khi cần. Cấm xóa admin cuối cùng, kể cả hai yêu cầu concurrent.
- DB transaction trước, cleanup file local sau commit bằng job idempotent có thể
  retry; file/SMTP không nằm trong transaction DB. DB rollback giữ account dùng
  được và không xóa file trước. Không xóa file dùng chung hay remote avatar.
- Chặn phiên cũ ở HTTP và hub, xóa cache user nhạy cảm. Hủy/định danh lại draft
  local theo account; cookie/browser còn tồn tại không phục hồi account đã xóa.
- Mọi test xóa chỉ dùng user/file synthetic ở DB cô lập. UI mô tả chính xác dữ
  liệu bị xóa và trường hợp không thể xóa; không hứa xóa mọi dấu vết trong backup.

## 4. Phạm vi file và quyền thực thi đề xuất

Được sửa có mục đích: Account controller/DTO/read VM; services trong
`Services/Accounts/`, `Services/Learning/`, `Services/Notifications/`; model/mapping/
migrations liên quan; Account views/CSS/JS; tracker và các trang luyện cần gắn
tracker; notification worker/transport và service worker mới; các test tương ứng.
Tên service mới là đề xuất, không tuyên bố đã tồn tại.

Các thay đổi đăng nhập/reset/hub chỉ cho security-state consistency cần thiết;
không redesign auth, không sửa payment business logic, không redesign trang đồng
đội. Shared layout hiện đã dirty: ưu tiên không sửa thêm; nếu cần một hook token/
tracker/subscription thì liệt kê consumers và kiểm tra before/after theo DESIGN.md.

**Quyền protected-file chỉ có hiệu lực nếu chủ dự án gửi launch block:**

1. `Program.cs`: DI cho service/transport, typed options, cookie events/security
   validation, worker registration và middleware cần thiết trong phạm vi trên.
2. `TCTEnglish/TCTEnglish.csproj`: chỉ thêm package pin
   `Lib.Net.Http.WebPush` version `3.3.1` sau kiểm tra dependency, nếu chưa có.
3. Model/DbContext/migrations additive và local synthetic acceptance theo mục 5.

Không cho sửa appsettings, framework/provider pins, hosting/secrets/deploy gates;
không tự mở rộng quyền dựa vào văn bản plan. Nếu launch block không được gửi,
Cursor phải xác nhận quyền từ chỉ dẫn owner thực tế trước protected-file edits.

## 5. Schema và bảo toàn dữ liệu

Public account code chỉ là projection, không migration. Các thay đổi còn lại có
khả năng cần schema thật: security version/verified-email/deletion state; challenge
digest/expiry/purpose; telemetry session/checkpoint/time buckets; goal target
history; preferences; push subscriptions; delivery ledger/lease/cleanup jobs.
Thiết kế nhỏ nhất đáp ứng acceptance, tái sử dụng bảng/hạ tầng phù hợp đã có.

- Trước mỗi migration: impact matrix bảng/cột/index/FK, dữ liệu cũ, tương thích,
  Up/Down và cách backfill. Nullable/default an toàn; baseline history có cutoff
  rõ, không sửa số đếm học hay xác minh legacy email bằng phỏng đoán.
- Cùng change set phải có migration + Designer + snapshot. Không sửa migration
  đã áp dụng, không tạo lại lịch sử, không DROP TABLE/COLUMN hay reset DB.
- DB hỗ trợ MariaDB **10.5.23**, EF tool repo **9.0.4**, Pomelo **9.0.0**, net9.0.
  Test upgrade + chạy lại + preservation trên DB disposable cùng provider.
- Unique keys chống replay: account preference user, challenge identity/digest,
  session sequence, history version, push endpoint binding và delivery period key.
  Index phải hỗ trợ query user/date/feature và due-work scans. Kiểm tra indexlength,
  collation, FK support index order, DateTime kinds và concurrency tại MariaDB.
- Không dùng DB owner để thử rollback/destructive delete. Các bảng cá nhân mới
  không thuộc shared-data export; không thêm chúng vào allowlist/package. Không
  chỉnh học liệu shared trong task này để tránh tạo gói giả; nếu chủ đích chỉnh
  content được hỗ trợ thì workflow export/review vẫn bắt buộc.

## 6. Các batch thực thi, dependencies và checkpoint

Mỗi batch ghi concise quyết định, file đổi, test thật và blocker vào
`docs/feature-audits/2026-11-11-account-functional-execution-ledger.md`.
Không hỏi xác nhận giữa các batch đã được owner cấp quyền; blocker một channel
không ngăn hoàn thiện các phần độc lập.

| Batch | Công việc theo thứ tự | Acceptance trước khi chuyển batch |
| --- | --- | --- |
| B0 | Chụp Git/diff baseline; xác minh source/UI mới nhất, service registrations, FK inventory; đọc bug memory đúng triệu chứng; lập contract/schema impact và test baseline tập trung | Phân biệt existing dirty work với thay đổi mới; endpoint/test/browser/provider khả dụng được ghi, không đoán role/email state. |
| B1 | Tách input/read VM, sửa đổi tên/avatar và metadata/public code; refresh claims đúng; hydrate lỗi đầy đủ | Name/avatar persist sau refresh; DB fail giữ ảnh cũ; ID ổn định/unique; không còn chỉ số/security placeholder sai. |
| B2 | Security proof/challenge, email verification/change, password/social-first-password, security version/cookie/hub; schema đi kèm | Không bypass bằng marker/cookie cũ; single-use/race/expiry/unique email; lỗi giữ đúng tab; fake SMTP không báo đã xác minh. |
| B3 | Telemetry v2, CSRF transport, canonical practice coverage, target history baseline/version; migration/provider tests | Idle/hidden/retry/multi-tab/foreign-session không cộng sai; target history không bị rewrite; legacy vẫn đọc được. |
| B4 | Service/read VM habit aggregates và cards/charts có empty/partial/unknown states; rules gợi ý | Fixture 17/20 = 85%; giờ/skill tính đúng; hôm nay/history unknown xử lý theo mục 3; session hoạt động không cấp XP. |
| B5 | Preferences API/form và Account surfaces dùng chung; tích hợp inbox hiện có | Lưu/reload đồng nhất; owner isolation/CSRF; không fake opt-in/delivery; count/read/list không hồi quy. |
| B6 | Web Push + service worker + typed config + weekly email + delivery/worker ledger; disabled/failure/unknown/revoke paths | Fake transports chứng minh schedule/lease/restart/dedup/opt-out; live chỉ test khi có quyền và dữ liệu thử; log không lộ endpoint/key. |
| B7 | Account-deletion service/confirmation/reauth, data policy, rollback/cleanup/last-admin/shared-owner guards | Xóa synthetic account đầy đủ, user khác nguyên vẹn; stale HTTP/hub bị chặn; failure không để mất dữ liệu một phần. |
| B8 | Review diff inline, regression/build, MariaDB/schema checks, browser desktop→tablet→mobile; cập nhật bug log/known issue đúng | Mọi yêu cầu có evidence hoặc blocker cụ thể; chưa có provider/browser evidence không được ghi PASS. Bàn giao local còn chạy code mới nếu chủ dự án đang test. |

## 7. Ma trận kiểm thử bắt buộc

| Nhóm | Ca hành vi cần chứng minh |
| --- | --- |
| Profile | Tên trống/dài/Unicode; submit chỉ tên không field đọc; tamper Role/Email; ảnh đúng/sai MIME/signature/>2MiB; save DB fail; hai upload concurrent; external/path traversal cleanup; refresh claims trên header và Account. |
| Mã/metadata | Hai user cùng tên khác mã; đổi tên/restart không đổi mã; ID > 8 digits; unknown CreatedAt/email verification/social/password state; HTTP local không được hiện HTTPS verified. |
| Email/password | Wrong current password; email trùng/case/whitespace/race; pending email chưa có hiệu lực; token wrong-purpose/expired/replay/foreign user; SMTP fail; malformed hash; social marker không bypass; session khác và pending reset mất hiệu lực đúng. |
| Tracking | Hidden/idle/open catalog không tăng; resume/pagehide/offline/retry; negative/oversized/session giả; CSRF thiếu/sai; dedup và nhiều tab; UTC 17:00 ↔ ngày mới UTC+7; legacy row không tự có hour; canonical Speaking/Writing có coverage. |
| Habits | New user/0/unknown/partial coverage; 7/28 ngày; ties; đổi target/tạm dừng target/hôm nay chưa hết; inactive days vẫn có mẫu số khi history hợp lệ; fixture 17/20; không cộng XP/completion từ timer. |
| Prefs/inbox | Hai tài khoản, đổi tab/reload; timezone/giờ sai; tắt kênh không xóa inbox; existing count/list/paging/mark-read/mark-all và non-owner 404/CSRF400 vẫn đúng. |
| Delivery | Permission default/granted/denied/unsupported; VAPID/SMTP missing; subscription/revoke/rebind/switch account; SSRF URL/redirect/private network; schedule/daily/weekly catch-up; two workers/lease expiry; 404/410, 429/backoff, timeout unknown và opt-out/delete ngay trước dispatch. |
| Delete | No confirm/no proof/foreign ID; double POST/concurrency; mọi bảng FK mới/cũ; private vs shared dependencies; last-admin race; DB rollback giữ ảnh; file cleanup retry; account không login/HTTP/hub lại được, không gửi reminder tiếp. |
| UI | Profile tabs và Settings trực tiếp; pending/error/success không mất dữ liệu; keyboard/Tab/Escape/focus; 1440/768/390 và 200% zoom; long text/ID; không phá Clay layout vừa chỉnh; không fake trạng thái bảo mật/phần trăm. |

Tận dụng `AccountCorrectnessIntegrationTests`, `AccountUiRenderTests`,
`ProfileBadgeReadTests`, `NotificationReliabilityIntegrationTests`,
`NotificationWorkerRecoveryTests`, `NotificationWorkerBatchTests`,
`MariaDbNotificationWorkerAcceptanceTests` và các Node test account/notification.
Thêm test mới cho hành vi còn thiếu; nếu sửa bug, ghi failing regression trước
implementation khi dùng unit/integration test. Không chỉ regex source hay assert
test giả lập riêng mà không chạy production JS/service thực.

Các lệnh nền đã xác minh đường dẫn/tool, không phải kết quả chạy ở lượt planning:

```powershell
dotnet tool restore
dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext --configuration Release
node --test scripts/tests/*.cjs
git diff --check
```

Test Node trên Windows phải kiểm tra glob expansion; nếu runner không expand,
enumerate file bằng PowerShell rồi truyền array, không bỏ sót test. MariaDB tests
hiện dùng `TCT_ACCEPTANCE_MARIADB` và `TCT_ACCEPTANCE_RUN_ID`: lấy cách cô lập DB
từ fixture/script hiện hành, không in connection string và không set vào DB owner.
Test skipped vì thiếu env không phải provider PASS. Khi thêm Web Push dependency,
chạy thêm `dotnet list TCTEnglish/TCTEnglish.csproj package --vulnerable --include-transitive`.

Nếu cần local web: dùng `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`;
preserve listener, chỉ restart launcher có ownership rõ hoặc chọn port khác với
Release. Xác nhận `/health/ready` và `scripts/speech-status.ps1`. Không gọi AI/speech
trả phí hay gửi mail/push người thật để lấy bằng chứng. Provider acceptance fake
và live phải ghi thành hai nhóm riêng.

## 8. Deliverables và tiêu chí bàn giao

- Code/DTO/service/UI trong phạm vi, migration đầy đủ, regression tests có ý nghĩa.
- Execution ledger: baseline, decisions, schema/data/FK policy, batch status,
  exact commands/results, browser evidence và provider/config blockers.
- Bổ sung bug log cho bug đã sửa; update known issue nếu đúng mục đang mở. Không
  chép lại bug log lịch sử hay ghi kết quả chưa chạy.
- Hướng dẫn vận hành ngắn: field config metadata, local schedule behavior,
  verification/opt-in/unsubscribe và cleanup; không chứa key hay người nhận thật.
- Vietnamese handoff phân biệt implementation, local test, provider-live chưa
  kiểm chứng, CI/staging/production chưa thực hiện. Không commit/push tự động.
- Bộ tài liệu/source/test đủ để Codex tiếp tục review và fix khi chủ dự án báo xong;
  Codex phải kiểm tra diff mới, không coi lời báo PASS của Cursor là bằng chứng cuối.

## 9. Checklist theo dõi

- [x] B0 baseline, contracts, dirty-work protection, FK/schema assessment.
- [x] B1 profile/avatar/public code và metadata thật.
- [x] B2 email/password/reauth/session invalidation.
- [x] B3 telemetry/history + MariaDB proof. Review đã kiểm tra upgrade/repeat/bảo toàn dữ liệu trên MariaDB10.5.23 và các ca concurrency thực.
- [x] B4 habits với công thức và empty/unknown states.
- [x] B5 preferences/inbox integration.
- [x] B6 triển khai Web Push/weekly email + offline transport/schedule/recovery proof. Live VAPID/SMTP chưa nghiệm thu; xem ledger để phân biệt cấu hình và gửi thật.
- [x] B7 deletion/privacy/rollback/last-admin/shared-data safeguards. Đã kiểm tra native MariaDB và xóa synthetic qua trình duyệt local; không xóa tài khoản chủ dự án.
- [ ] B8 còn kiểm tra thủ công native browser200% zoom và gửi Push/email thật khi có cấu hình. Build/regressions/schema/MariaDB/browser1440/768/390 đã có evidence trong execution ledger; không coi phần chưa quan sát là PASS.

## 10. Prompt và quyền kích hoạt

Prompt: [Cursor Grok 4.7 xhigh — Account functional completion](../../../.ai/prompts/cursor-grok47-account-functional-completion.md).
Đoạn chat ở cuối prompt do chủ dự án chủ động gửi mới cấp quyền protected-file
được liệt kê. Nó không cấp quyền secret operation, Git/release hay xóa dữ liệu thật.

## 11. Tài liệu ngoài đã đối chiếu ngày 11/11/2026

- [.NET 9 String.GetHashCode](https://learn.microsoft.com/en-us/dotnet/api/system.string.gethashcode?view=net-9.0): hash không phải mã định danh cần lưu ổn định.
- [ASP.NET Core 9 cookie validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-9.0): điểm tích hợp ValidatePrincipal để đánh giá phiên dựa trên trạng thái server.
- [MDN Push API](https://developer.mozilla.org/en-US/docs/Web/API/Push_API): service worker/subscription và xử lý push ngoài vòng đời trang.
- [MDN Notifications permissions](https://developer.mozilla.org/en-US/docs/Web/API/Notifications_API/Using_the_Notifications_API): xin quyền theo tương tác người dùng, xử lý denied và secure context.
- [Lib.Net.Http.WebPush 3.3.1](https://www.nuget.org/packages/Lib.Net.Http.WebPush/3.3.1), [source chính chủ](https://github.com/tpeczek/Lib.Net.Http.WebPush): pin đề xuất và khả năng target .NET; execution vẫn phải kiểm tra advisory/restore, đây không phải chứng nhận đã test package trong ứng dụng.
