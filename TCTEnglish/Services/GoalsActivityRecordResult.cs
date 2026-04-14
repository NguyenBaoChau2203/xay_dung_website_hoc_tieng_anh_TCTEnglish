namespace TCTVocabulary.Services
{
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

        public static GoalsActivityRecordResult Success(int streak)
        {
            return new(OperationStatus.Success, streak);
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
