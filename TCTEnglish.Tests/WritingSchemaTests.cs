using System.Linq;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingSchemaTests
{
    [Fact]
    public void UserWritingAttempt_ForeignKeys_AvoidSqlServerMultipleCascadePaths()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(nameof(UserWritingAttempt_ForeignKeys_AvoidSqlServerMultipleCascadePaths))
            .Options;

        using var context = new DbflashcardContext(options);
        var attemptEntity = context.Model.FindEntityType(typeof(UserWritingAttempt));
        Assert.NotNull(attemptEntity);
        var foreignKeys = attemptEntity.GetForeignKeys().ToList();

        var userForeignKey = Assert.Single(foreignKeys.Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(User)));
        var exerciseForeignKey = Assert.Single(foreignKeys.Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(WritingExercise)));
        var sentenceForeignKey = Assert.Single(foreignKeys.Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(WritingExerciseSentence)));

        Assert.Equal(DeleteBehavior.Cascade, userForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.NoAction, exerciseForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, sentenceForeignKey.DeleteBehavior);
    }

    [Fact]
    public void WritingExercise_UserForeignKey_UsesNoActionDeleteBehavior()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(nameof(WritingExercise_UserForeignKey_UsesNoActionDeleteBehavior))
            .Options;

        using var context = new DbflashcardContext(options);
        var writingExerciseEntity = context.Model.FindEntityType(typeof(WritingExercise));
        Assert.NotNull(writingExerciseEntity);

        var userForeignKey = Assert.Single(writingExerciseEntity.GetForeignKeys()
            .Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(User)));

        Assert.Equal(DeleteBehavior.NoAction, userForeignKey.DeleteBehavior);
    }

    [Fact]
    public void WritingExercise_ContainsOwnerAndCatalogIndexes()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(nameof(WritingExercise_ContainsOwnerAndCatalogIndexes))
            .Options;

        using var context = new DbflashcardContext(options);
        var writingExerciseEntity = context.Model.FindEntityType(typeof(WritingExercise));
        Assert.NotNull(writingExerciseEntity);

        var indexNames = writingExerciseEntity.GetIndexes()
            .Select(index => index.GetDatabaseName())
            .Where(indexName => indexName != null)
            .ToHashSet();

        Assert.Contains("IX_WritingExercises_UserId_IsPublished_Level_ContentType_Topic", indexNames);
        Assert.Contains("IX_WritingExercises_UserId_IsPublished_CreatedAt", indexNames);
    }

    [Fact]
    public void WritingGenerationLog_UsesUserNoActionAndQuotaIndexes()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(nameof(WritingGenerationLog_UsesUserNoActionAndQuotaIndexes))
            .Options;

        using var context = new DbflashcardContext(options);
        var logEntity = context.Model.FindEntityType(typeof(WritingGenerationLog));
        Assert.NotNull(logEntity);

        var userForeignKey = Assert.Single(logEntity.GetForeignKeys()
            .Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(User)));

        Assert.Equal(DeleteBehavior.NoAction, userForeignKey.DeleteBehavior);

        var indexNames = logEntity.GetIndexes()
            .Select(index => index.GetDatabaseName())
            .Where(indexName => indexName != null)
            .ToHashSet();

        Assert.Contains("IX_WritingGenerationLogs_UserId_RequestedAtUtc", indexNames);
        Assert.Contains("IX_WritingGenerationLogs_UserId_RequestType_RequestedAtUtc", indexNames);
    }
}
