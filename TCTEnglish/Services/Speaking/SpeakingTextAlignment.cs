namespace TCTEnglish.Services.Speaking;

public enum SpeakingAlignmentKind
{
    Matched = 1,
    Missing = 2,
    Extra = 3,
    Substituted = 4
}

public sealed class SpeakingAlignmentOperation
{
    public required SpeakingAlignmentKind Kind { get; init; }
    public string ReferenceToken { get; init; } = string.Empty;
    public string HypothesisToken { get; init; } = string.Empty;
}

public sealed class SpeakingTextAlignmentResult
{
    public required bool ComparisonAvailable { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<SpeakingAlignmentOperation> Operations { get; init; }
        = Array.Empty<SpeakingAlignmentOperation>();
}

/// <summary>
/// Deterministic token alignment for recognition feedback. Uses the same
/// tokenization as <see cref="SpeakingTextMatch"/>. Not a phoneme/fluency score.
/// </summary>
public static class SpeakingTextAlignment
{
    public const int MaxTokensPerSide = 256;

    public static SpeakingTextAlignmentResult Compare(string? reference, string? hypothesis)
    {
        var referenceTokens = SpeakingTextMatch.Tokenize(reference);
        var hypothesisTokens = SpeakingTextMatch.Tokenize(hypothesis);

        if (referenceTokens.Count > MaxTokensPerSide || hypothesisTokens.Count > MaxTokensPerSide)
        {
            return new SpeakingTextAlignmentResult
            {
                ComparisonAvailable = false,
                UnavailableReason = "token_limit",
                Operations = Array.Empty<SpeakingAlignmentOperation>()
            };
        }

        return new SpeakingTextAlignmentResult
        {
            ComparisonAvailable = true,
            Operations = Align(referenceTokens, hypothesisTokens)
        };
    }

    private static IReadOnlyList<SpeakingAlignmentOperation> Align(
        IReadOnlyList<string> referenceTokens,
        IReadOnlyList<string> hypothesisTokens)
    {
        var n = referenceTokens.Count;
        var m = hypothesisTokens.Count;
        var dist = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++)
        {
            dist[i, 0] = i;
        }

        for (var j = 0; j <= m; j++)
        {
            dist[0, j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var equal = string.Equals(
                    referenceTokens[i - 1],
                    hypothesisTokens[j - 1],
                    StringComparison.Ordinal);
                var diagonal = dist[i - 1, j - 1] + (equal ? 0 : 1);
                var deleteCost = dist[i - 1, j] + 1;
                var insertCost = dist[i, j - 1] + 1;
                dist[i, j] = Math.Min(diagonal, Math.Min(deleteCost, insertCost));
            }
        }

        // Backtrace: prefer exact match, then missing, then extra, then substitute.
        // Tie-break is fixed so repeated tokens stay deterministic.
        var operations = new List<SpeakingAlignmentOperation>();
        var iCursor = n;
        var jCursor = m;
        while (iCursor > 0 || jCursor > 0)
        {
            if (iCursor > 0 && jCursor > 0
                && string.Equals(referenceTokens[iCursor - 1], hypothesisTokens[jCursor - 1], StringComparison.Ordinal)
                && dist[iCursor, jCursor] == dist[iCursor - 1, jCursor - 1])
            {
                operations.Add(new SpeakingAlignmentOperation
                {
                    Kind = SpeakingAlignmentKind.Matched,
                    ReferenceToken = referenceTokens[iCursor - 1],
                    HypothesisToken = hypothesisTokens[jCursor - 1]
                });
                iCursor--;
                jCursor--;
                continue;
            }

            if (iCursor > 0 && dist[iCursor, jCursor] == dist[iCursor - 1, jCursor] + 1)
            {
                // Peek: if next best is an insert of a different token, fold into substitute.
                if (jCursor > 0
                    && dist[iCursor, jCursor] == dist[iCursor - 1, jCursor - 1] + 1
                    && !string.Equals(referenceTokens[iCursor - 1], hypothesisTokens[jCursor - 1], StringComparison.Ordinal)
                    && dist[iCursor - 1, jCursor] + 1 == dist[iCursor, jCursor - 1] + 1)
                {
                    operations.Add(new SpeakingAlignmentOperation
                    {
                        Kind = SpeakingAlignmentKind.Substituted,
                        ReferenceToken = referenceTokens[iCursor - 1],
                        HypothesisToken = hypothesisTokens[jCursor - 1]
                    });
                    iCursor--;
                    jCursor--;
                    continue;
                }

                operations.Add(new SpeakingAlignmentOperation
                {
                    Kind = SpeakingAlignmentKind.Missing,
                    ReferenceToken = referenceTokens[iCursor - 1]
                });
                iCursor--;
                continue;
            }

            if (jCursor > 0 && dist[iCursor, jCursor] == dist[iCursor, jCursor - 1] + 1)
            {
                operations.Add(new SpeakingAlignmentOperation
                {
                    Kind = SpeakingAlignmentKind.Extra,
                    HypothesisToken = hypothesisTokens[jCursor - 1]
                });
                jCursor--;
                continue;
            }

            operations.Add(new SpeakingAlignmentOperation
            {
                Kind = SpeakingAlignmentKind.Substituted,
                ReferenceToken = referenceTokens[iCursor - 1],
                HypothesisToken = hypothesisTokens[jCursor - 1]
            });
            iCursor--;
            jCursor--;
        }

        operations.Reverse();
        return CollapseAdjacentMissingExtra(operations);
    }

    /// <summary>
    /// Fold adjacent missing+extra (either order) into a single substituted op for clearer UI.
    /// </summary>
    private static IReadOnlyList<SpeakingAlignmentOperation> CollapseAdjacentMissingExtra(
        IReadOnlyList<SpeakingAlignmentOperation> operations)
    {
        var collapsed = new List<SpeakingAlignmentOperation>(operations.Count);
        for (var index = 0; index < operations.Count; index++)
        {
            var current = operations[index];
            if (index + 1 < operations.Count)
            {
                var next = operations[index + 1];
                if (current.Kind == SpeakingAlignmentKind.Missing && next.Kind == SpeakingAlignmentKind.Extra)
                {
                    collapsed.Add(new SpeakingAlignmentOperation
                    {
                        Kind = SpeakingAlignmentKind.Substituted,
                        ReferenceToken = current.ReferenceToken,
                        HypothesisToken = next.HypothesisToken
                    });
                    index++;
                    continue;
                }

                if (current.Kind == SpeakingAlignmentKind.Extra && next.Kind == SpeakingAlignmentKind.Missing)
                {
                    collapsed.Add(new SpeakingAlignmentOperation
                    {
                        Kind = SpeakingAlignmentKind.Substituted,
                        ReferenceToken = next.ReferenceToken,
                        HypothesisToken = current.HypothesisToken
                    });
                    index++;
                    continue;
                }
            }

            collapsed.Add(current);
        }

        return collapsed;
    }
}
