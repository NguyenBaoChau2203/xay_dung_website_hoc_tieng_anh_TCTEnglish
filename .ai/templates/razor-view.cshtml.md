# Razor feature view scaffold

Read DESIGN.md and tct-ui-component. Resolve the actual typed ViewModel and route
before copying this example. New view models use TCTEnglish.ViewModels; existing
legacy model namespaces must be verified, not assumed.

```cshtml
@model TCTEnglish.ViewModels.{Feature}ViewModel
@{
    ViewData["Title"] = "{Feature}";
}

<main class="container-fluid py-4" aria-labelledby="featureTitle">
    <h1 id="featureTitle" class="h3 mb-4">{Feature}</h1>
    @* Render encoded model values and the feature's actual accessible controls. *@
</main>

@section Styles {
    <link rel="stylesheet" href="~/css/{feature}.css" asp-append-version="true" />
}

@section Scripts {
    <script src="~/js/{feature}.js" asp-append-version="true"></script>
}
```

Omit empty sections/assets; do not create files solely to satisfy the scaffold.
Inspect the layout before adding a second main landmark. Forms use Tag Helpers,
antiforgery and visible labels/validation. Reuse Bootstrap modal behavior for
destructive confirmations, with named close controls and focus return. Match the
page's Font Awesome/Material Symbols family; do not assume Bootstrap Icons exist.
Cover empty/loading/error states and check narrow/desktop views in a browser.
