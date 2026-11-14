namespace TCTVocabulary.ViewModels
{
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

    /// <summary>How many learned words sit in each of the four learner-facing memory stages.</summary>
    public class VocabularyMemoryViewModel
    {
        public int New { get; set; }
        public int Growing { get; set; }
        public int Strong { get; set; }
        public int Long { get; set; }
        public int Total => New + Growing + Strong + Long;
    }

    /// <summary>One bar of the "Lịch ôn 7 ngày tới" chart.</summary>
    public class VocabularyForecastDayViewModel
    {
        /// <summary>"Hôm nay", then "T2".."T7", "CN".</summary>
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsToday { get; set; }
        /// <summary>Bar height 0-100, relative to the busiest day.</summary>
        public int HeightPercent { get; set; }
    }

    /// <summary>A row of "Sắp ôn lại".</summary>
    public class VocabularyUpcomingWordViewModel
    {
        public string Term { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Stage { get; set; } = "new";
        /// <summary>"Hôm nay", "Ngày mai" or "Sau n ngày".</summary>
        public string Label { get; set; } = string.Empty;
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
        /// <summary>One of star, talk, book, food, travel, jobs, colors: the cover illustration.</summary>
        public string ArtKey { get; set; } = "star";
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

        public const int DailyTargetWords = 10;

        public int DueCount { get; set; }
        /// <summary>Estimated minutes for the due words: ceil(due x 15 s / 60), at least 1.</summary>
        public int DueMinutes { get; set; } = 1;
        /// <summary>Distinct system words reviewed or learned today (business date).</summary>
        public int TodayCount { get; set; }
        public int DailyTarget { get; set; } = DailyTargetWords;
        public int Streak { get; set; }
        public int LearnedCount { get; set; }
        public VocabularyMemoryViewModel Memory { get; set; } = new();
        /// <summary>Seven days starting today; day 0 includes everything overdue.</summary>
        public List<VocabularyForecastDayViewModel> Forecast { get; set; } = new();
        /// <summary>Label of the nearest future due date when nothing is due now (for example "Ngày mai"); null when there is none.</summary>
        public string? NextDueLabel { get; set; }
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
        /// <summary>Words of this lesson that are due now.</summary>
        public int DueCount { get; set; }
        /// <summary>One stage key per word ("none" for an unlearned word); drives the row of dots.</summary>
        public List<string> WordStages { get; set; } = new();
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
        /// <summary>Memory level 0-6; null when the word has not been learned yet.</summary>
        public int? Level { get; set; }
        /// <summary>"none", "new", "growing", "strong" or "long".</summary>
        public string Stage { get; set; } = "none";
        public string StageLabel { get; set; } = "Chưa học";
        /// <summary>"Hôm nay", "Ngày mai", "Sau n ngày" or "Chưa học".</summary>
        public string NextReviewLabel { get; set; } = "Chưa học";
        public string? Emoji { get; set; }
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
        public VocabularyMemoryViewModel Memory { get; set; } = new();
        /// <summary>Up to six words with the nearest review date.</summary>
        public List<VocabularyUpcomingWordViewModel> Upcoming { get; set; } = new();
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
        /// <summary>Memory level 0-6; null for a word that has no progress yet.</summary>
        public int? Level { get; set; }
        /// <summary>"none", "new", "growing", "strong" or "long".</summary>
        public string Stage { get; set; } = "none";
        public string? StageLabel { get; set; }
        public string? Emoji { get; set; }
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
        /// <summary>Review sessions: due words that did not fit in this batch of 20.</summary>
        public int RemainingDue { get; set; }
        /// <summary>When nothing is due: the label of the nearest future review ("Ngày mai", ...); null when there is none.</summary>
        public string? NextDueLabel { get; set; }

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
                emptyReason = EmptyReason,
                remainingDue = RemainingDue,
                nextDueLabel = NextDueLabel
            }, options);
        }
    }

    /// <summary>The answer of POST /Vocabulary/Review/Record.</summary>
    public class VocabularyReviewRecordResult
    {
        public bool Success { get; set; } = true;
        public int CardId { get; set; }
        public int Level { get; set; }
        public string Stage { get; set; } = "new";
        public string StageLabel { get; set; } = string.Empty;
        public DateTime? NextReviewDate { get; set; }
        public string NextReviewLabel { get; set; } = string.Empty;
        public bool Promoted { get; set; }
        public bool Demoted { get; set; }
        public int XpEarned { get; set; }
        public int Streak { get; set; }
    }
}
