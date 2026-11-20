using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Services.Accounts;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Security;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// BILLING-ROLE-CHANGE-SIGNS-OUT: a purchase, an expiry or an admin changes only the stored role. That must renew the
/// cookie, not end the session; every other reason to refuse a cookie still refuses it.
/// </summary>
public class AccountSessionRoleChangeTests
{
    private static ClaimsPrincipal Cookie(string role, string? version = "0", int userId = TestDataIds.UserId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, "student@tct.local"),
            new(ClaimTypes.Role, role)
        };
        if (version != null) claims.Add(new Claim(AccountSecurityClaims.Version, version));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
    }

    private static async Task<AccountSessionResult> EvaluateAsync(
        ClaimsPrincipal principal, Action<User>? change = null)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        if (change != null)
        {
            var user = db.Users.First(u => u.UserId == TestDataIds.UserId);
            change(user);
            await db.SaveChangesAsync();
        }

        return await AccountSessionValidator.EvaluateAsync(db, principal, DateTime.UtcNow);
    }

    [Fact]
    public async Task OnlyTheRoleDiffers_IsRoleChanged_WithTheStoredRole()
    {
        // The stored role is Premium (just bought); the cookie still says Standard.
        var result = await EvaluateAsync(Cookie(Roles.Standard), u => u.Role = Roles.Premium);

        Assert.Equal(AccountSessionStatus.RoleChanged, result.Status);
        Assert.Equal(Roles.Premium, result.CurrentRole);
    }

    [Fact]
    public async Task AnExpiredPremium_IsRoleChangedBackToStandard()
    {
        var result = await EvaluateAsync(Cookie(Roles.Premium), u => u.Role = Roles.Standard);

        Assert.Equal(AccountSessionStatus.RoleChanged, result.Status);
        Assert.Equal(Roles.Standard, result.CurrentRole);
    }

    [Fact]
    public async Task ARoleChange_WithANewSecurityVersion_IsStillRefused()
    {
        var result = await EvaluateAsync(Cookie(Roles.Standard, version: "0"), u => { u.Role = Roles.Premium; u.SecurityVersion = 7; });

        Assert.Equal(AccountSessionStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task ARoleChange_OnAnAnonymisedAccount_IsStillRefused()
    {
        var result = await EvaluateAsync(Cookie(Roles.Standard), u => { u.Role = Roles.Premium; u.AnonymizedAt = DateTime.UtcNow; });

        Assert.Equal(AccountSessionStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task ARoleChange_OnABlockedAccount_IsStillRefused()
    {
        var result = await EvaluateAsync(Cookie(Roles.Standard), u =>
        {
            u.Role = Roles.Premium;
            u.Status = UserStatus.Blocked;
            u.LockExpiry = DateTime.UtcNow.AddDays(1);
        });

        Assert.Equal(AccountSessionStatus.Invalid, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ACookieWithoutARoleOrVersion_IsRefused(string? role)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, TestDataIds.UserId.ToString()), new(AccountSecurityClaims.Version, "0") };
        if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));

        Assert.Equal(AccountSessionStatus.Invalid,
            (await EvaluateAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies")))).Status);
        Assert.Equal(AccountSessionStatus.Invalid, (await EvaluateAsync(Cookie(Roles.Standard, version: null))).Status);
    }

    [Fact]
    public async Task TheStrictCheckUsedByTheHubs_StillRequiresTheStoredRole()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = db.Users.First(u => u.UserId == TestDataIds.UserId);
        user.Role = Roles.Premium;
        await db.SaveChangesAsync();

        Assert.False(await AccountSessionValidator.IsValidAsync(db, Cookie(Roles.Standard), DateTime.UtcNow));
        Assert.True(await AccountSessionValidator.IsValidAsync(db, Cookie(Roles.Premium), DateTime.UtcNow));
    }

    [Fact]
    public void WithRole_KeepsEveryOtherClaim_AndReplacesOnlyTheRole()
    {
        var renewed = AccountSessionValidator.WithRole(Cookie(Roles.Standard), Roles.Premium);

        Assert.Equal(Roles.Premium, renewed.FindFirst(ClaimTypes.Role)!.Value);
        Assert.Single(renewed.FindAll(ClaimTypes.Role));
        Assert.Equal(TestDataIds.UserId.ToString(), renewed.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        Assert.Equal("student@tct.local", renewed.FindFirst(ClaimTypes.Email)!.Value);
        Assert.Equal("0", renewed.FindFirst(AccountSecurityClaims.Version)!.Value);
        Assert.Equal("Cookies", renewed.Identity!.AuthenticationType);
        Assert.True(renewed.IsInRole(Roles.Premium));
        Assert.False(renewed.IsInRole(Roles.Standard));
    }

    [Fact]
    public void TheCookieEvent_RenewsOnARoleChange_AndRejectsOtherwise()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TCTEnglish", "Program.cs"));

        Assert.Contains("AccountSessionStatus.RoleChanged", source, StringComparison.Ordinal);
        Assert.Contains("context.ReplacePrincipal(", source, StringComparison.Ordinal);
        Assert.Contains("context.ShouldRenew = true;", source, StringComparison.Ordinal);
        Assert.Contains("session.Status != AccountSessionStatus.Valid", source, StringComparison.Ordinal);
    }
}
