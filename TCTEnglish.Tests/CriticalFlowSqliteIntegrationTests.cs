using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class CriticalFlowSqliteIntegrationTests
{
    [Fact]
    public async Task Landing_AllowsAnonymousUsers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync("/Home/Landing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_RequiresAuthentication_AndLoadsForSignedInUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var anonymousClient = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var authenticatedClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var anonymousResponse = await anonymousClient.GetAsync("/Home/Index");
        using var authenticatedResponse = await authenticatedClient.GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
    }

    [Fact]
    public async Task SpeakingMode_RequiresAuthentication_AndLoadsForSignedInUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var anonymousClient = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var authenticatedClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var anonymousResponse = await anonymousClient.GetAsync($"/Home/Speaking/{TestDataIds.UserSetId}");
        using var authenticatedResponse = await authenticatedClient.GetAsync($"/Home/Speaking/{TestDataIds.UserSetId}");

        Assert.True(
            anonymousResponse.StatusCode == HttpStatusCode.Redirect
            || anonymousResponse.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected an auth failure for anonymous speaking mode, got {(int)anonymousResponse.StatusCode} {anonymousResponse.StatusCode}.");
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
    }

    [Theory]
    [InlineData("/Home/WriteMode/301")]
    [InlineData("/Home/QuizMode/301")]
    [InlineData("/Home/MatchingMode/301")]
    public async Task SecondaryStudyModes_RequireAuthentication_AndLoadForSignedInUser(string route)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var anonymousClient = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var authenticatedClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var anonymousResponse = await anonymousClient.GetAsync(route);
        using var authenticatedResponse = await authenticatedClient.GetAsync(route);

        Assert.True(
            anonymousResponse.StatusCode == HttpStatusCode.Redirect
            || anonymousResponse.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected an auth failure for anonymous route '{route}', got {(int)anonymousResponse.StatusCode} {anonymousResponse.StatusCode}.");
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
    }

    [Fact]
    public async Task LegacyHomeAuthRoutes_RedirectToAccountActions()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var loginResponse = await client.GetAsync("/Home/Login");
        using var registerResponse = await client.GetAsync("/Home/Register");

        AssertRedirectPath(loginResponse, "/Account/Login");
        AssertRedirectPath(registerResponse, "/Account/Register");
    }

    [Fact]
    public async Task FolderDetail_ReturnsOkForOwner()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync($"/Home/FolderDetail/{TestDataIds.UserFolderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/Vocabulary/Detail?setId=302")]
    [InlineData("/Vocabulary/Study?setId=302")]
    public async Task SetDetailAndStudy_ReturnOkForAuthenticatedUser(string route)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ClassDetail_IsMembershipSensitive()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var outsiderClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);
        using var memberClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.MemberUserId, Roles.Standard);

        using var outsiderResponse = await outsiderClient.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");
        using var memberResponse = await memberClient.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");
        var outsiderBody = await outsiderResponse.Content.ReadAsStringAsync();
        var memberBody = await memberResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, outsiderResponse.StatusCode);
        Assert.Contains("Hãy tham gia nhóm", outsiderBody, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-CHAT-501", outsiderBody, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, memberResponse.StatusCode);
        Assert.Contains("PRIVATE-CHAT-501", memberBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LearningRecord_PersistsProgress_AndReturnsStreak()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Vocabulary/Study?setId=302");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/LearningApi/record")
        {
            Content = new StringContent(
                """{"cardId":401,"masteryLevel":"good","timestamp":"2026-03-19T00:00:00Z"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payloadDocument = JsonDocument.Parse(payload);
        Assert.True(payloadDocument.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(1, payloadDocument.RootElement.GetProperty("streak").GetInt32());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var progress = await context.LearningProgresses
            .AsNoTracking()
            .SingleAsync(lp => lp.UserId == TestDataIds.UserId && lp.CardId == TestDataIds.UserCardId);

        Assert.Equal("Reviewing", progress.Status);
        Assert.True(progress.RepetitionCount > 0);
    }

    [Fact]
    public async Task AdminUserList_IsRoleProtected()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var standardClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var adminResponse = await adminClient.GetAsync("/Admin/UserManagement");
        using var standardResponse = await standardClient.GetAsync("/Admin/UserManagement");

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, standardResponse.StatusCode);
    }

    [Fact]
    public async Task JoinLeaveClass_UpdatesMembershipRelation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        Assert.Equal(0, await CountClassMembershipsAsync(factory, TestDataIds.ClassId, TestDataIds.OutsiderUserId));

        var joinToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Class");
        using var joinRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/JoinClass")
        {
            Content = new StringContent(
                $"classId={TestDataIds.ClassId}&password=correct-password",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        joinRequest.Headers.Add("RequestVerificationToken", joinToken);

        using var joinResponse = await client.SendAsync(joinRequest);

        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
        Assert.Equal(1, await CountClassMembershipsAsync(factory, TestDataIds.ClassId, TestDataIds.OutsiderUserId));

        var leaveToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/ClassDetail/{TestDataIds.ClassId}");
        using var leaveRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/LeaveClass")
        {
            Content = new StringContent(
                $"classId={TestDataIds.ClassId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        leaveRequest.Headers.Add("RequestVerificationToken", leaveToken);

        using var leaveResponse = await client.SendAsync(leaveRequest);

        Assert.Equal(HttpStatusCode.OK, leaveResponse.StatusCode);
        Assert.Equal(0, await CountClassMembershipsAsync(factory, TestDataIds.ClassId, TestDataIds.OutsiderUserId));
    }

    [Fact]
    public async Task SaveFolderAndUnsaveFolder_UpdateSavedFolderRelation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        Assert.Equal(0, await CountSavedFoldersAsync(factory, TestDataIds.OutsiderUserId, TestDataIds.UserFolderId));

        var saveToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/FolderDetail/{TestDataIds.UserFolderId}");
        using var saveRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/SaveFolder")
        {
            Content = new StringContent(
                $"folderId={TestDataIds.UserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        saveRequest.Headers.Add("RequestVerificationToken", saveToken);

        using var saveResponse = await client.SendAsync(saveRequest);

        AssertRedirectPath(saveResponse, $"/Home/FolderDetail/{TestDataIds.UserFolderId}");
        Assert.Equal(1, await CountSavedFoldersAsync(factory, TestDataIds.OutsiderUserId, TestDataIds.UserFolderId));

        var unsaveToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/FolderDetail/{TestDataIds.UserFolderId}");
        using var unsaveRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/UnsaveFolder")
        {
            Content = new StringContent(
                $"folderId={TestDataIds.UserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        unsaveRequest.Headers.Add("RequestVerificationToken", unsaveToken);

        using var unsaveResponse = await client.SendAsync(unsaveRequest);

        AssertRedirectPath(unsaveResponse, $"/Home/FolderDetail/{TestDataIds.UserFolderId}");
        Assert.Equal(0, await CountSavedFoldersAsync(factory, TestDataIds.OutsiderUserId, TestDataIds.UserFolderId));
    }

    [Fact]
    public async Task ChatUpload_RequiresClassAccess()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var outsiderClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);
        using var memberClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.MemberUserId, Roles.Standard);

        var outsiderToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(outsiderClient, "/Home/Class");
        var outsiderUpload = await UploadChatImageAsync(outsiderClient, outsiderToken);

        Assert.Equal(HttpStatusCode.NotFound, outsiderUpload.StatusCode);

        var memberToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(memberClient, $"/Home/ClassDetail/{TestDataIds.ClassId}");
        string? uploadedImageUrl = null;

        try
        {
            var memberUpload = await UploadChatImageAsync(memberClient, memberToken);
            uploadedImageUrl = memberUpload.ImageUrl;

            Assert.Equal(HttpStatusCode.OK, memberUpload.StatusCode);
            Assert.NotNull(memberUpload.ImageUrl);
            Assert.StartsWith("/uploads/chat/", memberUpload.ImageUrl, StringComparison.Ordinal);
        }
        finally
        {
            DeleteUploadedChatFile(factory, uploadedImageUrl);
        }
    }

    private static async Task<int> CountClassMembershipsAsync(TestWebApplicationFactory factory, int classId, int userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        return await context.ClassMembers
            .AsNoTracking()
            .CountAsync(cm => cm.ClassId == classId && cm.UserId == userId);
    }

    private static async Task<int> CountSavedFoldersAsync(TestWebApplicationFactory factory, int userId, int folderId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        return await context.SavedFolders
            .AsNoTracking()
            .CountAsync(sf => sf.UserId == userId && sf.FolderId == folderId);
    }

    private static async Task<(HttpStatusCode StatusCode, string? ImageUrl)> UploadChatImageAsync(HttpClient client, string antiForgeryToken)
    {
        using var formData = IntegrationTestClientHelper.CreatePngUploadForm(
            "image",
            "chat.png",
            "classId",
            TestDataIds.ClassId.ToString());

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Chat/UploadImage")
        {
            Content = formData
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return (response.StatusCode, null);
        }

        using var payloadDocument = JsonDocument.Parse(payload);
        return (response.StatusCode, payloadDocument.RootElement.GetProperty("imageUrl").GetString());
    }

    private static void DeleteUploadedChatFile(TestWebApplicationFactory factory, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        var environment = factory.Services.GetRequiredService<IWebHostEnvironment>();
        var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(environment.WebRootPath, relativePath);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }

    private static void AssertRedirectPath(HttpResponseMessage response, string expectedPath)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var location = response.Headers.Location!;
        var actualPath = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;

        Assert.Equal(expectedPath, actualPath);
    }
}
