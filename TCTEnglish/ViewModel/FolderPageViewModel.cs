using TCTVocabulary.Models;

namespace TCTVocabulary.ViewModel
{
    public class FolderPageViewModel
    {
        public List<Folder> MyFolders { get; set; } = new();
        public List<Folder> SavedFolders { get; set; } = new();
    }
}
