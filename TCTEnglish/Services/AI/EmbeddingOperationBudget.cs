using System.Text;

namespace TCTEnglish.Services.AI;

/// <summary>
/// One maintenance job's attempt and usage ledger. The chars/4 estimate is not
/// an input to this type. Callers reserve a request before every HTTP send,
/// including a transport retry, and record usage as soon as a response proves it.
/// </summary>
public sealed class EmbeddingOperationBudget
{
    private int _pendingTokenUpperBound;
    private bool _hasPendingAttempt;
    public EmbeddingOperationBudget(int maxRequests, int maxInputTokens)
    {
        if (maxRequests < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRequests));
        }

        if (maxInputTokens < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInputTokens));
        }

        MaxRequests = maxRequests;
        MaxInputTokens = maxInputTokens;
    }

    public int MaxRequests { get; }

    public int MaxInputTokens { get; }

    public int Attempts { get; private set; }

    public int KnownPromptTokens { get; private set; }

    /// <summary>Conservative cost retained for sends whose usage is unknown.</summary>
    public int UncertainPromptTokenUpperBound { get; private set; }

    public int AvailableInputTokens => (int)Math.Max(0L, (long)MaxInputTokens
        - KnownPromptTokens - UncertainPromptTokenUpperBound - _pendingTokenUpperBound);

    /// <summary>
    /// True when a send completed without a usable usage object, or when a
    /// checked addition overflowed. KnownPromptTokens remains the sum already proved.
    /// </summary>
    public bool UsageIncomplete { get; private set; }

    public bool CanAttempt => Attempts < MaxRequests;

    public bool TryReserveAttempt(int inputTokenUpperBound = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokenUpperBound);
        MarkPendingUsageUnknown();
        if (!CanAttempt || inputTokenUpperBound > AvailableInputTokens)
        {
            return false;
        }

        Attempts++;
        _pendingTokenUpperBound = inputTokenUpperBound;
        _hasPendingAttempt = true;
        return true;
    }

    public void RecordKnownUsage(int promptTokens)
    {
        _pendingTokenUpperBound = 0;
        _hasPendingAttempt = false;
        if (promptTokens < 0)
        {
            promptTokens = 0;
        }

        try
        {
            KnownPromptTokens = checked(KnownPromptTokens + promptTokens);
        }
        catch (OverflowException)
        {
            KnownPromptTokens = int.MaxValue;
            UsageIncomplete = true;
        }
    }

    public void MarkUsageUnknown()
    {
        UsageIncomplete = true;
        if (_hasPendingAttempt)
        {
            UncertainPromptTokenUpperBound = (int)Math.Min(int.MaxValue,
                (long)UncertainPromptTokenUpperBound + _pendingTokenUpperBound);
            _pendingTokenUpperBound = 0;
            _hasPendingAttempt = false;
        }
    }

    public void MarkPendingUsageUnknown()
    {
        if (_hasPendingAttempt) MarkUsageUnknown();
    }
}

/// <summary>
/// Per-call instructions for one embeddings invocation. The budget is shared
/// across batches and retries of the same maintenance job.
/// </summary>
public sealed class EmbeddingInvocation
{
    public EmbeddingOperationBudget? Budget { get; init; }

    public Func<CancellationToken, Task<bool>>? ConfirmCurrent { get; init; }

    public Func<int, int, CancellationToken, Task<bool>>? RecordUsage { get; init; }
}

/// <summary>
/// Admission ceiling for text-embedding-3-small. The model uses byte-level BPE,
/// so merges can only reduce the token count and the count cannot exceed the
/// UTF-8 byte length. This is not a tokenizer measurement and not the chars/4 estimate.
/// </summary>
public static class EmbeddingTokenBound
{
    public static int Upper(string text)
    {
        var bytes = Encoding.UTF8.GetByteCount(text);
        return Math.Max(1, bytes);
    }

    public static int Sum(IEnumerable<string> texts)
    {
        var total = 0;
        foreach (var text in texts)
        {
            try
            {
                total = checked(total + Upper(text));
            }
            catch (OverflowException)
            {
                return int.MaxValue;
            }
        }

        return total;
    }

    public static bool Fits(IReadOnlyList<string> texts, int maxTokens, int maxRequests, out int safeTokens, out int safeRequests)
    {
        safeTokens = 0;
        safeRequests = 0;
        if (texts.Count == 0)
        {
            return true;
        }

        var bounds = new int[texts.Count];
        for (var index = 0; index < texts.Count; index++)
        {
            bounds[index] = Upper(texts[index]);
            if (bounds[index] > maxTokens)
            {
                safeTokens = int.MaxValue;
                safeRequests = int.MaxValue;
                return false;
            }

            try
            {
                safeTokens = checked(safeTokens + bounds[index]);
            }
            catch (OverflowException)
            {
                safeTokens = int.MaxValue;
                safeRequests = int.MaxValue;
                return false;
            }
        }

        if (safeTokens > maxTokens)
        {
            safeRequests = int.MaxValue;
            return false;
        }

        var cursor = 0;
        while (cursor < bounds.Length)
        {
            var used = 0;
            var count = 0;
            while (cursor < bounds.Length
                && count < EmbeddingOptions.MaxInputsPerRequest
                && used <= maxTokens - bounds[cursor])
            {
                used += bounds[cursor];
                count++;
                cursor++;
            }

            if (count == 0)
            {
                safeRequests = int.MaxValue;
                return false;
            }

            safeRequests++;
        }

        return safeRequests <= maxRequests;
    }
}
