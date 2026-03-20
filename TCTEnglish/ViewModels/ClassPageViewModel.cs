namespace TCTVocabulary.ViewModels
{
    public class ClassPageViewModel
    {
        public List<ClassCardViewModel> Classes { get; set; } = new();
    }

    public class ClassCardViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
    }

    public class ClassSearchResultViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public bool HasPassword { get; set; }
        public string? ImageUrl { get; set; }
    }
}
