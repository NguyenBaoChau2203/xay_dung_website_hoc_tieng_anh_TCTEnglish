using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class LearningAreaProgressRetriever : IKnowledgeRetriever
{
    private readonly DbflashcardContext _context;

    public LearningAreaProgressRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent)
        => intent == UserIntent.MyProgress || intent == UserIntent.WebsiteGuide;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        var normalizedMessage = AiTextNormalizer.Normalize(userMessage);
        if (!IsLearningAreaProgressQuery(normalizedMessage))
        {
            return [];
        }

        var today = DateTime.UtcNow.Date;
        var todayActivity = await _context.UserDailyActivities
            .AsNoTracking()
            .Where(activity => activity.UserId == userId && activity.ActivityDate == today)
            .Select(activity => new ActivitySnapshot
            {
                XpEarned = activity.XpEarned,
                CardsReviewed = activity.CardsReviewed,
                SpeakingCompletedCount = activity.SpeakingCompletedCount,
                WritingCompletedCount = activity.WritingCompletedCount,
                ReadingCompletedCount = activity.ReadingCompletedCount,
                ListeningCompletedCount = activity.ListeningCompletedCount
            })
            .FirstOrDefaultAsync(ct) ?? new ActivitySnapshot();

        var readingCompleted = await _context.UserReadingHistories
            .AsNoTracking()
            .Where(history => history.UserId == userId && history.IsCompleted)
            .CountAsync(ct);

        var writingCompleted = await _context.UserWritingExerciseProgresses
            .AsNoTracking()
            .Where(progress => progress.UserId == userId && progress.IsCompleted)
            .CountAsync(ct);

        var listeningCompleted = await _context.UserListeningProgresses
            .AsNoTracking()
            .Where(progress => progress.UserId == userId && progress.CompletedAt != null)
            .CountAsync(ct);

        var speakingCompleted = await _context.UserSpeakingVideoCompletions
            .AsNoTracking()
            .Where(progress => progress.UserId == userId && progress.IsCompleted)
            .CountAsync(ct);

        var availableReading = await _context.ReadingPassages
            .AsNoTracking()
            .Where(passage => passage.IsPublished)
            .CountAsync(ct);

        var availableWriting = await _context.WritingExercises
            .AsNoTracking()
            .Where(exercise => exercise.IsPublished || exercise.UserId == userId)
            .CountAsync(ct);

        var availableListening = await _context.ListeningLessons
            .AsNoTracking()
            .Where(lesson => lesson.IsPublished || lesson.OwnerUserId == userId)
            .CountAsync(ct);

        var availableSpeaking = await _context.SpeakingVideos
            .AsNoTracking()
            .CountAsync(ct);

        return
        [
            new(
                "learning-area-summary",
                string.Join(
                    '|',
                    $"todayXp={todayActivity.XpEarned}",
                    $"todayVocabulary={todayActivity.CardsReviewed}",
                    $"todaySpeaking={todayActivity.SpeakingCompletedCount}",
                    $"todayWriting={todayActivity.WritingCompletedCount}",
                    $"todayReading={todayActivity.ReadingCompletedCount}",
                    $"todayListening={todayActivity.ListeningCompletedCount}",
                    $"readingCompleted={readingCompleted}",
                    $"writingCompleted={writingCompleted}",
                    $"listeningCompleted={listeningCompleted}",
                    $"speakingCompleted={speakingCompleted}",
                    $"availableReading={availableReading}",
                    $"availableWriting={availableWriting}",
                    $"availableListening={availableListening}",
                    $"availableSpeaking={availableSpeaking}"),
                KnowledgeSnippetSources.LearningAreaSummary,
                "/Home/Index")
        ];
    }

    private static bool IsLearningAreaProgressQuery(string normalizedMessage)
    {
        var hasLearningAreaKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "reading",
            "writing",
            "listening",
            "speaking",
            "course",
            "courses",
            "ky nang",
            "hoc phan");

        if (!hasLearningAreaKeyword)
        {
            return false;
        }

        if (AiTextNormalizer.ContainsAny(normalizedMessage, "cach", "huong dan", "tinh nang", "hoat dong", "o dau"))
        {
            return false;
        }

        return AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "cua toi",
            "tien do",
            "hom nay",
            "da hoan thanh",
            "ket qua",
            "bao nhieu",
            "hoc den dau",
            "xem");
    }

    private sealed class ActivitySnapshot
    {
        public int XpEarned { get; set; }

        public int CardsReviewed { get; set; }

        public int SpeakingCompletedCount { get; set; }

        public int WritingCompletedCount { get; set; }

        public int ReadingCompletedCount { get; set; }

        public int ListeningCompletedCount { get; set; }
    }
}
