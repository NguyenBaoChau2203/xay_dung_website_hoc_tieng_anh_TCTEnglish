// Adapted from OpenAI Agents JS, MIT, revision 1215a24fb4725742c7b88094812b7ba18c46041c.
// See third_party/openai-realtime-server-controlled/NOTICE.md.
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TCTEnglish.Services.AI.Voice;

public static partial class VoiceSdpPolicy
{
    public const int MaxBytes = 256 * 1024;
    public static void Validate(string? sdp)
    {
        if (sdp == null || Encoding.UTF8.GetByteCount(sdp) > MaxBytes || sdp.Contains('\0'))
            throw new VoiceException("invalid_sdp");
        var lines = sdp.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || lines[0].Trim() != "v=0") throw new VoiceException("invalid_sdp");
        var media = lines.Where(line => MediaStart().IsMatch(line)).ToList();
        if (media.Count != 1) throw new VoiceException("invalid_sdp");
        var match = Media().Match(media[0]);
        if (!match.Success || !match.Groups[1].Value.Equals("audio", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535) throw new VoiceException("invalid_sdp");
    }
    [GeneratedRegex(@"^\s*m\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex MediaStart();
    [GeneratedRegex(@"^\s*m\s*=\s*([^\s]+)\s+(\d+)(?:/\d+)?\s+\S+\s+\S+", RegexOptions.IgnoreCase)]
    private static partial Regex Media();
}
