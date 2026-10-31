using System.Security.Cryptography;
using System.Text;

namespace TCTEnglish.Services.Speaking;

public static class SpeakingImportFingerprints
{
    /// <summary>
    /// Stable owner+video key. Do not change this algorithm for existing caption rows.
    /// Subtitle imports share it so they cannot bypass the unique video slot.
    /// </summary>
    public static string ForOwnerVideo(int userId, string youtubeId)
    {
        var raw = $"{userId}:{youtubeId}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    public static string ContentHash(IEnumerable<string> lines)
    {
        var normalized = string.Join(
            '\n',
            lines.Select(line => (line ?? string.Empty).Trim()).Where(line => line.Length > 0));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}
