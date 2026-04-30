using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTVocabulary.Models;
using TCTEnglish.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    public class BillingSeederTests
    {
        private ServiceProvider GetServiceProvider(string dbName)
        {
            var services = new ServiceCollection();
            services.AddDbContext<DbflashcardContext>(options =>
                options.UseInMemoryDatabase(databaseName: dbName));
            services.AddLogging();
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task SeedAsync_FirstRun_CreatesTwoPlans()
        {
            // Arrange
            var sp = GetServiceProvider(Guid.NewGuid().ToString());

            // Act
            await BillingSeedData.SeedAsync(sp);

            // Assert
            using var scope = sp.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var count = await context.PremiumPlans.CountAsync();
            Assert.Equal(2, count);
            
            var monthly = await context.PremiumPlans.FirstAsync(p => p.Code == "premium_monthly");
            Assert.Equal(49000, monthly.PriceVnd);
            Assert.True(monthly.IsActive);
        }

        [Fact]
        public async Task SeedAsync_SecondRun_DoesNotDuplicate()
        {
            // Arrange
            var sp = GetServiceProvider(Guid.NewGuid().ToString());
            
            // Act
            await BillingSeedData.SeedAsync(sp);
            await BillingSeedData.SeedAsync(sp);

            // Assert
            using var scope = sp.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var count = await context.PremiumPlans.CountAsync();
            Assert.Equal(2, count);
        }

        [Fact]
        public async Task SeedAsync_ExistingPlan_UpdatesSafeFields()
        {
            // Arrange
            var sp = GetServiceProvider(Guid.NewGuid().ToString());
            using (var scope = sp.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
                // Pre-seed an old version
                context.PremiumPlans.Add(new PremiumPlan
                {
                    Code = "premium_monthly",
                    Name = "Old Name",
                    Description = "Old Desc",
                    PriceVnd = 1000,
                    DurationDays = 30,
                    IsActive = true,
                    DisplayOrder = 1,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
                });
                await context.SaveChangesAsync();
            }

            // Act
            await BillingSeedData.SeedAsync(sp);

            // Assert
            using (var scope = sp.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
                var updated = await context.PremiumPlans.FirstAsync(p => p.Code == "premium_monthly");
                Assert.Equal("Premium 1 tháng", updated.Name);
                Assert.Equal(49000, updated.PriceVnd);
                Assert.NotNull(updated.UpdatedAtUtc);
            }
        }
    }
}
