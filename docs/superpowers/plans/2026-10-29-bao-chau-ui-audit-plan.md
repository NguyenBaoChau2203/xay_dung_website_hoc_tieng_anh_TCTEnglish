# Kế hoạch rà soát và đề xuất nâng cấp UI thuộc phần Nguyễn Bảo Châu

**Ngày:** 29/10/2026. **Baseline khi soạn:** commit `3d27a68` trên `master`.
**Người duyệt:** Nguyễn Bảo Châu, nhóm trưởng.
**Công cụ thực hiện:** Antigravity; người dùng tự chọn **Gemini 3.8 Flash High**.
**Trạng thái:** `ANALYSIS_ONLY — CHƯA ĐƯỢC PHÉP IMPLEMENT`.

Chủ dự án chỉ giao model này nhiệm vụ phân tích và đề xuất giao diện. Khả năng
hay tên model không tạo ngoại lệ cho phạm vi. Plan/prompt không đổi model setting
và không xác nhận model có sẵn trên mọi tài khoản Antigravity.

## 1. Mục tiêu và kết quả cần bàn giao

Rà soát giao diện hiện có trong phạm vi bên dưới, nhận diện vấn đề có bằng chứng,
đề xuất hướng nâng cấp phù hợp TCT English và chia thành các lô nhỏ để chủ dự án
duyệt. Giữ toàn bộ nghiệp vụ, luồng người dùng, dữ liệu và hợp đồng kỹ thuật.

Kết quả của lượt Antigravity là **báo cáo phân tích + đề xuất**, không phải code,
CSS, mockup HTML đã chạy hay một phiên sửa giao diện. Chỉ một agent đọc, phân tích
và tự review. Không spawn worker/reviewer, gọi AI CLI khác hoặc tạo chat phụ.

Prompt điều hành: [antigravity-bao-chau-ui-analysis.md](../../../.ai/prompts/antigravity-bao-chau-ui-analysis.md).
Tất cả đường dẫn nguồn trong plan này tính từ **repo root**; dấu `/` dùng được để
tra cứu trên Windows. Không suy luận đường dẫn từ cấu trúc `TCTVocabulary` cũ.

## 2. Ranh giới ghi file của giai đoạn phân tích

| Loại | Quyền trong lượt này |
| --- | --- |
| Source giao diện, CSS, JS, C#, cấu hình, package, migration, seed, test | Chỉ đọc. Không sửa, format, xóa, di chuyển, tạo file nguồn hoặc cài dependency. |
| Plan này, prompt, DESIGN.md, rule, skill, hook, MCP | Chỉ đọc. Không sửa các rào chắn để mở rộng nhiệm vụ. |
| Báo cáo phân tích | Được tạo/cập nhật duy nhất `docs/ui-audits/2026-10-29-bao-chau-ui-analysis.md`, ghi rõ trạng thái chờ duyệt. Nếu file có sẵn, đọc và giữ kết quả trước, không đè phần của người khác. |
| Bằng chứng browser | Được lưu screenshot/snapshot đã loại dữ liệu nhạy cảm ở `.tmp/ui-audit/bao-chau-2026-10-29/` hoặc artifact của Antigravity. Không tạo bản sao HTML/JS/CSS sửa thử. |
| Git | Đọc status/log/diff. Không checkout/merge/rebase/reset/stash/stage/commit/push, tạo PR hay deploy. Giữ nguyên công việc có sẵn. |

Runtime/cache thông thường do công cụ đọc tạo ra không phải giấy phép sửa source.
Nếu browser không có hoặc không truy cập được, tiếp tục phần source và đánh dấu
thiếu bằng chứng; không cài MCP, đổi browser setting, tự bật debugging/trust.

## 3. Thứ tự đọc và công cụ

