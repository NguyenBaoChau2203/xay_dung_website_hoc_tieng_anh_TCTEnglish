using System.Linq;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SpeakingSchemaTests
{
    [Fact]
    public void SpeakingVideo_OwnerForeignKey_UsesNoActionDeleteBehavior()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(nameof(SpeakingVideo_OwnerForeignKey_UsesNoActionDeleteBehavior))
            .Options;

        using var context = new DbflashcardContext(options);
        var speakingVideoEntity = context.Model.FindEntityType(typeof(SpeakingVideo));
        Assert.NotNull(speakingVideoEntity);

        var ownerForeignKey = Assert.Single(speakingVideoEntity.GetForeignKeys()
            .Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(User)
                && foreignKey.Properties.Any(property => property.Name == nameof(SpeakingVideo.OwnerUserId))));

        Assert.Equal(DeleteBehavior.NoAction, ownerForeignKey.DeleteBehavior);
    }

    [Fact]
    public void SpeakingVideo_ContainsOwnerIndexes_ForPrivateImports()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(nameof(SpeakingVideo_ContainsOwnerIndexes_ForPrivateImports))
            .Options;

        using var context = new DbflashcardContext(options);
        var speakingVideoEntity = context.Model.FindEntityType(typeof(SpeakingVideo));
        Assert.NotNull(speakingVideoEntity);

        var indexNames = speakingVideoEntity.GetIndexes()
            .Select(index => index.GetDatabaseName())
            .Where(indexName => indexName != null)
            .ToHashSet();

        Assert.Contains("IX_SpeakingVideos_OwnerUserId_CreatedAt", indexNames);
        Assert.Contains("IX_SpeakingVideos_OwnerUserId_YoutubeId", indexNames);
    }

    [Fact]
    public void SpeakingSentence_ContainsVideoStartTimeIndex()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(nameof(SpeakingSentence_ContainsVideoStartTimeIndex))
            .Options;

        using var context = new DbflashcardContext(options);
        var speakingSentenceEntity = context.Model.FindEntityType(typeof(SpeakingSentence));
        Assert.NotNull(speakingSentenceEntity);

        var indexNames = speakingSentenceEntity.GetIndexes()
            .Select(index => index.GetDatabaseName())
            .Where(indexName => indexName != null)
            .ToHashSet();

        Assert.Contains("IX_SpeakingSentences_VideoId_StartTime", indexNames);
    }
}
