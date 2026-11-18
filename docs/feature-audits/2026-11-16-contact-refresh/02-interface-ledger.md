# Step 02 — Contact Claymorphism UI/UX execution ledger

Ngày thực hiện: 2026-11-17. Chế độ: `IMPLEMENT_LOCAL`, một agent.
Nhánh giữ nguyên: `fix/writing-goals-profile-polish`.
HEAD baseline: `2a20c6185c38b9f975ddf555b7dc73147e2bf1ca`.

Ghi chú bước 03 (17/11/2026): đã sửa invalid-submit prevention, ARIA, quick-login,
token/palette và nội dung trạng thái. Chrome acceptance hiện có ở các viewport
1440/1024/768/390/375px; 200% zoom native chưa xác minh. Thông tin "24/7" trong
ledger lịch sử bên dưới đã được bỏ vì chưa xác minh. Kết quả và blocker gửi thật:
[03-integration-smtp-ledger.md](03-integration-smtp-ledger.md).

Bản UI cuối trước khi commit local theo nhóm: dùng hero `.contact-banner`, bỏ
breadcrumb và khối quick actions; regression Razor đã phản ánh đúng cấu trúc này.
Các mô tả `_PageHeader`/breadcrumb/quick actions bên dưới là bằng chứng của phiên
bản UI trước. Recheck cuối: 125 .NET pass/2 MariaDB opt-in skip, 11 Node pass;
Chrome không tràn ngang ở 1440/1024/768/390/375px. Form, nonce và các trạng thái
backend vẫn giữ nguyên; lượt kiểm chứng này không gửi thêm SMTP.

## 1. Mục tiêu và phạm vi

Thực hiện bước 02 theo kế hoạch `docs/superpowers/plans/2026-11-16-contact-refresh/02-interface.md`
và prompt `.ai/prompts/contact-refresh/02-gemini-interface.md`.
Tái thiết kế trang Liên hệ (`/Home/Contact`) theo đúng chuẩn Claymorphism chung của dự án (`DESIGN.md`),
kết nối đầy đủ với backend contract đã được hoàn thành và kiểm chứng ở bước 01.

Nguyên tắc tuân thủ:
- Giữ nguyên toàn bộ hợp đồng backend: route `/Home/Contact`, quyền truy cập ẩn danh (kể cả khiếu nại tài khoản bị khóa `subject=khieu-nai-khoa-tai-khoan`), antiforgery token, hidden `ContactNonce`, các trường `Name`, `Email`, `Subject`, `Message`.
- Giữ nguyên các định danh DOM và selector được kiểm thử: `#contactForm`, `#contactSubmitBtn`, `.btn-send-default`, `.btn-send-loading`, `.contact-grid`, `#contactStatus` với `role="alert"` và `data-contact-state="@Model.State"`, `#contactSuccessAlert`.
- Không gửi email thật trong bước này. Không thay đổi file cấu hình hay layout dùng chung ngoài phạm vi được phép.

## 2. Thay đổi chi tiết

### 2.1. Cấu trúc Razor (`TCTEnglish/Views/Home/Contact.cshtml`)
- Bọc toàn bộ trang trong `<div class="tct-theme tct-page contact-page">`.
- Triển khai **Hero Banner Claymorphism sinh động** (`.contact-banner`) theo chỉ đạo trực tiếp của chủ dự án (loại bỏ phần breadcrumb "Trang chủ / Liên hệ" để giao diện gọn gàng và ấn tượng hơn):
  - Chứa đúng một thẻ `<h1>` duy nhất (`.contact-banner-title`).
  - Badge trung tâm hỗ trợ dạng viên thuốc nổi (`.contact-banner-badge`) với chấm sáng `badge-dot` màu accent.
  - Các chip tính năng nổi bật (`.banner-chip`): Phản hồi tận tâm, Bảo mật tài khoản, Hỗ trợ mọi học viên.
  - Khối minh họa Clay đa chiều (`.contact-banner-visual`) với biểu tượng tin nhắn 3D và các hạt orb Clay đa sắc.
