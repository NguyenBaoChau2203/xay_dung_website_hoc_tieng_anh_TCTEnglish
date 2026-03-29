using System;
using System.Collections.Generic;
using System.Linq;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI;

public sealed class AiContextBuilder : IAiContextBuilder
{
    private readonly IAiTokenCounter _tokenCounter;

    public AiContextBuilder(IAiTokenCounter tokenCounter)
    {
        _tokenCounter = tokenCounter;
    }

    public AiContextBuildResult BuildContextMessages(
        string systemPrompt,
        string currentUserMessage,
        IReadOnlyList<AiMessage> history,
        AiOptions options)
    {
        var normalizedSystemPrompt = systemPrompt?.Trim() ?? string.Empty;
        var normalizedCurrentMessage = currentUserMessage?.Trim() ?? string.Empty;

        var maxInputBudget = ResolveMaxInputBudget(options);
        var reservedOutputTokens = Math.Max(0, options.MaxOutputTokens);
        var maxInputWithoutReserve = Math.Max(0, maxInputBudget - reservedOutputTokens);

        var wasCurrentMessageTrimmed = false;

        var systemTokens = _tokenCounter.CountTokens(normalizedSystemPrompt);
        var currentTokens = _tokenCounter.CountTokens(normalizedCurrentMessage);

        if (systemTokens + currentTokens > maxInputWithoutReserve)
        {
            var maxCurrentTokens = Math.Max(0, maxInputWithoutReserve - systemTokens);
            var trimmedCurrent = TrimToTokenLimit(normalizedCurrentMessage, maxCurrentTokens);
            wasCurrentMessageTrimmed = !string.Equals(trimmedCurrent, normalizedCurrentMessage, StringComparison.Ordinal);

            normalizedCurrentMessage = trimmedCurrent;
            currentTokens = _tokenCounter.CountTokens(normalizedCurrentMessage);
        }

        if (systemTokens + currentTokens > maxInputWithoutReserve)
        {
            var maxSystemTokens = Math.Max(0, maxInputWithoutReserve - currentTokens);
            normalizedSystemPrompt = TrimToTokenLimit(normalizedSystemPrompt, maxSystemTokens);
            systemTokens = _tokenCounter.CountTokens(normalizedSystemPrompt);
        }

        var selectedHistory = new List<AiContextMessage>();
        var historyTokens = 0;

        foreach (var message in history.OrderByDescending(x => x.CreatedAtUtc))
        {
            var role = MapRole(message.Role);
            var content = message.Content ?? string.Empty;
            var messageTokens = _tokenCounter.CountTokens(content);

            if (systemTokens + currentTokens + historyTokens + messageTokens > maxInputWithoutReserve)
            {
                break;
            }

            selectedHistory.Add(new AiContextMessage(role, content));
            historyTokens += messageTokens;
        }

        selectedHistory.Reverse();

        var resultMessages = new List<AiContextMessage>(selectedHistory.Count + 2)
        {
            new("system", normalizedSystemPrompt)
        };

        resultMessages.AddRange(selectedHistory);
        resultMessages.Add(new AiContextMessage("user", normalizedCurrentMessage));

        var inputTokens = systemTokens + currentTokens + historyTokens;
        var plannedTotalTokens = inputTokens + reservedOutputTokens;

        return new AiContextBuildResult(
            resultMessages,
            inputTokens,
            reservedOutputTokens,
            plannedTotalTokens,
            wasCurrentMessageTrimmed);
    }

    private static int ResolveMaxInputBudget(AiOptions options)
    {
        var modelLimit = Math.Max(1, options.ModelContextLimit);
        var requestBudget = options.RequestTokenBudget > 0
            ? options.RequestTokenBudget
            : modelLimit;

        return Math.Min(modelLimit, requestBudget);
    }

    private string TrimToTokenLimit(string text, int maxTokens)
    {
        if (maxTokens <= 0 || string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (_tokenCounter.CountTokens(text) <= maxTokens)
        {
            return text;
        }

        var left = 0;
        var right = text.Length;
        var bestLength = 0;

        while (left <= right)
        {
            var middle = left + (right - left) / 2;
            var candidate = text[..middle];
            var candidateTokens = _tokenCounter.CountTokens(candidate);

            if (candidateTokens <= maxTokens)
            {
                bestLength = middle;
                left = middle + 1;
            }
            else
            {
                right = middle - 1;
            }
        }

        return text[..bestLength].TrimEnd();
    }

    private static string MapRole(AiMessageRole role)
    {
        return role switch
        {
            AiMessageRole.System => "system",
            AiMessageRole.User => "user",
            AiMessageRole.Assistant => "assistant",
            _ => "user"
        };
    }
}
