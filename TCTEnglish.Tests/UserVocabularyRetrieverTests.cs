using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Internal.Retrievers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class UserVocabularyRetrieverTests
{
    [Fact]
    public async Task RetrieveAsync_ReturnsOnlyOwnedSets()
    {
        var dbName = $"user-vocabulary-retriever-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);

        context.Users.AddRange(
            new User { UserId = 1, Email = "owner@test.local", PasswordHash = "hash", Role = Roles.Standard },
            new User { UserId = 2, Email = "outsider@test.local", PasswordHash = "hash", Role = Roles.Standard });

        context.Sets.AddRange(
            new Set { SetId = 11, OwnerId = 1, SetName = "Owner Set", CreatedAt = DateTime.UtcNow },
            new Set { SetId = 12, OwnerId = 2, SetName = "Outsider Set", CreatedAt = DateTime.UtcNow.AddMinutes(-1) });

        context.Cards.AddRange(
            new Card { CardId = 21, SetId = 11, Term = "forecast", Definition = "predict" },
            new Card { CardId = 22, SetId = 11, Term = "invoice", Definition = "hoa don" },
            new Card { CardId = 23, SetId = 12, Term = "policy", Definition = "chinh sach" });

        await context.SaveChangesAsync();

        var retriever = new UserVocabularyRetriever(context);
        var snippets = await retriever.RetrieveAsync(1, "bộ từ của tôi", CancellationToken.None);

        Assert.Equal(2, snippets.Count);
        Assert.Equal(KnowledgeSnippetSources.UserVocabularySummary, snippets[0].Source);
        Assert.Equal("Owner Set", snippets[1].Title);
        Assert.Equal("cardCount=2", snippets[1].Body);
    }

    [Fact]
    public async Task RetrieveAsync_WhenUserHasNoSets_ReturnsEmpty()
    {
        var dbName = $"user-vocabulary-retriever-empty-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);

        context.Users.Add(new User { UserId = 5, Email = "empty@test.local", PasswordHash = "hash", Role = Roles.Standard });
        await context.SaveChangesAsync();

        var retriever = new UserVocabularyRetriever(context);
        var snippets = await retriever.RetrieveAsync(5, "bộ từ của tôi", CancellationToken.None);

        Assert.Empty(snippets);
    }

    private static DbflashcardContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new DbflashcardContext(options);
    }
}
