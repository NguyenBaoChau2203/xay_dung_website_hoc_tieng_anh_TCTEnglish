using System.Linq;
using Microsoft.EntityFrameworkCore;
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
}
