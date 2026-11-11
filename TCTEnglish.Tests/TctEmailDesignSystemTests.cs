using System.Globalization;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Emails;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class TctEmailDesignSystemTests
{
    [Fact]
    public void PasswordReset_RendersClayCard_WithFallbackUrl_And15MinExpiry()
    {
        var link = "https://tctenglish.vn/Account/ResetPassword?token=test-reset-token-123";
        var html = TctEmailRenderer.RenderPasswordReset(link, 15);

        Assert.Contains("<!DOCTYPE html", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lang='vi'", html, StringComparison.Ordinal);
        Assert.Contains("Đặt lại mật khẩu", html, StringComparison.Ordinal);
        Assert.Contains("15 phút", html, StringComparison.Ordinal);
        Assert.Contains(TctEmailTokens.Primary, html, StringComparison.Ordinal);
        Assert.Contains(TctEmailTokens.ClayCanvas, html, StringComparison.Ordinal);
        Assert.Contains(TctEmailTokens.CardRadius, html, StringComparison.Ordinal);
        Assert.Contains(link, html, StringComparison.Ordinal);
        Assert.Contains("Không nhấp được nút trên?", html, StringComparison.Ordinal);
        Assert.DoesNotContain("mật khẩu của bạn là", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PasswordReset_HtmlEncodesMaliciousLink_DoesNotInjectAttributeOrScript()
    {
        var malicious = "https://example.com/reset?token=\"><script>alert('xss')</script>&safe=1";
        var html = TctEmailRenderer.RenderPasswordReset(malicious, 15);

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&quot;&gt;&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;&amp;safe=1", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EmailChange_RendersOldEmailPreservedNotice_And30MinExpiry()
    {
        var link = "https://tctenglish.vn/Account/ConfirmEmailChange?token=new-email-token-456";
        var html = TctEmailRenderer.RenderEmailChange(link, 30);

        Assert.Contains("Xác nhận địa chỉ email mới", html, StringComparison.Ordinal);
        Assert.Contains("30 phút", html, StringComparison.Ordinal);
        Assert.Contains("vẫn giữ hiệu lực", html, StringComparison.Ordinal);
        Assert.Contains(link, html, StringComparison.Ordinal);
        Assert.Contains("Không nhấp được nút trên?", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Proof_VerifyEmail_RendersExactOpaqueCode_And10MinExpiry()
    {
        var code = "6a4f9b2d8e1c5a7f3b0e9d2c4a8f1b5e7d9c2a4f8e0b1c3d5a7f9b2d8e1c3a5b";
        var html = TctEmailRenderer.RenderProof(AccountProofPurpose.VerifyEmail, code, 10);

        Assert.Contains("Xác minh địa chỉ email", html, StringComparison.Ordinal);
        Assert.Contains("10 phút", html, StringComparison.Ordinal);
        Assert.Contains(code, html, StringComparison.Ordinal);
        Assert.Contains("word-break: break-all", html, StringComparison.Ordinal);
        Assert.DoesNotContain("đã được xác minh", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Proof_Reauth_RendersSecurityIdentityCopy_AndWrapsLongCode()
    {
        var code = "e8c2a1b94d7f0e3a5c6b8d2f1e4a7c9b0d2e4f6a8c1e3b5d7f9a0c2e4b6d8f1a";
        var html = TctEmailRenderer.RenderProof(AccountProofPurpose.Reauth, code, 10);

        Assert.Contains("Mã xác thực bảo mật", html, StringComparison.Ordinal);
        Assert.Contains("xác thực lại danh tính", html, StringComparison.Ordinal);
        Assert.Contains(code, html, StringComparison.Ordinal);
    }

    [Fact]
    public void Proof_SetFirstPassword_RendersSocialLoginSetupCopy()
    {
        var code = "112233445566778899aabbccddeeff00112233445566778899aabbccddeeff00";
        var html = TctEmailRenderer.RenderProof(AccountProofPurpose.SetFirstPassword, code, 10);

        Assert.Contains("Thiết lập mật khẩu tài khoản", html, StringComparison.Ordinal);
        Assert.Contains("mạng xã hội", html, StringComparison.Ordinal);
        Assert.Contains(code, html, StringComparison.Ordinal);
    }

    [Fact]
    public void Proof_Deletion_RendersWarning_AndDoesNotClaimAccountDeleted()
    {
        var code = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";
        var html = TctEmailRenderer.RenderProof(AccountProofPurpose.Deletion, code, 10);

        Assert.Contains("Xác nhận yêu cầu xóa tài khoản", html, StringComparison.Ordinal);
        Assert.Contains("chưa bị xóa", html, StringComparison.Ordinal);
        Assert.Contains(TctEmailTokens.DangerSoft, html, StringComparison.Ordinal);
        Assert.Contains(code, html, StringComparison.Ordinal);
        Assert.DoesNotContain("tài khoản đã bị xóa thành công", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Proof_WeeklyUnsubscribe_WithUrl_RendersCtaButtonAndNotice()
    {
        var link = "https://tctenglish.vn/Account/Settings?unsubscribeToken=token123";
        var html = TctEmailRenderer.RenderProof(AccountProofPurpose.WeeklyUnsubscribe, "token123", 20160, link);

        Assert.Contains("Quản lý tùy chọn email tuần", html, StringComparison.Ordinal);
        Assert.Contains("không tự động", html, StringComparison.Ordinal);
        Assert.Contains(link, html, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountBlocked_PreservesUtcTimezone_AndEncodesMaliciousReason()
    {
        var lockDate = new DateTime(2026, 12, 31, 15, 30, 0, DateTimeKind.Utc);
        var reason = "Spam bot <b>alert</b> & \"injection\"";
        var html = TctEmailRenderer.RenderAccountBlocked(reason, lockDate);

        Assert.Contains("31/12/2026 15:30:00 (UTC)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>alert</b>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;alert&lt;/b&gt; &amp; &quot;injection&quot;", html, StringComparison.Ordinal);
        Assert.Contains(TctEmailTokens.Danger, html, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountBlocked_Permanent_ShowsPermanentStatus()
    {
        var html = TctEmailRenderer.RenderAccountBlocked("Vi phạm điều khoản nhiều lần", null);

        Assert.Contains("Vĩnh viễn", html, StringComparison.Ordinal);
        Assert.Contains("Tài khoản bị khóa", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountUnlocked_AutoUnlock_RendersAutoCopy_AndOmitsEmptyLoginUrl()
    {
        var html = TctEmailRenderer.RenderAccountUnlocked(isAutoUnlock: true, loginUrl: null);

        Assert.Contains("tự động (hết thời hạn khóa)", html, StringComparison.Ordinal);
        Assert.Contains(TctEmailTokens.SuccessText, html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountUnlocked_ManualUnlock_RendersManualCopy_AndIncludesValidLoginUrl()
    {
        var loginUrl = "https://tctenglish.vn/Account/Login";
        var html = TctEmailRenderer.RenderAccountUnlocked(isAutoUnlock: false, loginUrl: loginUrl);

        Assert.Contains("thủ công bởi quản trị viên", html, StringComparison.Ordinal);
        Assert.Contains("Đăng nhập ngay", html, StringComparison.Ordinal);
        Assert.Contains(loginUrl, html, StringComparison.Ordinal);
    }

    [Fact]
    public void SupportContact_EncodesAllInputs_PreservesLinebreaks_AndIncludesDiagnostics()
    {
        var name = "Nguyễn <Admin> An";
        var email = "test\"@example.com";
        var subject = "Hỗ trợ & báo lỗi <script>";
        var message = "Dòng 1: Cần trợ giúp.\r\nDòng 2: Phát âm không nghe được.\nLine 3: <b>bold</b>";
        var ip = "192.168.1.100";
        var agent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        var html = TctEmailRenderer.RenderSupportContact(name, email, subject, message, ip, agent);

        Assert.Contains("Nguyễn &lt;Admin&gt; An", html, StringComparison.Ordinal);
        Assert.Contains("test&quot;@example.com", html, StringComparison.Ordinal);
        Assert.Contains("Hỗ trợ &amp; báo lỗi &lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>bold</b>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;b&gt;bold&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.Contains("<br />", html, StringComparison.Ordinal);
        Assert.Contains("192.168.1.100", html, StringComparison.Ordinal);
        Assert.Contains("Mozilla/5.0", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WeeklyLearningSummary_RendersVietnamTimezone_StatCards_AndActiveState()
    {
        var start = new DateTime(2026, 11, 2);
        var end = new DateTime(2026, 11, 8);
        var settingsUrl = "https://tctenglish.vn/Account/Settings";

        var html = TctEmailRenderer.RenderWeeklyLearningSummary(start, end, activeDays: 5, estimatedMinutes: 135, settingsUrl);

        Assert.Contains("02/11 – 08/11 (giờ Việt Nam)", html, StringComparison.Ordinal);
        Assert.Contains("5", html, StringComparison.Ordinal);
        Assert.Contains("/ 7 ngày", html, StringComparison.Ordinal);
        Assert.Contains("135", html, StringComparison.Ordinal);
        Assert.Contains("phút", html, StringComparison.Ordinal);
        Assert.Contains(settingsUrl, html, StringComparison.Ordinal);
        Assert.DoesNotContain("XP", html, StringComparison.Ordinal);
        Assert.DoesNotContain("streak", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WeeklyLearningSummary_ZeroDays_RendersEncouragingEmptyState_WithoutInventedXp()
    {
        var start = new DateTime(2026, 11, 2);
        var end = new DateTime(2026, 11, 8);

        var html = TctEmailRenderer.RenderWeeklyLearningSummary(start, end, activeDays: 0, estimatedMinutes: 0, settingsUrl: null);

        Assert.Contains("0 <span style='font-size: 15px; font-weight: 500; color: #5b6578;'>/ 7 ngày</span>", html, StringComparison.Ordinal);
        Assert.Contains("chưa ghi nhận lượt học tập nào", html, StringComparison.Ordinal);
        Assert.DoesNotContain("XP", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FakeTransport_SendPasswordResetAsync_TransmitsValidClayHtml()
    {
        var transport = new FakeTransport();
        var sender = CreateSender(transport);
        var resetLink = "https://tctenglish.vn/Account/ResetPassword?token=reset-fake-token";

        await sender.SendPasswordResetAsync("learner@test.local", resetLink);

        Assert.Equal(1, transport.Attempts);
        Assert.True(transport.CompletedSuccessfully);
        Assert.Contains(TctEmailTokens.Primary, transport.LastBodyHtml, StringComparison.Ordinal);
        Assert.Contains("Đặt lại mật khẩu", transport.LastBodyHtml, StringComparison.Ordinal);
        Assert.Contains(resetLink, transport.LastBodyHtml, StringComparison.Ordinal);
        Assert.Contains("15 phút", transport.LastBodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FakeTransport_SendBlockedNotificationAsync_TransmitsStyledHtml()
    {
        var transport = new FakeTransport();
        var sender = CreateSender(transport);
        var expiry = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);

        await sender.SendBlockedNotificationAsync("learner@test.local", "Vi phạm bảo mật", expiry);

        Assert.Equal(1, transport.Attempts);
        Assert.True(transport.CompletedSuccessfully);
        Assert.Contains(TctEmailTokens.Danger, transport.LastBodyHtml, StringComparison.Ordinal);
        Assert.Contains("01/12/2026 10:00:00 (UTC)", transport.LastBodyHtml, StringComparison.Ordinal);
        Assert.Contains("Vi phạm bảo mật", transport.LastBodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FakeTransport_SendContactMessageAsync_TransmitsStyledHtml()
    {
        var transport = new FakeTransport();
        var sender = CreateSender(transport);

        var success = await sender.SendContactMessageAsync(
            "Minh Chau",
            "chau@example.local",
            "Góp ý tính năng",
            "Ứng dụng chạy rất mượt!\nCảm ơn đội ngũ.",
            "10.0.0.1",
            "Edge/130.0");

        Assert.True(success);
        Assert.Equal(1, transport.Attempts);
        Assert.Contains("Minh Chau", transport.LastBodyHtml, StringComparison.Ordinal);
        Assert.Contains("chau@example.local", transport.LastBodyHtml, StringComparison.Ordinal);
        Assert.Contains("Góp ý tính năng", transport.LastBodyHtml, StringComparison.Ordinal);
        Assert.Contains("<br />", transport.LastBodyHtml, StringComparison.Ordinal);
        Assert.Contains("10.0.0.1", transport.LastBodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void PlainTextRenderers_GenerateCleanAndConsistentContent()
    {
        var reset = TctEmailRenderer.RenderPlainTextPasswordReset("https://tctenglish.vn/reset", 15);
        Assert.Contains("Đặt lại mật khẩu", reset, StringComparison.Ordinal);
        Assert.Contains("https://tctenglish.vn/reset", reset, StringComparison.Ordinal);
        Assert.Contains("15 phút", reset, StringComparison.Ordinal);

        var proof = TctEmailRenderer.RenderPlainTextProof(AccountProofPurpose.Deletion, "del-code-123", 10);
        Assert.Contains("CHƯA bị xóa", proof, StringComparison.Ordinal);
        Assert.Contains("del-code-123", proof, StringComparison.Ordinal);

        var weekly = TctEmailRenderer.RenderPlainTextWeeklyLearningSummary(new DateTime(2026, 11, 2), new DateTime(2026, 11, 8), 4, 90, "https://tctenglish.vn/settings");
        Assert.Contains("4/7 ngày", weekly, StringComparison.Ordinal);
        Assert.Contains("90 phút", weekly, StringComparison.Ordinal);
        Assert.Contains("https://tctenglish.vn/settings", weekly, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportPreviews_GeneratesProductionRenderedFixtures_AndIndexHtml()
    {
        var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var targetDir = Path.Combine(rootDir, "docs", "design-system", "email-preview");
        Directory.CreateDirectory(targetDir);

        var fixtures = new Dictionary<string, (string Title, string Family, string Html, string PlainText, string Subject, string Ttl, string Invariant)>
        {
            ["password-reset"] = (
                "Đặt lại mật khẩu (Tiêu chuẩn)",
                "Bảo mật tài khoản",
                TctEmailRenderer.RenderPasswordReset("https://tctenglish.vn/Account/ResetPassword?token=4a9f2e7b1c3d5a8f0e2b4c6a8d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e", 15),
                TctEmailRenderer.RenderPlainTextPasswordReset("https://tctenglish.vn/Account/ResetPassword?token=4a9f2e7b1c3d5a8f0e2b4c6a8d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e", 15),
                "Đặt lại mật khẩu - TCT English",
                "15 phút",
                "Hiển thị nút CTA và liên kết phụ; không tiết lộ mật khẩu cũ hay khẳng định đã đặt lại."
            ),
            ["password-reset-long"] = (
                "Đặt lại mật khẩu (URL & Token dài)",
                "Bảo mật tài khoản",
                TctEmailRenderer.RenderPasswordReset("https://tctenglish.vn/Account/ResetPassword?token=abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789&source=security_automated_dispatch_system_boundary_check_2026", 15),
                TctEmailRenderer.RenderPlainTextPasswordReset("https://tctenglish.vn/Account/ResetPassword?token=abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789&source=security_automated_dispatch_system_boundary_check_2026", 15),
                "Đặt lại mật khẩu - TCT English",
                "15 phút",
                "Kiểm tra xuống dòng tự động cho URL và Token dài (word-break), không làm tràn layout 375px."
            ),
            ["verify-email"] = (
                "Xác minh email tài khoản",
                "Xác minh danh tính",
                TctEmailRenderer.RenderProof(AccountProofPurpose.VerifyEmail, "7f8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b", 10),
                TctEmailRenderer.RenderPlainTextProof(AccountProofPurpose.VerifyEmail, "7f8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b", 10),
                "Xác minh email tài khoản - TCT English",
                "10 phút",
                "Mã opaque 64 ký tự hex đầy đủ, không cắt ngắn; không tuyên bố đã xác minh xong."
            ),
            ["email-change"] = (
                "Xác nhận địa chỉ email mới",
                "Đổi thông tin đăng nhập",
                TctEmailRenderer.RenderEmailChange("https://tctenglish.vn/Account/ConfirmEmailChange?token=newmail_token_1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d", 30),
                TctEmailRenderer.RenderPlainTextEmailChange("https://tctenglish.vn/Account/ConfirmEmailChange?token=newmail_token_1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d", 30),
                "Xác nhận email mới - TCT English",
                "30 phút",
                "Khẳng định rõ email cũ vẫn giữ hiệu lực cho đến khi liên kết được bấm xác nhận."
            ),
            ["reauth-proof"] = (
                "Mã xác thực bảo mật (Reauth)",
                "Xác thực danh tính",
                TctEmailRenderer.RenderProof(AccountProofPurpose.Reauth, "3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d", 10),
                TctEmailRenderer.RenderPlainTextProof(AccountProofPurpose.Reauth, "3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d", 10),
                "Mã xác nhận tài khoản - TCT English",
                "10 phút",
                "Mục đích xác thực lại danh tính trước khi đổi cài đặt nhạy cảm."
            ),
            ["first-password-proof"] = (
                "Thiết lập mật khẩu đầu tiên",
                "Tài khoản Social Login",
                TctEmailRenderer.RenderProof(AccountProofPurpose.SetFirstPassword, "5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b", 10),
                TctEmailRenderer.RenderPlainTextProof(AccountProofPurpose.SetFirstPassword, "5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b", 10),
                "Mã xác nhận tài khoản - TCT English",
                "10 phút",
                "Dành riêng cho tài khoản Google/Facebook đăng ký chưa có mật khẩu cục bộ."
            ),
            ["deletion-proof"] = (
                "Xác nhận yêu cầu xóa tài khoản",
                "Thao tác nguy hiểm",
                TctEmailRenderer.RenderProof(AccountProofPurpose.Deletion, "ff00ee11dd22cc33bb44aa5599668877ff00ee11dd22cc33bb44aa5599668877", 10),
                TctEmailRenderer.RenderPlainTextProof(AccountProofPurpose.Deletion, "ff00ee11dd22cc33bb44aa5599668877ff00ee11dd22cc33bb44aa5599668877", 10),
                "Mã xác nhận xóa tài khoản - TCT English",
                "10 phút",
                "Cảnh báo rõ tài khoản CHƯA bị xóa; chỉ thực hiện khi nhập mã xác nhận."
            ),
            ["weekly-unsubscribe"] = (
                "Quản lý hủy đăng ký email tuần",
                "Tùy chọn thông báo",
                TctEmailRenderer.RenderProof(AccountProofPurpose.WeeklyUnsubscribe, "unsub_token_998877", 20160, "https://tctenglish.vn/Account/Settings?tab=notifications&unsubToken=unsub_token_998877"),
                TctEmailRenderer.RenderPlainTextProof(AccountProofPurpose.WeeklyUnsubscribe, "unsub_token_998877", 20160, "https://tctenglish.vn/Account/Settings?tab=notifications&unsubToken=unsub_token_998877"),
                "Quản lý tùy chọn email tuần - TCT English",
                "14 ngày",
                "Mở trang cài đặt để xác nhận, không tự động hủy đăng ký khi chỉ mở xem thư."
            ),
            ["account-blocked-finite"] = (
                "Thông báo khóa tài khoản (Có thời hạn)",
                "Trạng thái tài khoản",
                TctEmailRenderer.RenderAccountBlocked("Đăng nhập sai quá số lần quy định từ nhiều địa chỉ IP khả nghi.", new DateTime(2026, 12, 1, 8, 30, 0, DateTimeKind.Utc)),
                TctEmailRenderer.RenderPlainTextAccountBlocked("Đăng nhập sai quá số lần quy định từ nhiều địa chỉ IP khả nghi.", new DateTime(2026, 12, 1, 8, 30, 0, DateTimeKind.Utc)),
                "Tài khoản của bạn đã bị khóa - TCT English",
                "Thời hạn hiển thị chuẩn UTC",
                "Giữ nguyên định dạng giờ UTC và thông báo khiếu nại qua email hỗ trợ."
            ),
            ["account-blocked-permanent"] = (
                "Thông báo khóa tài khoản (Vĩnh viễn)",
                "Trạng thái tài khoản",
                TctEmailRenderer.RenderAccountBlocked("Vi phạm nghiêm trọng điều khoản sử dụng & chia sẻ trái phép tài liệu bản quyền.", null),
                TctEmailRenderer.RenderPlainTextAccountBlocked("Vi phạm nghiêm trọng điều khoản sử dụng & chia sẻ trái phép tài liệu bản quyền.", null),
                "Tài khoản của bạn đã bị khóa - TCT English",
                "Vĩnh viễn",
                "Hiển thị trạng thái Vĩnh viễn và lý do được mã hóa HTML an toàn."
            ),
            ["account-unlocked-auto"] = (
                "Tài khoản mở khóa (Tự động hết hạn khóa)",
                "Trạng thái tài khoản",
                TctEmailRenderer.RenderAccountUnlocked(isAutoUnlock: true, loginUrl: null),
                TctEmailRenderer.RenderPlainTextAccountUnlocked(isAutoUnlock: true, loginUrl: null),
                "Tài khoản của bạn đã được mở khóa - TCT English",
                "Không áp dụng",
                "Thông báo mở khóa tự động; không bịa liên kết đăng nhập giả định."
            ),
            ["account-unlocked-manual"] = (
                "Tài khoản mở khóa (Quản trị viên)",
                "Trạng thái tài khoản",
                TctEmailRenderer.RenderAccountUnlocked(isAutoUnlock: false, loginUrl: "https://tctenglish.vn/Account/Login"),
                TctEmailRenderer.RenderPlainTextAccountUnlocked(isAutoUnlock: false, loginUrl: "https://tctenglish.vn/Account/Login"),
                "Tài khoản của bạn đã được mở khóa - TCT English",
                "Không áp dụng",
                "Mở khóa thủ công bởi quản trị viên kèm nút đăng nhập xác thực."
            ),
            ["support-contact"] = (
                "Tin nhắn liên hệ hỗ trợ (Tiêu chuẩn)",
                "Hỗ trợ người dùng",
                TctEmailRenderer.RenderSupportContact("Lê Hoàng Nam", "hoangnam@example.vn", "Hỏi về gói học từ vựng IELTS", "Xin chào ban quản trị,\n\nTôi muốn hỏi gói từ vựng IELTS có bài tập Speaking đi kèm không?\n\nXin cảm ơn!", "113.161.42.19", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0"),
                TctEmailRenderer.RenderPlainTextSupportContact("Lê Hoàng Nam", "hoangnam@example.vn", "Hỏi về gói học từ vựng IELTS", "Xin chào ban quản trị,\n\nTôi muốn hỏi gói từ vựng IELTS có bài tập Speaking đi kèm không?\n\nXin cảm ơn!", "113.161.42.19", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0"),
                "[Liên hệ] Hỏi về gói học từ vựng IELTS",
                "Không áp dụng",
                "Giữ ngắt dòng tin nhắn; hiển thị IP và trình duyệt của người gửi."
            ),
            ["support-contact-edge"] = (
                "Tin nhắn liên hệ hỗ trợ (Ký tự đặc biệt & XSS)",
                "Hỗ trợ người dùng",
                TctEmailRenderer.RenderSupportContact("<img src=x onerror=alert(1)> Hacker & \"Dev\"", "hacker\"@inject.test", "Cảnh báo bảo mật <script>alert('pwn')</script>", "Thử nghiệm injection:\r\n<script>window.location='http://evil.com'</script>\n<style>body{display:none;}</style>\n&quot;&lt;&gt; Unicode: 🔥 Tiếng Việt có dấu đầy đủ và chuẩn xác.", "2001:0db8:85a3:0000:0000:8a2e:0370:7334", "curl/7.88.1 (<script>)"),
                TctEmailRenderer.RenderPlainTextSupportContact("<img src=x onerror=alert(1)> Hacker & \"Dev\"", "hacker\"@inject.test", "Cảnh báo bảo mật <script>alert('pwn')</script>", "Thử nghiệm injection:\r\n<script>window.location='http://evil.com'</script>\n<style>body{display:none;}</style>\n&quot;&lt;&gt; Unicode: 🔥 Tiếng Việt có dấu đầy đủ và chuẩn xác.", "2001:0db8:85a3:0000:0000:8a2e:0370:7334", "curl/7.88.1 (<script>)"),
                "[Liên hệ] Cảnh báo bảo mật <script>alert('pwn')</script>",
                "Không áp dụng",
                "Mọi thẻ HTML và ký tự đặc biệt đều được escape an toàn; không phá vỡ email."
            ),
            ["weekly-summary-active"] = (
                "Tổng kết tuần học (Có hoạt động tích cực)",
                "Học tập hàng tuần",
                TctEmailRenderer.RenderWeeklyLearningSummary(new DateTime(2026, 11, 2), new DateTime(2026, 11, 8), activeDays: 5, estimatedMinutes: 142, settingsUrl: "https://tctenglish.vn/Account/Settings"),
                TctEmailRenderer.RenderPlainTextWeeklyLearningSummary(new DateTime(2026, 11, 2), new DateTime(2026, 11, 8), activeDays: 5, estimatedMinutes: 142, settingsUrl: "https://tctenglish.vn/Account/Settings"),
                "Tổng kết tuần học TCT English",
                "Gửi định kỳ thứ Hai hàng tuần",
                "Tính theo giờ Việt Nam; số ngày và thời lượng chính xác; có liên kết cài đặt."
            ),
            ["weekly-summary-empty"] = (
                "Tổng kết tuần học (Không có hoạt động)",
                "Học tập hàng tuần",
                TctEmailRenderer.RenderWeeklyLearningSummary(new DateTime(2026, 11, 2), new DateTime(2026, 11, 8), activeDays: 0, estimatedMinutes: 0, settingsUrl: "https://tctenglish.vn/Account/Settings"),
                TctEmailRenderer.RenderPlainTextWeeklyLearningSummary(new DateTime(2026, 11, 2), new DateTime(2026, 11, 8), activeDays: 0, estimatedMinutes: 0, settingsUrl: "https://tctenglish.vn/Account/Settings"),
                "Tổng kết tuần học TCT English",
                "Gửi định kỳ thứ Hai hàng tuần",
                "Trạng thái yên tĩnh, lời nhắn động viên; không bịa đặt số liệu hay XP giả."
            )
        };

        foreach (var (key, data) in fixtures)
        {
            var htmlPath = Path.Combine(targetDir, $"{key}.html");
            var txtPath = Path.Combine(targetDir, $"{key}.txt");
            File.WriteAllText(htmlPath, data.Html, System.Text.Encoding.UTF8);
            File.WriteAllText(txtPath, data.PlainText, System.Text.Encoding.UTF8);
            Assert.True(File.Exists(htmlPath));
            Assert.True(File.Exists(txtPath));
        }

        // Generate the gallery index.html
        var galleryHtml = BuildGalleryIndexHtml(fixtures);
        var indexPath = Path.Combine(targetDir, "index.html");
        File.WriteAllText(indexPath, galleryHtml, System.Text.Encoding.UTF8);
        Assert.True(File.Exists(indexPath));
    }

    private static string BuildGalleryIndexHtml(Dictionary<string, (string Title, string Family, string Html, string PlainText, string Subject, string Ttl, string Invariant)> fixtures)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>TCT English — Email Design System Gallery</title>
    <link rel=""preconnect"" href=""https://fonts.googleapis.com"">
    <link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;600&display=swap"" rel=""stylesheet"">
    <style>
        :root {
            --bg-canvas: #f0f3ff;
            --surface: #ffffff;
            --surface-muted: #f8fafc;
            --primary: #4255ff;
            --primary-hover: #2f3fe0;
            --primary-soft: #eef2ff;
            --accent-yellow: #ffcd1f;
            --text: #111c2d;
            --text-secondary: #444656;
            --text-muted: #5b6578;
            --border: #e2e8f0;
            --border-control: #7c869b;
            --card-border: #dbe4ff;
            --card-shadow: 0 10px 25px rgba(66, 85, 255, 0.08), 0 4px 10px rgba(0, 0, 0, 0.03);
            --font: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
            --mono: 'JetBrains Mono', ui-monospace, Menlo, Consolas, monospace;
        }

        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: var(--font); background-color: var(--bg-canvas); color: var(--text); height: 100vh; display: flex; flex-direction: column; overflow: hidden; }

        /* Top Header */
        .header {
            height: 64px; background: var(--surface); border-bottom: 1px solid var(--border);
            display: flex; align-items: center; justify-content: space-between; padding: 0 24px;
            box-shadow: 0 2px 8px rgba(66, 85, 255, 0.05); flex-shrink: 0; z-index: 10;
        }
        .header-brand { display: flex; align-items: center; gap: 12px; }
        .brand-badge {
            background: var(--primary-soft); color: var(--primary); font-weight: 800;
            font-size: 15px; padding: 4px 10px; border-radius: 8px; border: 1px solid var(--card-border);
            letter-spacing: -0.3px;
        }
        .brand-title { font-size: 16px; font-weight: 700; color: var(--text); }
        .brand-subtitle { font-size: 13px; color: var(--text-muted); }

        .toolbar { display: flex; align-items: center; gap: 16px; }
        .btn-group { display: inline-flex; background: var(--bg-canvas); padding: 3px; border-radius: 10px; border: 1px solid var(--border); }
        .btn-toggle {
            background: transparent; border: none; padding: 6px 14px; font-size: 13px; font-weight: 600;
            color: var(--text-secondary); border-radius: 7px; cursor: pointer; transition: all 0.15s ease;
        }
        .btn-toggle:hover { color: var(--text); }
        .btn-toggle.active { background: var(--surface); color: var(--primary); box-shadow: 0 2px 6px rgba(0, 0, 0, 0.06); }

        /* Workspace Grid */
        .workspace { display: flex; flex: 1; overflow: hidden; }

        /* Sidebar Navigation */
        .sidebar {
            width: 320px; background: var(--surface); border-right: 1px solid var(--border);
            overflow-y: auto; padding: 16px 12px; flex-shrink: 0; display: flex; flex-direction: column; gap: 16px;
        }
        .nav-group-title {
            font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.8px;
            color: var(--text-muted); padding: 6px 12px 4px 12px;
        }
        .nav-item {
            display: flex; flex-direction: column; gap: 3px; padding: 10px 14px; border-radius: 10px;
            color: var(--text); text-decoration: none; font-size: 14px; font-weight: 500;
            transition: all 0.15s ease; cursor: pointer; border: 1px solid transparent;
        }
        .nav-item:hover { background: var(--bg-canvas); }
        .nav-item.active {
            background: var(--primary-soft); color: var(--primary); font-weight: 600;
            border-color: var(--card-border);
        }
        .nav-item-meta { font-size: 11px; color: var(--text-muted); font-weight: 400; }
        .nav-item.active .nav-item-meta { color: var(--primary); opacity: 0.85; }

        /* Preview Canvas */
        .canvas-area {
            flex: 1; display: flex; flex-direction: column; background: #e5e9f5;
            overflow-y: auto; align-items: center; padding: 24px 16px; position: relative;
        }

        .preview-wrapper {
            transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1);
            width: 600px; max-width: 100%; display: flex; flex-direction: column; gap: 16px;
        }
        .preview-wrapper.mode-desktop { width: 600px; }
        .preview-wrapper.mode-mobile { width: 375px; }
        .preview-wrapper.mode-fluid { width: 100%; max-width: 800px; }

        .preview-frame-container {
            background: var(--surface); border-radius: 16px; overflow: hidden;
            box-shadow: 0 12px 36px rgba(17, 28, 45, 0.12); border: 1px solid rgba(66, 85, 255, 0.1);
        }
        .iframe-window { width: 100%; height: 740px; border: none; display: block; background: #f0f3ff; }

        /* Raw Text / Code View */
        .text-view-container {
            display: none; background: #0f172a; color: #f8fafc; border-radius: 16px;
            padding: 20px; font-family: var(--mono); font-size: 13px; line-height: 1.6;
            overflow: auto; max-height: 740px; white-space: pre-wrap; word-break: break-word;
            border: 1px solid #334155; box-shadow: 0 12px 36px rgba(17, 28, 45, 0.15);
        }

        /* Metadata Card */
        .meta-card {
            background: var(--surface); border-radius: 14px; border: 1px solid var(--border);
            padding: 16px 20px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.04);
            display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px;
        }
        .meta-col { display: flex; flex-direction: column; gap: 4px; }
        .meta-label { font-size: 11px; text-transform: uppercase; font-weight: 700; color: var(--text-muted); letter-spacing: 0.5px; }
        .meta-val { font-size: 13px; font-weight: 600; color: var(--text); word-break: break-word; }

        /* Dark mode simulator */
        .simulate-dark .iframe-window { filter: invert(0.92) hue-rotate(180deg); }
    </style>
</head>
<body>

    <header class=""header"">
        <div class=""header-brand"">
            <span class=""brand-badge"">TCT</span>
            <div>
                <div class=""brand-title"">Email Design System Gallery</div>
                <div class=""brand-subtitle"">Hệ thống email chuẩn Claymorphism cho TCT English</div>
            </div>
        </div>

        <div class=""toolbar"">
            <!-- Viewport toggle -->
            <div class=""btn-group"">
                <button type=""button"" class=""btn-toggle active"" onclick=""setViewport('desktop', this)"">Desktop (600px)</button>
                <button type=""button"" class=""btn-toggle"" onclick=""setViewport('mobile', this)"">Mobile (375px)</button>
                <button type=""button"" class=""btn-toggle"" onclick=""setViewport('fluid', this)"">Toàn màn hình</button>
            </div>

            <!-- View mode toggle -->
            <div class=""btn-group"">
                <button type=""button"" class=""btn-toggle active"" onclick=""setViewMode('preview', this)"">Giao diện Email</button>
                <button type=""button"" class=""btn-toggle"" onclick=""setViewMode('plaintext', this)"">Bản Text thuần</button>
                <button type=""button"" class=""btn-toggle"" onclick=""setViewMode('source', this)"">Mã nguồn HTML</button>
            </div>

            <!-- Dark mode simulation toggle -->
            <div class=""btn-group"">
                <button type=""button"" class=""btn-toggle"" id=""darkModeToggle"" onclick=""toggleDarkMode(this)"">Mô phỏng Dark Mode</button>
            </div>
        </div>
    </header>

    <div class=""workspace"">
        <aside class=""sidebar"" id=""navSidebar"">
");

        // Group fixtures
        var groups = new (string GroupName, string[] Keys)[]
        {
            ("Bảo mật & Xác thực tài khoản", new[] { "password-reset", "password-reset-long", "verify-email", "email-change" }),
            ("Mã xác thực nhạy cảm", new[] { "reauth-proof", "first-password-proof", "deletion-proof", "weekly-unsubscribe" }),
            ("Trạng thái tài khoản", new[] { "account-blocked-finite", "account-blocked-permanent", "account-unlocked-auto", "account-unlocked-manual" }),
            ("Hỗ trợ & Học tập hàng tuần", new[] { "support-contact", "support-contact-edge", "weekly-summary-active", "weekly-summary-empty" })
        };

        var first = true;
        foreach (var (groupName, keys) in groups)
        {
            sb.AppendLine($"            <div class=\"nav-group-title\">{groupName}</div>");
            foreach (var key in keys)
            {
                if (!fixtures.TryGetValue(key, out var f)) continue;
                var activeClass = first ? " active" : string.Empty;
                sb.AppendLine($@"            <div class=""nav-item{activeClass}"" data-key=""{key}"" onclick=""selectFixture('{key}', this)"">
                <span>{f.Title}</span>
                <span class=""nav-item-meta"">{f.Family} &bull; {f.Ttl}</span>
            </div>");
                first = false;
            }
        }

        sb.AppendLine(@"        </aside>

        <main class=""canvas-area"" id=""canvasArea"">
            <div class=""preview-wrapper mode-desktop"" id=""previewWrapper"">

                <!-- Metadata Card -->
                <div class=""meta-card"">
                    <div class=""meta-col"">
                        <span class=""meta-label"">Tiêu đề email (Subject)</span>
                        <span class=""meta-val"" id=""metaSubject"">-</span>
                    </div>
                    <div class=""meta-col"">
                        <span class=""meta-label"">Thời gian hiệu lực (TTL)</span>
                        <span class=""meta-val"" id=""metaTtl"">-</span>
                    </div>
                    <div class=""meta-col"">
                        <span class=""meta-label"">Phân loại & Nguyên tắc bảo mật</span>
                        <span class=""meta-val"" id=""metaInvariant"">-</span>
                    </div>
                </div>

                <!-- Preview Frame -->
                <div class=""preview-frame-container"" id=""frameContainer"">
                    <iframe class=""iframe-window"" id=""previewIframe"" src=""password-reset.html"" title=""Email Preview""></iframe>
                </div>

                <!-- Text / Source Container -->
                <div class=""text-view-container"" id=""textViewContainer""></div>

            </div>
        </main>
    </div>

    <script>
        const fixtures = {");

        var fEntries = new List<string>();
        foreach (var (k, v) in fixtures)
        {
            var escSubject = v.Subject.Replace("\"", "\\\"");
            var escTtl = v.Ttl.Replace("\"", "\\\"");
            var escInv = v.Invariant.Replace("\"", "\\\"");
            fEntries.Add($"\"{k}\": {{ title: \"{v.Title}\", subject: \"{escSubject}\", ttl: \"{escTtl}\", invariant: \"{escInv}\", htmlFile: \"{k}.html\", txtFile: \"{k}.txt\" }}");
        }
        sb.AppendLine(string.Join(",\n            ", fEntries));

        sb.AppendLine(@"        };

        let currentKey = 'password-reset';
        let currentMode = 'preview';

        function updateMeta(key) {
            const f = fixtures[key];
            if (!f) return;
            document.getElementById('metaSubject').textContent = f.subject;
            document.getElementById('metaTtl').textContent = f.ttl;
            document.getElementById('metaInvariant').textContent = f.invariant;
        }

        function selectFixture(key, element) {
            currentKey = key;
            document.querySelectorAll('.nav-item').forEach(el => el.classList.remove('active'));
            if (element) element.classList.add('active');
            updateMeta(key);
            loadContent();
        }

        function setViewport(mode, btn) {
            const wrap = document.getElementById('previewWrapper');
            wrap.className = 'preview-wrapper mode-' + mode;
            btn.parentElement.querySelectorAll('.btn-toggle').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
        }

        function setViewMode(mode, btn) {
            currentMode = mode;
            btn.parentElement.querySelectorAll('.btn-toggle').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            loadContent();
        }

        function toggleDarkMode(btn) {
            const canvas = document.getElementById('canvasArea');
            canvas.classList.toggle('simulate-dark');
            btn.classList.toggle('active');
        }

        async function loadContent() {
            const f = fixtures[currentKey];
            const iframe = document.getElementById('previewIframe');
            const frameContainer = document.getElementById('frameContainer');
            const textContainer = document.getElementById('textViewContainer');

            if (currentMode === 'preview') {
                frameContainer.style.display = 'block';
                textContainer.style.display = 'none';
                iframe.src = f.htmlFile;
            } else if (currentMode === 'plaintext') {
                frameContainer.style.display = 'none';
                textContainer.style.display = 'block';
                try {
                    const resp = await fetch(f.txtFile);
                    textContainer.textContent = await resp.text();
                } catch (e) {
                    textContainer.textContent = 'Đang tải bản text thuần cho ' + f.title + '...';
                }
            } else if (currentMode === 'source') {
                frameContainer.style.display = 'none';
                textContainer.style.display = 'block';
                try {
                    const resp = await fetch(f.htmlFile);
                    textContainer.textContent = await resp.text();
                } catch (e) {
                    textContainer.textContent = 'Đang tải mã nguồn HTML...';
                }
            }
        }

        // Initialize with default
        updateMeta('password-reset');
    </script>
</body>
</html>");

        return sb.ToString();
    }

    private static SmtpAppEmailSender CreateSender(ISmtpMailTransport transport)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmtpSettings:Host"] = "smtp.test.local",
                ["SmtpSettings:Port"] = "587",
                ["SmtpSettings:SenderEmail"] = "noreply@tctenglish.vn",
                ["SmtpSettings:SenderName"] = "TCT English",
                ["SmtpSettings:Password"] = "test-secret",
                ["SmtpSettings:SupportEmail"] = "support@tctenglish.vn",
                ["SmtpSettings:TimeoutSeconds"] = "5"
            })
            .Build();

        return new SmtpAppEmailSender(config, transport, NullLogger<SmtpAppEmailSender>.Instance);
    }

    private sealed class FakeTransport : ISmtpMailTransport
    {
        public int Attempts { get; private set; }
        public bool CompletedSuccessfully { get; private set; }
        public string LastBodyHtml { get; private set; } = string.Empty;

        public Task SendAsync(string host, int port, string username, string password, MailMessage message, CancellationToken cancellationToken)
        {
            Attempts++;
            LastBodyHtml = message.Body ?? string.Empty;
            CompletedSuccessfully = true;
            return Task.CompletedTask;
        }
    }
}
