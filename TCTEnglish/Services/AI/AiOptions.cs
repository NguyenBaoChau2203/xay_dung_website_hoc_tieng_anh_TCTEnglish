using System;

namespace TCTEnglish.Services.AI;

public class AiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public string Model { get; set; } = "gemini-2.5-flash-lite";
    public double Temperature { get; set; } = 0.4;
    public int MaxOutputTokens { get; set; } = 600;
    public int ModelContextLimit { get; set; } = 128000;
    public int MaxInputChars { get; set; } = 1000;
    public int RequestTokenBudget { get; set; } = 4600;
    public int DailyTokenBudgetPerUser { get; set; } = 60000;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int RetryMaxAttempts { get; set; } = 2;

    public int EffectiveRequestTimeoutSeconds => Math.Max(1, RequestTimeoutSeconds);

    public int EffectiveRetryMaxAttempts => Math.Max(0, RetryMaxAttempts);
}

