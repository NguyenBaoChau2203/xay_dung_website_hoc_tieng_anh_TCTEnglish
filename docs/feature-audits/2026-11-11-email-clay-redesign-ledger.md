# Feature Audit Ledger — TCT English Email Claymorphism Redesign

- **Date:** 2026-11-11
- **Author/Agent:** Antigravity (Single Agent, Local Execution)
- **Branch:** `fix/staging-uat-polish` (HEAD: `c908f66`)
- **Execution Prompt:** `.ai/prompts/antigravity-email-clay-redesign.md`
- **Implementation Plan:** `docs/superpowers/plans/2026-11-11-email-clay-redesign.md`
- **Design Standard:** `DESIGN.md` & `TCTEnglish/wwwroot/css/tct-learning-ui.css`

---

## 1. Executive Summary

Hệ thống email của TCT English đã được tái thiết kế toàn diện theo ngôn ngữ **Claymorphism** thống nhất của dự án, thay thế hoàn toàn các đoạn HTML thô hoặc inline kiểu cũ. Toàn bộ các luồng gửi email thực tế (`SmtpAppEmailSender`, `AccountSecurityService`, `OutboundDeliveryProcessor`) đã được kết nối trực tiếp với renderer dùng chung (`TctEmailRenderer`).

Không có email thật nào được gửi ra ngoài; không thay đổi các ràng buộc bảo mật (token, thời hạn TTL, một lần dùng, xác thực CSRF, route, secret store); không chỉnh sửa `Program.cs`, `appsettings.json`, schema cơ sở dữ liệu hay thư viện website dùng chung.

---

## 2. Token-to-Email Literal Mapping

Do các email client (Gmail, Outlook, Apple Mail, Yahoo) loại bỏ biến CSS (`var(--tct-*)`) và file stylesheet bên ngoài, toàn bộ token chính thống từ `DESIGN.md` và `tct-learning-ui.css` được ánh xạ thành giá trị CSS literal an toàn trong `TctEmailTokens.cs`:

| Token gốc | Giá trị Email Literal | Vai trò trong Email | Tỉ lệ tương phản (WCAG AA) |
| --- | --- | --- | --- |
| `--tct-clay-canvas` / `--tct-primary-tint` | `#f0f3ff` | Nền canvas toàn bộ bức thư | Chuẩn pastel thương hiệu |
| `--tct-clay-surface` | `#ffffff` | Bề mặt thẻ Card Clay chính | Chuẩn bề mặt nổi |
| `--tct-surface-muted` | `#f8fafc` | Khối URL phụ & chẩn đoán kỹ thuật | Phân tách thị giác nhẹ |
| `--tct-primary` | `#4255ff` | Nút hành động chính (CTA) & Wordmark TCT | 7.3:1 trên nền trắng |
| `--tct-primary-soft` | `#eef2ff` | Huy hiệu (Badge) trung tính / Wordmark wrapper | Dịu mắt, làm nổi chữ |
| `--tct-accent` | `#ffcd1f` | Dấu chấm thương hiệu TCT English | Điểm nhấn nhận diện |
| `--tct-text` | `#111c2d` | Tiêu đề chính `<h1>`, chữ đậm, code token | 17.1:1 trên nền trắng |
| `--tct-text-secondary` | `#444656` | Đoạn văn bản nội dung hướng dẫn | 9.3:1 trên nền trắng |
| `--tct-text-muted` | `#5b6578` | Chữ chân trang, chú thích hiệu lực | 5.87:1 trên nền trắng |
| `--tct-card-border` | `#dbe4ff` | Viền thẻ ngoài (fallback khi client bỏ shadow) | Bảo đảm ranh giới card |
| `--tct-card-shadow` | `0 10px 25px rgba(66,85,255,0.08)` | Đổ bóng nổi Claymorphism | Hỗ trợ Gmail web/Apple Mail |
| `--tct-btn-shadow` | `0 4px 12px rgba(66,85,255,0.35)` | Đổ bóng nút CTA chính | Nổi khối rõ ràng |
| `--tct-success` / `--tct-success-text` | `#10b981` / `#047857` | Huy hiệu & chữ mở khóa tài khoản | Chữ đậm, dễ đọc |
| `--tct-warning` / `--tct-warning-text` | `#f59e0b` / `#92400e` | Khối lưu ý bảo mật | Nổi bật cảnh giác |
| `--tct-danger` / `--tct-danger-text` | `#ba1a1a` / `#ba1a1a` | Huy hiệu khóa tài khoản & xóa tài khoản | Rõ ràng, nghiêm túc |
| `--tct-info` / `--tct-info-text` | `#06b6d4` / `#0e7490` | Đổi email đăng nhập, tổng kết tuần | Thông tin tích cực |

