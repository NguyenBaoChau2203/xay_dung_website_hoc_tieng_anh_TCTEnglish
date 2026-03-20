using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.ViewModels
{
    public class ContactFormViewModel
    {
        public const string GeneralSubject = "general";
        public const string AppealSubject = "khieu-nai-khoa-tai-khoan";
        public const string BugSubject = "bug";
        public const string FeedbackSubject = "feedback";
        public const string OtherSubject = "other";

        public static IReadOnlyDictionary<string, string> SubjectOptions { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GeneralSubject] = "Câu hỏi chung",
                [AppealSubject] = "Khiếu nại tài khoản",
                [BugSubject] = "Báo lỗi / Bug",
                [FeedbackSubject] = "Góp ý cải tiến",
                [OtherSubject] = "Khác"
            };

        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 kí tự.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(254, ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn chủ đề.")]
        [StringLength(50)]
        public string Subject { get; set; } = GeneralSubject;

        [Required(ErrorMessage = "Vui lòng nhập nội dung.")]
        [StringLength(2000, ErrorMessage = "Nội dung không được vượt quá 2000 kí tự.")]
        public string Message { get; set; } = string.Empty;

        public string SubjectDisplayName =>
            SubjectOptions.TryGetValue(Subject, out var label) ? label : SubjectOptions[GeneralSubject];

        public static string NormalizeSubject(string? subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                return GeneralSubject;
            }

            var normalized = subject.Trim().ToLowerInvariant();
            return SubjectOptions.ContainsKey(normalized) ? normalized : GeneralSubject;
        }
    }
}
