using System.Reflection;
using AngleSharp.Html.Parser;
using MySqlConnector;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Parser and serializer boundaries from the AngleSharp maintainer advisories.
/// AngleSharp parses HTML; these tests do not treat it as a sanitizer.
/// </summary>
public sealed class AngleSharpAdvisoryCompatibilityTests
{
    private readonly HtmlParser _parser = new();

    [Fact]
    public void Runtime_LoadsPinnedParserAndConnector()
    {
        Assert.Equal(new Version(1, 10, 1, 0), typeof(HtmlParser).Assembly.GetName().Version);
        Assert.Contains("1.10.1", typeof(HtmlParser).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        var connector = typeof(MySqlConnection).Assembly;
        Assert.Equal("MySqlConnector", connector.GetName().Name);
        Assert.Contains("2.6.2", connector.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
    }

    [Fact]
    public void AnnotationXmlHtmlIntegrationPoint_IsVisibleToADomWalk()
    {
        const string payload =
            "<math><annotation-xml encoding=\"text/html\">" +
            "<title><a encoding=\"</title><img src=x onerror=alert()>\">" +
            "</annotation-xml></math>";

        var document = _parser.ParseDocument(payload);
        var image = document.All.Single(element => element.LocalName == "img");

        Assert.Equal("x", image.GetAttribute("src"));
        Assert.Equal("alert()", image.GetAttribute("onerror"));
    }

    [Fact]
    public void AttributeSerialization_EscapesMarkupThatWouldBreakOutOnReparse()
    {
        var document = _parser.ParseDocument("<div id=\"holder\">ok</div>");
        document.GetElementById("holder")!.SetAttribute("title", "</div><img src=x onerror=alert()>");

        var serialized = document.Body!.InnerHtml;
        Assert.Contains("&lt;", serialized, StringComparison.Ordinal);
        Assert.Contains("&gt;", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", serialized, StringComparison.OrdinalIgnoreCase);

        var reparsed = _parser.ParseDocument(serialized);
        Assert.Empty(reparsed.QuerySelectorAll("img"));
    }

    [Fact]
    public void SvgStyleSerialization_DoesNotPromoteInertTextToAnHtmlImage()
    {
        const string payload =
            "<svg><style>&lt;/style&gt;&lt;img src=x onerror=window.__anglesharpMxss=1&gt;</style></svg>";

        var first = _parser.ParseDocument(payload);
        Assert.Empty(first.QuerySelectorAll("img"));
        Assert.NotNull(first.QuerySelector("style"));

        var serialized = first.Body!.InnerHtml;
        Assert.Contains("&lt;", serialized, StringComparison.Ordinal);
        Assert.Contains("&gt;", serialized, StringComparison.Ordinal);

        var second = _parser.ParseDocument(serialized);
        Assert.Empty(second.QuerySelectorAll("img"));
        Assert.DoesNotContain(second.All, element => element.HasAttribute("onerror"));
    }

    [Fact]
    public void HtmlStyleAndSelectXmp_KeepRawTextInsteadOfCreatingScriptOrImage()
    {
        const string styleText = "&lt;img src=x onerror=alert(1)&gt;";
        using var style = _parser.ParseDocument($"<style>{styleText}</style>");
        Assert.Equal(styleText, style.Head!.QuerySelector("style")!.TextContent);
        Assert.Empty(style.QuerySelectorAll("img"));
        // A leading style belongs to head; serializing body would test an empty string.
        using var styleReparsed = _parser.ParseDocument(style.DocumentElement.OuterHtml);
        Assert.NotNull(styleReparsed.Head!.QuerySelector("style"));
        Assert.Equal(styleText, styleReparsed.Head.QuerySelector("style")!.TextContent);
        Assert.Empty(styleReparsed.QuerySelectorAll("img"));

        const string selectPayload = "<select><xmp><script>window.__parse_poc=1</script>";
        var select = _parser.ParseDocument(selectPayload);
        var xmp = select.QuerySelector("xmp");
        Assert.NotNull(xmp);
        Assert.Empty(select.QuerySelectorAll("script"));
        Assert.Contains("window.__parse_poc=1", xmp!.TextContent, StringComparison.Ordinal);

        var reparsed = _parser.ParseDocument(select.DocumentElement.OuterHtml);
        Assert.Empty(reparsed.QuerySelectorAll("script"));
        Assert.Contains("window.__parse_poc=1", reparsed.QuerySelector("xmp")!.TextContent, StringComparison.Ordinal);
    }
}