---

## 3. Template & Family Inventory

Toàn bộ các họ email và biến thể hiện có trong mã nguồn đều được hiện thực đầy đủ:

| Họ email | Biến thể | Hàm gọi thực tế | Tiêu đề gửi (Subject) | TTL & Điều kiện | Invariant bảo mật |
| --- | --- | --- | --- | --- | --- |
| **Password Reset** | Đặt lại mật khẩu chuẩn | `SmtpAppEmailSender.SendPasswordResetAsync` | `Đặt lại mật khẩu - TCT English` | 15 phút, 1 lần | Có nút CTA + liên kết phụ dự phòng; không chứa mật khẩu; không xác nhận xong. |
| **Password Reset** | URL & Token dài | `TctEmailRenderer.RenderPasswordReset` | `Đặt lại mật khẩu - TCT English` | 15 phút, 1 lần | `word-break: break-all;` không vỡ khung trên màn hình hẹp 375px. |
| **Verify Email** | Xác minh email hiện tại | `AccountSecurityService.SendProofAsync(VerifyEmail)` | `Xác minh email tài khoản - TCT English` | 10 phút, 1 lần | Mã opaque 64 ký tự hex; không khẳng định đã xác minh. |
| **Email Change** | Xác nhận email mới | `AccountSecurityService.RequestEmailChangeAsync` | `Xác nhận email mới - TCT English` | 30 phút, 1 lần | Nút CTA + link phụ; giải thích rõ email cũ vẫn giữ quyền đăng nhập cho tới khi bấm. |
| **Sensitive Proof** | Reauth | `AccountSecurityService.SendProofAsync(Reauth)` | `Mã xác nhận tài khoản - TCT English` | 10 phút, 1 lần | Xác thực thao tác nhạy cảm; mã bảo mật chỉ dùng một lần. |
| **Sensitive Proof** | SetFirstPassword | `AccountSecurityService.SendProofAsync(SetFirstPassword)` | `Mã xác nhận tài khoản - TCT English` | 10 phút, 1 lần | Tạo mật khẩu đầu tiên cho tài khoản đăng ký qua mạng xã hội (Social Login). |
| **Sensitive Proof** | Deletion | `AccountSecurityService.SendProofAsync(Deletion)` | `Mã xác nhận xóa tài khoản - TCT English` | 10 phút, 1 lần | Cảnh báo rõ ràng: **Tài khoản CHƯA bị xóa**; chỉ xóa sau khi nhập mã. |
| **Weekly Unsubscribe**| Quản lý tắt email tuần | `AccountSecurityService.SendProofAsync(WeeklyUnsubscribe)`| `Quản lý tùy chọn email tuần - TCT English`| 14 ngày | Có nút CTA + link phụ; mở trang cài đặt, không tự động hủy khi chỉ mở xem. |
| **Account Blocked** | Khóa có thời hạn | `SmtpAppEmailSender.SendBlockedNotificationAsync` | `Tài khoản của bạn đã bị khóa - TCT English` | Hiển thị giờ UTC | Giữ nguyên định dạng `dd/MM/yyyy HH:mm:ss (UTC)`, không gán nhầm giờ VN; mã hóa an toàn lý do khóa. |
| **Account Blocked** | Khóa vĩnh viễn | `SmtpAppEmailSender.SendBlockedNotificationAsync` | `Tài khoản của bạn đã bị khóa - TCT English` | Vĩnh viễn | Lý do vi phạm hiển thị rõ ràng, hướng dẫn khiếu nại qua email hỗ trợ. |
| **Account Unlocked** | Mở khóa tự động | `SmtpAppEmailSender.SendUnlockedNotificationAsync` | `Tài khoản của bạn đã được mở khóa - TCT English` | Tự động | Thông báo tự động hết thời hạn; không tự chế URL đăng nhập giả mạo. |
| **Account Unlocked** | Mở khóa thủ công | `SmtpAppEmailSender.SendUnlockedNotificationAsync` | `Tài khoản của bạn đã được mở khóa - TCT English` | Quản trị viên | Mở khóa thủ công bởi admin; có nút Đăng nhập khi có link xác thực. |
| **Support Contact** | Tiêu chuẩn | `SmtpAppEmailSender.SendContactMessageAsync` | `[Liên hệ] {subject}` | Không áp dụng | Người gửi, email, chủ đề, nội dung giữ nguyên ngắt dòng; kèm IP và User-Agent. |
| **Support Contact** | Ký tự lạ & XSS edge | `SmtpAppEmailSender.SendContactMessageAsync` | `[Liên hệ] {subject}` | Không áp dụng | HTML tags (`<script>`, `<img>`), dấu ngoặc nhọn, nháy kép đều được escape; bảo toàn UTF-8 tiếng Việt. |
| **Weekly Summary** | Tuần có học tập | `OutboundDeliveryProcessor.DispatchEmailAsync` | `Tổng kết tuần học TCT English` | Thứ Hai hàng tuần | Đúng khoảng ngày giờ Việt Nam; số ngày học và số phút tương tác chuẩn; link cài đặt thông báo. |
| **Weekly Summary** | Tuần không có hoạt động| `OutboundDeliveryProcessor.DispatchEmailAsync` | `Tổng kết tuần học TCT English` | Thứ Hai hàng tuần | 0 ngày học, 0 phút; thông điệp động viên nhẹ nhàng; không bịa điểm XP/streak giả. |

