using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Listening catalogue direction B, K1 (plan 2026-11-20-listening-catalog-b, sections 2.2, 2.3 and 3): the level bands of
/// the pills, the hero mode / lesson / hint computed by <see cref="IListeningService"/>, and the hint text that mirrors
/// listening-core.js heroHint.
/// </summary>
public sealed class ListeningCatalogBLogicTests
{
    private const int StudentId = 9501;
    private const int A1FullId = 95001;   // A1: transcript + quiz + vocab, the first lesson of the lowest level
    private const int A1PlainId = 95002;  // A1: transcript only
    private const int A2Id = 95003;       // A2: transcript + quiz
    private const int B1Id = 95004;       // B1: transcript + vocab
    private const int C2Id = 95005;       // C2: transcript + quiz + vocab

    // ------------------------------------------------------------------ bands

    [Theory]
    [InlineData("A1", "A1–A2")]
    [InlineData("A2", "A1–A2")]
    [InlineData("B1", "B1")]
    [InlineData("B2", "B2")]
    [InlineData("C1", "C1–C2")]
    [InlineData("C2", "C1–C2")]
    [InlineData("c2", "C1–C2")]
    [InlineData("Z9", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Bands_Of_MapsEveryLevelToItsPill(string? level, string expected)
        => Assert.Equal(expected, ListeningBands.Of(level));

    [Fact]
    public void Bands_AreFourInOrder_WithTheLevelTheUrlKeeps()
    {
        Assert.Equal(new[] { "A1–A2", "B1", "B2", "C1–C2" }, ListeningBands.All.Select(b => b.Key));
        Assert.Equal(new[] { "A1", "B1", "B2", "C1" }, ListeningBands.All.Select(b => b.Levels[0]));
    }

    [Fact]
    public void Index_Bands_HideEmptyBands_AndSumTheLevelsOfABand()
    {
        var vm = new ListeningIndexViewModel
        {
            Levels = new List<ListeningLevelMetaViewModel>
            {
                new() { Key = "A1", LessonCount = 0, CompletedCount = 0 },
                new() { Key = "A2", LessonCount = 1, CompletedCount = 1 },
                new() { Key = "B1", LessonCount = 10, CompletedCount = 2 },
                new() { Key = "B2", LessonCount = 0, CompletedCount = 0 },
                new() { Key = "C1", LessonCount = 3, CompletedCount = 0 },
                new() { Key = "C2", LessonCount = 2, CompletedCount = 1 },
            },
        };

        var bands = ListeningBands.WithLessons(vm.Levels);
        Assert.Equal(new[] { "A1–A2", "B1", "C1–C2" }, bands.Select(b => b.Key));
        Assert.Equal(new[] { 1, 10, 5 }, bands.Select(b => b.LessonCount));
        Assert.Equal(new[] { 1, 2, 1 }, bands.Select(b => b.CompletedCount));
        Assert.Empty(ListeningBands.WithLessons(new List<ListeningLevelMetaViewModel>()));
    }

    [Fact]
    public void Index_SelectedBand_FollowsTheLevelOfTheDeepLink()
    {
        Assert.Equal("C1–C2", new ListeningIndexViewModel { SelectedLevel = "C2" }.SelectedBand);
        Assert.Equal("A1–A2", new ListeningIndexViewModel { SelectedLevel = "A2" }.SelectedBand);
        Assert.Equal("all", new ListeningIndexViewModel { SelectedLevel = null }.SelectedBand);
        Assert.Equal("all", new ListeningIndexViewModel { SelectedLevel = "all" }.SelectedBand);
    }

    // ------------------------------------------------------------------ hero hint (mirrors listening-core.js heroHint)

    [Theory]
    [InlineData(true, false, false, "Còn quiz và từ vựng")]
    [InlineData(true, false, true, "Còn quiz")]
    [InlineData(true, true, false, "Còn từ vựng")]
    [InlineData(false, false, false, "Còn nghe hết lời thoại, quiz và từ vựng")]
    [InlineData(false, true, true, "Còn nghe hết lời thoại")]
    [InlineData(true, true, true, "")]
    public void HeroHint_NamesTheUnfinishedStepsThatHaveContent(bool transcript, bool quiz, bool vocab, string expected)
    {
        var card = new ListeningLessonCardViewModel
        {
            TranscriptLineCount = 3, QuizQuestionCount = 2, VocabItemCount = 4,
            TranscriptCompleted = transcript, QuizCompleted = quiz, VocabReviewed = vocab,
        };
        Assert.Equal(expected, ListeningHeroHint.ForContinue(card));
    }

    [Fact]
    public void HeroHint_OnlyNamesStepsTheLessonHas()
    {
        var transcriptAndVocab = new ListeningLessonCardViewModel { TranscriptLineCount = 3, VocabItemCount = 4, TranscriptCompleted = true };
        Assert.Equal("Còn từ vựng", ListeningHeroHint.ForContinue(transcriptAndVocab));

        var transcriptOnly = new ListeningLessonCardViewModel { TranscriptLineCount = 2 };
        Assert.Equal("Còn nghe hết lời thoại", ListeningHeroHint.ForContinue(transcriptOnly));

        Assert.Equal(string.Empty, ListeningHeroHint.ForContinue(new ListeningLessonCardViewModel()));
    }

    // ------------------------------------------------------------------ hero mode from the service

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.Users.Add(new User
        {
            UserId = StudentId, Email = "catalog-b@test.local", PasswordHash = "hash", FullName = "Catalog B",
            Role = Roles.Standard, Status = UserStatus.Online, CreatedAt = DateTime.UtcNow,
        });
        db.ListeningLessons.AddRange(
            Lesson(A1FullId, "Ordering coffee", "A1", "Daily", lines: 3, questions: 2, vocab: 2),
            Lesson(A1PlainId, "At the airport", "A1", "Travel", lines: 2, questions: 0, vocab: 0),
            Lesson(A2Id, "Doctor visit", "A2", "Daily", lines: 2, questions: 1, vocab: 0),
            Lesson(B1Id, "Job talk", "B1", "Work", lines: 2, questions: 0, vocab: 2),
            Lesson(C2Id, "Idioms", "C2", "Daily", lines: 2, questions: 1, vocab: 1));
        await db.SaveChangesAsync();
        return factory;
    }

    private static ListeningLesson Lesson(int id, string title, string level, string topic, int lines, int questions, int vocab)
    {
        var lesson = new ListeningLesson
        {
            Id = id, Title = title, Level = level, Topic = topic, YoutubeId = "dQw4w9WgXcQ", Duration = "01:20",
            IsPublished = true, CreatedAt = DateTime.UtcNow.AddMinutes(id % 1000),
        };
        for (var i = 0; i < lines; i++)
            lesson.TranscriptLines.Add(new ListeningTranscriptLine { OrderIndex = i + 1, Speaker = "A", Text = $"Line {i + 1}", StartTime = i * 4, EndTime = i * 4 + 3 });
        for (var i = 0; i < questions; i++)
            lesson.QuizQuestions.Add(new ListeningQuizQuestion { OrderIndex = i + 1, QuestionText = $"Q{i + 1}", OptionA = "a", OptionB = "b", OptionC = "c", OptionD = "d", CorrectAnswer = "A" });
        for (var i = 0; i < vocab; i++)
            lesson.VocabItems.Add(new ListeningVocabItem { OrderIndex = i + 1, Word = $"word{i + 1}", Definition = "meaning" });
        return lesson;
    }

    private static async Task<ListeningIndexViewModel> IndexAsync(TestWebApplicationFactory factory, int? userId)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IListeningService>();
        return await service.GetIndexViewModelAsync(null, null, userId);
    }

