using TCTVocabulary.Models;

namespace TCTVocabulary.ViewModel
{
        public class ClassDetailViewModel
        {
            public Class Class { get; set; } = null!;
            public List<User> Members { get; set; } = new();
            public List<ClassMessage> Messages { get; set; } = new();
        }

}
