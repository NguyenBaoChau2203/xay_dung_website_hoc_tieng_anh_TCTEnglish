using TCTVocabulary.Models;

namespace TCTVocabulary.ViewModel
{
    public class MyClassViewModel
    {
        public List<Class> MyClasses { get; set; } = new();
        public List<Class> JoinedClasses { get; set; } = new();
    }
}