    [Fact]
    public async Task Hero_Guest_OffersTheFirstLessonOfTheLowestLevel_WithTheLoginHint()
    {
        await using var factory = await CreateFactoryAsync();
        var vm = await IndexAsync(factory, null);

        Assert.Equal("guest", vm.HeroMode);
        Assert.Equal(A1FullId, vm.HeroLesson!.Id);
        Assert.Equal("Đăng nhập để lưu tiến độ", vm.HeroHint);
    }

    [Fact]
    public async Task Hero_SignedInWithoutProgress_StartsTheFirstUnfinishedLesson_AndCountsWhatIsLeft()
    {
        await using var factory = await CreateFactoryAsync();
        var vm = await IndexAsync(factory, StudentId);

        Assert.Equal("start", vm.HeroMode);
        Assert.Equal(A1FullId, vm.HeroLesson!.Id);
        Assert.Equal("5 bài đang chờ bạn", vm.HeroHint);
    }

    [Fact]
    public async Task Hero_Start_SkipsFinishedLessons_AndTheHintCountsOnlyUnfinishedOnes()
    {
        await using var factory = await CreateFactoryAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.UserListeningProgresses.Add(new UserListeningProgress
            {
                UserId = StudentId, LessonId = A1FullId, TranscriptCompleted = true, QuizCompleted = true, VocabReviewed = true,
                CompletedAt = DateTime.UtcNow.AddDays(-2), LastAccessedAt = DateTime.UtcNow.AddDays(-2),
            });
            await db.SaveChangesAsync();
        }

