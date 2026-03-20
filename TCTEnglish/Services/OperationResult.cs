namespace TCTVocabulary.Services
{
    public enum OperationStatus
    {
        Success,
        NotFound,
        Invalid
    }

    public sealed class OperationResult
    {
        private OperationResult(OperationStatus status, string? errorMessage = null)
        {
            Status = status;
            ErrorMessage = errorMessage;
        }

        public OperationStatus Status { get; }
        public string? ErrorMessage { get; }

        public static OperationResult Success()
        {
            return new(OperationStatus.Success);
        }

        public static OperationResult NotFound(string? errorMessage = null)
        {
            return new(OperationStatus.NotFound, errorMessage);
        }

        public static OperationResult Invalid(string? errorMessage)
        {
            return new(OperationStatus.Invalid, errorMessage);
        }
    }
}