1. `AGENTS.md`, `.ai/README.md`, `DESIGN.md`.
2. `.agents/skills/tct-superpowers/SKILL.md`,
   `.agents/skills/tct-ui-review/SKILL.md`,
   `.agents/skills/tct-ui-component/SKILL.md`,
   `.agents/skills/tct-ui-ux-pro-max/SKILL.md`.
   Chỉ dùng phần phân tích/thiết kế của skill, không bắt đầu các bước implementation.
3. `docs/project-structure.md`, `.ai/context/coding-conventions.md`,
   `.ai/context/known-issues.md`; phần phân công Nguyễn Bảo Châu trong `README.md`.
   Có thể dùng `.ai/roles/ui-reviewer.md` như checklist inline.
4. Với code đã index, CodeGraph trước, theo từng file/nhóm nhỏ. Không init/index/
   sync. Nếu source bị cảnh báo stale hoặc CodeGraph chỉ trả về directive của
   Razor, nêu giới hạn rồi đọc có giới hạn phần markup/CSS/JS hiện tại.
5. Chỉ tra thêm `.ai/vendor/impeccable/{audit,craft-floor,adapt,harden,typeset,polish}.md`
   khi cần một tiêu chí cụ thể. Không đọc tất cả vendor/catalog vào context.
   Có thể chạy lookup UI/UX Pro Max kiểu chỉ đọc theo skill; không dùng tùy chọn
   persist, không tạo design system mới hoặc ghi đè DESIGN.md.

Stack thực tế: Razor MVC, Bootstrap 5.3, CSS, JavaScript/jQuery; project target
net9.0/EF Core 9.0.4. Không chuyển sang React/Tailwind/shadcn hoặc thêm CDN/font/
thư viện icon/motion. Plan cũ chỉ để hiểu lịch sử; không thực thi task cũ hay dùng
quyền sửa source đã cấp cho một việc khác.

## 4. Danh sách 25 view cần có dòng coverage

Đây là phạm vi phụ trách đã đối chiếu README/lịch sử Git, không tuyên bố sở hữu
độc quyền từng dòng. Một số view là cách hiển thị khác của cùng tính năng.
Mỗi ID phải có kết quả đọc source, tình trạng browser, phát hiện hoặc lý do chưa
xác minh. Không tự mở rộng sang toàn bộ thư mục Views hay Areas/Admin.

