using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Rendered billing pages (plan sections 5.1–5.3). Text is asserted on the HTML-decoded page; Razor writes
/// non-ASCII letters as numeric entities. The layout itself carries scripts and styles, so inline-block checks
/// look only at the billing page block.
/// </summary>
public class BillingPagesIntegrationTests
{
    private const string PageMarker = "tct-theme tct-page tb-page";
    private static readonly string Nbsp = " ";

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync(
        bool vnpayReady = false, bool momoReady = false, bool momoMock = false)
    {
        var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IPaymentProviderHealthService>();
            var health = new Mock<IPaymentProviderHealthService>();
            health.Setup(h => h.GetProviderHealthStatus()).Returns(new List<ProviderHealthViewModel>
            {
                new() { ProviderCode = PaymentProviders.VNPay, DisplayName = "VNPay", Enabled = vnpayReady, Configured = vnpayReady },
                new() { ProviderCode = PaymentProviders.MoMo, DisplayName = "MoMo", Enabled = momoReady, Configured = momoReady }
            });
            services.AddSingleton(health.Object);

            if (momoMock)
            {
                // Local mock mode (Development only): the real MoMo gateway becomes usable without credentials.
                services.Configure<TCTEnglish.Services.Billing.MoMo.MoMoOptions>(o =>
                {
                    o.Enabled = true;
                    o.MockModeEnabled = true;
                    o.MockSecretKey = "test-only-mock-key";
                });
            }
        });
        await factory.InitializeAsync();
        await SeedPlansAsync(factory);
        return factory;
    }

    private static async Task SeedPlansAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        if (await db.PremiumPlans.AnyAsync())
        {
            return;
        }

        db.PremiumPlans.AddRange(
            new PremiumPlan { Code = "premium_monthly", Name = "Premium 1 tháng", Description = "d", PriceVnd = 49000m, DurationDays = 30, IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow },
            new PremiumPlan { Code = "premium_yearly", Name = "Premium 1 năm", Description = "d", PriceVnd = 399000m, DurationDays = 365, IsActive = true, DisplayOrder = 2, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private static string Decode(string html) => WebUtility.HtmlDecode(html);

    private static async Task<string> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} -> {(int)response.StatusCode}\n{body[..Math.Min(body.Length, 500)]}");
        return body;
    }

    /// <summary>The billing page block: from its wrapper to the end of the layout's &lt;main&gt;.</summary>
    private static string PageBlock(string html)
    {
        var start = html.IndexOf(PageMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "billing page wrapper not found");
        var end = html.IndexOf("</main>", start, StringComparison.Ordinal);
        Assert.True(end > start, "end of <main> not found");
        return html[start..end];
    }

    private static string BlockBeforeScripts(string html) => PageBlock(html);

    // ─── /Premium ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pricing_ForAFreeLearner_ShowsOneFormWithBothChoiceGroupsAndTheToken()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true, momoReady: true);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await GetAsync(client, "/Premium");
        var text = Decode(html);
        var block = BlockBeforeScripts(html);

        Assert.Contains("class=\"tct-theme tct-page tb-page tb-page--pricing\"", html);
        Assert.Contains("Nâng cấp Premium", text);
        Assert.Contains("Thanh toán một lần cho mỗi gói. Không tự động gia hạn.", text);
        Assert.Contains("Bạn đang dùng gói Miễn phí", text);
        Assert.Contains("Nâng cấp để mở các tính năng dùng AI và nội dung riêng của bạn.", text);
        Assert.Equal(1, Regex.Matches(block, "<form\\b").Count);
        Assert.Contains("name=\"__RequestVerificationToken\"", block);
        Assert.Equal(2, Regex.Matches(block, "name=\"PlanCode\"").Count);
        Assert.Equal(2, Regex.Matches(block, "name=\"Provider\"").Count);
        Assert.Contains("1. Chọn gói", text);
        Assert.Contains("2. Thanh toán", text);
        Assert.Contains("Lịch sử thanh toán", text);
        Assert.Contains("<fieldset", block);
        Assert.Contains("<legend", block);
    }

    [Fact]
    public async Task Pricing_PlansShowMoneyPerMonthAndSaving_AndTheLongestPlanIsPreselected()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await GetAsync(client, "/Premium");
        var text = Decode(html);

        Assert.Contains($"49.000{Nbsp}₫", text);
        Assert.Contains($"399.000{Nbsp}₫", text);
        Assert.Contains("Dùng trong 30 ngày", text);
        Assert.Contains($"Dùng trong 365 ngày · khoảng 33.250{Nbsp}₫/tháng", text);
        Assert.Contains("Tiết kiệm 32%", text);
        Assert.Equal(1, Regex.Matches(text, "Tiết kiệm").Count);
        var yearly = Regex.Match(html, "<input[^>]*name=\"PlanCode\"[^>]*value=\"premium_yearly\"[^>]*>");
        Assert.Contains("checked", yearly.Value);
        var monthly = Regex.Match(html, "<input[^>]*name=\"PlanCode\"[^>]*value=\"premium_monthly\"[^>]*>");
        Assert.DoesNotContain("checked", monthly.Value);
    }

    [Fact]
    public async Task Pricing_SummaryShowsThePreselectedPlan_AndTheButtonWorksWithoutScript()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var text = Decode(await GetAsync(client, "/Premium"));

        Assert.Contains("Tổng thanh toán", text);
        Assert.Matches("365 ngày, đến \\d{2}/\\d{2}/\\d{4}", text);
        Assert.Matches(">\\s*Thanh toán\\s*<", text);
        Assert.Contains("Bạn sẽ được chuyển sang trang của VNPay để hoàn tất. TCT English không lưu thông tin thẻ của bạn.", text);
        Assert.Contains("Sau khi thanh toán xong, bạn có thể được yêu cầu đăng nhập lại để tài khoản nhận quyền Premium.", text);
    }

    [Fact]
    public async Task Pricing_ComparisonTable_IsARealTableWithTextAlternativesAndOnlyTrueClaims()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await GetAsync(client, "/Premium");
        var text = Decode(html);

        Assert.Contains("<table", html);
        Assert.Contains("<caption", html);
        Assert.Contains("scope=\"col\"", html);
        Assert.Contains("scope=\"row\"", html);
        Assert.Contains("Premium có thêm gì", text);
        Assert.Contains("Tính năng", text);
        Assert.Contains("Miễn phí", text);
        Assert.Contains("Từ vựng, bộ thẻ, luyện tập, ôn theo lịch", text);
        Assert.Contains("Bài nghe, bài đọc, bài nói có sẵn", text);
        Assert.Contains("15 lượt mỗi ngày", text);
        Assert.Contains("Không giới hạn lượt", text);
        Assert.Contains(">Có<", html);
        Assert.Contains(">Không có<", html);
        foreach (var benefit in PremiumBenefits.Gated)
        {
            Assert.Contains(benefit.Title, text);
        }

        Assert.DoesNotContain("quảng cáo", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pricing_UsesFontAwesome_NoBootstrapIcons_NoInternalWords_NoInlineStyles()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true, momoReady: true);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await GetAsync(client, "/Premium");
        var block = BlockBeforeScripts(html);

        Assert.DoesNotContain("bi bi-", block);
        Assert.Contains("fa-solid fa-", block);
        Assert.DoesNotContain("IPN", Decode(block));
        Assert.DoesNotContain(" style=\"", block);
        Assert.DoesNotContain("<style", block);
        Assert.DoesNotContain("<script", block);
        Assert.Contains("/css/billing.css", html);
        Assert.Contains("/js/billing-core.js", html);
        Assert.Contains("/js/billing-pricing.js", html);
    }

    [Fact]
    public async Task Pricing_MethodRows_OnlyAvailableGatewaysAreSelectable()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true, momoReady: false);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await GetAsync(client, "/Premium");
        var text = Decode(html);

        var vnpay = Regex.Match(html, "<input[^>]*name=\"Provider\"[^>]*value=\"vnpay\"[^>]*>").Value;
        var momo = Regex.Match(html, "<input[^>]*name=\"Provider\"[^>]*value=\"momo\"[^>]*>").Value;
        Assert.Contains("checked", vnpay);
        Assert.DoesNotContain("disabled", vnpay);
        Assert.Contains("disabled", momo);
        Assert.DoesNotContain("checked", momo);
        Assert.Contains("Thẻ ATM, QR ngân hàng, thẻ quốc tế", text);
        Assert.Contains("Tạm thời chưa dùng được", text);
        Assert.DoesNotContain("chưa cấu hình", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pricing_WithNoGateway_ShowsTheNoticeAndNoPayButton()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: false, momoReady: false);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await GetAsync(client, "/Premium");

        Assert.Contains("Cổng thanh toán đang tạm ngưng. Vui lòng quay lại sau.", Decode(html));
        Assert.DoesNotContain("data-tb-submit", html);
        Assert.DoesNotContain("name=\"Provider\"", html);
    }

    [Fact]
    public async Task Pricing_InLocalMockMode_MoMoCarriesTheTestChip_AndIsPreselectedWhenItIsTheOnlyOne()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: false, momoReady: true, momoMock: true);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var html = await GetAsync(client, "/Premium");
        var text = Decode(html);

        Assert.Contains("Thử nghiệm", text);
        var momo = Regex.Match(html, "<input[^>]*name=\"Provider\"[^>]*value=\"momo\"[^>]*>").Value;
        Assert.Contains("checked", momo);
        Assert.Contains("Bạn sẽ được chuyển sang trang của MoMo", text);
        Assert.Contains("data-tb-submit", html);
    }

    [Fact]
    public async Task Pricing_ForAPremiumLearner_ShowsTheEndDateAndDaysLeft_AndStillOffersToExtend()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        var ends = DateTime.UtcNow.AddDays(100).AddHours(1);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var user = await db.Users.FirstAsync(u => u.UserId == TestDataIds.UserId);
            user.Role = Roles.Premium;
            var plan = await db.PremiumPlans.FirstAsync();
            db.UserSubscriptions.Add(new UserSubscription
            {
                UserId = user.UserId, PlanId = plan.Id, Status = SubscriptionStatuses.Active,
                StartsAtUtc = DateTime.UtcNow.AddDays(-5), EndsAtUtc = ends, CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);
        var html = await GetAsync(client, "/Premium");
        var text = Decode(html);

        Assert.Contains("Bạn đang dùng Premium", text);
        Assert.Contains($"Có hiệu lực đến {BillingTime.ToDisplayDate(ends)} (còn 101 ngày). Mua thêm sẽ được cộng nối vào hạn này.", text);
        Assert.Contains("data-tb-submit", html);
        // The new end date counts from the current end date.
        Assert.Contains($"365 ngày, đến {BillingTime.ToDisplayDate(ends.AddDays(365))}", text);
    }

    [Fact]
    public async Task Pricing_ForAnAdmin_HasNoPayButtonAndNoForm()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true, momoReady: true);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var html = await GetAsync(client, "/Premium");
        var block = BlockBeforeScripts(html);
        var text = Decode(block);

        Assert.Contains("Tài khoản của bạn có quyền Premium không thời hạn.", text);
        Assert.DoesNotContain("<form", block);
        Assert.DoesNotContain("data-tb-submit", block);
        Assert.Contains("Premium có thêm gì", text);
    }

    [Fact]
    public async Task Pricing_ShowsACheckoutError_AboveTheColumns_AsAnAlert_AfterAFailedCheckout()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true, momoReady: true, momoMock: true);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Premium");

        using var post = await client.PostAsync("/Billing/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PlanCode"] = "does_not_exist", ["Provider"] = "momo", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        var html = await GetAsync(client, "/Premium");

        Assert.Contains("data-tb-error", html);
        Assert.Contains("role=\"alert\"", html);
        Assert.Contains("Gói Premium không tồn tại.", Decode(html));
    }

    [Fact]
    public async Task Checkout_ByAnAdmin_IsRefused_WithTheServerSideMessage_AndCreatesNoOrder()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true, momoReady: true, momoMock: true);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Premium");

        using var post = await client.PostAsync("/Billing/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PlanCode"] = "premium_yearly", ["Provider"] = "momo", ["__RequestVerificationToken"] = token
        }));
        var html = await GetAsync(client, "/Premium");

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains("Tài khoản quản trị đã có sẵn quyền Premium.", Decode(html));
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<DbflashcardContext>().PaymentOrders);
    }

    [Fact]
    public async Task Checkout_WithoutTheAntiforgeryToken_IsRejected()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true, momoReady: true, momoMock: true);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var post = await client.PostAsync("/Billing/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PlanCode"] = "premium_yearly", ["Provider"] = "momo"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    [Fact]
    public async Task Pricing_ForAnAnonymousVisitor_RedirectsToLogin()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync("/Premium");

        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, response.StatusCode.ToString());
    }
}
