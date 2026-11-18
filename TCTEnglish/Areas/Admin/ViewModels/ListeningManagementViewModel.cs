using TCTEnglish.Services.Admin;

namespace TCTVocabulary.Areas.Admin.ViewModels
{
    /// <summary>List page of listening lessons. Rows and figures come from IAdminListeningService.</summary>
    public class ListeningIndexViewModel
    {
        public AdminLessonListResult List { get; set; } = new();
        public AdminLessonFigures Figures { get; set; } = new(0, 0, 0, 0);
        public string? Q { get; set; }
        public string Level { get; set; } = "";
        /// <summary>"system" (default), "learner" or "all".</summary>
        public string Source { get; set; } = "system";
        /// <summary>"", "published", "draft" or "incomplete".</summary>
        public string Status { get; set; } = "";
    }

    /// <summary>One transcript row as typed in the form: times are text such as "1:05.5" and are parsed on the server.</summary>
    public class TranscriptRowPost
    {
        public string? Speaker { get; set; }
        public string? Text { get; set; }
        public string? VietnameseMeaning { get; set; }
        public string? Start { get; set; }
        public string? End { get; set; }
    }

    /// <summary>One editable row of a tab. <see cref="Index"/> is a number, or "__i__" for the template the browser clones.</summary>
    public sealed record RowView<T>(string Index, int Number, T Row, IReadOnlyDictionary<string, string> Errors, string ErrorPrefix)
    {
        public string? Error(string field) => Errors.TryGetValue($"{ErrorPrefix}[{Index}].{field}", out var message) ? message : null;
        public bool HasError => Errors.Keys.Any(k => k.StartsWith($"{ErrorPrefix}[{Index}].", StringComparison.Ordinal));
    }

    public sealed record SaveBarModel(string SaveText, string DiscardUrl);

    public static class LessonTabs
    {
        public const string Info = "info";
        public const string Transcript = "transcript";
        public const string Quiz = "quiz";
        public const string Vocab = "vocab";

        public static string Normalize(string? tab) => tab is Transcript or Quiz or Vocab ? tab : Info;
    }

    /// <summary>The four-tab lesson editor. Each tab saves itself; the values shown are the posted ones after an error.</summary>
    public class LessonEditorViewModel
    {
        public AdminLessonEditor Editor { get; set; } = AdminLessonEditor.NewLesson();
        public string Tab { get; set; } = LessonTabs.Info;
        public AdminLessonInfoInput Info { get; set; } = new();
        public List<TranscriptRowPost> Lines { get; set; } = new();
        public List<AdminQuizInput> Questions { get; set; } = new();
        public List<AdminVocabInput> Items { get; set; } = new();
        public IReadOnlyDictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();
        public string? Summary { get; set; }

        public bool ReadOnly => Editor.IsLearnerOwned;

        public static LessonEditorViewModel From(AdminLessonEditor editor, string tab) => new()
        {
            Editor = editor,
            Tab = LessonTabs.Normalize(tab),
            Info = editor.Info,
            Lines = editor.Lines.Select(l => new TranscriptRowPost
            {
                Speaker = l.Speaker,
                Text = l.Text,
                VietnameseMeaning = l.VietnameseMeaning,
                Start = AdminInputRules.FormatClock(l.StartTime),
                End = AdminInputRules.FormatClock(l.EndTime),
            }).ToList(),
            Questions = editor.Questions.ToList(),
            Items = editor.Items.ToList(),
        };
    }
}