| ID | Trang/màn hình | View chính xác | Cách xác định màn hình khi phân tích |
| --- | --- | --- | --- |
| AU01 | Đăng nhập / đăng ký | `TCTEnglish/Views/Account/Auth.cshtml` | `/Account/Login`, `/Account/Register`; kiểm tra hai tab. |
| AU02 | Quên mật khẩu | `TCTEnglish/Views/Account/ForgotPassword.cshtml` | `/Account/ForgotPassword`. |
| AU03 | Xác nhận yêu cầu quên mật khẩu | `TCTEnglish/Views/Account/ForgotPasswordConfirmation.cshtml` | Là view kết quả của flow; không giả định có GET cùng tên, không gửi email để mở nó. |
| AU04 | Đặt lại mật khẩu | `TCTEnglish/Views/Account/ResetPassword.cshtml` | `/Account/ResetPassword` cần trạng thái/token hợp lệ; không tự tạo token hoặc đặt mật khẩu. |
| AU05 | Hồ sơ | `TCTEnglish/Views/Account/Profile.cshtml` | `/Account/Profile`, phiên test đã đăng nhập. |
| AU06 | Cài đặt tài khoản | `TCTEnglish/Views/Account/Settings.cshtml` | `/Account/Settings`; chỉ quan sát tab/modal, không lưu hoặc xóa. |
| SP01 | Danh sách video Speaking | `TCTEnglish/Views/Speaking/Index.cshtml` | `/Speaking` hoặc `/Speaking/Index`; thư viện và vùng video cá nhân. |
| SP02 | Luyện nói / dictation | `TCTEnglish/Views/Speaking/Practice.cshtml` | `/Speaking/Practice/{id}` với ID test có thật. |
| SP03 | Speaking trong luồng học theo bộ thẻ | `TCTEnglish/Views/Study/Speaking.cshtml` | Xác minh route `/Home/Speaking` và tham số từ controller/source. |
| WR01 | Chọn cấp độ/nội dung Writing | `TCTEnglish/Views/Study/Writing.cshtml` | `/Home/Writing`. |
| WR02 | Danh sách bài Writing | `TCTEnglish/Views/Study/WritingExercises.cshtml` | `/Home/Writing/Exercises`; bộ lọc, phân trang, vùng Premium/AI có sẵn. |
| WR03 | Làm bài Writing | `TCTEnglish/Views/Study/WritingPractice.cshtml` | `/Home/Writing/Practice?exerciseId=...` với bài test có thật. |
| GO01 | Mục tiêu, streak, huy hiệu | `TCTEnglish/Views/Goals/Index.cshtml` | `/Goals`; chỉ xem trạng thái, không cập nhật mục tiêu/phần thưởng. |
| AI01 | AI Chat đầy đủ | `TCTEnglish/Views/Ai/Chat.cshtml` | `/AI/Chat`; không gửi prompt để thử giao diện. |
| AI02 | AI Chat nhúng | `TCTEnglish/Views/Ai/ChatEmbed.cshtml` | `/AI/Chat?embed=true`; kiểm tra host container thực tế. |
| CO01 | Liên hệ | `TCTEnglish/Views/Home/Contact.cshtml` | `/Home/Contact`; không submit/email. |
| AD01 | Dashboard quản trị | `TCTEnglish/Areas/Admin/Views/Dashboard/Index.cshtml` | `/Admin/Dashboard`; cần phiên test Admin. |
| AD02 | Quản lý người dùng | `TCTEnglish/Areas/Admin/Views/UserManagement/Index.cshtml` | `/Admin/UserManagement`; list/filter/modal, không sửa quyền/trạng thái. |
| AD03 | Danh sách video quản trị | `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Index.cshtml` | `/Admin/SpeakingVideoManagement`. |
| AD04 | Thêm video Speaking | `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Create.cshtml` | `/Admin/SpeakingVideoManagement/Create`; không import hoặc trích xuất transcript. |
| AD05 | Sửa video Speaking | `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/Edit.cshtml` | `/Admin/SpeakingVideoManagement/Edit/{id}`; không lưu. |
| AD06 | Danh sách bài viết quản trị | `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Index.cshtml` | `/Admin/WritingExerciseManagement`. |
| AD07 | Thêm bài viết | `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Create.cshtml` | `/Admin/WritingExerciseManagement/Create`; không lưu. |
| AD08 | Sửa bài viết | `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/Edit.cshtml` | `/Admin/WritingExerciseManagement/Edit/{id}`; không lưu. |
| AD09 | Quản trị AI | `TCTEnglish/Areas/Admin/Views/AiManagement/Index.cshtml` | `/Admin/AiManagement`; không train/reload model/chạy yêu cầu AI. |

URL ở trên là bản đồ khởi đầu từ source, không chứng nhận trang đã được mở bằng
browser. Đối chiếu route hiện tại trước khi dùng; không đổi route để khớp bảng.

### Tài nguyên hỗ trợ phải đọc đúng mức

- `TCTEnglish/Views/Ai/_ChatShell.cshtml`: giao diện thực dùng chung AI01/AI02.
- `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/_ExerciseForm.cshtml`:
  markup form chung AD07/AD08.
- `_ExerciseFormScripts.cshtml` trong cùng thư mục: **chỉ đọc**, thuộc script.
- CSS đang có: `TCTEnglish/wwwroot/css/auth-stitch.css`, `account.css`,
  `speaking.css`, `writing-index.css`, `writing-exercises.css`, `writing.css`,
  `goals.css`, `ai-chat.css`, `admin-writing-exercise.css`, `contact.css`.
  Xác minh nơi include/consumer trước khi đề xuất; tên file không chứng minh độc lập.
