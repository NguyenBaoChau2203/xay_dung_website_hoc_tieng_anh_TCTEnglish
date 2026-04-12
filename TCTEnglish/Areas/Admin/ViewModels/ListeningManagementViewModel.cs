using System.ComponentModel.DataAnnotations;
using TCTVocabulary.Models;

namespace TCTVocabulary.Areas.Admin.ViewModels;

public class ListeningLessonListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Level { get; set; } = null!;
    public string Topic { get; set; } = null!;
    public string? YoutubeId { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TranscriptCount { get; set; }
    public int QuizCount { get; set; }
    public int VocabCount { get; set; }
}

public class ListeningLessonIndexViewModel
{
    public List<ListeningLessonListItemViewModel> Lessons { get; set; } = new();
    public string? SearchToken { get; set; }
    public string? LevelFilter { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
}

public class ListeningLessonCreateEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "Cấp độ là bắt buộc")]
    [RegularExpression("^(A1|A2|B1|B2|C1|C2)$", ErrorMessage = "Cấp độ phải là A1-C2")]
    public string Level { get; set; } = "A1";

    [Required(ErrorMessage = "Chủ đề là bắt buộc")]
    [MaxLength(100)]
    public string Topic { get; set; } = "General";

    [MaxLength(50)]
    public string? YoutubeId { get; set; }

    [MaxLength(500)]
    public string? AudioUrl { get; set; }

    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    [MaxLength(20)]
    public string? Duration { get; set; }

    [MaxLength(100)]
    public string? Speaker1Name { get; set; }

    [MaxLength(50)]
    public string? Speaker1Country { get; set; }

    [MaxLength(100)]
    public string? Speaker2Name { get; set; }

    [MaxLength(50)]
    public string? Speaker2Country { get; set; }

    public bool IsPublished { get; set; }
}

public class ListeningTranscriptManageViewModel
{
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = null!;
    public string? YoutubeId { get; set; }
    public List<ListeningTranscriptLineViewModel> Lines { get; set; } = new();
}

public class ListeningTranscriptLineViewModel
{
    public int Id { get; set; }
    public int OrderIndex { get; set; }
    [Required]
    public string Speaker { get; set; } = "Speaker 1";
    [Required]
    public string Text { get; set; } = null!;
    public string? VietnameseMeaning { get; set; }
    public double? StartTime { get; set; }
    public double? EndTime { get; set; }
}

public class ListeningQuizManageViewModel
{
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = null!;
    public List<ListeningQuizQuestionViewModel> Questions { get; set; } = new();
}

public class ListeningQuizQuestionViewModel
{
    public int Id { get; set; }
    public int OrderIndex { get; set; }
    [Required]
    public string QuestionText { get; set; } = null!;
    [Required]
    public string OptionA { get; set; } = null!;
    [Required]
    public string OptionB { get; set; } = null!;
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    [Required]
    [RegularExpression("^[ABCD]$")]
    public string CorrectAnswer { get; set; } = "A";
    public string? Explanation { get; set; }
}

public class ListeningVocabManageViewModel
{
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = null!;
    public List<ListeningVocabItemViewModel> Items { get; set; } = new();
}

public class ListeningVocabItemViewModel
{
    public int Id { get; set; }
    public int OrderIndex { get; set; }
    [Required]
    public string Word { get; set; } = null!;
    [Required]
    public string Definition { get; set; } = null!;
    public string? ExampleSentence { get; set; }
    public string? ImageUrl { get; set; }
}
