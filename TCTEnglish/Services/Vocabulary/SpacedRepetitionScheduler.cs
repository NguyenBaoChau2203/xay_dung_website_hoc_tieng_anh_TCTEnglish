namespace TCTVocabulary.Services.Vocabulary
{
    /// <summary>What the learner's answers for one word added up to in a session.</summary>
    public enum ReviewOutcome
    {
        /// <summary>Two or more wrong answers: back to the start.</summary>
        Again,
        /// <summary>Exactly one wrong answer: keep the level.</summary>
        Hard,
        /// <summary>No wrong answer: climb one level.</summary>
        Good,
        /// <summary>"Tôi đã biết từ này": jump to at least level 4.</summary>
        Known
    }

    /// <summary>The stored state of one word for one learner. <paramref name="Exists"/> is false when there is no progress row.</summary>
    public sealed record SrsState(int Level, DateTime? NextReviewUtc, bool Exists);

    public sealed record SrsResult(
        int Level,
        string Status,
        DateTime NextReviewUtc,
        bool Promoted,
        bool Demoted,
        bool EarlyReview,
        bool CountsWrong);

    /// <summary>
    /// The memory ladder of the system Vocabulary module: seven levels (0-6) with the intervals
    /// 10 min, 1, 3, 7, 14, 30 and 60 days. Pure: no database, no clock; the caller supplies the time.
    /// </summary>
    public static class SpacedRepetitionScheduler
    {
        public const int MaxLevel = 6;

        public const string StatusLearning = "Learning";
        public const string StatusReviewing = "Reviewing";
        public const string StatusMastered = "Mastered";

        private static readonly TimeSpan[] Intervals =
        {
            TimeSpan.FromMinutes(10),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(3),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(14),
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(60)
        };

        private static readonly string[] IntervalLabels =
        {
            "Ôn lại sau 10 phút",
            "Ôn lại sau 1 ngày",
            "Ôn lại sau 3 ngày",
            "Ôn lại sau 7 ngày",
            "Ôn lại sau 14 ngày",
            "Ôn lại sau 30 ngày",
            "Ôn lại sau 60 ngày"
        };

        public static TimeSpan IntervalFor(int level) => Intervals[Clamp(level)];

        /// <summary>Server-built Vietnamese label for the interval after reaching <paramref name="level"/>.</summary>
        public static string NextReviewLabelFor(int level) => IntervalLabels[Clamp(level)];

        /// <summary>Label returned when an early review leaves the schedule untouched.</summary>
        public const string ScheduleUnchangedLabel = "Lịch ôn giữ nguyên";

        /// <summary>
        /// Reads an existing progress row as a level (plan 4.2): the repetition count clamped to 0-6;
        /// "Mastered" rows are at least level 4; "Learning" rows are level 0.
        /// </summary>
        public static int LevelFrom(int repetitionCount, string? status)
        {
            if (string.Equals(status, StatusLearning, StringComparison.Ordinal))
            {
                return 0;
            }

            var level = Clamp(repetitionCount);
            if (string.Equals(status, StatusMastered, StringComparison.Ordinal) && level < 4)
            {
                level = 4;
            }

            return level;
        }

        public static string StatusFor(int level)
        {
            var clamped = Clamp(level);
            return clamped == 0 ? StatusLearning : clamped <= 3 ? StatusReviewing : StatusMastered;
        }

        /// <summary>"new" (0-1), "growing" (2-3), "strong" (4-5) or "long" (6).</summary>
        public static string StageFor(int level)
        {
            var clamped = Clamp(level);
            return clamped <= 1 ? "new" : clamped <= 3 ? "growing" : clamped <= 5 ? "strong" : "long";
        }

        public static string StageLabelFor(int level)
        {
            return StageFor(level) switch
            {
                "new" => "Mới học",
                "growing" => "Đang nhớ",
                "strong" => "Nhớ tốt",
                _ => "Thuộc lâu"
            };
        }

        public static SrsResult Apply(SrsState current, ReviewOutcome outcome, DateTime nowUtc)
        {
            var startLevel = current.Exists ? Clamp(current.Level) : 0;

            // P7: replaying a word that is not due yet never promotes it and never moves its date.
            if (current.Exists
                && current.NextReviewUtc.HasValue
                && current.NextReviewUtc.Value > nowUtc
                && outcome != ReviewOutcome.Again)
            {
                return new SrsResult(
                    startLevel, StatusFor(startLevel), current.NextReviewUtc.Value,
                    Promoted: false, Demoted: false, EarlyReview: true, CountsWrong: false);
            }

            var newLevel = outcome switch
            {
                ReviewOutcome.Again => 0,
                ReviewOutcome.Hard => Math.Max(1, startLevel),
                ReviewOutcome.Good => Math.Min(MaxLevel, startLevel + 1),
                ReviewOutcome.Known => Math.Max(4, startLevel),
                _ => startLevel
            };

            return new SrsResult(
                newLevel,
                StatusFor(newLevel),
                nowUtc + IntervalFor(newLevel),
                Promoted: newLevel > startLevel,
                Demoted: newLevel < startLevel,
                EarlyReview: false,
                CountsWrong: outcome == ReviewOutcome.Again);
        }

        /// <summary>Accepts exactly the four outcome names, ignoring case. No trimming and no numbers.</summary>
        public static bool TryParseOutcome(string? raw, out ReviewOutcome outcome)
        {
            switch (raw?.ToLowerInvariant())
            {
                case "again":
                    outcome = ReviewOutcome.Again;
                    return true;
                case "hard":
                    outcome = ReviewOutcome.Hard;
                    return true;
                case "good":
                    outcome = ReviewOutcome.Good;
                    return true;
                case "known":
                    outcome = ReviewOutcome.Known;
                    return true;
                default:
                    outcome = default;
                    return false;
            }
        }

        private static int Clamp(int level) => Math.Clamp(level, 0, MaxLevel);
    }
}
