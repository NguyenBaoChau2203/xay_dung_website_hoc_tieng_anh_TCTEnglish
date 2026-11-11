using TCTEnglish.Services.AI.Rag.Corpus;

namespace TCTEnglish.Services.AI.Rag;

public sealed record GroundedTurnInput(
    int UserId,
    string Message,
    IReadOnlyList<AiContextMessage> Context,
    IReadOnlyList<GroundedHistoryTurn> History,
    ChatTurnBudgetDecision? Budget,
    IAiTurnStageJournal? Journal);

public sealed record GroundedTurnExecution(AiChatAnswer Answer);

public interface IGroundedChatPipeline
{
    Task<GroundedTurnExecution> ExecuteAsync(GroundedTurnInput input, CancellationToken ct);
}

public sealed class GroundedChatPipeline : IGroundedChatPipeline
{
    private readonly IAiChatAnswerService _answers;
    private readonly IHybridRetrievalService _hybrid;
    private readonly IChatSentenceSelector _selector;
    private readonly IAiTokenCounter _tokens;

    public GroundedChatPipeline(
        IAiChatAnswerService answers,
        IHybridRetrievalService hybrid,
        IChatSentenceSelector selector,
        IAiTokenCounter tokens)
    {
        _answers = answers;
        _hybrid = hybrid;
        _selector = selector;
        _tokens = tokens;
    }

