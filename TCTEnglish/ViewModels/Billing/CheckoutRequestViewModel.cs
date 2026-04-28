using System.ComponentModel.DataAnnotations;

namespace TCTEnglish.ViewModels.Billing
{
    /// <summary>
    /// Client-submitted checkout request. Only plan code and provider
    /// are accepted — amount and duration are resolved server-side.
    /// </summary>
    public class CheckoutRequestViewModel
    {
        [Required]
        [MaxLength(50)]
        public string PlanCode { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string Provider { get; set; } = null!;
    }
}
