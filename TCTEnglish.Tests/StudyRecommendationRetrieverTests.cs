using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Internal.Retrievers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class StudyRecommendationRetrieverTests
{
    [Fact]
    public async Task RetrieveAsync_CountsMasteredStatusVariantsAsMastered()
    {
        await using var context = CreateContext(nameof(RetrieveAsync_CountsMasteredStatusVariantsAsMastered));
        await SeedUsersAsync(context);
        await SeedOwnedSetAsync(context);

        context.LearningProgresses.AddRange(
            new LearningProgress { ProgressId = 101, UserId = 1, CardId = 21, Status = "Mastered" },
            new LearningProgress { ProgressId = 102, UserId = 1, CardId = 22, Status = "mastered" },
            new LearningProgress { ProgressId = 103, UserId = 1, CardId = 23, Status = "Learned" },
            new LearningProgress { ProgressId = 104, UserId = 1, CardId = 24, Status = "Learning" });

        await context.SaveChangesAsync();

        var retriever = new StudyRecommendationRetriever(context);
        var snippets = await retriever.RetrieveAsync(1, "toi nen hoc gi tiep", CancellationToken.None);

        var recommendation = Assert.Single(snippets);
        Assert.Equal(KnowledgeSnippetSources.StudyRecommendation, recommendation.Source);
        Assert.Equal("Owner Study Set", recommendation.Title);
        Assert.Contains("remainingCount=1", recommendation.Body);
    }

    [Fact]
    public async Task RetrieveAsync_DoesNotCountAnotherUsersProgressOnOwnedCards()
    {
        await using var context = CreateContext(nameof(RetrieveAsync_DoesNotCountAnotherUsersProgressOnOwnedCards));
        await SeedUsersAsync(context);
        await SeedOwnedSetAsync(context, cardCount: 1);

        context.LearningProgresses.Add(new LearningProgress
        {
            ProgressId = 201,
            UserId = 2,
            CardId = 21,
            Status = "Mastered"
        });

        await context.SaveChangesAsync();

        var retriever = new StudyRecommendationRetriever(context);
        var snippets = await retriever.RetrieveAsync(1, "toi nen hoc gi tiep", CancellationToken.None);

        var recommendation = Assert.Single(snippets);
        Assert.Equal("Owner Study Set", recommendation.Title);
        Assert.Contains("remainingCount=1", recommendation.Body);
    }

    [Fact]
    public async Task RetrieveAsync_DoesNotReturnAnotherUsersSet()
    {
        await using var context = CreateContext(nameof(RetrieveAsync_DoesNotReturnAnotherUsersSet));
        await SeedUsersAsync(context);

        context.Sets.Add(new Set
        {
            SetId = 12,
            OwnerId = 2,
            SetName = "Outsider Set",
            CreatedAt = DateTime.UtcNow
        });

        context.Cards.Add(new Card
        {
            CardId = 31,
            SetId = 12,
            Term = "invoice",
            Definition = "bill"
        });

        await context.SaveChangesAsync();

        var retriever = new StudyRecommendationRetriever(context);
        var snippets = await retriever.RetrieveAsync(1, "toi nen hoc gi tiep", CancellationToken.None);

        Assert.Empty(snippets);
    }

    [Fact]
    public async Task RetrieveAsync_WithOwnedSetAndNoProgress_ReturnsRecommendation()
    {
        await using var context = CreateContext(nameof(RetrieveAsync_WithOwnedSetAndNoProgress_ReturnsRecommendation));
        await SeedUsersAsync(context);
        await SeedOwnedSetAsync(context, cardCount: 3);

        var retriever = new StudyRecommendationRetriever(context);
        var snippets = await retriever.RetrieveAsync(1, "toi nen hoc gi tiep", CancellationToken.None);

        var recommendation = Assert.Single(snippets);
        Assert.Equal("Owner Study Set", recommendation.Title);
        Assert.Contains("remainingCount=3", recommendation.Body);
    }

    [Fact]
    public async Task RetrieveAsync_UsesDailyActivitiesForGoalRemaining()
    {
        await using var context = CreateContext(nameof(RetrieveAsync_UsesDailyActivitiesForGoalRemaining));
        await SeedUsersAsync(context, primaryGoal: 5);
        await SeedOwnedSetAsync(context, cardCount: 2);

        context.UserDailyActivities.Add(new UserDailyActivity
        {
            Id = 301,
            UserId = 1,
            ActivityDate = DateTime.UtcNow.Date,
            CardsReviewed = 3
        });

        await context.SaveChangesAsync();

        var retriever = new StudyRecommendationRetriever(context);
        var snippets = await retriever.RetrieveAsync(1, "toi nen hoc gi tiep", CancellationToken.None);

        var recommendation = Assert.Single(snippets);
        Assert.Contains("goalRemaining=2", recommendation.Body);
    }

    private static DbflashcardContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new DbflashcardContext(options);
    }

    private static async Task SeedUsersAsync(DbflashcardContext context, int primaryGoal = 0)
    {
        context.Users.AddRange(
            new User { UserId = 1, Email = "owner@test.local", PasswordHash = "hash", Role = Roles.Standard, Goal = primaryGoal },
            new User { UserId = 2, Email = "outsider@test.local", PasswordHash = "hash", Role = Roles.Standard });

        await context.SaveChangesAsync();
    }

    private static async Task SeedOwnedSetAsync(DbflashcardContext context, int cardCount = 4)
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
