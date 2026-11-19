namespace TCTEnglish.Services.Onboarding;

public enum OnboardingOutcome
{
    Success,
    /// <summary>The input is not acceptable (400).</summary>
    Invalid,
    /// <summary>Not found, or not owned by the user (404).</summary>
    NotFound,
    /// <summary>The same request id was used with a different payload (409).</summary>
    Conflict
}

public sealed class OnboardingResult<T>
{
    private OnboardingResult(OnboardingOutcome outcome, T? value, string? error)
    {
        Outcome = outcome;
        Value = value;
        Error = error;
    }

    public OnboardingOutcome Outcome { get; }
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess => Outcome == OnboardingOutcome.Success;

    public static OnboardingResult<T> Ok(T value) => new(OnboardingOutcome.Success, value, null);
    public static OnboardingResult<T> Invalid(string error) => new(OnboardingOutcome.Invalid, default, error);
    public static OnboardingResult<T> NotFound() => new(OnboardingOutcome.NotFound, default, null);
    public static OnboardingResult<T> Conflict(string error) => new(OnboardingOutcome.Conflict, default, error);
}

/// <summary>What the host page needs to decide whether to open the pop-up.</summary>
public sealed record OnboardingState(bool ShouldPrompt, string Status, bool HasDecks, bool BannerVisible);

/// <summary>The learner's choices from steps 1–3.</summary>
public sealed record OnboardingChoices(IReadOnlyList<string> Goals, IReadOnlyList<string> Topics, string Level, int Pace);

public sealed record PreviewCard(int CardId, string Term, string? Meaning, string? ImageUrl, string? Cefr);

public sealed record PreviewDeck(string Key, string Name, string Goal, IReadOnlyList<string> Topics, IReadOnlyList<PreviewCard> Cards);

public sealed record OnboardingPreview(IReadOnlyList<PreviewDeck> Decks, int Total, int Days);

/// <summary>Step 4 submitted: the choices, the decks switched off and the cards marked "Đã biết".</summary>
public sealed record CompleteRequest(Guid RequestId, OnboardingChoices Choices, IReadOnlyList<string> DecksOff, IReadOnlyList<int> KnownCardIds);

public sealed record CreatedDeck(int SetId, string Name, int Count);

public sealed record CompleteResult(int FolderId, IReadOnlyList<CreatedDeck> Decks, int Pace);

public sealed record RefillResult(int Added, bool Exhausted);

/// <summary>The decks an onboarding created, as stored in <c>UserOnboarding.DecksJson</c>.</summary>
public sealed record OnboardingDeckRef(int SetId, string Goal, IReadOnlyList<string> TopicKeys);

/// <summary>One topic tile of step 2: the system set, its band, the goals it fits and the cover photo (a safe URL, or null).</summary>
public sealed record CatalogTopicView(string Key, string Name, string Band, IReadOnlyList<string> Goals, string? CoverUrl);

/// <summary>Everything the pop-up needs besides the user's own state.</summary>
public sealed record OnboardingCatalogView(IReadOnlyList<OnboardingGoal> Goals, IReadOnlyList<CatalogTopicView> Topics, IReadOnlyList<string> WelcomeCovers);

/// <summary>What the "Gợi ý thêm cùng chủ đề" card of a starter deck shows: the topics, the next cards (up to 4) and how many are left (up to 20).</summary>
public sealed record RefillSuggestion(IReadOnlyList<string> TopicNames, IReadOnlyList<PreviewCard> Preview, int Available);

public interface IOnboardingService
{
    /// <summary>Null unless the set is one of the user's own onboarding decks.</summary>
    Task<RefillSuggestion?> GetRefillSuggestionAsync(int userId, int setId, CancellationToken ct = default);

    /// <summary>The goals, the 48 topics with their cover photos (one grouped query) and the welcome thumbnails.</summary>
    Task<OnboardingCatalogView> GetCatalogAsync(CancellationToken ct = default);

    /// <summary>Creates the user's row on first call (Pending if eligible, NotEligible otherwise) and says whether to prompt now.</summary>
    Task<OnboardingState> GetStateAsync(int userId, CancellationToken ct = default);

    /// <summary>The pop-up opened by itself: counts one prompt, at most once per 20 hours.</summary>
    Task MarkPromptedAsync(int userId, CancellationToken ct = default);

    /// <summary>"Để sau": the second one ends the automatic prompts. Returns the status.</summary>
    Task<string> SkipAsync(int userId, CancellationToken ct = default);

    Task<OnboardingResult<OnboardingPreview>> BuildPreviewAsync(int userId, OnboardingChoices choices, CancellationToken ct = default);

    Task<OnboardingResult<CompleteResult>> CompleteAsync(int userId, CompleteRequest request, CancellationToken ct = default);

    /// <summary>Adds the next cards of the deck's topics to an onboarding deck of this user.</summary>
    Task<OnboardingResult<RefillResult>> RefillAsync(int userId, int setId, int count, Guid requestId, CancellationToken ct = default);

    Task DismissBannerAsync(int userId, CancellationToken ct = default);
}
