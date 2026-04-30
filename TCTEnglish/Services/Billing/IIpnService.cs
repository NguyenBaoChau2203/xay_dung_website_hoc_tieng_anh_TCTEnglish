using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TCTEnglish.Services.Billing
{
    public class IpnProcessingResult
    {
        public string RspCode { get; init; } = null!;
        public string Message { get; init; } = null!;

        public static IpnProcessingResult Ok()
            => new() { RspCode = "00", Message = "Confirm Success" };

        public static IpnProcessingResult InvalidSignature()
            => new() { RspCode = "97", Message = "Invalid signature" };

        public static IpnProcessingResult OrderNotFound()
            => new() { RspCode = "01", Message = "Order not found" };

        public static IpnProcessingResult AmountMismatch()
            => new() { RspCode = "04", Message = "invalid amount" };

        public static IpnProcessingResult CurrencyMismatch()
            => new() { RspCode = "04", Message = "Invalid Currency" };

        public static IpnProcessingResult AlreadyConfirmed()
            => new() { RspCode = "02", Message = "Order already confirmed" };

        public static IpnProcessingResult InvalidOrder()
            => new() { RspCode = "01", Message = "Order not found" };

        public static IpnProcessingResult UnknownError()
            => new() { RspCode = "99", Message = "Unknown error" };
    }

    public interface IIpnService
    {
        Task<IpnProcessingResult> ProcessVnPayIpnAsync(
            IDictionary<string, string> queryParams,
            CancellationToken ct = default);

        Task ProcessMoMoIpnAsync(
            IDictionary<string, string> parameters,
            CancellationToken ct = default);
    }
}
