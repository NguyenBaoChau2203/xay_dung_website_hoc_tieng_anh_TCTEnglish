using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TCTVocabulary.Models;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Security;

namespace TCTEnglish.Tests
{
    public class PremiumAccessServiceTests
    {
        private DbflashcardContext GetInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new DbflashcardContext(options);
        }

        [Fact]
        public async Task GetAccessSnapshotAsync_AdminUser_ReturnsPremiumAndAllFeatures()
        {
            // Arrange
            var context = GetInMemoryContext(Guid.NewGuid().ToString());
            var user = new User { UserId = 1, Email = "admin@test.com", PasswordHash = "hash", Role = Roles.Admin };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new PremiumAccessService(context);

            // Act
            var snapshot = await service.GetAccessSnapshotAsync(user.UserId);

            // Assert
            Assert.True(snapshot.IsAuthenticated);
            Assert.True(snapshot.IsPremium);
            Assert.True(snapshot.IsAdmin);
            Assert.Equal(Roles.Admin, snapshot.Role);
            Assert.True(snapshot.HasFeature(PremiumFeatures.WritingAiGeneration));
            Assert.Equal(PremiumFeatures.AllFeatures.Count, snapshot.Features.Count);
        }

        [Fact]
        public async Task GetAccessSnapshotAsync_LegacyPremiumUser_ReturnsPremiumAndAllFeatures()
        {
            // Arrange
            var context = GetInMemoryContext(Guid.NewGuid().ToString());
            var user = new User { UserId = 2, Email = "premium@test.com", PasswordHash = "hash", Role = Roles.Premium };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new PremiumAccessService(context);

            // Act
            var snapshot = await service.GetAccessSnapshotAsync(user.UserId);

            // Assert
            Assert.True(snapshot.IsAuthenticated);
            Assert.True(snapshot.IsPremium);
            Assert.False(snapshot.IsAdmin);
            Assert.Equal(Roles.Premium, snapshot.Role);
            Assert.True(snapshot.HasFeature(PremiumFeatures.ListeningAiQuiz));
        }

        [Fact]
        public async Task GetAccessSnapshotAsync_StandardUser_ReturnsNotPremiumAndNoFeatures()
        {
            // Arrange
            var context = GetInMemoryContext(Guid.NewGuid().ToString());
            var user = new User { UserId = 3, Email = "standard@test.com", PasswordHash = "hash", Role = Roles.Standard };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new PremiumAccessService(context);

            // Act
            var snapshot = await service.GetAccessSnapshotAsync(user.UserId);

            // Assert
            Assert.True(snapshot.IsAuthenticated);
            Assert.False(snapshot.IsPremium);
            Assert.False(snapshot.IsAdmin);
            Assert.Equal(Roles.Standard, snapshot.Role);
            Assert.False(snapshot.HasFeature(PremiumFeatures.WritingAiGeneration));
            Assert.Empty(snapshot.Features);
        }

        [Fact]
        public async Task GetAccessSnapshotAsync_NullRole_DefaultsToStandard()
        {
            // Arrange
            var context = GetInMemoryContext(Guid.NewGuid().ToString());
            var user = new User { UserId = 4, Email = "unknown@test.com", PasswordHash = "hash", Role = null };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new PremiumAccessService(context);

            // Act
            var snapshot = await service.GetAccessSnapshotAsync(user.UserId);

            // Assert
            Assert.True(snapshot.IsAuthenticated);
            Assert.False(snapshot.IsPremium);
            Assert.Equal(Roles.Standard, snapshot.Role);
            Assert.Empty(snapshot.Features);
        }

        [Fact]
        public async Task GetAccessSnapshotAsync_ActiveSubscription_IncludesEndsAtUtc()
        {
            // Arrange
            var context = GetInMemoryContext(Guid.NewGuid().ToString());
            var user = new User { UserId = 10, Email = "sub@test.com", PasswordHash = "hash", Role = Roles.Standard };
            context.Users.Add(user);

            var endsAt = DateTime.UtcNow.AddDays(30);
            context.UserSubscriptions.Add(new UserSubscription
            {
                UserId = user.UserId,
                PlanId = 1,
                Status = SubscriptionStatuses.Active,
                StartsAtUtc = DateTime.UtcNow,
                EndsAtUtc = endsAt
            });
            await context.SaveChangesAsync();

            var service = new PremiumAccessService(context);

            // Act
            var snapshot = await service.GetAccessSnapshotAsync(user.UserId);

            // Assert
            Assert.True(snapshot.IsPremium);
            Assert.NotNull(snapshot.PremiumEndsAtUtc);
            Assert.Equal(endsAt, snapshot.PremiumEndsAtUtc);
        }

        [Theory]
        [InlineData(Roles.Admin, PremiumFeatures.WritingAiGeneration, true)]
        [InlineData(Roles.Premium, PremiumFeatures.ListeningAiQuiz, true)]
        [InlineData(Roles.Standard, PremiumFeatures.WritingAiGeneration, false)]
        public async Task HasFeatureAsync_ReturnsCorrectAccess(string role, string feature, bool expected)
        {
            // Arrange
            var context = GetInMemoryContext(Guid.NewGuid().ToString());
            var user = new User { UserId = 20, Email = "feature@test.com", PasswordHash = "hash", Role = role };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new PremiumAccessService(context);

            // Act
            var result = await service.HasFeatureAsync(user.UserId, feature);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
