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
        private OperationResult(OperationStatus status, string? errorMessage = null, IReadOnlyDictionary<string, string>? fieldErrors = null)
        {
            Status = status;
            ErrorMessage = errorMessage;
            FieldErrors = fieldErrors;
        }

        public OperationStatus Status { get; }
        public string? ErrorMessage { get; }

        /// <summary>Per-field validation messages when the failure is tied to specific form fields.</summary>
        public IReadOnlyDictionary<string, string>? FieldErrors { get; }

        public bool IsSuccess => Status == OperationStatus.Success;

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

        public static OperationResult Invalid(string? errorMessage, IReadOnlyDictionary<string, string> fieldErrors)
        {
            return new(OperationStatus.Invalid, errorMessage, fieldErrors);
        }
    }

    /// <summary>Same shape as <see cref="OperationResult"/> but carries a value on success.</summary>
    public sealed class OperationResult<T>
    {
        private OperationResult(OperationStatus status, T? value, string? errorMessage, IReadOnlyDictionary<string, string>? fieldErrors)
        {
            Status = status;
            Value = value;
            ErrorMessage = errorMessage;
            FieldErrors = fieldErrors;
        }

        public OperationStatus Status { get; }
        public T? Value { get; }
        public string? ErrorMessage { get; }
        public IReadOnlyDictionary<string, string>? FieldErrors { get; }

        public bool IsSuccess => Status == OperationStatus.Success;

        public static OperationResult<T> Success(T value)
        {
            return new(OperationStatus.Success, value, null, null);
        }

        public static OperationResult<T> NotFound(string? errorMessage = null)
        {
            return new(OperationStatus.NotFound, default, errorMessage, null);
        }

        public static OperationResult<T> Invalid(string? errorMessage, IReadOnlyDictionary<string, string>? fieldErrors = null)
        {
            return new(OperationStatus.Invalid, default, errorMessage, fieldErrors);
        }

        public static implicit operator OperationResult(OperationResult<T> result) => result.Status switch
        {
            OperationStatus.Success => OperationResult.Success(),
            OperationStatus.NotFound => OperationResult.NotFound(result.ErrorMessage),
            _ => result.FieldErrors is { } fields
                ? OperationResult.Invalid(result.ErrorMessage, fields)
                : OperationResult.Invalid(result.ErrorMessage),
        };
    }
}
