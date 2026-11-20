using System;
using System.Collections.Generic;
using System.Linq;
using TCTEnglish.ViewModels.Billing;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Builds the /Premium page model (plan 5.1): money text, price per month, saving against the cheapest monthly
    /// plan, the preselected plan (the longest) and method (the first one that is available), and the status card.
    /// The same arithmetic lives in <c>billing-core.js</c>; the server renders it so the page works without JavaScript.
    /// </summary>
    public static class PricingPageBuilder
    {
        public static PricingViewModel Build(
            IReadOnlyCollection<PricingPlanViewModel> plans,
            PremiumAccessSnapshot access,
            IReadOnlyDictionary<string, bool> providerReadiness,
            bool moMoMockMode,
            string? errorMessage,
            DateTime nowUtc,
            bool payOsMockMode = false)
        {
            var list = plans.Select(Clone).ToList();

            var monthly = list.Where(p => p.DurationDays > 0 && p.DurationDays < 60 && p.PriceVnd > 0)
                .OrderBy(p => p.PriceVnd)
                .FirstOrDefault();

            foreach (var plan in list)
            {
                plan.PriceText = BillingTime.FormatMoney(plan.PriceVnd);
                var months = Math.Max(1, (int)Math.Round(plan.DurationDays / 30.0, MidpointRounding.AwayFromZero));
                if (plan.DurationDays >= 365 && plan.PriceVnd > 0)
                {
                    var perMonth = Math.Round(plan.PriceVnd / months, MidpointRounding.AwayFromZero);
                    plan.PerMonthText = $"khoảng {BillingTime.FormatMoney(perMonth)}/tháng";
                }

                if (monthly != null && months > 1 && plan.PriceVnd > 0)
                {
                    var saving = (int)Math.Round((1m - plan.PriceVnd / (monthly.PriceVnd * months)) * 100m, MidpointRounding.AwayFromZero);
                    plan.SavingPercent = Math.Max(0, saving);
                }
            }

            // The longest plan is the one most buyers should see first; ties keep the display order.
            var preselected = list.OrderByDescending(p => p.DurationDays).FirstOrDefault();
            if (preselected != null)
            {
                preselected.IsPreselected = true;
            }

            bool Ready(string code) => providerReadiness.TryGetValue(code, out var ok) && ok;
            var providers = new List<PricingProviderViewModel>
            {
                new()
                {
                    Code = PaymentProviders.PayOS,
                    Name = "Chuyển khoản QR (payOS)",
                    Badge = "payOS",
                    Description = "Quét mã bằng app ngân hàng bất kỳ",
                    Available = Ready(PaymentProviders.PayOS),
                    IsTest = payOsMockMode && Ready(PaymentProviders.PayOS)
                },
                new()
                {
                    Code = PaymentProviders.VNPay,
                    Name = "VNPay",
                    Badge = "VNPAY",
                    Description = "Thẻ ATM, QR ngân hàng, thẻ quốc tế",
                    Available = Ready(PaymentProviders.VNPay)
                },
                new()
                {
                    Code = PaymentProviders.MoMo,
                    Name = "Ví MoMo",
                    Badge = "MoMo",
                    Description = "Quét mã QR bằng ứng dụng MoMo",
                    Available = Ready(PaymentProviders.MoMo),
                    IsTest = moMoMockMode && Ready(PaymentProviders.MoMo)
                }
            };
            var first = providers.FirstOrDefault(p => p.Available);
            if (first != null)
            {
                first.IsPreselected = true;
            }

            DateTime? currentEnd = access.PremiumEndsAtUtc.HasValue && access.PremiumEndsAtUtc.Value > nowUtc
                ? access.PremiumEndsAtUtc
                : null;

            return new PricingViewModel
            {
                Plans = list,
                CurrentAccess = access,
                ErrorMessage = errorMessage,
                ProviderReadiness = providerReadiness.ToDictionary(kv => kv.Key, kv => kv.Value),
                Providers = providers,
                Status = PremiumStatusViewModel.From(access, compact: false),
                NowMs = new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
                CurrentEndMs = currentEnd.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(currentEnd.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
                    : null
            };
        }

        private static PricingPlanViewModel Clone(PricingPlanViewModel p) => new()
        {
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
            PriceVnd = p.PriceVnd,
            DurationDays = p.DurationDays
        };

        /// <summary>"365 ngày, đến 17/11/2027": the buyer's new end date; purchases stack on a running subscription.</summary>
        public static string ValidityText(PricingViewModel model, PricingPlanViewModel plan)
        {
            var startMs = model.CurrentEndMs.HasValue && model.CurrentEndMs.Value > model.NowMs
                ? model.CurrentEndMs.Value
                : model.NowMs;
            var start = DateTimeOffset.FromUnixTimeMilliseconds(startMs).UtcDateTime;
            var end = start.AddDays(plan.DurationDays);
            return $"{plan.DurationDays} ngày, đến {BillingTime.ToDisplayDate(end)}";
        }
    }
}
