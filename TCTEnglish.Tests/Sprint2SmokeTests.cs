using System.Net;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Controllers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class Sprint2SmokeTests
{
    [Fact]
    public async Task JoinLeaveClass_RoundTripSucceedsForOutsider()
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
        var joinPayload = await joinResponse.Content.ReadAsStringAsync();
        using (var joinDocument = JsonDocument.Parse(joinPayload))
        {
            Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
            Assert.Equal(
                $"/Home/ClassDetail/{TestDataIds.ClassId}",
                joinDocument.RootElement.GetProperty("redirectUrl").GetString());
        }

        Assert.Equal(1, await CountClassMembershipsAsync(factory, TestDataIds.ClassId, TestDataIds.OutsiderUserId));

        using var joinedResponse = await client.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");
        var joinedBody = await joinedResponse.Content.ReadAsStringAsync();
        AssertPrivateClassDetail(joinedResponse.StatusCode, joinedBody);

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

        using var outsiderResponse = await client.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");
        var outsiderBody = await outsiderResponse.Content.ReadAsStringAsync();
        AssertSanitizedOutsiderClassDetail(outsiderResponse.StatusCode, outsiderBody);
    }

    [Fact]
    public async Task ClassDetail_OutsiderGetsSanitizedView()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        using var response = await client.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");
        var body = await response.Content.ReadAsStringAsync();

        AssertSanitizedOutsiderClassDetail(response.StatusCode, body);
    }

    [Fact]
    public async Task ClassDetail_AdminGetsPrivateViewWithoutMembership()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        Assert.Equal(0, await CountClassMembershipsAsync(factory, TestDataIds.ClassId, TestDataIds.AdminUserId));

        using var response = await client.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");
        var body = await response.Content.ReadAsStringAsync();

        AssertPrivateClassDetail(response.StatusCode, body);
        Assert.Contains("Sprint Two Member", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassDetail_MemberGetsPrivateView()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.MemberUserId, Roles.Standard);

        Assert.Equal(1, await CountClassMembershipsAsync(factory, TestDataIds.ClassId, TestDataIds.MemberUserId));

        using var response = await client.GetAsync($"/Home/ClassDetail/{TestDataIds.ClassId}");
        var body = await response.Content.ReadAsStringAsync();

        AssertPrivateClassDetail(response.StatusCode, body);
        Assert.Contains("Sprint Two Member", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassManagement_AdminCanKickMembersAndRemoveSharedFolders()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        Assert.Equal(1, await CountClassMembershipsAsync(factory, TestDataIds.ClassId, TestDataIds.MemberUserId));
        Assert.Equal(1, await CountClassFoldersAsync(factory, TestDataIds.ClassId, TestDataIds.UserFolderId));

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/ClassDetail/{TestDataIds.ClassId}");

        using var kickRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/KickMember")
        {
            Content = new StringContent(
                $"classId={TestDataIds.ClassId}&userId={TestDataIds.MemberUserId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        kickRequest.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var kickResponse = await client.SendAsync(kickRequest);

        Assert.Equal(HttpStatusCode.OK, kickResponse.StatusCode);
        Assert.Equal(0, await CountClassMembershipsAsync(factory, TestDataIds.ClassId, TestDataIds.MemberUserId));

        var removeToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/ClassDetail/{TestDataIds.ClassId}");
        using var removeRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/RemoveFolderFromClass")
        {
            Content = new StringContent(
                $"classId={TestDataIds.ClassId}&folderId={TestDataIds.UserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        removeRequest.Headers.Add("RequestVerificationToken", removeToken);

        using var removeResponse = await client.SendAsync(removeRequest);

        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        Assert.Equal(0, await CountClassFoldersAsync(factory, TestDataIds.ClassId, TestDataIds.UserFolderId));
    }

    [Fact]
    public async Task ChatUpload_OutsiderGetsNotFound()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/ClassDetail/{TestDataIds.ClassId}");
        var uploadResult = await UploadChatImageAsync(client, antiForgeryToken);

        Assert.Equal(HttpStatusCode.NotFound, uploadResult.StatusCode);
        Assert.Null(uploadResult.ImageUrl);
    }

    [Fact]
    public async Task ChatUpload_AdminCanUploadImage()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/ClassDetail/{TestDataIds.ClassId}");
        string? imageUrl = null;

        try
        {
            var uploadResult = await UploadChatImageAsync(client, antiForgeryToken);
            imageUrl = uploadResult.ImageUrl;

            Assert.Equal(HttpStatusCode.OK, uploadResult.StatusCode);
            Assert.NotNull(imageUrl);
            Assert.StartsWith("/uploads/chat/", imageUrl, StringComparison.Ordinal);
        }
        finally
        {
            DeleteUploadedChatFile(factory, imageUrl);
        }
    }

    [Fact]
    public async Task ChatUpload_MemberCanUploadImage()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.MemberUserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/ClassDetail/{TestDataIds.ClassId}");
        string? imageUrl = null;

        try
        {
            var uploadResult = await UploadChatImageAsync(client, antiForgeryToken);
            imageUrl = uploadResult.ImageUrl;

            Assert.Equal(HttpStatusCode.OK, uploadResult.StatusCode);
            Assert.NotNull(imageUrl);
            Assert.StartsWith("/uploads/chat/", imageUrl, StringComparison.Ordinal);
        }
        finally
        {
            DeleteUploadedChatFile(factory, imageUrl);
        }
    }

    [Fact]
    public async Task LearningRecord_RejectsMissingAntiforgeryToken()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/LearningApi/record")
        {
            Content = new StringContent(
                """{"cardId":401,"masteryLevel":"good","timestamp":"2026-03-19T00:00:00Z"}""",
                Encoding.UTF8,
                "application/json")
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LearningRecord_AcceptsAuthenticatedRequestsWithAntiforgery()
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
        Assert.Contains("\"success\":true", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LearningRecord_UpdatesUserStreakAndReturnsCurrentCount()
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

        using (var document = JsonDocument.Parse(payload))
        {
            Assert.Equal(1, document.RootElement.GetProperty("streak").GetInt32());
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        Assert.Equal(1, user.Streak);
        Assert.Equal(DateTime.UtcNow.Date, user.LastStudyDate?.Date);
    }

    [Fact]
    public void DependencyInjection_ResolvesRegisteredStreakService()
    {
        using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var streakService = scope.ServiceProvider.GetService<IStreakService>();

        Assert.NotNull(streakService);
        Assert.IsType<StreakService>(streakService);
    }

    [Fact]
    public void DependencyInjection_ResolvesRegisteredFileStorageService()
    {
        using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var fileStorageService = scope.ServiceProvider.GetService<IFileStorageService>();

        Assert.NotNull(fileStorageService);
        Assert.IsType<LocalFileStorageService>(fileStorageService);
    }

    [Fact]
    public void StreakConsumers_RequireConstructorInjectedService()
    {
        AssertRequiresNonOptionalService(typeof(HomeController), typeof(IStreakService));
        AssertRequiresNonOptionalService(typeof(LearningApiController), typeof(IStreakService));
    }

    [Fact]
    public void FileStorageConsumers_RequireConstructorInjectedService()
    {
        AssertRequiresNonOptionalService(typeof(ChatController), typeof(IFileStorageService));
        AssertRequiresNonOptionalService(typeof(SetController), typeof(IFileStorageService));
        AssertRequiresNonOptionalService(typeof(ClassService), typeof(IFileStorageService));
        AssertRequiresNonOptionalService(typeof(AvatarUploadService), typeof(IFileStorageService));
    }

    private static async Task<int> CountClassMembershipsAsync(
        TestWebApplicationFactory factory,
        int classId,
        int userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        return await context.ClassMembers.CountAsync(cm => cm.ClassId == classId && cm.UserId == userId);
    }

    private static async Task<int> CountClassFoldersAsync(
        TestWebApplicationFactory factory,
        int classId,
        int folderId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        return await context.ClassFolders.CountAsync(cf => cf.ClassId == classId && cf.FolderId == folderId);
    }

    private static async Task<(HttpStatusCode StatusCode, string Payload, string? ImageUrl)> UploadChatImageAsync(
        HttpClient client,
        string antiForgeryToken)
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
        string? imageUrl = null;

        if (response.IsSuccessStatusCode)
        {
            using var document = JsonDocument.Parse(payload);
            imageUrl = document.RootElement.GetProperty("imageUrl").GetString();
        }

        return (response.StatusCode, payload, imageUrl);
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

    private static void AssertPrivateClassDetail(HttpStatusCode statusCode, string body)
    {
        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.Contains("id=\"chatMessages\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tab-members", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tab-folders", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIVATE-CHAT-501", body, StringComparison.Ordinal);
        Assert.Contains("Sprint One Folder", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>Hãy tham gia nhóm</h2>", body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Bạn cần tham gia lớp học này để xem nội dung chat, thư mục và thành viên.",
            body,
            StringComparison.Ordinal);
    }

    private static void AssertSanitizedOutsiderClassDetail(HttpStatusCode statusCode, string body)
    {
        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.Contains("<h2>Hãy tham gia nhóm</h2>", body, StringComparison.Ordinal);
        Assert.Contains(
            "Bạn cần tham gia lớp học này để xem nội dung chat, thư mục và thành viên.",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"chatMessages\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tab-members", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tab-folders", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE-CHAT-501", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Sprint One Folder", body, StringComparison.Ordinal);
    }

    private static void AssertRequiresNonOptionalService(Type targetType, Type serviceType)
    {
        var serviceParameter = targetType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Single(parameter => parameter.ParameterType == serviceType);

        Assert.False(serviceParameter.IsOptional);
        Assert.False(serviceParameter.HasDefaultValue);
    }
}
