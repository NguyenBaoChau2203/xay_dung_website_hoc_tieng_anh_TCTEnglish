namespace TCTVocabulary.ViewModels
{
    public class FolderDetailViewModel
    {
        public FolderSummaryViewModel Folder { get; set; } = new();
        public List<FolderSetItemViewModel> Sets { get; set; } = new();
        public int CurrentUserId { get; set; }
        public bool IsSaved { get; set; }
        public bool IsOwner { get; set; }
        public bool CanManage { get; set; }
    }

    public class FolderSummaryViewModel
    {
        public int FolderId { get; set; }
        public int UserId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
    }

    public class FolderSetItemViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public int CardCount { get; set; }
    }
}
