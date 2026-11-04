namespace TCTVocabulary.Areas.Admin.ViewModels;

public class AdminCatalogPageViewModel
{
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public string Search { get; set; } = "";
    public string Level { get; set; } = "";
    public string Topic { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Visibility { get; set; } = "";
}

public sealed class SpeakingVideoIndexViewModel : AdminCatalogPageViewModel
{
    public List<SpeakingVideoListItemViewModel> Videos { get; set; } = [];
    public List<string> Levels { get; set; } = [];
    public List<string> Topics { get; set; } = [];
}
