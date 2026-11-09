using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Verifies HTTP/Razor rendering, accessibility, and form contracts for the redesigned Claymorphism Writing module.
/// Covers learner surfaces (WR01, WR02, WR03) and Admin surfaces (WA01, WA02, WA03).
/// </summary>
public sealed class WritingUiRenderTests
{
    private static async Task SeedWritingExerciseAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var existing = await context.WritingExercises.FindAsync(9901);
        if (existing == null)
        {
            var exercise = new WritingExercise
            {
                Id = 9901,
                Title = "Email xin nghi phep",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Workplace",
                SourceType = "system",
                PreviewText = "Huong dan viet email xin nghi phep chuyen nghiep",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            };
            context.WritingExercises.Add(exercise);

            context.WritingExerciseSentences.AddRange(
                new WritingExerciseSentence
                {
                    Id = 99011,
                    WritingExerciseId = 9901,
                    SortOrder = 1,
                    VietnameseText = "Toi xin phep nghi 2 ngay.",
                    EnglishMeaning = "I would like to request two days off.",
                    BreakAfter = false
                },
                new WritingExerciseSentence
                {
                    Id = 99012,
                    WritingExerciseId = 9901,
                    SortOrder = 2,
                    VietnameseText = "Toi se ban giao cong viec cho Nam.",
                    EnglishMeaning = "I will hand over my tasks to Nam.",
                    BreakAfter = false
                }
            );

            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task WritingIndex_RendersClaymorphismTheme_AndSingleH1()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Home/Writing");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("tct-theme", html);
        Assert.Contains("writing-index-page", html);
        Assert.Contains("writing-banner-clay.svg", html);

        // Verify single H1
        var h1Matches = Regex.Matches(html, @"<h1[\s>]", RegexOptions.IgnoreCase);
        Assert.Single(h1Matches);
        Assert.Contains("Kỹ năng Viết", html);
    }

    [Fact]
    public async Task WritingExercises_RendersCatalog_AndToolbarFilters()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedWritingExerciseAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Home/Writing/Exercises");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("tct-theme", html);
        Assert.Contains("writing-exercises-page", html);
        Assert.Contains("data-writing-filter-form", html);
        Assert.Contains("data-filter-select", html);

        // Verify single H1
        var h1Matches = Regex.Matches(html, @"<h1[\s>]", RegexOptions.IgnoreCase);
        Assert.Single(h1Matches);
        Assert.Contains("Danh sách bài tập viết", html);
    }

    [Fact]
    public async Task WritingPractice_RendersWorkspace_AndPreservesRequiredDomIds()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedWritingExerciseAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Home/Writing/Practice?exerciseId=9901");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("tct-theme", html);
        Assert.Contains("writing-practice-page", html);
        Assert.Contains("data-writing-practice", html);
        Assert.Contains("id=\"answerInput\"", html);
        Assert.Contains("id=\"btnSubmit\"", html);
        Assert.Contains("id=\"btnHint\"", html);
        Assert.Contains("id=\"feedbackBanner\"", html);
        Assert.Contains("id=\"writingPracticeData\"", html);
        Assert.Contains("__RequestVerificationToken", html);

        // Verify single H1 (visually hidden or visible)
        var h1Matches = Regex.Matches(html, @"<h1[\s>]", RegexOptions.IgnoreCase);
        Assert.Single(h1Matches);
    }

    [Fact]
    public async Task AdminWritingIndex_RendersAdminCatalog_WithStatCardsAndTable()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedWritingExerciseAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var response = await client.GetAsync("/Admin/WritingExerciseManagement");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("tct-theme", html);
        Assert.Contains("admin-writing-page", html);
        Assert.Contains("admin-writing-stat-card", html);
        Assert.Contains("exerciseTable", html);
        Assert.Contains("deleteExerciseModal", html);

        var h1Matches = Regex.Matches(html, @"<h1[\s>]", RegexOptions.IgnoreCase);
        Assert.Single(h1Matches);
        Assert.Contains("Quản lý Writing Exercises", html);
    }

    [Fact]
    public async Task AdminWritingCreate_RendersBilingualBuilder_AndAntiforgery()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var response = await client.GetAsync("/Admin/WritingExerciseManagement/Create");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("tct-theme", html);
        Assert.Contains("writingExerciseForm", html);
        Assert.Contains("FullVietnameseText", html);
        Assert.Contains("FullEnglishText", html);
        Assert.Contains("autoPairList", html);
        Assert.Contains("__RequestVerificationToken", html);

        var h1Matches = Regex.Matches(html, @"<h1[\s>]", RegexOptions.IgnoreCase);
        Assert.Single(h1Matches);
        Assert.Contains("Thêm Writing Exercise", html);
    }
}
