namespace TCTEnglish.Services.Admin;

public enum AdminResultStatus
{
    Success,
    NotFound,
    Invalid,
    /// <summary>The caller is an admin but the rule forbids the change (for example editing a learner's own content).</summary>
    Forbidden,
}

/// <summary>
/// Outcome of an admin service call. Unlike the shared OperationResult it can say "forbidden",
/// carry per-row messages (keyed like "Lines[2].EndTime") and non-blocking warnings.
/// </summary>
public sealed class AdminResult
{
    private static readonly IReadOnlyList<string> NoWarnings = Array.Empty<string>();

    private AdminResult(AdminResultStatus status, string? message, IReadOnlyDictionary<string, string>? fieldErrors, IReadOnlyList<string>? warnings)
    {
        Status = status;
        Message = message;
        FieldErrors = fieldErrors ?? new Dictionary<string, string>();
        Warnings = warnings ?? NoWarnings;
    }

    public AdminResultStatus Status { get; }
    public string? Message { get; }
    public IReadOnlyDictionary<string, string> FieldErrors { get; }
    public IReadOnlyList<string> Warnings { get; }
    public bool IsSuccess => Status == AdminResultStatus.Success;

    public static AdminResult Success(IReadOnlyList<string>? warnings = null) => new(AdminResultStatus.Success, null, null, warnings);
    public static AdminResult NotFound(string? message = null) => new(AdminResultStatus.NotFound, message, null, null);
    public static AdminResult Forbidden(string message) => new(AdminResultStatus.Forbidden, message, null, null);
    public static AdminResult Invalid(string? message, IReadOnlyDictionary<string, string>? fieldErrors = null) => new(AdminResultStatus.Invalid, message, fieldErrors, null);
}

/// <summary><see cref="AdminResult"/> that carries a value on success.</summary>
public sealed class AdminResult<T>
{
    private AdminResult(AdminResult inner, T? value)
    {
        Inner = inner;
        Value = value;
    }

    private AdminResult Inner { get; }
    public T? Value { get; }
    public AdminResultStatus Status => Inner.Status;
    public string? Message => Inner.Message;
    public IReadOnlyDictionary<string, string> FieldErrors => Inner.FieldErrors;
    public IReadOnlyList<string> Warnings => Inner.Warnings;
    public bool IsSuccess => Inner.IsSuccess;

    public static AdminResult<T> Success(T value, IReadOnlyList<string>? warnings = null) => new(AdminResult.Success(warnings), value);
    public static AdminResult<T> NotFound(string? message = null) => new(AdminResult.NotFound(message), default);
    public static AdminResult<T> Forbidden(string message) => new(AdminResult.Forbidden(message), default);
    public static AdminResult<T> Invalid(string? message, IReadOnlyDictionary<string, string>? fieldErrors = null) => new(AdminResult.Invalid(message, fieldErrors), default);

    public static implicit operator AdminResult(AdminResult<T> result) => result.Inner;
}
