using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsPhase5IntegrationTests
{
    [Fact]
    public async Task GoalsPage_ShowsDeferredAreas_AndDoesNotRenderReadingListeningOptions()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Goals");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Reading, Listening hiện đang tạm hoãn", body, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"Reading\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"Listening\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateGoal_DeferredAreaSubmission_ReturnsValidationError_AndDoesNotCreateGoal()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Goals");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Goals/UpdateGoal")
        {
            Content = new StringContent(
                "GoalEditor.GoalArea=Reading&GoalEditor.TargetValue=5",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var readingGoalCount = await context.UserGoals
            .AsNoTracking()
            .CountAsync(goal => goal.UserId == TestDataIds.UserId && goal.GoalArea == GoalArea.Reading && goal.IsActive);

        Assert.Equal(0, readingGoalCount);
    }

    [Fact]
    public async Task ReadingAndListeningPages_ShowExplicitDeferredGateCopy()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var readingResponse = await client.GetAsync("/Home/Reading");
        var readingBody = await readingResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, readingResponse.StatusCode);
        Assert.Contains("Reading hiện đang ở trạng thái tạm hoãn trong hệ thống Goals/XP", readingBody, StringComparison.Ordinal);

        using var listeningResponse = await client.GetAsync("/Home/Listening");
        var listeningBody = await listeningResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, listeningResponse.StatusCode);
        Assert.Contains("Listening hiện đang ở trạng thái tạm hoãn trong hệ thống Goals/XP", listeningBody, StringComparison.Ordinal);
    }
}
