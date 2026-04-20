using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Internal.Retrievers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class LearningProgressRetrieverTests
{
    [Fact]
    public async Task RetrieveAsync_PartialProgressCountsUnstartedCardsInRemainingCount()
    {
        await using var context = CreateContext(nameof(RetrieveAsync_PartialProgressCountsUnstartedCardsInRemainingCount));
        await SeedUsersAsync(context);
        await SeedOwnedSetAsync(context, cardCount: 10);

        context.LearningProgresses.AddRange(
            new LearningProgress
            {
                ProgressId = 301,
                UserId = 1,
                CardId = 21,
                Status = "Learning",
                LastReviewedDate = DateTime.UtcNow
            },
            new LearningProgress
            {
                ProgressId = 302,
                UserId = 1,
                CardId = 22,
                Status = "Mastered",
                LastReviewedDate = DateTime.UtcNow.AddMinutes(-5)
            });

        await context.SaveChangesAsync();

        var retriever = new LearningProgressRetriever(context);
        var snippets = await retriever.RetrieveAsync(1, "toi nen hoc gi tiep", CancellationToken.None);

        var recommendation = Assert.Single(snippets, snippet => snippet.Source == KnowledgeSnippetSources.StudyRecommendation);
        Assert.Contains("remainingCount=9", recommendation.Body);
    }

    private static DbflashcardContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new DbflashcardContext(options);
    }

    private static async Task SeedUsersAsync(DbflashcardContext context)
    {
        context.Users.AddRange(
            new User { UserId = 1, Email = "owner@test.local", PasswordHash = "hash", Role = Roles.Standard },
            new User { UserId = 2, Email = "outsider@test.local", PasswordHash = "hash", Role = Roles.Standard });

        await context.SaveChangesAsync();
    }

    private static async Task SeedOwnedSetAsync(DbflashcardContext context, int cardCount)
    {
        context.Sets.Add(new Set
        {
            SetId = 11,
            OwnerId = 1,
            SetName = "Owner Study Set",
            CreatedAt = DateTime.UtcNow
        });

        for (var i = 0; i < cardCount; i++)
        {
            context.Cards.Add(new Card
            {
                CardId = 21 + i,
                SetId = 11,
                Term = $"term-{i + 1}",
                Definition = $"definition-{i + 1}"
            });
        }

        await context.SaveChangesAsync();
    }
}
