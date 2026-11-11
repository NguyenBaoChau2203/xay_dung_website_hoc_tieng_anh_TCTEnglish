using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Emails;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Accounts;

public interface IAccountSecurityService
{
    bool IsMailConfigured { get; }

    Task<AccountActionResult> RequestEmailChangeAsync(User user, string newEmail, string? currentPassword, string? proofCode, string confirmUrlFormat, CancellationToken cancellationToken = default);

    Task<AccountActionResult> ConfirmEmailChangeAsync(int userId, string? token, CancellationToken cancellationToken = default);

    Task<AccountActionResult> VerifyCurrentEmailAsync(int userId, string? token, CancellationToken cancellationToken = default);

    Task<AccountActionResult> ChangePasswordAsync(User user, string? currentPassword, string? proofCode, string? newPassword, string? confirmPassword, CancellationToken cancellationToken = default);

    Task<AccountActionResult> SendProofAsync(User user, string purpose, string? confirmUrlFormat, CancellationToken cancellationToken = default);

    Task<bool> HasFreshProofAsync(User user, string? currentPassword, string? proofCode, string purpose, CancellationToken cancellationToken = default);

    bool IsSocialLoginMarker(User user);
}

public sealed class AccountActionResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public string? Message { get; init; }
    public bool ReissueSession { get; init; }

    public static AccountActionResult Ok(string message, bool reissueSession = false)
        => new() { Succeeded = true, Message = message, ReissueSession = reissueSession };

    public static AccountActionResult Fail(string error)
        => new() { Error = error };
}

public sealed class AccountSecurityService : IAccountSecurityService
{
    private static readonly TimeSpan EmailChangeTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ProofTtl = TimeSpan.FromMinutes(10);
    private readonly DbflashcardContext _context;
    private readonly IAppEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public AccountSecurityService(
        DbflashcardContext context,
        IAppEmailSender emailSender,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _context = context;
        _emailSender = emailSender;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public bool IsMailConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["SmtpSettings:Host"])
        && !string.IsNullOrWhiteSpace(_configuration["SmtpSettings:SenderEmail"])
        && !string.IsNullOrWhiteSpace(_configuration["SmtpSettings:Password"]);

    public bool IsSocialLoginMarker(User user)
        => user.PasswordHash != null
           && user.PasswordHash.StartsWith("SOCIAL_LOGIN_", StringComparison.Ordinal);

    public async Task<AccountActionResult> RequestEmailChangeAsync(
        User user,
        string newEmail,
        string? currentPassword,
        string? proofCode,
        string confirmUrlFormat,
        CancellationToken cancellationToken = default)
    {
        var normalized = (newEmail ?? string.Empty).Trim();
        if (!IsPlausibleEmail(normalized))
        {
            return AccountActionResult.Fail("Email không hợp lệ.");
        }

        var issued = await UserMutationScope.ExecuteAsync(_context, user.UserId,
            () => PrepareEmailChangeAsync(user.UserId, normalized, currentPassword, proofCode, cancellationToken),
            result => result.Succeeded, cancellationToken);
        if (!issued.Succeeded || issued.Token == null)
        {
            return AccountActionResult.Fail(issued.Error ?? "Không tạo được mã xác nhận.");
        }

        var link = string.Format(CultureInfo.InvariantCulture, confirmUrlFormat, Uri.EscapeDataString(issued.Token));
        var htmlBody = TctEmailRenderer.RenderEmailChange(link, (int)EmailChangeTtl.TotalMinutes);
        var sent = await _emailSender.TrySendTrackedHtmlAsync(
            normalized,
            "Xác nhận email mới - TCT English",
            htmlBody,
            cancellationToken);
        if (sent != EmailDispatchResult.Sent)
        {
            await ConsumeDigestAsync(user.UserId, issued.Digest, cancellationToken);
            return AccountActionResult.Fail("Chưa gửi được email xác nhận. Email đăng nhập không đổi.");
        }

        return AccountActionResult.Ok("Đã gửi email xác nhận tới địa chỉ mới. Email đăng nhập hiện tại vẫn còn hiệu lực.");
    }

