using System.Net;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace TCTEnglish.Tests;

public sealed class Pr140ReadingHttpReviewTests
{
    [Fact]
    public async Task AdminArchive_UnpublishesWithoutDeletingLearnerHistory()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        int passageId;
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var passage = new ReadingPassage { Title = "Referenced reading", Content = "Preserve history", Level = "A1",
                IsPublished = true, Status = "Published", CreatedAt = DateTime.UtcNow };
            db.ReadingPassages.Add(passage);
            await db.SaveChangesAsync();
            passageId = passage.Id;
            db.UserReadingHistories.Add(new UserReadingHistory { UserId = TestDataIds.UserId, ReadingPassageId = passageId,
                IsCompleted = true, Score = 4, ViewedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Admin);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Admin/ReadingManagement/Create");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/ReadingManagement/Archive")
        { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = passageId.ToString() }) };
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var kept = await context.ReadingPassages.SingleAsync(p => p.Id == passageId);
        Assert.Equal("Archived", kept.Status);
        Assert.False(kept.IsPublished);
        Assert.Equal(4, (await context.UserReadingHistories.SingleAsync(h => h.ReadingPassageId == passageId)).Score);
    }

    [Fact]
    public async Task AdminEditor_UsesNativeValidationWithoutOrphanJQueryPlugins()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Admin);
        using var page = await client.GetAsync("/Admin/ReadingManagement/Create");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/lib/jquery-validation/", html, StringComparison.Ordinal);
        var title = System.Text.RegularExpressions.Regex.Match(html, "<input\\b[^>]*id=\"Title\"[^>]*>").Value;
        var content = System.Text.RegularExpressions.Regex.Match(html, "<textarea\\b[^>]*id=\"Content\"[^>]*>").Value;
        Assert.Matches("\\brequired(?:\\s|=|>)", title);
        Assert.Matches("\\brequired(?:\\s|=|>)", content);
    }
}
