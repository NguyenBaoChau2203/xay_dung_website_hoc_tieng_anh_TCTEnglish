namespace TCTEnglish.Services.AI.Internal;

internal enum CardLookupSourceKind
{
    None,
    Specified,
    Comparison
}

internal sealed record CardLookupRequest(
    IReadOnlyList<string> TargetTokens,
    CardLookupSourceKind SourceKind,
    IReadOnlyList<string> SourceLabels);
