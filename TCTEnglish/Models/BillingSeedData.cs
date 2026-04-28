using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Models
{
    public static class BillingSeedData
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

            var plans = new List<PremiumPlan>
            {
                new PremiumPlan
                {
                    Code = "premium_monthly",
                    Name = "Premium 1 tháng",
                    Description = "Mở khóa toàn bộ tính năng Writing & Speaking trong 30 ngày.",
                    PriceVnd = 49000,
                    DurationDays = 30,
                    IsActive = true,
                    DisplayOrder = 1
                },
                new PremiumPlan
                {
                    Code = "premium_yearly",
                    Name = "Premium 1 năm",
                    Description = "Gói tiết kiệm nhất. Mở khóa toàn bộ tính năng trong 365 ngày.",
                    PriceVnd = 399000,
                    DurationDays = 365,
                    IsActive = true,
                    DisplayOrder = 2
                }
            };

            var now = DateTime.UtcNow;

            foreach (var plan in plans)
            {
                var existingPlan = await context.PremiumPlans.FirstOrDefaultAsync(p => p.Code == plan.Code);
                if (existingPlan == null)
                {
                    plan.CreatedAtUtc = now;
                    context.PremiumPlans.Add(plan);
                }
                else
                {
                    // Idempotent update: Update safe fields if plan exists
                    existingPlan.Name = plan.Name;
                    existingPlan.Description = plan.Description;
                    existingPlan.PriceVnd = plan.PriceVnd;
                    existingPlan.DurationDays = plan.DurationDays;
                    existingPlan.IsActive = plan.IsActive;
                    existingPlan.DisplayOrder = plan.DisplayOrder;
                    existingPlan.UpdatedAtUtc = now;
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
