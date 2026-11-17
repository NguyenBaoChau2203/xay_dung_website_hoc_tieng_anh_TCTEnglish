using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace TCTEnglish.Services.Contact;

public static class ContactInput
{
    public static bool HasLineBreak(string value) => value.Contains('\r') || value.Contains('\n');

    public static bool IsMailbox(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254 || HasLineBreak(value)
            || !new EmailAddressAttribute().IsValid(value)) return false;
        return MailAddress.TryCreate(value, out var address)
            && address.DisplayName.Length == 0 && address.Address == value;
    }

    public static string BoundDiagnostic(string? value, int limit) =>
        string.IsNullOrEmpty(value) ? "N/A" : value[..Math.Min(limit, value.Length)];
}
