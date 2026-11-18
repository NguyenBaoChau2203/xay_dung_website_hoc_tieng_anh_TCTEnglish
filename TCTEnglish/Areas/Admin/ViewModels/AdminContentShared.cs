namespace TCTVocabulary.Areas.Admin.ViewModels;

/// <summary>Colour roles of the admin content kit. Rendered as text tags and tinted figure icons, never by colour alone.</summary>
public static class AcTone
{
    public const string Primary = "primary";
    public const string Success = "success";
    public const string Warning = "warning";
    public const string Danger = "danger";
    public const string Info = "info";
    public const string Muted = "muted";
}

public sealed record AcCrumb(string Text, string? Url = null);

/// <summary>A header button or link. <see cref="Style"/> is "primary", "outline" or "danger". A POST action renders a form with the antiforgery token and, when <see cref="ConfirmMessage"/> is set, asks first.</summary>
public sealed record AcAction(string Text, string Url, string Icon, string Style = "outline", string? Title = null, bool IsPost = false, string? ConfirmMessage = null);

public sealed class AcPageHeaderModel
{
    public string Icon { get; set; } = "fa-solid fa-table-list";
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public IReadOnlyList<AcCrumb> Crumbs { get; set; } = Array.Empty<AcCrumb>();
    public IReadOnlyList<AcAction> Actions { get; set; } = Array.Empty<AcAction>();
    /// <summary>Optional text tags shown next to the title (level, status, source).</summary>
    public IReadOnlyList<AcTag> Tags { get; set; } = Array.Empty<AcTag>();
}

public sealed record AcFigure(string Label, string Value, string Icon, string Tone = AcTone.Primary, string? Hint = null);

public sealed record AcTag(string Text, string Tone = AcTone.Muted, string? Icon = null);

public sealed record AcOption(string Value, string Text);

/// <summary>One control of the filter bar. <see cref="Kind"/> is "search" or "select".</summary>
public sealed class AcFilterField
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Kind { get; set; } = "select";
    public string? Value { get; set; }
    public string? Placeholder { get; set; }
    public bool Disabled { get; set; }
    public string? Hint { get; set; }
    public IReadOnlyList<AcOption> Options { get; set; } = Array.Empty<AcOption>();
}

public sealed class AcFilterBarModel
{
    public string Action { get; set; } = "Index";
    public string Controller { get; set; } = "";
    public string FormId { get; set; } = "acFilterForm";
    public IReadOnlyList<AcFilterField> Fields { get; set; } = Array.Empty<AcFilterField>();
    /// <summary>Extra values kept on submit (for example the set id on the cards page).</summary>
    public IReadOnlyDictionary<string, string> Hidden { get; set; } = new Dictionary<string, string>();
}

public sealed class AcTableHeaderModel
{
    public string Icon { get; set; } = "fa-solid fa-list";
    public string Title { get; set; } = "";
    public int Count { get; set; }
    /// <summary>Optional sort select; submits the surrounding filter form (data-ac-autosubmit).</summary>
    public AcFilterField? Sort { get; set; }
    public string? SortFormId { get; set; }
}

public sealed class AcPagerModel
{
    public string Action { get; set; } = "Index";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalItems { get; set; }
    public int TotalPages => TCTEnglish.Services.Admin.AdminInputRules.TotalPages(TotalItems, PageSize);
    public int From => TotalItems == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int To => Math.Min(TotalItems, Page * PageSize);
    /// <summary>Route values kept on every page link (filters, set id…). "page" is added per link.</summary>
    public IReadOnlyDictionary<string, string?> Route { get; set; } = new Dictionary<string, string?>();
}

/// <summary>Delete button whose form asks for the exact name before it submits (SweetAlert2, see admin-content.js).</summary>
public sealed class AcConfirmDeleteModel
{
    public string Action { get; set; } = "Delete";
    public string Controller { get; set; } = "";
    public IReadOnlyDictionary<string, string> Route { get; set; } = new Dictionary<string, string>();
    /// <summary>The exact text the admin has to type back.</summary>
    public string ConfirmName { get; set; } = "";
    public string Title { get; set; } = "Xóa?";
    /// <summary>What will be lost, one sentence ("12 thẻ và tiến độ học của 3 học viên sẽ bị xóa.").</summary>
    public string Impact { get; set; } = "";
    public string AriaLabel { get; set; } = "Xóa";
    public string ButtonText { get; set; } = "";
    public string ConfirmText { get; set; } = "Xóa";
    public string Icon { get; set; } = "fa-solid fa-trash-can";
}
