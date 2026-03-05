using TCTVocabulary.Models;

namespace TCTVocabulary.ViewModel
{
    public class SearchViewModel
    {
        public string Query { get; set; } = "";

        public List<Folder> Folders { get; set; } = new();
        public List<Class> Classes { get; set; } = new();
        public List<User> Users { get; set; } = new();
    }
}
