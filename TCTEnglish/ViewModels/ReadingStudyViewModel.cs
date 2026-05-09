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

        // ─── Bản dịch nổi bật ────────────────────────────────────────────────
        public List<ReadingTranslationCardViewModel> FeaturedTranslations { get; set; }
            = new();

        /// <summary>Id bản dịch của user hiện tại (null = chưa có)</summary>
        public int? CurrentUserTranslationId { get; set; }
    }
}