using System.Globalization;
using System.Net;
using System.Text;
using TCTEnglish.Models;

namespace TCTEnglish.Services.Emails;

public enum EmailStatusType
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger
}

/// <summary>
/// Pure, self-contained email renderer for TCT English.
/// Implements the Claymorphism visual direction using email-client-safe inline CSS,
/// presentation tables, and robust fallbacks. Free of EF Core, SMTP secrets, and HTTP request context.
/// </summary>
public static class TctEmailRenderer
{
    /// <summary>
    /// Encodes HTML special characters to prevent markup and attribute injection while preserving UTF-8 Vietnamese text.
    /// </summary>
    public static string Encode(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Renders the password reset email with primary CTA, fallback URL, 15-minute expiry, and unsolicited-request guidance.
    /// </summary>
    public static string RenderPasswordReset(string resetLink, int expiryMinutes = 15)
    {
        var safeLink = Encode(resetLink);
        var preheader = $"Yêu cầu đặt lại mật khẩu tài khoản TCT English (hiệu lực trong {expiryMinutes} phút).";
        var heading = "Đặt lại mật khẩu";

        var body = $@"
            <p style='margin: 0 0 16px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản <strong>TCT English</strong> của bạn.
            </p>
            <p style='margin: 0 0 20px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                Nhấp vào nút bên dưới để tạo mật khẩu mới. Liên kết này có hiệu lực trong <strong>{expiryMinutes} phút</strong> và chỉ sử dụng một lần.
            </p>

            <!-- Primary Action Button -->
            <table role='presentation' border='0' cellpadding='0' cellspacing='0' style='margin: 24px 0 20px 0;'>
                <tr>
                    <td align='center' bgcolor='{TctEmailTokens.Primary}' style='border-radius: {TctEmailTokens.ButtonRadius}; background-color: {TctEmailTokens.Primary}; box-shadow: {TctEmailTokens.ButtonShadow};'>
                        <a href='{safeLink}' target='_blank' rel='noopener noreferrer' style='display: inline-block; padding: 14px 32px; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; font-weight: 700; color: #ffffff; text-decoration: none; border-radius: {TctEmailTokens.ButtonRadius}; border: 1px solid {TctEmailTokens.Primary}; line-height: 1;'>
                            Đặt lại mật khẩu
                        </a>
                    </td>
                </tr>
            </table>

            <!-- Fallback URL -->
            <div style='margin: 20px 0 24px 0; padding: 14px 16px; background-color: {TctEmailTokens.SurfaceMuted}; border: 1px solid {TctEmailTokens.Border}; border-radius: {TctEmailTokens.BoxRadius}; font-family: {TctEmailTokens.FontFamily}; font-size: 13px; line-height: 1.5; color: {TctEmailTokens.TextMuted};'>
                <span style='font-weight: 600; color: {TctEmailTokens.Text};'>Không nhấp được nút trên?</span> Sao chép và dán liên kết sau vào trình duyệt:<br />
                <a href='{safeLink}' target='_blank' rel='noopener noreferrer' style='color: {TctEmailTokens.Primary}; word-break: break-all; overflow-wrap: anywhere; text-decoration: underline;'>{safeLink}</a>
            </div>

            <!-- Security Note -->
            <div style='padding: 12px 16px; background-color: {TctEmailTokens.WarningSoft}; border-left: 4px solid {TctEmailTokens.Warning}; border-radius: 4px 8px 8px 4px; font-family: {TctEmailTokens.FontFamily}; font-size: 13px; line-height: 1.5; color: {TctEmailTokens.WarningText};'>
                <strong>Lưu ý bảo mật:</strong> Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này. Mật khẩu hiện tại của bạn vẫn được giữ an toàn.
            </div>";

        return RenderShell(
            preheader: preheader,
            heading: heading,
            badgeText: "Bảo mật tài khoản",
            statusType: EmailStatusType.Neutral,
            bodyHtml: body);
    }

    /// <summary>
    /// Renders the new-email confirmation email with CTA, fallback URL, 30-minute expiry, and explanation that old login email remains active.
    /// </summary>
    public static string RenderEmailChange(string confirmLink, int expiryMinutes = 30)
    {
        var safeLink = Encode(confirmLink);
        var preheader = $"Xác nhận địa chỉ email mới cho tài khoản TCT English (hiệu lực trong {expiryMinutes} phút).";
        var heading = "Xác nhận địa chỉ email mới";

        var body = $@"
            <p style='margin: 0 0 16px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                Chúng tôi nhận được yêu cầu cập nhật địa chỉ email đăng nhập cho tài khoản <strong>TCT English</strong> sang hòm thư này.
            </p>
            <p style='margin: 0 0 20px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                Vui lòng nhấp vào nút bên dưới để hoàn tất việc đổi email. Liên kết có hiệu lực trong <strong>{expiryMinutes} phút</strong> và chỉ sử dụng một lần.
            </p>

            <!-- Primary Action Button -->
            <table role='presentation' border='0' cellpadding='0' cellspacing='0' style='margin: 24px 0 20px 0;'>
                <tr>
                    <td align='center' bgcolor='{TctEmailTokens.Primary}' style='border-radius: {TctEmailTokens.ButtonRadius}; background-color: {TctEmailTokens.Primary}; box-shadow: {TctEmailTokens.ButtonShadow};'>
                        <a href='{safeLink}' target='_blank' rel='noopener noreferrer' style='display: inline-block; padding: 14px 32px; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; font-weight: 700; color: #ffffff; text-decoration: none; border-radius: {TctEmailTokens.ButtonRadius}; border: 1px solid {TctEmailTokens.Primary}; line-height: 1;'>
                            Xác nhận email
                        </a>
                    </td>
                </tr>
            </table>

            <!-- Fallback URL -->
            <div style='margin: 20px 0 24px 0; padding: 14px 16px; background-color: {TctEmailTokens.SurfaceMuted}; border: 1px solid {TctEmailTokens.Border}; border-radius: {TctEmailTokens.BoxRadius}; font-family: {TctEmailTokens.FontFamily}; font-size: 13px; line-height: 1.5; color: {TctEmailTokens.TextMuted};'>
                <span style='font-weight: 600; color: {TctEmailTokens.Text};'>Không nhấp được nút trên?</span> Sao chép và dán liên kết sau vào trình duyệt:<br />
                <a href='{safeLink}' target='_blank' rel='noopener noreferrer' style='color: {TctEmailTokens.Primary}; word-break: break-all; overflow-wrap: anywhere; text-decoration: underline;'>{safeLink}</a>
            </div>

            <!-- Continuity & Security Note -->
            <div style='padding: 12px 16px; background-color: {TctEmailTokens.InfoSoft}; border-left: 4px solid {TctEmailTokens.Info}; border-radius: 4px 8px 8px 4px; font-family: {TctEmailTokens.FontFamily}; font-size: 13px; line-height: 1.5; color: {TctEmailTokens.InfoText};'>
                <strong>Lưu ý:</strong> Địa chỉ email đăng nhập hiện tại của bạn vẫn giữ hiệu lực cho đến khi bạn xác nhận thành công liên kết này. Nếu bạn không yêu cầu thay đổi, vui lòng bỏ qua email.
            </div>";

        return RenderShell(
            preheader: preheader,
            heading: heading,
            badgeText: "Đổi email đăng nhập",
            statusType: EmailStatusType.Info,
            bodyHtml: body);
    }

    /// <summary>
    /// Renders account security challenge proof emails (VerifyEmail, Reauth, SetFirstPassword, Deletion, WeeklyUnsubscribe).
    /// Preserves exact opaque code display (supporting up to 64 hex characters) with clean wrapping and no truncation.
    /// </summary>
    public static string RenderProof(string purpose, string token, int expiryMinutes = 10, string? confirmUrl = null)
    {
        var safeToken = Encode(token);

        string heading;
        string badgeText;
        EmailStatusType statusType;
        string instructions;
        string securityAdvice;
        string? ctaButtonHtml = null;
        string? fallbackLinkHtml = null;

        switch (purpose)
        {
            case AccountProofPurpose.VerifyEmail:
                heading = "Xác minh địa chỉ email";
                badgeText = "Xác minh tài khoản";
                statusType = EmailStatusType.Info;
                instructions = "Bạn đang thực hiện xác minh địa chỉ email cho tài khoản <strong>TCT English</strong> của mình.";
                securityAdvice = "Nhập mã này vào biểu mẫu xác minh trên trang web. Nếu bạn không yêu cầu xác minh email, vui lòng bỏ qua thư này.";
                break;

            case AccountProofPurpose.Reauth:
                heading = "Mã xác thực bảo mật";
                badgeText = "Xác thực danh tính";
                statusType = EmailStatusType.Neutral;
                instructions = "Bạn đang thực hiện một thao tác yêu cầu xác thực lại danh tính trên tài khoản <strong>TCT English</strong>.";
                securityAdvice = "Tuyệt đối không chia sẻ mã này với bất kỳ ai. Đội ngũ TCT English không bao giờ yêu cầu bạn cung cấp mã xác nhận qua điện thoại hoặc tin nhắn.";
                break;

            case AccountProofPurpose.SetFirstPassword:
                heading = "Thiết lập mật khẩu tài khoản";
                badgeText = "Cài đặt mật khẩu";
                statusType = EmailStatusType.Info;
                instructions = "Tài khoản của bạn đăng nhập qua mạng xã hội và đang yêu cầu thiết lập mật khẩu cục bộ đầu tiên trên <strong>TCT English</strong>.";
                securityAdvice = "Sử dụng mã bên dưới để hoàn tất việc tạo mật khẩu. Nếu bạn không bắt đầu yêu cầu này, hãy đăng nhập và kiểm tra lại lịch sử bảo mật của bạn.";
                break;

            case AccountProofPurpose.Deletion:
                heading = "Xác nhận yêu cầu xóa tài khoản";
                badgeText = "Thao tác nhạy cảm";
                statusType = EmailStatusType.Danger;
                instructions = "Hệ thống nhận được yêu cầu <strong>xóa vĩnh viễn</strong> tài khoản TCT English của bạn. Mọi dữ liệu học tập, tiến độ và huy hiệu sẽ bị xóa khi hoàn tất.";
                securityAdvice = "<strong style='color: " + TctEmailTokens.DangerText + ";'>Cảnh báo:</strong> Tài khoản của bạn <strong>chưa bị xóa</strong>. Quá trình xóa chỉ thực hiện sau khi bạn nhập mã này vào trang xác nhận.";
                break;

            case AccountProofPurpose.WeeklyUnsubscribe:
                heading = "Quản lý tùy chọn email tuần";
                badgeText = "Tùy chọn thông báo";
                statusType = EmailStatusType.Neutral;
                instructions = "Bạn đã yêu cầu quản lý hoặc tắt email tổng kết tuần của <strong>TCT English</strong>.";
                securityAdvice = "Nhấp liên kết để mở trang cài đặt. Thao tác này không tự động tắt email cho đến khi bạn xác nhận trên hệ thống.";
                if (!string.IsNullOrWhiteSpace(confirmUrl))
                {
                    var safeConfirmUrl = Encode(confirmUrl);
                    ctaButtonHtml = $@"
                        <table role='presentation' border='0' cellpadding='0' cellspacing='0' style='margin: 24px 0 20px 0;'>
                            <tr>
                                <td align='center' bgcolor='{TctEmailTokens.Primary}' style='border-radius: {TctEmailTokens.ButtonRadius}; background-color: {TctEmailTokens.Primary}; box-shadow: {TctEmailTokens.ButtonShadow};'>
                                    <a href='{safeConfirmUrl}' target='_blank' rel='noopener noreferrer' style='display: inline-block; padding: 14px 32px; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; font-weight: 700; color: #ffffff; text-decoration: none; border-radius: {TctEmailTokens.ButtonRadius}; border: 1px solid {TctEmailTokens.Primary}; line-height: 1;'>
                                        Quản lý email tuần
                                    </a>
                                </td>
                            </tr>
                        </table>";
                    fallbackLinkHtml = $@"
                        <div style='margin: 20px 0 24px 0; padding: 14px 16px; background-color: {TctEmailTokens.SurfaceMuted}; border: 1px solid {TctEmailTokens.Border}; border-radius: {TctEmailTokens.BoxRadius}; font-family: {TctEmailTokens.FontFamily}; font-size: 13px; line-height: 1.5; color: {TctEmailTokens.TextMuted};'>
                            <span style='font-weight: 600; color: {TctEmailTokens.Text};'>Không nhấp được nút trên?</span> Mở liên kết sau:<br />
                            <a href='{safeConfirmUrl}' target='_blank' rel='noopener noreferrer' style='color: {TctEmailTokens.Primary}; word-break: break-all; overflow-wrap: anywhere; text-decoration: underline;'>{safeConfirmUrl}</a>
                        </div>";
                }
                break;

            default:
                heading = "Mã xác nhận tài khoản";
                badgeText = "Xác nhận";
                statusType = EmailStatusType.Neutral;
                instructions = "Bạn đang thực hiện thao tác cần mã xác nhận trên tài khoản <strong>TCT English</strong>.";
                securityAdvice = "Mã chỉ có giá trị trong thời gian giới hạn và sử dụng một lần duy nhất.";
                break;
        }

        var preheader = $"{heading} trên TCT English: mã bảo mật chỉ dùng một lần (hiệu lực {expiryMinutes} phút).";

        var codeBlockHtml = $@"
            <!-- Opaque Code Presentation Box -->
            <div style='margin: 24px 0; padding: 18px 20px; background-color: {TctEmailTokens.ClayCanvas}; border: 1px solid {TctEmailTokens.CardBorder}; border-radius: {TctEmailTokens.BoxRadius}; text-align: center; box-shadow: {TctEmailTokens.InsetBoxShadow};'>
                <div style='font-family: {TctEmailTokens.FontFamily}; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.5px; color: {TctEmailTokens.Primary}; margin-bottom: 8px;'>
                    Mã xác nhận bảo mật
                </div>
                <div style='font-family: {TctEmailTokens.MonoFontFamily}; font-size: 20px; font-weight: 700; color: {TctEmailTokens.Text}; letter-spacing: 1px; word-break: break-all; overflow-wrap: anywhere; line-height: 1.4; user-select: all;'>
                    {safeToken}
                </div>
                <div style='font-family: {TctEmailTokens.FontFamily}; font-size: 12px; color: {TctEmailTokens.TextMuted}; margin-top: 8px;'>
                    Hiệu lực trong <strong>{expiryMinutes} phút</strong> &bull; Chỉ dùng một lần
                </div>
            </div>";

        var body = $@"
            <p style='margin: 0 0 16px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                {instructions}
            </p>
            {ctaButtonHtml}
            {fallbackLinkHtml}
            {(!string.IsNullOrEmpty(ctaButtonHtml) ? string.Empty : codeBlockHtml)}
            <div style='padding: 12px 16px; background-color: {(statusType == EmailStatusType.Danger ? TctEmailTokens.DangerSoft : TctEmailTokens.SurfaceMuted)}; border-left: 4px solid {(statusType == EmailStatusType.Danger ? TctEmailTokens.Danger : TctEmailTokens.Primary)}; border-radius: 4px 8px 8px 4px; font-family: {TctEmailTokens.FontFamily}; font-size: 13px; line-height: 1.5; color: {(statusType == EmailStatusType.Danger ? TctEmailTokens.DangerText : TctEmailTokens.TextSecondary)};'>
                {securityAdvice}
            </div>";

        return RenderShell(
            preheader: preheader,
            heading: heading,
            badgeText: badgeText,
            statusType: statusType,
            bodyHtml: body);
    }

    /// <summary>
    /// Renders account blocked notification with reason, finite or permanent lock expiry, and appeal instructions.
    /// Preserves actual timezone/expiry wording (e.g. UTC).
    /// </summary>
    public static string RenderAccountBlocked(string? reason, DateTime? lockExpiry)
    {
        var expiryText = lockExpiry.HasValue && lockExpiry.Value < DateTime.MaxValue
            ? lockExpiry.Value.ToString("dd/MM/yyyy HH:mm:ss (UTC)", CultureInfo.InvariantCulture)
            : "Vĩnh viễn";
        var safeReason = Encode(string.IsNullOrWhiteSpace(reason) ? "Không có lý do cụ thể." : reason.Trim());
        var preheader = "Thông báo: Tài khoản TCT English của bạn đã bị khóa bởi quản trị viên.";
        var heading = "Tài khoản của bạn đã bị khóa";

        var body = $@"
            <p style='margin: 0 0 16px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                Tài khoản của bạn trên <strong>TCT English</strong> đã bị tạm khóa bởi quản trị viên hệ thống.
            </p>

            <!-- Status Details Card -->
            <div style='margin: 20px 0 24px 0; padding: 18px 20px; background-color: {TctEmailTokens.DangerSoft}; border: 1px solid {TctEmailTokens.DangerBorder}; border-radius: {TctEmailTokens.BoxRadius}; font-family: {TctEmailTokens.FontFamily};'>
                <table role='presentation' width='100%' cellpadding='0' cellspacing='0' border='0'>
                    <tr>
                        <td style='padding: 4px 0; font-size: 14px; font-weight: 600; color: {TctEmailTokens.DangerText}; width: 140px; vertical-align: top;'>
                            Lý do:
                        </td>
                        <td style='padding: 4px 0; font-size: 14px; color: {TctEmailTokens.Text}; word-break: break-word;'>
                            {safeReason}
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0 4px 0; font-size: 14px; font-weight: 600; color: {TctEmailTokens.DangerText}; width: 140px; vertical-align: top;'>
                            Thời gian mở khóa:
                        </td>
                        <td style='padding: 8px 0 4px 0; font-size: 14px; font-weight: 700; color: {TctEmailTokens.Text};'>
                            {expiryText}
                        </td>
                    </tr>
                </table>
            </div>

            <!-- Appeal Guidance -->
            <div style='padding: 12px 16px; background-color: {TctEmailTokens.SurfaceMuted}; border-left: 4px solid {TctEmailTokens.BorderControl}; border-radius: 4px 8px 8px 4px; font-family: {TctEmailTokens.FontFamily}; font-size: 13px; line-height: 1.5; color: {TctEmailTokens.TextMuted};'>
                Nếu bạn cho rằng quyết định này là nhầm lẫn hoặc cần hỗ trợ thêm, vui lòng liên hệ quản trị viên qua hòm thư hỗ trợ chính thức của TCT English.
            </div>";

        return RenderShell(
            preheader: preheader,
            heading: heading,
            badgeText: "Tài khoản bị khóa",
            statusType: EmailStatusType.Danger,
            bodyHtml: body);
    }

    /// <summary>
    /// Renders account unlocked notification with automatic/manual variant and calm presentation.
    /// Includes login link only if a valid authoritative URL is provided.
    /// </summary>
    public static string RenderAccountUnlocked(bool isAutoUnlock, string? loginUrl = null)
    {
        var unlockType = isAutoUnlock ? "tự động (hết thời hạn khóa)" : "thủ công bởi quản trị viên";
        var preheader = "Thông báo: Tài khoản TCT English của bạn đã được mở khóa.";
        var heading = "Tài khoản đã được mở khóa";

        var loginActionHtml = string.Empty;
        if (!string.IsNullOrWhiteSpace(loginUrl))
        {
            var safeLoginUrl = Encode(loginUrl);
            loginActionHtml = $@"
                <table role='presentation' border='0' cellpadding='0' cellspacing='0' style='margin: 24px 0 16px 0;'>
                    <tr>
                        <td align='center' bgcolor='{TctEmailTokens.Primary}' style='border-radius: {TctEmailTokens.ButtonRadius}; background-color: {TctEmailTokens.Primary}; box-shadow: {TctEmailTokens.ButtonShadow};'>
                            <a href='{safeLoginUrl}' target='_blank' rel='noopener noreferrer' style='display: inline-block; padding: 14px 32px; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; font-weight: 700; color: #ffffff; text-decoration: none; border-radius: {TctEmailTokens.ButtonRadius}; border: 1px solid {TctEmailTokens.Primary}; line-height: 1;'>
                                Đăng nhập ngay
                            </a>
                        </td>
                    </tr>
                </table>";
        }

        var body = $@"
            <p style='margin: 0 0 16px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                Tài khoản của bạn trên <strong>TCT English</strong> đã được mở khóa <strong>{unlockType}</strong>.
            </p>
            <p style='margin: 0 0 20px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                Bạn có thể đăng nhập lại và tiếp tục lộ trình học từ vựng và luyện tập phát âm.
            </p>
            {loginActionHtml}
            <div style='padding: 12px 16px; background-color: {TctEmailTokens.SuccessSoft}; border-left: 4px solid {TctEmailTokens.Success}; border-radius: 4px 8px 8px 4px; font-family: {TctEmailTokens.FontFamily}; font-size: 13px; line-height: 1.5; color: {TctEmailTokens.SuccessText};'>
                Chúc bạn có những giờ học hiệu quả cùng TCT English!
            </div>";

        return RenderShell(
            preheader: preheader,
            heading: heading,
            badgeText: "Đã mở khóa",
            statusType: EmailStatusType.Success,
            bodyHtml: body);
    }

    /// <summary>
    /// Renders contact form submission for support staff with safely encoded user input and line breaks preserved.
    /// </summary>
    public static string RenderSupportContact(
        string senderName,
        string senderEmail,
        string subject,
        string message,
        string? ipAddress,
        string? userAgent)
    {
        var safeName = Encode(senderName?.Trim() ?? string.Empty);
        var safeEmail = Encode(senderEmail?.Trim() ?? string.Empty);
        var safeSubject = Encode(subject?.Trim() ?? string.Empty);
        var safeMessage = Encode(message?.Trim() ?? string.Empty)
            .Replace("\r\n", "<br />")
            .Replace("\n", "<br />");
        var safeIp = Encode(ipAddress ?? "N/A");
        var safeAgent = Encode(userAgent ?? "N/A");

        var preheader = $"Tin nhắn liên hệ mới từ {safeName}: {safeSubject}";
        var heading = "Yêu cầu liên hệ mới";

        var body = $@"
            <p style='margin: 0 0 16px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                Hệ thống nhận được một tin nhắn liên hệ từ người dùng qua biểu mẫu hỗ trợ:
            </p>

            <!-- Sender Metadata -->
            <div style='margin: 0 0 18px 0; padding: 14px 18px; background-color: {TctEmailTokens.SurfaceMuted}; border: 1px solid {TctEmailTokens.Border}; border-radius: {TctEmailTokens.BoxRadius}; font-family: {TctEmailTokens.FontFamily};'>
                <table role='presentation' width='100%' cellpadding='0' cellspacing='0' border='0'>
                    <tr>
                        <td style='padding: 4px 0; font-size: 13px; font-weight: 600; color: {TctEmailTokens.TextMuted}; width: 100px;'>Người gửi:</td>
                        <td style='padding: 4px 0; font-size: 14px; font-weight: 600; color: {TctEmailTokens.Text};'>{safeName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 4px 0; font-size: 13px; font-weight: 600; color: {TctEmailTokens.TextMuted}; width: 100px;'>Email:</td>
                        <td style='padding: 4px 0; font-size: 14px; color: {TctEmailTokens.Primary};'><a href='mailto:{safeEmail}' style='color: {TctEmailTokens.Primary}; text-decoration: underline;'>{safeEmail}</a></td>
                    </tr>
                    <tr>
                        <td style='padding: 4px 0; font-size: 13px; font-weight: 600; color: {TctEmailTokens.TextMuted}; width: 100px;'>Chủ đề:</td>
                        <td style='padding: 4px 0; font-size: 14px; color: {TctEmailTokens.Text}; font-weight: 500;'>{safeSubject}</td>
                    </tr>
                </table>
            </div>

            <!-- Message Body -->
            <div style='margin: 0 0 20px 0;'>
                <div style='font-family: {TctEmailTokens.FontFamily}; font-size: 13px; font-weight: 600; color: {TctEmailTokens.TextMuted}; margin-bottom: 6px; text-transform: uppercase; letter-spacing: 0.5px;'>Nội dung tin nhắn:</div>
                <div style='padding: 18px 20px; background-color: {TctEmailTokens.ClayCanvas}; border: 1px solid {TctEmailTokens.CardBorder}; border-radius: {TctEmailTokens.BoxRadius}; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.65; color: {TctEmailTokens.Text}; box-shadow: {TctEmailTokens.InsetBoxShadow}; word-break: break-word;'>
                    {safeMessage}
                </div>
            </div>

            <!-- Diagnostics -->
            <div style='padding: 10px 14px; background-color: {TctEmailTokens.SurfaceMuted}; border: 1px solid {TctEmailTokens.Border}; border-radius: 8px; font-family: {TctEmailTokens.MonoFontFamily}; font-size: 11px; line-height: 1.5; color: {TctEmailTokens.TextMuted};'>
                <strong>Client IP:</strong> {safeIp}<br />
                <strong>User Agent:</strong> {safeAgent}
            </div>";

        return RenderShell(
            preheader: preheader,
            heading: heading,
            badgeText: "Hỗ trợ khách hàng",
            statusType: EmailStatusType.Info,
            bodyHtml: body);
    }

    /// <summary>
    /// Renders the weekly learning summary email with actual period dates, Vietnam learning timezone,
    /// verified active days, estimated interaction minutes, quiet empty state, and management settings link.
    /// </summary>
    public static string RenderWeeklyLearningSummary(
        DateTime start,
        DateTime end,
        int activeDays,
        int estimatedMinutes,
        string? settingsUrl)
    {
        var periodText = $"Tuần {start:dd/MM} – {end:dd/MM} (giờ Việt Nam)";
        var preheader = $"Tổng kết tuần học TCT English: {activeDays} ngày hoạt động, ~{estimatedMinutes} phút tương tác.";
        var heading = "Tổng kết tuần học tập";

        var safeSettingsUrl = !string.IsNullOrWhiteSpace(settingsUrl) ? Encode(settingsUrl) : null;

        var emptyStateNotice = activeDays == 0
            ? $@"
                <div style='margin: 16px 0; padding: 16px 18px; background-color: {TctEmailTokens.SurfaceMuted}; border: 1px solid {TctEmailTokens.Border}; border-radius: {TctEmailTokens.BoxRadius}; font-family: {TctEmailTokens.FontFamily}; font-size: 14px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                    Tuần qua hệ thống chưa ghi nhận lượt học tập nào của bạn. Hãy dành 10–15 phút mỗi ngày để tích lũy từ vựng và duy trì phản xạ tiếng Anh nhé!
                </div>"
            : $@"
                <div style='margin: 16px 0; padding: 14px 18px; background-color: {TctEmailTokens.SuccessSoft}; border-left: 4px solid {TctEmailTokens.Success}; border-radius: 4px 8px 8px 4px; font-family: {TctEmailTokens.FontFamily}; font-size: 14px; line-height: 1.5; color: {TctEmailTokens.SuccessText};'>
                    Tuyệt vời! Bạn đã duy trì học tập trong <strong>{activeDays} ngày</strong> với khoảng <strong>{estimatedMinutes} phút</strong> tương tác được ghi nhận.
                </div>";

        var settingsButtonHtml = safeSettingsUrl != null
            ? $@"
                <div style='margin: 24px 0 8px 0; text-align: center;'>
                    <a href='{safeSettingsUrl}' target='_blank' rel='noopener noreferrer' style='display: inline-block; padding: 12px 28px; background-color: {TctEmailTokens.SurfaceMuted}; border: 1px solid {TctEmailTokens.BorderControl}; border-radius: {TctEmailTokens.ButtonRadius}; font-family: {TctEmailTokens.FontFamily}; font-size: 14px; font-weight: 600; color: {TctEmailTokens.Text}; text-decoration: none;'>
                        Quản lý hoặc tắt email tuần
                    </a>
                </div>"
            : string.Empty;

        var body = $@"
            <div style='margin: 0 0 16px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 14px; font-weight: 600; color: {TctEmailTokens.Primary}; letter-spacing: 0.3px;'>
                📅 {periodText}
            </div>
            <p style='margin: 0 0 20px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 15px; line-height: 1.6; color: {TctEmailTokens.TextSecondary};'>
                Dưới đây là tổng kết hoạt động học tập trên hệ thống máy chủ <strong>TCT English</strong> trong tuần vừa qua của bạn:
            </p>

            <!-- Clay Stat Cards Grid -->
            <table role='presentation' width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 20px 0;'>
                <tr>
                    <td width='48%' style='padding-right: 2%; vertical-align: top;'>
                        <div style='background-color: {TctEmailTokens.ClayCanvas}; border: 1px solid {TctEmailTokens.CardBorder}; border-radius: 16px; padding: 18px 14px; text-align: center; box-shadow: {TctEmailTokens.InsetBoxShadow};'>
                            <div style='font-family: {TctEmailTokens.FontFamily}; font-size: 12px; font-weight: 700; color: {TctEmailTokens.Primary}; text-transform: uppercase; letter-spacing: 0.5px;'>
                                Số ngày học
                            </div>
                            <div style='font-family: {TctEmailTokens.FontFamily}; font-size: 32px; font-weight: 800; color: {TctEmailTokens.Text}; line-height: 1.2; margin-top: 6px;'>
                                {activeDays} <span style='font-size: 15px; font-weight: 500; color: {TctEmailTokens.TextMuted};'>/ 7 ngày</span>
                            </div>
                        </div>
                    </td>
                    <td width='48%' style='padding-left: 2%; vertical-align: top;'>
                        <div style='background-color: {TctEmailTokens.ClayCanvas}; border: 1px solid {TctEmailTokens.CardBorder}; border-radius: 16px; padding: 18px 14px; text-align: center; box-shadow: {TctEmailTokens.InsetBoxShadow};'>
                            <div style='font-family: {TctEmailTokens.FontFamily}; font-size: 12px; font-weight: 700; color: {TctEmailTokens.Primary}; text-transform: uppercase; letter-spacing: 0.5px;'>
                                Thời gian tương tác
                            </div>
                            <div style='font-family: {TctEmailTokens.FontFamily}; font-size: 32px; font-weight: 800; color: {TctEmailTokens.Text}; line-height: 1.2; margin-top: 6px;'>
                                {estimatedMinutes} <span style='font-size: 15px; font-weight: 500; color: {TctEmailTokens.TextMuted};'>phút</span>
                            </div>
                        </div>
                    </td>
                </tr>
            </table>

            {emptyStateNotice}
            {settingsButtonHtml}";

        return RenderShell(
            preheader: preheader,
            heading: heading,
            badgeText: "Học tập hàng tuần",
            statusType: EmailStatusType.Info,
            bodyHtml: body,
            footerSettingsUrl: safeSettingsUrl);
    }

    // ==================== Plain Text Counterparts ====================

    public static string RenderPlainTextPasswordReset(string resetLink, int expiryMinutes = 15)
    {
        return $@"TCT English - Đặt lại mật khẩu

Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản TCT English của bạn.

Để tạo mật khẩu mới, vui lòng mở liên kết sau trong trình duyệt:
{resetLink}

Liên kết có hiệu lực trong {expiryMinutes} phút và chỉ sử dụng một lần.

Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.
--
TCT English - Hệ thống học tiếng Anh trực tuyến";
    }

    public static string RenderPlainTextEmailChange(string confirmLink, int expiryMinutes = 30)
    {
        return $@"TCT English - Xác nhận địa chỉ email mới

Chúng tôi nhận được yêu cầu đổi email đăng nhập tài khoản TCT English sang địa chỉ này.

Để xác nhận, vui lòng mở liên kết sau:
{confirmLink}

Liên kết có hiệu lực trong {expiryMinutes} phút và chỉ dùng một lần.

Lưu ý: Email đăng nhập hiện tại của bạn vẫn giữ hiệu lực cho đến khi bạn xác nhận thành công liên kết trên.
--
TCT English - Hệ thống học tiếng Anh trực tuyến";
    }

    public static string RenderPlainTextProof(string purpose, string token, int expiryMinutes = 10, string? confirmUrl = null)
    {
        var title = purpose switch
        {
            AccountProofPurpose.VerifyEmail => "Xác minh địa chỉ email",
            AccountProofPurpose.Reauth => "Mã xác thực bảo mật",
            AccountProofPurpose.SetFirstPassword => "Thiết lập mật khẩu tài khoản",
            AccountProofPurpose.Deletion => "Xác nhận yêu cầu xóa tài khoản",
            AccountProofPurpose.WeeklyUnsubscribe => "Quản lý tùy chọn email tuần",
            _ => "Mã xác nhận tài khoản"
        };

        var sb = new StringBuilder();
        sb.AppendLine($"TCT English - {title}");
        sb.AppendLine();

        if (purpose == AccountProofPurpose.Deletion)
        {
            sb.AppendLine("Hệ thống nhận được yêu cầu xóa vĩnh viễn tài khoản TCT English của bạn.");
            sb.AppendLine("CẢNH BÁO: Tài khoản của bạn CHƯA bị xóa. Thao tác xóa chỉ được thực hiện sau khi bạn nhập mã này.");
            sb.AppendLine();
            sb.AppendLine($"Mã xác nhận bảo mật của bạn là: {token}");
            sb.AppendLine($"Mã có hiệu lực trong {expiryMinutes} phút và chỉ dùng một lần.");
        }
        else if (purpose == AccountProofPurpose.WeeklyUnsubscribe && !string.IsNullOrWhiteSpace(confirmUrl))
        {
            sb.AppendLine("Mở liên kết sau để truy cập trang quản lý và xác nhận tắt email tuần:");
            sb.AppendLine(confirmUrl);
            sb.AppendLine();
            sb.AppendLine("Lưu ý: Mở liên kết không tự động tắt email cho đến khi bạn bấm xác nhận.");
        }
        else
        {
            sb.AppendLine($"Mã xác nhận bảo mật của bạn là: {token}");
            sb.AppendLine($"Mã có hiệu lực trong {expiryMinutes} phút và chỉ dùng một lần.");
        }

        sb.AppendLine();
        sb.AppendLine("--\nTCT English - Hệ thống học tiếng Anh trực tuyến");
        return sb.ToString();
    }

    public static string RenderPlainTextAccountBlocked(string? reason, DateTime? lockExpiry)
    {
        var expiryText = lockExpiry.HasValue && lockExpiry.Value < DateTime.MaxValue
            ? lockExpiry.Value.ToString("dd/MM/yyyy HH:mm:ss (UTC)", CultureInfo.InvariantCulture)
            : "Vĩnh viễn";

        return $@"TCT English - Thông báo khóa tài khoản

Tài khoản của bạn trên TCT English đã bị khóa bởi quản trị viên.

Lý do: {reason ?? "Không có lý do cụ thể."}
Thời gian mở khóa: {expiryText}

Nếu bạn cho rằng đây là nhầm lẫn, vui lòng liên hệ quản trị viên qua email hỗ trợ.
--
TCT English - Hệ thống học tiếng Anh trực tuyến";
    }

    public static string RenderPlainTextAccountUnlocked(bool isAutoUnlock, string? loginUrl = null)
    {
        var unlockType = isAutoUnlock ? "tự động (hết thời hạn khóa)" : "thủ công bởi quản trị viên";
        var loginSection = !string.IsNullOrWhiteSpace(loginUrl) ? $"\nĐăng nhập lại tại: {loginUrl}\n" : string.Empty;

        return $@"TCT English - Tài khoản đã được mở khóa

Tài khoản của bạn trên TCT English đã được mở khóa {unlockType}.
Bạn có thể đăng nhập lại và tiếp tục lộ trình học tập của mình.{loginSection}
--
TCT English - Hệ thống học tiếng Anh trực tuyến";
    }

    public static string RenderPlainTextSupportContact(
        string senderName,
        string senderEmail,
        string subject,
        string message,
        string? ipAddress,
        string? userAgent)
    {
        return $@"TCT English - Yêu cầu liên hệ mới

Người gửi: {senderName}
Email: {senderEmail}
Chủ đề: {subject}

Nội dung:
{message}

---
IP: {ipAddress ?? "N/A"}
User-Agent: {userAgent ?? "N/A"}";
    }

    public static string RenderPlainTextWeeklyLearningSummary(
        DateTime start,
        DateTime end,
        int activeDays,
        int estimatedMinutes,
        string? settingsUrl)
    {
        var periodText = $"Tuần {start:dd/MM} – {end:dd/MM} (giờ Việt Nam)";
        var summaryText = activeDays == 0
            ? "Tuần qua hệ thống chưa ghi nhận lượt học tập nào của bạn. Hãy dành 10-15 phút mỗi ngày để duy trì thói quen học tiếng Anh nhé!"
            : $"Bạn đã duy trì học tập trong {activeDays}/7 ngày với khoảng {estimatedMinutes} phút tương tác được ghi nhận.";
        var settingsSection = !string.IsNullOrWhiteSpace(settingsUrl)
            ? $"\nQuản lý hoặc tắt email tuần tại: {settingsUrl}\n"
            : string.Empty;

        return $@"TCT English - Tổng kết tuần học tập
{periodText}

- Số ngày học: {activeDays}/7 ngày
- Thời gian tương tác: {estimatedMinutes} phút

{summaryText}{settingsSection}
--
TCT English - Hệ thống học tiếng Anh trực tuyến";
    }

    /// <summary>
    /// Shared email shell implementing the TCT English Claymorphism visual container.
    /// Uses presentation tables, inline CSS fallbacks, and resilient styling.
    /// </summary>
    public static string RenderShell(
        string preheader,
        string heading,
        string? badgeText,
        EmailStatusType statusType,
        string bodyHtml,
        string? footerSettingsUrl = null)
    {
        var safePreheader = Encode(preheader);
        var safeHeading = Encode(heading);

        // Preheader whitespace padding so email preview snippets do not leak footer text
        var preheaderPadding = string.Concat(Enumerable.Repeat("&nbsp;&zwnj;", 30));

        // Semantic badge styling
        var badgeHtml = string.Empty;
        if (!string.IsNullOrWhiteSpace(badgeText))
        {
            var safeBadge = Encode(badgeText.Trim());
            var (badgeBg, badgeBorder, badgeColor) = statusType switch
            {
                EmailStatusType.Success => (TctEmailTokens.SuccessSoft, TctEmailTokens.SuccessBorder, TctEmailTokens.SuccessText),
                EmailStatusType.Warning => (TctEmailTokens.WarningSoft, TctEmailTokens.WarningBorder, TctEmailTokens.WarningText),
                EmailStatusType.Danger => (TctEmailTokens.DangerSoft, TctEmailTokens.DangerBorder, TctEmailTokens.DangerText),
                EmailStatusType.Info => (TctEmailTokens.InfoSoft, TctEmailTokens.InfoBorder, TctEmailTokens.InfoText),
                _ => (TctEmailTokens.PrimarySoft, TctEmailTokens.CardBorder, TctEmailTokens.PrimaryStrong)
            };

            badgeHtml = $@"
                <div style='margin-bottom: 14px;'>
                    <span style='display: inline-block; padding: 4px 14px; background-color: {badgeBg}; border: 1px solid {badgeBorder}; border-radius: {TctEmailTokens.BadgeRadius}; font-family: {TctEmailTokens.FontFamily}; font-size: 12px; font-weight: 700; color: {badgeColor}; letter-spacing: 0.3px;'>
                        {safeBadge}
                    </span>
                </div>";
        }

        var footerSettingsLinkHtml = !string.IsNullOrWhiteSpace(footerSettingsUrl)
            ? $" &bull; <a href='{footerSettingsUrl}' target='_blank' rel='noopener noreferrer' style='color: {TctEmailTokens.Primary}; text-decoration: underline;'>Cài đặt thông báo</a>"
            : string.Empty;

        var sb = new StringBuilder();
        sb.Append($@"<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>
<html xmlns='http://www.w3.org/1999/xhtml' lang='vi' xml:lang='vi'>
<head>
    <meta http-equiv='Content-Type' content='text/html; charset=UTF-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <meta name='format-detection' content='telephone=no, date=no, address=no, email=no' />
    <meta name='x-apple-disable-message-reformatting' />
    <title>{safeHeading} - TCT English</title>
    <!--[if mso]>
    <style type='text/css'>
        table, td {{ border-collapse: collapse; mso-table-lspace: 0pt; mso-table-rspace: 0pt; }}
        body, table, td, p, a, li, blockquote {{ font-family: Arial, sans-serif !important; }}
    </style>
    <![endif]-->
    <style type='text/css'>
        /* Resets */
        body {{ margin: 0; padding: 0; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; background-color: {TctEmailTokens.ClayCanvas}; }}
        table {{ border-collapse: collapse; mso-table-lspace: 0pt; mso-table-rspace: 0pt; }}
        img {{ border: 0; outline: none; text-decoration: none; -ms-interpolation-mode: bicubic; }}
        a {{ text-decoration: none; }}

        /* Responsive breakpoints */
        @media only screen and (max-width: 620px) {{
            .email-container {{ width: 100% !important; max-width: 100% !important; }}
            .email-card {{ padding: 24px 20px !important; border-radius: 16px !important; }}
            .email-header {{ padding: 24px 16px 12px 16px !important; }}
            .email-footer {{ padding: 20px 16px 28px 16px !important; }}
            .email-title {{ font-size: 20px !important; }}
        }}
    </style>
</head>
<body bgcolor='{TctEmailTokens.ClayCanvas}' style='margin: 0; padding: 0; background-color: {TctEmailTokens.ClayCanvas}; font-family: {TctEmailTokens.FontFamily}; color: {TctEmailTokens.Text};'>

    <!-- Hidden Preheader for Inbox Snippet -->
    <div style='display: none; font-size: 1px; line-height: 1px; max-height: 0px; max-width: 0px; opacity: 0; overflow: hidden; mso-hide: all; font-family: sans-serif;'>
        {safePreheader} {preheaderPadding}
    </div>

    <!-- Outer Table Canvas -->
    <table role='presentation' width='100%' cellpadding='0' cellspacing='0' border='0' bgcolor='{TctEmailTokens.ClayCanvas}' style='background-color: {TctEmailTokens.ClayCanvas}; width: 100%; margin: 0; padding: 0;'>
        <tr>
            <td align='center' style='padding: 32px 16px;'>

                <!-- Inner Centered 600px Container -->
                <table role='presentation' class='email-container' width='600' cellpadding='0' cellspacing='0' border='0' style='max-width: 600px; width: 100%; margin: 0 auto;'>

                    <!-- Brand Header / Wordmark -->
                    <tr>
                        <td align='center' class='email-header' style='padding: 0 0 20px 0;'>
                            <div style='font-family: {TctEmailTokens.FontFamily}; font-size: 26px; font-weight: 800; letter-spacing: -0.5px; color: {TctEmailTokens.Text}; line-height: 1.2;'>
                                <span style='display: inline-block; color: {TctEmailTokens.Primary}; background-color: {TctEmailTokens.PrimarySoft}; border: 1px solid {TctEmailTokens.CardBorder}; border-radius: 10px; padding: 2px 10px; margin-right: 4px; box-shadow: 0 2px 6px rgba(66, 85, 255, 0.12);'>TCT</span>&nbsp;<span style='color: {TctEmailTokens.Text};'>English</span><span style='display: inline-block; width: 7px; height: 7px; background-color: {TctEmailTokens.AccentYellow}; border-radius: 50%; margin-left: 3px; vertical-align: baseline;'></span>
                            </div>
                        </td>
                    </tr>

                    <!-- Primary Clay Surface Card -->
                    <tr>
                        <td bgcolor='{TctEmailTokens.ClaySurface}' class='email-card' style='background-color: {TctEmailTokens.ClaySurface}; border-radius: {TctEmailTokens.CardRadius}; border: 1px solid {TctEmailTokens.CardBorder}; box-shadow: {TctEmailTokens.CardShadow}; padding: 36px 36px 32px 36px;'>
                            {badgeHtml}
                            <h1 class='email-title' style='margin: 0 0 18px 0; font-family: {TctEmailTokens.FontFamily}; font-size: 22px; font-weight: 700; line-height: 1.35; color: {TctEmailTokens.Text};'>
                                {safeHeading}
                            </h1>
                            {bodyHtml}
                        </td>
                    </tr>

                    <!-- Quiet Brand Footer -->
                    <tr>
                        <td align='center' class='email-footer' style='padding: 24px 20px 16px 20px; font-family: {TctEmailTokens.FontFamily}; font-size: 12px; line-height: 1.6; color: {TctEmailTokens.TextMuted}; text-align: center;'>
                            <div>Email này được gửi tự động từ hệ thống <strong>TCT English</strong>.</div>
                            <div style='margin-top: 4px;'>
                                &copy; {DateTime.UtcNow.Year} TCT English. Bảo lưu mọi quyền.{footerSettingsLinkHtml}
                            </div>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>

</body>
</html>");

        return sb.ToString();
    }
}