        var vm = await IndexAsync(factory, StudentId);
        Assert.Equal("start", vm.HeroMode);
        Assert.Equal(A1PlainId, vm.HeroLesson!.Id);
        Assert.Equal("4 bài đang chờ bạn", vm.HeroHint);
    }

    [Fact]
    public async Task Hero_Continue_UsesTheLessonOpenedMostRecently_AndNamesTheMissingSteps()
    {
        await using var factory = await CreateFactoryAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.UserListeningProgresses.AddRange(
                new UserListeningProgress { UserId = StudentId, LessonId = A2Id, TranscriptCompleted = true, QuizCompleted = true, CompletedAt = DateTime.UtcNow.AddDays(-1), LastAccessedAt = DateTime.UtcNow.AddDays(-1) },
                new UserListeningProgress { UserId = StudentId, LessonId = A1FullId, TranscriptCompleted = true, LastAccessedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var vm = await IndexAsync(factory, StudentId);
        Assert.Equal("continue", vm.HeroMode);
        Assert.Equal(A1FullId, vm.HeroLesson!.Id);
        Assert.Equal("Còn quiz và từ vựng", vm.HeroHint);
    }

    [Fact]
    public async Task Hero_EveryPublicLessonFinished_ReviewsTheMostRecentlyFinishedOne()
    {
        await using var factory = await CreateFactoryAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var when = DateTime.UtcNow.AddDays(-10);
            foreach (var id in new[] { A1FullId, A1PlainId, A2Id, B1Id, C2Id })
            {
                db.UserListeningProgresses.Add(new UserListeningProgress
                {
                    UserId = StudentId, LessonId = id, TranscriptCompleted = true, QuizCompleted = true, VocabReviewed = true,
                    CompletedAt = when, LastAccessedAt = when,
                });
                when = when.AddDays(1);
            }
            await db.SaveChangesAsync();
        }

        var vm = await IndexAsync(factory, StudentId);
        Assert.Equal("review", vm.HeroMode);
        Assert.Equal(C2Id, vm.HeroLesson!.Id); // finished last
        Assert.Equal("Bạn đã nghe hết các bài hiện có", vm.HeroHint);
    }

    [Fact]
    public async Task Hero_NoPublicLessons_HasNoHero()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var guest = await IndexAsync(factory, null);
        Assert.Equal("none", guest.HeroMode);
        Assert.Null(guest.HeroLesson);
        Assert.Equal(string.Empty, guest.HeroHint);
    }
}