- JS cần đọc để bảo toàn DOM contract: `TCTEnglish/wwwroot/js/speaking.js`,
  `writing-index.js`, `writing-exercises.js`, `writing.js`, `goals.js`, `ai-chat.js`,
  `admin-user-management.js`, `contact.js`, cùng inline script của view tương ứng.

### Shared assets có nguy cơ lan sang phần của đồng đội

Đọc để hiểu, **không đưa vào danh sách sửa mặc định**, kể cả sau một lời duyệt UI
chung chung: `TCTEnglish/Views/Shared/_Layout.cshtml`,
`TCTEnglish/Areas/Admin/Views/Shared/_AdminLayout.cshtml`,
`TCTEnglish/Views/_ViewStart.cshtml`, `TCTEnglish/Views/_ViewImports.cshtml`,
`TCTEnglish/wwwroot/css/layout.css`, `site.css`, `tct-learning-ui.css`,
`TCTEnglish/wwwroot/js/layout.js`, `site.js`, `user-activity.js`,
`TCTEnglish/wwwroot/css/ai-chat-launcher.css` và `TCTEnglish/wwwroot/js/ai-chat-launcher.js`.

`account.css` cũng được `_AdminLayout.cshtml` include nên có ảnh hưởng rộng.
Chat launcher nằm trong layout chung nên việc bạn làm AI không mặc nhiên cho phép
đổi launcher trên toàn website. Đề xuất CSS theo page root, không override global
`body`, `:root`, `.btn`, `.card`, `input`, `table` cho tất cả trang. Bất cứ thay đổi
shared nào phải nêu consumer ngoài scope và có quyết định riêng của chủ dự án.

### Không thuộc lượt rà soát này

Landing, Dashboard người dùng `Views/Home/Index.cshtml`, layout/sidebar/footer
chung, Reading, Listening, Vocabulary, Folder, Set/Card, Class/Chat, Billing/Payment,
Notification và các trang quản trị những mảng đó. Không dùng từ “dashboard” để
gộp Dashboard người dùng với Dashboard Admin. `Study/WriteMode.cshtml` là chế độ
ôn từ vựng, không phải `Study/WritingPractice.cshtml`; không gộp vào Writing.

## 5. Những hợp đồng không được tác động

Trong phân tích, lập bảng hook/consumer để đề xuất UI khả thi mà không cần sửa
nghiệp vụ. Trong mọi lô triển khai tương lai cũng phải giữ các điểm này trừ khi
chủ dự án cấp một nhiệm vụ khác rõ ràng:

- C# trong controller/service/model/ViewModel/hub/worker; Razor `@model`, khối
  tính toán/điều kiện/vòng lặp/phân quyền/serialize dữ liệu.
- Route/action, query/form parameter, `name`, `asp-for`, `asp-action`,
  `asp-controller`, `asp-area`, `asp-route-*`, method/enctype, antiforgery,
  validation binding, URL từ Razor và JSON payload.
- `id`, selector class, `data-*`, DOM nesting/quan hệ mà JavaScript dựa vào,
  inline event handler, script init và thứ tự include. Nếu markup mới cần sửa
  JS mới chạy được, loại phương án đó khỏi phạm vi UI-only hoặc ghi blocker.
- State machine, permission/quota/Premium gating, kết quả chấm điểm, thời gian
  ghi âm, microphone/media/YouTube APIs, transcript, AI request/prompt, lưu tiến
  độ, streak/badge, tài khoản, SQL/EF/migrations/seeding, email/OAuth.
- Nội dung bài học/copy mang nghĩa nghiệp vụ, số liệu thật và trạng thái thực.
  Không làm đẹp bằng cách ẩn lỗi, xóa validation, giả số liệu hoặc bỏ tính năng.

## 6. Hướng thiết kế để phân tích