    private async Task<(bool Succeeded, string? Error, string? Token, string? Digest, string? RecipientEmail)> PrepareEmailChangeAsync(
        int userId, string normalized, string? currentPassword, string? proofCode, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        if (user == null || IsUnavailable(user)) return (false, "Tài khoản không còn hoạt động.", null, null, null);
        if (string.Equals(normalized, user.Email, StringComparison.OrdinalIgnoreCase))
            return (false, "Email mới trùng email đăng nhập hiện tại.", null, null, null);
        if (!await HasFreshProofAsync(user, currentPassword, proofCode, AccountProofPurpose.Reauth, cancellationToken))
            return (false, "Cần mật khẩu hiện tại hoặc mã xác nhận còn hiệu lực. Dấu SOCIAL_LOGIN không đủ để đổi email.", null, null, null);
        var taken = await _context.Users.AsNoTracking().AnyAsync(
            u => u.UserId != user.UserId && u.Email.ToLower() == normalized.ToLower(),
            cancellationToken);
        if (taken)
        {
            return (false, "Email này đã được sử dụng bởi người dùng khác.", null, null, null);
        }

        if (!IsMailConfigured)
        {
            return (false, "Chưa gửi được email xác nhận vì máy chủ thư chưa được cấu hình. Email đăng nhập không đổi.", null, null, null);
        }

        return await IssueChallengeCoreAsync(userId, AccountProofPurpose.EmailChange, EmailChangeTtl, normalized, cancellationToken);
    }

    public Task<AccountActionResult> ConfirmEmailChangeAsync(int userId, string? token, CancellationToken cancellationToken = default)
        => UserMutationScope.ExecuteAsync(_context, userId,
            () => ConfirmEmailChangeCoreAsync(userId, token, cancellationToken), result => result.Succeeded, cancellationToken);

    private async Task<AccountActionResult> ConfirmEmailChangeCoreAsync(int userId, string? token, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user == null || IsUnavailable(user))
        {
            return AccountActionResult.Fail("Liên kết xác nhận không hợp lệ.");
        }

        var challenge = await FindLiveChallengeAsync(user, token, AccountProofPurpose.EmailChange, cancellationToken);
        if (challenge == null || string.IsNullOrWhiteSpace(challenge.NewEmail))
        {
            return AccountActionResult.Fail("Liên kết xác nhận không hợp lệ, đã dùng hoặc đã hết hạn.");
        }

        var newEmail = challenge.NewEmail;
        var taken = await _context.Users.AnyAsync(
            u => u.UserId != user.UserId && u.Email.ToLower() == newEmail.ToLower(),
            cancellationToken);
        if (taken)
        {
            return AccountActionResult.Fail("Email này đã được sử dụng bởi người dùng khác.");
        }

        challenge.ConsumedAtUtc = UtcNow;
        user.Email = newEmail;
        user.EmailVerifiedAt = UtcNow;
        await InvalidateSecurityStateAsync(user, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return AccountActionResult.Fail("Email này đã được sử dụng bởi người dùng khác.");
        }

