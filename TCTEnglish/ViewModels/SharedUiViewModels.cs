namespace TCTEnglish.ViewModels
{
    /// <summary>Visual weight of a link rendered as a button in a shared partial.</summary>
    public enum PageActionVariant
    {
        Primary,
        Secondary,
        Danger
    }

    /// <summary>One breadcrumb entry. The last entry is rendered as the current page.</summary>
    public sealed class PageBreadcrumbItem
    {
        public PageBreadcrumbItem(string text, string? url = null)
        {
            Text = text;
            Url = url;
        }

        public string Text { get; }

        /// <summary>Local URL only (checked with IUrlHelper.IsLocalUrl at render time); null renders plain text.</summary>
        public string? Url { get; }
    }

    /// <summary>A navigation action shown beside the page title. POST actions belong in page markup.</summary>
    public sealed class PageHeaderAction
    {
        public PageHeaderAction(string label, string url, PageActionVariant variant = PageActionVariant.Secondary, string? iconClass = null)
        {
            Label = label;
            Url = url;
            Variant = variant;
            IconClass = iconClass;
        }

        public string Label { get; }
        public string Url { get; }
        public PageActionVariant Variant { get; }

        /// <summary>Icon CSS classes from the family loaded by the current layout (Font Awesome in both shells).</summary>
        public string? IconClass { get; }
    }

    /// <summary>Model for Views/Shared/_PageHeader.cshtml. Renders the page's single h1.</summary>
    public sealed class PageHeaderViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public IReadOnlyList<PageBreadcrumbItem> Breadcrumbs { get; set; } = Array.Empty<PageBreadcrumbItem>();
        public IReadOnlyList<PageHeaderAction> Actions { get; set; } = Array.Empty<PageHeaderAction>();
    }

    public enum StateMessageKind
    {
        Empty,
        Loading,
        Error,
        Success
    }

    /// <summary>Model for Views/Shared/_StateMessage.cshtml (loading, empty, error and success blocks).</summary>
    public sealed class StateMessageViewModel
    {
        public StateMessageKind Kind { get; set; } = StateMessageKind.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? ActionLabel { get; set; }

        /// <summary>Local URL only; the action is omitted when this is null or not local.</summary>
        public string? ActionUrl { get; set; }
    }
}