    public async Task<GroundedTurnExecution> ExecuteAsync(GroundedTurnInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var deterministic = await _answers.AnswerAsync(input.UserId, input.Message, input.Context, ct);
        if (deterministic.Outcome is HybridRagOutcome.Answered or HybridRagOutcome.NeedsClarification or HybridRagOutcome.AccessDenied)
        {
            return new GroundedTurnExecution(deterministic);
        }

        if (!GroundedAnswerComposer.ShouldRetrieve(input.Message, input.History) || input.Budget is not { AllowEmbedding: true } budget)
        {
            return new GroundedTurnExecution(deterministic);
        }

        var journal = input.Journal;
        if (journal is not null && !await journal.BeginAsync(AiQuotaTurnStage.Embedding, budget.EmbeddingUpper, ct))
        {
            return Lost(input);
        }

        var retrievalQuery = GroundedAnswerComposer.RetrievalQuery(input.Message, input.History);
        var hybrid = await _hybrid.RetrieveAsync(
            input.UserId,
            retrievalQuery,
            new HybridRetrievalRequest(true)
            {
                EmbeddingAttempts = Math.Max(1, budget.EmbeddingAttempts),
                ConfirmCurrent = journal is null ? null : journal.OwnsAsync,
                RecordUsage = journal is null ? null : (known, unknown, token) => journal.RecordUsageAsync(AiQuotaTurnStage.Embedding, known, unknown, token)
            },
            ct);
        if (journal is not null)
        {
            var unknownEmbedding = hybrid.EmbeddingUnknownTokens;
            if (hybrid.EmbeddingUsageUnknown && unknownEmbedding == 0)
            {
                unknownEmbedding = budget.EmbeddingUpper;
            }

            var embeddingRecorded = hybrid.EmbeddingAttempted
                ? await journal.RecordUsageAsync(AiQuotaTurnStage.Embedding, hybrid.EmbeddingPromptTokens, unknownEmbedding, ct)
                : await journal.ClearUnknownAsync(AiQuotaTurnStage.Embedding, ct);
            if (!embeddingRecorded)
            {
                return Lost(input);
            }
        }

        if (hybrid.Mode == HybridRetrievalModes.Unavailable
            && hybrid.Evidence.Count == 0
            && !hybrid.EmbeddingAttempted
            && !hybrid.RecheckRejected)
        {
            return new GroundedTurnExecution(deterministic);
        }

        var sentences = Fit(hybrid.Evidence.Select(ToSentence).ToList(), budget.EvidenceSlack);
        var selectorStatus = GroundedSelectorStatus.NotRequired;
        string? modelJson = null;
        var recheckRejected = hybrid.RecheckRejected;
        if (sentences.Count > 1 && GroundedAnswerComposer.CountCovering(retrievalQuery, sentences) > 1)
        {
            if (!await SourcesCurrentAsync(input.UserId, sentences, ct))
            {
                sentences = [];
                recheckRejected = true;
            }
            else if (!TryFitSelection(input, retrievalQuery, budget, ref sentences, out var selectionRequest))
            {
                selectorStatus = GroundedSelectorStatus.Invalid;
            }
            else if (GroundedAnswerComposer.CountCovering(retrievalQuery, sentences) <= 1)
            {
                selectorStatus = GroundedSelectorStatus.NotRequired;
            }
            else if (journal is not null && !await journal.OwnsAsync(ct))
            {
                return Lost(input);
            }
            else
            {
                var attempts = Math.Max(1, budget.GenerationAttempts);
                var measured = _selector.MeasureSelection(selectionRequest);
                var generationBound = measured <= 0
                    ? budget.UnknownGenerationUpper
                    : (int)Math.Min(int.MaxValue, (long)attempts * (measured + budget.GenerationUpper));
                if (budget.UnknownGenerationUpper > 0 && generationBound > budget.UnknownGenerationUpper)
                {
                    selectorStatus = GroundedSelectorStatus.Invalid;
                }
                else if (journal is not null && !await journal.BeginAsync(AiQuotaTurnStage.Generating, generationBound, ct))
                {
                    return Lost(input);
                }
                else
                {
                    try
                    {
                        selectionRequest = selectionRequest with
                        {
                            ConfirmCurrent = async token => (journal is null || await journal.OwnsAsync(token))
                                && await SourcesCurrentAsync(input.UserId, sentences, token),
                            RecordUsage = journal is null ? null : (known, unknown, token) => journal.RecordUsageAsync(AiQuotaTurnStage.Generating, known, unknown, token)
                        };
                        var selection = await _selector.SelectAsync(selectionRequest, ct);
                        var stillOwns = journal is null || await journal.OwnsAsync(ct);
                        var stillCurrent = await SourcesCurrentAsync(input.UserId, sentences, ct);
                        if (journal is not null)
                        {
                            var known = selection.UsageKnown ? selection.TotalTokens : 0;
                            var unknown = selection.UnknownTokens > 0
                                ? selection.UnknownTokens
                                : selection.UsageKnown ? 0 : generationBound;
                            if (!await journal.RecordUsageAsync(AiQuotaTurnStage.Generating, known, unknown, ct) || !stillOwns)
                            {
                                return Lost(input);
                            }
                        }

                        if (!stillCurrent)
                        {
                            sentences = [];
                            recheckRejected = true;
                        }
                        else if (!selection.OutputAccepted || string.IsNullOrWhiteSpace(selection.Json))
                        {
                            selectorStatus = GroundedSelectorStatus.Invalid;
                        }
                        else
                        {
                            modelJson = selection.Json;
                            selectorStatus = GroundedSelectorStatus.Completed;
                        }
                    }
                    catch (AiProviderException ex) when ((ex.ErrorCode is AiProviderException.ErrorCodeBudgetExhausted
                        or AiProviderException.ErrorCodeInvalidConfiguration
                        or AiProviderException.ErrorCodeCorpusStale
                        or AiProviderException.ErrorCodeCircuitOpen)
                        && !ex.HasRecordedUsage && ex.RecordedUnknownTokens == 0)
                    {
                        if (journal is not null && !await journal.ClearUnknownAsync(AiQuotaTurnStage.Generating, ct))
                        {
                            return Lost(input);
                        }

                        selectorStatus = ex.ErrorCode is AiProviderException.ErrorCodeInvalidConfiguration
                            or AiProviderException.ErrorCodeCircuitOpen
                            ? GroundedSelectorStatus.Unavailable
                            : GroundedSelectorStatus.Invalid;
                    }
                    catch (AiProviderException)
                    {
                        if (journal is not null && !await journal.RecordUnknownAsync(AiQuotaTurnStage.Generating, generationBound, ct))
                        {
                            return Lost(input);
                        }

                        selectorStatus = GroundedSelectorStatus.Unavailable;
                    }
                }
            }
        }

        if (hybrid.EmbeddingAttempted && hybrid.EmbeddingUsageUnknown && sentences.Count == 0 && !recheckRejected)
        {
            selectorStatus = GroundedSelectorStatus.Unavailable;
        }

        ct.ThrowIfCancellationRequested();
        if (journal is not null && !await journal.OwnsAsync(ct)) return Lost(input);
        if (sentences.Count > 0 && !await SourcesCurrentAsync(input.UserId, sentences, ct))
        {
            sentences = [];
            recheckRejected = true;
        }

        var composition = GroundedAnswerComposer.Compose(new GroundedComposeRequest(
            input.Message,
            input.History,
            sentences,
            modelJson,
            selectorStatus,
            recheckRejected));
        return new GroundedTurnExecution(ToAnswer(composition, hybrid.Mode));
    }

