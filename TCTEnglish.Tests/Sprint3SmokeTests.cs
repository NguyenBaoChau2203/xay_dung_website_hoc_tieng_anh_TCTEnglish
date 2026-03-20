using System.Net;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Controllers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class Sprint3SmokeTests
{
    [Fact]
    public async Task VocabularyPages_RenderSystemVocabularyForAuthenticatedUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var routes = new[]
        {
            "/Vocabulary/Index",
            $"/Vocabulary/FolderDetail?folderId={TestDataIds.SystemFolderId}",
            $"/Vocabulary/Detail?setId={TestDataIds.SystemSetId}",
            $"/Vocabulary/Topics?setId={TestDataIds.SystemSetId}",
            $"/Vocabulary/TopicDetail?setId={TestDataIds.SystemSetId}",
            $"/Vocabulary/Study?setId={TestDataIds.SystemSetId}"
        };

        foreach (var route in routes)
        {
            using var response = await client.GetAsync(route);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Expected 200 for '{route}' but got {(int)response.StatusCode} ({response.StatusCode}).");
        }

        var indexBody = await client.GetStringAsync("/Vocabulary/Index");
        var detailBody = await client.GetStringAsync($"/Vocabulary/Detail?setId={TestDataIds.SystemSetId}");

        Assert.Contains("System Sprint Folder", indexBody, StringComparison.Ordinal);
        Assert.Contains("Sprint One System Set", detailBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VocabularyPages_KeepUserOwnedDataOutOfSystemCatalog()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var detailResponse = await client.GetAsync($"/Vocabulary/Detail?setId={TestDataIds.UserSetId}");
        using var folderResponse = await client.GetAsync($"/Vocabulary/FolderDetail?folderId={TestDataIds.UserFolderId}");

        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, folderResponse.StatusCode);
    }

    [Fact]
    public void DependencyInjection_ResolvesRegisteredStudyService()
    {
        using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var studyService = scope.ServiceProvider.GetService<IStudyService>();

        Assert.NotNull(studyService);
        Assert.IsType<StudyService>(studyService);
    }

    [Fact]
    public void VocabularyController_RequiresConstructorInjectedStudyService()
    {
        AssertRequiresNonOptionalService(typeof(VocabularyController), typeof(IStudyService));
    }

    private static void AssertRequiresNonOptionalService(Type targetType, Type serviceType)
    {
        var constructor = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(ctor => ctor.GetParameters().Length)
            .First();

        var parameter = constructor.GetParameters()
            .SingleOrDefault(candidate => candidate.ParameterType == serviceType);

        Assert.NotNull(parameter);
        Assert.False(parameter!.HasDefaultValue);
    }
}
