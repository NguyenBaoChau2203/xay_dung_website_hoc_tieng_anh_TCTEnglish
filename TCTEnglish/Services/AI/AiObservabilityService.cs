using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.ViewModels.AI;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI;

public sealed class AiObservabilityService : IAiObservabilityService
{
    private readonly DbflashcardContext _context;
    private readonly AiOptions _options;
    private readonly ILogger<AiObservabilityService> _logger;

    public AiObservabilityService(
        DbflashcardContext context,
        IOptions<AiOptions> options,
        ILogger<AiObservabilityService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiObservabilitySnapshotDto> GetUserSnapshotAsync(int userId, int lookbackDays, CancellationToken ct)
    {
        var normalizedDays = Math.Clamp(lookbackDays, 1, 30);
        var utcNow = DateTime.UtcNow;
        var startDate = utcNow.Date.AddDays(-(normalizedDays - 1));

        var requestLogs = await _context.AiRequestLogs
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.RequestedAtUtc >= startDate)
            .OrderBy(x => x.RequestedAtUtc)
            .ToListAsync(ct);

        var requestCount = requestLogs.Count;
        var errorCount = requestLogs.Count(x => !x.IsSuccess);
        var errorRatePercent = requestCount == 0
            ? 0d
            : Math.Round((double)errorCount * 100d / requestCount, 2, MidpointRounding.AwayFromZero);

        var latencyValues = requestLogs
            .Where(x => x.IsSuccess && x.LatencyMs.HasValue)
            .Select(x => x.LatencyMs!.Value)
            .OrderBy(x => x)
            .ToList();

        var latencyP50 = CalculatePercentile(latencyValues, 0.5);
        var latencyP95 = CalculatePercentile(latencyValues, 0.95);

        var dailyTokenMap = requestLogs
            .Where(x => x.IsSuccess)
            .GroupBy(x => DateOnly.FromDateTime(x.RequestedAtUtc))
            .ToDictionary(
                x => x.Key,
                x => x.Sum(log => log.TotalTokens ?? ((log.PromptTokens ?? 0) + (log.CompletionTokens ?? 0))));

        var tokenUsageByDay = Enumerable.Range(0, normalizedDays)
            .Select(offset => DateOnly.FromDateTime(startDate.AddDays(offset)))
            .Select(day => new AiTokenUsageByDayDto(day, dailyTokenMap.GetValueOrDefault(day)))
            .ToList();

        var today = DateOnly.FromDateTime(utcNow);
        var tokensUsedToday = dailyTokenMap.GetValueOrDefault(today);
        var dailyTokenBudget = Math.Max(0, _options.DailyTokenBudgetPerUser);

        var budgetUsagePercent = dailyTokenBudget == 0
            ? 0d
            : Math.Round((double)tokensUsedToday * 100d / dailyTokenBudget, 2, MidpointRounding.AwayFromZero);

        var isBudgetWarning = dailyTokenBudget > 0 && tokensUsedToday >= (int)Math.Ceiling(dailyTokenBudget * 0.8);
        var isBudgetExceeded = dailyTokenBudget > 0 && tokensUsedToday > dailyTokenBudget;
        var isErrorSpikeDetected = requestCount >= 5 && errorRatePercent >= 30d;

        if (isBudgetWarning || isBudgetExceeded)
        {
            _logger.LogWarning(
                "AI budget warning for user {userId}. TokensUsedToday {tokensUsedToday}. DailyBudget {dailyBudget}. BudgetUsagePercent {budgetUsagePercent}",
                userId,
                tokensUsedToday,
                dailyTokenBudget,
                budgetUsagePercent);
        }

        if (isErrorSpikeDetected)
        {
            _logger.LogWarning(
                "AI error spike detected for user {userId}. RequestCount {requestCount}. ErrorCount {errorCount}. ErrorRatePercent {errorRatePercent}",
                userId,
                requestCount,
                errorCount,
                errorRatePercent);
        }

        return new AiObservabilitySnapshotDto(
            requestCount,
            errorCount,
            errorRatePercent,
            latencyP50,
            latencyP95,
            tokensUsedToday,
            dailyTokenBudget,
            budgetUsagePercent,
            isBudgetWarning,
            isBudgetExceeded,
            isErrorSpikeDetected,
            tokenUsageByDay);
    }

    private static int? CalculatePercentile(IReadOnlyList<int> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return null;
        }

        var rank = (int)Math.Ceiling(percentile * sortedValues.Count);
        var index = Math.Clamp(rank - 1, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }
}


