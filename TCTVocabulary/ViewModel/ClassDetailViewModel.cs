using TCTVocabulary.Models;

namespace TCTVocabulary.ViewModel
{
        public class ClassDetailViewModel
        {
        public Class Class { get; set; } = null!;
        public List<User> Members { get; set; } = new();

        public List<ClassMessageViewModel> Messages { get; set; } = new();
        public List<Folder> MyFolders { get; set; } = new();
        public List<Folder> SavedFolders { get; set; } = new();
        public List<ClassFolder> ClassFolders { get; set; } = new();




    }

}
