namespace TCTEnglish.Services.AI;

public interface IOpenAiEmbeddingClient
{
    Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct);

    /// <summary>
    /// Same transport as <see cref="EmbedAsync(IReadOnlyList{string}, CancellationToken)"/>.
    /// A maintenance job passes its shared attempt ledger and a freshness check that
    /// runs before every HTTP send, including a retry. Callers that do not override
    /// this method use the default single-attempt adapter below.
    /// </summary>
    async Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, EmbeddingInvocation invocation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(invocation);
        ct.ThrowIfCancellationRequested();
        if (invocation.ConfirmCurrent is not null && !await invocation.ConfirmCurrent(ct))
        {
            throw new AiProviderException("Corpus changed before embedding.", AiProviderException.ErrorCodeCorpusStale, false);
        }

        ct.ThrowIfCancellationRequested();
        var ledger = invocation.Budget;
        if (ledger is not null && !ledger.TryReserveAttempt(EmbeddingTokenBound.Sum(inputs)))
        {
            throw new AiProviderException("Embedding budget exhausted.", AiProviderException.ErrorCodeBudgetExhausted, false);
        }

        try
        {
            var batch = await EmbedAsync(inputs, ct);
            if (batch.PromptTokens > 0 && batch.PromptTokens == batch.TotalTokens)
            {
                ledger?.RecordKnownUsage(batch.PromptTokens);
            }
            else
            {
                ledger?.MarkPendingUsageUnknown();
            }

            return batch;
        }
        catch
        {
            ledger?.MarkPendingUsageUnknown();
            throw;
        }
    }
}
