using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
    [Fact]
    public async Task WritingPractice_RendersServerSummaryTargets_ForLiveAttemptAndStatusUpdates()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedWritingExerciseAsync(factory);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=9901");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("id=\"exerciseStatusLabel\" aria-live=\"polite\">Chưa bắt đầu</span>", html);
        Assert.Contains("id=\"exerciseAttemptCount\">0 lượt gửi</span>", html);
    }

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

    private static async Task UpdateUserRoleAsync(TestWebApplicationFactory factory, int userId, string role)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(item => item.UserId == userId);
        user.Role = role;
        await context.SaveChangesAsync();
    }

    private static async Task SeedPrivateExercisesAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var user = await context.Users.SingleAsync(item => item.UserId == TestDataIds.UserId);
        user.Role = Roles.Premium;

        var exOpenAi = await context.WritingExercises.FindAsync(9911);
        if (exOpenAi == null)
        {
            context.WritingExercises.Add(new WritingExercise
            {
                Id = 9911,
                UserId = TestDataIds.UserId,
                Title = "Bài viết từ OpenAI",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Workplace",
                SourceType = "user",
                PreviewText = "Huong dan viet email",
                IsPublished = false,
                CreatedAt = DateTime.UtcNow,
                InputMode = "from_brief",
                GenerationMethod = "openai"
            });
            context.WritingExerciseSentences.Add(new WritingExerciseSentence
            {
                Id = 99111,
                WritingExerciseId = 9911,
                SortOrder = 1,
                VietnameseText = "Câu 1 OpenAI",
                EnglishMeaning = "Sentence 1 OpenAI"
            });
        }

        var exTemplate = await context.WritingExercises.FindAsync(9912);
        if (exTemplate == null)
        {
            context.WritingExercises.Add(new WritingExercise
            {
                Id = 9912,
                UserId = TestDataIds.UserId,
                Title = "Bài viết từ khung nội bộ",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Workplace",
                SourceType = "user",
                PreviewText = "Huong dan viet email theo khung",
                IsPublished = false,
                CreatedAt = DateTime.UtcNow,
                InputMode = "from_brief",
                GenerationMethod = "internal_template",
                TemplateKey = "leave-request",
                TemplateVersion = 1
            });
            context.WritingExerciseSentences.Add(new WritingExerciseSentence
            {
                Id = 99121,
                WritingExerciseId = 9912,
                SortOrder = 1,
                VietnameseText = "Câu 1 Template",
                EnglishMeaning = "Sentence 1 Template"
            });
        }

        var exLegacy = await context.WritingExercises.FindAsync(9913);
        if (exLegacy == null)
        {
            context.WritingExercises.Add(new WritingExercise
            {
                Id = 9913,
                UserId = TestDataIds.UserId,
                Title = "Bài viết cá nhân cũ",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Workplace",
                SourceType = "user",
                PreviewText = "Huong dan viet cu",
                IsPublished = false,
                CreatedAt = DateTime.UtcNow,
                InputMode = null,
                GenerationMethod = null
            });
            context.WritingExerciseSentences.Add(new WritingExerciseSentence
            {
                Id = 99131,
                WritingExerciseId = 9913,
                SortOrder = 1,
                VietnameseText = "Câu 1 Legacy",
                EnglishMeaning = "Sentence 1 Legacy"
            });
        }

        await context.SaveChangesAsync();
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

        // Verify breadcrumb links to /Home/Writing
        Assert.Contains("<a href=\"/Home/Writing\">Trang chủ</a>", html);

        // Verify filter toolbar is positioned after personal section and before system catalog
        var personalIndex = html.IndexOf("data-writing-personal", StringComparison.Ordinal);
        var filterIndex = html.IndexOf("data-writing-filter-form", StringComparison.Ordinal);
        var catalogIndex = html.IndexOf("id=\"system-catalog-heading\"", StringComparison.Ordinal);

        Assert.True(personalIndex >= 0 && personalIndex < filterIndex, "Personal section must precede filter toolbar");
        Assert.True(filterIndex < catalogIndex, "Filter toolbar must precede system catalog");

        // Verify topic chip rendered with icon and distinct color class
        Assert.Matches(@"<span class=""writing-card-chip writing-card-chip--[a-z0-9-]+""[^>]*>\s*<i class=""fa-solid fa-[a-z0-9-]+", html);
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

    [Fact]
    public async Task WritingExercises_RendersTwoCreationModes_TabsControlsAndOfferCard()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);
        await SeedWritingExerciseAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);
        using var response = await client.GetAsync("/Home/Writing/Exercises");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        // Accessible two creation modes tabs
        Assert.Contains("data-writing-create-form", html);
        Assert.Contains("data-writing-mode-input", html);
        Assert.Contains("data-mode-tab=\"from_brief\"", html);
        Assert.Contains("data-mode-tab=\"from_text\"", html);
        Assert.Contains("data-mode-panel=\"from_brief\"", html);
        Assert.Contains("data-mode-panel=\"from_text\"", html);
        Assert.Contains("Mô tả bài muốn luyện", html);
        Assert.Contains("Dán bài có sẵn", html);

        // Brief input and live character feedback targets
        Assert.Contains("data-brief-input", html);
        Assert.Contains("data-brief-char-counter", html);
        Assert.Contains("data-writing-char-summary", html);

        // Meaningful example chips
        Assert.Contains("data-example-chip", html);
        Assert.Contains("Xin nghỉ phép (Cơ bản - Email)", html);

        // Controls grid
        Assert.Contains("data-brief-control=\"level\"", html);
        Assert.Contains("data-brief-control=\"contentType\"", html);
        Assert.Contains("data-brief-control=\"length\"", html);
        Assert.Contains("data-brief-control=\"tone\"", html);

        // Template offer card and action targets
        Assert.Contains("data-template-offer-card", html);
        Assert.Contains("data-offer-accept-check", html);
        Assert.Contains("data-offer-confirm-btn", html);
        Assert.Contains("data-offer-cancel-btn", html);
        Assert.Contains("data-offer-catalog-btn", html);
    }

    [Fact]
    public async Task WritingExercises_RendersOriginChipsForPrivateCards()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedPrivateExercisesAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);
        using var response = await client.GetAsync("/Home/Writing/Exercises");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        // Three origin variants rendered in learner's private section
        Assert.Contains("writing-card-chip--origin", html);
        Assert.Contains("Bài viết cá nhân", html);
        Assert.Contains("Tạo theo khung song ngữ nội bộ", html);
        Assert.Contains("Nguồn: Chưa xác định", html);
    }

    [Fact]
    public async Task WritingPractice_RendersOriginStatPill_ForPrivateExercise()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedPrivateExercisesAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);

        // 1. OpenAI exercise
        using (var responseOpenAi = await client.GetAsync("/Home/Writing/Practice?exerciseId=9911"))
        {
            Assert.Equal(HttpStatusCode.OK, responseOpenAi.StatusCode);
            var htmlOpenAi = await responseOpenAi.Content.ReadAsStringAsync();
            Assert.Contains("stat-pill--origin", htmlOpenAi);
            Assert.Contains("Bài viết cá nhân", htmlOpenAi);
        }

        // 2. Internal template exercise
        using (var responseTemplate = await client.GetAsync("/Home/Writing/Practice?exerciseId=9912"))
        {
            Assert.Equal(HttpStatusCode.OK, responseTemplate.StatusCode);
            var htmlTemplate = await responseTemplate.Content.ReadAsStringAsync();
            Assert.Contains("stat-pill--origin", htmlTemplate);
            Assert.Contains("Tạo theo khung song ngữ nội bộ", htmlTemplate);
        }

        // 3. Legacy exercise without generation method
        using (var responseLegacy = await client.GetAsync("/Home/Writing/Practice?exerciseId=9913"))
        {
            Assert.Equal(HttpStatusCode.OK, responseLegacy.StatusCode);
            var htmlLegacy = await responseLegacy.Content.ReadAsStringAsync();
            Assert.Contains("stat-pill--origin", htmlLegacy);
            Assert.Contains("Nguồn: Chưa xác định", htmlLegacy);
        }
    }
}
