using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Renders the shared design-system partials through the application's real Razor engine.
/// Guards the behaviors teammates rely on: encoded text, local-only URLs, one h1, and the
/// role/aria contract of state messages. It deliberately does not assert colors or classes.
/// </summary>
public sealed class SharedUiPartialsRenderTests
{
    private const string PageHeaderView = "~/Views/Shared/_PageHeader.cshtml";
    private const string StateMessageView = "~/Views/Shared/_StateMessage.cshtml";

    /// <summary>
    /// Razor's default encoder writes non-ASCII letters as numeric entities (ị becomes &#x1ECB;).
    /// Assert visible text on the decoded string; assert XSS safety on the raw string. A
    /// "does not contain" check on raw HTML would pass trivially for entity-encoded text.
    /// </summary>
    private static string Text(string html) => WebUtility.HtmlDecode(html);

    private static async Task<string> RenderAsync<TModel>(string viewPath, TModel model)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var viewEngine = services.GetRequiredService<ICompositeViewEngine>();
        var found = viewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);
        Assert.True(found.Success, $"View not found: {viewPath}. Searched: {string.Join(", ", found.SearchedLocations ?? Array.Empty<string>())}");

        var viewData = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions());
        await found.View.RenderAsync(viewContext);
        return writer.ToString();
    }

    [Fact]
    public async Task PageHeader_EncodesUserText_AndRendersExactlyOneH1()
    {
        var html = await RenderAsync(PageHeaderView, new PageHeaderViewModel
        {
            Title = "<script>alert('t')</script> Du lịch & Giao thông",
            Description = "<img src=x onerror=alert(1)> mô tả"
        });

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("Du lịch & Giao thông", Text(html));
        Assert.Single(Regex.Matches(html, "<h1[\\s>]"));
        Assert.DoesNotContain("<nav", html); // no breadcrumbs, no empty landmark
    }

    [Fact]
    public async Task PageHeader_Breadcrumbs_LastIsCurrentPage_AndOnlyLocalLinksAreAnchors()
    {
        var html = await RenderAsync(PageHeaderView, new PageHeaderViewModel
        {
            Title = "Chi tiết",
            Breadcrumbs = new[]
            {
                new PageBreadcrumbItem("Từ vựng", "/Vocabulary/Index"),
                new PageBreadcrumbItem("Ngoài", "https://evil.example/phish"),
                new PageBreadcrumbItem("Script", "javascript:alert(1)"),
                new PageBreadcrumbItem("Trang hiện tại", "/Vocabulary/Detail/3")
            }
        });

        Assert.Contains("href=\"/Vocabulary/Index\"", html);
        Assert.Contains("<span>Ngoài</span>", Text(html));    // non-local URL degrades to plain text
        Assert.DoesNotContain("evil.example", html);          // and the URL itself is never emitted
        Assert.DoesNotContain("javascript:", html);
        Assert.Contains("<span aria-current=\"page\">Trang hiện tại</span>", Text(html));
        Assert.DoesNotContain("href=\"/Vocabulary/Detail/3\"", html); // current page is not a link
        Assert.Contains("aria-label=\"Breadcrumb\"", html);
    }

    [Fact]
    public async Task PageHeader_Actions_AreLocalLinksOnly()
    {
        var html = await RenderAsync(PageHeaderView, new PageHeaderViewModel
        {
            Title = "Bộ từ",
            Actions = new[]
            {
                new PageHeaderAction("Bắt đầu học", "/Vocabulary/Study/3", PageActionVariant.Primary, "fa-solid fa-play"),
                new PageHeaderAction("Bên ngoài", "https://evil.example/x", PageActionVariant.Primary),
                new PageHeaderAction("Nguy hiểm", "//evil.example/x", PageActionVariant.Danger)
            }
        });

        Assert.Contains("href=\"/Vocabulary/Study/3\"", html);
        Assert.Contains("Bắt đầu học", Text(html));
        Assert.DoesNotContain("evil.example", html);
        Assert.DoesNotContain("Bên ngoài", Text(html));   // the whole action is dropped, label included
        Assert.DoesNotContain("Nguy hiểm", Text(html));
        Assert.Contains("aria-hidden=\"true\"", html); // decorative icon is hidden from assistive tech
    }

    [Theory]
    [InlineData(StateMessageKind.Error, "alert")]
    [InlineData(StateMessageKind.Empty, "status")]
    [InlineData(StateMessageKind.Success, "status")]
    [InlineData(StateMessageKind.Loading, "status")]
    public async Task StateMessage_UsesTheRightLiveRegionRole(StateMessageKind kind, string expectedRole)
    {
        var html = await RenderAsync(StateMessageView, new StateMessageViewModel { Kind = kind, Title = "Tiêu đề" });

        Assert.Contains($"role=\"{expectedRole}\"", html);
        Assert.Equal(kind == StateMessageKind.Loading, html.Contains("aria-busy=\"true\"", StringComparison.Ordinal));
        Assert.Equal(kind == StateMessageKind.Loading, html.Contains("spinner-border", StringComparison.Ordinal));
        Assert.Contains("<p class=\"tct-state__title\">Tiêu đề</p>", Text(html));
    }

    [Fact]
    public async Task StateMessage_EncodesText_AndOmitsNonLocalAction()
    {
        var external = await RenderAsync(StateMessageView, new StateMessageViewModel
        {
            Kind = StateMessageKind.Error,
            Title = "<b>Lỗi</b>",
            Message = "<script>x</script>",
            ActionLabel = "Thử lại",
            ActionUrl = "https://evil.example/retry"
        });
        var local = await RenderAsync(StateMessageView, new StateMessageViewModel
        {
            Kind = StateMessageKind.Empty,
            Title = "Trống",
            ActionLabel = "Tạo mới",
            ActionUrl = "/Set/CreateSet"
        });

        Assert.DoesNotContain("<b>", external);
        Assert.DoesNotContain("<script>", external);
        Assert.Contains("&lt;b&gt;", external);
        Assert.DoesNotContain("evil.example", external);
        Assert.DoesNotContain("Thử lại", Text(external)); // action dropped, not rendered as dead text
        Assert.Contains("href=\"/Set/CreateSet\"", local);
        Assert.Contains("Tạo mới", Text(local));
    }
}
