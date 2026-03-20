namespace TCTVocabulary.ViewModels
{
    public class FolderPageViewModel
    {
        public List<FolderCardViewModel> MyFolders { get; set; } = new();
        public List<FolderCardViewModel> SavedFolders { get; set; } = new();
    }

    public class FolderCardViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public int SetCount { get; set; }
        public string CreatorName { get; set; } = string.Empty;
    }
}
