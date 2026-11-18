using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public class PersonalSetsSchemaTests
{
    private static async Task WithDb(Func<DbflashcardContext, Task> action)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SharedDataPackageTests.SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await action(db);
    }

    [Fact]
    public void ModelContainsSubscriptionWithUniqueUserSetIndexAndCascadeForeignKeys()
    {
        using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite("Data Source=:memory:").Options);
        var entity = db.Model.FindEntityType(typeof(SetSubscription));
        Assert.NotNull(entity);
        Assert.Contains(entity!.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "UserId", "SetId" }));
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior));
        Assert.Equal(2, entity.GetForeignKeys().Count());
    }

    [Fact]
    public void ShareSlugIsUniqueAndVisibilityDefaultsToPublicInModelAndClr()
    {
        using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite("Data Source=:memory:").Options);
        var set = db.Model.FindEntityType(typeof(Set))!;
        Assert.Contains(set.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(Set.ShareSlug));
        Assert.Equal("Public", set.FindProperty(nameof(Set.Visibility))!.GetDefaultValue());
        Assert.False(set.FindProperty(nameof(Set.Visibility))!.IsNullable);
        Assert.Equal("Public", new Set().Visibility);
        Assert.False(new Card().AiGenerated);
        Assert.False(new LearningProgress().IsSuspended);
    }

    [Fact]
    public Task ShareSlugUniquenessAndSubscriptionUniquenessAreEnforced() => WithDb(async db =>
    {
        db.Users.Add(new User { UserId = 1, Email = "a@example.test", PasswordHash = "x", Role = "Standard", CreatedAt = DateTime.UtcNow });
        db.Sets.Add(new Set { SetId = 10, SetName = "A", OwnerId = 1, ShareSlug = "abcdefghij" });
        db.Sets.Add(new Set { SetId = 11, SetName = "B", OwnerId = 1, ShareSlug = null });
        db.Sets.Add(new Set { SetId = 12, SetName = "C", OwnerId = 1, ShareSlug = null });
        await db.SaveChangesAsync();

        db.Sets.Add(new Set { SetId = 13, SetName = "D", OwnerId = 1, ShareSlug = "abcdefghij" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.SetSubscriptions.Add(new SetSubscription { UserId = 1, SetId = 10, SubscribedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.SetSubscriptions.Add(new SetSubscription { UserId = 1, SetId = 10, SubscribedAt = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    });
}
