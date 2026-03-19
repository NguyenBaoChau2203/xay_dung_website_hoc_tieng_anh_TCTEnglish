using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class Sprint1SmokeTests
{
    [Fact]
    public async Task Landing_AllowsAnonymousUsers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/Home/Landing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_ReturnsOkForAuthenticatedUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var response = await client.GetAsync("/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task Dashboard_RedirectsAnonymousUsersToLanding()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.DoesNotContain("/Home/Index", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Home/FolderDetail/201")]
    [InlineData("/Vocabulary/Detail?setId=302")]
    [InlineData("/Vocabulary/Study?setId=302")]
    [InlineData("/Home/ClassDetail/501")]
    public async Task CoreDetailRoutes_ReturnOkForAuthenticatedUser(string route)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task ClassDetail_RejectsAnonymousUsersSafely()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized
            || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected an auth failure for anonymous class detail, got {(int)response.StatusCode} {response.StatusCode}.");
    }

    [Fact]
    public async Task LearningRecord_RejectsAnonymousRequests()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.PostAsync(
            "/api/LearningApi/record",
            new StringContent("""{"cardId":401,"masteryLevel":"good","timestamp":"2026-03-19T00:00:00Z"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LearningRecord_AcceptsAuthenticatedRequestsWithAntiforgery()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await GetAntiForgeryTokenAsync(client, "/Vocabulary/Study?setId=302");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/LearningApi/record")
        {
            Content = new StringContent(
                """{"cardId":401,"masteryLevel":"good","timestamp":"2026-03-19T00:00:00Z"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"success\":true", payload);
    }

    [Fact]
    public async Task AdminUserList_ReturnsOkForAdminAndForbiddenForStandardUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var standardClient = CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var adminResponse = await adminClient.GetAsync("/Admin/UserManagement");
        var standardResponse = await standardClient.GetAsync("/Admin/UserManagement");
        var adminBody = await adminResponse.Content.ReadAsStringAsync();

        Assert.True(adminResponse.StatusCode == HttpStatusCode.OK, adminBody);
        Assert.Equal(HttpStatusCode.Forbidden, standardResponse.StatusCode);
    }

    [Fact]
    public async Task JoinClass_RejectsWrongPassword()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var antiForgeryToken = await GetAntiForgeryTokenAsync(client, "/Home/Class");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/JoinClass")
        {
            Content = new StringContent(
                $"classId={TestDataIds.ClassId}&password=wrong-password",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task JoinClass_RejectsMissingAntiforgeryToken()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/JoinClass")
        {
            Content = new StringContent(
                $"classId={TestDataIds.ClassId}&password=correct-password",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ClassDetail_HidesPrivateDataFromOutsider()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var response = await client.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("tham gia", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE-CHAT-501", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Sprint One Admin", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Sprint One Folder", body, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"chatMessages\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tab-members", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tab-folders", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatUpload_ReturnsNotFoundForNonMembers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var antiForgeryToken = await GetAntiForgeryTokenAsync(client, "/Home/Class");
        using var imageContent = new ByteArrayContent("fake-image"u8.ToArray());
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        using var formData = new MultipartFormDataContent
        {
            { imageContent, "image", "chat.png" },
            { new StringContent(TestDataIds.ClassId.ToString()), "classId" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Chat/UploadImage")
        {
            Content = formData
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(
        TestWebApplicationFactory factory,
        int userId,
        string role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.EmailHeaderName, $"user{userId}@test.local");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.NameHeaderName, $"User {userId}");

        return client;
    }

    private static async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string route)
    {
        var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Request to '{route}' failed with {(int)response.StatusCode} {response.StatusCode}.{Environment.NewLine}{body}");
        }

        var match = Regex.Match(
            body,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"|value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var token = match.Groups[1].Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = match.Groups[2].Value;
        }

        Assert.False(string.IsNullOrWhiteSpace(token), $"Missing antiforgery token on route {route}.{Environment.NewLine}{body}");
        return token;
    }
}
