using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Internal.Retrievers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiBaselineRegressionTests
{
    private const int PrimaryUserId = 101;
    private const int SecondaryUserId = 102;
    private const int JoinedClassUserId = 103;
    private const int EmptyUserId = 104;

    [Fact]
    public async Task GenerateReplyAsync_Greeting_ReturnsGreetingTemplate()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_Greeting_ReturnsGreetingTemplate));
        await SeedUsersAsync(context);

        var reply = await SendAsync(context, PrimaryUserId, "xin chao");

        Assert.Contains("TCT English", reply.Text);
        Assert.Contains("bộ từ vựng", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_MyVocabulary_ReturnsOwnedSetSummary()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_MyVocabulary_ReturnsOwnedSetSummary));
        await SeedUsersAsync(context);
        await SeedVocabularyAsync(context);

        var reply = await SendAsync(context, PrimaryUserId, "toi co nhung bo tu vung nao");

        Assert.Contains("Business Core", reply.Text);
        Assert.Contains("Travel Pack", reply.Text);
        Assert.DoesNotContain("Outsider Set", reply.Text);
    }

    [Fact]
    public async Task GenerateReplyAsync_MyProgress_ReturnsProgressSummary()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_MyProgress_ReturnsProgressSummary));
        await SeedUsersAsync(context, primaryUserGoal: 5, primaryUserStreak: 3, lastStudyDateUtc: DateTime.UtcNow.Date);
        await SeedVocabularyAsync(context);
        await SeedLearningProgressAsync(context, includePrimaryUserProgress: true);

        var reply = await SendAsync(context, PrimaryUserId, "tien do hoc cua toi ra sao");

        Assert.Contains("**1**", reply.Text);
        Assert.Contains("**3 ngày**", reply.Text);
        Assert.Contains("còn thiếu 3 thẻ", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_CardLookup_ReturnsMatchingOwnedCard()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_CardLookup_ReturnsMatchingOwnedCard));
        await SeedUsersAsync(context);
        await SeedVocabularyAsync(context);

        var reply = await SendAsync(context, PrimaryUserId, "forecast nghia la gi");

        Assert.Contains("forecast", reply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("predict demand", reply.Text);
        Assert.DoesNotContain("outsider only", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_SpeakingSuggestion_ReturnsSuggestedVideo()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_SpeakingSuggestion_ReturnsSuggestedVideo));
        await SeedUsersAsync(context);
        await SeedSpeakingAsync(context, includeVideos: true);

        var reply = await SendAsync(context, PrimaryUserId, "goi y bai speaking meeting phu hop");

        Assert.Contains("Team Meeting Basics", reply.Text);
        Assert.Contains("Meeting", reply.Text);
    }

    [Fact]
    public async Task GenerateReplyAsync_ClassInfo_ReturnsOwnedAndJoinedClasses()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_ClassInfo_ReturnsOwnedAndJoinedClasses));
        await SeedUsersAsync(context);
        await SeedClassDataAsync(context, includeJoinedClass: true);

        var reply = await SendAsync(context, PrimaryUserId, "lop hoc cua toi");

        Assert.Contains("Sprint One Class", reply.Text);
        Assert.Contains("Speaking Club", reply.Text);
        Assert.Contains("Chủ lớp", reply.Text);
    }

    [Fact]
    public async Task GenerateReplyAsync_WebsiteGuide_ReturnsGuideRoute()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_WebsiteGuide_ReturnsGuideRoute));
        await SeedUsersAsync(context);

        var reply = await SendAsync(context, PrimaryUserId, "cach tao lop hoc");

        Assert.Contains("/Home/CreateClass", reply.Text);
        Assert.Contains("Tạo lớp", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_StudyRecommendation_ReturnsRecommendation()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_StudyRecommendation_ReturnsRecommendation));
        await SeedUsersAsync(context, primaryUserGoal: 5, primaryUserStreak: 4, lastStudyDateUtc: DateTime.UtcNow.Date);
        await SeedVocabularyAsync(context);

        var travelPack = await context.Sets.FirstAsync(set => set.SetId == 202);
        travelPack.CreatedAt = DateTime.UtcNow.AddMinutes(5);
        await context.SaveChangesAsync();

        await SeedLearningProgressAsync(context, includePrimaryUserProgress: true);

        var reply = await SendAsync(context, PrimaryUserId, "toi nen hoc gi tiep theo");

        Assert.Contains("Business Core", reply.Text);
        Assert.DoesNotContain("Travel Pack", reply.Text);
        Assert.Contains("Flashcard", reply.Text);
        Assert.Contains("Quiz", reply.Text);
    }

    [Fact]
    public async Task GenerateReplyAsync_StudyRecommendation_WithOwnedSetsButNoLearningProgress_ReturnsRecommendation()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_StudyRecommendation_WithOwnedSetsButNoLearningProgress_ReturnsRecommendation));
        await SeedUsersAsync(context, primaryUserGoal: 5, primaryUserStreak: 2);
        await SeedVocabularyAsync(context);

        var reply = await SendAsync(context, PrimaryUserId, "toi nen hoc gi tiep");

        Assert.Contains("Business Core", reply.Text);
        Assert.Contains("còn 3 thẻ", reply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Flashcard", reply.Text);
        Assert.Contains("Quiz", reply.Text);
        Assert.DoesNotContain("Outsider Set", reply.Text);
    }

    [Fact]
    public async Task GenerateReplyAsync_OutOfScope_ReturnsRefusal()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_OutOfScope_ReturnsRefusal));

        var reply = await SendAsync(context, PrimaryUserId, "giai thich ngu phap hien tai hoan thanh");

        Assert.Contains("TCT English", reply.Text);
        Assert.Contains("speaking", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_MyVocabulary_WhenNoSets_ReturnsEmptyState()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_MyVocabulary_WhenNoSets_ReturnsEmptyState));
        await SeedUsersAsync(context);

        var reply = await SendAsync(context, EmptyUserId, "toi co nhung bo tu vung nao");

        Assert.Contains("chưa tìm thấy bộ từ vựng", reply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tạo bộ từ mới", reply.Text);
    }

    [Fact]
    public async Task GenerateReplyAsync_MyProgress_WhenNoLearningData_ReturnsEmptyState()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_MyProgress_WhenNoLearningData_ReturnsEmptyState));
        await SeedUsersAsync(context);

        var reply = await SendAsync(context, EmptyUserId, "tien do hoc cua toi");

        Assert.Contains("chưa tìm thấy dữ liệu tiến độ", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_CardLookup_WhenCardMissing_ReturnsEmptyState()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_CardLookup_WhenCardMissing_ReturnsEmptyState));
        await SeedUsersAsync(context);
        await SeedVocabularyAsync(context);

        var reply = await SendAsync(context, PrimaryUserId, "policy nghia la gi");

        Assert.Contains("không tìm thấy từ", reply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("policy", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_SpeakingSuggestion_WhenNoVideos_ReturnsEmptyState()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_SpeakingSuggestion_WhenNoVideos_ReturnsEmptyState));
        await SeedUsersAsync(context);

        var reply = await SendAsync(context, PrimaryUserId, "goi y bai speaking phu hop");

        Assert.Contains("chưa tìm thấy bài speaking phù hợp", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_ClassInfo_WhenNoMemberships_ReturnsEmptyState()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_ClassInfo_WhenNoMemberships_ReturnsEmptyState));
        await SeedUsersAsync(context);

        var reply = await SendAsync(context, EmptyUserId, "lop hoc cua toi");

        Assert.Contains("chưa tham gia lớp học nào", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_StudyRecommendation_WhenNoSets_ReturnsEmptyState()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_StudyRecommendation_WhenNoSets_ReturnsEmptyState));
        await SeedUsersAsync(context);

        var reply = await SendAsync(context, EmptyUserId, "toi nen hoc gi tiep");

        Assert.Contains("chưa có đủ dữ liệu học", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_StudyRecommendation_DoesNotLeakAnotherUsersSetsCardsOrProgress()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_StudyRecommendation_DoesNotLeakAnotherUsersSetsCardsOrProgress));
        await SeedUsersAsync(context);

        context.Sets.Add(new Set
        {
            SetId = 211,
            OwnerId = SecondaryUserId,
            SetName = "Outsider Study Plan",
            CreatedAt = DateTime.UtcNow
        });

        context.Cards.Add(new Card
        {
            CardId = 311,
            SetId = 211,
            Term = "outsider-term",
            Definition = "outsider definition"
        });

        context.LearningProgresses.Add(new LearningProgress
        {
            ProgressId = 411,
            UserId = SecondaryUserId,
            CardId = 311,
            Status = "Learning",
            LastReviewedDate = DateTime.UtcNow
        });

        context.LearningProgresses.Add(new LearningProgress
        {
            ProgressId = 412,
            UserId = PrimaryUserId,
            CardId = 311,
            Status = "Learning",
            LastReviewedDate = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var reply = await SendAsync(context, PrimaryUserId, "toi nen hoc gi tiep");

        Assert.Contains("chưa có đủ dữ liệu học", reply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Outsider Study Plan", reply.Text);
        Assert.DoesNotContain("outsider-term", reply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outsider definition", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_CardLookup_DoesNotLeakAnotherUsersCard()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_CardLookup_DoesNotLeakAnotherUsersCard));
        await SeedUsersAsync(context);
        await SeedVocabularyAsync(context);

        var reply = await SendAsync(context, PrimaryUserId, "invoice nghia la gi");

        Assert.Contains("không tìm thấy từ", reply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outsider only", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateReplyAsync_ClassInfo_DoesNotLeakAnotherUsersClass()
    {
        await using var context = CreateContext(nameof(GenerateReplyAsync_ClassInfo_DoesNotLeakAnotherUsersClass));
        await SeedUsersAsync(context);
        await SeedClassDataAsync(context, includeJoinedClass: true, includeOutsiderOnlyClass: true);

        var reply = await SendAsync(context, PrimaryUserId, "toi dang tham gia lop nao");

        Assert.DoesNotContain("Private Outsider Class", reply.Text);
        Assert.Contains("Sprint One Class", reply.Text);
    }

    private static DbflashcardContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new DbflashcardContext(options);
    }

    private static async Task<AiProviderReply> SendAsync(DbflashcardContext context, int userId, string message)
    {
        var provider = new InternalKnowledgeProvider(
            new DeterministicIntentClassifier(),
            [
                new WebsiteGuideRetriever(GetGuideFilePath()),
                new UserVocabularyRetriever(context),
                new LearningProgressRetriever(context),
                new StudyRecommendationRetriever(context),
                new CardLookupRetriever(context),
                new SpeakingRetriever(context),
                new ClassRetriever(context)
            ],
            new TemplateAnswerComposer());

        return await provider.GenerateReplyAsync(
            userId,
            [new AiContextMessage("user", message)],
            CancellationToken.None);
    }

    private static async Task SeedUsersAsync(
        DbflashcardContext context,
        int primaryUserGoal = 0,
        int primaryUserStreak = 0,
        DateTime? lastStudyDateUtc = null)
    {
        context.Users.AddRange(
            new User
            {
                UserId = PrimaryUserId,
                Email = "primary@test.local",
                PasswordHash = "hash",
                FullName = "Primary User",
                Role = Roles.Standard,
                Goal = primaryUserGoal,
                Streak = primaryUserStreak,
                LastStudyDate = lastStudyDateUtc
            },
            new User
            {
                UserId = SecondaryUserId,
                Email = "secondary@test.local",
                PasswordHash = "hash",
                FullName = "Secondary User",
                Role = Roles.Standard
            },
            new User
            {
                UserId = JoinedClassUserId,
                Email = "joined@test.local",
                PasswordHash = "hash",
                FullName = "Joined User",
                Role = Roles.Standard
            },
            new User
            {
                UserId = EmptyUserId,
                Email = "empty@test.local",
                PasswordHash = "hash",
                FullName = "Empty User",
                Role = Roles.Standard
            });

        await context.SaveChangesAsync();
    }

    private static async Task SeedVocabularyAsync(DbflashcardContext context)
    {
        context.Sets.AddRange(
            new Set
            {
                SetId = 201,
                OwnerId = PrimaryUserId,
                SetName = "Business Core",
                CreatedAt = DateTime.UtcNow
            },
            new Set
            {
                SetId = 202,
                OwnerId = PrimaryUserId,
                SetName = "Travel Pack",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new Set
            {
                SetId = 203,
                OwnerId = SecondaryUserId,
                SetName = "Outsider Set",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            });

        context.Cards.AddRange(
            new Card
            {
                CardId = 301,
                SetId = 201,
                Term = "forecast",
                Definition = "predict demand"
            },
            new Card
            {
                CardId = 302,
                SetId = 201,
                Term = "budget",
                Definition = "money plan"
            },
            new Card
            {
                CardId = 303,
                SetId = 201,
                Term = "meeting",
                Definition = "team discussion"
            },
            new Card
            {
                CardId = 304,
                SetId = 202,
                Term = "boarding pass",
                Definition = "travel ticket"
            },
            new Card
            {
                CardId = 305,
                SetId = 203,
                Term = "forecast",
                Definition = "outsider only"
            },
            new Card
            {
                CardId = 306,
                SetId = 203,
                Term = "invoice",
                Definition = "outsider only"
            });

        await context.SaveChangesAsync();
    }

    private static async Task SeedLearningProgressAsync(DbflashcardContext context, bool includePrimaryUserProgress)
    {
        if (!includePrimaryUserProgress)
        {
            return;
        }

        context.LearningProgresses.AddRange(
            new LearningProgress
            {
                ProgressId = 401,
                UserId = PrimaryUserId,
                CardId = 301,
                Status = "Mastered",
                LastReviewedDate = DateTime.UtcNow
            },
            new LearningProgress
            {
                ProgressId = 402,
                UserId = PrimaryUserId,
                CardId = 302,
                Status = "Learning",
                LastReviewedDate = DateTime.UtcNow.AddMinutes(-10)
            },
            new LearningProgress
            {
                ProgressId = 403,
                UserId = PrimaryUserId,
                CardId = 303,
                Status = "New",
                LastReviewedDate = DateTime.UtcNow.AddMinutes(-20)
            });

        context.UserDailyActivities.Add(new UserDailyActivity
        {
            UserId = PrimaryUserId,
            ActivityDate = DateTime.UtcNow.Date,
            CardsReviewed = 2
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedClassDataAsync(
        DbflashcardContext context,
        bool includeJoinedClass,
        bool includeOutsiderOnlyClass = false)
    {
        context.Classes.Add(new Class
        {
            ClassId = 501,
            ClassName = "Sprint One Class",
            OwnerId = PrimaryUserId,
            CreatedAt = DateTime.UtcNow
        });

        if (includeJoinedClass)
        {
            context.Classes.Add(new Class
            {
                ClassId = 502,
                ClassName = "Speaking Club",
                OwnerId = SecondaryUserId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            });

            context.ClassMembers.Add(new ClassMember
            {
                ClassId = 502,
                UserId = PrimaryUserId
            });
        }

        if (includeOutsiderOnlyClass)
        {
            context.Classes.Add(new Class
            {
                ClassId = 503,
                ClassName = "Private Outsider Class",
                OwnerId = SecondaryUserId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-4)
            });

            context.ClassMembers.Add(new ClassMember
            {
                ClassId = 503,
                UserId = JoinedClassUserId
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedSpeakingAsync(DbflashcardContext context, bool includeVideos)
    {
        if (!includeVideos)
        {
            return;
        }

        context.SpeakingPlaylists.AddRange(
            new SpeakingPlaylist
            {
                Id = 601,
                Name = "Office English"
            },
            new SpeakingPlaylist
            {
                Id = 602,
                Name = "Meetings"
            });

        context.SpeakingVideos.AddRange(
            new SpeakingVideo
            {
                Id = 701,
                PlaylistId = 601,
                Title = "Office Introductions",
                YoutubeId = "office-intro",
                Level = "A1",
                Topic = "Office"
            },
            new SpeakingVideo
            {
                Id = 702,
                PlaylistId = 602,
                Title = "Team Meeting Basics",
                YoutubeId = "team-meeting",
                Level = "A2",
                Topic = "Meeting"
            });

        context.SpeakingSentences.AddRange(
            new SpeakingSentence
            {
                Id = 801,
                VideoId = 701,
                Text = "Welcome to the office.",
                VietnameseMeaning = "Chào mừng đến văn phòng.",
                StartTime = 0,
                EndTime = 3
            },
            new SpeakingSentence
            {
                Id = 802,
                VideoId = 702,
                Text = "Let's start the meeting.",
                VietnameseMeaning = "Hãy bắt đầu cuộc họp.",
                StartTime = 0,
                EndTime = 4
            });

        context.UserSpeakingProgresses.Add(new UserSpeakingProgress
        {
            Id = 901,
            UserId = PrimaryUserId,
            SentenceId = 801,
            TotalScore = 92,
            AccuracyScore = 92,
            FluencyScore = 92,
            CompletenessScore = 92,
            PracticedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static string GetGuideFilePath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "TCTEnglish",
            "wwwroot",
            "data",
            "ai",
            "website-guides.json"));
    }
}
