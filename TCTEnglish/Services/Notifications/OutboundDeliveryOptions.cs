using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;

namespace TCTEnglish.Services.Notifications;

public sealed class OutboundDeliveryOptions
{
    public const string SectionName = "Notifications:Outbound";
    public string? VapidPublicKey { get; set; }
    public string? VapidPrivateKey { get; set; }
    public string VapidSubject { get; set; } = string.Empty;
    public string? PublicBaseUrl { get; set; }

    public bool PublicSiteConfigured => Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback));

    public string? AccountSettingsUrl => PublicSiteConfigured
        ? new Uri(new Uri(PublicBaseUrl!.TrimEnd('/') + "/"), "Account/Settings").AbsoluteUri : null;

    public bool PushConfigured
    {
        get
        {
            if (string.IsNullOrWhiteSpace(VapidPublicKey) || string.IsNullOrWhiteSpace(VapidPrivateKey)
                || VapidPublicKey.Length != 87 || VapidPrivateKey.Length != 43
                || !Uri.TryCreate(VapidSubject, UriKind.Absolute, out var contact)
                || (contact.Scheme != "mailto" && contact.Scheme != "https")) return false;
            if (contact.Scheme == "mailto" && (!System.Net.Mail.MailAddress.TryCreate(VapidSubject["mailto:".Length..], out var address)
                || !address.Host.Contains('.') || address.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))) return false;
            if (contact.Scheme == "https" && (contact.IsLoopback || !string.IsNullOrEmpty(contact.UserInfo))) return false;
            try
            {
                static byte[] Decode(string text) => Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/') + new string('=', (4 - text.Length % 4) % 4));
                var publicKey = Decode(VapidPublicKey);
                var privateKey = Decode(VapidPrivateKey);
                if (publicKey.Length != 65 || publicKey[0] != 4 || privateKey.Length != 32) return false;
                using var key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = privateKey });
                var derived = key.ExportParameters(false).Q;
                return CryptographicOperations.FixedTimeEquals(publicKey.AsSpan(1, 32), derived.X)
                    && CryptographicOperations.FixedTimeEquals(publicKey.AsSpan(33, 32), derived.Y);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or CryptographicException) { return false; }
        }
    }
}

public static class ReminderTimeZone
{
    private static readonly string[] Allowed = ["Asia/Ho_Chi_Minh", "SE Asia Standard Time", "UTC"];

    public static bool TryResolve(string? id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id) || !Allowed.Contains(id, StringComparer.Ordinal))
        {
            return false;
        }

        foreach (var candidate in Candidates(id))
        {
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(candidate);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return false;
    }

    private static IEnumerable<string> Candidates(string id)
    {
        yield return id;
        if (id == "Asia/Ho_Chi_Minh")
        {
            yield return "SE Asia Standard Time";
        }

        if (id == "SE Asia Standard Time")
        {
            yield return "Asia/Ho_Chi_Minh";
        }
    }
}

public static class WebPushEndpointGuard
{
    private static readonly string[] AllowedHosts =
    [
        "fcm.googleapis.com",
        "updates.push.services.mozilla.com",
        "web.push.apple.com",
        "wns2-pn1.azure.com",
        "notify.windows.com"
    ];

    public static bool TryNormalize(string? endpoint, out Uri uri, out string? error)
    {
        uri = null!;
        error = null;
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > 2000)
        {
            error = "Endpoint push không hợp lệ.";
            return false;
        }

        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(parsed.UserInfo)
            || !string.IsNullOrEmpty(parsed.Fragment)
            || parsed.Port is not (-1 or 443))
        {
            error = "Endpoint push phải là HTTPS công khai, cổng 443.";
            return false;
        }

        if (!HostAllowed(parsed.Host))
        {
            error = "Máy chủ push không nằm trong danh sách cho phép.";
            return false;
        }

        uri = parsed;
        return true;
    }

    // Resolve once and connect to the checked IP, preventing a second DNS lookup/rebinding.
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext connection, CancellationToken cancellationToken)
    {
        if (connection.DnsEndPoint.Port != 443 || !HostAllowed(connection.DnsEndPoint.Host))
            throw new HttpRequestException("Push destination is not allowed.");
        var addresses = await Dns.GetHostAddressesAsync(connection.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(IsPrivateOrLocal))
            throw new HttpRequestException("Push destination is not public.");
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, connection.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException) { socket.Dispose(); }
            catch { socket.Dispose(); throw; }
        }
        throw new HttpRequestException("Push destination is unavailable.");
    }

    public static string Hash(string endpoint)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint))).ToLowerInvariant();

    private static bool HostAllowed(string host)
    {
        if (AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".wns.windows.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivateOrLocal(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) return IsPrivateOrLocal(address.MapToIPv4());
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6UniqueLocal();
        }

        var bytes = address.GetAddressBytes();
        if (bytes[0] >= 224) return true;
        return bytes[0] switch
        {
            0 or 10 or 127 => true,
            169 when bytes[1] == 254 => true,
            172 when bytes[1] is >= 16 and <= 31 => true,
            192 when bytes[1] == 168 => true,
            100 when bytes[1] is >= 64 and <= 127 => true,
            _ => false
        };
    }

    private static bool IsIPv6UniqueLocal(this IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return (bytes[0] & 0xFE) == 0xFC;
    }
}
