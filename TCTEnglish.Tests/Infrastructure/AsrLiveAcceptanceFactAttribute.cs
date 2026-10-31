using Xunit;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Explicit opt-in only. Discovery never performs network I/O.
/// An opted-in engine failure must fail the test, not silently become a skip.
/// </summary>
public sealed class AsrLiveAcceptanceFactAttribute : FactAttribute
{
    public AsrLiveAcceptanceFactAttribute()
    {
        if (ParseEndpoint(Environment.GetEnvironmentVariable("TCT_ASR_LIVE_BASEURL")) is null)
        {
            Skip = "Not run: set TCT_ASR_LIVE_BASEURL to an explicit loopback whisper engine.";
        }
    }

    internal static Uri? ParseEndpoint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !uri.IsLoopback
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.AbsolutePath != "/")
            throw new InvalidOperationException("ASR acceptance requires an HTTP(S) loopback base URL without credentials, path, query or fragment.");
        return uri;
    }
}
