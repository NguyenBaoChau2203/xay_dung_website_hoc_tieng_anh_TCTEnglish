using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.Security;

namespace TCTEnglish.Services.Accounts;

public enum AccountSessionStatus
{
    /// <summary>The cookie must be refused (unknown user, new security version, anonymised or locked account).</summary>
    Invalid,

    /// <summary>Every check passes and the cookie's role equals the stored role.</summary>
    Valid,

    /// <summary>
    /// Every check passes and only the role differs (a purchase, an expiry or an admin changed it). The cookie is
    /// still the same person's: it is renewed with the stored role instead of ending the session.
    /// </summary>
    RoleChanged
}

public readonly record struct AccountSessionResult(AccountSessionStatus Status, string? CurrentRole);

public static class AccountSessionValidator
{
    /// <summary>Strict check (hubs): valid only when the role in the cookie is the stored role.</summary>
    public static async Task<bool> IsValidAsync(DbflashcardContext context, ClaimsPrincipal? principal,
        DateTime utcNow, CancellationToken cancellationToken = default)
        => (await EvaluateAsync(context, principal, utcNow, cancellationToken)).Status == AccountSessionStatus.Valid;

    public static async Task<AccountSessionResult> EvaluateAsync(DbflashcardContext context, ClaimsPrincipal? principal,
        DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var invalid = new AccountSessionResult(AccountSessionStatus.Invalid, null);
        if (!principal.TryGetUserId(out var userId)
            || !int.TryParse(principal?.FindFirst(AccountSecurityClaims.Version)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var version)) return invalid;
        var role = principal?.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrWhiteSpace(role)) return invalid;
        var user = await context.Users.AsNoTracking().Where(row => row.UserId == userId)
            .Select(row => new { row.SecurityVersion, row.AnonymizedAt, row.Status, row.LockExpiry, row.Role })
            .FirstOrDefaultAsync(cancellationToken);
        if (user == null || user.AnonymizedAt != null || user.SecurityVersion != version
            || (user.Status == UserStatus.Blocked && !(user.LockExpiry.HasValue && user.LockExpiry <= utcNow)))
        {
            return invalid;
        }

        var currentRole = Roles.Normalize(user.Role);
        return Roles.Normalize(role) == currentRole
            ? new AccountSessionResult(AccountSessionStatus.Valid, currentRole)
            : new AccountSessionResult(AccountSessionStatus.RoleChanged, currentRole);
    }

    /// <summary>The same identity with the role claim replaced by <paramref name="role"/>.</summary>
    public static ClaimsPrincipal WithRole(ClaimsPrincipal principal, string role)
    {
        var source = principal.Identity as ClaimsIdentity;
        var identity = new ClaimsIdentity(
            source?.Claims.Where(claim => claim.Type != ClaimTypes.Role) ?? Enumerable.Empty<Claim>(),
            source?.AuthenticationType,
            source?.NameClaimType ?? ClaimTypes.Name,
            ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }
}
