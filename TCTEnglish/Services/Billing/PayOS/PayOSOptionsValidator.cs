using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Billing.PayOS
{
    /// <summary>
    /// The payOS mock exists for Development only. A Production host that switches it on does not start
    /// serving payments quietly in test mode: reading the options fails loudly instead.
    /// </summary>
    public sealed class PayOSOptionsValidator : IValidateOptions<PayOSOptions>
    {
        private readonly IWebHostEnvironment _environment;

        public PayOSOptionsValidator(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public ValidateOptionsResult Validate(string? name, PayOSOptions options)
        {
            if (options.MockMode && !_environment.IsDevelopment())
            {
                return ValidateOptionsResult.Fail(
                    "PayOS:MockMode is for the Development environment only. Set PayOS:MockMode=false.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
