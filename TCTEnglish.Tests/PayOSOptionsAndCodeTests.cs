using System;
using System.Linq;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Billing.PayOS;
using TCTEnglish.Tests.TestHelpers;
using Xunit;

namespace TCTEnglish.Tests
{
    public class PayOSOptionsAndCodeTests
    {
        private static PayOSOptions Configured() => new()
        {
            Enabled = true,
            ClientId = "client-id-value",
            ApiKey = "api-key-value",
            ChecksumKey = "checksum-key-value",
            PublicBaseUrl = "https://tct.example.com"
        };

        [Fact]
        public void FullyConfigured_NeedsEnabledThreeKeysAndAPublicUrl()
        {
            Assert.True(Configured().IsFullyConfigured(isDevelopment: false));
            Assert.False(new PayOSOptions().IsFullyConfigured(isDevelopment: false));

            foreach (var broken in new[]
                     {
                         Configured().With(o => o.Enabled = false),
                         Configured().With(o => o.ClientId = ""),
                         Configured().With(o => o.ApiKey = "  "),
                         Configured().With(o => o.ChecksumKey = "CHANGE_ME"),
                         Configured().With(o => o.PublicBaseUrl = ""),
                         Configured().With(o => o.BaseUrl = "http://api.example.com")
                     })
            {
                Assert.False(broken.IsFullyConfigured(isDevelopment: false));
            }
        }

        [Fact]
        public void PublicUrl_MayBeHttpOnlyInDevelopment()
        {
            var local = Configured().With(o => o.PublicBaseUrl = "http://localhost:5100");

            Assert.True(local.IsFullyConfigured(isDevelopment: true));
            Assert.False(local.IsFullyConfigured(isDevelopment: false));
        }

        [Fact]
        public void ConfigurationErrors_NameTheMissingSettings_ButNeverTheValues()
        {
            var errors = new PayOSOptions { Enabled = true, ApiKey = "super-secret-api-key" }
                .GetConfigurationErrors(isDevelopment: false);

            Assert.Contains("ClientId", errors);
            Assert.Contains("ChecksumKey", errors);
            Assert.DoesNotContain(errors, e => e.Contains("super-secret-api-key", StringComparison.Ordinal));
        }

        [Fact]
        public void Mock_NeedsDevelopmentEnabledAndMockMode()
        {
            var mock = new PayOSOptions { Enabled = true, MockMode = true };

            Assert.True(mock.IsMockActive(isDevelopment: true));
            Assert.False(mock.IsMockActive(isDevelopment: false));
            Assert.False(new PayOSOptions { Enabled = false, MockMode = true }.IsMockActive(true));
            Assert.False(new PayOSOptions { Enabled = true, MockMode = false }.IsMockActive(true));
        }

        [Theory]
        [InlineData("Production", true, false)]
        [InlineData("Staging", true, false)]
        [InlineData("Development", true, true)]
        [InlineData("Production", false, true)]
        public void Validator_RefusesMockModeOutsideDevelopment(string environment, bool mockMode, bool valid)
        {
            var validator = new PayOSOptionsValidator(new StubWebHostEnvironment { EnvironmentName = environment });

            var result = validator.Validate(null, new PayOSOptions { MockMode = mockMode });

            Assert.Equal(valid, result.Succeeded);
        }

        [Fact]
        public void Normalize_ClampsTheTimeout_AndFixesPaths()
        {
            var normalized = new PayOSOptions { ReturnPath = "Billing/PayOSReturn", TimeoutSeconds = 0, BaseUrl = "https://x.example/" }.Normalize();

            Assert.Equal("/Billing/PayOSReturn", normalized.ReturnPath);
            Assert.Equal(5, normalized.TimeoutSeconds);
            Assert.Equal("https://x.example", normalized.BaseUrl);
        }

        [Fact]
        public void OrderCode_IsFifteenDigits_AndFitsAnIntegerThatJavaScriptCanHold()
        {
            var codes = Enumerable.Range(0, 200).Select(_ => PayOSOrderCode.Generate(new DateTime(2026, 11, 20, 13, 5, 9, DateTimeKind.Utc))).ToList();

            Assert.All(codes, c =>
            {
                Assert.InRange(c.ToString().Length, 15, 15);
                Assert.InRange(c, 1L, 9_007_199_254_740_991L);
                Assert.StartsWith("261120130509", c.ToString(), StringComparison.Ordinal);
            });
            Assert.True(codes.Distinct().Count() > 150, "the random suffix must vary");
        }

        [Fact]
        public void Description_IsNineCharactersAtMost_AndEndsWithTheCodeTail()
        {
            var description = PayOSOrderCode.Description(261120130509123);

            Assert.Equal("TCT509123", description);
            Assert.True(description.Length <= PayOSOrderCode.MaxDescriptionLength);
            Assert.Equal("TCT42", PayOSOrderCode.Description(42));
        }

        [Theory]
        [InlineData("261120130509123", true)]
        [InlineData(" 12 ", true)]
        [InlineData("0", false)]
        [InlineData("-5", false)]
        [InlineData("12a", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void TryParse_AcceptsOnlyPositiveWholeNumbers(string? value, bool ok)
        {
            Assert.Equal(ok, PayOSOrderCode.TryParse(value, out _));
        }
    }

    internal static class PayOSOptionsTestExtensions
    {
        public static PayOSOptions With(this PayOSOptions options, Action<PayOSOptions> change)
        {
            change(options);
            return options;
        }
    }
}
