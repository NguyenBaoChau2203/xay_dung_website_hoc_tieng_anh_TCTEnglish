using System.Text;

namespace TCTEnglish.Services.Speaking;

/// <summary>
/// Ordered token overlap vs reference. Measures transcript match, not fluency/prosody.
/// </summary>
public static class SpeakingTextMatch
{
    public static double ScorePercent(string? reference, string? hypothesis)
    {
        var referenceTokens = Tokenize(reference);
        var hypothesisTokens = Tokenize(hypothesis);
        if (referenceTokens.Count == 0)
        {
            return hypothesisTokens.Count == 0 ? 100d : 0d;
        }

        if (hypothesisTokens.Count == 0)
        {
            return 0d;
        }

        // Longest common subsequence ratio against reference length.
        var lcs = LongestCommonSubsequence(referenceTokens, hypothesisTokens);
        return Math.Clamp(100d * lcs / referenceTokens.Count, 0d, 100d);
    }

    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var normalized = Normalize(text);
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) || ch is '\'' or '-')
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }
            }
        }

        return builder.ToString().Trim();
    }

    private static int LongestCommonSubsequence(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var n = a.Count;
        var m = b.Count;
        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                if (string.Equals(a[i - 1], b[j - 1], StringComparison.Ordinal))
                {
                    curr[j] = prev[j - 1] + 1;
                }
                else
                {
                    curr[j] = Math.Max(prev[j], curr[j - 1]);
                }
            }

            (prev, curr) = (curr, prev);
            Array.Clear(curr);
        }

        return prev[m];
    }
}
