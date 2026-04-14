namespace TCTEnglish.ViewModels
{
    public class ReadingStudyViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public string Content { get; set; }

        public string ImageUrl { get; set; }

        public string Level { get; set; }

        public bool IsCompleted { get; set; }

        public List<QuestionViewModel> Questions { get; set; }
    }
}