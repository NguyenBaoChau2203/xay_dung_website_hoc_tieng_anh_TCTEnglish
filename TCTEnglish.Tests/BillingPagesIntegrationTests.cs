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

    // ─── /Billing/Order/{code} ────────────────────────────────────────────────

    private static int _orderCounter;

    private static async Task<string> SeedOrderAsync(
        TestWebApplicationFactory factory,
        int userId,
        string status,
        string provider = PaymentProviders.MoMo,
        TimeSpan? expiresIn = null,
        string? qr = "00020101021238570010A000000727",
        string? payUrl = "https://pay.example/momo/abc",
        string? deepLink = "momo://app?action=pay",
        string? returnPayload = null,
        bool withSubscription = false,
        string? bank = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var plan = await db.PremiumPlans.FirstAsync(p => p.Code == "premium_yearly");
        var code = $"TCT-TEST-{Interlocked.Increment(ref _orderCounter):D4}";
        var order = new PaymentOrder
        {
            OrderCode = code, UserId = userId, PlanId = plan.Id, Provider = provider, AmountVnd = plan.PriceVnd,
            Currency = "VND", Status = status,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-3), UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAtUtc = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(12)),
            PaidAtUtc = status == PaymentOrderStatuses.Paid ? new DateTime(2026, 11, 17, 6, 45, 0, DateTimeKind.Utc) : null,
            ProviderQrCodePayload = provider == PaymentProviders.MoMo ? qr : null,
            ProviderPaymentUrl = payUrl,
            ProviderDeepLink = provider == PaymentProviders.MoMo ? deepLink : null,
            ProviderTransactionId = status == PaymentOrderStatuses.Paid ? "14872360" : null,
            BankCode = bank,
            ReturnPayloadJson = returnPayload
        };
        db.PaymentOrders.Add(order);
        await db.SaveChangesAsync();
        if (withSubscription)
        {
            db.UserSubscriptions.Add(new UserSubscription
            {
                UserId = userId, PlanId = plan.Id, Status = SubscriptionStatuses.Active,
                StartsAtUtc = new DateTime(2026, 11, 17, 6, 45, 0, DateTimeKind.Utc),
                EndsAtUtc = new DateTime(2027, 11, 17, 6, 45, 0, DateTimeKind.Utc),
                ActivatedByPaymentOrderId = order.Id, CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return code;
    }

    private static HttpClient Learner(TestWebApplicationFactory factory)
        => IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

    [Fact]
    public async Task Order_MoMoWaiting_ShowsTheQrCodeCountdownStepsAndTheSummary()
    {
        await using var factory = await CreateFactoryAsync(momoReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending);
        using var client = Learner(factory);

        var html = await GetAsync(client, $"/Billing/Order/{code}");
        var text = Decode(html);
        var block = PageBlock(html);

        Assert.Contains("tb-stage--waiting", html);
        Assert.Equal(1, Regex.Matches(block, "<h1\\b").Count);
        Assert.Contains("Thanh toán bằng MoMo", text);
        Assert.Contains("Nâng cấp Premium", text);
        Assert.Contains("data-qr=\"00020101021238570010A000000727\"", html);
        Assert.Contains($"aria-label=\"Mã QR thanh toán MoMo cho đơn {code}\"", html);
        Assert.Contains("Mã hết hạn sau", text);
        Assert.Contains("data-tb-countdown", html);
        Assert.Contains("Mở ứng dụng MoMo trên điện thoại", text);
        Assert.Contains("Chọn Quét mã và quét mã QR này", text);
        Assert.Contains($"Xác nhận thanh toán 399.000{Nbsp}₫", text);
        Assert.Contains("Đang chờ bạn thanh toán… trang sẽ tự cập nhật", text);
        Assert.Contains("aria-live=\"polite\"", block);
        Assert.Contains("Mã đơn", text);
        Assert.Contains(code, text);
        Assert.Contains("365 ngày", text);
        Assert.Contains($"399.000{Nbsp}₫", text);
        Assert.Contains("Hủy và chọn cách khác", text);
        Assert.Contains("Sau khi thanh toán xong, bạn có thể được yêu cầu đăng nhập lại để tài khoản nhận quyền Premium.", text);
        Assert.Contains("/lib/pinned/qrcode-1.0.0.min.js", html);
        Assert.DoesNotContain("cdn.jsdelivr.net/npm/qrcodejs", html);
        Assert.Contains("/js/billing-order.js", html);
        Assert.DoesNotContain("<script", block);
        Assert.DoesNotContain(" style=\"", block);
        Assert.DoesNotContain("bi bi-", block);
        Assert.DoesNotContain("IPN", text);
        Assert.Contains("data-tb-app-link", html);
    }

    [Fact]
    public async Task Order_MoMoWaiting_WithoutAQrPayload_SaysSoAndKeepsTheAppButton()
    {
        await using var factory = await CreateFactoryAsync(momoReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending, qr: null);
        using var client = Learner(factory);

        var text = Decode(await GetAsync(client, $"/Billing/Order/{code}"));

        Assert.Contains("Không lấy được mã QR. Hãy bấm Mở ứng dụng MoMo hoặc tạo đơn mới.", text);
        Assert.Contains("Mở ứng dụng MoMo", text);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://insecure.example/pay")]
    [InlineData("data:text/html,x")]
    public async Task Order_NeverRendersAnUnsafeStoredAddressIntoALink(string stored)
    {
        await using var factory = await CreateFactoryAsync(momoReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending, payUrl: stored, deepLink: stored);
        using var client = Learner(factory);

        var html = await GetAsync(client, $"/Billing/Order/{code}");

        Assert.DoesNotContain(stored, WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("data-tb-app-link", html);
    }

    [Fact]
    public async Task Order_MockBox_OnlyWhenTheLocalMockModeIsOn()
    {
        await using var plain = await CreateFactoryAsync(momoReady: true);
        var plainCode = await SeedOrderAsync(plain, TestDataIds.UserId, PaymentOrderStatuses.Pending);
        using var plainClient = Learner(plain);
        var plainText = Decode(await GetAsync(plainClient, $"/Billing/Order/{plainCode}"));

        await using var mocked = await CreateFactoryAsync(momoReady: true, momoMock: true);
        var mockCode = await SeedOrderAsync(mocked, TestDataIds.UserId, PaymentOrderStatuses.Pending);
        using var mockClient = Learner(mocked);
        var mockText = Decode(await GetAsync(mockClient, $"/Billing/Order/{mockCode}"));

        Assert.DoesNotContain("Giả lập thanh toán thành công", plainText);
        Assert.DoesNotContain("Chế độ thử nghiệm", plainText);
        Assert.Contains("Chế độ thử nghiệm (chỉ có ở máy phát triển).", mockText);
        Assert.Contains("Không có tiền thật được chuyển.", mockText);
        Assert.Contains("Giả lập thanh toán thành công", mockText);
    }

    [Fact]
    public async Task Order_MockBox_NeverShowsOnAPaidOrClosedOrder()
    {
        await using var mocked = await CreateFactoryAsync(momoReady: true, momoMock: true);
        var paid = await SeedOrderAsync(mocked, TestDataIds.UserId, PaymentOrderStatuses.Paid, withSubscription: true);
        var cancelled = await SeedOrderAsync(mocked, TestDataIds.UserId, PaymentOrderStatuses.Cancelled);
        using var client = Learner(mocked);

        Assert.DoesNotContain("Giả lập thanh toán thành công", Decode(await GetAsync(client, $"/Billing/Order/{paid}")));
        Assert.DoesNotContain("Giả lập thanh toán thành công", Decode(await GetAsync(client, $"/Billing/Order/{cancelled}")));
    }

    [Fact]
    public async Task Order_VnPayWaiting_OffersToContinueOnVnPay_WithTheStoredAddress()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending, PaymentProviders.VNPay,
            payUrl: "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?x=1");
        using var client = Learner(factory);

        var html = await GetAsync(client, $"/Billing/Order/{code}");
        var text = Decode(html);

        Assert.Contains("Đơn đang chờ thanh toán", text);
        Assert.Contains("Tiếp tục thanh toán trên VNPay", text);
        Assert.Contains("href=\"https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?x=1\"", html);
        Assert.DoesNotContain("data-qr", html);
    }

    [Fact]
    public async Task Order_VnPayWaiting_AfterExpiry_IsNotCompleted_AndOffersToPayAgain()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending, PaymentProviders.VNPay,
            expiresIn: TimeSpan.FromMinutes(-2));
        using var client = Learner(factory);

        var text = Decode(await GetAsync(client, $"/Billing/Order/{code}"));

        Assert.Contains("Thanh toán chưa hoàn tất", text);
        Assert.Contains("Đơn đã hết hạn trước khi được thanh toán.", text);
        Assert.Contains("Thanh toán lại", text);
    }

    [Fact]
    public async Task Order_Confirming_AfterASuccessfulReturn_ExplainsAndOffersARecheck()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending, PaymentProviders.VNPay,
            returnPayload: "{\"vnp_ResponseCode\":\"00\",\"vnp_TransactionStatus\":\"00\"}");
        using var client = Learner(factory);

        var html = await GetAsync(client, $"/Billing/Order/{code}");
        var text = Decode(html);

        Assert.Contains("tb-stage--confirming", html);
        Assert.Contains("Đang xác nhận thanh toán", text);
        Assert.Contains("Chúng tôi đang chờ VNPay xác nhận giao dịch. Việc này thường mất dưới một phút; trang sẽ tự cập nhật.", text);
        Assert.Matches("Đang kiểm tra… lần gần nhất \\d{2}:\\d{2}:\\d{2}", text);
        Assert.Contains("Kiểm tra lại ngay", text);
        Assert.Contains($"action=\"/Billing/Order/{code}/Refresh\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("Đã bị trừ tiền nhưng quá 15 phút chưa được kích hoạt?", text);
        Assert.Contains("href=\"/Home/Contact\"", html);
        Assert.DoesNotContain("IPN", text);
        Assert.DoesNotContain("Manual review", text);
    }

    [Fact]
    public async Task Order_ManualReview_ShowsThePlainSentence_WithoutPollingOrAButton()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.ManualReview, PaymentProviders.VNPay);
        using var client = Learner(factory);

        var html = await GetAsync(client, $"/Billing/Order/{code}");
        var text = Decode(html);

        Assert.Contains("Đơn đang được kiểm tra thêm. Chúng tôi sẽ cập nhật trong thời gian sớm nhất.", text);
        Assert.DoesNotContain("Kiểm tra lại ngay", text);
        Assert.Contains("data-manual-review=\"true\"", html);
        Assert.DoesNotContain("manual", text.Replace("manual-review", string.Empty), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Order_Paid_ShowsTheReceiptEndDateAndTheUnlockedFeatures()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Paid, PaymentProviders.VNPay,
            withSubscription: true, bank: "NCB");
        using var client = Learner(factory);

        var html = await GetAsync(client, $"/Billing/Order/{code}");
        var text = Decode(html);

        Assert.Contains("tb-stage--paid", html);
        Assert.Contains("Thanh toán thành công", text);
        Assert.Contains("Premium của bạn có hiệu lực đến", text);
        Assert.Contains("<strong>17/11/2027</strong>", text);
        Assert.Contains(code, text);
        Assert.Contains("Premium 1 năm", text);
        Assert.Contains($"399.000{Nbsp}₫", text);
        Assert.Contains("VNPay · NCB", text);
        Assert.Contains("17/11/2026 13:45", text); // 06:45 UTC in Vietnam time
        Assert.Contains("14872360", text);
        foreach (var highlight in PremiumBenefits.UnlockedHighlights)
        {
            Assert.Contains(highlight, text);
        }

        Assert.Contains("Bắt đầu học", text);
        Assert.Contains("href=\"/Home/Index\"", html);
        Assert.Contains("Xem lịch sử thanh toán", text);
        Assert.DoesNotContain("data-follows=\"true\"", html);
    }

    [Theory]
    [InlineData(PaymentOrderStatuses.Cancelled, "Bạn đã hủy giao dịch trên VNPay.", true)]
    [InlineData(PaymentOrderStatuses.Failed, "Giao dịch không thành công.", true)]
    [InlineData(PaymentOrderStatuses.Expired, "Đơn đã hết hạn trước khi được thanh toán.", true)]
    [InlineData(PaymentOrderStatuses.Refunded, "Khoản thanh toán này đã được hoàn lại.", false)]
    public async Task Order_NotCompleted_SaysWhy_NeverClaimsNoMoneyWasTaken(string status, string sentence, bool canPayAgain)
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, status, PaymentProviders.VNPay, expiresIn: TimeSpan.FromMinutes(-30));
        using var client = Learner(factory);

        var html = await GetAsync(client, $"/Billing/Order/{code}");
        var text = Decode(html);

        Assert.Contains("Thanh toán chưa hoàn tất", text);
        Assert.Contains(sentence, text);
        Assert.Contains("Nếu tài khoản của bạn đã bị trừ tiền, hãy liên hệ hỗ trợ kèm mã đơn.", text);
        Assert.DoesNotContain("chưa bị trừ tiền", text);
        Assert.Equal(canPayAgain, text.Contains("Thanh toán lại"));
        Assert.Contains("Xem lịch sử thanh toán", text);
        Assert.DoesNotContain("data-follows=\"true\"", html);
    }

    [Fact]
    public async Task Order_ForSomeoneElse_Is404_AndAnUnknownCodeToo()
    {
        await using var factory = await CreateFactoryAsync(momoReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending);
        using var outsider = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        using var other = await outsider.GetAsync($"/Billing/Order/{code}");
        using var missing = await outsider.GetAsync("/Billing/Order/NOPE");
        using var status = await outsider.GetAsync($"/Billing/OrderStatus?orderCode={code}");

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
    }

    [Fact]
    public async Task Order_ForAnAnonymousVisitor_RedirectsToLogin()
    {
        await using var factory = await CreateFactoryAsync(momoReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending);
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync($"/Billing/Order/{code}");

        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, response.StatusCode.ToString());
    }

    [Fact]
    public async Task OldMoMoQrLinks_RedirectToTheOrderPage()
    {
        await using var factory = await CreateFactoryAsync(momoReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending);
        using var client = Learner(factory);

        using var response = await client.GetAsync($"/Billing/MoMoQr?orderCode={code}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Billing/Order/{code}", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task AnUnverifiableReturn_ShowsTheGenericPage_WithNoOrderData()
    {
        await using var factory = await CreateFactoryAsync(vnpayReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending, PaymentProviders.VNPay);
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync($"/Billing/VnPayReturn?vnp_TxnRef={code}&vnp_ResponseCode=00&vnp_SecureHash=bad");
        var html = await response.Content.ReadAsStringAsync();
        var text = Decode(html);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Không xác minh được kết quả thanh toán", text);
        Assert.Contains("Hãy mở Lịch sử thanh toán để xem trạng thái đơn của bạn.", text);
        var block = PageBlock(html);
        Assert.DoesNotContain(code, Decode(block));
        Assert.DoesNotContain("399.000", Decode(block));
        Assert.DoesNotContain("IPN", Decode(block));
    }

    [Fact]
    public async Task TheMockFlow_PaysTheOrder_AndTheOrderPageShowsIt()
    {
        await using var factory = await CreateFactoryAsync(momoReady: true, momoMock: true);
        using var client = Learner(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Premium");

        using var checkout = await client.PostAsync("/Billing/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PlanCode"] = "premium_yearly", ["Provider"] = "momo", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, checkout.StatusCode);
        var orderUrl = checkout.Headers.Location!.OriginalString;
        Assert.StartsWith("/Billing/Order/", orderUrl);
        var code = orderUrl["/Billing/Order/".Length..];

        var page = await GetAsync(client, orderUrl);
        Assert.Contains("Giả lập thanh toán thành công", Decode(page));
        var pageToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, orderUrl);

        using var confirm = await client.PostAsync("/Billing/MoMoMockConfirm", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["orderCode"] = code, ["__RequestVerificationToken"] = pageToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        Assert.Equal(orderUrl, confirm.Headers.Location!.OriginalString);

        var paid = Decode(await GetAsync(client, orderUrl));
        Assert.Contains("Thanh toán thành công", paid);
        Assert.Contains("Premium của bạn có hiệu lực đến", paid);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var order = await db.PaymentOrders.SingleAsync(o => o.OrderCode == code);
        Assert.Equal(PaymentOrderStatuses.Paid, order.Status);
        Assert.NotNull(order.ActivatedAtUtc);
    }

    [Fact]
    public async Task Refresh_ByTheOwner_ReturnsJson_AndByAnotherUser_Is404()
    {
        await using var factory = await CreateFactoryAsync(momoReady: true);
        var code = await SeedOrderAsync(factory, TestDataIds.UserId, PaymentOrderStatuses.Pending, PaymentProviders.MoMo);
        using var owner = Learner(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(owner, $"/Billing/Order/{code}");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Billing/Order/{code}/Refresh");
        request.Headers.Add("RequestVerificationToken", token);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var ok = await owner.SendAsync(request);
        var json = await ok.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("\"stage\":\"waiting\"", json);

        using var outsider = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);
        var otherToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(outsider, "/Premium");
        using var otherRequest = new HttpRequestMessage(HttpMethod.Post, $"/Billing/Order/{code}/Refresh");
        otherRequest.Headers.Add("RequestVerificationToken", otherToken);
        using var denied = await outsider.SendAsync(otherRequest);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

        using var noToken = new HttpRequestMessage(HttpMethod.Post, $"/Billing/Order/{code}/Refresh");
        using var rejected = await owner.SendAsync(noToken);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }
}
