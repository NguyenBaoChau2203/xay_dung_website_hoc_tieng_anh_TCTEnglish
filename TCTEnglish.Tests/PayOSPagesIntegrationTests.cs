using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.PayOS;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>Rendered payOS pages: the method choice on /Premium, the order page states and the local mock page.</summary>
public class PayOSPagesIntegrationTests
{
    private const string PayOsCode = "261120130509123";
    private static int _counter;

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync(
        bool payOsReady = true, bool vnpayReady = true, bool payOsMock = false)
    {
        var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IPaymentProviderHealthService>();
            var health = new Mock<IPaymentProviderHealthService>();
            health.Setup(h => h.GetProviderHealthStatus()).Returns(new List<ProviderHealthViewModel>
            {
                new() { ProviderCode = PaymentProviders.PayOS, DisplayName = "payOS", Enabled = payOsReady, Configured = payOsReady },
                new() { ProviderCode = PaymentProviders.VNPay, DisplayName = "VNPay", Enabled = vnpayReady, Configured = vnpayReady },
                new() { ProviderCode = PaymentProviders.MoMo, DisplayName = "MoMo", Enabled = false, Configured = false }
            });
            services.AddSingleton(health.Object);

            if (payOsMock)
            {
                services.Configure<PayOSOptions>(o =>
                {
                    o.Enabled = true;
                    o.MockMode = true;
                });
            }
        });
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        if (!await db.PremiumPlans.AnyAsync())
        {
            db.PremiumPlans.AddRange(
                new PremiumPlan { Code = "premium_monthly", Name = "Premium 1 tháng", Description = "d", PriceVnd = 49000m, DurationDays = 30, IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow },
                new PremiumPlan { Code = "premium_yearly", Name = "Premium 1 năm", Description = "d", PriceVnd = 399000m, DurationDays = 365, IsActive = true, DisplayOrder = 2, CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        return factory;
    }

    private static string Decode(string html) => WebUtility.HtmlDecode(html);

    private static async Task<(HttpStatusCode Status, string Body)> TryGetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<string> GetAsync(HttpClient client, string url)
    {
        var (status, body) = await TryGetAsync(client, url);
        Assert.True(status == HttpStatusCode.OK, $"{url} -> {(int)status}\n{body[..Math.Min(body.Length, 400)]}");
        return body;
    }

    private static HttpClient Learner(TestWebApplicationFactory factory)
        => IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

    private static async Task<string> SeedPayOsOrderAsync(
        TestWebApplicationFactory factory,
        string status,
        string? payUrl = "https://pay.payos.vn/web/abc123",
        TimeSpan? expiresIn = null,
        int userId = TestDataIds.UserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var plan = await db.PremiumPlans.FirstAsync(p => p.Code == "premium_yearly");
        var code = $"TCT-PAYOS-{Interlocked.Increment(ref _counter):D4}";
        db.PaymentOrders.Add(new PaymentOrder
        {
            OrderCode = code,
            UserId = userId,
            PlanId = plan.Id,
            Provider = PaymentProviders.PayOS,
            ProviderRequestId = PayOsCode,
            AmountVnd = plan.PriceVnd,
            Currency = "VND",
            Status = status,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-3),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAtUtc = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(12)),
            ProviderPaymentUrl = payUrl
        });
        await db.SaveChangesAsync();
        return code;
    }

    // ─── /Premium ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pricing_OffersPayOSFirst_PreselectedWithItsWording_AndHidesAnUnavailableMethod()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Learner(factory);

        var html = await GetAsync(client, "/Premium");
        var text = Decode(html);

        var values = Regex.Matches(html, "<input[^>]*name=\"Provider\"[^>]*value=\"([a-z]+)\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(new[] { "payos", "vnpay" }, values);
        Assert.Contains("checked", Regex.Match(html, "<input[^>]*name=\"Provider\"[^>]*value=\"payos\"[^>]*>").Value);
        Assert.DoesNotContain("checked", Regex.Match(html, "<input[^>]*name=\"Provider\"[^>]*value=\"vnpay\"[^>]*>").Value);
        Assert.Contains("Chuyển khoản QR (payOS)", text);
        Assert.Contains("Quét mã bằng app ngân hàng bất kỳ", text);
        Assert.Contains("Bạn sẽ được chuyển sang trang của payOS để hoàn tất.", text);
        Assert.DoesNotContain("Ví MoMo", text);
        Assert.DoesNotContain("Tạm thời chưa dùng được", text);
        Assert.Contains("data-name=\"payOS\"", html);
    }

    [Fact]
    public async Task Pricing_WithPayOSNotReady_DoesNotShowIt()
    {
        await using var factory = await CreateFactoryAsync(payOsReady: false);
        using var client = Learner(factory);

        var html = await GetAsync(client, "/Premium");

        Assert.DoesNotContain("value=\"payos\"", html);
        Assert.DoesNotContain("payOS", Decode(html));
        Assert.Contains("value=\"vnpay\"", html);
    }

    [Fact]
    public async Task Pricing_PayOSMock_CarriesTheTestChip()
    {
        await using var factory = await CreateFactoryAsync(payOsMock: true);
        using var client = Learner(factory);

        var text = Decode(await GetAsync(client, "/Premium"));

        Assert.Contains("Thử nghiệm", text);
    }

    // ─── /Billing/Order/{code} ────────────────────────────────────────────────

    [Fact]
    public async Task Order_PayOSWaiting_OffersToReopenThePaymentPage_AndSaysThePageUpdatesByItself()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Learner(factory);
        var code = await SeedPayOsOrderAsync(factory, PaymentOrderStatuses.Pending);

        var html = await GetAsync(client, $"/Billing/Order/{code}");
        var text = Decode(html);

        Assert.Contains("Thanh toán bằng chuyển khoản QR", text);
        Assert.Contains("Mở lại trang thanh toán", text);
        Assert.Contains("href=\"https://pay.payos.vn/web/abc123\"", html);
        Assert.Contains("Sau khi chuyển khoản, trang sẽ tự cập nhật trong vài giây.", text);
        Assert.Contains("data-provider=\"payos\"", html);
        Assert.DoesNotContain("data-qr=", html);
        Assert.DoesNotContain("Giả lập thanh toán thành công", text);
    }

    [Fact]
    public async Task Order_PayOSWithoutAUsableAddress_SaysSoInsteadOfARedirectingLink()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Learner(factory);
        var code = await SeedPayOsOrderAsync(factory, PaymentOrderStatuses.Pending, payUrl: "javascript:alert(1)");

        var html = await GetAsync(client, $"/Billing/Order/{code}");
        var block = html[html.IndexOf("tct-theme tct-page tb-page", StringComparison.Ordinal)..html.IndexOf("</main>", StringComparison.Ordinal)];

        Assert.DoesNotContain("javascript:", block);
        Assert.DoesNotContain("Mở lại trang thanh toán", Decode(block));
        Assert.Contains("Không mở lại được trang thanh toán của đơn này.", Decode(block));
    }

    [Fact]
    public async Task Order_PayOSCancelled_SaysItWasCancelled_AndOffersToPayAgain()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Learner(factory);
        var code = await SeedPayOsOrderAsync(factory, PaymentOrderStatuses.Cancelled);

        var text = Decode(await GetAsync(client, $"/Billing/Order/{code}"));

        Assert.Contains("Bạn đã hủy thanh toán. Bạn có thể chọn gói và thanh toán lại.", text);
        Assert.Contains("Thanh toán lại", text);
    }

    [Fact]
    public async Task Order_PayOSOfAnotherBuyer_IsNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Learner(factory);
        int otherUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var other = new User { Email = $"other-{Guid.NewGuid():N}@test.com", PasswordHash = "hash", Role = Roles.Standard };
            db.Users.Add(other);
            await db.SaveChangesAsync();
            otherUserId = other.UserId;
        }

        var code = await SeedPayOsOrderAsync(factory, PaymentOrderStatuses.Pending, userId: otherUserId);

        var (status, _) = await TryGetAsync(client, $"/Billing/Order/{code}");

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task History_ShowsThePayOSLabel()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Learner(factory);
        await SeedPayOsOrderAsync(factory, PaymentOrderStatuses.Pending);

        var text = Decode(await GetAsync(client, "/Billing/History"));

        Assert.Contains("payOS", text);
    }

    // ─── the local mock page ──────────────────────────────────────────────────

    [Fact]
    public async Task MockPage_ShowsTheTestBanner_ThePayAndCancelActions_AndAnAntiforgeryToken()
    {
        await using var factory = await CreateFactoryAsync(payOsMock: true);
        using var client = Learner(factory);
        var code = await SeedPayOsOrderAsync(factory, PaymentOrderStatuses.Pending, payUrl: null);
        var html = await GetAsync(client, $"/Billing/PayOSMock?orderCode={code}");
        var text = Decode(html);

        Assert.Contains("Chế độ thử – không trừ tiền thật", text);
        Assert.Contains("Thanh toán thử", text);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains($"/Billing/PayOSCancel?orderCode={PayOsCode}&amp;cancel=true&amp;status=CANCELLED&amp;ref={code}", html);
        Assert.DoesNotContain(" style=\"", html[html.IndexOf("data-tb-payos-mock", StringComparison.Ordinal)..html.IndexOf("</main>", StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task MockPage_IsNotFound_WhenTheMockIsOff()
    {
        await using var factory = await CreateFactoryAsync(payOsMock: false);
        using var client = Learner(factory);
        var code = await SeedPayOsOrderAsync(factory, PaymentOrderStatuses.Pending, payUrl: null);

        var (status, _) = await TryGetAsync(client, $"/Billing/PayOSMock?orderCode={code}");

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task MockOrder_WaitingPage_LinksToTheLocalMockPage()
    {
        await using var factory = await CreateFactoryAsync(payOsMock: true);
        using var client = Learner(factory);
        var code = await SeedPayOsOrderAsync(factory, PaymentOrderStatuses.Pending, payUrl: null);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var order = await db.PaymentOrders.FirstAsync(o => o.OrderCode == code);
            order.ProviderPaymentUrl = $"/Billing/PayOSMock?orderCode={Uri.EscapeDataString(code)}";
            await db.SaveChangesAsync();
        }

        var html = await GetAsync(client, $"/Billing/Order/{code}");

        Assert.Contains($"href=\"/Billing/PayOSMock?orderCode={code}\"", html);
    }

    [Fact]
    public async Task OrderPage_NeverRendersAForeignRelativeAddressForPayOS()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = Learner(factory);
        var code = await SeedPayOsOrderAsync(factory, PaymentOrderStatuses.Pending, payUrl: "/Account/Delete?x=1");

        var html = await GetAsync(client, $"/Billing/Order/{code}");

        Assert.DoesNotContain("/Account/Delete", html);
    }
}
