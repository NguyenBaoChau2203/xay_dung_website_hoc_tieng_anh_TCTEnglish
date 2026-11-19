using System.Threading;
using System.Threading.Tasks;

namespace TCTEnglish.Services.Listening
{
    /// <summary>The personal set that "Lưu vào Bộ thẻ" fills. Named exactly this so a user's earlier set is found again.</summary>
    public static class ListeningWordSet
    {
        public const string Name = "Từ vựng Luyện nghe";
        public const string Description = "Từ vựng bạn lưu khi luyện nghe.";

        /// <summary>Public URL of a personal set (the same route the set pages use).</summary>
        public static string UrlFor(int setId) => $"/Sets/{setId}";
    }

    /// <summary>Tap-a-word lookup and "save to my set" for the Listening practice page.</summary>
    public interface IListeningWordService
    {
        /// <summary>
        /// Looks a single word up for a lesson the caller may use. Lesson vocabulary is answered for everyone (inflections
        /// included). Any other word is answered by the dictionary only for Premium and Admin; others get a "locked" answer.
        /// </summary>
        Task<ListeningWordLookupResult> LookupAsync(int lessonId, string? term, int? userId, CancellationToken ct = default);

        /// <summary>Adds a word to the caller's own "Từ vựng Luyện nghe" set (created on the first save).</summary>
        Task<ListeningWordSaveResult> SaveAsync(int lessonId, ListeningWordSaveRequest? request, int userId, CancellationToken ct = default);
    }

    public enum ListeningWordLookupStatus
    {
        Found,
        InvalidTerm,
        NotFound,
        RateLimited,
        ProviderFailed
    }

    public sealed class ListeningWordLookupResult
    {
        public ListeningWordLookupStatus Status { get; private init; }

        /// <summary>"lesson", "dictionary" or "locked".</summary>
        public string? Source { get; private init; }
        public string? Word { get; private init; }
        public string? Definition { get; private init; }
        public string? Example { get; private init; }
        public string? Phonetic { get; private init; }

        /// <summary>True for "locked": the page shows the line translation and the Premium hint instead.</summary>
        public bool LineTranslationOnly { get; private init; }
        public int RetryAfterSeconds { get; private init; }

        private ListeningWordLookupResult() { }

        public static ListeningWordLookupResult Found(string source, string word, string? definition, string? example, string? phonetic)
            => new() { Status = ListeningWordLookupStatus.Found, Source = source, Word = word, Definition = definition, Example = example, Phonetic = phonetic };

        public static ListeningWordLookupResult Locked(string word)
            => new() { Status = ListeningWordLookupStatus.Found, Source = "locked", Word = word, LineTranslationOnly = true };

        public static ListeningWordLookupResult Failure(ListeningWordLookupStatus status, int retryAfterSeconds = 0)
            => new() { Status = status, RetryAfterSeconds = retryAfterSeconds };
    }

    /// <summary>JSON body of POST api/listening/word/save.</summary>
    public sealed class ListeningWordSaveRequest
    {
        public int LessonId { get; set; }
        public string? Term { get; set; }
        public string? Definition { get; set; }
        public string? Example { get; set; }
    }

    public enum ListeningWordSaveStatus
    {
        Saved,
        Duplicate,
        Invalid,
        NotFound,
        Failed
    }

    public sealed class ListeningWordSaveResult
    {
        public ListeningWordSaveStatus Status { get; private init; }
        public int? SetId { get; private init; }
        public string? SetUrl => SetId.HasValue ? ListeningWordSet.UrlFor(SetId.Value) : null;

        private ListeningWordSaveResult() { }

        public static ListeningWordSaveResult Of(ListeningWordSaveStatus status, int? setId = null) => new() { Status = status, SetId = setId };
    }
}