Giữ identity trong DESIGN.md: primary `#4255ff`, nền `#f6f7fb`, surface trắng,
text `#303545`, border `#e5e7eb`, Inter/fallback hiện có. Đối chiếu font/icon của
**từng layout**, không áp một quy tắc icon chung lên mọi trang: admin hiện có
Bootstrap Icons; frontend có các font/icon khác nhau đang được include.

Mục tiêu nâng cấp: phân cấp rõ, dễ đọc nội dung Việt/Anh, khoảng cách nhất quán,
CTA chính rõ, form/table thuận tiện, trạng thái dễ hiểu, mobile không vỡ bố cục.
Ưu tiên giao diện học tập và quản trị thực dụng; không tự biến thành landing
page, bento/glassmorphism dày đặc, gradient phủ mọi khối hoặc thêm animation để
trang có vẻ mới. Animation chỉ được đề xuất nếu có ích, hỗ trợ reduced motion;
không sửa animation/timer/JS trong lượt phân tích.

Đọc brand gốc là bước nền, không phải kết luận rằng trang đã đồng nhất. Một số
view tự include font/style và có CSS inline. Không gọi chúng là lỗi thị giác khi
chưa có bằng chứng cụ thể. Không có thư mục `docs/design-references/` trong
checkout lúc soạn; các link mockup trong plan cũ có thể không tồn tại. Không tự
tải mẫu/ảnh, không bịa đã xem reference hoặc render một mockup thay ứng dụng.

## 7. Trình tự phân tích — được thực hiện ngay

### A0. Chốt baseline và khả năng quan sát

- [ ] Đọc các tài liệu bắt buộc, báo đã hiểu `ANALYSIS_ONLY` và một agent.
- [ ] Ghi branch/HEAD, `git status --short`, các file đã thay đổi trước lượt này.
      Không dọn/reset/stash; không kéo/merge code mới trong lúc audit.
- [ ] Kiểm tra khả năng dùng CodeGraph và browser của **phiên Antigravity hiện tại**.
      Chrome DevTools MCP vừa cài ở Codex user config không mặc nhiên có ở đây.
- [ ] Xác nhận base URL local đã chạy, viewport và tài khoản test/role có sẵn bằng
      thông tin không bí mật. Nếu thiếu, hỏi ngắn trong báo cáo/chat và tiếp tục
      source audit. Không xin mật khẩu/token trong chat hay đọc secrets.

Không tự `dotnet run`/khởi động server: known issue ghi nhận startup seeding có
ghi database. Không tạo account, seed, migration hoặc bật worker để mở trang.
Chỉ dùng môi trường local/test đã sẵn sàng cho việc quan sát; không thao tác
production. Nếu trang tự ghi hoạt động/tiến độ/quota khi mở, chỉ quan sát trên dữ
liệu test đã cho phép; nếu chưa có môi trường đó thì dừng phần browser tương ứng.

### A1. Inventory source và vùng ảnh hưởng

- [ ] Đi qua đủ 25 ID theo nhóm; map route/view/layout/partial/CSS/JS.
- [ ] Với mỗi nhóm, ghi các selector/form/data binding phải giữ và các consumer
      shared. Code C# chỉ đọc phần cần xác nhận render/contract, không audit/refactor backend.
- [ ] Phân biệt trang độc lập, modal, tab, embedded shell, partial và trạng thái
      chỉ truy cập qua flow. Không tạo route hoặc dữ liệu mới để tiện xem.

### A2. Quan sát UI có bằng chứng

- [ ] Nếu có môi trường phù hợp, quan sát desktop 1440x900, tablet 768x1024 và
      mobile 375x812. Có thể spot-check 320px/zoom 200% để phát hiện overflow.
- [ ] Kiểm tra hierarchy/spacing/type/contrast, focus/keyboard, nhãn form, action
      priority, table/card, modal, long text và bố cục mixed Việt/Anh.
