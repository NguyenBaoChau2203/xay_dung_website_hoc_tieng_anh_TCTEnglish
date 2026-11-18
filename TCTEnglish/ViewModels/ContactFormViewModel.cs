using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TCTEnglish.Services.Contact;

namespace TCTVocabulary.ViewModels
{
    public enum ContactFormState { Ready, Invalid, Unavailable, Failed, Unknown, Throttled, Duplicate, Expired }

    public class ContactFormViewModel : IValidatableObject
    {
        private string _name = string.Empty;
        private string _email = string.Empty;
        private string _subject = string.Empty;
        private string _message = string.Empty;
        private bool _nameHadLineBreak;
        private bool _emailHadLineBreak;
        private bool _subjectHadLineBreak;
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
        public string Name
        {
            get => _name;
            set { _nameHadLineBreak = ContactInput.HasLineBreak(value ?? string.Empty); _name = value?.Trim() ?? string.Empty; }
        }

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(254, ErrorMessage = "Email không hợp lệ.")]
        public string Email
        {
            get => _email;
            set { _emailHadLineBreak = ContactInput.HasLineBreak(value ?? string.Empty); _email = value?.Trim() ?? string.Empty; }
        }

        [Required(ErrorMessage = "Vui lòng chọn chủ đề.")]
        [StringLength(50)]
        public string Subject
        {
            get => _subject;
            set { _subjectHadLineBreak = ContactInput.HasLineBreak(value ?? string.Empty); _subject = value?.Trim().ToLowerInvariant() ?? string.Empty; }
        }

        [Required(ErrorMessage = "Vui lòng nhập nội dung.")]
        [StringLength(2000, ErrorMessage = "Nội dung không được vượt quá 2000 kí tự.")]
        public string Message { get => _message; set => _message = value?.Trim() ?? string.Empty; }

        [Required(ErrorMessage = "Biểu mẫu đã hết hạn. Vui lòng gửi lại từ biểu mẫu mới.")]
        [StringLength(2048)]
        public string ContactNonce { get; set; } = string.Empty;

        [BindNever]
        public ContactFormState State { get; set; }

        public string? StatusMessage => State switch
        {
            ContactFormState.Invalid => "Vui lòng kiểm tra các thông tin đã nhập.",
            ContactFormState.Unavailable => "Kênh gửi hỗ trợ hiện chưa sẵn sàng. Nội dung của bạn được giữ lại; vui lòng thử lại sau.",
            ContactFormState.Failed => "Chưa gửi được yêu cầu. Nội dung được giữ lại; bạn có thể chủ động gửi lại.",
            ContactFormState.Unknown => "Chưa xác nhận được kết quả gửi. Thư có thể đã được tiếp nhận; vui lòng không gửi lại ngay để tránh trùng lặp. Nội dung của bạn vẫn được giữ lại.",
            ContactFormState.Throttled => "Đã đạt giới hạn gửi hoặc hệ thống đang bận. Vui lòng chờ rồi thử lại; nội dung được giữ lại.",
            ContactFormState.Duplicate => "Yêu cầu này đã được xử lý hoặc đang gửi. Vui lòng không gửi lại để tránh trùng lặp.",
            ContactFormState.Expired => "Biểu mẫu không còn hợp lệ hoặc đã hết hạn. Nội dung được giữ lại trong biểu mẫu mới; hãy kiểm tra trước khi gửi.",
            _ => null
        };

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (_emailHadLineBreak || !ContactInput.IsMailbox(Email))
                yield return new ValidationResult("Vui lòng nhập một địa chỉ email hợp lệ.", new[] { nameof(Email) });
            if (_nameHadLineBreak)
                yield return new ValidationResult("Họ tên không được chứa ký tự xuống dòng.", new[] { nameof(Name) });
            if (_subjectHadLineBreak || !SubjectOptions.ContainsKey(Subject))
                yield return new ValidationResult("Vui lòng chọn chủ đề hợp lệ.", new[] { nameof(Subject) });
        }

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
