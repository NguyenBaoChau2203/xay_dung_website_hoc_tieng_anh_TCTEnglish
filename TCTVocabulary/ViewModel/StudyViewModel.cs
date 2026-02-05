using TCTVocabulary.Models;

namespace TCTVocabulary.Models.ViewModels
{
    public class StudyViewModel
    {
        public Set Set { get; set; } = null!;
        public List<Card> Cards { get; set; } = new List<Card>();
        public List<int> MasteredCardIds { get; set; } = new List<int>();
        public List<int> LearningCardIds { get; set; } = new List<int>();
    }
}