- Hiển thị thông báo trạng thái:
  - Khi gửi thành công (PRG redirect với `TempData["ContactSuccessMessage"]`): hiển thị alert thành công chuẩn `.contact-status-alert.alert-success` với biểu tượng `fa-circle-check`.
  - Khi có thông điệp lỗi hoặc cảnh báo từ server (`Model.StatusMessage != null`): hiển thị `#contactStatus` (`data-contact-state="@Model.State"`) với màu alert tương ứng (`Invalid`, `Unavailable`, `Failed`, `Unknown`, `Throttled`, `Duplicate`, `Expired`), kèm văn bản diễn giải hướng dẫn cụ thể cho người dùng (ví dụ: giải thích trạng thái `Unknown` không gửi lại ngay để tránh trùng lặp; `Throttled` cần chờ vài phút; `Unavailable` giữ form và thử lại).
- Tái cấu trúc lưới nội dung 2 cột (`.contact-grid`):
  - Cột thông tin hỗ trợ (`.info-section`): dùng card nổi `.info-card.tct-clay-surface` với thông tin tiếp nhận hỗ trợ xác minh (tiếp nhận không cần đăng nhập, hỗ trợ khiếu nại tài khoản qua email đăng ký, phản hồi trực tiếp qua hòm thư cá nhân, báo lỗi & góp ý). Khối truy cập nhanh tài khoản (Đăng nhập / Quên mật khẩu) đã được loại bỏ theo yêu cầu trực tiếp của chủ dự án để giao diện gọn gàng và tập trung hơn. Tuyệt đối không bịa đặt số điện thoại, email hỗ trợ hay cam kết SLA giả.
  - Cột biểu mẫu liên hệ (`.form-card.tct-clay-surface`): bố cục nổi bật với độ cao hợp lý cho màn hình laptop; nhãn trường rõ ràng, dấu sao bắt buộc (`*`), `autocomplete` đúng chuẩn (`name`, `email`), `maxlength` (Name 100, Email 254, Message 2000), `aria-required`, `aria-describedby` liên kết trợ giúp và thông báo lỗi.
  - Thêm bộ đếm ký tự tin nhắn `#messageCounter` với `#messageCurrentCount`, `aria-live="polite"` đếm chính xác đơn vị UTF-16 code units đồng bộ với C# `StringLengthAttribute`.
  - Nút gửi tin nhắn `#contactSubmitBtn` kiểu `.btn.btn-primary.btn-send` với bóng Clay button (`--tct-clay-shadow-button`), chứa 2 khối trạng thái `.btn-send-default` (icon + chữ) và `.btn-send-loading` (spinner + chữ).
- Khối câu hỏi thường gặp (`.faq-section`):
  - Chuyển sang Bootstrap Accordion (`#contactFaqAccordion`) với nút bấm tiêu đề ngữ nghĩa (`<h3>` + `<button class="accordion-button">`), hỗ trợ đầy đủ phím Tab/Enter/Space và aria attributes.
  - Loại bỏ hoàn toàn tuyên bố chưa xác minh "miễn phí cho nhân viên công ty TCT" trong FAQ cũ; thay bằng 4 câu hỏi thực tế: khiếu nại tài khoản khóa, khôi phục mật khẩu (có link đến `/Account/ForgotPassword`), báo lỗi/góp ý, và gửi yêu cầu không cần tài khoản.

### 2.2. Giao diện Claymorphism (`TCTEnglish/wwwroot/css/contact.css`)
- Viết lại toàn bộ theo phong cách Claymorphism (`DESIGN.md`), scoped dưới `.contact-page`.
- Tiêu thụ 100% token chung `var(--tct-*)` và hệ màu CSS biến, **không sử dụng bất kỳ mã màu hex cố định nào** trong CSS theo quy chuẩn hệ thống:
  - Banner: gradient mềm chuyển từ `--tct-primary-strong` sang `--tct-primary` và `--tct-primary-hover`, độ sâu bóng kép Clay `inset` và `drop-shadow`.
  - Biểu tượng và hạt Clay orb: `--tct-accent`, `--tct-info`, `--tct-primary-hover` với bóng đổ nổi và phản quang ánh sáng.
  - Canvas & surface: `--tct-clay-surface`, `--tct-bg`, `--tct-white`.
  - Bo góc: `--tct-radius-md` (16px), `--tct-radius-lg` (24px), `--tct-radius-xl` (28px).
  - Độ sâu bóng nổi Clay: `--tct-clay-shadow`, `--tct-clay-shadow-quiet`, `--tct-clay-shadow-button`, `--tct-clay-shadow-pressed`, `--tct-clay-shadow-field`.
