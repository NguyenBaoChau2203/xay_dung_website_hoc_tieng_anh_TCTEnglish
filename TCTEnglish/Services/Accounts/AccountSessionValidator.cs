using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.Security;

namespace TCTEnglish.Services.Accounts;

public static class AccountSessionValidator
{
    public static async Task<bool> IsValidAsync(DbflashcardContext context, ClaimsPrincipal? principal,
        DateTime utcNow, CancellationToken cancellationToken = default)
    {
        if (!principal.TryGetUserId(out var userId)
            || !int.TryParse(principal?.FindFirst(AccountSecurityClaims.Version)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var version)) return false;
        var role = principal?.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrWhiteSpace(role)) return false;
        var user = await context.Users.AsNoTracking().Where(row => row.UserId == userId)
            .Select(row => new { row.SecurityVersion, row.AnonymizedAt, row.Status, row.LockExpiry, row.Role })
            .FirstOrDefaultAsync(cancellationToken);
        return user != null && user.AnonymizedAt == null && user.SecurityVersion == version
            && Roles.Normalize(user.Role) == Roles.Normalize(role)
            && (user.Status != UserStatus.Blocked || (user.LockExpiry.HasValue && user.LockExpiry <= utcNow));
    }
}
