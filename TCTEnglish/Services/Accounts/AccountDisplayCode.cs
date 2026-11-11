using System.Globalization;

namespace TCTEnglish.Services.Accounts;

public static class AccountDisplayCode
{
    public static string Format(int userId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"TCT-{userId:D8}");
    }
}

public static class AccountPasswordRules
{
    public const int MinimumCharacters = 6;
    public const int MaximumUtf8Bytes = 72;

    public static bool TryValidate(string? password, out string error)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumCharacters)
        {
            error = "Mật khẩu mới phải có ít nhất 6 ký tự.";
            return false;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(password) > MaximumUtf8Bytes)
        {
            error = "Mật khẩu quá dài so với giới hạn lưu trữ an toàn. Hãy chọn mật khẩu ngắn hơn.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public static class AccountSecurityClaims
{
    public const string Version = "tct_secver";

    public static string FormatVersion(int securityVersion)
        => securityVersion.ToString(CultureInfo.InvariantCulture);
}