        return AccountActionResult.Ok("Đã xác nhận email mới.", reissueSession: true);
    }

    public Task<AccountActionResult> ChangePasswordAsync(
        User user,
        string? currentPassword,
        string? proofCode,
        string? newPassword,
        string? confirmPassword,
        CancellationToken cancellationToken = default)
        => UserMutationScope.ExecuteAsync(_context, user.UserId,
            () => ChangePasswordCoreAsync(user.UserId, currentPassword, proofCode, newPassword, confirmPassword, cancellationToken),
            result => result.Succeeded, cancellationToken);

    private async Task<AccountActionResult> ChangePasswordCoreAsync(int userId, string? currentPassword,
        string? proofCode, string? newPassword, string? confirmPassword, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        if (user == null || IsUnavailable(user))
            return AccountActionResult.Fail("Không tìm thấy tài khoản đang hoạt động.");
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            return AccountActionResult.Fail("Mật khẩu xác nhận không khớp.");
        }

        if (!AccountPasswordRules.TryValidate(newPassword, out var passwordError))
        {
            return AccountActionResult.Fail(passwordError);
        }

        if (IsSocialLoginMarker(user))
        {
            if (!await ConsumeProofAsync(user, proofCode, AccountProofPurpose.SetFirstPassword, cancellationToken))
            {
                return AccountActionResult.Fail("Tài khoản có dấu SOCIAL_LOGIN chưa có mật khẩu cục bộ. Hãy yêu cầu mã gửi tới email hiện tại trước khi đặt mật khẩu.");
            }

        }
        else
        {
            if (!VerifyPassword(currentPassword, user.PasswordHash))
            {
                return AccountActionResult.Fail("Mật khẩu hiện tại không đúng.");
            }

            if (VerifyPassword(newPassword, user.PasswordHash))
            {
                return AccountActionResult.Fail("Mật khẩu mới không được trùng với mật khẩu hiện tại. Vui lòng chọn mật khẩu khác.");
            }
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await InvalidateSecurityStateAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return AccountActionResult.Ok("Cập nhật mật khẩu thành công.", reissueSession: true);
    }

    public async Task<AccountActionResult> SendProofAsync(User user, string purpose, string? confirmUrlFormat, CancellationToken cancellationToken = default)
    {
        if (purpose is not (AccountProofPurpose.Reauth or AccountProofPurpose.SetFirstPassword or AccountProofPurpose.Deletion or AccountProofPurpose.WeeklyUnsubscribe or AccountProofPurpose.VerifyEmail))
        {
            return AccountActionResult.Fail("Mục đích xác nhận không hợp lệ.");
        }

        if (IsUnavailable(user) || string.IsNullOrWhiteSpace(user.Email))
        {
            return AccountActionResult.Fail("Không gửi được mã xác nhận cho tài khoản này.");
        }

        if (!IsMailConfigured)
        {
            return AccountActionResult.Fail("Máy chủ thư chưa được cấu hình nên chưa gửi được mã xác nhận.");
        }

        var ttl = purpose == AccountProofPurpose.WeeklyUnsubscribe ? TimeSpan.FromDays(14) : ProofTtl;
        var issued = await IssueChallengeAsync(user, purpose, ttl, null, cancellationToken);
        if (!issued.Succeeded || issued.Token == null)
        {
            return AccountActionResult.Fail(issued.Error ?? "Không tạo được mã xác nhận.");
        }

        var confirmUrl = purpose == AccountProofPurpose.WeeklyUnsubscribe && !string.IsNullOrWhiteSpace(confirmUrlFormat)
            ? string.Format(CultureInfo.InvariantCulture, confirmUrlFormat, Uri.EscapeDataString(issued.Token))
            : null;
        var body = TctEmailRenderer.RenderProof(purpose, issued.Token, (int)ttl.TotalMinutes, confirmUrl);
        var subject = purpose switch
        {
            AccountProofPurpose.VerifyEmail => "Xác minh email tài khoản - TCT English",
            AccountProofPurpose.Deletion => "Mã xác nhận xóa tài khoản - TCT English",
            AccountProofPurpose.WeeklyUnsubscribe => "Quản lý tùy chọn email tuần - TCT English",
            _ => "Mã xác nhận tài khoản - TCT English"
        };
        var sent = await _emailSender.TrySendTrackedHtmlAsync(issued.RecipientEmail!, subject, body, cancellationToken);
        if (sent != EmailDispatchResult.Sent)
        {
            await ConsumeDigestAsync(user.UserId, issued.Digest, cancellationToken);
            return AccountActionResult.Fail("Chưa gửi được mã xác nhận.");
        }

        return AccountActionResult.Ok("Đã gửi mã xác nhận tới email hiện tại.");
    }

    public async Task<bool> HasFreshProofAsync(User user, string? currentPassword, string? proofCode, string purpose, CancellationToken cancellationToken = default)
    {
        await _context.Entry(user).ReloadAsync(cancellationToken);
        if (IsUnavailable(user) || _context.Entry(user).State == EntityState.Detached) return false;
        if (!IsSocialLoginMarker(user) && VerifyPassword(currentPassword, user.PasswordHash))
        {
            return true;
        }

        return await ConsumeProofAsync(user, proofCode, purpose, cancellationToken);
    }

    public Task<AccountActionResult> VerifyCurrentEmailAsync(int userId, string? token, CancellationToken cancellationToken = default)
        => UserMutationScope.ExecuteAsync(_context, userId, async () =>
        {
            var user = await _context.Users.FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);
            if (user == null || IsUnavailable(user)
                || !await ConsumeProofAsync(user, token, AccountProofPurpose.VerifyEmail, cancellationToken))
                return AccountActionResult.Fail("Mã xác minh email không hợp lệ, đã dùng hoặc hết hạn.");
            user.EmailVerifiedAt = UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return AccountActionResult.Ok("Đã xác minh email hiện tại.");
        }, result => result.Succeeded, cancellationToken);

    private bool IsUnavailable(User user) => user.AnonymizedAt != null
        || (user.Status == UserStatus.Blocked && (user.LockExpiry == null || user.LockExpiry > UtcNow));

    private async Task<bool> ConsumeProofAsync(User user, string? token, string purpose, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128) return false;
        var digest = Digest(token.Trim());
        var now = UtcNow;
        return await _context.AccountSecurityChallenges
            .Where(row => row.UserId == user.UserId && row.Purpose == purpose && row.TokenDigest == digest
                && row.ConsumedAtUtc == null && row.ExpiresAtUtc > now && row.SecurityVersion == user.SecurityVersion)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ConsumedAtUtc, now), cancellationToken) == 1;
    }

    public static bool VerifyPassword(string? password, string? storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        if (storedHash.StartsWith("SOCIAL_LOGIN_", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            return false;
        }
    }

    private async Task InvalidateSecurityStateAsync(User user, CancellationToken cancellationToken)
    {
        user.SecurityVersion += 1;
        user.ResetPasswordToken = null;
        user.ResetPasswordTokenExpiry = null;
        var now = UtcNow;
        await _context.AccountSecurityChallenges
            .Where(c => c.UserId == user.UserId && c.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.ConsumedAtUtc, now), cancellationToken);
    }

    private async Task<(bool Succeeded, string? Error, string? Token, string? Digest, string? RecipientEmail)> IssueChallengeAsync(
        User user,
        string purpose,
        TimeSpan ttl,
        string? newEmail,
        CancellationToken cancellationToken)
        => await UserMutationScope.ExecuteAsync(_context, user.UserId,
            () => IssueChallengeCoreAsync(user.UserId, purpose, ttl, newEmail, cancellationToken),
            result => result.Succeeded, cancellationToken);

    private async Task<(bool Succeeded, string? Error, string? Token, string? Digest, string? RecipientEmail)> IssueChallengeCoreAsync(
        int userId, string purpose, TimeSpan ttl, string? newEmail, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        if (user == null || IsUnavailable(user))
            return (false, "Tài khoản không còn hoạt động.", null, null, null);
        var since = UtcNow.AddMinutes(-15);
        var recent = await _context.AccountSecurityChallenges.CountAsync(
            c => c.UserId == user.UserId && c.Purpose == purpose && c.CreatedAtUtc >= since,
            cancellationToken);
        if (recent >= 5)
        {
            return (false, "Bạn đã yêu cầu quá nhiều mã. Hãy thử lại sau.", null, null, null);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var digest = Digest(token);
        _context.AccountSecurityChallenges.Add(new AccountSecurityChallenge
        {
            UserId = user.UserId,
            Purpose = purpose,
            TokenDigest = digest,
            NewEmail = newEmail,
            SecurityVersion = user.SecurityVersion,
            ExpiresAtUtc = UtcNow.Add(ttl),
            CreatedAtUtc = UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        return (true, null, token, digest, user.Email);
    }

    private async Task<AccountSecurityChallenge?> FindLiveChallengeAsync(User user, string? token, string purpose, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        {
            return null;
        }

        var digest = Digest(token.Trim());
        var now = UtcNow;
        return await _context.AccountSecurityChallenges.FirstOrDefaultAsync(
            c => c.UserId == user.UserId
                 && c.Purpose == purpose
                 && c.TokenDigest == digest
                 && c.ConsumedAtUtc == null
                 && c.ExpiresAtUtc > now
                 && c.SecurityVersion == user.SecurityVersion,
            cancellationToken);
    }

    private async Task ConsumeDigestAsync(int userId, string? digest, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return;
        }

        var row = await _context.AccountSecurityChallenges.FirstOrDefaultAsync(
            c => c.UserId == userId && c.TokenDigest == digest,
            cancellationToken);
        if (row != null)
        {
            row.ConsumedAtUtc = UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private static string Digest(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static bool IsPlausibleEmail(string email)
    {
        if (email.Length is < 3 or > 255 || email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(email);
            return parsed.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
