using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TCTVocabulary.Models;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Security;
using TCTVocabulary.Services;

namespace TCTEnglish.Tests
{
    public class SubscriptionServiceTests
    {
        private DbflashcardContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new DbflashcardContext(options);
        }

        /// <summary>Inserts a User + PremiumPlan + paid PaymentOrder into the DB.</summary>
        private async Task<(User user, PremiumPlan plan, PaymentOrder order)> SeedPaidOrder(
            DbflashcardContext ctx, string role = "Standard", int durationDays = 30, string orderStatus = "paid")
        {
            var user = new User
            {
                UserId = 0, // auto
                Email = $"user-{Guid.NewGuid():N}@test.com",
                PasswordHash = "hash",
                Role = role
            };
            ctx.Users.Add(user);

            var plan = new PremiumPlan
            {
                Code = $"plan-{Guid.NewGuid():N}",
                Name = "Test Plan",
                Description = "Test",
                PriceVnd = 49000,
                DurationDays = durationDays,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            var order = new PaymentOrder
            {
                OrderCode = $"ORD-{Guid.NewGuid():N}",
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = PaymentProviders.VNPay,
                AmountVnd = plan.PriceVnd,
                Currency = "VND",
                Status = orderStatus,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
                PaidAtUtc = orderStatus == "paid" ? DateTime.UtcNow : null
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();

            return (user, plan, order);
        }

        // ───────────────────────────────────────────────────────────────────────
        //  ActivateFromPaidOrderAsync
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Activate_PaidOrder_CreatesSubscription()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan, order) = await SeedPaidOrder(ctx);
            var svc = new SubscriptionService(ctx);

            var sub = await svc.ActivateFromPaidOrderAsync(order.Id);

            Assert.Equal(user.UserId, sub.UserId);
            Assert.Equal(plan.Id, sub.PlanId);
            Assert.Equal(SubscriptionStatuses.Active, sub.Status);
            Assert.Equal(order.Id, sub.ActivatedByPaymentOrderId);
            Assert.True((sub.EndsAtUtc - sub.StartsAtUtc).TotalDays >= plan.DurationDays - 1);
        }

