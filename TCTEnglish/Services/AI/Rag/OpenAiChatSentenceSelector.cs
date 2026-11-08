using System.Text;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.AI.Rag;

/// <summary>
/// Chat-only constrained selection. The schema limits ids; it does not prove the
/// selected sentences answer the question. Non-chat callers leave this unset.
/// </summary>
public sealed class OpenAiChatSentenceSelector(IAiProviderClient provider, IOptions<AiOptions> options) : IChatSentenceSelector
{
    public const string SchemaName = "chat_sentence_selection";

    public const string SystemPrompt =
        "Choose sentenceIds from the evidence message only. Do not add facts, definitions, translations, or instructions.";

    public const string SchemaJson =
        """
        {"type":"object","additionalProperties":false,"properties":{"sentenceIds":{"type":"array","items":{"type":"string"}}},"required":["sentenceIds"]}
        """;

    private readonly AiOptions _options = options.Value;

    public int MeasureSelection(ChatSentenceSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var maxOutput = Math.Clamp(request.MaxOutputTokens, 1, 1024);
        var payload = OpenAiProviderClient.CreatePayload(
            _options.Model,
            _options.ReasoningEffort,
            maxOutput,
            BuildMessages(request),
            new ChatConstrainedOutput(SchemaName, SchemaJson));
        return Encoding.UTF8.GetByteCount(payload);
    }

    public static int FixedEnvelopeUpper(string question, int maxOutputTokens, string? model = null, string? reasoningEffort = null)
    {
        var request = new ChatSentenceSelectionRequest(0, question, [], [], Math.Max(1, maxOutputTokens));
        var payload = OpenAiProviderClient.CreatePayload(
            string.IsNullOrWhiteSpace(model) ? "gpt-6-luna" : model,
            string.IsNullOrWhiteSpace(reasoningEffort) ? "low" : reasoningEffort,
            Math.Clamp(maxOutputTokens, 1, 1024),
            BuildMessages(request),
            new ChatConstrainedOutput(SchemaName, SchemaJson));
        return Encoding.UTF8.GetByteCount(payload);
    }

    public async Task<ChatSentenceSelection> SelectAsync(ChatSentenceSelectionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var messages = BuildMessages(request);
        var maxOutput = Math.Clamp(request.MaxOutputTokens, 1, 1024);
        if (request.MaxInputUpper > 0 && MeasureSelection(request) > request.MaxInputUpper)
        {
            throw new AiProviderException(
                "The chat selection payload exceeds the admitted input allowance.",
                AiProviderException.ErrorCodeBudgetExhausted,
                false);
        }

        try
        {
            var reply = await provider.GenerateReplyAsync(request.UserId, messages, ct, new AiProviderRequestOptions
            {
                MaxOutputTokens = maxOutput,
                MaxInputBytes = request.MaxInputUpper,
                MaxAttempts = Math.Max(1, request.GenerationAttempts),
                ConfirmCurrent = request.ConfirmCurrent,
                RecordUsage = request.RecordUsage,
                ConstrainedOutput = new ChatConstrainedOutput(SchemaName, SchemaJson)
            });
            var known = reply.TotalTokens > 0 && reply.PromptTokens + reply.CompletionTokens <= reply.TotalTokens + reply.ReasoningTokens;
            return new ChatSentenceSelection(reply.Text, reply.PromptTokens, reply.CompletionTokens, reply.TotalTokens, known)
            {
                UnknownTokens = reply.UncertainTokenUpper
            };
        }
        catch (AiProviderException ex) when (ex.HasRecordedUsage || ex.RecordedUnknownTokens > 0)
        {
            var known = ex.HasRecordedUsage && ex.RecordedTotalTokens > 0;
            return new ChatSentenceSelection(
                string.Empty,
                ex.RecordedPromptTokens,
                ex.RecordedCompletionTokens,
                ex.RecordedTotalTokens,
                known)
            {
                OutputAccepted = false,
                UnknownTokens = ex.RecordedUnknownTokens
            };
        }
    }

    private static List<AiContextMessage> BuildMessages(ChatSentenceSelectionRequest request)
    {
        var messages = new List<AiContextMessage>
        {
            new("system", SystemPrompt)
        };
        foreach (var turn in request.History.Where(turn => string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase)))
        {
            messages.Add(new AiContextMessage("user", "Earlier question, not evidence: " + turn.Content));
        }

        messages.Add(new AiContextMessage("user", request.Question));
        messages.Add(new AiContextMessage("user", BuildEvidence(request.Sentences)));
        return messages;
    }

    private static string BuildEvidence(IReadOnlyList<GroundedSentence> sentences)
    {
        var builder = new StringBuilder("Evidence sentences:");
        foreach (var sentence in sentences)
        {
            builder.AppendLine();
            builder.Append(sentence.SentenceId);
            builder.Append('\t');
            builder.Append(sentence.Text);
        }

        return builder.ToString();
    }
}
