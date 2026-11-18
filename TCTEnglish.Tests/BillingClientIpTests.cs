using System.Net;
using TCTEnglish.Services.Billing;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>The address sent to VNPay (plan 4.7, F19).</summary>
    public class BillingClientIpTests
    {
        private static IPAddress Ip(string text) => IPAddress.Parse(text);

        [Theory]
        // Direct connections: the connection address wins and a forged X-Forwarded-For is ignored.
        [InlineData("203.0.113.9", "198.51.100.7", "203.0.113.9")]
        [InlineData("203.0.113.9", null, "203.0.113.9")]
        [InlineData("2001:db8::7", "1.2.3.4", "2001:db8::7")]
        // Behind a proxy on the same host or network: the first valid forwarded address is believed.
        [InlineData("127.0.0.1", "198.51.100.7", "198.51.100.7")]
        [InlineData("::1", "198.51.100.7, 10.0.0.2", "198.51.100.7")]
        [InlineData("10.1.2.3", "198.51.100.7", "198.51.100.7")]
        [InlineData("192.168.0.5", " 198.51.100.7 ,10.0.0.2", "198.51.100.7")]
        [InlineData("172.20.0.1", "198.51.100.7", "198.51.100.7")]
        [InlineData("127.0.0.1", "2001:db8::9", "2001:db8::9")]
        // A forwarded value that is not an IP address is never passed on.
        [InlineData("127.0.0.1", "not-an-ip", "127.0.0.1")]
        [InlineData("127.0.0.1", "<script>", "127.0.0.1")]
        [InlineData("127.0.0.1", "", "127.0.0.1")]
        [InlineData("10.0.0.9", "unknown", "10.0.0.9")]
        // Loopback and IPv4-mapped addresses are written as IPv4 text.
        [InlineData("::1", null, "127.0.0.1")]
        [InlineData("::ffff:203.0.113.9", null, "203.0.113.9")]
        [InlineData("::ffff:127.0.0.1", null, "127.0.0.1")]
        [InlineData("127.0.0.1", "::1", "127.0.0.1")]
        [InlineData("127.0.0.1", "::ffff:198.51.100.7", "198.51.100.7")]
        public void Resolve_PicksTheRightAddress(string remote, string? forwardedFor, string expected)
        {
            Assert.Equal(expected, BillingClientIp.Resolve(Ip(remote), forwardedFor));
        }

        [Fact]
        public void NoConnectionAddress_FallsBackToLoopback_OrTheForwardedAddress()
        {
            Assert.Equal("127.0.0.1", BillingClientIp.Resolve(null, null));
            Assert.Equal("127.0.0.1", BillingClientIp.Resolve(null, "garbage"));
            Assert.Equal("198.51.100.7", BillingClientIp.Resolve(null, "198.51.100.7"));
        }
    }
}