- [ ] Mỗi ID ghi trạng thái hiện có: normal, loading, empty, error/validation,
      disabled/locked, success/completed, mobile. Không cần ép tất cả state trên
      mọi trang; đánh dấu không áp dụng hoặc không quan sát được.
- [ ] Chỉ mở tab/modal/accordion không ghi dữ liệu. Không submit, gửi email/AI,
      import, train, upload, thay quyền, xóa, ghi âm, chấm bài hay hoàn thành bài.
      Không inject CSS/HTML hoặc sửa DOM để giả lập bản nâng cấp.
- [ ] Nêu “quan sát browser”, “suy ra từ source” hoặc “chưa xác minh” ở từng finding.
      Console/network chỉ làm bằng chứng; lỗi backend ghi riêng ngoài scope.

### A3. Đề xuất và chia lô

- [ ] Lập finding có ID, page ID, file:line, viewport/state, ảnh nếu có, hệ quả
      người dùng, mức độ và đề xuất nhỏ nhất có ích. Không báo số điểm giả chính xác.
- [ ] Đưa một hướng thiết kế khuyến nghị dựa identity hiện có, giải thích cho từng
      module; mô tả wireframe bằng chữ/bảng, không viết code hoặc sửa thử UI.
- [ ] Với mỗi đề xuất, chỉ ra các file markup/CSS dự kiến, selector giữ nguyên,
      shared impact, mức nỗ lực tương đối S/M/L và cách nghiệm thu sau này.
- [ ] Phân loại: UI-only khả thi; cần quyết định shared; phụ thuộc behavior/backend
      và ngoài scope. Không biến phụ thuộc backend thành việc Gemini tự sửa.
- [ ] Tự review độ phủ và ranh giới bằng cùng một agent; lưu báo cáo và dừng.

Mức độ: P1 = cản trở thao tác/đọc nội dung quan trọng hoặc lỗi accessibility lớn;
P2 = gây khó dùng/không nhất quán đáng kể; P3 = polish nhỏ. Nếu chưa có bằng chứng
render, không gọi một trạng thái là “đã tái hiện”. Không bắt buộc tìm lỗi cho đủ số.

## 8. Cấu trúc báo cáo bắt buộc

File đầu ra: `docs/ui-audits/2026-10-29-bao-chau-ui-analysis.md`.

1. **Trạng thái và baseline:** ANALYSIS_ONLY, model người dùng chọn, HEAD/branch,
   phạm vi đã đọc, môi trường/URL, browser/tool có thật, giới hạn.
2. **Coverage đủ 25 ID:** route/view, role/state, nguồn đã đọc, viewport đã xem,
   screenshot, browser/source-only/chưa xác minh; không dùng “done” chung chung.
3. **Contract & blast radius:** selector/form/partial/CSS/JS consumers; shared assets
   phải giữ. Đặc biệt account.css, admin layout và AI launcher.
4. **Findings ưu tiên:** mỗi dòng có evidence, ảnh/line, hệ quả và đề xuất UI cụ thể.
5. **Hướng nâng cấp:** hierarchy, typography, spacing, card/table/form/feedback,
   responsive/accessibility theo module, dựa brand và behavior hiện có.
6. **Đề xuất lô U1–U5:** exact file list, việc làm/không làm, selector cần giữ,
   shared decision, effort và acceptance; đây vẫn là đề xuất chưa cấp quyền sửa.
7. **Ma trận nghiệm thu tương lai:** bài kiểm tra hiển thị/keyboard và chức năng
   sẵn có cần được chủ dự án/agent được giao triển khai kiểm tra sau phê duyệt.
8. **Blocker và quyết định cần chủ dự án chọn:** không có login/server/reference
   thì nêu chính xác. Backend defect để mục ngoài scope; không xin “sửa luôn”.
9. **Diff cuối và câu dừng:** file nào đã tạo/sửa, xác nhận chỉ có báo cáo/bằng chứng
   được phép. Nếu file khác đã thay đổi từ trước, không nhận là thay đổi của mình.

