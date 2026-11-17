using System;
using System.Net;
using System.Net.Sockets;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// The buyer address sent to VNPay as <c>vnp_IpAddr</c>. The connection address is preferred. A
    /// <c>X-Forwarded-For</c> entry is only believed when the connection comes from a loopback or private
    /// address (a reverse proxy on the same host or network), and only when it parses as an IP address.
    /// IPv6 loopback and IPv4-mapped addresses are written as IPv4 text.
    /// </summary>
    public static class BillingClientIp
    {
        public const string Fallback = "127.0.0.1";

        public static string Resolve(IPAddress? remote, string? forwardedFor)
        {
            var connection = Normalize(remote);

            if (remote == null || IsProxyAddress(remote))
            {
                var first = (forwardedFor ?? string.Empty).Split(',')[0].Trim();
                if (IPAddress.TryParse(first, out var forwarded))
                {
                    return Normalize(forwarded) ?? Fallback;
                }
            }

            return connection ?? Fallback;
        }

        private static string? Normalize(IPAddress? address)
        {
            if (address == null)
            {
                return null;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (IPAddress.IsLoopback(address))
            {
                return Fallback;
            }

            return address.ToString();
        }

        private static bool IsProxyAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();
                return b[0] == 10
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                    || (b[0] == 192 && b[1] == 168);
            }

            return address.AddressFamily == AddressFamily.InterNetworkV6
                && (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || (address.GetAddressBytes()[0] & 0xFE) == 0xFC);
        }
    }
}
