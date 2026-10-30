namespace TCTVocabulary.Services
{
    public sealed class LearningActivityTransition
    {
        public int CurrentStreak { get; init; }
        public int PreviousLongestStreak { get; init; }
        public bool DidIncrease { get; init; }
        public IReadOnlyList<int> NewlyAwardedBadgeIds { get; init; } = Array.Empty<int>();
    }

    public sealed class GoalsActivityRecordResult
    {
        private GoalsActivityRecordResult(OperationStatus status, int streak = 0, string? errorMessage = null)
        {
            Status = status;
            Streak = streak;
            ErrorMessage = errorMessage;
        }

        public OperationStatus Status { get; }
        public int Streak { get; }
        public string? ErrorMessage { get; }
        public LearningActivityTransition? Transition { get; private set; }

        public static GoalsActivityRecordResult Success(int streak, LearningActivityTransition? transition = null)
        {
            return new(OperationStatus.Success, streak) { Transition = transition };
        }

        public static GoalsActivityRecordResult NotFound(string? errorMessage = null)
        {
            return new(OperationStatus.NotFound, 0, errorMessage);
        }

        public static GoalsActivityRecordResult Invalid(string? errorMessage)
        {
            return new(OperationStatus.Invalid, 0, errorMessage);
        }
    }
}
