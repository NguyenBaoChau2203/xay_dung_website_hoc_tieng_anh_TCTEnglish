using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class FolderSetIdorRegressionTests
{
    private const string OwnedFolderName = "Sprint One Folder";
    private const string DeletableFolderName = "Sprint One Delete Folder";
    private const string OwnedSetName = "Sprint One User Set";
    private const string OwnedSetDescription = "User-owned set";

    [Fact]
    public async Task FolderMutations_ReturnNotFoundForOutsider_AndLeaveFolderUnchanged()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var updateToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Folder");
        using var updateRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/UpdateFolderName")
        {
            Content = new StringContent(
                $"folderId={TestDataIds.UserFolderId}&newName=Outsider Rename Attempt",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        updateRequest.Headers.Add("RequestVerificationToken", updateToken);

        using var updateResponse = await client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        var deleteToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Folder");
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/DeleteFolder")
        {
            Content = new StringContent(
                $"id={TestDataIds.UserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        deleteRequest.Headers.Add("RequestVerificationToken", deleteToken);

        using var deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        var folder = await FindFolderAsync(factory, TestDataIds.UserFolderId);

        Assert.NotNull(folder);
        Assert.Equal(OwnedFolderName, folder!.FolderName);
        Assert.Equal(TestDataIds.UserId, folder.UserId);
    }

    [Fact]
    public async Task SetDeleteMutations_ReturnNotFoundForOutsider_AndLeaveSetUnchanged()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var deleteToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/CreateSet");
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/DeleteSet")
        {
            Content = new StringContent(
                $"setId={TestDataIds.UserSetId}&folderId={TestDataIds.UserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        deleteRequest.Headers.Add("RequestVerificationToken", deleteToken);

        using var deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        var removeToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/CreateSet");
        using var removeRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/RemoveSetFromFolder")
        {
            Content = new StringContent(
                $"setId={TestDataIds.UserSetId}&folderId={TestDataIds.UserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        removeRequest.Headers.Add("RequestVerificationToken", removeToken);

        using var removeResponse = await client.SendAsync(removeRequest);

        Assert.Equal(HttpStatusCode.NotFound, removeResponse.StatusCode);

        var set = await FindSetAsync(factory, TestDataIds.UserSetId);

        Assert.NotNull(set);
        Assert.Equal(TestDataIds.UserId, set!.OwnerId);
        Assert.Equal(TestDataIds.UserFolderId, set.FolderId);
        Assert.Equal(OwnedSetName, set.SetName);
    }

    [Fact]
    public async Task EditSet_GetAndPost_ReturnNotFoundForOutsider_AndLeaveSetUnchanged()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        using var getResponse = await client.GetAsync($"/Home/EditSet/{TestDataIds.UserSetId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var editToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/CreateSet");
        using var editRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/EditSet")
        {
            Content = new StringContent(
                $"SetId={TestDataIds.UserSetId}&SetName=Outsider Edit Attempt&Description=blocked&Terms=term&Definitions=definition&ExistingImageUrls=",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        editRequest.Headers.Add("RequestVerificationToken", editToken);

        using var editResponse = await client.SendAsync(editRequest);

        Assert.Equal(HttpStatusCode.NotFound, editResponse.StatusCode);

        var set = await FindSetAsync(factory, TestDataIds.UserSetId);

        Assert.NotNull(set);
        Assert.Equal(OwnedSetName, set!.SetName);
        Assert.Equal(OwnedSetDescription, set.Description);
        Assert.Single(set.Cards);
    }

    [Fact]
    public async Task CreateSet_ReturnsNotFoundWhenPostingIntoAnotherUsersFolder()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Home/CreateSet?folderId={TestDataIds.UserFolderId}");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/CreateSet")
        {
            Content = new StringContent(
                $"folderId={TestDataIds.UserFolderId}&SetName=Unauthorized Create Attempt&Description=blocked&Terms=term&Definitions=definition",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var createdSetCount = await context.Sets
            .AsNoTracking()
            .CountAsync(set =>
                set.OwnerId == TestDataIds.OutsiderUserId &&
                set.FolderId == TestDataIds.UserFolderId &&
                set.SetName == "Unauthorized Create Attempt");

        Assert.Equal(0, createdSetCount);
    }

    [Fact]
    public async Task OwnerCanStillRenameFolder_AndReadWriteEditSet_OnLegacyHomeRoutes()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var editPageResponse = await client.GetAsync($"/Home/EditSet/{TestDataIds.UserSetId}");
        var editPageBody = await editPageResponse.Content.ReadAsStringAsync();

        Assert.True(editPageResponse.StatusCode == HttpStatusCode.OK, editPageBody);
        Assert.Contains(OwnedSetName, editPageBody, StringComparison.Ordinal);

        var renameToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Folder");
        using var renameRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/UpdateFolderName")
        {
            Content = new StringContent(
                $"folderId={TestDataIds.UserFolderId}&newName=Renamed Sprint Folder",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        renameRequest.Headers.Add("RequestVerificationToken", renameToken);

        using var renameResponse = await client.SendAsync(renameRequest);

        AssertRedirectPath(renameResponse, $"/Home/FolderDetail/{TestDataIds.UserFolderId}");

        var editToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/Home/EditSet/{TestDataIds.UserSetId}");
        using var editRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/EditSet")
        {
            Content = new StringContent(
                $"SetId={TestDataIds.UserSetId}&SetName=Renamed Sprint Set&Description=Updated description&Terms=forecast&Definitions=predict&ExistingImageUrls=",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        editRequest.Headers.Add("RequestVerificationToken", editToken);

        using var editResponse = await client.SendAsync(editRequest);

        AssertRedirectPath(editResponse, $"/Home/FolderDetail/{TestDataIds.UserFolderId}");

        var folder = await FindFolderAsync(factory, TestDataIds.UserFolderId);
        var set = await FindSetAsync(factory, TestDataIds.UserSetId);

        Assert.NotNull(folder);
        Assert.Equal("Renamed Sprint Folder", folder!.FolderName);
        Assert.NotNull(set);
        Assert.Equal("Renamed Sprint Set", set!.SetName);
        Assert.Equal("Updated description", set.Description);
        Assert.Single(set.Cards);
    }

    [Fact]
    public async Task OwnerCanStillDeleteOwnedFolder_OnLegacyHomeRoute()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var deleteToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Folder");
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/DeleteFolder")
        {
            Content = new StringContent(
                $"id={TestDataIds.DeletableUserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        deleteRequest.Headers.Add("RequestVerificationToken", deleteToken);

        using var deleteResponse = await client.SendAsync(deleteRequest);

        AssertRedirectPath(deleteResponse, "/Home/Folder");

        var deletedFolder = await FindFolderAsync(factory, TestDataIds.DeletableUserFolderId);

        Assert.Null(deletedFolder);
    }

    [Fact]
    public async Task OwnerCanStillDeleteOwnedSet_OnLegacyHomeRoute()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var deleteToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/CreateSet");
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/DeleteSet")
        {
            Content = new StringContent(
                $"setId={TestDataIds.UserSetId}&folderId={TestDataIds.UserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        deleteRequest.Headers.Add("RequestVerificationToken", deleteToken);

        using var deleteResponse = await client.SendAsync(deleteRequest);

        AssertRedirectPath(deleteResponse, $"/Home/FolderDetail/{TestDataIds.UserFolderId}");

        var deletedSet = await FindSetAsync(factory, TestDataIds.UserSetId);

        Assert.Null(deletedSet);
    }

    [Fact]
    public async Task FolderMutations_RejectMissingAntiforgeryToken_AndLeaveDataUnchanged()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var renameRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/UpdateFolderName")
        {
            Content = new StringContent(
                $"folderId={TestDataIds.UserFolderId}&newName=Blocked Rename Attempt",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };

        using var renameResponse = await client.SendAsync(renameRequest);

        Assert.Equal(HttpStatusCode.BadRequest, renameResponse.StatusCode);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/DeleteFolder")
        {
            Content = new StringContent(
                $"id={TestDataIds.DeletableUserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };

        using var deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        var renamedFolder = await FindFolderAsync(factory, TestDataIds.UserFolderId);
        var undeletedFolder = await FindFolderAsync(factory, TestDataIds.DeletableUserFolderId);

        Assert.NotNull(renamedFolder);
        Assert.Equal(OwnedFolderName, renamedFolder!.FolderName);
        Assert.NotNull(undeletedFolder);
        Assert.Equal(DeletableFolderName, undeletedFolder!.FolderName);
    }

    [Fact]
    public async Task SetMutations_RejectMissingAntiforgeryToken_AndLeaveDataUnchanged()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/CreateSet")
        {
            Content = new StringContent(
                $"folderId={TestDataIds.UserFolderId}&SetName=Blocked Create Attempt&Description=blocked&Terms=term&Definitions=definition",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };

        using var createResponse = await client.SendAsync(createRequest);

        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);

        using var editRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/EditSet")
        {
            Content = new StringContent(
                $"SetId={TestDataIds.UserSetId}&SetName=Blocked Edit Attempt&Description=blocked&Terms=term&Definitions=definition&ExistingImageUrls=",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };

        using var editResponse = await client.SendAsync(editRequest);

        Assert.Equal(HttpStatusCode.BadRequest, editResponse.StatusCode);

        using var removeRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/RemoveSetFromFolder")
        {
            Content = new StringContent(
                $"setId={TestDataIds.UserSetId}&folderId={TestDataIds.UserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };

        using var removeResponse = await client.SendAsync(removeRequest);

        Assert.Equal(HttpStatusCode.BadRequest, removeResponse.StatusCode);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/DeleteSet")
        {
            Content = new StringContent(
                $"setId={TestDataIds.UserSetId}&folderId={TestDataIds.UserFolderId}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };

        using var deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        var set = await FindSetAsync(factory, TestDataIds.UserSetId);

        Assert.NotNull(set);
        Assert.Equal(OwnedSetName, set!.SetName);
        Assert.Equal(OwnedSetDescription, set.Description);
        Assert.Equal(TestDataIds.UserFolderId, set.FolderId);
        Assert.Single(set.Cards);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var createdSetCount = await context.Sets
            .AsNoTracking()
            .CountAsync(candidate =>
                candidate.OwnerId == TestDataIds.UserId &&
                candidate.FolderId == TestDataIds.UserFolderId &&
                candidate.SetName == "Blocked Create Attempt");

        Assert.Equal(0, createdSetCount);
    }

    private static async Task<Folder?> FindFolderAsync(TestWebApplicationFactory factory, int folderId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        return await context.Folders
            .AsNoTracking()
            .FirstOrDefaultAsync(folder => folder.FolderId == folderId);
    }

    private static async Task<Set?> FindSetAsync(TestWebApplicationFactory factory, int setId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        return await context.Sets
            .AsNoTracking()
            .Include(set => set.Cards)
            .FirstOrDefaultAsync(set => set.SetId == setId);
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