Checklist đầu ra:

- [ ] Đủ 25 ID hoặc có lý do rõ cho mỗi mục chưa xem; không gộp mất AU03/SP03/AI02.
- [ ] Có file/line và trạng thái bằng chứng, không bịa screenshot/browser result.
- [ ] Không có file app/tooling/config nào được sửa trong lượt phân tích.
- [ ] Không có đề xuất ngầm cần đổi backend/JS hoặc CSS global không đánh dấu.
- [ ] Nêu ưu tiên và một lô đầu tiên đề nghị chủ dự án xem xét.
- [ ] Kết thúc: **“Đã hoàn tất phân tích UI. Chưa triển khai. Chờ Nguyễn Bảo Châu
      duyệt phạm vi/lô cụ thể.”**

## 9. Lộ trình triển khai dự kiến — KHÓA cho tới khi được duyệt

Đây là cách nhóm công việc để báo cáo đề xuất, **không phải danh sách cần thực thi
ngay**. Chỉ lời duyệt rõ “triển khai lô Ux, các file..., theo hướng...” mới mở lô
đó. “Tiếp tục”, “đẹp hơn”, “xem tiếp” mặc định tiếp tục phân tích, không mở khóa.

| Lô | ID | Mục tiêu đề xuất | Điều kiện phê duyệt |
| --- | --- | --- | --- |
| U1 | AU01–AU06, CO01 | Hệ thống form/account/contact, ưu tiên nhãn, readability, responsive, feedback. | Duyệt file list; quyết định riêng về account.css vì admin dùng chung. |
| U2 | SP01–SP03 | Library, video/sentence/transcript, control hierarchy, mobile. | Giữ nguyên selector, microphone/media/state/progress; không sửa speaking.js. |
| U3 | WR01–WR03, AD06–AD08 | Luồng chọn/làm bài và form quản trị Writing. | Giữ JSON payload/data attributes/validation; _ExerciseFormScripts.cshtml chỉ đọc. |
| U4 | GO01, AI01–AI02, AD09 | Mục tiêu, badge, chat đọc dễ và panel quản trị AI. | Không sửa quota, AI calls, reward logic, launcher global hay tiến độ. |
| U5 | AD01–AD05 | KPI/list/filter/modal quản trị còn lại. | Không đổi _AdminLayout hoặc khu vực admin của đồng đội; giữ RBAC/action hooks. |

Sau phê duyệt, một lô UI chỉ gồm markup trình bày và CSS được chỉ tên. Razor
logic, mọi JS (kể cả inline), backend và cấu hình vẫn khóa. Nếu không thể đạt
functional parity trong giới hạn đó thì báo blocker; không mở rộng scope.

Nghiệm thu một lô tương lai: so sánh before/after ở cùng dữ liệu/viewport; không
vỡ layout/console hoặc mất control; hợp đồng DOM/forms/JSON giữ nguyên; các test
liên quan và `dotnet build` đạt; rà regression trên consumer shared nếu được duyệt
chạm shared. Chạy `dotnet test` theo AGENTS.md bởi người/agent được giao triển
khai. Browser/test không chạy được phải ghi chưa xác minh. Build/test không phải
việc cần chạy để bắt đầu lượt phân tích, và không thay thế lời duyệt triển khai.

## 10. Ghi nhận lúc soạn plan

Codex chỉ soạn plan và prompt; đã đối chiếu file/asset include/contract từ source,
chưa thực hiện audit thị giác trên 25 trang. Không kết luận sẵn trang nào đẹp/xấu
hoặc có lỗi. Plan cũ của Speaking trỏ mockup không có trong checkout hiện tại;
Razor được CodeGraph bao phủ không đầy đủ phần markup nên đã đọc source có giới hạn.
`account.css` được layout admin include là bằng chứng cụ thể cho rủi ro shared.
Không có implementation, đổi cấu hình, commit hoặc push trong lần soạn này.