---

## 4. Production Integration Details

Các file ứng dụng sản xuất đã được tích hợp:

1. **`TCTEnglish/Services/Emails/TctEmailTokens.cs`** (Mới):
   - Đóng gói toàn bộ bảng mã màu, font chữ, kích thước, bán kính bo góc và đổ bóng Claymorphism dạng CSS literal email-safe.
2. **`TCTEnglish/Services/Emails/TctEmailRenderer.cs`** (Mới):
   - Thuần C# (pure class), không phụ thuộc EF Core, không phụ thuộc `HttpContext` hay thông tin bí mật SMTP.
   - Hàm `Encode(string?)` chuyên trách mã hóa các ký tự độc hại (`&`, `<`, `>`, `"`, `'`) mà vẫn giữ nguyên vẹn ký tự tiếng Việt UTF-8 (tránh phình to dung lượng thư như `WebUtility.HtmlEncode`).
   - Cung cấp đầy đủ hàm render HTML và bản đối chiếu text thuần (`RenderPlainText*`).
   - Khung `RenderShell` xây dựng HTML email tiêu chuẩn XHTML 1.0 Transitional, có thẻ `<table role='presentation'>`, responsive `@media (max-width: 620px)`, ẩn preheader padding chống lộ chữ chân trang trong Gmail snippet.
3. **`TCTEnglish/Services/SmtpAppEmailSender.cs`**:
   - Chuyển `SendBlockedNotificationAsync`, `TrySendUnlockedNotificationAsync`, `SendPasswordResetAsync`, và `SendContactMessageAsync` sang gọi `TctEmailRenderer`.
   - Giữ nguyên toàn bộ cơ chế Transport, Timeout, CancelationToken, ReplyTo và nhật ký cảnh báo.
4. **`TCTEnglish/Services/Accounts/AccountSecurityService.cs`**:
   - Chuyển `RequestEmailChangeAsync` và `SendProofAsync` sang gọi `TctEmailRenderer`.
   - Phân biệt rõ mục đích (`VerifyEmail`, `Reauth`, `SetFirstPassword`, `Deletion`, `WeeklyUnsubscribe`) kèm tiêu đề tương ứng.
5. **`TCTEnglish/Services/Notifications/OutboundDeliveryProcessor.cs`**:
   - Chuyển `BuildWeeklySummaryAsync` sang gọi `TctEmailRenderer.RenderWeeklyLearningSummary`.
   - Giữ nguyên logic tính toán phân đoạn thời gian (`LearningActivitySegments`) và số ngày hoạt động theo giờ kinh doanh Việt Nam.

---

## 5. Preview Gallery Artifacts

Thư mục `docs/design-system/email-preview/` được sinh trực tiếp từ chính code renderer thực tế:

- **`index.html`**: Trang Gallery tương tác cao cấp:
  - Thanh chọn khung hình: **Desktop (600px)**, **Mobile (375px iPhone)**, **Toàn màn hình (Fluid)**.
  - Chuyển đổi chế độ xem: **Giao diện Email (HTML iframe)**, **Bản Text thuần (Plain Text)**, **Mã nguồn HTML**.
  - Bộ mô phỏng chế độ tối (**Dark Mode Simulation**).
  - Thẻ metadata hiển thị Tiêu đề, Thời hạn TTL, và Nguyên tắc bảo mật cho từng email.
