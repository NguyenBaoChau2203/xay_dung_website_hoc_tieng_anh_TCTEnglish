using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCTVocabulary.Models;

namespace TCTEnglish.Tests.Infrastructure;

public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserIdHeaderName = "X-Test-UserId";
    public const string RoleHeaderName = "X-Test-Role";
    public const string EmailHeaderName = "X-Test-Email";
    public const string NameHeaderName = "X-Test-Name";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeaderName, out var userIdValues)
            || string.IsNullOrWhiteSpace(userIdValues.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = userIdValues.ToString();
        var role = Request.Headers[RoleHeaderName].ToString();
        var email = Request.Headers[EmailHeaderName].ToString();
        var name = Request.Headers[NameHeaderName].ToString();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, string.IsNullOrWhiteSpace(role) ? Roles.Standard : role),
            new Claim(ClaimTypes.Email, string.IsNullOrWhiteSpace(email) ? $"user{userId}@test.local" : email),
            new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(name) ? $"User {userId}" : name)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
