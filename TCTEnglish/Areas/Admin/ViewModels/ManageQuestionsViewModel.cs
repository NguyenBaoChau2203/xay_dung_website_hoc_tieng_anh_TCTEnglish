using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace TCTEnglish.Areas.Admin.ViewModels
{
    public class ManageQuestionsViewModel
    {
        public int PassageId { get; set; }
        public string PassageTitle { get; set; }

        // Danh sách câu hỏi đã có
        public List<ReadingQuestionItem> ExistingQuestions { get; set; } = new();

        // --- Dữ liệu để tạo câu hỏi MỚI ---
        [Required(ErrorMessage = "Vui lòng nhập nội dung câu hỏi")]
        public string NewQuestionText { get; set; }

        public int NewOrderIndex { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập đáp án A")]
        public string OptionA { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập đáp án B")]
        public string OptionB { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập đáp án C")]
        public string OptionC { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập đáp án D")]
        public string OptionD { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn đáp án đúng")]
        public string CorrectOption { get; set; } // "A", "B", "C", "D"
    }

    public class ReadingQuestionItem
    {
        public int Id { get; set; }
        public string QuestionText { get; set; }
        public int OrderIndex { get; set; }
        public List<string> Options { get; set; } = new();
        public string CorrectOptionText { get; set; }
    }
}