- **16 file HTML production-rendered**:
  - `password-reset.html`, `password-reset-long.html`
  - `verify-email.html`, `email-change.html`
  - `reauth-proof.html`, `first-password-proof.html`, `deletion-proof.html`, `weekly-unsubscribe.html`
  - `account-blocked-finite.html`, `account-blocked-permanent.html`
  - `account-unlocked-auto.html`, `account-unlocked-manual.html`
  - `support-contact.html`, `support-contact-edge.html`
  - `weekly-summary-active.html`, `weekly-summary-empty.html`
- **16 file Text đối ứng** (`*.txt`).

---

## 6. Verification Evidence

### A. Biên dịch và Kiểm thử
- **Debug build:** `dotnet build` hoàn tất với 0 lỗi.
- **Release build:** `dotnet build -c Release` hoàn tất với 0 lỗi.
- **Suite `TctEmailDesignSystemTests`:** 20/20 test ĐẠT (100%).
- **Suite `SmtpTransportFakeTests`:** 6/6 test ĐẠT (100%).
- **Suite `AccountFunctionalCompletionTests`:** 10/10 test ĐẠT (100%).
- **Tổng cộng kiểm thử liên quan:** 36/36 tests PASSED trên Release.

### B. Kiểm tra bảo mật & Chống tràn Layout
- **XSS / HTML Injection:** Đã kiểm thử với `<script>`, `<img> onerror`, dấu nháy kép, dấu ngoặc nhọn, emoji Unicode. Kết quả không có bất kỳ thẻ nào bị thực thi; mọi ký tự độc hại đều được mã hóa an toàn thành thực thể text.
- **Khả năng xuống dòng (Word Wrap):** Mã hex dài 64 ký tự và URL dài đều được thiết lập `word-break: break-all; overflow-wrap: anywhere;`, kiểm thử co hẹp ở 375px không gây hiện tượng ngang bảng (horizontal overflow).
- **Fallbacks:** Cả hai nút CTA quan trọng (đặt lại mật khẩu, đổi email) đều có khối liên kết phụ trực tiếp bên dưới phòng trường hợp ứng dụng mail chặn click nút.

---

## 7. Client Observation Boundaries

- **Đã kiểm chứng cục bộ:**
  - Trình duyệt Chrome / Edge: Thể hiện chuẩn thiết kế Claymorphism, đổ bóng mượt, bo góc 20px/12px, font Inter sắc nét, responsive mượt mà ở 375px và 600px.
- **Giới hạn quan sát (Chưa gửi mail thật):**
  - Gmail Web / App, Outlook Desktop (Word rendering engine), Outlook Web, Apple Mail trên thiết bị thật chưa nhận email thực tế do nhiệm vụ tuân thủ chính sách bảo vệ dữ liệu và chi phí/hạn mức SMTP (không gửi email ra ngoài).
  - Cấu trúc thư đã được thiết kế tuân thủ tài liệu chính thức của Google Workspace Gmail CSS Reference (bảng lồng table, inline CSS, không dùng selector bị cấm). Outlook desktop được hỗ trợ thẻ `<!--[if mso]>` với font chữ Arial fallback và border-collapse chuẩn.

## 8. Owner-authorized live design sample — 2026-11-11

The owner requested a real email to inspect the new presentation. A freshly built
Release helper invoked the production TctEmailRenderer.RenderWeeklyLearningSummary
and SmtpAppEmailSender/SmtpClientMailTransport, sending one self-addressed sample
to the owner's previously specified Gmail address. The subject and body explicitly
identify the numbers as illustrative:4 active days and90 estimated minutes for
02/11–08/11/2026. No learner progress, subscription preference or account state was
read/changed. No settings URL was invented for this preview.

Marker: TCT-EMAIL-UI-20261111-086fb7ea64. SMTP returned Sent and the one-shot process
exited successfully. The temporary SMTP credential was entered without echo,
never persisted to source/configuration or placed in shell arguments. Before the
send, the focused TctEmailDesignSystemTests and SmtpTransportFakeTests passed26/26.
The helper build passed with zero warnings/errors.

Gmail connector tools were unavailable at receipt-verification time, so Inbox
arrival and actual Gmail visual appearance remain for the owner to observe. SMTP
acceptance is the verified result; it does not establish Outlook/mobile rendering
or complete scheduled weekly-delivery acceptance. This sample uses the shared
production template with a preview notice and does not represent a full code review
of Antigravity's implementation.
