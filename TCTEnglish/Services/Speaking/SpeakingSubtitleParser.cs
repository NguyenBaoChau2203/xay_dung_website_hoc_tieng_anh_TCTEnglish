using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Speaking;

public static class SpeakingSubtitleParser
{
    private static readonly Regex CueTimeRegex = new(
        @"^(?:(?<sh>\d{2,}):)?(?<sm>\d{2}):(?<ss>\d{2})[\.,](?<sms>\d{3})\s+-->\s+(?:(?<eh>\d{2,}):)?(?<em>\d{2}):(?<es>\d{2})[\.,](?<ems>\d{3})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static (bool Ok, string? Error, List<SpeakingSentence> Sentences) Parse(
        string content,
        int maxCues = 2000,
        double maxDurationSeconds = 30 * 60)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return (false, "Subtitle trống.", new List<SpeakingSentence>());
        }

        content = content.TrimStart('\uFEFF');
        if (Encoding.UTF8.GetByteCount(content) > 1024 * 1024)
        {
            return (false, "Subtitle vượt quá 1 MiB.", new List<SpeakingSentence>());
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sentences = new List<SpeakingSentence>();
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            i++;
            if (line.Length == 0
                || line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("STYLE", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("REGION", StringComparison.OrdinalIgnoreCase)
                || line.All(char.IsDigit))
            {
                continue;
            }

            if (!line.Contains("-->", StringComparison.Ordinal))
            {
                continue;
            }

            var match = CueTimeRegex.Match(line);
            if (!match.Success || !TryToSeconds(match, "sh", "sm", "ss", "sms", out var start) || !TryToSeconds(match, "eh", "em", "es", "ems", out var end))
            {
                return (false, "Timestamp subtitle không hợp lệ.", new List<SpeakingSentence>());
            }

            if (!(start >= 0 && end > start && end <= maxDurationSeconds))
            {
                return (false, "Timestamp subtitle không hợp lệ.", new List<SpeakingSentence>());
            }

            var textBuilder = new StringBuilder();
            while (i < lines.Length && lines[i].Trim().Length > 0 && !lines[i].Contains("-->", StringComparison.Ordinal))
            {
                if (textBuilder.Length > 0)
                {
                    textBuilder.Append(' ');
                }

                textBuilder.Append(lines[i].Trim());
                i++;
            }

            var text = Regex.Replace(textBuilder.ToString(), "<[^>]+>", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return (false, "Cue không có nội dung.", new List<SpeakingSentence>());
            }

            if (sentences.Count > 0 && start < sentences[^1].EndTime)
            {
                return (false, "Cue chồng thời gian hoặc không theo thứ tự.", new List<SpeakingSentence>());
            }

            sentences.Add(new SpeakingSentence
            {
                StartTime = start,
                EndTime = end,
                Text = text,
                VietnameseMeaning = string.Empty
            });

            if (sentences.Count > maxCues)
            {
                return (false, "Subtitle vượt quá số cue cho phép.", new List<SpeakingSentence>());
            }
        }

        if (sentences.Count == 0)
        {
            return (false, "Không parse được cue hợp lệ.", new List<SpeakingSentence>());
        }

        return (true, null, sentences);
    }

    private static bool TryToSeconds(Match match, string hourGroup, string minuteGroup, string secondGroup, string milliGroup, out double seconds)
    {
        seconds = 0;
        if (!int.TryParse(match.Groups[minuteGroup].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || !int.TryParse(match.Groups[secondGroup].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var secs)
            || !int.TryParse(match.Groups[milliGroup].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var millis)
            || minutes is < 0 or > 59
            || secs is < 0 or > 59)
        {
            return false;
        }

        var hours = 0;
        if (match.Groups[hourGroup].Success
            && !int.TryParse(match.Groups[hourGroup].Value, NumberStyles.None, CultureInfo.InvariantCulture, out hours))
        {
            return false;
        }

        seconds = hours * 3600d + minutes * 60d + secs + millis / 1000d;
        return true;
    }
}
