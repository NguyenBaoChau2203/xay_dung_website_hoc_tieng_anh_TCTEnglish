namespace TCTEnglish.Services.Onboarding;

/// <summary>JSON body of <c>POST api/onboarding/preview</c> (and the shared part of complete).</summary>
public class OnboardingChoicesDto
{
    public List<string>? Goals { get; set; }
    public List<string>? Topics { get; set; }
    public string? Level { get; set; }
    public int Pace { get; set; }

    public OnboardingChoices ToChoices() => new(
        (Goals ?? new List<string>()).Select(x => (x ?? string.Empty).Trim()).ToList(),
        (Topics ?? new List<string>()).Select(x => (x ?? string.Empty).Trim()).ToList(),
        (Level ?? string.Empty).Trim(),
        Pace);
}

/// <summary>JSON body of <c>POST api/onboarding/complete</c>. The request id is a string so a bad one is a 400 with a message.</summary>
public sealed class OnboardingCompleteDto : OnboardingChoicesDto
{
    public string? RequestId { get; set; }
    public List<string>? DecksOff { get; set; }
    public List<int>? KnownCardIds { get; set; }
}

/// <summary>JSON body of <c>POST api/onboarding/refill/{setId}</c>.</summary>
public sealed class OnboardingRefillDto
{
    public string? RequestId { get; set; }
    public int Count { get; set; }
}
