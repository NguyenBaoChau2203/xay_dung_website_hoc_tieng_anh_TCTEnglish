namespace TCTEnglish.Services.Admin;

public static class AdminListeningMessages
{
    public const string LearnerLessonReadOnly = "Không sửa bài nghe riêng của học viên.";
    public const string LearnerCannotBePublished = "Bài nghe riêng của học viên không xuất bản vào danh mục chung.";
    public const string NoTranscriptToPublish = "Cần ít nhất một dòng lời thoại trước khi xuất bản.";
    public const string PublishedNeedsTranscript = "Bài đã xuất bản cần ít nhất một dòng lời thoại. Hãy chuyển về bản nháp trước khi xóa hết lời thoại.";
    public const string YoutubeInvalid = "Mã hoặc liên kết YouTube không hợp lệ.";
    public const string DurationInvalid = "Thời lượng dạng mm:ss, ví dụ 04:12.";
    public const string ImportNeedsYoutube = "Bài chưa có mã YouTube hợp lệ để nhập lời thoại.";
    public const string ImportFailed = "Không lấy được lời thoại từ YouTube lúc này. Hãy thử lại sau hoặc nhập tay.";
    public const string ImportEmpty = "YouTube không trả về lời thoại cho video này. Hãy nhập tay.";
    public const string ImportBusy = "Đang nhập lời thoại, vui lòng chờ.";
    public const string ConfirmTitleMismatch = "Tên xác nhận không khớp.";
    public const string NoQuizWarning = "Bài chưa có câu hỏi quiz.";
    public const string NoVocabWarning = "Bài chưa có từ vựng.";
    public const string NoTimedLinesWarning = "Bài dùng video YouTube nhưng lời thoại chưa có mốc thời gian.";
    public const string DuplicateYoutubeWarning = "Đã có bài nghe hệ thống khác dùng video này.";

    public static string ImportReplaceNeeded(int existing) => $"Bài đã có {existing} dòng lời thoại. Hãy xác nhận thay thế bằng lời thoại từ YouTube.";
}

/// <summary>Source is "system" (default), "learner" or "all". Status is "", "published", "draft" or "incomplete".</summary>
public sealed record AdminLessonQuery(
    string? Source = null,
    string? Level = null,
    string? Status = null,
    string? Q = null,
    int Page = 1,
    int PageSize = 20);

public sealed class AdminLessonRow
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Topic { get; init; } = "";
    public string Level { get; init; } = "";
    public string? Duration { get; init; }
    public string? YoutubeId { get; init; }
    public string? ThumbnailUrl { get; init; }
    public bool IsPublished { get; init; }
    public bool IsLearnerOwned { get; init; }
    public string? OwnerName { get; init; }
    public int TranscriptCount { get; init; }
    public int TimedLineCount { get; init; }
    public int QuizCount { get; init; }
    public int VocabCount { get; init; }
    public bool HasInvalidQuiz { get; init; }
    public DateTime CreatedAt { get; init; }
    /// <summary>No transcript, or a quiz question that cannot be answered.</summary>
    public bool IsIncomplete => TranscriptCount == 0 || HasInvalidQuiz;
}

public sealed class AdminLessonListResult
{
    public IReadOnlyList<AdminLessonRow> Rows { get; init; } = Array.Empty<AdminLessonRow>();
    public int TotalItems { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalPages { get; init; }
    /// <summary>The source actually applied ("system", "learner" or "all").</summary>
    public string Source { get; init; } = "system";
}

/// <summary>SystemLessons and Published are about system lessons; DraftOrIncomplete counts system lessons that are drafts or have no transcript.</summary>
public sealed record AdminLessonFigures(int SystemLessons, int Published, int DraftOrIncomplete, int LearnerLessons);

public sealed class AdminLessonInfoInput
{
    public string? Title { get; set; }
    public string? Level { get; set; }
    public string? Topic { get; set; }
    /// <summary>A bare id or any YouTube link; stored as the 11-character id.</summary>
    public string? YoutubeId { get; set; }
    public string? AudioUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Duration { get; set; }
    public string? Speaker1Name { get; set; }
    public string? Speaker1Country { get; set; }
    public string? Speaker2Name { get; set; }
    public string? Speaker2Country { get; set; }
    /// <summary>Only used when creating; the publish rules then apply (a new lesson has no transcript yet).</summary>
    public bool PublishNow { get; set; }
}

public sealed class AdminTranscriptLineInput
{
    public string? Speaker { get; set; }
    public string? Text { get; set; }
    public string? VietnameseMeaning { get; set; }
    public double? StartTime { get; set; }
    public double? EndTime { get; set; }
}

public sealed class AdminQuizInput
{
    public string? QuestionText { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? Explanation { get; set; }
}

public sealed class AdminVocabInput
{
    public string? Word { get; set; }
    public string? Definition { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ImageUrl { get; set; }
}

public sealed class AdminLessonEditor
{
    public int Id { get; init; }
    public bool IsNew => Id == 0;
    public AdminLessonInfoInput Info { get; init; } = new();
    public bool IsPublished { get; init; }
    public bool IsLearnerOwned { get; init; }
    public string? OwnerName { get; init; }
    public DateTime? CreatedAt { get; init; }
    public IReadOnlyList<AdminTranscriptLineInput> Lines { get; init; } = Array.Empty<AdminTranscriptLineInput>();
    public IReadOnlyList<AdminQuizInput> Questions { get; init; } = Array.Empty<AdminQuizInput>();
    public IReadOnlyList<AdminVocabInput> Items { get; init; } = Array.Empty<AdminVocabInput>();
    /// <summary>Learners who have progress on this lesson; their progress is removed when the lesson is deleted.</summary>
    public int LearnerProgressCount { get; init; }
    public bool HasTranscript => Lines.Count > 0;
    public bool HasTimedLines => Lines.Any(l => l.StartTime.HasValue && l.EndTime.HasValue);
    public bool HasQuiz => Questions.Count > 0;
    public bool HasVocab => Items.Count > 0;

    public static AdminLessonEditor NewLesson() => new() { Info = new AdminLessonInfoInput { Level = "A2" } };
}

public interface IAdminListeningService
{
    Task<AdminLessonListResult> ListLessonsAsync(AdminLessonQuery query, CancellationToken ct = default);
    Task<AdminLessonFigures> GetFiguresAsync(CancellationToken ct = default);
    Task<AdminLessonEditor?> GetEditorAsync(int id, CancellationToken ct = default);
    /// <summary>Creates a draft when <paramref name="id"/> is null, otherwise updates the info of a system lesson.</summary>
    Task<AdminResult<int>> SaveInfoAsync(int? id, AdminLessonInfoInput input, CancellationToken ct = default);
    Task<AdminResult> SaveTranscriptAsync(int id, IReadOnlyList<AdminTranscriptLineInput> lines, CancellationToken ct = default);
    /// <summary>Returns the number of imported lines. <paramref name="replaceExisting"/> must be true when the lesson already has lines.</summary>
    Task<AdminResult<int>> ImportTranscriptAsync(int id, bool replaceExisting, CancellationToken ct = default);
    Task<AdminResult> SaveQuizAsync(int id, IReadOnlyList<AdminQuizInput> questions, CancellationToken ct = default);
    Task<AdminResult> SaveVocabAsync(int id, IReadOnlyList<AdminVocabInput> items, CancellationToken ct = default);
    Task<AdminResult> SetPublishedAsync(int id, bool published, CancellationToken ct = default);
    Task<AdminResult> DeleteAsync(int id, string? confirmTitle, int adminId, CancellationToken ct = default);
}