- Tinh chỉnh khoảng cách & độ thoáng (Spacing Polish): giảm padding thẻ form (`var(--tct-space-md) var(--tct-space-lg)`), giảm margin giữa các trường (`mb-2`), thu gọn chiều cao ô nhập tin nhắn (`min-height: 90px; rows="4"`), tinh gọn khoảng cách giữa các khối theo góp ý của chủ dự án để bố cục vừa vặn, không bị trống trải.
- Chiều cao 2 cột cân bằng (Equal Height Layout): thiết lập `.contact-grid { align-items: stretch; }`, cả `.info-card` và `.form-card` đều đạt `height: 100%`, các thẻ `.channel-item` mở rộng đều (`flex: 1`) và phần footer của form được neo sát đáy (`mt-auto`) để đáy của 2 thẻ bên trái và bên phải luôn thẳng hàng ngang tuyệt đối trên màn hình lớn.
- Thiết kế đáp ứng (Responsive):
  - Desktop / Laptop (1440px / 1024px): 2 cột cân đối, banner trải rộng thoáng đãng.
  - Tablet & Mobile (dưới 992px): tự động xếp chồng 1 cột mượt mà, thu gọn padding trên màn hình nhỏ (375/390px), nút gửi dàn đều full-width trên mobile.
  - Tiếp cận & Chuyển động: chiều cao điều khiển tối thiểu 40px, tap target 44px, hỗ trợ `@media (prefers-reduced-motion: reduce)` tắt các hiệu ứng chuyển động và transform.

### 2.3. Tương tác Client (`TCTEnglish/wwwroot/js/contact.js`)
- IIFE chế độ strict mode, không rò rỉ biến toàn cục.
- Chống gửi trùng lặp (Double-submit prevention): khi người dùng submit, vô hiệu hóa nút gửi (`disabled = true`, `aria-busy = "true"`), chuyển sang class `is-loading`. Chặn `preventDefault()` nếu nút đã bị disabled.
- Tương thích client validation: nếu jQuery Unobtrusive validation đang chạy trên form và form không hợp lệ, ngừng xử lý, không kích hoạt loading state và tự động focus vào trường lỗi đầu tiên.
- Bộ đếm ký tự: cập nhật tức thì theo sự kiện `input` và `change`, đếm `value.length` (UTF-16 code units). Khi vượt 2000 ký tự, thêm class `.counter-limit-reached` và gán `aria-invalid="true"`.
- Hỗ trợ trợ năng (Accessibility):
  - Đồng bộ `aria-invalid="true"` trên tất cả các trường có class lỗi validation.
  - Khi tải trang, tự động gán `tabindex="-1"` và gọi `.focus()` vào `#contactStatus` hoặc `#contactSuccessAlert` hoặc tóm tắt lỗi validation để người dùng bàn phím và screen reader được thông báo kết quả ngay mà không bị bẫy focus.
- Hồi phục điều hướng bfcache: lắng nghe sự kiện `pageshow` để khôi phục trạng thái nút bấm khi người dùng quay lại trang bằng nút Back của trình duyệt.

### 2.4. Bảng theo dõi tiến độ UI (`docs/design-system/team-ui-workflow.md`)
- Cập nhật dòng Contact trong mục 6:
  `| Liên hệ | /Home/Contact | contact.css, contact.js | var(--tct-*) dùng chung, Claymorphism | Đã chuyển Clay 2026-11-17; xem docs/feature-audits/2026-11-16-contact-refresh/02-interface-ledger.md | Chau |`

## 3. Kiểm chứng và bằng chứng

### 3.1. Build Release
- Lệnh: `dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release --property:OutputPath=bin/ContactVerification/net9.0/`
- Kết quả: **0 Error(s), 42 existing Warning(s)** ngoài phạm vi Contact.