        [Fact]
        public async Task Activate_PaidOrder_SetsRoleToPremium()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, _, order) = await SeedPaidOrder(ctx, role: Roles.Standard);
            var svc = new SubscriptionService(ctx);

            await svc.ActivateFromPaidOrderAsync(order.Id);

            var reloaded = await ctx.Users.FindAsync(user.UserId);
            Assert.Equal(Roles.Premium, reloaded!.Role);
        }

        [Fact]
        public async Task Activate_AdminUser_DoesNotChangeRole()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, _, order) = await SeedPaidOrder(ctx, role: Roles.Admin);
            var svc = new SubscriptionService(ctx);

            await svc.ActivateFromPaidOrderAsync(order.Id);

            var reloaded = await ctx.Users.FindAsync(user.UserId);
            Assert.Equal(Roles.Admin, reloaded!.Role);
        }

        [Fact]
        public async Task Activate_DuplicateCall_DoesNotStackDuration()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, _, order) = await SeedPaidOrder(ctx);
            var svc = new SubscriptionService(ctx);

            var first = await svc.ActivateFromPaidOrderAsync(order.Id);
            var second = await svc.ActivateFromPaidOrderAsync(order.Id);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(first.EndsAtUtc, second.EndsAtUtc);
            Assert.Equal(1, await ctx.UserSubscriptions.CountAsync());
        }

        [Fact]
        public async Task Activate_UserWith10DaysLeft_ExtendsByPlanDuration()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan, _) = await SeedPaidOrder(ctx, durationDays: 30);

            // Insert a pre-existing active subscription with 10 days remaining
            var nowUtc = DateTime.UtcNow;
            var existingSub = new UserSubscription
            {
                UserId = user.UserId,
                PlanId = plan.Id,
                Status = SubscriptionStatuses.Active,
                StartsAtUtc = nowUtc.AddDays(-20),
                EndsAtUtc = nowUtc.AddDays(10),
                CreatedAtUtc = nowUtc.AddDays(-20)
            };
            ctx.UserSubscriptions.Add(existingSub);
            await ctx.SaveChangesAsync();

            // Create a second paid order for the same user
            var order2 = new PaymentOrder
            {
                OrderCode = $"ORD2-{Guid.NewGuid():N}",
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = PaymentProviders.VNPay,
                AmountVnd = plan.PriceVnd,
                Currency = "VND",
                Status = PaymentOrderStatuses.Paid,
                CreatedAtUtc = nowUtc,
                ExpiresAtUtc = nowUtc.AddMinutes(15),
                PaidAtUtc = nowUtc
            };
            ctx.PaymentOrders.Add(order2);
            await ctx.SaveChangesAsync();

            var svc = new SubscriptionService(ctx);
            var newSub = await svc.ActivateFromPaidOrderAsync(order2.Id);

            // The new subscription should start at the existing end (nowUtc + 10d)
            // and end at existingEnd + 30d = nowUtc + 40d
            var expectedEnd = existingSub.EndsAtUtc.AddDays(30);
            var diffDays = Math.Abs((newSub.EndsAtUtc - expectedEnd).TotalDays);
            Assert.True(diffDays < 1, $"Expected ~{expectedEnd:O} but got {newSub.EndsAtUtc:O}");
        }

        [Fact]
        public async Task Activate_UnpaidOrder_Throws()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, _, order) = await SeedPaidOrder(ctx, orderStatus: PaymentOrderStatuses.Pending);
            var svc = new SubscriptionService(ctx);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.ActivateFromPaidOrderAsync(order.Id));

            Assert.Contains("pending", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Activate_NonExistentOrder_Throws()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var svc = new SubscriptionService(ctx);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.ActivateFromPaidOrderAsync(999));
        }

        // ───────────────────────────────────────────────────────────────────────
        //  ExpireSubscriptionsAsync
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Expire_OverdueSubscription_SetsExpiredAndDowngradesRole()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan, _) = await SeedPaidOrder(ctx);

            // Set role to Premium (simulating prior activation)
            user.Role = Roles.Premium;

            var sub = new UserSubscription
            {
                UserId = user.UserId,
                PlanId = plan.Id,
                Status = SubscriptionStatuses.Active,
                StartsAtUtc = DateTime.UtcNow.AddDays(-35),
                EndsAtUtc = DateTime.UtcNow.AddDays(-5), // expired 5 days ago
                CreatedAtUtc = DateTime.UtcNow.AddDays(-35)
            };
            ctx.UserSubscriptions.Add(sub);
            await ctx.SaveChangesAsync();

            var svc = new SubscriptionService(ctx);
            var count = await svc.ExpireSubscriptionsAsync();

            Assert.Equal(1, count);

            var reloadedSub = await ctx.UserSubscriptions.FindAsync(sub.Id);
            Assert.Equal(SubscriptionStatuses.Expired, reloadedSub!.Status);

            var reloadedUser = await ctx.Users.FindAsync(user.UserId);
            Assert.Equal(Roles.Standard, reloadedUser!.Role);
        }

        [Fact]
        public async Task Expire_AdminUser_DoesNotDowngrade()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan, _) = await SeedPaidOrder(ctx, role: Roles.Admin);

            var sub = new UserSubscription
            {
                UserId = user.UserId,
                PlanId = plan.Id,
                Status = SubscriptionStatuses.Active,
                StartsAtUtc = DateTime.UtcNow.AddDays(-35),
                EndsAtUtc = DateTime.UtcNow.AddDays(-5),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-35)
            };
            ctx.UserSubscriptions.Add(sub);
            await ctx.SaveChangesAsync();

            var svc = new SubscriptionService(ctx);
            await svc.ExpireSubscriptionsAsync();

            var reloadedUser = await ctx.Users.FindAsync(user.UserId);
            Assert.Equal(Roles.Admin, reloadedUser!.Role);
        }

        // ───────────────────────────────────────────────────────────────────────
        //  PremiumAccessService integration with subscriptions
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task PremiumAccess_ActiveSubscription_ReturnsPremium()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var user = new User
            {
                Email = "sub-user@test.com",
                PasswordHash = "hash",
                Role = Roles.Standard
            };
            ctx.Users.Add(user);

            var plan = new PremiumPlan
            {
                Code = "monthly", Name = "Monthly", Description = "d",
                PriceVnd = 49000, DurationDays = 30, IsActive = true,
                DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
            };
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            ctx.UserSubscriptions.Add(new UserSubscription
            {
                UserId = user.UserId,
                PlanId = plan.Id,
                Status = SubscriptionStatuses.Active,
                StartsAtUtc = DateTime.UtcNow.AddDays(-5),
                EndsAtUtc = DateTime.UtcNow.AddDays(25),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
            });
            await ctx.SaveChangesAsync();

            var accessSvc = new PremiumAccessService(ctx);
            var snapshot = await accessSvc.GetAccessSnapshotAsync(user.UserId);

            Assert.True(snapshot.IsPremium);
            Assert.True(snapshot.HasFeature(PremiumFeatures.WritingAiGeneration));
            Assert.NotNull(snapshot.PremiumEndsAtUtc);
        }

        [Fact]
        public async Task PremiumAccess_LegacyPremiumRole_StillWorks()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var user = new User
            {
                Email = "legacy@test.com",
                PasswordHash = "hash",
                Role = Roles.Premium
            };
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();

            var accessSvc = new PremiumAccessService(ctx);
            var snapshot = await accessSvc.GetAccessSnapshotAsync(user.UserId);

            Assert.True(snapshot.IsPremium);
            Assert.True(snapshot.HasFeature(PremiumFeatures.ListeningAiQuiz));
            // No subscription → PremiumEndsAtUtc should be null
            Assert.Null(snapshot.PremiumEndsAtUtc);
        }

        [Fact]
        public async Task PremiumAccess_ExpiredSubscription_NotPremium()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var user = new User
            {
                Email = "expired@test.com",
                PasswordHash = "hash",
                Role = Roles.Standard
            };
            ctx.Users.Add(user);

            var plan = new PremiumPlan
            {
                Code = "monthly2", Name = "Monthly", Description = "d",
                PriceVnd = 49000, DurationDays = 30, IsActive = true,
                DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
            };
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            ctx.UserSubscriptions.Add(new UserSubscription
            {
                UserId = user.UserId,
                PlanId = plan.Id,
                Status = SubscriptionStatuses.Expired,
                StartsAtUtc = DateTime.UtcNow.AddDays(-35),
                EndsAtUtc = DateTime.UtcNow.AddDays(-5),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-35)
            });
            await ctx.SaveChangesAsync();

            var accessSvc = new PremiumAccessService(ctx);
            var snapshot = await accessSvc.GetAccessSnapshotAsync(user.UserId);

            Assert.False(snapshot.IsPremium);
            Assert.Empty(snapshot.Features);
        }
        [Fact]
        public async Task Revoke_ActiveSubscription_SetsRevokedAndDowngrades()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan, _) = await SeedPaidOrder(ctx, role: Roles.Premium);

            var sub = new UserSubscription
            {
                UserId = user.UserId,
                PlanId = plan.Id,
                Status = SubscriptionStatuses.Active,
                StartsAtUtc = DateTime.UtcNow.AddDays(-5),
                EndsAtUtc = DateTime.UtcNow.AddDays(25),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
            };
            ctx.UserSubscriptions.Add(sub);
            await ctx.SaveChangesAsync();

            var svc = new SubscriptionService(ctx);
            var result = await svc.RevokeAsync(user.UserId, "Testing revocation");

            Assert.Equal(OperationStatus.Success, result.Status);

            var reloadedSub = await ctx.UserSubscriptions.FindAsync(sub.Id);
            Assert.Equal(SubscriptionStatuses.Revoked, reloadedSub!.Status);

            var reloadedUser = await ctx.Users.FindAsync(user.UserId);
            Assert.Equal(Roles.Standard, reloadedUser!.Role);
        }

        [Fact]
        public async Task GrantManual_NewUser_CreatesSubscriptionAndUpgrades()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var user = new User { Email = "grant@test.com", PasswordHash = "h", Role = Roles.Standard };
            ctx.Users.Add(user);

            var plan = new PremiumPlan
            {
                Code = "manual", Name = "Manual", Description = "d", PriceVnd = 0, DurationDays = 30, IsActive = true, CreatedAtUtc = DateTime.UtcNow
            };
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            var svc = new SubscriptionService(ctx);
            var result = await svc.GrantManualAsync(user.UserId, plan.Id, 30, "Bonus");

            Assert.Equal(OperationStatus.Success, result.Status);

            var sub = await ctx.UserSubscriptions.FirstAsync(s => s.UserId == user.UserId);
            Assert.Equal(SubscriptionStatuses.Active, sub.Status);
            Assert.Null(sub.ActivatedByPaymentOrderId); // Manual grant has no payment order

            var reloadedUser = await ctx.Users.FindAsync(user.UserId);
            Assert.Equal(Roles.Premium, reloadedUser!.Role);
        }
    }
}
