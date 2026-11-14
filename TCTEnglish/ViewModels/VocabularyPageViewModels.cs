namespace TCTVocabulary.ViewModels
{
    public class VocabularyIndexViewModel
    {
        public VocabularyDashboardStatsViewModel Stats { get; set; } = new();
        public VocabularyContinueLearningViewModel? ContinueLearning { get; set; }
        public List<string> AllTags { get; set; } = new();
        public List<VocabularyFolderSectionViewModel> Folders { get; set; } = new();
    }

    public class VocabularyDashboardStatsViewModel
    {
        public int TotalCards { get; set; }
        public int TotalSets { get; set; }
        public int MasteredCards { get; set; }
        public int DueToday { get; set; }
        public int Streak { get; set; }
        public int LongestStreak { get; set; }
    }

    public class VocabularyContinueLearningViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public int CardCount { get; set; }
        public int RemainingCardCount { get; set; }
        public int ProgressPercentage { get; set; }
        public bool HasProgress { get; set; }
    }

    public class VocabularyFolderSectionViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public int SetCount { get; set; }
        public List<VocabularySetListItemViewModel> Sets { get; set; } = new();
    }

    public class VocabularyFolderDetailViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public int TotalSets { get; set; }
        public int TotalCards { get; set; }
        public int TotalViews { get; set; }
        public List<VocabularySetListItemViewModel> Sets { get; set; } = new();
    }

    public class VocabularySetListItemViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DescriptionText { get; set; } = "Không có mô tả";
        public int CardCount { get; set; }
        public int MasteredCardCount { get; set; }
        public int DueTodayCount { get; set; }
        public int RemainingCardCount { get; set; }
        public int ProgressPercentage { get; set; }
        public int EstimatedMinutes { get; set; }
        public int ViewCount { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class VocabularySetDetailViewModel
    {
        public VocabularySetSummaryViewModel Set { get; set; } = new();
        public VocabularySetStatsViewModel Stats { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public List<VocabularyTopicSummaryViewModel> Topics { get; set; } = new();
        public List<VocabularyRelatedSetViewModel> RelatedSets { get; set; } = new();
    }

    public class VocabularyTopicsViewModel
    {
        public VocabularySetSummaryViewModel Set { get; set; } = new();
        public VocabularySetStatsViewModel Stats { get; set; } = new();
        public List<VocabularyTopicSummaryViewModel> Topics { get; set; } = new();
    }

    public class VocabularyTopicDetailViewModel
    {
        public VocabularySetSummaryViewModel Set { get; set; } = new();
        public VocabularyTopicSummaryViewModel Topic { get; set; } = new();
        public VocabularySetStatsViewModel Stats { get; set; } = new();
        public List<VocabularyTopicCardViewModel> Cards { get; set; } = new();
    }

    public class VocabularySetStatsViewModel
    {
        public int TotalCards { get; set; }
        public int LearnedCards { get; set; }
        public int RemainingCards { get; set; }
        public int Accuracy { get; set; }
        public int NewCards { get; set; }
        public int ReviewingCards { get; set; }
        public int MasteredCards { get; set; }
        public int DueToday { get; set; }
    }

    public class VocabularyTopicSummaryViewModel
    {
        public string TopicName { get; set; } = string.Empty;
        public int TotalCards { get; set; }
        public int LearnedCards { get; set; }
        public int Progress { get; set; }
    }

    public class VocabularyRelatedSetViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public int CardCount { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class VocabularyTopicCardViewModel
    {
        public int CardId { get; set; }
        public string Term { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public string? Phonetic { get; set; }
        public string? Example { get; set; }
        public string? ExampleTranslation { get; set; }
        public string? ImageUrl { get; set; }
        public string StatusClass { get; set; } = "new";
        public string StatusText { get; set; } = "Mới";
    }

    // ── Rebuilt system Vocabulary module: home, course and lesson screens ──

    public class VocabularyStatsViewModel
    {
        public int LearnedCards { get; set; }
        public int KnownCards { get; set; }
        public int DueCards { get; set; }
        public int LearnedToday { get; set; }
        public int Streak { get; set; }
        public int TotalCards { get; set; }
    }

    /// <summary>The lesson the home page recommends.</summary>
    public class VocabularyNextLessonViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public int LessonNumber { get; set; }
        /// <summary>Full title, for example "Bài 5 · Animals".</summary>
        public string LessonTitle { get; set; } = string.Empty;
        public int NewWordCount { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    public class VocabularyCourseCardViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public string? Level { get; set; }
        public int WordCount { get; set; }
        public int LessonCount { get; set; }
        public int LearnedCount { get; set; }
        public int KnownCount { get; set; }
        public int DueCount { get; set; }
        public int ProgressPercent { get; set; }
        /// <summary>"new", "progress" or "done".</summary>
        public string Status { get; set; } = "new";
        public string IconKey { get; set; } = "book";
        public string Hue { get; set; } = "primary";
    }

    public class VocabularyCollectionViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public List<VocabularyCourseCardViewModel> Courses { get; set; } = new();
    }

    public class VocabularyHomeViewModel
    {
        public VocabularyStatsViewModel Stats { get; set; } = new();
        public VocabularyNextLessonViewModel? Next { get; set; }
        public string? DueUrl { get; set; }
        public List<VocabularyCollectionViewModel> Collections { get; set; } = new();
        public List<string> Levels { get; set; } = new();
        public int? FilterFolderId { get; set; }
        public string? FilterFolderName { get; set; }
    }

    public class VocabularyLessonRowViewModel
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public int LearnedCount { get; set; }
        /// <summary>"new", "progress" or "done".</summary>
        public string State { get; set; } = "new";
        public string Url { get; set; } = string.Empty;
        public bool IsNext { get; set; }
        public List<string> PreviewTerms { get; set; } = new();
        public int MoreCount { get; set; }
    }

    public class VocabularyWordRowViewModel
    {
        public int CardId { get; set; }
        public string Term { get; set; } = string.Empty;
        public string? Phonetic { get; set; }
        public string Definition { get; set; } = string.Empty;
        public string? Example { get; set; }
        public string? ExampleTranslation { get; set; }
        public string? ImageUrl { get; set; }
        public int LessonNumber { get; set; }
        public string Topic { get; set; } = string.Empty;
        /// <summary>"new", "learning" or "known".</summary>
        public string State { get; set; } = "new";
        public bool IsDue { get; set; }
    }

    public class VocabularyLessonLinkViewModel
    {
        public int Number { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    public class VocabularyCourseViewModel
    {
        public VocabularyCourseCardViewModel Course { get; set; } = new();
        public int? FolderId { get; set; }
        public string? FolderName { get; set; }
        public VocabularyLessonLinkViewModel? NextLesson { get; set; }
        public bool CourseComplete { get; set; }
        public string? DueUrl { get; set; }
        public string PracticeUrl { get; set; } = string.Empty;
        public List<VocabularyLessonRowViewModel> Lessons { get; set; } = new();
        public List<VocabularyWordRowViewModel> Words { get; set; } = new();
        /// <summary>"lessons" or "words"; set by the controller from the requested URL.</summary>
        public string ActiveTab { get; set; } = "lessons";
        public string? TopicFilter { get; set; }
    }

    /// <summary>A session word as the lesson script receives it.</summary>
    public class VocabularyLessonWordViewModel
    {
        public int Id { get; set; }
        public string Term { get; set; } = string.Empty;
        public string? Phonetic { get; set; }
        public string Definition { get; set; } = string.Empty;
        public string? Example { get; set; }
        public string? ExampleVi { get; set; }
        public string? ImageUrl { get; set; }
    }

    /// <summary>A card used only as a wrong-answer distractor.</summary>
    public class VocabularyPoolWordViewModel
    {
        public int Id { get; set; }
        public string Term { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
    }

    public class VocabularyLessonViewModel
    {
        /// <summary>"learn", "relearn" or "review".</summary>
        public string Kind { get; set; } = "learn";
        public int? SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public int? LessonNumber { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public List<VocabularyLessonWordViewModel> Words { get; set; } = new();
        public List<VocabularyPoolWordViewModel> Pool { get; set; } = new();
        public string CourseUrl { get; set; } = "/Vocabulary/Index";
        public string? NextLessonUrl { get; set; }
        public int? NextLessonNumber { get; set; }
        public string? ReviewUrl { get; set; }
        public string HomeUrl { get; set; } = "/Vocabulary/Index";
        /// <summary>"no-cards", "complete" or "nothing-due"; null when there is something to study.</summary>
        public string? EmptyReason { get; set; }
        /// <summary>URL of lesson 1 of the course (used by "Ôn lại từ đầu").</summary>
        public string? RestartUrl { get; set; }
        /// <summary>URL that studies the same lesson again as a "relearn" session.</summary>
        public string? RelearnUrl { get; set; }

        /// <summary>JSON for the page's config script. The default encoder escapes &lt; &gt; &amp;, so it is safe inside a script tag.</summary>
        public string ToConfigJson()
        {
            var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                kind = Kind,
                setId = SetId,
                setName = SetName,
                lessonNumber = LessonNumber,
                lessonTitle = LessonTitle,
                words = Words,
                pool = Pool,
                courseUrl = CourseUrl,
                nextLessonUrl = NextLessonUrl,
                nextLessonNumber = NextLessonNumber,
                reviewUrl = ReviewUrl,
                homeUrl = HomeUrl,
                restartUrl = RestartUrl,
                relearnUrl = RelearnUrl,
                emptyReason = EmptyReason
            }, options);
        }
    }
}
