using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AccountCorrectnessIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Theory]
    [InlineData("SOCIAL_LOGIN_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("$2a$10$truncated_invalid_salt")]
    public async Task Login_WithNonBcryptStoredHash_ReturnsGenericFailureWithout500(string storedHash)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var email = $"social-{Guid.NewGuid():N}@test.local";
        await SeedUserAsync(factory, email, storedHash);

        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Account/Login");

        using var response = await PostLoginAsync(client, email, "any-password", token);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("SaltParse", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(
            "Email hoặc mật khẩu không đúng.",
            doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Login_WithValidBcryptHash_StillSucceeds()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var email = $"local-{Guid.NewGuid():N}@test.local";
        var password = "CorrectHorseBattery1";
        await SeedUserAsync(factory, email, BCrypt.Net.BCrypt.HashPassword(password));

        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Account/Login");

        using var response = await PostLoginAsync(client, email, password, token);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task ResetPassword_RejectsNullExpiryToken()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var email = $"reset-null-{Guid.NewGuid():N}@test.local";
        var token = Guid.NewGuid().ToString("N");
        await SeedUserAsync(
            factory,
            email,
            BCrypt.Net.BCrypt.HashPassword("OldPassword1"),
            resetToken: token,
            resetExpiry: null);

        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var antiForgery = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Account/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Token"] = token,
            ["NewPassword"] = "NewPassword1!",
            ["ConfirmPassword"] = "NewPassword1!",
            ["__RequestVerificationToken"] = antiForgery
        });

        using var response = await client.PostAsync("/Account/ResetPassword", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Đặt lại mật khẩu thành công", body, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.True(
            BCrypt.Net.BCrypt.Verify("OldPassword1", user.PasswordHash),
            "Null expiry must not consume the reset or change the password.");
        Assert.Equal(token, user.ResetPasswordToken);
        Assert.Null(user.ResetPasswordTokenExpiry);
        Assert.Contains("validation-summary-errors", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ConsumesOnce()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var email = $"reset-ok-{Guid.NewGuid():N}@test.local";
        var token = Guid.NewGuid().ToString("N");
        await SeedUserAsync(
            factory,
            email,
            BCrypt.Net.BCrypt.HashPassword("OldPassword1"),
            resetToken: token,
            resetExpiry: DateTime.UtcNow.AddMinutes(15));

        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var antiForgery = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Account/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}");

        using var firstContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Token"] = token,
            ["NewPassword"] = "NewPassword1!",
            ["ConfirmPassword"] = "NewPassword1!",
            ["__RequestVerificationToken"] = antiForgery
        });
        using var firstResponse = await client.PostAsync("/Account/ResetPassword", firstContent);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        using var secondContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Token"] = token,
            ["NewPassword"] = "AnotherPassword1!",
            ["ConfirmPassword"] = "AnotherPassword1!",
            ["__RequestVerificationToken"] = antiForgery
        });
        using var secondResponse = await client.PostAsync("/Account/ResetPassword", secondContent);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.DoesNotContain("Đặt lại mật khẩu thành công", secondBody, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword1!", user.PasswordHash));
        Assert.Null(user.ResetPasswordToken);
        Assert.Null(user.ResetPasswordTokenExpiry);
    }

    [Fact]
    public async Task ResetPassword_ConcurrentPosts_OnlyOneConsumesToken()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var email = $"reset-race-{Guid.NewGuid():N}@test.local";
        var token = Guid.NewGuid().ToString("N");
        await SeedUserAsync(
            factory,
            email,
            BCrypt.Net.BCrypt.HashPassword("OldPassword1"),
            resetToken: token,
            resetExpiry: DateTime.UtcNow.AddMinutes(15));

        using var clientA = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var clientB = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var antiForgeryA = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            clientA,
            $"/Account/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}");
        var antiForgeryB = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            clientB,
            $"/Account/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}");

        FormUrlEncodedContent Build(string password, string antiForgery) => new(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Token"] = token,
            ["NewPassword"] = password,
            ["ConfirmPassword"] = password,
            ["__RequestVerificationToken"] = antiForgery
        });

        var taskA = clientA.PostAsync("/Account/ResetPassword", Build("RacePasswordA1!", antiForgeryA));
        var taskB = clientB.PostAsync("/Account/ResetPassword", Build("RacePasswordB1!", antiForgeryB));
        var responses = await Task.WhenAll(taskA, taskB);

        var redirectCount = responses.Count(r => r.StatusCode == HttpStatusCode.Redirect);
        Assert.Equal(1, redirectCount);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.Null(user.ResetPasswordToken);
        Assert.Null(user.ResetPasswordTokenExpiry);
        var matchedA = BCrypt.Net.BCrypt.Verify("RacePasswordA1!", user.PasswordHash);
        var matchedB = BCrypt.Net.BCrypt.Verify("RacePasswordB1!", user.PasswordHash);
        Assert.True(matchedA ^ matchedB, "Exactly one concurrent reset must consume the token.");
    }

    [Fact]
    public async Task AccessDeniedPage_ReturnsForbiddenWithExplanation_AndAdminStillForbidsStandard()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory,
            TestDataIds.UserId,
            Roles.Standard);

        using var denied = await client.GetAsync("/Account/AccessDenied");
        var deniedBody = await denied.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("không có quyền", deniedBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quay lại", deniedBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Status Code: 404", deniedBody, StringComparison.OrdinalIgnoreCase);

        using var admin = await client.GetAsync("/Admin/Dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
    }

    [Fact]
    public async Task FeatureIdor_StillReturnsNotFound_NotAccessDenied()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory,
            TestDataIds.OutsiderUserId,
            Roles.Standard);

        using var response = await client.GetAsync($"/Home/EditSet/{TestDataIds.UserSetId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Settings_SocialOnlyHash_ShowsUnconfirmedProviderState()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var user = await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
            user.PasswordHash = "SOCIAL_LOGIN_" + Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory,
            TestDataIds.UserId,
            Roles.Standard);

        using var response = await client.GetAsync("/Account/Settings");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(">Đã kết nối</p>", body, StringComparison.Ordinal);
        Assert.Contains("chưa xác nhận", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminUserManagement_JsonMessages_AreValidVietnamese()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory,
            TestDataIds.AdminUserId,
            Roles.Admin);

        using var response = await client.GetAsync("/Admin/UserManagement/GetUser?userId=999999");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("KhÃ´ng", body, StringComparison.Ordinal);
        Assert.Contains("Không tìm thấy người dùng.", body, StringComparison.Ordinal);
    }

    private static async Task SeedUserAsync(
        TestWebApplicationFactory factory,
        string email,
        string passwordHash,
        string? resetToken = null,
        DateTime? resetExpiry = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.Users.Add(new User
        {
            Email = email,
            PasswordHash = passwordHash,
            FullName = "Plan2 Account Fixture",
            Role = Roles.Standard,
            Status = UserStatus.Offline,
            CreatedAt = DateTime.UtcNow,
            ResetPasswordToken = resetToken,
            ResetPasswordTokenExpiry = resetExpiry
        });
        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string email,
        string password,
        string antiForgeryToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["__RequestVerificationToken"] = antiForgeryToken
        });
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        return await client.PostAsync("/Account/Login", content);
    }
}
