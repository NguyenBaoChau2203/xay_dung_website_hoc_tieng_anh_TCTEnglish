namespace TCTEnglish.ViewModels;

public sealed class RecentWritingExerciseViewModel : WritingExerciseCardViewModel
{
    public string Level { get; set; } = "";
    public string LevelTitle { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string ContentTypeTitle { get; set; } = "";
    public DateTime LastOpenedAtUtc { get; set; }
    public string LastOpenedAtDisplay { get; set; } = "";
    public string PracticeUrl => $"/Home/Writing/Practice?level={Uri.EscapeDataString(Level)}&contentType={Uri.EscapeDataString(ContentType)}&exerciseId={Id}";
}
