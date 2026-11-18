using System.Text.RegularExpressions;

namespace TCTEnglish.Services.Speaking;

/// <summary>Presentation-only formatting; persisted source cues remain unchanged.</summary>
public static partial class TranscriptDisplayText
{
    public static string Format(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var clean = SpeakerMarker().Replace(text, string.Empty);
        return HorizontalWhitespace().Replace(clean, " ").Trim();
    }

    // YouTube speaker markers at a line start or before a spoken word. Keep
    // ordinary comparisons, numeric shift operators and URL fragments intact.
    [GeneratedRegex("""(?m)(?:^[\t ]*>>(?!>)[\t ]*|(?<=\s)>>(?!>)[\t ]*(?=[\p{L}"'\[]))""")]
    private static partial Regex SpeakerMarker();

    [GeneratedRegex(@"[^\S\r\n]+")]
    private static partial Regex HorizontalWhitespace();
}
