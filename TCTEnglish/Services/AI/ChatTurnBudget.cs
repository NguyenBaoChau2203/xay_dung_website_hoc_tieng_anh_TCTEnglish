using TCTEnglish.Services.AI.Rag;

namespace TCTEnglish.Services.AI;

public readonly record struct ChatTurnBudgetDecision(
    int ReservedTokens,
    int EmbeddingUpper,
    int EvidenceSlack,
    int GenerationUpper,
    bool AllowEmbedding,
    int GenerationInputUpper = 0,
    int UnknownGenerationUpper = 0,
    int GenerationAttempts = 1,
    int EmbeddingAttempts = 1);

/// <summary>
/// One conservative ceiling computed before any paid retrieval or generation call.
/// Evidence added later has to fit in the slack from this same decision.
/// </summary>
public static class ChatTurnBudget
{
    public const int MaxEvidenceTokens = 800;

    public static bool TryCreate(
        string query,
        int plannedContextAndOutput,
        int maxOutputTokens,
        int requestTokenBudget,
        out ChatTurnBudgetDecision decision,
        int generationAttempts = 1,
        int embeddingAttempts = 1,
        string? model = null,
        string? reasoningEffort = null)
    {
        decision = default;
        if (string.IsNullOrWhiteSpace(query) || requestTokenBudget <= 0 || plannedContextAndOutput < 0)
        {
            return false;
        }

        if (query.Length > new EmbeddingOptions().MaxInputCharacters)
        {
            return false;
        }

        var textAttempts = Math.Max(1, generationAttempts);
        var vectorAttempts = Math.Max(1, embeddingAttempts);
        var output = Math.Max(1, maxOutputTokens);
        var perAttemptEmbedding = EmbeddingTokenBound.Upper(query);
        var embedding = (int)Math.Min(int.MaxValue, (long)perAttemptEmbedding * vectorAttempts);
        var planned = plannedContextAndOutput;
        var room = (long)requestTokenBudget - planned - embedding;
        if (room < 0)
        {
            return false;
        }

        var evidence = (int)Math.Min(MaxEvidenceTokens, room);
        var configuredOutput = output;
        var envelope = OpenAiChatSentenceSelector.FixedEnvelopeUpper(query, configuredOutput, model, reasoningEffort);
        while (textAttempts > 1 && room / textAttempts < envelope + 1)
        {
            textAttempts--;
        }

        var perAttempt = (int)(room / textAttempts);
        int inputUpper;
        int generationCharge;
        if (perAttempt <= envelope)
        {
            // The fixed selector payload cannot fit. Admit retrieval, but do not send generation.
            output = Math.Min(configuredOutput, Math.Max(1, perAttempt));
            inputUpper = 0;
            generationCharge = textAttempts * output;
        }
        else
        {
            output = (int)Math.Min(configuredOutput, perAttempt - envelope);
            envelope = OpenAiChatSentenceSelector.FixedEnvelopeUpper(query, output, model, reasoningEffort);
            inputUpper = Math.Max(0, perAttempt - output);
            if (inputUpper < envelope)
            {
                inputUpper = 0;
            }

            generationCharge = inputUpper == 0
                ? textAttempts * output
                : textAttempts * (inputUpper + output);
        }

        var generationReserve = Math.Min(room, Math.Max(evidence, generationCharge));
        var reserved = planned + embedding + (int)generationReserve;
        if (reserved > requestTokenBudget)
        {
            return false;
        }

        decision = new ChatTurnBudgetDecision(
            reserved,
            embedding,
            evidence,
            output,
            true,
            inputUpper,
            generationCharge,
            textAttempts,
            vectorAttempts);
        return true;
    }
}
