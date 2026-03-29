namespace TCTEnglish.Services.AI;

public class AiOptions
{
    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
    public double Temperature { get; set; } = 0.4;
    public int MaxOutputTokens { get; set; } = 600;
    public int ModelContextLimit { get; set; } = 128000;
    public int MaxInputChars { get; set; } = 1000;
    public int RequestTokenBudget { get; set; } = 4600;
    public int DailyTokenBudgetPerUser { get; set; } = 60000;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int RetryMaxAttempts { get; set; } = 2;
}