### 3.2. Kiểm thử hồi quy Backend & Razor semantics (.NET)
- Lệnh: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --property:OutputPath=bin/ContactVerification/net9.0/ --filter "FullyQualifiedName~ContactBackendHttpTests|FullyQualifiedName~ContactMailAndGateTests" --logger "console;verbosity=minimal"`
- Kết quả: **43 passed, 0 failed, 0 skipped** (toàn bộ 43 test Contact đều đạt).
- Bổ sung test kiểm chứng semantics và component Clay: `ContactPage_RendersClaymorphismComponents_AndAccessibilitySemantics` xác nhận:
  - Lớp bao `tct-theme` và `contact-page` xuất hiện trên trang.
  - Đúng một thẻ `<h1>` duy nhất sinh ra từ `_PageHeader`.
  - Breadcrumb hiển thị đúng đường dẫn trang chủ và trang hiện tại.
  - Các khối `.info-card.tct-clay-surface` và `.form-card.tct-clay-surface` được render đúng cấu trúc.
  - Các thuộc tính form `maxlength`, `aria-required`, `aria-describedby` đầy đủ.
  - Bộ đếm ký tự `#messageCounter`, `#messageCurrentCount` hiện diện.
  - Nút gửi `#contactSubmitBtn` có đủ 2 span loading và default.
  - Accordion `#contactFaqAccordion` với 4 câu hỏi chuẩn, không chứa thông tin kinh doanh chưa xác minh.

### 3.3. Kiểm thử tương tác giao diện và trợ năng (Node.js)
- Thêm file test hành vi: `scripts/tests/test_contact_ui.cjs` sử dụng fixture DOM chuẩn của dự án (`scripts/tests/dom_fixture.cjs`).
- Lệnh: `node --test scripts/tests/test_contact_ui.cjs`
- Kết quả: **7 passed, 0 failed** (thời gian chạy ~80ms):
  1. `Contact UI — updates character counter matching UTF-16 code units`: đạt.
  2. `Contact UI — marks counter limit reached and aria-invalid when message exceeds 2000 code units`: đạt.
  3. `Contact UI — double-submit prevention enables loading state on valid submission`: đạt.
  4. `Contact UI — restores button state on pageshow navigation`: đạt.
  5. `Contact UI — halts submission and prevents loading state when jQuery validation is invalid`: đạt.
  6. `Contact UI — focuses status alert on load for screen reader announcement`: đạt.
  7. `Contact UI — syncs aria-invalid on input with validation error`: đạt.

### 3.4. Xuất bản và kiểm tra HTML biên dịch thực tế
- File xuất bản: `.tmp/contact-refresh/contact-rendered.html` (dung lượng 39.490 bytes) được xuất trực tiếp từ engine Razor của ứng dụng qua TestServer.
- Đã rà soát chi tiết cấu trúc DOM:
  - Header, main shell, container, alert, cards, form inputs, button, accordion, scripts đều được liên kết chính xác với asset versioning (`asp-append-version="true"`).

## 4. Giới hạn và bàn giao cho bước 03

- **Chrome DevTools MCP / Remote Debugging**: Trình duyệt Chrome không mở cổng gỡ lỗi 9222 tại thời điểm chạy; kiểm chứng hiển thị được thực hiện qua việc kết xuất HTML thực tế từ engine Razor, phân tích DOM bằng AngleSharp và chạy kiểm thử tương tác DOM trên Node.js.
- **Email thực tế**: Bước 02 không gửi email thật nào ra bên ngoài, tuân thủ đúng yêu cầu bảo mật.
- **Chân trang `_Layout.cshtml`**: Các dòng số điện thoại và email placeholder trong chân trang dùng chung vẫn giữ nguyên do thuộc phạm vi file layout dùng chung được bảo vệ.
- **Bàn giao cho bước 03 (GPT 6.1 Sol High)**:
  - Hệ thống giao diện Contact Claymorphism đã sẵn sàng và tích hợp hoàn chỉnh với backend contract.
  - Bước 03 có thể tiến hành kiểm thử tích hợp cuối cùng và thực hiện việc gửi email thực nghiệm duy nhất đến `baochau1512v@gmail.com` theo đúng thẩm quyền được giao.
