using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using TCTEnglish.Models.Reading;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.Reading;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class ReadingAssistServiceTests
{
    [Fact]
    public async Task Assist_UsesPassageVocabulary_WithoutCallingAi()
    {
        await using var db = CreateContext(nameof(Assist_UsesPassageVocabulary_WithoutCallingAi));
        var (userId, passageId) = await SeedPublishedPassageAsync(db, withVocab: true);
        var learning = new ReadingLearningService(db, new Mock<IGoalsService>().Object);
        var ai = new Mock<IAiProviderClient>(MockBehavior.Strict);
        var assist = CreateAssist(learning, ai.Object, db);

        var result = await assist.AssistAsync(userId, new ReadingAssistRequest
        {
            PassageId = passageId,
            SelectedText = "oxygen",
            Mode = ReadingAssistModes.Dictionary
        });

        Assert.True(result.Success);
        Assert.Equal(ReadingAssistSources.PassageVocab, result.Source);
        Assert.Equal("khí oxy", result.VietnameseMeaning);
        ai.Verify(a => a.GenerateReplyAsync(
            It.IsAny<int>(),
            It.IsAny<IReadOnlyList<AiContextMessage>>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<AiProviderRequestOptions>()), Times.Never);
    }

    [Fact]
    public async Task Assist_CallsAi_WhenWordMissingFromPassage()
    {
        await using var db = CreateContext(nameof(Assist_CallsAi_WhenWordMissingFromPassage));
        var (userId, passageId) = await SeedPublishedPassageAsync(db, withVocab: false);
        var learning = new ReadingLearningService(db, new Mock<IGoalsService>().Object);
        var ai = new Mock<IAiProviderClient>();
        ai.Setup(a => a.GenerateReplyAsync(
                userId,
                It.IsAny<IReadOnlyList<AiContextMessage>>(),
                It.IsAny<CancellationToken>(),
                It.Is<AiProviderRequestOptions>(o => o.ConstrainedOutput != null)))
            .ReturnsAsync(new AiProviderReply(
                """{"lemma":"forest","translation":"rừng","partOfSpeech":"noun","ipa":null,"englishDefinition":"a large area of trees","vietnameseMeaning":"rừng","example":"The forest is quiet."}""",
                10, 20, 30, "gpt-6-luna", "test"));

        var assist = CreateAssist(learning, ai.Object, db);
        var result = await assist.AssistAsync(userId, new ReadingAssistRequest
        {
            PassageId = passageId,
            SelectedText = "forest",
            Mode = ReadingAssistModes.Translate,
            SurroundingSentence = "The forest is quiet."
        });

        Assert.True(result.Success);
        Assert.Equal(ReadingAssistSources.Ai, result.Source);
        Assert.Equal("rừng", result.Translation);
        Assert.Equal("forest", result.Lemma);
        ai.VerifyAll();
    }

    [Fact]
    public async Task Assist_MissingApiKey_DoesNotCallProvider()
    {
        await using var db = CreateContext(nameof(Assist_MissingApiKey_DoesNotCallProvider));
        var (userId, passageId) = await SeedPublishedPassageAsync(db, withVocab: false);
        var learning = new ReadingLearningService(db, new Mock<IGoalsService>().Object);
        var ai = new Mock<IAiProviderClient>(MockBehavior.Strict);
        var assist = CreateAssist(learning, ai.Object, db, apiKey: "");

        var result = await assist.AssistAsync(userId, new ReadingAssistRequest
        {
            PassageId = passageId,
            SelectedText = "happy",
            Mode = ReadingAssistModes.Translate
        });

        Assert.False(result.Success);
        Assert.Equal("not_configured", result.ErrorCode);
        Assert.Contains("từ vựng có sẵn", result.ErrorMessage ?? "", StringComparison.Ordinal);
        Assert.DoesNotContain(".ps1", result.ErrorMessage ?? "", StringComparison.Ordinal);
        ai.Verify(a => a.GenerateReplyAsync(
            It.IsAny<int>(),
            It.IsAny<IReadOnlyList<AiContextMessage>>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<AiProviderRequestOptions>()), Times.Never);
    }

    [Fact]
    public async Task Assist_RejectsOversizedSelection()
    {
        await using var db = CreateContext(nameof(Assist_RejectsOversizedSelection));
        var (userId, passageId) = await SeedPublishedPassageAsync(db, withVocab: false);
        var learning = new ReadingLearningService(db, new Mock<IGoalsService>().Object);
        var assist = CreateAssist(learning, Mock.Of<IAiProviderClient>(), db);

        var result = await assist.AssistAsync(userId, new ReadingAssistRequest
        {
            PassageId = passageId,
            SelectedText = string.Join(' ', Enumerable.Repeat("word", 9)),
            Mode = ReadingAssistModes.Translate
        });

        Assert.False(result.Success);
        Assert.Equal("invalid_selection", result.ErrorCode);
    }

    [Fact]
    public async Task Assist_ReturnsNotFound_WhenPassageInaccessible()
    {
        await using var db = CreateContext(nameof(Assist_ReturnsNotFound_WhenPassageInaccessible));
        db.Users.Add(new User { UserId = 91, Email = "a@test.local", PasswordHash = "x", Role = Roles.Standard, ReadingLevel = "A1" });
        await db.SaveChangesAsync();
        var learning = new ReadingLearningService(db, new Mock<IGoalsService>().Object);
        var assist = CreateAssist(learning, Mock.Of<IAiProviderClient>(), db);

        var result = await assist.AssistAsync(91, new ReadingAssistRequest
        {
            PassageId = 99999,
            SelectedText = "hello",
            Mode = ReadingAssistModes.Translate
        });

        Assert.False(result.Success);
        Assert.Equal("not_found", result.ErrorCode);
    }

    [Fact]
    public async Task Assist_MapsProviderFailure_WithoutLeakingDetails()
    {
        await using var db = CreateContext(nameof(Assist_MapsProviderFailure_WithoutLeakingDetails));
        var (userId, passageId) = await SeedPublishedPassageAsync(db, withVocab: false);
        var learning = new ReadingLearningService(db, new Mock<IGoalsService>().Object);
        var ai = new Mock<IAiProviderClient>();
        ai.Setup(a => a.GenerateReplyAsync(
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<AiContextMessage>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<AiProviderRequestOptions>()))
            .ThrowsAsync(new AiProviderException("secret key xyz", AiProviderException.ErrorCodeQuota, false));

        var assist = CreateAssist(learning, ai.Object, db);
        var result = await assist.AssistAsync(userId, new ReadingAssistRequest
        {
            PassageId = passageId,
            SelectedText = "quiet",
            Mode = ReadingAssistModes.Dictionary
        });

        Assert.False(result.Success);
        Assert.Equal("quota", result.ErrorCode);
        Assert.DoesNotContain("secret", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xyz", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveToNotebook_PersistsAiFields_WhenNoPassageVocab()
    {
        await using var db = CreateContext(nameof(SaveToNotebook_PersistsAiFields_WhenNoPassageVocab));
        var (userId, passageId) = await SeedPublishedPassageAsync(db, withVocab: false);
        var learning = new ReadingLearningService(db, new Mock<IGoalsService>().Object);

        var entry = await learning.SaveToNotebookAsync(
            userId,
            passageId,
            null,
            "forest",
            new ReadingNotebookFields
            {
                Ipa = "/ˈfɒr.ɪst/",
                PartOfSpeech = "noun",
                EnglishDefinition = "a large area of trees",
                VietnameseMeaning = "rừng",
                Example = "The forest is quiet."
            });

        Assert.Equal("forest", entry.Word);
        Assert.Equal("rừng", entry.VietnameseMeaning);
        Assert.Equal("noun", entry.PartOfSpeech);
        Assert.Equal("/ˈfɒr.ɪst/", entry.Ipa);
        Assert.Equal("a large area of trees", entry.EnglishDefinition);
    }

    private static async Task<(int UserId, int PassageId)> SeedPublishedPassageAsync(DbflashcardContext db, bool withVocab)
    {
        const int userId = 77;
        db.Users.Add(new User
        {
            UserId = userId,
            Email = $"{userId}@test.local",
            PasswordHash = "x",
            Role = Roles.Standard,
            ReadingLevel = "B1"
        });
        var passage = new ReadingPassage
        {
            Title = "Nature",
            Content = "Trees give oxygen. The forest is quiet.",
            Level = "A1",
            Status = ReadingStatuses.Published,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow,
            Paragraphs =
            {
                new ReadingParagraph { ParagraphOrder = 1, ParagraphContent = "Trees give oxygen." },
                new ReadingParagraph { ParagraphOrder = 2, ParagraphContent = "The forest is quiet." }
            },
            Questions =
            {
                new ReadingQuestion
                {
                    QuestionText = "Q",
                    QuestionType = ReadingQuestionTypes.ShortAnswer,
                    CorrectAnswerText = "oxygen",
                    OrderIndex = 1,
                    Points = 1
                }
            }
        };
        db.ReadingPassages.Add(passage);
        await db.SaveChangesAsync();

        if (withVocab)
        {
            db.ReadingVocabularyItems.Add(new ReadingVocabularyItem
            {
                ReadingPassageId = passage.Id,
                Word = "oxygen",
                VietnameseMeaning = "khí oxy",
                EnglishDefinition = "a gas",
                PartOfSpeech = "noun",
                ParagraphId = passage.Paragraphs.First(p => p.ParagraphOrder == 1).Id
            });
            await db.SaveChangesAsync();
        }

        return (userId, passage.Id);
    }

    private static ReadingAssistService CreateAssist(
        IReadingLearningService learning,
        IAiProviderClient ai,
        DbflashcardContext db,
        string apiKey = "sk-test-local-not-a-real-key")
    {
        var options = Options.Create(new AiOptions
        {
            ApiKey = apiKey,
            Model = "gpt-6-luna",
            ReasoningEffort = "low",
            BaseUrl = "https://api.openai.com/v1"
        });
        return new ReadingAssistService(learning, ai, db, options);
    }

    private static DbflashcardContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new DbflashcardContext(options);
    }
}
