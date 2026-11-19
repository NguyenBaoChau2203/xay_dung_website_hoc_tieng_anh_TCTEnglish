using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Migrations;
using TCTEnglish.Models;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>S1 of the onboarding feature: the one new table and its migration.</summary>
public sealed class OnboardingSchemaTests
{
    [Fact]
    public void Migration_OnlyCreatesTheNewTable_AndDownDropsOnlyIt()
    {
        var migration = new AddUserOnboarding();

        var up = Assert.Single(migration.UpOperations);
        var create = Assert.IsType<CreateTableOperation>(up);
        Assert.Equal("UserOnboardings", create.Name);
        Assert.Equal(new[] { "UserId" }, create.PrimaryKey!.Columns);
        var foreignKey = Assert.Single(create.ForeignKeys);
        Assert.Equal("Users", foreignKey.PrincipalTable);
        Assert.Equal(ReferentialAction.Cascade, foreignKey.OnDelete);

        var down = Assert.Single(migration.DownOperations);
        Assert.Equal("UserOnboardings", Assert.IsType<DropTableOperation>(down).Name);
    }

    [Fact]
    public async Task Row_RoundTripsEveryField_AndIsRemovedWithItsUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var now = new DateTime(2026, 11, 19, 8, 30, 0, DateTimeKind.Utc);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.UserOnboardings.Add(new UserOnboarding
            {
                UserId = TestDataIds.OutsiderUserId,
                Status = OnboardingStatus.Completed,
                PromptCount = 1,
                LastPromptedAt = now,
                GoalsJson = "[\"travel\",\"communication\"]",
                TopicKeysJson = "[\"A06\",\"C03\"]",
                Level = "A2",
                DailyNewWords = 10,
                KnownTermsJson = "[\"hello\"]",
                DecksJson = "[{\"setId\":1,\"goal\":\"travel\",\"topicKeys\":[\"A06\"]}]",
                FolderId = 12,
                LastRequestId = Guid.NewGuid().ToString(),
                LastRequestHash = new string('a', 64),
                LastResultJson = "{\"ok\":true}",
                BannerDismissedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                CompletedAt = now
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var row = await db.UserOnboardings.AsNoTracking().SingleAsync(x => x.UserId == TestDataIds.OutsiderUserId);
            Assert.Equal(OnboardingStatus.Completed, row.Status);
            Assert.Equal("A2", row.Level);
            Assert.Equal(10, row.DailyNewWords);
            Assert.Equal("[\"A06\",\"C03\"]", row.TopicKeysJson);
            Assert.Equal(12, row.FolderId);
            Assert.Equal(64, row.LastRequestHash!.Length);

            // one row per user: a second row for the same user is rejected
            db.UserOnboardings.Add(new UserOnboarding { UserId = TestDataIds.OutsiderUserId, CreatedAt = now, UpdatedAt = now });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.UserOnboardings.CountAsync());
            await db.Users.Where(u => u.UserId == TestDataIds.OutsiderUserId).ExecuteDeleteAsync();
            Assert.Equal(0, await db.UserOnboardings.CountAsync());
        }
    }

    [Fact]
    public void Model_MapsTheColumnsTheMigrationCreates()
    {
        using var factory = new TestWebApplicationFactory();
        factory.InitializeAsync().GetAwaiter().GetResult();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var entity = db.Model.FindEntityType(typeof(UserOnboarding))!;
        Assert.Equal("UserOnboardings", entity.GetTableName());
        Assert.Equal(16, entity.FindProperty(nameof(UserOnboarding.Status))!.GetMaxLength());
        Assert.Equal(2, entity.FindProperty(nameof(UserOnboarding.Level))!.GetMaxLength());
        Assert.Equal("mediumtext", entity.FindProperty(nameof(UserOnboarding.KnownTermsJson))!.GetColumnType());
        Assert.Equal("mediumtext", entity.FindProperty(nameof(UserOnboarding.DecksJson))!.GetColumnType());
        // FolderId has no foreign key: a user may delete the folder
        Assert.DoesNotContain(entity.GetForeignKeys(), fk => fk.Properties.Any(p => p.Name == nameof(UserOnboarding.FolderId)));
    }
}
