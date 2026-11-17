using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>
    /// What the billing pages show about a user's Premium (plan 4.6): the end date for everyone who has one,
    /// the permanent flag, days left, and lazy expiry so a lapsed subscription never looks active.
    /// </summary>
    public class PremiumStatusAndExpiryTests
    {
        private static DbflashcardContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new DbflashcardContext(options);
        }

        private static async Task<(User user, PremiumPlan plan)> SeedAsync(DbflashcardContext ctx, string role)
        {
            var user = new User { Email = $"u-{Guid.NewGuid():N}@test.com", PasswordHash = "h", Role = role };
            var plan = new PremiumPlan
            {
                Code = $"p-{Guid.NewGuid():N}", Name = "Premium", Description = "d", PriceVnd = 49000m,
                DurationDays = 30, IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();
            return (user, plan);
        }

        private static async Task AddSubscriptionAsync(
            DbflashcardContext ctx, User user, PremiumPlan plan, DateTime endsAtUtc, string status = SubscriptionStatuses.Active)
        {
            ctx.UserSubscriptions.Add(new UserSubscription
            {
                UserId = user.UserId, PlanId = plan.Id, Status = status,
                StartsAtUtc = endsAtUtc.AddDays(-30), EndsAtUtc = endsAtUtc, CreatedAtUtc = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        // ─── Snapshot ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task RolePremiumSubscriber_SeesTheEndDate_AndIsNotPermanent()
        {
            await using var ctx = CreateContext();
            var (user, plan) = await SeedAsync(ctx, Roles.Premium);
            var ends = DateTime.UtcNow.AddDays(10).AddHours(3);
            await AddSubscriptionAsync(ctx, user, plan, ends);

            var snapshot = await new PremiumAccessService(ctx).GetAccessSnapshotAsync(user.UserId);

            Assert.True(snapshot.IsPremium);
            Assert.Equal(ends, snapshot.PremiumEndsAtUtc);
            Assert.False(snapshot.IsPermanent);
            Assert.Equal(11, snapshot.DaysLeft);
        }

        [Fact]
        public async Task StandardRoleWithAnActiveSubscription_StillSeesTheEndDate()
        {
            await using var ctx = CreateContext();
            var (user, plan) = await SeedAsync(ctx, Roles.Standard);
            var ends = DateTime.UtcNow.AddDays(5).AddMinutes(1);
            await AddSubscriptionAsync(ctx, user, plan, ends);

            var snapshot = await new PremiumAccessService(ctx).GetAccessSnapshotAsync(user.UserId);

            Assert.True(snapshot.IsPremium);
            Assert.Equal(ends, snapshot.PremiumEndsAtUtc);
            Assert.False(snapshot.IsPermanent);
            Assert.Equal(6, snapshot.DaysLeft);
        }

        [Fact]
        public async Task TheLatestActiveSubscription_DecidesTheEndDate()
        {
            await using var ctx = CreateContext();
            var (user, plan) = await SeedAsync(ctx, Roles.Premium);
            var later = DateTime.UtcNow.AddDays(40);
            await AddSubscriptionAsync(ctx, user, plan, DateTime.UtcNow.AddDays(10));
            await AddSubscriptionAsync(ctx, user, plan, later);
            await AddSubscriptionAsync(ctx, user, plan, DateTime.UtcNow.AddDays(90), SubscriptionStatuses.Revoked);

            var snapshot = await new PremiumAccessService(ctx).GetAccessSnapshotAsync(user.UserId);

            Assert.Equal(later, snapshot.PremiumEndsAtUtc);
        }

        [Fact]
        public async Task LegacyPremiumRole_WithNoSubscriptionRow_IsPermanent_WithoutAnEndDate()
        {
            await using var ctx = CreateContext();
            var (user, _) = await SeedAsync(ctx, Roles.Premium);

            var snapshot = await new PremiumAccessService(ctx).GetAccessSnapshotAsync(user.UserId);

            Assert.True(snapshot.IsPremium);
            Assert.True(snapshot.IsPermanent);
            Assert.Null(snapshot.PremiumEndsAtUtc);
            Assert.Null(snapshot.DaysLeft);
        }

        [Fact]
        public async Task PremiumRole_WhoseOnlySubscriptionHasLapsed_IsNotPermanent()
        {
            await using var ctx = CreateContext();
            var (user, plan) = await SeedAsync(ctx, Roles.Premium);
            await AddSubscriptionAsync(ctx, user, plan, DateTime.UtcNow.AddDays(-1), SubscriptionStatuses.Expired);

            var snapshot = await new PremiumAccessService(ctx).GetAccessSnapshotAsync(user.UserId);

            Assert.False(snapshot.IsPermanent);
            Assert.Null(snapshot.PremiumEndsAtUtc);
        }

        [Fact]
        public async Task Admin_IsPermanent_AndNeverHasAnEndDate()
        {
            await using var ctx = CreateContext();
            var (user, plan) = await SeedAsync(ctx, Roles.Admin);
            await AddSubscriptionAsync(ctx, user, plan, DateTime.UtcNow.AddDays(10));

            var snapshot = await new PremiumAccessService(ctx).GetAccessSnapshotAsync(user.UserId);

            Assert.True(snapshot.IsPremium);
            Assert.True(snapshot.IsAdmin);
            Assert.True(snapshot.IsPermanent);
            Assert.Null(snapshot.PremiumEndsAtUtc);
        }

        [Fact]
        public async Task FreeUser_IsNotPremium_NotPermanent_WithNothingElse()
        {
            await using var ctx = CreateContext();
            var (user, _) = await SeedAsync(ctx, Roles.Standard);

            var snapshot = await new PremiumAccessService(ctx).GetAccessSnapshotAsync(user.UserId);

            Assert.False(snapshot.IsPremium);
            Assert.False(snapshot.IsPermanent);
            Assert.Null(snapshot.PremiumEndsAtUtc);
            Assert.Null(snapshot.DaysLeft);
        }

        [Theory]
        [InlineData(Roles.Admin, false, true)]
        [InlineData(Roles.Premium, false, true)]
        [InlineData(Roles.Premium, true, false)]
        [InlineData(Roles.Standard, false, false)]
        [InlineData(null, false, false)]
        public void PremiumAccessRules_IsPermanent(string? role, bool hasSubscriptionRow, bool expected)
        {
            Assert.Equal(expected, PremiumAccessRules.IsPermanent(role, hasSubscriptionRow));
        }

        // ─── Lazy expiry ──────────────────────────────────────────────────────────

        [Fact]
        public async Task ExpireForUser_ExpiresOnlyThatUsersOverdueSubscriptions_AndDowngradesTheRole()
        {
            await using var ctx = CreateContext();
            var (lapsed, plan) = await SeedAsync(ctx, Roles.Premium);
            var (other, _) = await SeedAsync(ctx, Roles.Premium);
            await AddSubscriptionAsync(ctx, lapsed, plan, DateTime.UtcNow.AddHours(-1));
            await AddSubscriptionAsync(ctx, other, plan, DateTime.UtcNow.AddHours(-1));

            var count = await new SubscriptionService(ctx).ExpireForUserAsync(lapsed.UserId);

            Assert.Equal(1, count);
            Assert.Equal(SubscriptionStatuses.Expired, ctx.UserSubscriptions.Single(s => s.UserId == lapsed.UserId).Status);
            Assert.Equal(Roles.Standard, ctx.Users.Single(u => u.UserId == lapsed.UserId).Role);
            // The other user's overdue subscription is left for the worker.
            Assert.Equal(SubscriptionStatuses.Active, ctx.UserSubscriptions.Single(s => s.UserId == other.UserId).Status);
            Assert.Equal(Roles.Premium, ctx.Users.Single(u => u.UserId == other.UserId).Role);
        }

        [Fact]
        public async Task ExpireForUser_KeepsPremium_WhenAnotherSubscriptionIsStillActive()
        {
            await using var ctx = CreateContext();
            var (user, plan) = await SeedAsync(ctx, Roles.Premium);
            await AddSubscriptionAsync(ctx, user, plan, DateTime.UtcNow.AddHours(-1));
            await AddSubscriptionAsync(ctx, user, plan, DateTime.UtcNow.AddDays(20));

            var count = await new SubscriptionService(ctx).ExpireForUserAsync(user.UserId);

            Assert.Equal(1, count);
            Assert.Equal(Roles.Premium, ctx.Users.Single(u => u.UserId == user.UserId).Role);
        }

        [Fact]
        public async Task ExpireForUser_NeverTouchesAnAdmin()
        {
            await using var ctx = CreateContext();
            var (admin, plan) = await SeedAsync(ctx, Roles.Admin);
            await AddSubscriptionAsync(ctx, admin, plan, DateTime.UtcNow.AddHours(-1));

            await new SubscriptionService(ctx).ExpireForUserAsync(admin.UserId);

            Assert.Equal(Roles.Admin, ctx.Users.Single(u => u.UserId == admin.UserId).Role);
        }

        [Fact]
        public async Task ExpireForUser_IsIdempotent_AndCountsNothingWhenNothingIsOverdue()
        {
            await using var ctx = CreateContext();
            var (user, plan) = await SeedAsync(ctx, Roles.Premium);
            await AddSubscriptionAsync(ctx, user, plan, DateTime.UtcNow.AddHours(-1));
            var service = new SubscriptionService(ctx);

            Assert.Equal(1, await service.ExpireForUserAsync(user.UserId));
            Assert.Equal(0, await service.ExpireForUserAsync(user.UserId));
            Assert.Equal(0, await service.ExpireForUserAsync(user.UserId + 999));
        }

        [Fact]
        public async Task ExpireSubscriptions_StillExpiresEveryone()
        {
            await using var ctx = CreateContext();
            var (a, plan) = await SeedAsync(ctx, Roles.Premium);
            var (b, _) = await SeedAsync(ctx, Roles.Premium);
            await AddSubscriptionAsync(ctx, a, plan, DateTime.UtcNow.AddHours(-1));
            await AddSubscriptionAsync(ctx, b, plan, DateTime.UtcNow.AddHours(-2));

            Assert.Equal(2, await new SubscriptionService(ctx).ExpireSubscriptionsAsync());
        }
    }
}
