using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Services.Admin;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

public sealed class AdminListeningServiceTests : IAsyncLifetime
{
    private const int Learner = 1, OtherLearner = 2, Admin = 8;
    private const string Id = "dQw4w9WgXcQ";

    private SqliteConnection _connection = null!;
    private DbflashcardContext _db = null!;
    private FakeTranscripts _transcripts = null!;
    private AdminListeningService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        foreach (var (id, role) in new[] { (Learner, "Premium"), (OtherLearner, "Premium"), (Admin, "Admin") })
        {
            _db.Users.Add(new User { UserId = id, Email = $"u{id}@test.local", PasswordHash = "x", FullName = $"User {id}", Role = role, CreatedAt = DateTime.UtcNow });
        }

        await _db.SaveChangesAsync();
        _transcripts = new FakeTranscripts();
        _service = new AdminListeningService(_db, _transcripts, NullLogger<AdminListeningService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ------------------------------------------------------------------ seed helpers

    private async Task<ListeningLesson> AddLessonAsync(string title = "Bài hệ thống", bool published = false, int? owner = null, string level = "B1", string? youtube = null, int lines = 0, int timed = 0, int quiz = 0, int vocab = 0)
    {
        var lesson = new ListeningLesson
        {
            Title = title, Level = level, Topic = "Chủ đề", YoutubeId = youtube, IsPublished = published, OwnerUserId = owner,
            CreatedAt = DateTime.UtcNow,
        };
        for (var i = 0; i < lines; i++)
        {
            lesson.TranscriptLines.Add(new ListeningTranscriptLine
            {
                OrderIndex = i + 1, Speaker = "Speaker 1", Text = $"Dòng {i + 1}",
                StartTime = i < timed ? i * 2.0 : null, EndTime = i < timed ? i * 2.0 + 1.5 : null,
            });
        }

        for (var i = 0; i < quiz; i++)
        {
            lesson.QuizQuestions.Add(new ListeningQuizQuestion { OrderIndex = i + 1, QuestionText = $"Câu {i + 1}?", OptionA = "a", OptionB = "b", CorrectAnswer = "A" });
        }

        for (var i = 0; i < vocab; i++)
        {
            lesson.VocabItems.Add(new ListeningVocabItem { OrderIndex = i + 1, Word = $"w{i}", Definition = "d" });
        }

        _db.ListeningLessons.Add(lesson);
        await _db.SaveChangesAsync();
        return lesson;
    }

    private static AdminLessonInfoInput ValidInfo(string title = "Bài mới") => new()
    {
        Title = title, Level = "B1", Topic = "Du lịch", YoutubeId = Id, Duration = "4:12",
    };

    private static AdminQuizInput ValidQuestion(string text = "Ai nói?") => new()
    {
        QuestionText = text, OptionA = "Anh", OptionB = "Chị", CorrectAnswer = "A",
    };

    private async Task<ListeningLesson> ReloadAsync(int id)
        => await _db.ListeningLessons.AsNoTracking().SingleAsync(l => l.Id == id);

    // ------------------------------------------------------------------ L1 / D2: learner lessons

    [Fact]
    public async Task L1_ALearnersLesson_IsReadOnlyForAnAdmin_ButCanBeUnpublishedAndDeleted()
    {
        var lesson = await AddLessonAsync("Bài riêng", published: true, owner: Learner, youtube: Id, lines: 2, quiz: 1, vocab: 1);

        var info = await _service.SaveInfoAsync(lesson.Id, ValidInfo("Đổi tên"), default);
        var transcript = await _service.SaveTranscriptAsync(lesson.Id, new[] { new AdminTranscriptLineInput { Text = "x" } });
        var quiz = await _service.SaveQuizAsync(lesson.Id, new[] { ValidQuestion() });
        var vocab = await _service.SaveVocabAsync(lesson.Id, new[] { new AdminVocabInput { Word = "w", Definition = "d" } });
        var import = await _service.ImportTranscriptAsync(lesson.Id, true);
        var publishAgain = await _service.SetPublishedAsync(lesson.Id, true);

        foreach (var forbidden in new[] { (AdminResult)info, transcript, quiz, vocab, import, publishAgain })
        {
            Assert.Equal(AdminResultStatus.Forbidden, forbidden.Status);
        }

        Assert.Equal(AdminListeningMessages.LearnerLessonReadOnly, info.Message);
        var saved = await _db.ListeningLessons.AsNoTracking().Include(l => l.TranscriptLines).Include(l => l.QuizQuestions).Include(l => l.VocabItems).SingleAsync(l => l.Id == lesson.Id);
        Assert.Equal("Bài riêng", saved.Title);
        Assert.Equal(2, saved.TranscriptLines.Count);
        Assert.Single(saved.QuizQuestions);
        Assert.Single(saved.VocabItems);
        Assert.Equal(Learner, saved.OwnerUserId);

        var unpublish = await _service.SetPublishedAsync(lesson.Id, false);
        Assert.True(unpublish.IsSuccess);
        Assert.False((await ReloadAsync(lesson.Id)).IsPublished);

        var delete = await _service.DeleteAsync(lesson.Id, "Bài riêng", Admin);
        Assert.True(delete.IsSuccess);
        Assert.False(await _db.ListeningLessons.AnyAsync(l => l.Id == lesson.Id));
    }

    [Fact]
    public async Task L1_TheEditor_ShowsTheOwnerAndTheKindOfLesson()
    {
        var system = await AddLessonAsync("Hệ thống");
        var learner = await AddLessonAsync("Riêng", owner: Learner);

        var systemEditor = await _service.GetEditorAsync(system.Id);
        var learnerEditor = await _service.GetEditorAsync(learner.Id);

        Assert.False(systemEditor!.IsLearnerOwned);
        Assert.Null(systemEditor.OwnerName);
        Assert.True(learnerEditor!.IsLearnerOwned);
        Assert.Equal("User 1", learnerEditor.OwnerName);
        Assert.Null(await _service.GetEditorAsync(9999));
    }

    // ------------------------------------------------------------------ info: L2, L3, L10

    [Fact]
    public async Task SaveInfo_CreatesADraftOwnedByNobody_AndStoresTheYoutubeIdOnly()
    {
        var result = await _service.SaveInfoAsync(null, new AdminLessonInfoInput
        {
            Title = "  Bài   mới ", Level = "B2", Topic = " Du lịch ", YoutubeId = "https://youtu.be/" + Id + "?si=abc", Duration = "4:12",
            Speaker1Name = " Anna ", Speaker1Country = "UK", Speaker2Name = "Ben", Speaker2Country = "US",
            AudioUrl = "https://cdn.example.com/a.mp3",
        });

        Assert.True(result.IsSuccess);
        var lesson = await ReloadAsync(result.Value);
        Assert.Equal("Bài mới", lesson.Title);
        Assert.Equal("Du lịch", lesson.Topic);
        Assert.Equal("B2", lesson.Level);
        Assert.Equal(Id, lesson.YoutubeId);
        Assert.Equal("04:12", lesson.Duration);
        Assert.Equal($"https://img.youtube.com/vi/{Id}/hqdefault.jpg", lesson.ThumbnailUrl);
        Assert.Equal("Anna", lesson.Speaker1Name);
        Assert.Equal("https://cdn.example.com/a.mp3", lesson.AudioUrl);
        Assert.False(lesson.IsPublished);
        Assert.Null(lesson.OwnerUserId);
    }

    [Fact]
    public async Task SaveInfo_KeepsAnExplicitThumbnail_AndAllowsALessonWithoutYoutube()
    {
        var result = await _service.SaveInfoAsync(null, new AdminLessonInfoInput
        {
            Title = "Chỉ có âm thanh", Level = "A1", Topic = "Chào hỏi", AudioUrl = "/media/a.mp3", ThumbnailUrl = "/images/t.png",
        });

        Assert.True(result.IsSuccess);
        var lesson = await ReloadAsync(result.Value);
        Assert.Null(lesson.YoutubeId);
        Assert.Equal("/images/t.png", lesson.ThumbnailUrl);
        Assert.Null(lesson.Duration);
    }

    public static IEnumerable<object[]> InvalidInfos() => new[]
    {
        new object[] { "Title", new AdminLessonInfoInput { Title = "", Level = "B1", Topic = "t" } },
        new object[] { "Title", new AdminLessonInfoInput { Title = new string('a', 256), Level = "B1", Topic = "t" } },
        new object[] { "Topic", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "" } },
        new object[] { "Topic", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = new string('t', 101) } },
        new object[] { "Level", new AdminLessonInfoInput { Title = "a", Level = "D1", Topic = "t" } },
        new object[] { "Level", new AdminLessonInfoInput { Title = "a", Level = "b1", Topic = "t" } },
        new object[] { "Level", new AdminLessonInfoInput { Title = "a", Level = null, Topic = "t" } },
        new object[] { "YoutubeId", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", YoutubeId = "not a video" } },
        new object[] { "YoutubeId", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", YoutubeId = "https://evil.example/watch?v=dQw4w9WgXcQ" } },
        new object[] { "AudioUrl", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", AudioUrl = "http://cdn.example.com/a.mp3" } },
        new object[] { "AudioUrl", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", AudioUrl = "https://x.example/" + new string('u', 500) } },
        new object[] { "ThumbnailUrl", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", ThumbnailUrl = "javascript:alert(1)" } },
        new object[] { "Duration", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", Duration = "4 phút" } },
        new object[] { "Speaker1Name", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", Speaker1Name = new string('s', 151) } },
        new object[] { "Speaker2Name", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", Speaker2Name = new string('s', 151) } },
        new object[] { "Speaker1Country", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", Speaker1Country = new string('c', 101) } },
        new object[] { "Speaker2Country", new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", Speaker2Country = new string('c', 101) } },
    };

    [Theory]
    [MemberData(nameof(InvalidInfos))]
    public async Task L2_L3_L10_SaveInfo_RejectsBadInput_WithAFieldMessage_AndSavesNothing(string field, AdminLessonInfoInput input)
    {
        var result = await _service.SaveInfoAsync(null, input);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.True(result.FieldErrors.ContainsKey(field), $"expected an error on {field}, got {string.Join(",", result.FieldErrors.Keys)}");
        Assert.Equal(0, await _db.ListeningLessons.CountAsync());
    }

    [Fact]
    public async Task L2_L10_SaveInfo_UsesTheVietnameseMessagesOfThePlan()
    {
        var result = await _service.SaveInfoAsync(null, new AdminLessonInfoInput { Title = "a", Level = "B1", Topic = "t", YoutubeId = "xx", Duration = "9 giờ" });

        Assert.Equal(AdminListeningMessages.YoutubeInvalid, result.FieldErrors["YoutubeId"]);
        Assert.Equal(AdminListeningMessages.DurationInvalid, result.FieldErrors["Duration"]);
    }

    [Fact]
    public async Task SaveInfo_OnAnExistingSystemLesson_ChangesTheInfoButNotThePublishStateOrTheContent()
    {
        var lesson = await AddLessonAsync("Cũ", published: true, youtube: Id, lines: 2, quiz: 1);

        var result = await _service.SaveInfoAsync(lesson.Id, new AdminLessonInfoInput { Title = "Mới", Level = "C1", Topic = "Công nghệ", YoutubeId = Id, PublishNow = false });

        Assert.True(result.IsSuccess);
        Assert.Equal(lesson.Id, result.Value);
        var saved = await _db.ListeningLessons.AsNoTracking().Include(l => l.TranscriptLines).Include(l => l.QuizQuestions).SingleAsync(l => l.Id == lesson.Id);
        Assert.Equal("Mới", saved.Title);
        Assert.Equal("C1", saved.Level);
        Assert.True(saved.IsPublished);
        Assert.Equal(2, saved.TranscriptLines.Count);
        Assert.Single(saved.QuizQuestions);
        Assert.Null(saved.OwnerUserId);
    }

    [Fact]
    public async Task SaveInfo_ReportsAMissingLesson()
    {
        var result = await _service.SaveInfoAsync(9999, ValidInfo());
        Assert.Equal(AdminResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SaveInfo_WarnsWhenAnotherSystemLessonUsesTheSameVideo()
    {
        await AddLessonAsync("Đã có", youtube: Id);

        var result = await _service.SaveInfoAsync(null, ValidInfo());

        Assert.True(result.IsSuccess);
        Assert.Contains(AdminListeningMessages.DuplicateYoutubeWarning, result.Warnings);
    }

    [Fact]
    public async Task SaveInfo_DoesNotWarnAboutALearnersLessonOrItself()
    {
        var own = await AddLessonAsync("Chính nó", youtube: Id);
        await AddLessonAsync("Của học viên", owner: Learner, youtube: Id);

        var result = await _service.SaveInfoAsync(own.Id, ValidInfo("Chính nó"));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task L8_APublishNowOnCreate_IsBlocked_BecauseThereIsNoTranscriptYet()
    {
        var input = ValidInfo();
        input.PublishNow = true;

        var result = await _service.SaveInfoAsync(null, input);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal(AdminListeningMessages.NoTranscriptToPublish, result.FieldErrors["PublishNow"]);
        Assert.Equal(0, await _db.ListeningLessons.CountAsync());
    }

    // ------------------------------------------------------------------ publish rules: L8

    [Fact]
    public async Task L8_Publish_IsBlockedWithoutATranscript()
    {
        var lesson = await AddLessonAsync(quiz: 2, vocab: 2);

        var result = await _service.SetPublishedAsync(lesson.Id, true);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal(AdminListeningMessages.NoTranscriptToPublish, result.Message);
        Assert.False((await ReloadAsync(lesson.Id)).IsPublished);
    }

    [Fact]
    public async Task L8_Publish_WorksWithATranscript_AndWarnsAboutMissingParts()
    {
        var lesson = await AddLessonAsync(youtube: Id, lines: 2);

        var result = await _service.SetPublishedAsync(lesson.Id, true);

        Assert.True(result.IsSuccess);
        Assert.True((await ReloadAsync(lesson.Id)).IsPublished);
        Assert.Contains(AdminListeningMessages.NoQuizWarning, result.Warnings);
        Assert.Contains(AdminListeningMessages.NoVocabWarning, result.Warnings);
        Assert.Contains(AdminListeningMessages.NoTimedLinesWarning, result.Warnings);
    }

    [Fact]
    public async Task L8_Publish_HasNoWarningsForACompleteLesson()
    {
        var lesson = await AddLessonAsync(youtube: Id, lines: 3, timed: 3, quiz: 2, vocab: 2);

        var result = await _service.SetPublishedAsync(lesson.Id, true);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task L8_ALessonWithoutYoutube_DoesNotNeedTimedLines()
    {
        var lesson = await AddLessonAsync(lines: 2, quiz: 1, vocab: 1);

        var result = await _service.SetPublishedAsync(lesson.Id, true);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(AdminListeningMessages.NoTimedLinesWarning, result.Warnings);
    }

    [Fact]
    public async Task L8_Unpublish_AlwaysWorks_AndAMissingLessonIsNotFound()
    {
        var lesson = await AddLessonAsync(published: true);

        Assert.True((await _service.SetPublishedAsync(lesson.Id, false)).IsSuccess);
        Assert.False((await ReloadAsync(lesson.Id)).IsPublished);
        Assert.Equal(AdminResultStatus.NotFound, (await _service.SetPublishedAsync(9999, true)).Status);
    }

    // ------------------------------------------------------------------ transcript: L6

    [Fact]
    public async Task L6_SaveTranscript_ReplacesTheLines_InOrder_WithDefaultsAndTrimming()
    {
        var lesson = await AddLessonAsync(lines: 3);

        var result = await _service.SaveTranscriptAsync(lesson.Id, new[]
        {
            new AdminTranscriptLineInput { Speaker = " Anna ", Text = "  Hello   there ", VietnameseMeaning = " Xin chào ", StartTime = 0, EndTime = 1.5 },
            new AdminTranscriptLineInput { Speaker = "", Text = "How are you?", StartTime = 1.5, EndTime = 3 },
            new AdminTranscriptLineInput { Text = "Fine.", StartTime = null, EndTime = null },
        });

        Assert.True(result.IsSuccess);
        var lines = await _db.ListeningTranscriptLines.AsNoTracking().Where(l => l.LessonId == lesson.Id).OrderBy(l => l.OrderIndex).ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, lines.Select(l => l.OrderIndex));
        Assert.Equal("Anna", lines[0].Speaker);
        Assert.Equal("Hello there", lines[0].Text);
        Assert.Equal("Xin chào", lines[0].VietnameseMeaning);
        Assert.Equal("Speaker 1", lines[1].Speaker);
        Assert.Equal("Speaker 1", lines[2].Speaker);
        Assert.Null(lines[2].StartTime);
        Assert.Null(lines[2].EndTime);
    }

    [Fact]
    public async Task L6_SaveTranscript_SkipsFullyEmptyRows_AndNumbersTheRest()
    {
        var lesson = await AddLessonAsync();

        var result = await _service.SaveTranscriptAsync(lesson.Id, new[]
        {
            new AdminTranscriptLineInput { Text = "One" },
            new AdminTranscriptLineInput { Speaker = "  ", Text = "  " },
            new AdminTranscriptLineInput { Text = "Two" },
        });

        Assert.True(result.IsSuccess);
        var lines = await _db.ListeningTranscriptLines.AsNoTracking().Where(l => l.LessonId == lesson.Id).OrderBy(l => l.OrderIndex).ToListAsync();
        Assert.Equal(new[] { "One", "Two" }, lines.Select(l => l.Text));
        Assert.Equal(new[] { 1, 2 }, lines.Select(l => l.OrderIndex));
    }

    [Fact]
    public async Task L6_SaveTranscript_ReportsEveryBadRow_AndChangesNothing()
    {
        var lesson = await AddLessonAsync(lines: 2);

        var result = await _service.SaveTranscriptAsync(lesson.Id, new[]
        {
            new AdminTranscriptLineInput { Text = "OK", StartTime = 0, EndTime = 2 },
            new AdminTranscriptLineInput { Text = "", VietnameseMeaning = "có nghĩa nhưng không có lời" },
            new AdminTranscriptLineInput { Text = "End before start", StartTime = 5, EndTime = 4 },
            new AdminTranscriptLineInput { Text = "Negative", StartTime = -1, EndTime = 1 },
            new AdminTranscriptLineInput { Text = "Only start", StartTime = 7, EndTime = null },
            new AdminTranscriptLineInput { Text = "Only end", StartTime = null, EndTime = 9 },
            new AdminTranscriptLineInput { Text = "Goes back in time", StartTime = 1, EndTime = 2 },
            new AdminTranscriptLineInput { Text = new string('t', 1001), StartTime = 20, EndTime = 21 },
            new AdminTranscriptLineInput { Speaker = new string('s', 101), Text = "Long speaker", StartTime = 22, EndTime = 23 },
        });

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.True(result.FieldErrors.ContainsKey("Lines[1].Text"), "row 2 has meaning but no text");
        Assert.Equal("Dòng 3: thời điểm kết thúc phải sau thời điểm bắt đầu.", result.FieldErrors["Lines[2].EndTime"]);
        Assert.True(result.FieldErrors.ContainsKey("Lines[3].StartTime"), "negative start");
        Assert.True(result.FieldErrors.ContainsKey("Lines[4].EndTime"), "start without end");
        Assert.True(result.FieldErrors.ContainsKey("Lines[5].StartTime"), "end without start");
        Assert.True(result.FieldErrors.ContainsKey("Lines[6].StartTime"), "start earlier than the previous line");
        Assert.True(result.FieldErrors.ContainsKey("Lines[7].Text"), "text over the limit");
        Assert.True(result.FieldErrors.ContainsKey("Lines[8].Speaker"), "speaker over the limit");
        Assert.False(result.FieldErrors.ContainsKey("Lines[0].Text"));
        Assert.Equal(2, await _db.ListeningTranscriptLines.CountAsync(l => l.LessonId == lesson.Id));
        Assert.All(await _db.ListeningTranscriptLines.AsNoTracking().Where(l => l.LessonId == lesson.Id).ToListAsync(), l => Assert.StartsWith("Dòng ", l.Text));
    }

    [Fact]
    public async Task L6_SaveTranscript_AcceptsEqualStartTimesAndTheLimits()
    {
        var lesson = await AddLessonAsync();

        var result = await _service.SaveTranscriptAsync(lesson.Id, new[]
        {
            new AdminTranscriptLineInput { Text = new string('a', 1000), Speaker = new string('s', 100), VietnameseMeaning = new string('v', 1000), StartTime = 0, EndTime = 0.5 },
            new AdminTranscriptLineInput { Text = "Same start", StartTime = 0, EndTime = 3 },
        });

        Assert.True(result.IsSuccess, string.Join(";", result.FieldErrors.Select(kv => kv.Key + "=" + kv.Value)));
    }

    [Fact]
    public async Task L6_APublishedLesson_CannotBeLeftWithoutATranscript()
    {
        var lesson = await AddLessonAsync(published: true, lines: 2);

        var result = await _service.SaveTranscriptAsync(lesson.Id, new[] { new AdminTranscriptLineInput { Text = "  " } });

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal(AdminListeningMessages.PublishedNeedsTranscript, result.Message);
        Assert.Equal(2, await _db.ListeningTranscriptLines.CountAsync(l => l.LessonId == lesson.Id));
    }

    [Fact]
    public async Task L6_ADraft_CanBeEmptied_AndAMissingLessonIsNotFound()
    {
        var lesson = await AddLessonAsync(lines: 2);

        Assert.True((await _service.SaveTranscriptAsync(lesson.Id, Array.Empty<AdminTranscriptLineInput>())).IsSuccess);
        Assert.Equal(0, await _db.ListeningTranscriptLines.CountAsync(l => l.LessonId == lesson.Id));
        Assert.Equal(AdminResultStatus.NotFound, (await _service.SaveTranscriptAsync(9999, new[] { new AdminTranscriptLineInput { Text = "x" } })).Status);
    }

    // ------------------------------------------------------------------ quiz: L4

    [Fact]
    public async Task L4_SaveQuiz_StoresTrimmedQuestions_InOrder_AndKeepsLearnerProgress()
    {
        var lesson = await AddLessonAsync(quiz: 3);
        _db.UserListeningProgresses.Add(new UserListeningProgress { UserId = OtherLearner, LessonId = lesson.Id, QuizScore = 80 });
        await _db.SaveChangesAsync();

        var result = await _service.SaveQuizAsync(lesson.Id, new[]
        {
            new AdminQuizInput { QuestionText = "  Ai   nói? ", OptionA = " Anh ", OptionB = " Chị ", OptionC = " Em ", OptionD = " Bố ", CorrectAnswer = "D", Explanation = " Vì sao " },
            new AdminQuizInput { QuestionText = "", OptionA = "", OptionB = "" },
            ValidQuestion("Câu hai"),
        });

        Assert.True(result.IsSuccess, string.Join(";", result.FieldErrors.Select(kv => kv.Key + "=" + kv.Value)));
        var questions = await _db.ListeningQuizQuestions.AsNoTracking().Where(q => q.LessonId == lesson.Id).OrderBy(q => q.OrderIndex).ToListAsync();
        Assert.Equal(2, questions.Count);
        Assert.Equal(new[] { 1, 2 }, questions.Select(q => q.OrderIndex));
        Assert.Equal("Ai nói?", questions[0].QuestionText);
        Assert.Equal("Anh", questions[0].OptionA);
        Assert.Equal("D", questions[0].CorrectAnswer);
        Assert.Equal("Vì sao", questions[0].Explanation);
        Assert.Null(questions[1].OptionC);
        Assert.Null(questions[1].OptionD);
        Assert.Equal(80, (await _db.UserListeningProgresses.AsNoTracking().SingleAsync(p => p.LessonId == lesson.Id)).QuizScore);
    }

    public static IEnumerable<object[]> InvalidQuestions() => new[]
    {
        new object[] { "Questions[0].OptionA", new AdminQuizInput { QuestionText = "q", OptionA = "", OptionB = "b", CorrectAnswer = "B" } },
        new object[] { "Questions[0].OptionA", new AdminQuizInput { QuestionText = "q", OptionA = null, OptionB = "b", CorrectAnswer = "B" } },
        new object[] { "Questions[0].OptionB", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "  ", CorrectAnswer = "A" } },
        new object[] { "Questions[0].CorrectAnswer", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", CorrectAnswer = "" } },
        new object[] { "Questions[0].CorrectAnswer", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", CorrectAnswer = null } },
        new object[] { "Questions[0].CorrectAnswer", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", CorrectAnswer = "E" } },
        new object[] { "Questions[0].CorrectAnswer", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", CorrectAnswer = "a" } },
        new object[] { "Questions[0].CorrectAnswer", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", OptionC = "", CorrectAnswer = "C" } },
        new object[] { "Questions[0].CorrectAnswer", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", OptionC = "c", OptionD = "", CorrectAnswer = "D" } },
        new object[] { "Questions[0].OptionD", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", OptionC = "", OptionD = "d", CorrectAnswer = "A" } },
        new object[] { "Questions[0].QuestionText", new AdminQuizInput { QuestionText = new string('q', 501), OptionA = "a", OptionB = "b", CorrectAnswer = "A" } },
        new object[] { "Questions[0].OptionA", new AdminQuizInput { QuestionText = "q", OptionA = new string('a', 501), OptionB = "b", CorrectAnswer = "A" } },
        new object[] { "Questions[0].OptionB", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = new string('b', 501), CorrectAnswer = "A" } },
        new object[] { "Questions[0].OptionC", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", OptionC = new string('c', 501), CorrectAnswer = "A" } },
        new object[] { "Questions[0].OptionD", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", OptionC = "c", OptionD = new string('d', 501), CorrectAnswer = "A" } },
        new object[] { "Questions[0].Explanation", new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", CorrectAnswer = "A", Explanation = new string('e', 1001) } },
    };

    [Theory]
    [MemberData(nameof(InvalidQuestions))]
    public async Task L4_SaveQuiz_RejectsAnUnanswerableOrOversizedQuestion_WithoutThrowing_AndKeepsTheOldQuiz(string field, AdminQuizInput question)
    {
        var lesson = await AddLessonAsync(quiz: 2);

        var result = await _service.SaveQuizAsync(lesson.Id, new[] { question });

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.True(result.FieldErrors.ContainsKey(field), $"expected an error on {field}, got {string.Join(",", result.FieldErrors.Keys)}");
        Assert.Equal(2, await _db.ListeningQuizQuestions.CountAsync(q => q.LessonId == lesson.Id));
    }

    [Fact]
    public async Task L4_TheMessageNamesTheQuestionAndTheEmptyOption()
    {
        var lesson = await AddLessonAsync();

        var result = await _service.SaveQuizAsync(lesson.Id, new[]
        {
            ValidQuestion("Một"),
            new AdminQuizInput { QuestionText = "Hai", OptionA = "a", OptionB = "b", OptionC = "", CorrectAnswer = "C" },
        });

        Assert.Equal("Câu 2: đáp án đúng C đang để trống.", result.FieldErrors["Questions[1].CorrectAnswer"]);
    }

    [Fact]
    public async Task L4_SaveQuiz_AcceptsFourOptionsAndTwoOptions_AndAnEmptyQuiz()
    {
        var lesson = await AddLessonAsync(quiz: 2);

        var full = await _service.SaveQuizAsync(lesson.Id, new[]
        {
            new AdminQuizInput { QuestionText = "q", OptionA = "a", OptionB = "b", OptionC = "c", OptionD = "d", CorrectAnswer = "D" },
            new AdminQuizInput { QuestionText = new string('q', 500), OptionA = new string('a', 500), OptionB = "b", CorrectAnswer = "B", Explanation = new string('e', 1000) },
        });
        var empty = await _service.SaveQuizAsync(lesson.Id, Array.Empty<AdminQuizInput>());

        Assert.True(full.IsSuccess, string.Join(";", full.FieldErrors.Select(kv => kv.Key + "=" + kv.Value)));
        Assert.True(empty.IsSuccess);
        Assert.Equal(0, await _db.ListeningQuizQuestions.CountAsync(q => q.LessonId == lesson.Id));
        Assert.Equal(AdminResultStatus.NotFound, (await _service.SaveQuizAsync(9999, new[] { ValidQuestion() })).Status);
    }

    // ------------------------------------------------------------------ vocabulary: L5

    [Fact]
    public async Task L5_SaveVocab_StoresTrimmedItems_InOrder_SkippingEmptyRows()
    {
        var lesson = await AddLessonAsync(vocab: 2);

        var result = await _service.SaveVocabAsync(lesson.Id, new[]
        {
            new AdminVocabInput { Word = "  luggage ", Definition = " hành lý ", ExampleSentence = " My luggage. ", ImageUrl = " https://cdn.example.com/l.png " },
            new AdminVocabInput { Word = " ", Definition = "" },
            new AdminVocabInput { Word = "ticket", Definition = "vé" },
        });

        Assert.True(result.IsSuccess, string.Join(";", result.FieldErrors.Select(kv => kv.Key + "=" + kv.Value)));
        var items = await _db.ListeningVocabItems.AsNoTracking().Where(v => v.LessonId == lesson.Id).OrderBy(v => v.OrderIndex).ToListAsync();
        Assert.Equal(new[] { "luggage", "ticket" }, items.Select(v => v.Word));
        Assert.Equal(new[] { 1, 2 }, items.Select(v => v.OrderIndex));
        Assert.Equal("hành lý", items[0].Definition);
        Assert.Equal("My luggage.", items[0].ExampleSentence);
        Assert.Equal("https://cdn.example.com/l.png", items[0].ImageUrl);
        Assert.Null(items[1].ImageUrl);
    }

    public static IEnumerable<object[]> InvalidVocab() => new[]
    {
        new object[] { "Items[0].Definition", new AdminVocabInput { Word = "w", Definition = "" } },
        new object[] { "Items[0].Definition", new AdminVocabInput { Word = "w", Definition = null } },
        new object[] { "Items[0].Definition", new AdminVocabInput { Word = "w", Definition = new string('d', 1001) } },
        new object[] { "Items[0].Word", new AdminVocabInput { Word = new string('w', 201), Definition = "d" } },
        new object[] { "Items[0].Word", new AdminVocabInput { Word = "", Definition = "chỉ có nghĩa" } },
        new object[] { "Items[0].ExampleSentence", new AdminVocabInput { Word = "w", Definition = "d", ExampleSentence = new string('e', 1001) } },
        new object[] { "Items[0].ImageUrl", new AdminVocabInput { Word = "w", Definition = "d", ImageUrl = "http://cdn.example.com/a.png" } },
        new object[] { "Items[0].ImageUrl", new AdminVocabInput { Word = "w", Definition = "d", ImageUrl = "javascript:alert(1)" } },
    };

    [Theory]
    [MemberData(nameof(InvalidVocab))]
    public async Task L5_SaveVocab_RejectsBadRows_WithoutThrowing_AndKeepsTheOldItems(string field, AdminVocabInput item)
    {
        var lesson = await AddLessonAsync(vocab: 2);

        var result = await _service.SaveVocabAsync(lesson.Id, new[] { item });

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.True(result.FieldErrors.ContainsKey(field), $"expected an error on {field}, got {string.Join(",", result.FieldErrors.Keys)}");
        Assert.Equal(2, await _db.ListeningVocabItems.CountAsync(v => v.LessonId == lesson.Id));
    }

    [Fact]
    public async Task L5_SaveVocab_AcceptsTheLimits_AndAMissingLessonIsNotFound()
    {
        var lesson = await AddLessonAsync();

        var result = await _service.SaveVocabAsync(lesson.Id, new[]
        {
            new AdminVocabInput { Word = new string('w', 200), Definition = new string('d', 1000), ExampleSentence = new string('e', 1000), ImageUrl = "/" + new string('u', 499) },
        });

        Assert.True(result.IsSuccess, string.Join(";", result.FieldErrors.Select(kv => kv.Key + "=" + kv.Value)));
        Assert.Equal(AdminResultStatus.NotFound, (await _service.SaveVocabAsync(9999, new[] { new AdminVocabInput { Word = "w", Definition = "d" } })).Status);
    }

    // ------------------------------------------------------------------ import: L7

    private static List<SpeakingSentence> Sentences(params string[] texts)
        => texts.Select((t, i) => new SpeakingSentence { Text = t, StartTime = i * 2.0, EndTime = i * 2.0 + 1.8 }).ToList();

    [Fact]
    public async Task L7_Import_NeedsAYoutubeId()
    {
        var lesson = await AddLessonAsync(youtube: null);

        var result = await _service.ImportTranscriptAsync(lesson.Id, true);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal(AdminListeningMessages.ImportNeedsYoutube, result.Message);
        Assert.Equal(0, _transcripts.Calls);
    }

    [Fact]
    public async Task L7_Import_AsksForConfirmationBeforeReplacingExistingLines()
    {
        var lesson = await AddLessonAsync(youtube: Id, lines: 4);
        _transcripts.Sentences = Sentences("New one", "New two", "New three");

        var refused = await _service.ImportTranscriptAsync(lesson.Id, replaceExisting: false);

        Assert.Equal(AdminResultStatus.Invalid, refused.Status);
        Assert.Equal(AdminListeningMessages.ImportReplaceNeeded(4), refused.Message);
        Assert.Equal(0, _transcripts.Calls);
        Assert.Equal(4, await _db.ListeningTranscriptLines.CountAsync(l => l.LessonId == lesson.Id));

        var accepted = await _service.ImportTranscriptAsync(lesson.Id, replaceExisting: true);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(3, accepted.Value);
        var lines = await _db.ListeningTranscriptLines.AsNoTracking().Where(l => l.LessonId == lesson.Id).OrderBy(l => l.OrderIndex).ToListAsync();
        Assert.Equal(new[] { "New one", "New two", "New three" }, lines.Select(l => l.Text));
        Assert.Equal(new[] { 1, 2, 3 }, lines.Select(l => l.OrderIndex));
        Assert.Equal(2.0, lines[1].StartTime);
    }

    [Fact]
    public async Task L7_Import_OnALessonWithoutLines_DoesNotNeedConfirmation_AndFillsTheDurationAndSpeakers()
    {
        var lesson = await AddLessonAsync(youtube: Id);
        lesson.Speaker1Name = "Anna";
        lesson.Speaker2Name = "Ben";
        await _db.SaveChangesAsync();
        _transcripts.Sentences = Sentences("Hello there", "Ben: Hi Anna", "[Carla]: Welcome");
        _transcripts.Duration = "3:07";

        var result = await _service.ImportTranscriptAsync(lesson.Id, replaceExisting: false);

        Assert.True(result.IsSuccess);
        var lines = await _db.ListeningTranscriptLines.AsNoTracking().Where(l => l.LessonId == lesson.Id).OrderBy(l => l.OrderIndex).ToListAsync();
        Assert.Equal("Anna", lines[0].Speaker);
        Assert.Equal("Ben", lines[1].Speaker);
        Assert.Equal("Hi Anna", lines[1].Text);
        Assert.Equal("Carla", lines[2].Speaker);
        Assert.Equal("Welcome", lines[2].Text);
        Assert.Equal("03:07", (await ReloadAsync(lesson.Id)).Duration);
    }

    [Fact]
    public async Task L7_Import_KeepsAnExistingDuration()
    {
        var lesson = await AddLessonAsync(youtube: Id);
        lesson.Duration = "05:00";
        await _db.SaveChangesAsync();
        _transcripts.Sentences = Sentences("a", "b", "c");
        _transcripts.Duration = "3:07";

        await _service.ImportTranscriptAsync(lesson.Id, false);

        Assert.Equal("05:00", (await ReloadAsync(lesson.Id)).Duration);
    }

    [Fact]
    public async Task L7_Import_ShowsAFriendlyMessageWhenTheProviderFails_AndKeepsTheLines()
    {
        var lesson = await AddLessonAsync(youtube: Id, lines: 2);
        _transcripts.Failure = new HttpRequestException("secret provider detail: 503 from upstream");

        var result = await _service.ImportTranscriptAsync(lesson.Id, true);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal(AdminListeningMessages.ImportFailed, result.Message);
        Assert.DoesNotContain("secret provider detail", result.Message);
        Assert.Equal(2, await _db.ListeningTranscriptLines.CountAsync(l => l.LessonId == lesson.Id));
    }

    [Fact]
    public async Task L7_Import_ReportsAnEmptyAnswer_AndKeepsTheLines()
    {
        var lesson = await AddLessonAsync(youtube: Id, lines: 2);
        _transcripts.Sentences = new List<SpeakingSentence>();

        var result = await _service.ImportTranscriptAsync(lesson.Id, true);

        Assert.Equal(AdminResultStatus.Invalid, result.Status);
        Assert.Equal(AdminListeningMessages.ImportEmpty, result.Message);
        Assert.Equal(2, await _db.ListeningTranscriptLines.CountAsync(l => l.LessonId == lesson.Id));
    }

    [Fact]
    public async Task L7_Import_AllowsOnlyOneImportPerLessonAtATime()
    {
        var lesson = await AddLessonAsync(youtube: Id);
        _transcripts.Sentences = Sentences("a", "b", "c");
        var gate = new TaskCompletionSource();
        _transcripts.Gate = gate.Task;

        var first = _service.ImportTranscriptAsync(lesson.Id, false);
        await _transcripts.Started.Task;

        var second = await _service.ImportTranscriptAsync(lesson.Id, false);
        Assert.Equal(AdminResultStatus.Invalid, second.Status);
        Assert.Equal(AdminListeningMessages.ImportBusy, second.Message);

        gate.SetResult();
        var firstResult = await first;
        Assert.True(firstResult.IsSuccess);
        Assert.Equal(1, _transcripts.Calls);

        // The guard is released afterwards.
        var third = await _service.ImportTranscriptAsync(lesson.Id, true);
        Assert.True(third.IsSuccess);
    }

    [Fact]
    public async Task L7_Import_ReleasesTheGuardAfterAFailure()
    {
        var lesson = await AddLessonAsync(youtube: Id);
        _transcripts.Failure = new InvalidOperationException("boom");
        await _service.ImportTranscriptAsync(lesson.Id, false);

        _transcripts.Failure = null;
        _transcripts.Sentences = Sentences("a", "b", "c");
        var retry = await _service.ImportTranscriptAsync(lesson.Id, false);

        Assert.True(retry.IsSuccess);
    }

    [Fact]
    public async Task L7_Import_ReportsAMissingLesson()
    {
        Assert.Equal(AdminResultStatus.NotFound, (await _service.ImportTranscriptAsync(9999, true)).Status);
    }

    // ------------------------------------------------------------------ delete: L9

    [Fact]
    public async Task L9_Delete_NeedsTheExactTitle_AndRemovesTheContentAndEveryonesProgress()
    {
        var lesson = await AddLessonAsync("Bài cần xóa", lines: 2, quiz: 2, vocab: 2);
        _db.UserListeningProgresses.Add(new UserListeningProgress { UserId = Learner, LessonId = lesson.Id, QuizScore = 50 });
        _db.UserListeningProgresses.Add(new UserListeningProgress { UserId = OtherLearner, LessonId = lesson.Id, QuizScore = 70 });
        await _db.SaveChangesAsync();
        var editor = await _service.GetEditorAsync(lesson.Id);
        Assert.Equal(2, editor!.LearnerProgressCount);

        foreach (var typed in new string?[] { null, "", "bài cần xóa", "Bài cần" })
        {
            var refused = await _service.DeleteAsync(lesson.Id, typed, Admin);
            Assert.Equal(AdminResultStatus.Invalid, refused.Status);
            Assert.Equal(AdminListeningMessages.ConfirmTitleMismatch, refused.Message);
        }

        Assert.True(await _db.ListeningLessons.AnyAsync(l => l.Id == lesson.Id));

        var result = await _service.DeleteAsync(lesson.Id, "  Bài cần xóa ", Admin);

        Assert.True(result.IsSuccess);
        Assert.False(await _db.ListeningLessons.AnyAsync(l => l.Id == lesson.Id));
        Assert.False(await _db.ListeningTranscriptLines.AnyAsync(l => l.LessonId == lesson.Id));
        Assert.False(await _db.ListeningQuizQuestions.AnyAsync(q => q.LessonId == lesson.Id));
        Assert.False(await _db.ListeningVocabItems.AnyAsync(v => v.LessonId == lesson.Id));
        Assert.False(await _db.UserListeningProgresses.AnyAsync(p => p.LessonId == lesson.Id));
    }

    [Fact]
    public async Task L9_Delete_LeavesOtherLessonsAlone_AndAMissingLessonIsNotFound()
    {
        var keep = await AddLessonAsync("Giữ lại", lines: 1, quiz: 1);
        var drop = await AddLessonAsync("Xóa đi", lines: 1);

        Assert.True((await _service.DeleteAsync(drop.Id, "Xóa đi", Admin)).IsSuccess);

        Assert.True(await _db.ListeningLessons.AnyAsync(l => l.Id == keep.Id));
        Assert.Equal(1, await _db.ListeningTranscriptLines.CountAsync(l => l.LessonId == keep.Id));
        Assert.Equal(AdminResultStatus.NotFound, (await _service.DeleteAsync(9999, "x", Admin)).Status);
    }

    // ------------------------------------------------------------------ list, figures, editor: S5 / L1 / L8

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task S5_ListLessons_ClampsAPageBelowOne(int page)
    {
        await AddLessonAsync();

        var result = await _service.ListLessonsAsync(new AdminLessonQuery(Page: page));

        Assert.Equal(1, result.Page);
        Assert.Single(result.Rows);
    }

    [Fact]
    public async Task S5_ListLessons_ShowsTheLastPage_WhenBeyondTheEnd()
    {
        for (var i = 0; i < 45; i++)
        {
            await AddLessonAsync($"Bài {i:00}");
        }

        var result = await _service.ListLessonsAsync(new AdminLessonQuery(Page: 99));

        Assert.Equal(45, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(3, result.Page);
        Assert.Equal(5, result.Rows.Count);
    }

    [Fact]
    public async Task L1_ListLessons_ShowsSystemLessonsByDefault_AndLearnerLessonsOnRequest()
    {
        await AddLessonAsync("Hệ thống 1");
        await AddLessonAsync("Hệ thống 2", published: true);
        await AddLessonAsync("Của học viên", owner: Learner);

        var system = await _service.ListLessonsAsync(new AdminLessonQuery());
        var learner = await _service.ListLessonsAsync(new AdminLessonQuery(Source: "learner"));
        var all = await _service.ListLessonsAsync(new AdminLessonQuery(Source: "all"));
        var unknown = await _service.ListLessonsAsync(new AdminLessonQuery(Source: "whatever"));

        Assert.Equal(2, system.TotalItems);
        Assert.Equal("system", system.Source);
        Assert.All(system.Rows, r => Assert.False(r.IsLearnerOwned));
        var learnerRow = Assert.Single(learner.Rows);
        Assert.True(learnerRow.IsLearnerOwned);
        Assert.Equal("User 1", learnerRow.OwnerName);
        Assert.Equal(3, all.TotalItems);
        Assert.Equal("system", unknown.Source);
        Assert.Equal(2, unknown.TotalItems);
    }

    [Fact]
    public async Task ListLessons_FiltersByLevelStatusAndText()
    {
        await AddLessonAsync("Du lịch Paris", level: "A2", published: true, youtube: Id, lines: 2, timed: 2, quiz: 1, vocab: 1);
        await AddLessonAsync("Công nghệ AI", level: "C1", published: false, lines: 1);
        await AddLessonAsync("Không có lời", level: "A2", published: true);

        var level = await _service.ListLessonsAsync(new AdminLessonQuery(Level: "A2"));
        var allLevels = await _service.ListLessonsAsync(new AdminLessonQuery(Level: "All"));
        var published = await _service.ListLessonsAsync(new AdminLessonQuery(Status: "published"));
        var draft = await _service.ListLessonsAsync(new AdminLessonQuery(Status: "draft"));
        var byTitle = await _service.ListLessonsAsync(new AdminLessonQuery(Q: "Paris"));
        var byTopic = await _service.ListLessonsAsync(new AdminLessonQuery(Q: "Chủ đề"));
        var byYoutube = await _service.ListLessonsAsync(new AdminLessonQuery(Q: Id));

        Assert.Equal(2, level.TotalItems);
        Assert.Equal(3, allLevels.TotalItems);
        Assert.Equal(2, published.TotalItems);
        Assert.Equal("Công nghệ AI", Assert.Single(draft.Rows).Title);
        Assert.Equal("Du lịch Paris", Assert.Single(byTitle.Rows).Title);
        Assert.Equal(3, byTopic.TotalItems);
        Assert.Equal("Du lịch Paris", Assert.Single(byYoutube.Rows).Title);
    }

    [Fact]
    public async Task L8_ListLessons_IncompleteMeansNoTranscriptOrAnUnanswerableQuestion()
    {
        await AddLessonAsync("Đầy đủ", published: true, lines: 2, quiz: 1);
        await AddLessonAsync("Thiếu lời", published: true, quiz: 1);
        var badQuiz = await AddLessonAsync("Quiz hỏng", published: false, lines: 2);
        _db.ListeningQuizQuestions.Add(new ListeningQuizQuestion { LessonId = badQuiz.Id, OrderIndex = 1, QuestionText = "q", OptionA = "a", OptionB = "b", OptionC = "", CorrectAnswer = "C" });
        var badAnswer = await AddLessonAsync("Đáp án lạ", published: false, lines: 2);
        _db.ListeningQuizQuestions.Add(new ListeningQuizQuestion { LessonId = badAnswer.Id, OrderIndex = 1, QuestionText = "q", OptionA = "a", OptionB = "b", CorrectAnswer = "Z" });
        await _db.SaveChangesAsync();

        var incomplete = await _service.ListLessonsAsync(new AdminLessonQuery(Status: "incomplete"));
        var all = await _service.ListLessonsAsync(new AdminLessonQuery());

        Assert.Equal(new[] { "Thiếu lời", "Quiz hỏng", "Đáp án lạ" }.OrderBy(x => x), incomplete.Rows.Select(r => r.Title).OrderBy(x => x));
        Assert.True(all.Rows.Single(r => r.Title == "Thiếu lời").IsIncomplete);
        Assert.True(all.Rows.Single(r => r.Title == "Quiz hỏng").HasInvalidQuiz);
        Assert.False(all.Rows.Single(r => r.Title == "Đầy đủ").IsIncomplete);
    }

    [Fact]
    public async Task ListLessons_CountsTranscriptTimedQuizAndVocab()
    {
        await AddLessonAsync("Có mốc", lines: 4, timed: 3, quiz: 2, vocab: 5);

        var row = Assert.Single((await _service.ListLessonsAsync(new AdminLessonQuery())).Rows);

        Assert.Equal(4, row.TranscriptCount);
        Assert.Equal(3, row.TimedLineCount);
        Assert.Equal(2, row.QuizCount);
        Assert.Equal(5, row.VocabCount);
    }

    [Fact]
    public async Task GetFigures_CountsSystemPublishedIncompleteAndLearnerLessons()
    {
        await AddLessonAsync("A", published: true, lines: 2);
        await AddLessonAsync("B", published: true);                 // published but no transcript: incomplete
        await AddLessonAsync("C", published: false, lines: 1);      // draft
        await AddLessonAsync("D", owner: Learner, published: true, lines: 1);
        await AddLessonAsync("E", owner: OtherLearner);

        var figures = await _service.GetFiguresAsync();

        Assert.Equal(3, figures.SystemLessons);
        Assert.Equal(2, figures.Published);
        Assert.Equal(2, figures.DraftOrIncomplete);
        Assert.Equal(2, figures.LearnerLessons);
    }

    [Fact]
    public async Task GetEditor_ReturnsTheInfoLinesQuizAndVocabInOrder_WithTheChecklist()
    {
        var lesson = await AddLessonAsync("Đủ", youtube: Id, lines: 3, timed: 3, quiz: 2, vocab: 1);
        lesson.Duration = "04:12";
        lesson.Speaker1Name = "Anna";
        await _db.SaveChangesAsync();

        var editor = await _service.GetEditorAsync(lesson.Id);

        Assert.Equal("Đủ", editor!.Info.Title);
        Assert.Equal("B1", editor.Info.Level);
        Assert.Equal(Id, editor.Info.YoutubeId);
        Assert.Equal("04:12", editor.Info.Duration);
        Assert.Equal("Anna", editor.Info.Speaker1Name);
        Assert.Equal(3, editor.Lines.Count);
        Assert.Equal("Dòng 1", editor.Lines[0].Text);
        Assert.Equal(2, editor.Questions.Count);
        Assert.Single(editor.Items);
        Assert.True(editor.HasTranscript && editor.HasTimedLines && editor.HasQuiz && editor.HasVocab);
        Assert.False(editor.IsPublished);

        var bare = await _service.GetEditorAsync((await AddLessonAsync("Trống")).Id);
        Assert.False(bare!.HasTranscript || bare.HasTimedLines || bare.HasQuiz || bare.HasVocab);
        Assert.True(AdminLessonEditor.NewLesson().IsNew);
    }

    // ------------------------------------------------------------------ fake provider

    private sealed class FakeTranscripts : IYoutubeTranscriptService
    {
        public List<SpeakingSentence> Sentences { get; set; } = new();
        public Exception? Failure { get; set; }
        public string? Duration { get; set; }
        public Task? Gate { get; set; }
        public int Calls { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<List<SpeakingSentence>> GetTranscriptAsync(string youtubeId)
        {
            Calls++;
            Started.TrySetResult();
            if (Gate != null)
            {
                await Gate;
            }

            if (Failure != null)
            {
                throw Failure;
            }

            return Sentences.Select(s => new SpeakingSentence { Text = s.Text, StartTime = s.StartTime, EndTime = s.EndTime }).ToList();
        }

        public Task<string?> GetVideoDurationAsync(string youtubeId) => Task.FromResult(Duration);
        public Task<YoutubeTranscriptResult> GetTranscriptForSpeakingImportAsync(string youtubeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<YoutubeTranscriptResult> GetEnglishCaptionsOnlyAsync(string youtubeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<YoutubeVideoMetadata?> GetVideoMetadataAsync(string youtubeId) => throw new NotSupportedException();
    }
}