    private GroundedTurnExecution Lost(GroundedTurnInput input)
    {
        var composition = GroundedAnswerComposer.Compose(new GroundedComposeRequest(
            input.Message,
            input.History,
            [],
            null,
            GroundedSelectorStatus.NotRequired,
            true));
        return new GroundedTurnExecution(ToAnswer(composition, HybridRetrievalModes.Unavailable));
    }

    private async Task<bool> SourcesCurrentAsync(int userId, IReadOnlyList<GroundedSentence> sentences, CancellationToken ct)
    {
        var identities = sentences
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence.DocumentHash))
            .Select(sentence => new GroundedSourceIdentity(sentence.SourceKey, sentence.DocumentHash!) { SnapshotId = sentence.SnapshotId })
            .Distinct()
            .ToList();
        return identities.Count == 0 || await _hybrid.SourcesStillCurrentAsync(userId, identities, ct);
    }

    private bool TryFitSelection(
        GroundedTurnInput input,
        string retrievalQuery,
        ChatTurnBudgetDecision budget,
        ref List<GroundedSentence> sentences,
        out ChatSentenceSelectionRequest request)
    {
        var history = (input.History ?? [])
            .Where(turn => string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase))
            .ToList();
        while (true)
        {
            request = new ChatSentenceSelectionRequest(
                input.UserId,
                retrievalQuery,
                history,
                sentences,
                budget.GenerationUpper)
            {
                MaxInputUpper = budget.GenerationInputUpper,
                GenerationAttempts = Math.Max(1, budget.GenerationAttempts)
            };
            var measured = _selector.MeasureSelection(request);
            if (measured <= 0)
            {
                return true;
            }

            if (budget.GenerationInputUpper > 0 && measured <= budget.GenerationInputUpper)
            {
                return true;
            }

            if (history.Count > 0)
            {
                history.RemoveAt(0);
                continue;
            }

            if (sentences.Count > 1)
            {
                sentences.RemoveAt(sentences.Count - 1);
                continue;
            }

            return false;
        }
    }

    private AiChatAnswer ToAnswer(GroundedComposition composition, string retrievalMode)
    {
        var prompt = _tokens.CountTokens(composition.Text);
        var evidence = composition.Sentences.Select(sentence => new HybridRagEvidence(
            sentence.SourceKey,
            sentence.SourceType,
            sentence.SentenceId,
            string.IsNullOrWhiteSpace(sentence.SentenceHash)
                ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sentence.Text))).ToLowerInvariant()
                : sentence.SentenceHash,
            sentence.CorpusVersion,
            sentence.ChunkerVersion,
            null,
            sentence.Route,
            string.IsNullOrWhiteSpace(sentence.Visibility) ? "public" : sentence.Visibility,
            null,
            DateTime.UtcNow,
            sentence.Text)
        {
            DocumentContentHash = sentence.DocumentHash,
            SnapshotId = sentence.SnapshotId,
            SourceVersion = sentence.SourceVersion,
            EntityKey = sentence.EntityKey
        }).ToList();
        return new AiChatAnswer(
            composition.Outcome,
            composition.Scope,
            composition.Text,
            evidence,
            prompt,
            _tokens.CountTokens(composition.Text),
            prompt + _tokens.CountTokens(composition.Text),
            AiChatAnswerService.ModelName,
            Guid.NewGuid().ToString("N"))
        {
            RetrievalMode = retrievalMode is "live" or "hybrid" or "lexical_degraded" or "unavailable"
                ? retrievalMode
                : "unspecified"
        };
    }

    private static List<GroundedSentence> Fit(List<GroundedSentence> sentences, int slack)
    {
        while (sentences.Count > 0 && EmbeddingTokenBound.Sum(sentences.Select(sentence => sentence.Text)) > slack)
        {
            sentences.RemoveAt(sentences.Count - 1);
        }

        return sentences;
    }

    private static GroundedSentence ToSentence(HybridEvidenceItem item)
        => new(
            string.IsNullOrWhiteSpace(item.SentenceId) ? item.ChunkId : item.SentenceId,
            item.SourceKey,
            item.SourceKind,
            item.Text,
            item.Route,
            item.EntityKey ?? item.SourceKey)
        {
            SentenceHash = item.SentenceHash,
            DocumentHash = string.IsNullOrWhiteSpace(item.SentenceHash) ? null : item.ContentHash,
            Visibility = item.Visibility,
            CorpusVersion = item.CorpusVersion,
            ChunkerVersion = item.ChunkerVersion,
            SourceVersion = item.SourceVersion,
            SnapshotId = item.SnapshotId
        };
}
